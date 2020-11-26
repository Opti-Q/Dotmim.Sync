using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
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
    public class WebApi2BugRepro02 : IClassFixture<SqliteSyncHttpFixture>, IDisposable
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

        public WebApi2BugRepro02(SqliteSyncHttpFixture fixture)
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
        public async Task WhenSendingOnSecondTry_StillUpdatesServiceTickets()
        {
            // Arrange
            var _ = await agent.SynchronizeAsync();

            var count = 2;
            var ticketIds = InsertRowsToClientDb(count);
            var url = new Uri(fixture.BaseAddress, "api/values");

            // create brand new client
            // make sure the server looses its session after the first successful request!
            var handler = new TestHttpHandler(
                url, 
                CancellationToken.None,
                (o) =>
                {
                    if (o is HttpMessage m && m.Step == HttpStep.GetChangeBatch)
                    {
                        throw new WebException("some connection issue... 🤷‍♂️");
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
            }
            catch (SyncException x)
            {
                exception = x;
            }

            // Assert
            exception.ShouldNotBeNull(" aw web exception was thrown!");

            // Arrange 2
            clientProvider = new SqliteSyncProvider(fixture.ClientSqliteFilePath);
            proxyClientProvider = new WebProxyClientProvider(url);
            agent = new SyncAgent(clientProvider, proxyClientProvider);
            agent.Configuration.BatchDirectory = Path.Combine(batchDir, "client");
            agent.Configuration.DownloadBatchSizeInKB = 500;

            int ticketNumber = 0;
            foreach (var ticketId in ticketIds)
            {
                UpdateRowInClientDb(ticketId, $"updated title {++ticketNumber}");
            }


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
                scmd.Parameters.AddWithValue("@title", "updated title %");

                var serverCount = scmd.ExecuteScalar();
                serverCount.ShouldBe(count);
            }
        }
        
        [Fact, TestPriority(2)]
        public async Task DetectingDmRowState_ServerChange()
        {
            // Arrange
            var _ = await agent.SynchronizeAsync();

            var count = 1;
            var ticketIds = InsertRowsToClientDb(count);
            var url = new Uri(fixture.BaseAddress, "api/values");

            clientProvider = new SqliteSyncProvider(fixture.ClientSqliteFilePath);
            proxyClientProvider = new WebProxyClientProvider(url);
            agent = new SyncAgent(clientProvider, proxyClientProvider);
            agent.Configuration.BatchDirectory = Path.Combine(batchDir, "client");
            agent.Configuration.DownloadBatchSizeInKB = 500;
            var __ = await agent.SynchronizeAsync();

            // Now update on server
            UpdateRowInServerDb(ticketIds.Single(), "updated on server :-)");

            // Act
            var session2 = await agent.SynchronizeAsync();

            // Assert 2
            session2.TotalChangesUploaded.ShouldBe(0);
            session2.TotalChangesDownloaded.ShouldBe(count);

            using (var sc = clientProvider.CreateConnection())
            {
                sc.Open();
                var scmd = (SqliteCommand)sc.CreateCommand();
                scmd.CommandText = "select title from servicetickets where serviceticketid = @id";
                scmd.Parameters.AddWithValue("@id", ticketIds.Single());

                using (var reader = await scmd.ExecuteReaderAsync())
                {
                    while (reader.Read())
                    {
                        var col = reader.GetString(0);
                        col.ShouldBe("updated on server :-)");
                    }

                }
            }
        }
        
        [Fact, TestPriority(3)]
        public async Task DetectingDmRowState_ClientChange()
        {
            // Arrange
            var _ = await agent.SynchronizeAsync();

            var count = 1;
            var ticketIds = InsertRowsToClientDb(count);
            var url = new Uri(fixture.BaseAddress, "api/values");

            clientProvider = new SqliteSyncProvider(fixture.ClientSqliteFilePath);
            proxyClientProvider = new WebProxyClientProvider(url);
            agent = new SyncAgent(clientProvider, proxyClientProvider);
            agent.Configuration.BatchDirectory = Path.Combine(batchDir, "client");
            agent.Configuration.DownloadBatchSizeInKB = 500;
            var __ = await agent.SynchronizeAsync();

            // Now update on server
            UpdateRowInServerDb(ticketIds.Single(), "updated on server :-)");

            // Act
            var session2 = await agent.SynchronizeAsync();

            // Assert 2
            session2.TotalChangesUploaded.ShouldBe(0);
            session2.TotalChangesDownloaded.ShouldBe(count);

            // Act 3
            UpdateRowInClientDb(ticketIds.Single(), "re-updated on CLIENT!!");

            var session3 = await agent.SynchronizeAsync();

            // Assert
            session3.TotalChangesUploaded.ShouldBe(count);
            session3.TotalChangesDownloaded.ShouldBe(0);

            using (var sc = serverProvider.CreateConnection())
            {
                sc.Open();
                var scmd = (SqlCommand)sc.CreateCommand();
                scmd.CommandText = "select title from servicetickets where serviceticketid = @id";
                scmd.Parameters.AddWithValue("@id", ticketIds.Single());

                using (var reader = await scmd.ExecuteReaderAsync())
                {
                    while (reader.Read())
                    {
                        var col = reader.GetString(0);
                        col.ShouldBe("re-updated on CLIENT!!");
                    }

                }
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

        private IEnumerable<Guid> InsertRowsToClientDb(int count)
        {
            var insertRowScript =
                $@"INSERT INTO [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) 
                VALUES (@id, @title, @description, 1, 0, datetime('now'), NULL, 1)";

            var longString = new String('x', 100);

            var ticketIds = new List<Guid>();

            using (var sqlConnection = new SqliteConnection(fixture.ClientSqliteConnectionString))
            {
                sqlConnection.Open();
                using (var tx = sqlConnection.BeginTransaction())
                {
                    using (var sqlCmd = new SqliteCommand(insertRowScript, sqlConnection))
                    {
                        sqlCmd.Parameters.AddWithValue("@id", Guid.Empty);
                        sqlCmd.Parameters.AddWithValue("@title", $"test ticket ");
                        sqlCmd.Parameters.AddWithValue("@description", longString);
                        sqlCmd.Transaction = tx;
                        sqlCmd.Prepare();

                        for (int i = 0; i < count; i++)
                        {
                            foreach (SqliteParameter p in sqlCmd.Parameters)
                            {
                                if (p.ParameterName == "@id")
                                {
                                    var ticketId = Guid.NewGuid();
                                    ticketIds.Add(ticketId);
                                    p.Value = ticketId;
                                }
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

            return ticketIds.AsEnumerable();
        }

        private void UpdateRowInClientDb(Guid id, string title)
        {
            var insertRowScript =
                $@"UPDATE  [ServiceTickets]  set [Title] = @title 
                    where [ServiceTicketID] = @id";

            using (var sqlConnection = new SqliteConnection(fixture.ClientSqliteConnectionString))
            {
                sqlConnection.Open();
                using (var tx = sqlConnection.BeginTransaction())
                {
                    using (var sqlCmd = new SqliteCommand(insertRowScript, sqlConnection))
                    {
                        sqlCmd.Parameters.AddWithValue("@id", Guid.Empty);
                        sqlCmd.Parameters.AddWithValue("@title", $"test ticket ");
                        sqlCmd.Transaction = tx;
                        sqlCmd.Prepare();

                        foreach (SqliteParameter p in sqlCmd.Parameters)
                        {
                            if (p.ParameterName == "@id")
                                p.Value = id;
                            if (p.ParameterName == "@title")
                                p.Value = title;
                        }

                        var nbRowsUpdated = sqlCmd.ExecuteNonQuery();
                        if (nbRowsUpdated < 0)
                            throw new Exception("Row not updated");
                 

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

        private void UpdateRowInServerDb(Guid id, string title)
        {
            var insertRowScript =
                $@"UPDATE  [ServiceTickets]  set [Title] = @title 
                    where [ServiceTicketID] = @id";

            using (var sqlConnection = new SqlConnection(fixture.ServerConnectionString))
            {
                sqlConnection.Open();
                using (var tx = sqlConnection.BeginTransaction())
                {
                    using (var sqlCmd = new SqlCommand(insertRowScript, sqlConnection))
                    {
                        sqlCmd.Parameters.AddWithValue("@id", Guid.Empty);
                        sqlCmd.Parameters.AddWithValue("@title", $"test ticket ");
                        sqlCmd.Transaction = tx;
                        //sqlCmd.Prepare();

                        foreach (SqlParameter p in sqlCmd.Parameters)
                        {
                            if (p.ParameterName == "@id")
                                p.Value = id;
                            if (p.ParameterName == "@title")
                                p.Value = title;
                        }

                        var nbRowsUpdated = sqlCmd.ExecuteNonQuery();
                        if (nbRowsUpdated < 0)
                            throw new Exception("Row not updated");


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

}

