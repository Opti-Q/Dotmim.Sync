using System;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
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
using Shouldly;
using SQLite;
using Xunit;

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

            clientProvider = new SqliteSyncProvider(fixture.ClientSqliteFilePath);
            proxyClientProvider = new WebProxyClientProvider(new Uri(fixture.BaseAddress, "api/values"));

            agent = new SyncAgent(clientProvider, proxyClientProvider);
            agent.Configuration.BatchDirectory = Path.Combine(batchDir, "client");
        }

        [Fact, TestPriority(1)]
        public async Task ReproduceSyncIssue()
        {
            // Arrange
            var session0 = await agent.SynchronizeAsync();

            var ev0 = new SemaphoreSlim(0, 1);
            var ev1 = new SemaphoreSlim(0, 1);
            var ev2 = new SemaphoreSlim(0, 1);
            var ev3 = new SemaphoreSlim(0, 1);

            // create brand new client
            clientProvider = new SqliteSyncProvider(fixture.ClientSqliteFilePath);
            proxyClientProvider = new WebProxyClientProvider(new Uri(fixture.BaseAddress, "api/values"));
            agent = new SyncAgent(clientProvider, proxyClientProvider);
            agent.Configuration.BatchDirectory = Path.Combine(batchDir, "client");
            agent.TableChangesSelecting += (s,e) =>
            {
                ev1.Release();
                ev2.Wait();
            };
            agent.TableChangesSelected += (s, e) =>
            {
                ev3.Release();
            };

            SyncContext t0Session = null;
            var t0 = Task.Run(async () =>
            {
                await ev0.WaitAsync();
                t0Session = await agent.SynchronizeAsync();
            });

            var t1 = Task.Run(async () =>
            {
                using (var db = new SQLiteConnection(fixture.ClientSqliteFilePath, SQLiteOpenFlags.Create|SQLiteOpenFlags.FullMutex|SQLiteOpenFlags.ReadWrite, true, true))
                {
                    ev0.Release();
                    await ev1.WaitAsync();

                    db.CreateTable<ServiceTicket>();
                    var a = new ServiceTicket
                    {
                        ServiceTicketId = Guid.NewGuid(),
                        Closed = DateTime.UtcNow,
                        Title = "inserted locally",
                        Description = "description from locally"
                    };

                    db.BeginTransaction();
                    db.Insert(a);

                    var ts = db.ExecuteScalar<string>("Select timestamp from ServiceTickets_tracking where ServiceTicketID = ?", a.ServiceTicketId);

                    ev2.Release();
                    await ev3.WaitAsync();
                    
                    db.Commit();
                }
            });

            // Act
            await Task.WhenAll(t0, t1);

            // Assert
            t0Session.ShouldNotBeNull();
            
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
    
    [SQLite.Table("ServiceTickets")]
    public class ServiceTicket
    {
        [SQLite.PrimaryKey]
        [SQLite.Column("ServiceTicketID")]
        public Guid ServiceTicketId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public int StatusValue { get; set; }
        public int EscalationLevel{ get; set; }
        public DateTime Opened{ get; set; }
        public DateTime Closed{ get; set; }
        [Column("CustomerID")]
        public int CustomerId { get; set; }
    }

}

