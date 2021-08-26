using System;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Dispatcher;
using Dotmim.Sync.Enumerations;
using Dotmim.Sync.Sqlite;
using Dotmim.Sync.SqlServer;
using Dotmim.Sync.Test.SqlUtils;
using Dotmim.Sync.Tests.Misc;
using Dotmim.Sync.Web.Client;
using Dotmim.Sync.Web.Server;
using Microsoft.Data.Sqlite;
using Microsoft.Owin.Hosting;
using Owin;
using Polly;
using Xunit;

namespace Dotmim.Sync.Tests
{
    public class SqliteSyncHttpFixture : IDisposable
    {
        public string createTableScript =
        $@"if (not exists (select * from sys.tables where name = 'ServiceTickets'))
            begin
                CREATE TABLE [ServiceTickets](
	            [ServiceTicketID] [uniqueidentifier] NOT NULL,
	            [Title] [nvarchar](max) NOT NULL,
	            [Description] [nvarchar](max) NULL,
	            [StatusValue] [int] NOT NULL,
	            [EscalationLevel] [int] NOT NULL,
	            [Opened] [datetime] NULL,
	            [Closed] [datetime] NULL,
	            [CustomerID] [int] NULL,
                CONSTRAINT [PK_ServiceTickets] PRIMARY KEY CLUSTERED ( [ServiceTicketID] ASC ));
            end";

        public string datas =
        $@"
            INSERT [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) VALUES (newid(), 'Titre 3', 'Description 3', 1, 0, CAST('2016-07-29T16:36:41.733' AS DateTime), NULL, 1)
            INSERT [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) VALUES (newid(), 'Titre 4', 'Description 4', 1, 0, CAST('2016-07-29T16:36:41.733' AS DateTime), NULL, 1)
            INSERT [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) VALUES (newid(), 'Titre Client 1', 'Description Client 1', 1, 0, CAST('2016-07-29T17:26:20.720' AS DateTime), NULL, 1)
            INSERT [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) VALUES (newid(), 'Titre 6', 'Description 6', 1, 0, CAST('2016-07-29T16:36:41.733' AS DateTime), NULL, 1)
            INSERT [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) VALUES (newid(), 'Titre 7', 'Description 7', 1, 0, CAST('2016-07-29T16:36:41.733' AS DateTime), NULL, 10)
            INSERT [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) VALUES (newid(), 'Titre 3', 'Description 3', 1, 0, CAST('2016-07-29T16:36:41.733' AS DateTime), NULL, 1)
            INSERT [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) VALUES (newid(), 'Titre 4', 'Description 4', 1, 0, CAST('2016-07-29T16:36:41.733' AS DateTime), NULL, 1)
            INSERT [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) VALUES (newid(), 'Titre Client 1', 'Description Client 1', 1, 0, CAST('2016-07-29T17:26:20.720' AS DateTime), NULL, 1)
            INSERT [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) VALUES (newid(), 'Titre 6', 'Description 6', 1, 0, CAST('2016-07-29T16:36:41.733' AS DateTime), NULL, 1)
            INSERT [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) VALUES (newid(), 'Titre 7', 'Description 7', 1, 0, CAST('2016-07-29T16:36:41.733' AS DateTime), NULL, 10)
            INSERT [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) VALUES (newid(), 'Titre 3', 'Description 3', 1, 0, CAST('2016-07-29T16:36:41.733' AS DateTime), NULL, 1)
            INSERT [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) VALUES (newid(), 'Titre 4', 'Description 4', 1, 0, CAST('2016-07-29T16:36:41.733' AS DateTime), NULL, 1)
            INSERT [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) VALUES (newid(), 'Titre Client 1', 'Description Client 1', 1, 0, CAST('2016-07-29T17:26:20.720' AS DateTime), NULL, 1)
            INSERT [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) VALUES (newid(), 'Titre 6', 'Description 6', 1, 0, CAST('2016-07-29T16:36:41.733' AS DateTime), NULL, 1)
            INSERT [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) VALUES (newid(), 'Titre 7', 'Description 7', 1, 0, CAST('2016-07-29T16:36:41.733' AS DateTime), NULL, 10)
            INSERT [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) VALUES (newid(), 'Titre 3', 'Description 3', 1, 0, CAST('2016-07-29T16:36:41.733' AS DateTime), NULL, 1)
            INSERT [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) VALUES (newid(), 'Titre 4', 'Description 4', 1, 0, CAST('2016-07-29T16:36:41.733' AS DateTime), NULL, 1)
            INSERT [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) VALUES (newid(), 'Titre Client 1', 'Description Client 1', 1, 0, CAST('2016-07-29T17:26:20.720' AS DateTime), NULL, 1)
            INSERT [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) VALUES (newid(), 'Titre 6', 'Description 6', 1, 0, CAST('2016-07-29T16:36:41.733' AS DateTime), NULL, 1)
            INSERT [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) VALUES (newid(), 'Titre 7', 'Description 7', 1, 0, CAST('2016-07-29T16:36:41.733' AS DateTime), NULL, 10)
            INSERT [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) VALUES (newid(), 'Titre 3', 'Description 3', 1, 0, CAST('2016-07-29T16:36:41.733' AS DateTime), NULL, 1)
            INSERT [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) VALUES (newid(), 'Titre 4', 'Description 4', 1, 0, CAST('2016-07-29T16:36:41.733' AS DateTime), NULL, 1)
            INSERT [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) VALUES (newid(), 'Titre Client 1', 'Description Client 1', 1, 0, CAST('2016-07-29T17:26:20.720' AS DateTime), NULL, 1)
            INSERT [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) VALUES (newid(), 'Titre 6', 'Description 6', 1, 0, CAST('2016-07-29T16:36:41.733' AS DateTime), NULL, 1)
            INSERT [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) VALUES (newid(), 'Titre 7', 'Description 7', 1, 0, CAST('2016-07-29T16:36:41.733' AS DateTime), NULL, 10)
            INSERT [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) VALUES (newid(), 'Titre 3', 'Description 3', 1, 0, CAST('2016-07-29T16:36:41.733' AS DateTime), NULL, 1)
            INSERT [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) VALUES (newid(), 'Titre 4', 'Description 4', 1, 0, CAST('2016-07-29T16:36:41.733' AS DateTime), NULL, 1)
            INSERT [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) VALUES (newid(), 'Titre Client 1', 'Description Client 1', 1, 0, CAST('2016-07-29T17:26:20.720' AS DateTime), NULL, 1)
            INSERT [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) VALUES (newid(), 'Titre 6', 'Description 6', 1, 0, CAST('2016-07-29T16:36:41.733' AS DateTime), NULL, 1)
            INSERT [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) VALUES (newid(), 'Titre 7', 'Description 7', 1, 0, CAST('2016-07-29T16:36:41.733' AS DateTime), NULL, 10)
            INSERT [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) VALUES (newid(), 'Titre 3', 'Description 3', 1, 0, CAST('2016-07-29T16:36:41.733' AS DateTime), NULL, 1)
            INSERT [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) VALUES (newid(), 'Titre 4', 'Description 4', 1, 0, CAST('2016-07-29T16:36:41.733' AS DateTime), NULL, 1)
            INSERT [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) VALUES (newid(), 'Titre Client 1', 'Description Client 1', 1, 0, CAST('2016-07-29T17:26:20.720' AS DateTime), NULL, 1)
            INSERT [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) VALUES (newid(), 'Titre 6', 'Description 6', 1, 0, CAST('2016-07-29T16:36:41.733' AS DateTime), NULL, 1)
            INSERT [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) VALUES (newid(), 'Titre 7', 'Description 7', 1, 0, CAST('2016-07-29T16:36:41.733' AS DateTime), NULL, 10)
            INSERT [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) VALUES (newid(), 'Titre 3', 'Description 3', 1, 0, CAST('2016-07-29T16:36:41.733' AS DateTime), NULL, 1)
            INSERT [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) VALUES (newid(), 'Titre 4', 'Description 4', 1, 0, CAST('2016-07-29T16:36:41.733' AS DateTime), NULL, 1)
            INSERT [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) VALUES (newid(), 'Titre Client 1', 'Description Client 1', 1, 0, CAST('2016-07-29T17:26:20.720' AS DateTime), NULL, 1)
            INSERT [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) VALUES (newid(), 'Titre 6', 'Description 6', 1, 0, CAST('2016-07-29T16:36:41.733' AS DateTime), NULL, 1)
            INSERT [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) VALUES (newid(), 'Titre 7', 'Description 7', 1, 0, CAST('2016-07-29T16:36:41.733' AS DateTime), NULL, 10)
            INSERT [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) VALUES (newid(), 'Titre 3', 'Description 3', 1, 0, CAST('2016-07-29T16:36:41.733' AS DateTime), NULL, 1)
            INSERT [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) VALUES (newid(), 'Titre 4', 'Description 4', 1, 0, CAST('2016-07-29T16:36:41.733' AS DateTime), NULL, 1)
            INSERT [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) VALUES (newid(), 'Titre Client 1', 'Description Client 1', 1, 0, CAST('2016-07-29T17:26:20.720' AS DateTime), NULL, 1)
            INSERT [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) VALUES (newid(), 'Titre 6', 'Description 6', 1, 0, CAST('2016-07-29T16:36:41.733' AS DateTime), NULL, 1)
            INSERT [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) VALUES (newid(), 'Titre 7', 'Description 7', 1, 0, CAST('2016-07-29T16:36:41.733' AS DateTime), NULL, 10)
            INSERT [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) VALUES (newid(), 'Titre 3', 'Description 3', 1, 0, CAST('2016-07-29T16:36:41.733' AS DateTime), NULL, 1)
            INSERT [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) VALUES (newid(), 'Titre 4', 'Description 4', 1, 0, CAST('2016-07-29T16:36:41.733' AS DateTime), NULL, 1)
            INSERT [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) VALUES (newid(), 'Titre Client 1', 'Description Client 1', 1, 0, CAST('2016-07-29T17:26:20.720' AS DateTime), NULL, 1)
            INSERT [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) VALUES (newid(), 'Titre 6', 'Description 6', 1, 0, CAST('2016-07-29T16:36:41.733' AS DateTime), NULL, 1)
            INSERT [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) VALUES (newid(), 'Titre 7', 'Description 7', 1, 0, CAST('2016-07-29T16:36:41.733' AS DateTime), NULL, 10)
          ";
        private readonly string baseAddress;

        public string[] Tables => new string[] { "ServiceTickets" };

        public String ServerConnectionString => HelperDB.GetDatabaseConnectionString(ServerDbName);
        public String ClientSqliteConnectionString { get; set; }
        public string ClientSqliteFilePath => Path.Combine(Directory.GetCurrentDirectory(), "sqliteHttpTmpDb_webApi2.db");

        public Uri BaseAddress => new Uri(baseAddress);

        public string ServerDbName { get; } = "Test_Sqlite_Http_Server_WebApi2";
        private HelperDB helperDb = new HelperDB();

        public SqliteSyncHttpFixture()
        {
            baseAddress = "http://localhost:9902";

            var builder = new SqliteConnectionStringBuilder { DataSource = ClientSqliteFilePath };
            this.ClientSqliteConnectionString = builder.ConnectionString;


            if (File.Exists(ClientSqliteFilePath))
                File.Delete(ClientSqliteFilePath);

            // create databases
            helperDb.CreateDatabase(ServerDbName);
            // create table
            helperDb.ExecuteScript(ServerDbName, createTableScript);
            // insert table
            helperDb.ExecuteScript(ServerDbName, datas);
        }

        public void Dispose()
        {
            helperDb.DeleteDatabase(this.ServerDbName);

            GC.Collect();
            GC.WaitForPendingFinalizers();

            if (File.Exists(this.ClientSqliteFilePath))
                File.Delete(this.ClientSqliteFilePath);

        }
    }

    public class TestControllerActivator : IHttpControllerActivator
    {
        private readonly Func<WebProxyServerProvider> _factory;

        public TestControllerActivator(Func<WebProxyServerProvider> factory)
        {
            this._factory = factory;
        }

        public IHttpController Create(HttpRequestMessage request, HttpControllerDescriptor controllerDescriptor, Type controllerType)
        {
            return new ValuesController(_factory());
        }
    }

    [RoutePrefix("api/values")]
    public class ValuesController : ApiController
    {
        // proxy to handle requests and send them to SqlSyncProvider
        private readonly WebProxyServerProvider webProxyServer;

        // Injected thanks to Dependency Injection
        public ValuesController(WebProxyServerProvider proxy)
        {
            webProxyServer = proxy;
        }

        // POST api/values
        [HttpPost]
        [Route("")]
        public async Task<HttpResponseMessage> Post()
        {
            return await webProxyServer.HandleRequestAsync(this.Request, new NullHttpContext());
        }
        
        public class NullHttpContext : HttpContextBase
        {
        }
    }

    [Collection("Http")]
    [TestCaseOrderer("Dotmim.Sync.Tests.Misc.PriorityOrderer", "Dotmim.Sync.WebApi2.Tests")]
    public class SqliteSyncHttpTests : IClassFixture<SqliteSyncHttpFixture>, IDisposable
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

        public SqliteSyncHttpTests(SqliteSyncHttpFixture fixture)
        {
            this.fixture = fixture;
            this.batchDir = Path.Combine(Environment.CurrentDirectory, Guid.NewGuid().ToString("N"));
            
            configurationProvider = () => new SyncConfiguration(fixture.Tables);

            serverProvider = new SqlSyncProvider(fixture.ServerConnectionString);
            proxyServerProvider = new WebProxyServerProvider(serverProvider);
            proxyServerProvider.ApplyChangesPolicy = 
                Policy.Handle<SqlException>(ex => ex.Message.Contains("deadlock victim"))
                .WaitAndRetryAsync(new[]
                {
                    TimeSpan.FromMilliseconds(500),
                    TimeSpan.FromMilliseconds(1000),
                    TimeSpan.FromMilliseconds(1500)
                });
            proxyServerProvider.GetChangesBatchPolicy = 
                Policy.Handle<SqlException>(ex => ex.Message.Contains("deadlock victim"))
                .WaitAndRetryAsync(new[]
                {
                    TimeSpan.FromMilliseconds(500),
                    TimeSpan.FromMilliseconds(1000),
                    TimeSpan.FromMilliseconds(1500)
                });

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

            clientProvider = new SqliteSyncProvider(fixture.ClientSqliteFilePath);
            proxyClientProvider = new WebProxyClientProvider(new Uri(fixture.BaseAddress, "api/values"));

            agent = new SyncAgent(clientProvider, proxyClientProvider);
            agent.Configuration.BatchDirectory = Path.Combine(batchDir, "client");
        }

        [Fact, TestPriority(1)]
        public async Task Initialize()
        {
            // Act
            var session = await agent.SynchronizeAsync();

            // Assert
            Assert.Equal(50, session.TotalChangesDownloaded);
            Assert.Equal(0, session.TotalChangesUploaded);
        }

        [Theory, ClassData(typeof(InlineConfigurations)), TestPriority(2)]
        public async Task SyncNoRows(SyncConfiguration conf)
        {
            conf.Add(fixture.Tables);
            configurationProvider = () => conf;

            // Act
            var session = await agent.SynchronizeAsync();

            // Assert
            Assert.Equal(0, session.TotalChangesDownloaded);
            Assert.Equal(0, session.TotalChangesUploaded);
        }

        [Theory, ClassData(typeof(InlineConfigurations)), TestPriority(3)]
        public async Task InsertFromServer(SyncConfiguration conf)
        {
            conf.Add(fixture.Tables);
            configurationProvider = () => conf;
            await agent.SynchronizeAsync();

            var insertRowScript =
            $@"INSERT [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) 
                VALUES (newid(), 'Insert One Row', 'Description Insert One Row', 1, 0, getdate(), NULL, 1)";

            // Act
            using (var sqlConnection = new SqlConnection(fixture.ServerConnectionString))
            {
                using (var sqlCmd = new SqlCommand(insertRowScript, sqlConnection))
                {
                    sqlConnection.Open();
                    sqlCmd.ExecuteNonQuery();
                    sqlConnection.Close();
                }
            }

            // Assert
            var session = await agent.SynchronizeAsync();
            
            Assert.Equal(1, session.TotalChangesDownloaded);
            Assert.Equal(0, session.TotalChangesUploaded);

        }

        [Theory, ClassData(typeof(InlineConfigurations)), TestPriority(4)]
        public async Task InsertFromClient(SyncConfiguration conf)
        {
            conf.Add(fixture.Tables);
            configurationProvider = () => conf;
            // provision client infrastructure - otherwise this test will fail when run separately, because the sqlite db table won't yet exist
            await agent.SynchronizeAsync();

            Guid newId = Guid.NewGuid();

            var insertRowScript =
            $@"INSERT INTO [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) 
                VALUES (@id, 'Insert One Row in Sqlite client', 'Description Insert One Row', 1, 0, datetime('now'), NULL, 1)";

            int nbRowsInserted = 0;

            using (var sqlConnection = new SqliteConnection(fixture.ClientSqliteConnectionString))
            {
                using (var sqlCmd = new SqliteCommand(insertRowScript, sqlConnection))
                {
                    sqlCmd.Parameters.AddWithValue("@id", newId);

                    sqlConnection.Open();
                    nbRowsInserted = sqlCmd.ExecuteNonQuery();
                    sqlConnection.Close();
                }
            }
            if (nbRowsInserted < 0)
                throw new Exception("Row not inserted");


            // Assert
            var session = await agent.SynchronizeAsync();

            Assert.Equal(0, session.TotalChangesDownloaded);
            Assert.Equal(1, session.TotalChangesUploaded);
        }

        [Theory, ClassData(typeof(InlineConfigurations)), TestPriority(5)]
        public async Task UpdateFromClient(SyncConfiguration conf)
        {
            conf.Add(fixture.Tables);
            configurationProvider = () => conf;
            // provision client infrastructure - otherwise this test will fail when run separately, because the sqlite db table won't yet exist
            await agent.SynchronizeAsync();

            Guid newId = Guid.NewGuid();

            var insertRowScript =
            $@"INSERT INTO [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) 
                VALUES (@id, 'Insert One Row in Sqlite client', 'Description Insert One Row', 1, 0, datetime('now'), NULL, 1)";

            // Act 1
            using (var sqlConnection = new SqliteConnection(fixture.ClientSqliteConnectionString))
            {
                using (var sqlCmd = new SqliteCommand(insertRowScript, sqlConnection))
                {
                    sqlCmd.Parameters.AddWithValue("@id", newId);

                    sqlConnection.Open();
                    sqlCmd.ExecuteNonQuery();
                    sqlConnection.Close();
                }
            }

            // Assert 1
            var session1 = await agent.SynchronizeAsync();


            Assert.Equal(0, session1.TotalChangesDownloaded);
            Assert.Equal(1, session1.TotalChangesUploaded);

            var updateRowScript =
            $@" Update [ServiceTickets] Set [Title] = 'Updated from Sqlite Client side !' 
                Where ServiceTicketId = @id";

            // Act 2
            using (var sqlConnection = new SqliteConnection(fixture.ClientSqliteConnectionString))
            {
                using (var sqlCmd = new SqliteCommand(updateRowScript, sqlConnection))
                {
                    sqlCmd.Parameters.AddWithValue("@id", newId);

                    sqlConnection.Open();
                    sqlCmd.ExecuteNonQuery();
                    sqlConnection.Close();
                }
            }


            // Assert 2
            var session2 = await agent.SynchronizeAsync();


            Assert.Equal(0, session2.TotalChangesDownloaded);
            Assert.Equal(1, session2.TotalChangesUploaded);
        }

        [Theory, ClassData(typeof(InlineConfigurations)), TestPriority(6)]
        public async Task UpdateFromServer(SyncConfiguration conf)
        {
            _ = await agent.SynchronizeAsync();

            conf.Add(fixture.Tables);
            configurationProvider = () => conf;
            
            var updateRowScript =
            $@" Declare @id uniqueidentifier;
                Select top 1 @id = ServiceTicketID from ServiceTickets;
                Update [ServiceTickets] Set [Title] = 'Updated from server' Where ServiceTicketId = @id";

            using (var sqlConnection = new SqlConnection(fixture.ServerConnectionString))
            {
                using (var sqlCmd = new SqlCommand(updateRowScript, sqlConnection))
                {
                    sqlConnection.Open();
                    sqlCmd.ExecuteNonQuery();
                    sqlConnection.Close();
                }
            }
            
            // Assert
            var session = await agent.SynchronizeAsync();
            
            Assert.Equal(1, session.TotalChangesDownloaded);
            Assert.Equal(0, session.TotalChangesUploaded);
        }

        [Theory, ClassData(typeof(InlineConfigurations)), TestPriority(7)]
        public async Task DeleteFromServer(SyncConfiguration conf)
        {
            conf.Add(fixture.Tables);
            configurationProvider = () => conf;

            var updateRowScript =
            $@" Declare @id uniqueidentifier;
                Select top 1 @id = ServiceTicketID from ServiceTickets;
                Delete From [ServiceTickets] Where ServiceTicketId = @id";

            using (var sqlConnection = new SqlConnection(fixture.ServerConnectionString))
            {
                using (var sqlCmd = new SqlCommand(updateRowScript, sqlConnection))
                {
                    sqlConnection.Open();
                    sqlCmd.ExecuteNonQuery();
                    sqlConnection.Close();
                }
            }

            // Assert
            var session = await agent.SynchronizeAsync();

            Assert.Equal(1, session.TotalChangesDownloaded);
            Assert.Equal(0, session.TotalChangesUploaded);
        }

        [Theory, ClassData(typeof(InlineConfigurations)), TestPriority(8)]
        public async Task DeleteFromClient(SyncConfiguration conf)
        {
            conf.Add(fixture.Tables);
            configurationProvider = () => conf;
            // provision client infrastructure - otherwise this test will fail when run separately, because the sqlite db table won't yet exist
            await agent.SynchronizeAsync();

            long count;
            var selectcount = $@"Select count(*) From [ServiceTickets]";
            var updateRowScript = $@"Delete From [ServiceTickets]";

            using (var sqlConnection = new SqliteConnection(fixture.ClientSqliteConnectionString))
            {
                sqlConnection.Open();
                using (var sqlCmd = new SqliteCommand(selectcount, sqlConnection))
                    count = (long)sqlCmd.ExecuteScalar();
                using (var sqlCmd = new SqliteCommand(updateRowScript, sqlConnection))
                    sqlCmd.ExecuteNonQuery();
                sqlConnection.Close();
            }


            // Assert
            var session = await agent.SynchronizeAsync();


            Assert.Equal(0, session.TotalChangesDownloaded);
            Assert.Equal(count, session.TotalChangesUploaded);

            // check all rows deleted on server side
            using (var sqlConnection = new SqlConnection(fixture.ServerConnectionString))
            {
                sqlConnection.Open();
                using (var sqlCmd = new SqlCommand(selectcount, sqlConnection))
                    count = (int)sqlCmd.ExecuteScalar();
            }
            Assert.Equal(0, count);
        }

        [Theory, ClassData(typeof(InlineConfigurations)), TestPriority(9)]
        public async Task ConflictInsertInsertServerWins(SyncConfiguration conf)
        {
            conf.Add(fixture.Tables);
            configurationProvider = () => conf;
            // provision client infrastructure - otherwise this test will fail when run separately, because the sqlite db table won't yet exist
            await agent.SynchronizeAsync();

            Guid insertConflictId = Guid.NewGuid();

            using (var sqlConnection = new SqliteConnection(fixture.ClientSqliteConnectionString))
            {
                var script = $@"INSERT INTO [ServiceTickets] 
                            ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) 
                            VALUES 
                            (@id, 'Conflict Line Client', 'Description client', 1, 0, datetime('now'), NULL, 1)";

                using (var sqlCmd = new SqliteCommand(script, sqlConnection))
                {
                    sqlCmd.Parameters.AddWithValue("@id", insertConflictId);

                    sqlConnection.Open();
                    sqlCmd.ExecuteNonQuery();
                    sqlConnection.Close();
                }
            }
            
            using (var sqlConnection = new SqlConnection(fixture.ServerConnectionString))
            {
                // TODO : Convert to @parameter
                var script = $@"INSERT [ServiceTickets] 
                            ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) 
                            VALUES 
                            ('{insertConflictId.ToString()}', 'Conflict Line Server', 'Description client', 1, 0, getdate(), NULL, 1)";

                using (var sqlCmd = new SqlCommand(script, sqlConnection))
                {
                    sqlConnection.Open();
                    sqlCmd.ExecuteNonQuery();
                    sqlConnection.Close();
                }
            }
            
            var session = await agent.SynchronizeAsync();

            // check statistics
            Assert.Equal(1, session.TotalChangesDownloaded);
            Assert.Equal(1, session.TotalChangesUploaded);
            Assert.Equal(0, session.TotalSyncConflicts);

            string expectedRes = string.Empty;
            using (var sqlConnection = new SqliteConnection(fixture.ClientSqliteConnectionString))
            {
                var script = $@"Select Title from [ServiceTickets] 
                             Where ServiceTicketID=@id";

                using (var sqlCmd = new SqliteCommand(script, sqlConnection))
                {
                    sqlCmd.Parameters.AddWithValue("@id", insertConflictId);

                    sqlConnection.Open();
                    expectedRes = sqlCmd.ExecuteScalar() as string;
                    sqlConnection.Close();
                }
            }

            // check good title on client
            Assert.Equal("Conflict Line Server", expectedRes);
        }

        [Theory, ClassData(typeof(InlineConfigurations)), TestPriority(10)]
        public async Task ConflictUpdateUpdateServerWins(SyncConfiguration conf)
        {
            conf.Add(fixture.Tables);
            configurationProvider = () => conf;
            // provision client infrastructure - otherwise this test will fail when run separately, because the sqlite db table won't yet exist
            await agent.SynchronizeAsync();

            Guid updateConflictId = Guid.NewGuid();
            using (var sqlConnection = new SqliteConnection(fixture.ClientSqliteConnectionString))
            {
                var script = $@"INSERT INTO [ServiceTickets] 
                            ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) 
                            VALUES 
                            (@id, 'Line Client', 'Description client', 1, 0, datetime('now'), NULL, 1)";

                using (var sqlCmd = new SqliteCommand(script, sqlConnection))
                {
                    sqlCmd.Parameters.AddWithValue("@id", updateConflictId);

                    sqlConnection.Open();
                    sqlCmd.ExecuteNonQuery();
                    sqlConnection.Close();
                }
            }

            var session = await agent.SynchronizeAsync();

            // check statistics
            Assert.Equal(0, session.TotalChangesDownloaded);
            Assert.Equal(1, session.TotalChangesUploaded);
            Assert.Equal(0, session.TotalSyncConflicts);

            using (var sqlConnection = new SqliteConnection(fixture.ClientSqliteConnectionString))
            {
                var script = $@"Update [ServiceTickets] 
                                Set Title = 'Updated from Client'
                                Where ServiceTicketId = @id";

                using (var sqlCmd = new SqliteCommand(script, sqlConnection))
                {
                    sqlCmd.Parameters.AddWithValue("@id", updateConflictId);

                    sqlConnection.Open();
                    sqlCmd.ExecuteNonQuery();
                    sqlConnection.Close();
                }
            }

            using (var sqlConnection = new SqlConnection(fixture.ServerConnectionString))
            {
                var script = $@"Update [ServiceTickets] 
                                Set Title = 'Updated from Server'
                                Where ServiceTicketId = '{updateConflictId.ToString()}'";

                using (var sqlCmd = new SqlCommand(script, sqlConnection))
                {
                    sqlConnection.Open();
                    sqlCmd.ExecuteNonQuery();
                    sqlConnection.Close();
                }
            }

            var session2 = await agent.SynchronizeAsync();

            // check statistics
            Assert.Equal(1, session2.TotalChangesDownloaded);
            Assert.Equal(1, session2.TotalChangesUploaded);
            Assert.Equal(0, session2.TotalSyncConflicts);

            string expectedRes = string.Empty;
            using (var sqlConnection = new SqliteConnection(fixture.ClientSqliteConnectionString))
            {
                var script = $@"Select Title from [ServiceTickets] 
                            Where ServiceTicketID=@id";

                using (var sqlCmd = new SqliteCommand(script, sqlConnection))
                {
                    sqlCmd.Parameters.AddWithValue("@id", updateConflictId);

                    sqlConnection.Open();
                    expectedRes = sqlCmd.ExecuteScalar() as string;
                    sqlConnection.Close();
                }
            }

            // check good title on client
            Assert.Equal("Updated from Server", expectedRes);
        }

        [Theory, ClassData(typeof(InlineConfigurations)), TestPriority(11)]
        public async Task ConflictUpdateUpdateClientWins(SyncConfiguration conf)
        {
            conf.Add(fixture.Tables);
            configurationProvider = () => conf;
            // provision client infrastructure - otherwise this test will fail when run separately, because the sqlite db table won't yet exist
            await agent.SynchronizeAsync();

            var id = Guid.NewGuid().ToString();

            using (var sqlConnection = new SqliteConnection(fixture.ClientSqliteConnectionString))
            {
                var script = $@"INSERT INTO [ServiceTickets] 
                            ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) 
                            VALUES 
                            (@id, 'Line for conflict', 'Description client', 1, 0, datetime('now'), NULL, 1)";

                using (var sqlCmd = new SqliteCommand(script, sqlConnection))
                {
                    sqlCmd.Parameters.AddWithValue("@id", id);

                    sqlConnection.Open();
                    sqlCmd.ExecuteNonQuery();
                    sqlConnection.Close();
                }
            }

            var session = await agent.SynchronizeAsync();

            // check statistics
            Assert.Equal(0, session.TotalChangesDownloaded);
            Assert.Equal(1, session.TotalChangesUploaded);
            Assert.Equal(0, session.TotalSyncConflicts);

            using (var sqlConnection = new SqliteConnection(fixture.ClientSqliteConnectionString))
            {
                var script = $@"Update [ServiceTickets] 
                                Set Title = 'Updated from Client'
                                Where ServiceTicketId = @id";

                using (var sqlCmd = new SqliteCommand(script, sqlConnection))
                {
                    sqlCmd.Parameters.AddWithValue("@id", id);

                    sqlConnection.Open();
                    sqlCmd.ExecuteNonQuery();
                    sqlConnection.Close();
                }
            }

            using (var sqlConnection = new SqlConnection(fixture.ServerConnectionString))
            {
                var script = $@"Update [ServiceTickets] 
                                Set Title = 'Updated from Server'
                                Where ServiceTicketId = '{id}'";

                using (var sqlCmd = new SqlCommand(script, sqlConnection))
                {
                    sqlConnection.Open();
                    sqlCmd.ExecuteNonQuery();
                    sqlConnection.Close();
                }
            }

            // Since we move to server side, it's server to handle errors
            proxyServerProvider.ApplyChangedFailed += (s, args) =>
            {
                args.Action = ConflictAction.ClientWins;
            };


            SyncContext session2 = null;
            await Assert.RaisesAsync<ApplyChangeFailedEventArgs>(
                h => proxyServerProvider.ApplyChangedFailed += h,
                h => proxyServerProvider.ApplyChangedFailed -= h, async () =>
                {
                    session2 = await agent.SynchronizeAsync();
                });
            

            // check statistics
            Assert.Equal(0, session2.TotalChangesDownloaded);
            Assert.Equal(1, session2.TotalChangesUploaded);
            Assert.Equal(1, session2.TotalSyncConflicts);

            string expectedRes = string.Empty;
            using (var sqlConnection = new SqlConnection(fixture.ServerConnectionString))
            {
                var script = $@"Select Title from [ServiceTickets] Where ServiceTicketID='{id}'";

                using (var sqlCmd = new SqlCommand(script, sqlConnection))
                {
                    sqlConnection.Open();
                    expectedRes = sqlCmd.ExecuteScalar() as string;
                    sqlConnection.Close();
                }
            }

            // check good title on client
            Assert.Equal("Updated from Client", expectedRes);
        }

        [Theory, ClassData(typeof(InlineConfigurations)), TestPriority(12)]
        public async Task ConflictInsertInsertConfigurationClientWins(SyncConfiguration conf)
        {
            conf.ConflictResolutionPolicy = ConflictResolutionPolicy.ClientWins;
            conf.Add(fixture.Tables);
            configurationProvider = () => conf;
            // provision client infrastructure - otherwise this test will fail when run separately, because the sqlite db table won't yet exist
            await agent.SynchronizeAsync();

            Guid id = Guid.NewGuid();

            using (var sqlConnection = new SqliteConnection(fixture.ClientSqliteConnectionString))
            {
                var script = $@"INSERT INTO [ServiceTickets] 
                            ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) 
                            VALUES 
                            (@id, 'Conflict Line Client', 'Description client', 1, 0, datetime('now'), NULL, 1)";

                using (var sqlCmd = new SqliteCommand(script, sqlConnection))
                {
                    sqlCmd.Parameters.AddWithValue("@id", id);

                    sqlConnection.Open();
                    sqlCmd.ExecuteNonQuery();
                    sqlConnection.Close();
                }
            }

            using (var sqlConnection = new SqlConnection(fixture.ServerConnectionString))
            {
                var script = $@"INSERT [ServiceTickets] 
                            ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) 
                            VALUES 
                            ('{id.ToString()}', 'Conflict Line Server', 'Description client', 1, 0, getdate(), NULL, 1)";

                using (var sqlCmd = new SqlCommand(script, sqlConnection))
                {
                    sqlConnection.Open();
                    sqlCmd.ExecuteNonQuery();
                    sqlConnection.Close();
                }
            }

            // Act
            var session = await agent.SynchronizeAsync();

            // check statistics
            Assert.Equal(0, session.TotalChangesDownloaded);
            Assert.Equal(1, session.TotalChangesUploaded);
            Assert.Equal(1, session.TotalSyncConflicts);

            string expectedRes = string.Empty;
            using (var sqlConnection = new SqlConnection(fixture.ServerConnectionString))
            {
                var script = $@"Select Title from [ServiceTickets] Where ServiceTicketID='{id.ToString()}'";

                using (var sqlCmd = new SqlCommand(script, sqlConnection))
                {
                    sqlConnection.Open();
                    expectedRes = sqlCmd.ExecuteScalar() as string;
                    sqlConnection.Close();
                }
            }

            // check good title on client
            Assert.Equal("Conflict Line Client", expectedRes);
        }
        
        [Theory, ClassData(typeof(InlineConfigurations)), TestPriority(13)]
        public async Task InsertAndUpdateOnClient_DataIsSentToServer(SyncConfiguration conf)
        {
            conf.Add(fixture.Tables);
            configurationProvider = () => conf;
            // provision client infrastructure - otherwise this test will fail when run separately, because the sqlite db table won't yet exist
            await agent.SynchronizeAsync();

            Guid newId = Guid.NewGuid();

            var insertRowScript =
                $@"INSERT INTO [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) 
                VALUES (@id, 'Insert and Update locally before syncing', 'Description Insert One Row', 1, 0, datetime('now'), NULL, 1)";
            int nbRowsInserted = 0;
            using (var sqlConnection = new SqliteConnection(fixture.ClientSqliteConnectionString))
            {
                using (var sqlCmd = new SqliteCommand(insertRowScript, sqlConnection))
                {
                    sqlCmd.Parameters.AddWithValue("@id", newId);

                    sqlConnection.Open();
                    nbRowsInserted = sqlCmd.ExecuteNonQuery();
                    sqlConnection.Close();
                }
            }
            if (nbRowsInserted < 0)
                throw new Exception("Row not inserted");

            // next.. we do an update
            var localDescription = "A local update not on the server yet!";
            var updateRowScript =
                $@"UPDATE [ServiceTickets] set [Description] = '{localDescription}' where ServiceTicketID = @id";
            using (var sqlConnection = new SqliteConnection(fixture.ClientSqliteConnectionString))
            {
                using (var sqlCmd = new SqliteCommand(updateRowScript, sqlConnection))
                {
                    sqlCmd.Parameters.AddWithValue("@id", newId);

                    sqlConnection.Open();
                    nbRowsInserted = sqlCmd.ExecuteNonQuery();
                    sqlConnection.Close();
                }
            }
            if (nbRowsInserted < 0)
                throw new Exception("Row not updated");

            // Act
            var session1 = await agent.SynchronizeAsync();

            // Assert
            // Now we check, if the update of the local row was actually applied on the server!
            var selectOnServerScript = "Select [Description] from ServiceTickets where [ServiceTicketID] = @id";
            using (var sqlConnection = new SqlConnection(fixture.ServerConnectionString))
            {
                using (var sqlCmd = new SqlCommand(selectOnServerScript, sqlConnection))
                {
                    sqlCmd.Parameters.AddWithValue("@id", newId);
                    sqlConnection.Open();
                    var result = sqlCmd.ExecuteScalar();
                    var description = (string)result;

                    Assert.Equal(localDescription, description);// "local update was NOT sent to the server!!"

                    sqlConnection.Close();
                }
            }


            Assert.Equal(0, session1.TotalChangesDownloaded);
            Assert.Equal(1, session1.TotalChangesUploaded);
            Assert.Equal(0, session1.TotalSyncConflicts);
            Assert.Equal(0, session1.TotalSyncErrors);
        }

        [Theory, ClassData(typeof(InlineConfigurations)), TestPriority(14)]
        public async Task InsertFromClient_WhenConnectionInterrupted_ThenUpdatedOnClientAndRetried_UpdateIsSentToServer(SyncConfiguration conf)
        {
            conf.Add(fixture.Tables);
            configurationProvider = () => conf;
            // provision client infrastructure - otherwise this test will fail when run separately, because the sqlite db table won't yet exist
            await agent.SynchronizeAsync();

            Guid newId = Guid.NewGuid();

            var insertRowScript =
                $@"INSERT INTO [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) 
                VALUES (@id, 'Insert One Row in Sqlite client', 'Description Insert One Row', 1, 0, datetime('now'), NULL, 1)";

            int nbRowsInserted = 0;

            using (var sqlConnection = new SqliteConnection(fixture.ClientSqliteConnectionString))
            {
                using (var sqlCmd = new SqliteCommand(insertRowScript, sqlConnection))
                {
                    sqlCmd.Parameters.AddWithValue("@id", newId);

                    sqlConnection.Open();
                    nbRowsInserted = sqlCmd.ExecuteNonQuery();
                    sqlConnection.Close();
                }
            }
            if (nbRowsInserted < 0)
                throw new Exception("Row not inserted");

            var scopeSaved = new EventHandler<ScopeEventArgs>((sen, arg) =>
            {
                throw new InvalidOperationException("Could not obtain lock");
            });

            agent.ScopeSaved += scopeSaved;
            SyncContext session1 = null;
            try
            {
                session1 = await agent.SynchronizeAsync();
            }
            catch (SyncException)
            {
                agent.ScopeSaved -= scopeSaved;
            }

            // now the local change was actually INSERTED on the server, 
            // but as the sync process was never completed, we do not know on the client, that we actually already sent the data

            // next.. we do an update
            var localDescription = "A local update not on the server yet!";
            var updateRowScript =
                $@"UPDATE [ServiceTickets] set [Description] = '{localDescription}' where ServiceTicketID = @id";

            using (var sqlConnection = new SqliteConnection(fixture.ClientSqliteConnectionString))
            {
                using (var sqlCmd = new SqliteCommand(updateRowScript, sqlConnection))
                {
                    sqlCmd.Parameters.AddWithValue("@id", newId);

                    sqlConnection.Open();
                    nbRowsInserted = sqlCmd.ExecuteNonQuery();
                    sqlConnection.Close();
                }
            }
            if (nbRowsInserted < 0)
                throw new Exception("Row not updated");


            // Act
            session1 = await agent.SynchronizeAsync();

            // Assert
            // Now we check, if the update of the local row was actually applied on the server!
            var selectOnServerScript = "Select [Description] from ServiceTickets where [ServiceTicketID] = @id";
            using (var sqlConnection = new SqlConnection(fixture.ServerConnectionString))
            {
                using (var sqlCmd = new SqlCommand(selectOnServerScript, sqlConnection))
                {
                    sqlCmd.Parameters.AddWithValue("@id", newId);
                    sqlConnection.Open();
                    var result = sqlCmd.ExecuteScalar();
                    var description = (string)result;

                    Assert.Equal(localDescription, description);// "local update was NOT sent to the server!!"

                    sqlConnection.Close();
                }
            }


            Assert.Equal(0, session1.TotalChangesDownloaded);
            Assert.Equal(1, session1.TotalChangesUploaded);
            Assert.Equal(0, session1.TotalSyncConflicts);
            Assert.Equal(0, session1.TotalSyncErrors);
        }

        [Theory, ClassData(typeof(InlineConfigurations)), TestPriority(15)]
        public async Task InsertFromServer_ThenUpdatedOnServer_UpdateIsSentToClient(SyncConfiguration conf)
        {
            conf.Add(fixture.Tables);
            configurationProvider = () => conf;
            // provision client infrastructure - otherwise this test will fail when run separately, because the sqlite db table won't yet exist
            await agent.SynchronizeAsync();

            Guid newId = Guid.NewGuid();

            var insertRowScript =
                $@"INSERT INTO [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) 
                VALUES (@id, 'Insert One Row in Sqlite client', 'Description Insert One Row', 1, 0, getutcdate(), NULL, 1)";

            int nbRowsInserted = 0;
            using (var sqlConnection = new SqlConnection(fixture.ServerConnectionString))
            {
                using (var sqlCmd = new SqlCommand(insertRowScript, sqlConnection))
                {
                    sqlCmd.Parameters.AddWithValue("@id", newId);

                    sqlConnection.Open();
                    nbRowsInserted = sqlCmd.ExecuteNonQuery();
                    sqlConnection.Close();
                }
            }
            if (nbRowsInserted < 0)
                throw new Exception("Row not inserted");

            // throw exception, when server scope is saved
            var scopeSaved = new EventHandler<ScopeEventArgs>((sen, arg) =>
            {
                throw new InvalidOperationException("Could not obtain lock");
            });

            SyncException exception = null;
            try
            {
                serverProvider.ScopeSaved += scopeSaved;
                await agent.SynchronizeAsync();
            }
            catch (SyncException x)
            {
                exception = x;
                serverProvider.ScopeSaved -= scopeSaved;
            }

            // now the local change was actually INSERTED on the client, 
            // but as the sync process was never completed, we do not know on the server, that we actually already received the data
            Assert.NotNull(exception); // exception is expected!

            // next.. we do an update
            var localDescription = "A local update not on the client yet!";
            var updateRowScript =
                $@"UPDATE [ServiceTickets] set [Description] = '{localDescription}' where ServiceTicketID = @id";

            using (var sqlConnection = new SqlConnection(fixture.ServerConnectionString))
            {
                using (var sqlCmd = new SqlCommand(updateRowScript, sqlConnection))
                {
                    sqlCmd.Parameters.AddWithValue("@id", newId);

                    sqlConnection.Open();
                    nbRowsInserted = sqlCmd.ExecuteNonQuery();
                    sqlConnection.Close();
                }
            }
            if (nbRowsInserted < 0)
                throw new Exception("Row not updated");


            // Act
            var session1 = await agent.SynchronizeAsync();

            // Assert
            // Now we check, if the update of the local row was actually applied on the server!
            var selectOnServerScript = "Select Description from ServiceTickets where ServiceTicketID = @id";
            using (var sqlConnection = new SqliteConnection(fixture.ClientSqliteConnectionString))
            {
                using (var sqlCmd = new SqliteCommand(selectOnServerScript, sqlConnection))
                {
                    sqlCmd.Parameters.AddWithValue("@id", newId);
                    sqlConnection.Open();
                    var result = sqlCmd.ExecuteScalar();
                    var description = (string)result;

                    Assert.Equal(localDescription, description);// "server update was NOT sent to the client!!"

                    sqlConnection.Close();
                }
            }


            Assert.Equal(1, session1.TotalChangesDownloaded);
            Assert.Equal(0, session1.TotalChangesUploaded);
            Assert.Equal(0, session1.TotalSyncConflicts); //no more unnecessary conflicts on client side
            Assert.Equal(0, session1.TotalSyncErrors);
        }

        [Theory, ClassData(typeof(InlineConfigurations)), TestPriority(16)]
        public async Task InsertAndUpdateOnServer_DataIsSyncedToClient(SyncConfiguration conf)
        {
            conf.Add(fixture.Tables);
            configurationProvider = () => conf;
            // provision client infrastructure - otherwise this test will fail when run separately, because the sqlite db table won't yet exist
            await agent.SynchronizeAsync();

            Guid newId = Guid.NewGuid();

            var insertRowScript =
                $@"INSERT INTO [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) 
                VALUES (@id, 'Insert One Row in Sqlite client', 'Description Insert One Row', 1, 0, getutcdate(), NULL, 1)";

            int nbRowsInserted = 0;
            using (var sqlConnection = new SqlConnection(fixture.ServerConnectionString))
            {
                using (var sqlCmd = new SqlCommand(insertRowScript, sqlConnection))
                {
                    sqlCmd.Parameters.AddWithValue("@id", newId);

                    sqlConnection.Open();
                    nbRowsInserted = sqlCmd.ExecuteNonQuery();
                    sqlConnection.Close();
                }
            }
            if (nbRowsInserted < 0)
                throw new Exception("Row not inserted");

            
            // next.. we do an update
            var localDescription = "Another server update not on the client yet!";
            var updateRowScript =
                $@"UPDATE [ServiceTickets] set [Description] = '{localDescription}' where ServiceTicketID = @id";

            using (var sqlConnection = new SqlConnection(fixture.ServerConnectionString))
            {
                using (var sqlCmd = new SqlCommand(updateRowScript, sqlConnection))
                {
                    sqlCmd.Parameters.AddWithValue("@id", newId);

                    sqlConnection.Open();
                    nbRowsInserted = sqlCmd.ExecuteNonQuery();
                    sqlConnection.Close();
                }
            }
            if (nbRowsInserted < 0)
                throw new Exception("Row not updated");


            // Act
            var session1 = await agent.SynchronizeAsync();

            // Assert
            // Now we check, if the update of the local row was actually applied on the server!
            var selectOnServerScript = "Select Description from ServiceTickets where ServiceTicketID = @id";
            using (var sqlConnection = new SqliteConnection(fixture.ClientSqliteConnectionString))
            {
                using (var sqlCmd = new SqliteCommand(selectOnServerScript, sqlConnection))
                {
                    sqlCmd.Parameters.AddWithValue("@id", newId);
                    sqlConnection.Open();
                    var result = sqlCmd.ExecuteScalar();
                    var description = (string)result;

                    Assert.Equal(localDescription, description);// "server update was NOT sent to the client!!"

                    sqlConnection.Close();
                }
            }


            Assert.Equal(1, session1.TotalChangesDownloaded);
            Assert.Equal(0, session1.TotalChangesUploaded);
            Assert.Equal(0, session1.TotalSyncConflicts);
            Assert.Equal(0, session1.TotalSyncErrors);
        }
        
        public void Dispose()
        {
            proxyClientProvider?.Dispose();
            agent?.Dispose();
            webApp?.Dispose();

            
            // IF the server directory still is there, it should at least be empty
            var serverDir = Path.Combine(this.batchDir, "server");
            if(Directory.Exists(serverDir))
                Assert.Equal(Directory.EnumerateFileSystemEntries(serverDir).Count(), 0); // "temporary data should be deleted"

            // IF the client directory still is there, it should at least be empty
            var clientDir = Path.Combine(this.batchDir, "client");
            if (Directory.Exists(clientDir))
                Assert.Equal(Directory.EnumerateFileSystemEntries(clientDir).Count(), 0); // "temporary data should be deleted"


            if (Directory.Exists(this.batchDir))
                Directory.Delete(this.batchDir, true);
        }
    }
}

