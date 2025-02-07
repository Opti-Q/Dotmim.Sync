using System;
using Dotmim.Sync.Batch;
using Dotmim.Sync.Data.Surrogate;

namespace Dotmim.Sync.Messages
{
    /// <summary>
    /// Response message containing sync results from server
    /// </summary>
    [Serializable]
    public class MessageEfficientSyncResponse
    {
        /// <summary>
        /// Gets or sets the final configuration after server processing
        /// </summary>
        public SyncConfiguration Configuration { get; set; }

        /// <summary>
        /// Gets or sets the server scope info
        /// </summary>
        public ScopeInfo ServerScope { get; set; }

        /// <summary>
        /// Gets or sets the changes applied on server for current batch
        /// </summary>
        public ChangesApplied ServerChangesApplied { get; set; }

        /// <summary>
        /// Gets or sets the current batch of changes to be applied on client
        /// </summary>
        public BatchPartInfo BatchPartInfo { get; set; }

        /// <summary>
        /// Gets or sets whether this is the final server batch
        /// </summary>
        public bool IsServerFinalBatch { get; set; }

        /// <summary>
        /// Gets or sets stats about changes selected on server
        /// </summary>
        public ChangesSelected ServerChangesSelected { get; set; }

        /// <summary>
        /// Gets or sets the timestamp from server
        /// </summary>
        public long ServerTimestamp { get; set; }
        /// <summary>
        /// Gets or Sets the tables schema. Overriding the default Schema DmSet property, which is not serializable
        /// </summary>
        public new DmSetSurrogate Schema { get; set; }
    }
}