using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dotmim.Sync.Batch;
using Dotmim.Sync.Data;
using Dotmim.Sync.Messages;

namespace Dotmim.Sync.Tests
{
    public class ProviderProxy : IProvider
    {
        private readonly IProvider p;

        public ProviderProxy(IProvider p)
        {
            this.p = p;
        }

        public event EventHandler<ProgressEventArgs> SyncProgress
        {
            add => p.SyncProgress += value;
            remove => p.SyncProgress -= value;
        }

        public event EventHandler<BeginSessionEventArgs> BeginSession
        {
            add => p.BeginSession += value;
            remove => p.BeginSession -= value;
        }

        public event EventHandler<EndSessionEventArgs> EndSession
        {
            add => p.EndSession += value;
            remove => p.EndSession -= value;
        }

        public event EventHandler<ScopeEventArgs> ScopeLoading
        {
            add => p.ScopeLoading += value;
            remove => p.ScopeLoading -= value;
        }

        public event EventHandler<ScopeEventArgs> ScopeSaved
        {
            add => p.ScopeSaved += value;
            remove => p.ScopeSaved -= value;
        }

        public event EventHandler<DatabaseApplyingEventArgs> DatabaseApplying
        {
            add => p.DatabaseApplying += value;
            remove => p.DatabaseApplying -= value;
        }

        public event EventHandler<DatabaseAppliedEventArgs> DatabaseApplied
        {
            add => p.DatabaseApplied += value;
            remove => p.DatabaseApplied -= value;
        }

        public event EventHandler<DatabaseTableApplyingEventArgs> DatabaseTableApplying
        {
            add => p.DatabaseTableApplying += value;
            remove => p.DatabaseTableApplying -= value;
        }

        public event EventHandler<DatabaseTableAppliedEventArgs> DatabaseTableApplied
        {
            add => p.DatabaseTableApplied += value;
            remove => p.DatabaseTableApplied -= value;
        }

        public event EventHandler<SchemaApplyingEventArgs> SchemaApplying
        {
            add => p.SchemaApplying += value;
            remove => p.SchemaApplying -= value;
        }

        public event EventHandler<SchemaAppliedEventArgs> SchemaApplied
        {
            add => p.SchemaApplied += value;
            remove => p.SchemaApplied -= value;
        }

        public event EventHandler<TableChangesSelectingEventArgs> TableChangesSelecting
        {
            add => p.TableChangesSelecting += value;
            remove => p.TableChangesSelecting -= value;
        }

        public event EventHandler<TableChangesSelectedEventArgs> TableChangesSelected
        {
            add => p.TableChangesSelected += value;
            remove => p.TableChangesSelected -= value;
        }

        public event EventHandler<TableChangesApplyingEventArgs> TableChangesApplying
        {
            add => p.TableChangesApplying += value;
            remove => p.TableChangesApplying -= value;
        }

        public event EventHandler<TableChangesAppliedEventArgs> TableChangesApplied
        {
            add => p.TableChangesApplied += value;
            remove => p.TableChangesApplied -= value;
        }

        public event EventHandler<ApplyChangeFailedEventArgs> ApplyChangedFailed
        {
            add => p.ApplyChangedFailed += value;
            remove => p.ApplyChangedFailed -= value;
        }

        public void SetCancellationToken(CancellationToken token)
        {
            p.SetCancellationToken(token);
        }

        public virtual Task<(SyncContext, SyncConfiguration)> BeginSessionAsync(SyncContext context, MessageBeginSession message)
        {
            return p.BeginSessionAsync(context, message);
        }

        public virtual Task<(SyncContext, List<ScopeInfo>)> EnsureScopesAsync(SyncContext context, MessageEnsureScopes messsage)
        {
            return p.EnsureScopesAsync(context, messsage);
        }

        public virtual Task<(SyncContext, DmSet)> EnsureSchemaAsync(SyncContext context, MessageEnsureSchema message)
        {
            return p.EnsureSchemaAsync(context, message);
        }

        public virtual Task<SyncContext> EnsureDatabaseAsync(SyncContext context, MessageEnsureDatabase message)
        {
            return p.EnsureDatabaseAsync(context, message);
        }

        public virtual Task<(SyncContext, ChangesApplied)> ApplyChangesAsync(SyncContext context, MessageApplyChanges message)
        {
            return p.ApplyChangesAsync(context, message);
        }

        public virtual Task<(SyncContext, BatchInfo, ChangesSelected)> GetChangeBatchAsync(SyncContext context, MessageGetChangesBatch message)
        {
            return p.GetChangeBatchAsync(context, message);
        }

        public virtual Task<SyncContext> WriteScopesAsync(SyncContext context, MessageWriteScopes message)
        {
            return p.WriteScopesAsync(context, message);
        }

        public virtual Task<SyncContext> EndSessionAsync(SyncContext context)
        {
            return p.EndSessionAsync(context);
        }

        public virtual Task<(SyncContext, long)> GetLocalTimestampAsync(SyncContext context, MessageTimestamp message)
        {
            return p.GetLocalTimestampAsync(context, message);
        }
    }
}