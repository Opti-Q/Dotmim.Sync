using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dotmim.Sync.Batch;
using Dotmim.Sync.Enumerations;
using Dotmim.Sync.Filter;
using Dotmim.Sync.Messages;

namespace Dotmim.Sync
{
    /// <summary>
/// Provides an efficient sync implementation that processes sync steps in fewer round trips
/// </summary>
public class EfficientSyncAgent : SyncAgent, IDisposable
{
    /// <summary>
    /// Creates a new instance of EfficientSyncAgent
    /// </summary>
    public EfficientSyncAgent(string scopeName, IProvider localProvider, IEfficientProvider remoteProvider)
        : base(scopeName, localProvider, remoteProvider)
    {
    }
    
    /// <summary>
    /// SyncAgent used in a web proxy sync session. No need to set tables, it's done from the server web api side.
    /// </summary>
    public EfficientSyncAgent(IProvider localProvider, IProvider remoteProvider)
        : base("DefaultScope", localProvider, remoteProvider)
    {
    }

    /// <summary>
    /// SyncAgent manage both server and client provider
    /// the tables array represents the tables you want to sync
    /// Don't work on the proxy provider
    /// </summary>
    public EfficientSyncAgent(string scopeName, IProvider clientProvider, IProvider serverProvider, string[] tables)
        : base(scopeName, clientProvider, serverProvider, tables)
    {
    }

    /// <summary>
    /// SyncAgent manage both server and client provider
    /// the tables array represents the tables you want to sync
    /// Don't work on the proxy provider
    /// </summary>
    public EfficientSyncAgent(IProvider clientProvider, IProvider serverProvider, string[] tables)
        : base("DefaultScope", clientProvider, serverProvider, tables)
    {
    }
    private IEfficientProvider remoteProvider => (IEfficientProvider)RemoteProvider;
    
    /// <summary>
    /// Performs a complete sync operation efficiently
    /// </summary>
    public override async Task<SyncContext> SynchronizeAsync(SyncType syncType, CancellationToken cancellationToken)
    {
        // Context, used to back and forth data between servers
        var context = new SyncContext(Guid.NewGuid())
        {
            // set start time
            StartTime = DateTime.Now,

            // if any parameters, set in context
            Parameters = this.Parameters,

            // set sync type (Normal, Reinitialize, ReinitializeWithUpload)
            SyncType = syncType,

            // set the scopename
            ScopeName = this.Configuration.ScopeName
        };
        
        LocalProvider.SetCancellationToken(cancellationToken);
        remoteProvider.SetCancellationToken(cancellationToken);

        try
        {
            // Locally, nothing really special. Eventually, editing the config object
            (context, this.Configuration) = await this.LocalProvider.BeginSessionAsync(context,
                new MessageBeginSession { SyncConfiguration = this.Configuration });

            // Get client scope
            var (c, scopes) = await this.LocalProvider.EnsureScopesAsync(context,
                new MessageEnsureScopes
                {
                    ScopeInfoTableName = Configuration.ScopeInfoTableName,
                    ScopeName = Configuration.ScopeName,
                    SerializationFormat = SerializationFormat.Json
                });
            context = c;
            var localScope = scopes.First();
            
             // Apply on local Provider
            (context, this.Configuration.Schema) = await this.LocalProvider.EnsureSchemaAsync(context,
                new MessageEnsureSchema
                {
                    Schema = this.Configuration.Schema,
                    SerializationFormat = this.Configuration.SerializationFormat
                });

            // Ensure database is ready
            context = await this.LocalProvider.EnsureDatabaseAsync(context,
                new MessageEnsureDatabase
                {
                    ScopeInfo = localScope,
                    Schema = Configuration.Schema,
                    Filters = Configuration.Filters,
                    SerializationFormat = SerializationFormat.Json
                });

            // Get initial timestamp
            var (_, clientTimestamp) = await this.LocalProvider.GetLocalTimestampAsync(context,
                new MessageTimestamp
                {
                    ScopeInfoTableName = Configuration.ScopeInfoTableName,
                    SerializationFormat = SerializationFormat.Json
                });

            // Get all client changes
            var (_, clientChanges, _) = await this.LocalProvider.GetChangeBatchAsync(context,
                new MessageGetChangesBatch
                {
                    ScopeInfo = localScope,
                    Schema = Configuration.Schema,
                    BatchDirectory = Configuration.BatchDirectory,
                    DownloadBatchSizeInKB = Configuration.DownloadBatchSizeInKB,
                    SerializationFormat = SerializationFormat.Json
                });

            // Process client changes in batches
            int batchIndex = 0;
            MessageEfficientSyncResponse finalResponse = null;
            long? serverTimeStamp = null;
            bool isFirstResponse = true;

            foreach (var batchPart in clientChanges.BatchPartsInfo)
            {
                var efficientMessage = new MessageEfficientSync
                {
                    Configuration = Configuration,
                    ClientScope = localScope,
                    BatchPartInfo = new BatchInfo 
                    { 
                        BatchPartsInfo = new List<BatchPartInfo> { batchPart },
                        Directory = clientChanges.Directory,
                        InMemory = clientChanges.InMemory
                    },
                    BatchIndex = batchIndex++,
                    IsFinalBatch = batchPart.IsLastBatch,
                    Parameters = new SyncParameterCollection(),
                    SyncType = SyncType.Normal,
                    SerializationFormat = SerializationFormat.Json
                };

                var response = await remoteProvider.ProcessEfficientSyncAsync(context, efficientMessage);

                if (isFirstResponse)
                {
                    isFirstResponse = false;

                    if (response.Schema != null)
                    {
                        var schema = response.Schema.ConvertToDmSet();
                        response.Schema.Dispose();
                        
                        // Apply on local Provider
                        (context, _) = await this.LocalProvider.EnsureSchemaAsync(context,
                            new MessageEnsureSchema
                            {
                                Schema = schema,
                                SerializationFormat = this.Configuration.SerializationFormat
                            });

                        // Ensure database is ready
                        context = await this.LocalProvider.EnsureDatabaseAsync(context,
                            new MessageEnsureDatabase
                            {
                                ScopeInfo = localScope,
                                Schema = schema,
                                Filters = Configuration.Filters,
                                SerializationFormat = SerializationFormat.Json
                            });
                    }
                }
                

                // Keep track of final response
                if (batchPart.IsLastBatch)
                    finalResponse = response;

                // Apply any server changes received
                if (response.BatchPartInfo != null)
                {
                   if (serverTimeStamp is null)
                        serverTimeStamp = response.ServerTimestamp;
                    
                    await this.LocalProvider.ApplyChangesAsync(context, new MessageApplyChanges
                    {
                        Changes = response.BatchPartInfo,
                        FromScope = response.ServerScope,
                        Schema = response.Configuration.Schema,
                        ScopeInfoTableName = Configuration.ScopeInfoTableName,
                        SerializationFormat = SerializationFormat.Json
                    });
                }
            }

            // Update local scope info after all changes are processed
            if (finalResponse != null)
            {
                var serverScope = finalResponse.ServerScope;
                var serverTimestamp = finalResponse.ServerTimestamp;

                // Update scope information
                serverScope.IsNewScope = false;
                serverScope.LastSync = DateTime.Now;
                serverScope.LastSyncTimestamp = serverTimestamp;
                serverScope.LastSyncDuration = DateTime.Now.Subtract(context.StartTime).Ticks;
                serverScope.IsLocal = false;

                localScope.IsNewScope = false;
                localScope.LastSync = DateTime.Now;
                localScope.LastSyncTimestamp = clientTimestamp;
                localScope.LastSyncDuration = DateTime.Now.Subtract(context.StartTime).Ticks;
                localScope.IsLocal = true;

                await this.LocalProvider.WriteScopesAsync(context, new MessageWriteScopes
                {
                    ScopeInfoTableName = Configuration.ScopeInfoTableName,
                    Scopes = new List<ScopeInfo> { localScope, serverScope },
                    SerializationFormat = SerializationFormat.Json
                });
            }

            // End session
            context = await this.LocalProvider.EndSessionAsync(context);

            return context;
        }
        catch (Exception ex)
        {
            throw new SyncException(ex, SyncStage.None, this.GetType().Name);
        }
    }

    protected override void Dispose(bool disposing)
    {
        (LocalProvider as IDisposable)?.Dispose();
        (RemoteProvider as IDisposable)?.Dispose();

        base.Dispose(disposing);
    }
}
}