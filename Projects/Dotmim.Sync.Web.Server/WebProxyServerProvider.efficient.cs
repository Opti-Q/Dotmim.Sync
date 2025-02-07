using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dotmim.Sync.Batch;
using Dotmim.Sync.Data;
using Dotmim.Sync.Data.Surrogate;
using Dotmim.Sync.Enumerations;
using Dotmim.Sync.Messages;
using Dotmim.Sync.Web.Client;
using Newtonsoft.Json.Linq;

namespace Dotmim.Sync.Web.Server
{
    public partial class WebProxyServerProvider
    {
        /// <summary>
        /// Handles an efficient sync request on the server side
        /// </summary>
        public async Task<HttpMessage> HandleEfficientSyncAsync(
            HttpMessage httpRequest)
        {
            MessageEfficientSync request;
            if (httpRequest.Content is HttpMessageApplyChanges)
                request = httpRequest.Content as MessageEfficientSync;
            else
                request = (httpRequest.Content as JObject).ToObject<MessageEfficientSync>();

            var context = httpRequest.SyncContext;
            var response = new MessageEfficientSyncResponse();
            var httpResponse = new HttpMessage
            {
                SyncContext = context,
                Content = response
            };
            // If the Conf is hosted by the server, we try to get the tables from it, overriding the client schema, if passed
            DmSet schema = null;
            if (this.Configuration.Schema != null)
                schema = this.Configuration.Schema;
            else if (request.Schema != null)
                schema = request.Schema.ConvertToDmSet();

            try
            {
                // Determine conflict resolution policies
                ConflictResolutionPolicy serverPolicy = request.Configuration.ConflictResolutionPolicy;
                ConflictResolutionPolicy clientPolicy = serverPolicy == ConflictResolutionPolicy.ServerWins
                    ? ConflictResolutionPolicy.ClientWins
                    : ConflictResolutionPolicy.ServerWins;
                // Only process begin session and ensure scopes on first batch
                if (request.BatchIndex == 0)
                {
                    // Process begin session
                    (context, response.Configuration) = await this.LocalProvider.BeginSessionAsync(context,
                        new MessageBeginSession { SyncConfiguration = request.Configuration });

                    // Ensure scopes
                    var (_, scopes) = await this.LocalProvider.EnsureScopesAsync(context,
                        new MessageEnsureScopes
                        {
                            ClientReferenceId = request.ClientScope.Id,
                            ScopeInfoTableName = request.Configuration.ScopeInfoTableName,
                            ScopeName = request.Configuration.ScopeName,
                            SerializationFormat = request.SerializationFormat
                        });

                    response.ServerScope = scopes.First(s => s.IsLocal);


                    (context, schema) = await this.LocalProvider.EnsureSchemaAsync(context,
                        new MessageEnsureSchema
                        {
                            Schema = schema,
                            SerializationFormat = request.SerializationFormat
                        });
                    response.Schema = new DmSetSurrogate(schema);

                    if (request.Schema != null)
                    {
                        request.Schema.Dispose();
                        request.Schema = null;
                    }

                    // Ensure database on server
                    context = await this.LocalProvider.EnsureDatabaseAsync(context,
                        new MessageEnsureDatabase
                        {
                            ScopeInfo = response.ServerScope,
                            Schema = schema,
                            Filters = request.Configuration.Filters,
                            SerializationFormat = request.SerializationFormat
                        });
                }

                // Apply current batch of client changes
                if (request.BatchPartInfo != null)
                {
                    (context, response.ServerChangesApplied) = await this.LocalProvider.ApplyChangesAsync(context,
                        new MessageApplyChanges
                        {
                            Changes = request.BatchPartInfo,
                            FromScope = request.ClientScope,
                            Schema = schema,
                            ScopeInfoTableName = request.Configuration.ScopeInfoTableName,
                            Policy = clientPolicy,
                            SerializationFormat = request.SerializationFormat
                        });
                }

                // Get next batch of server changes only if we've processed all client changes
                if (request.IsFinalBatch)
                {
                    (context, response.ServerTimestamp) = await this.LocalProvider.GetLocalTimestampAsync(context,
                        new MessageTimestamp
                        {
                            ScopeInfoTableName = request.Configuration.ScopeInfoTableName,
                            SerializationFormat = request.SerializationFormat
                        });

                    // Get server changes
                    (context, response.BatchPartInfo, response.ServerChangesSelected) =
                        await this.LocalProvider.GetChangeBatchAsync(context,
                            new MessageGetChangesBatch
                            {
                                ScopeInfo = request.ClientScope,
                                Schema = schema,
                                BatchDirectory = request.Configuration.BatchDirectory,
                                DownloadBatchSizeInKB = request.Configuration.DownloadBatchSizeInKB,
                                SerializationFormat = request.SerializationFormat
                            });

                    response.IsServerFinalBatch =
                        response.BatchPartInfo?.BatchPartsInfo.Last().IsLastBatch ?? true;
                }

                // End session if this is the last response
                if (request.IsFinalBatch && response.IsServerFinalBatch)
                {
                    context = await this.LocalProvider.WriteScopesAsync(context,
                        new MessageWriteScopes
                        {
                            ScopeInfoTableName = this.Configuration.ScopeInfoTableName,
                            Scopes = new List<ScopeInfo> { request.ClientScope, response.ServerScope },
                            SerializationFormat = this.Configuration.SerializationFormat
                        });

                    context = await this.LocalProvider.EndSessionAsync(context);
                }

                schema?.Clear();
                schema = null;

                return httpResponse;
            }
            catch (Exception ex)
            {
                throw new SyncException(ex, context.SyncStage, this.GetType().Name);
            }
        }
        
    private async Task<MessageEfficientSyncResponse> GetBatches(MessageEfficientSync request, MessageEfficientSyncResponse response, HttpMessage responseMessage)
    {
         void CleanUp(BatchInfo bi)
            {
                if (response.BatchPartInfo.IsLastBatch)
                {
                    this.LocalProvider.CacheManager.Remove("GetChangeBatch_BatchInfo");
                    this.LocalProvider.CacheManager.Remove("GetChangeBatch_ChangesSelected");
                    // clean up batch directory
                    if (Configuration.BatchDirectory != null && bi.Directory != null)
                    {
                        var dir = Path.Combine(Configuration.BatchDirectory, bi.Directory);
                        if (Directory.Exists(dir))
                            Directory.Delete(dir, true);
                    }
                }
            }

            // if we get the first batch info request, made it.
            // Server is able to define if it's in memory or not
            if (request.BatchIndexRequested == 0)
            {
                var (syncContext, bi, changesSelected) = await this.GetChangeBatchAsync(
                    httpMessage.SyncContext,
                    new MessageGetChangesBatch
                    {
                        ScopeInfo = scopeInfo,
                        Schema = response.Schema.ConvertToDmSet(),
                        DownloadBatchSizeInKB = response.DownloadBatchSizeInKB,
                        BatchDirectory = response.BatchDirectory,
                        Policy = response.Policy,
                        Filters = response.Filters
                    });

                // Select the first bpi needed (index == 0)
                if (bi.BatchPartsInfo.Count > 0)
                    response.BatchPartInfo = bi.BatchPartsInfo.First(bpi => bpi.Index == 0);

                response.InMemory = bi.InMemory;
                response.ChangesSelected = changesSelected;

                // if no changes, return
                if (response.BatchPartInfo == null)
                    return httpMessage;

                // if we are not in memory, we set the BI in session, to be able to get it back on next request
                if (!bi.InMemory)
                {
                    // Save the BatchInfo
                    this.LocalProvider.CacheManager.Set("GetChangeBatch_BatchInfo", bi);
                    this.LocalProvider.CacheManager.Set("GetChangeBatch_ChangesSelected", changesSelected);

                    // load the batchpart set directly, to be able to send it back
                    var batchPart = response.BatchPartInfo.GetBatch();
                    response.Set = batchPart.DmSetSurrogate;
                }
                else
                {
                    // We are in memory, so generate the DmSetSurrogate to be able to send it back
                    response.Set = new DmSetSurrogate(response.BatchPartInfo.Set);
                    response.BatchPartInfo.Set.Clear();
                    response.BatchPartInfo.Set = null;
                }

                // no need fileName info
                response.BatchPartInfo.FileName = null;

                httpMessage.SyncContext = syncContext;
                httpMessage.Content = response;

                CleanUp(bi);
                return httpMessage;
            }

            // We are in batch mode here
            var batchInfo = this.LocalProvider.CacheManager.GetValue<BatchInfo>("GetChangeBatch_BatchInfo");
            var stats = this.LocalProvider.CacheManager.GetValue<ChangesSelected>("GetChangeBatch_ChangesSelected");

            if (batchInfo == null)
                throw new SyncException("Session corrupted/lost: batchInfo stored in session can't be null if requesting another batch part info.",
                    httpMessage.SyncContext.SyncStage, this.LocalProvider.ProviderTypeName, SyncExceptionType.Argument);

            response.ChangesSelected = stats;
            response.InMemory = batchInfo.InMemory;

            var batchPartInfo = batchInfo.BatchPartsInfo.FirstOrDefault(bpi => bpi.Index == response.BatchIndexRequested);

            response.BatchPartInfo = batchPartInfo;

            // load the batchpart set directly, to be able to send it back
            response.Set = response.BatchPartInfo.GetBatch().DmSetSurrogate;
    }
    }
}