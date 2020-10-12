using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Dispatcher;
using Dotmim.Sync.Sqlite;
using Dotmim.Sync.SqlServer;
using Dotmim.Sync.Tests.Misc;
using Dotmim.Sync.Web;
using Dotmim.Sync.Web.Client;
using Dotmim.Sync.Web.Server;
using Microsoft.Data.Sqlite;
using Microsoft.Owin.Hosting;
using Owin;
using Shouldly;
using Xunit;
using SerializationFormat = Dotmim.Sync.Enumerations.SerializationFormat;

namespace Dotmim.Sync.Tests
{
    [Collection("Http")]
    [TestCaseOrderer("Dotmim.Sync.Tests.Misc.PriorityOrderer", "Dotmim.Sync.WebApi2.Tests")]
    public class WebApi2BugRepro : IClassFixture<SqliteSyncHttpFixture>, IDisposable
    {
        SqlSyncProvider serverProvider;
        SqliteSyncProvider clientProvider;
        SqliteSyncHttpFixture fixture;
        WebProxyServerProvider proxyServerProvider;
        WebProxyClientProvider proxyClientProvider;
        SyncAgent agent;
        private Func<SyncConfiguration> configurationProvider;
        private IDisposable webApp;
        private string batchDir;

        public WebApi2BugRepro(SqliteSyncHttpFixture fixture)
        {
            this.fixture = fixture;
            this.batchDir = Path.Combine(Environment.CurrentDirectory, Guid.NewGuid().ToString("N"));
            
            configurationProvider = () => new SyncConfiguration(fixture.Tables);

            serverProvider = new SqlSyncProvider(fixture.ServerConnectionString);
            proxyServerProvider = new WebProxyServerProvider(serverProvider);

            webApp = WebApp.Start(fixture.BaseAddress.OriginalString, (appBuilder) =>
            {
                // Configure Web API for self-host. 
                HttpConfiguration config = new HttpConfiguration();
                config.Routes.MapHttpRoute(
                    name: "DefaultApi",
                    routeTemplate: "api/{controller}/{actionid}/{id}",
                    defaults: new { actionid = RouteParameter.Optional, id = RouteParameter.Optional }
                );
                config.Services.Replace(typeof(IHttpControllerActivator), new TestControllerActivator(
                    () =>
                    {
                        var syncConfig = configurationProvider();
                        syncConfig.BatchDirectory = Path.Combine(batchDir, "server");
                        proxyServerProvider.Configuration = syncConfig;
                        return proxyServerProvider;
                    }));
                appBuilder.UseWebApi(config);
            });

            //var newUrl =
            //    fixture.BaseAddress.OriginalString.Replace(fixture.BaseAddress.Host,
            //        fixture.BaseAddress.Host + ".fiddler");
            //var newUri = new Uri(newUrl);
            var newUri = fixture.BaseAddress;

            clientProvider = new SqliteSyncProvider(fixture.ClientSqliteFilePath);
            proxyClientProvider = new WebProxyClientProvider(new Uri(newUri, "api/values"));

            agent = new SyncAgent(clientProvider, proxyClientProvider);
            agent.Configuration.BatchDirectory = Path.Combine(batchDir, "client");
        }
        
        [Fact, TestPriority(1)]
        public async Task SendMultipleBatches()
        {
            // Arrange
            var session0 = await agent.SynchronizeAsync();

            // insert 1001 rows
            var count = 3001;
            InsertRowsToClientDb(count);
            var url = new Uri(fixture.BaseAddress, "api/values");

            int batchSent = 0;
            // create brand new client
            // make sure the server looses its session after the first successful request!
            var handler = new TestHttpHandler(
                url, 
                CancellationToken.None,
                (o) =>
                {
                    if (o is HttpMessage m && m.Step == HttpStep.ApplyChanges)
                    {
                        // Clear the session cache of the serve - simulating an application pool reset
                        if(batchSent == 1)
                            serverProvider.CacheManager.Clear();

                        batchSent++;
                    }
                });

            clientProvider = new SqliteSyncProvider(fixture.ClientSqliteFilePath);
            proxyClientProvider = new WebProxyClientProvider(handler);
            agent = new SyncAgent(clientProvider, proxyClientProvider);
            agent.Configuration.BatchDirectory = Path.Combine(batchDir, "client");
            agent.Configuration.DownloadBatchSizeInKB = 500;

            SyncException exception = null;
            try
            {
                // Act
                await agent.SynchronizeAsync();

                // show what happens when bugfix is not in place
                using (var sc = serverProvider.CreateConnection())
                {
                    sc.Open();
                    var scmd = (SqlCommand)sc.CreateCommand();
                    scmd.CommandText = "select count(*) from servicetickets where title like @title";
                    scmd.Parameters.AddWithValue("@title", "test ticket %");

                    var serverCount = scmd.ExecuteScalar();
                    serverCount.ShouldBe(count);
                }
            }
            catch (SyncException x)
            {
                exception = x;
            }

            // Assert
            exception.ShouldNotBeNull();
            var wse = exception.InnerException as WebSyncException;
            wse.ShouldNotBeNull();
            wse.Message.ShouldBe("Session corrupted/lost: batchInfo stored in session can't be null if sending an additional batch part info.");

            // Arrange 2
            clientProvider = new SqliteSyncProvider(fixture.ClientSqliteFilePath);
            proxyClientProvider = new WebProxyClientProvider(url);
            agent = new SyncAgent(clientProvider, proxyClientProvider);
            agent.Configuration.BatchDirectory = Path.Combine(batchDir, "client");
            agent.Configuration.DownloadBatchSizeInKB = 500;

            // Act 2
            var session = await agent.SynchronizeAsync();

            // Assert 2
            session.TotalChangesUploaded.ShouldBe(count);
            session.TotalChangesDownloaded.ShouldBe(0);

            using (var sc = serverProvider.CreateConnection())
            {
                sc.Open();
                var scmd = (SqlCommand)sc.CreateCommand();
                scmd.CommandText = "select count(*) from servicetickets where title like @title";
                scmd.Parameters.AddWithValue("@title", "test ticket %");

                var serverCount = scmd.ExecuteScalar();
                serverCount.ShouldBe(count);
            }
        }

        
        [Fact, TestPriority(2)]
        public async Task ReceiveMultipleBatches()
        {
            // Arrange
            await agent.SynchronizeAsync();


            // insert 1001 rows
            const int count = 3001;
            const string ticketPrefix = "server ticket";
            InsertRowsToServerDb(count, ticketPrefix);
            var url = new Uri(fixture.BaseAddress, "api/values");

            int batchSent = 0;
            // create brand new client
            // make sure the server looses its session after the first successful request!
            var handler = new TestHttpHandler(
                url, 
                CancellationToken.None,
                (o) =>
                {
                    if (o is HttpMessage m && m.Step == HttpStep.GetChangeBatch)
                    {
                        // Clear the session cache of the serve - simulating an application pool reset
                        if(batchSent == 1)
                            serverProvider.CacheManager.Clear();

                        batchSent++;
                    }
                });

            clientProvider = new SqliteSyncProvider(fixture.ClientSqliteFilePath);
            proxyClientProvider = new WebProxyClientProvider(handler);
            agent = new SyncAgent(clientProvider, proxyClientProvider);
            agent.Configuration.BatchDirectory = Path.Combine(batchDir, "client");
            agent.Configuration.DownloadBatchSizeInKB = 500;

            SyncException exception = null;
            try
            {
                // Act
                await agent.SynchronizeAsync();

                // show what happens when bugfix is not in place
                using (var sc = clientProvider.CreateConnection())
                {
                    sc.Open();
                    var scmd = (SqliteCommand)sc.CreateCommand();
                    scmd.CommandText = "select count(*) from servicetickets where title like @title";
                    scmd.Parameters.AddWithValue("@title", $"{ticketPrefix} %");

                    var serverCount = scmd.ExecuteScalar();
                    serverCount.ShouldBe(count);
                }
            }
            catch (SyncException x)
            {
                exception = x;
            }

            // Assert
            exception.ShouldNotBeNull();
            var wse = exception.InnerException as WebSyncException;
            wse.ShouldNotBeNull();
            wse.Message.ShouldBe("Session corrupted/lost: batchInfo stored in session can't be null if requesting another batch part info.");

            // Arrange 2
            clientProvider = new SqliteSyncProvider(fixture.ClientSqliteFilePath);
            proxyClientProvider = new WebProxyClientProvider(url);
            agent = new SyncAgent(clientProvider, proxyClientProvider);
            agent.Configuration.BatchDirectory = Path.Combine(batchDir, "client");
            agent.Configuration.DownloadBatchSizeInKB = 500;

            // Act 2
            var session = await agent.SynchronizeAsync();

            // Assert 2
            session.TotalChangesUploaded.ShouldBe(0);
            session.TotalChangesDownloaded.ShouldBe(count);

            using (var sc = clientProvider.CreateConnection())
            {
                sc.Open();
                var scmd = (SqliteCommand)sc.CreateCommand();
                scmd.CommandText = "select count(*) from servicetickets where title like @title";
                scmd.Parameters.AddWithValue("@title", "test ticket %");

                var serverCount = scmd.ExecuteScalar();
                serverCount.ShouldBe(count);
            }
        }

        public class TestHttpHandler : HttpRequestHandler
        {
            private readonly Action<object> action;

            public TestHttpHandler(Uri serviceUri, CancellationToken cancellationToken, Action<object> action)
                : base(serviceUri, cancellationToken)
            {
                this.action = action;
            }

            public override Task<T> ProcessRequest<T>(T content, SerializationFormat serializationFormat, CancellationToken cancellationToken)
            {
                action(content);

                return base.ProcessRequest(content, serializationFormat, cancellationToken);
            }
        }

        private void InsertRowsToClientDb(int count)
        {
            var insertRowScript =
                $@"INSERT INTO [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) 
                VALUES (@id, @title, @description, 1, 0, datetime('now'), NULL, 1)";

            var longString = new String('x', 100);

            using (var sqlConnection = new SqliteConnection(fixture.ClientSqliteConnectionString))
            {
                sqlConnection.Open();
                using (var tx = sqlConnection.BeginTransaction())
                {
                    using (var sqlCmd = new SqliteCommand(insertRowScript, sqlConnection))
                    {
                        sqlCmd.Parameters.AddWithValue("@id", Guid.NewGuid());
                        sqlCmd.Parameters.AddWithValue("@title", $"test ticket ");
                        sqlCmd.Parameters.AddWithValue("@description", longString);
                        sqlCmd.Transaction = tx;
                        sqlCmd.Prepare();

                        for (int i = 0; i < count; i++)
                        {
                            foreach (SqliteParameter p in sqlCmd.Parameters)
                            {
                                if (p.ParameterName == "@id")
                                    p.Value = Guid.NewGuid();
                                if (p.ParameterName == "@title")
                                    p.Value = $"test ticket {i + 1:0000}";
                                if (p.ParameterName == "@description")
                                    p.Value = longString;
                            }

                            var nbRowsInserted = sqlCmd.ExecuteNonQuery();
                            if (nbRowsInserted < 0)
                                throw new Exception("Row not inserted");
                        }
                    }
                    tx.Commit();
                }
            }

        }

        private void InsertRowsToServerDb(int count, string titlePrefix)
        {
            var insertRowScript =
                $@"INSERT INTO [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) 
                VALUES (@id, @title, @description, 1, 0, getutcdate(), NULL, 1)";

            var longString = new String('x', 100);

            using (var sqlConnection = new SqlConnection(fixture.ServerConnectionString))
            {
                sqlConnection.Open();
                using (var tx = sqlConnection.BeginTransaction())
                {
                    using (var sqlCmd = new SqlCommand(insertRowScript, sqlConnection))
                    {
                        sqlCmd.Parameters.AddWithValue("@id", Guid.NewGuid()).SetDbType(DbType.Guid).SetLength(16);
                        sqlCmd.Parameters.AddWithValue("@title", $"test ticket ").SetDbType(DbType.String).SetLength(100);
                        sqlCmd.Parameters.AddWithValue("@description", longString).SetDbType(DbType.String).SetLength(100);
                        sqlCmd.Transaction = tx;
                        sqlCmd.Prepare();

                        for (int i = 0; i < count; i++)
                        {
                            foreach (SqlParameter p in sqlCmd.Parameters)
                            {
                                if (p.ParameterName == "@id")
                                    p.Value = Guid.NewGuid();
                                if (p.ParameterName == "@title")
                                    p.Value = $"{titlePrefix} {i + 1:0000}";
                                if (p.ParameterName == "@description")
                                    p.Value = longString;
                            }

                            var nbRowsInserted = sqlCmd.ExecuteNonQuery();
                            if (nbRowsInserted < 0)
                                throw new Exception("Row not inserted");
                        }
                    }
                    tx.Commit();
                }
            }

        }


        public void Dispose()
        {
            proxyClientProvider?.Dispose();
            agent?.Dispose();
            webApp?.Dispose();


            if (Directory.Exists(this.batchDir))
                Directory.Delete(this.batchDir, true);
        }
    }

    internal static class SqlParameterExtensions
    {
        public static SqlParameter SetDbType(this SqlParameter p, DbType type)
        {
            p.DbType = type;
            return p;
        }
        public static SqlParameter SetLength(this SqlParameter p, int size)
        {
            p.Size = size;
            return p;
        }
    }
}

