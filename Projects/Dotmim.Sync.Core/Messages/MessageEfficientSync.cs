using System;
using Dotmim.Sync.Batch;
using Dotmim.Sync.Data.Surrogate;
using Dotmim.Sync.Enumerations;
using Dotmim.Sync.Filter;

namespace Dotmim.Sync.Messages
{
    /// <summary>
    /// Represents a consolidated message containing all sync steps to be processed in a single request
    /// </summary>
    [Serializable]
    public class MessageEfficientSync 
    {
        
        /// <summary>
        /// Gets the output batch index, returned by the server
        /// </summary>
        public int BatchIndexRequested { get; set; }
        /// <summary>
        /// Gets or sets the configuration object containing sync settings
        /// </summary>
        public SyncConfiguration Configuration { get; set; }

        /// <summary>
        /// Gets or sets the client scope info
        /// </summary>
        public ScopeInfo ClientScope { get; set; }

        /// <summary>
        /// Gets or sets the batch info for current batch of changes
        /// </summary>
        public BatchPartInfo BatchPartInfo { get; set; }

        /// <summary>
        /// Gets or sets the current batch index being processed
        /// </summary>
        public int BatchIndex { get; set; }

        /// <summary>
        /// Gets or sets whether this is the final batch
        /// </summary>
        public bool IsFinalBatch { get; set; }

        /// <summary>
        /// Gets or sets the sync parameters for filtering
        /// </summary>
        public SyncParameterCollection Parameters { get; set; }

        /// <summary>
        /// Gets or sets the sync type (Normal, Reinitialize, etc.)
        /// </summary>
        public SyncType SyncType { get; set; }

        /// <summary>
        /// Gets or sets the serialization format used
        /// </summary>
        public SerializationFormat SerializationFormat { get; set; }
        
        /// <summary>
        /// Gets or Sets the tables schema. Overriding the default Schema DmSet property, which is not serializable
        /// </summary>
        public new DmSetSurrogate Schema { get; set; }
    }
}