using System;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Dispatcher;
using DotmimSyncLegacy.Builders;
using DotmimSyncLegacy.Enumerations;
using DotmimSyncLegacy.Sqlite;
using DotmimSyncLegacy.SqlServer;
using DotmimSyncLegacy.Test.SqlUtils;
using DotmimSyncLegacy.Tests.Misc;
using DotmimSyncLegacy.Web.Client;
using DotmimSyncLegacy.Web.Server;
using Microsoft.Data.Sqlite;
using Microsoft.Owin.Hosting;
using Owin;
using Xunit;
using Xunit.Abstractions;

namespace DotmimSyncLegacy.Tests
{
    public class SqliteSyncHttpLoadFixture : IDisposable
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
            INSERT [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) VALUES (newid(), 'Titre 3', ' ', 1, 0, CAST('2016-07-29T16:36:41.733' AS DateTime), NULL, 1)
            INSERT [ServiceTickets] ([ServiceTicketID], [Title], [Description], [StatusValue], [EscalationLevel], [Opened], [Closed], [CustomerID]) VALUES (newid(), 'Titre 4 Lorem ipsum dolor sit amet, consetetur sadipscing elitr, sed diam nonumy eirmod tempor invidunt ut labore et dolore magna aliquyam erat, sed diam voluptua. At vero eos et accusam et justo duo dolores et ea rebum. Stet clita kasd gubergren, no sea takimata sanctus est Lorem ipsum dolor sit amet. Lorem ipsum dolor sit amet, consetetur sadipscing elitr, sed diam nonumy eirmod tempor invidunt ut labore et dolore magna aliquyam erat, sed diam voluptua. At vero eos et accusam et justo duo dolores et ea rebum. Stet clita kasd gubergren, no sea takimata sanctus est Lorem ipsum dolor sit amet.', 'Description Lorem ipsum dolor sit amet, consetetur sadipscing elitr, sed diam nonumy eirmod tempor invidunt ut labore et dolore magna aliquyam erat, sed diam voluptua. At vero eos et accusam et justo duo dolores et ea rebum. Stet clita kasd gubergren, no sea takimata sanctus est Lorem ipsum dolor sit amet. Lorem ipsum dolor sit amet, consetetur sadipscing elitr, sed diam nonumy eirmod tempor invidunt ut labore et dolore magna aliquyam erat, sed diam voluptua. At vero eos et accusam et justo duo dolores et ea rebum. Stet clita kasd gubergren, no sea takimata sanctus est Lorem ipsum dolor sit amet.', 1, 0, CAST('2016-07-29T16:36:41.733' AS DateTime), NULL, 1)
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
        public string ClientSqliteFilePath => Path.Combine(Directory.GetCurrentDirectory(), "sqliteHttpTmpDb_webApi2_load.db");

        public Uri BaseAddress => new Uri(baseAddress);

        public string ServerDbName { get; } = "Test_Sqlite_Http_Server_WebApi2_Load";
        private HelperDB helperDb = new HelperDB();

        public int Multiplier { get; } = 500;

        public SqliteSyncHttpLoadFixture()
        {
            baseAddress = "http://localhost:9903";

            var builder = new SqliteConnectionStringBuilder { DataSource = ClientSqliteFilePath };
            this.ClientSqliteConnectionString = builder.ConnectionString;


            if (File.Exists(ClientSqliteFilePath))
                File.Delete(ClientSqliteFilePath);

            // create databases
            helperDb.CreateDatabase(ServerDbName);
            // create table
            helperDb.ExecuteScript(ServerDbName, createTableScript);

            for (int i = 0; i < Multiplier; i++)
            {
                // insert table
                helperDb.ExecuteScript(ServerDbName, datas);
            }

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


    [Collection("Http")]
    [TestCaseOrderer("DotmimSyncLegacy.Tests.Misc.PriorityOrderer", "Dotmim.Sync.WebApi2.Tests")]
    public class SqliteSyncHttpLoadTests : IClassFixture<SqliteSyncHttpLoadFixture>, IDisposable
    {
        SqlSyncProvider serverProvider;
        SqliteSyncProvider clientProvider;
        SqliteSyncHttpLoadFixture fixture;
        private readonly ITestOutputHelper output;
        WebProxyServerProvider proxyServerProvider;
        WebProxyClientProvider proxyClientProvider;
        SyncAgent agent;
        private Func<SyncConfiguration> configurationProvider;
        private IDisposable webApp;
        private string batchDir;

        public SqliteSyncHttpLoadTests(SqliteSyncHttpLoadFixture fixture, ITestOutputHelper output)
        {
            this.fixture = fixture;
            this.output = output;
            this.batchDir = Path.Combine(Environment.CurrentDirectory, Guid.NewGuid().ToString("N"));

            configurationProvider = () => new SyncConfiguration(fixture.Tables)
            {
                //SerializationFormat = SerializationFormat.Binary,
                SerializationFormat = SerializationFormat.Json,
                DownloadBatchSizeInKB = 400,
            };

            serverProvider = new SqlSyncProvider(fixture.ServerConnectionString);
            proxyServerProvider = new WebProxyServerProvider(serverProvider);

            webApp = WebApp.Start(fixture.BaseAddress.OriginalString, (appBuilder) =>
            {
                // Configure Web API for self-host. 
                HttpConfiguration config = new HttpConfiguration();
                config.Routes.MapHttpRoute(
                    name: "DefaultApi",
                    routeTemplate: "api/{controller}/{actionid}/{id}",
                    defaults: new {actionid = RouteParameter.Optional, id = RouteParameter.Optional}
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
        public async Task LoadLotsOfRowsFromServer()
        {
            var expectedCount = 50 * fixture.Multiplier;

            var sw = new Stopwatch();
            sw.Start();

            // Act
            agent.Configuration.DownloadBatchSizeInKB = 400; // no batching
            var session = await agent.SynchronizeAsync();

            sw.Stop();

            this.output.WriteLine($"Synchronized {session.TotalChangesDownloaded} rows in {sw.Elapsed}");

            var onp = new ObjectNameParser();
            SqliteSyncAdapter a;

            // Assert
            Assert.Equal(expectedCount, session.TotalChangesDownloaded);
            Assert.Equal(0, session.TotalChangesUploaded);
        }

        public void Dispose()
        {
            proxyClientProvider?.Dispose();
            agent?.Dispose();
            webApp?.Dispose();


            // IF the server directory still is there, it should at least be empty
            var serverDir = Path.Combine(this.batchDir, "server");
            if (Directory.Exists(serverDir))
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

