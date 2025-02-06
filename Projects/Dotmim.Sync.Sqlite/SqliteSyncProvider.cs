using Dotmim.Sync.Builders;
using Dotmim.Sync.Cache;
using Dotmim.Sync.Data;
using Dotmim.Sync.Manager;
using System;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using System.IO;
using System.Threading.Tasks;
using Dotmim.Sync.Messages;

namespace Dotmim.Sync.Sqlite
{

    public class SqliteSyncProvider : CoreProvider
    {
        private ICache cacheManager;
        private string filePath;
        private DbMetadata dbMetadata;
        private static String providerType;

        public override DbMetadata Metadata
        {
            get
            {
                if (dbMetadata == null)
                    dbMetadata = new SqliteDbMetadata();

                return dbMetadata;
            }
            set
            {
                dbMetadata = value;

            }
        }
        public override ICache CacheManager
        {
            get
            {
                if (cacheManager == null)
                    cacheManager = new InMemoryCache();

                return cacheManager;
            }
            set
            {
                cacheManager = value;

            }
        }

        /// <summary>
        /// Sqlite does not support Bulk operations
        /// </summary>
        public override bool SupportBulkOperations => false;

        /// <summary>
        /// SQLIte does not support to be a server side.
        /// Reason 1 : Can't easily insert / update batch with handling conflict
        /// Reason 2 : Can't filter rows (based on filterclause)
        /// </summary>
        public override bool CanBeServerProvider => false;

        public override string ProviderTypeName
        {
            get
            {
                return ProviderType;
            }
        }

        public static string ProviderType
        {
            get
            {
                if (!string.IsNullOrEmpty(providerType))
                    return providerType;

                Type type = typeof(SqliteSyncProvider);
                providerType = $"{type.Name}, {type.ToString()}";

                return providerType;
            }

        }
        public SqliteSyncProvider() : base()
        {
        }
        public SqliteSyncProvider(string filePath) : base()
        {
            this.filePath = filePath;
            var builder = new SqliteConnectionStringBuilder();

            if (filePath.ToLowerInvariant().StartsWith("data source"))
            {
                this.ConnectionString = filePath;
            }
            else
            {
                var fileInfo = new FileInfo(filePath);

                if (!Directory.Exists(fileInfo.Directory.FullName))
                    throw new Exception($"filePath directory {fileInfo.Directory.FullName} does not exists.");

                if (string.IsNullOrEmpty(fileInfo.Name))
                    throw new Exception($"Sqlite database file path needs a file name");

                builder.DataSource = filePath;

                this.ConnectionString = builder.ConnectionString;
            }

        }

        public SqliteSyncProvider(FileInfo fileInfo) : base()
        {
            this.filePath = fileInfo.FullName;
            var builder = new SqliteConnectionStringBuilder { DataSource = filePath };
            
            this.ConnectionString = builder.ConnectionString;
        }



        public SqliteSyncProvider(SqliteConnectionStringBuilder sQLiteConnectionStringBuilder) : base()
        {
            if (String.IsNullOrEmpty(sQLiteConnectionStringBuilder.DataSource))
                throw new Exception("You have to provide at least a DataSource property to be able to connect to your SQlite database.");

            this.filePath = sQLiteConnectionStringBuilder.DataSource;

            this.ConnectionString = sQLiteConnectionStringBuilder.ConnectionString;
        }

        protected override async Task OnGettingChanges(SyncContext context, DmTable tableDescription, DbConnection connection,
            DbTransaction transaction)
        {
            // Use the session ID from the context
            var sessionId = context.SessionId;
            
            var builder = this.GetDatabaseBuilder(tableDescription);
            using (var syncAdapter = builder.CreateSyncAdapter(connection, transaction))
            {
                var markRowsCommand = syncAdapter.GetCommand(DbCommandType.MarkRowsAsSyncing);
                DbManager.SetParameterValue(markRowsCommand, "sync_session_id", sessionId);
                var changed = await markRowsCommand.ExecuteNonQueryAsync();
                System.Diagnostics.Debug.WriteLine($"OnGettingChanges.{tableDescription.TableName} {context.SessionId:N}: {changed}");
            }
        }

        protected override async Task OnApplyingChangesAsync(SyncContext context, MessageApplyChanges message, DbConnection connection,
            DbTransaction applyTransaction)
        {
            foreach (var table in message.Schema.Tables)
            {
                var builder = GetDatabaseBuilder(table);
                using (var syncAdapter = builder.CreateSyncAdapter(connection, applyTransaction))
                {
                    var markRowsSyncedCommand = syncAdapter.GetCommand(DbCommandType.MarkRowsAsSynced);
                    DbManager.SetParameterValue(markRowsSyncedCommand, "sync_session_id", context.SessionId);
                    var changed = await markRowsSyncedCommand.ExecuteNonQueryAsync();
                    System.Diagnostics.Debug.WriteLine($"OnApplyingChanges.{table.TableName} {context.SessionId:N}: {changed}");
                }
            }
        }

        public override DbConnection CreateConnection() => new SqliteConnection(this.ConnectionString);

        public override DbBuilder GetDatabaseBuilder(DmTable tableDescription) => new SqliteBuilder(tableDescription);

        public override DbManager GetDbManager(string tableName) => new SqliteManager(tableName);

        public override DbScopeBuilder GetScopeBuilder() => new SqliteScopeBuilder();
    }
}
