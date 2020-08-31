﻿using DotmimSyncLegacy.Enumerations;
using DotmimSyncLegacy.SqlServer;
using DotmimSyncLegacy.Tests.Misc;
using DotmimSyncLegacy.Test.SqlUtils;
using System;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Xunit;

namespace DotmimSyncLegacy.Test
{
    public class SyncOneTableOneFieldFixture : IDisposable
    {
        private string createTableScript =
        $@"if (not exists (select * from sys.tables where name = 'Posts'))
            begin
                CREATE TABLE [dbo].[Posts](
	                [PostId] [int] IDENTITY(1,1) NOT NULL,
	                [Title] [nvarchar](max) NULL,
	                CONSTRAINT [PK_Posts] PRIMARY KEY CLUSTERED ([PostId] ASC) ON [PRIMARY]
                )
            end
    
        if (not exists (select * from sys.tables where name = 'Tags'))
            begin
                CREATE TABLE [dbo].[Tags](
	                [TagId] [int] IDENTITY(1,1) NOT NULL,
	                [Text] [nvarchar](max) NULL,
                        CONSTRAINT [PK_Tags] PRIMARY KEY CLUSTERED ([TagId] ASC) ON [PRIMARY]
                )
         end
        if (not exists (select * from sys.tables where name = 'PostTag'))
            begin
                CREATE TABLE [dbo].[PostTag](
	                [PostId] [int] NOT NULL,
	                [TagId] [int] NOT NULL,
	                CONSTRAINT [PK_PostTag] PRIMARY KEY CLUSTERED ([PostId] ASC, [TagId] ASC) ON [PRIMARY]
                )
            end
        ";

        private string datas =
        $@"
            SET IDENTITY_INSERT [dbo].[Posts] ON 
            INSERT [dbo].[Posts] ([PostId], [Title]) VALUES (1, N'Best Boutiques on the Eastside')
            INSERT [dbo].[Posts] ([PostId], [Title]) VALUES (2, N'Avoiding over-priced Hipster joints')
            INSERT [dbo].[Posts] ([PostId], [Title]) VALUES (3, N'Where to buy Mars Bars')
            SET IDENTITY_INSERT [dbo].[Posts] OFF
            SET IDENTITY_INSERT [dbo].[Tags] ON 
            INSERT [dbo].[Tags] ([TagId], [Text]) VALUES (1, N'Golden')
            INSERT [dbo].[Tags] ([TagId], [Text]) VALUES (2, N'Pineapple')
            INSERT [dbo].[Tags] ([TagId], [Text]) VALUES (3, N'Girlscout')
            INSERT [dbo].[Tags] ([TagId], [Text]) VALUES (4, N'Cookies')
            SET IDENTITY_INSERT [dbo].[Tags] OFF
            INSERT [dbo].[PostTag] ([PostId], [TagId]) VALUES (1, 1)
            INSERT [dbo].[PostTag] ([PostId], [TagId]) VALUES (3, 1)
            INSERT [dbo].[PostTag] ([PostId], [TagId]) VALUES (1, 2)
            INSERT [dbo].[PostTag] ([PostId], [TagId]) VALUES (3, 2)
            INSERT [dbo].[PostTag] ([PostId], [TagId]) VALUES (2, 3)
            INSERT [dbo].[PostTag] ([PostId], [TagId]) VALUES (3, 3)
            INSERT [dbo].[PostTag] ([PostId], [TagId]) VALUES (2, 4)
            INSERT [dbo].[PostTag] ([PostId], [TagId]) VALUES (3, 4)
        ";

        private HelperDB helperDb = new HelperDB();
        private string serverDbName = "Test_Simple_Server_OneField";
        private string client1DbName = "Test_Simple_Client_OneField";

        public string[] Tables => new string[] { "Posts", "Tags", "PostTag" };

        public String ServerConnectionString => HelperDB.GetDatabaseConnectionString(serverDbName);
        public String Client1ConnectionString => HelperDB.GetDatabaseConnectionString(client1DbName);

        public SyncOneTableOneFieldFixture()
        {
            // create databases
            helperDb.CreateDatabase(serverDbName);
            helperDb.CreateDatabase(client1DbName);

            // create table
            helperDb.ExecuteScript(serverDbName, createTableScript);

            // insert table
            helperDb.ExecuteScript(serverDbName, datas);
        }
        public void Dispose()
        {
            helperDb.DeleteDatabase(serverDbName);
            helperDb.DeleteDatabase(client1DbName);
        }

    }


    [TestCaseOrderer("DotmimSyncLegacy.Tests.Misc.PriorityOrderer", "Dotmim.Sync.Tests")]
    public class SyncOneTableOneFieldTests : IClassFixture<SyncOneTableOneFieldFixture>
    {
        SqlSyncProvider serverProvider;
        SqlSyncProvider clientProvider;
        SyncOneTableOneFieldFixture fixture;
        SyncAgent agent;

        public SyncOneTableOneFieldTests(SyncOneTableOneFieldFixture fixture)
        {
            this.fixture = fixture;

            serverProvider = new SqlSyncProvider(fixture.ServerConnectionString);
            clientProvider = new SqlSyncProvider(fixture.Client1ConnectionString);

            agent = new SyncAgent(clientProvider, serverProvider, fixture.Tables);
        }

        [Fact, TestPriority(0)]
        public async Task Initialize()
        {
            var session = await agent.SynchronizeAsync();

            Assert.Equal(15, session.TotalChangesDownloaded);
            Assert.Equal(0, session.TotalChangesUploaded);
        }

        [Fact, TestPriority(1)]
        public async Task InsertFromServer()
        {
            var insertRowScript =
            $@"
            SET IDENTITY_INSERT [dbo].[Posts] ON 
            INSERT [dbo].[Posts] ([PostId], [Title]) VALUES (11, N'Wines company')
            SET IDENTITY_INSERT [dbo].[Posts] OFF 

            SET IDENTITY_INSERT [dbo].[Tags] ON 
            INSERT [dbo].[Tags] ([TagId], [Text]) VALUES (12, N'Wine')
            SET IDENTITY_INSERT [dbo].[Tags] OFF

            INSERT [dbo].[PostTag] ([PostId], [TagId]) VALUES (11, 12)

            ";

            using (var sqlConnection = new SqlConnection(fixture.ServerConnectionString))
            {
                using (var sqlCmd = new SqlCommand(insertRowScript, sqlConnection))
                {
                    sqlConnection.Open();
                    sqlCmd.ExecuteNonQuery();
                    sqlConnection.Close();
                }
            }
            var session = await agent.SynchronizeAsync();

            Assert.Equal(3, session.TotalChangesDownloaded);
            Assert.Equal(0, session.TotalChangesUploaded);
        }

        [Fact, TestPriority(2)]
        public async Task UpdateFromServer()
        {
            var insertRowScript =
            $@"
            UPDATE [dbo].[Posts] SET Title = 'Wines Luxury Company' Where PostId = 11

            SET IDENTITY_INSERT [dbo].[Tags] ON 
            INSERT [dbo].[Tags] ([TagId], [Text]) VALUES (13, N'Deluxe White Wine')
            SET IDENTITY_INSERT [dbo].[Tags] OFF

            INSERT [dbo].[PostTag] ([PostId], [TagId]) VALUES (11, 13)

            ";

            using (var sqlConnection = new SqlConnection(fixture.ServerConnectionString))
            {
                using (var sqlCmd = new SqlCommand(insertRowScript, sqlConnection))
                {
                    sqlConnection.Open();
                    sqlCmd.ExecuteNonQuery();
                    sqlConnection.Close();
                }
            }
            var session = await agent.SynchronizeAsync();

            Assert.Equal(3, session.TotalChangesDownloaded);
            Assert.Equal(0, session.TotalChangesUploaded);
        }

        [Fact, TestPriority(3)]
        public async Task DeleteFromClient()
        {
            var insertRowScript =
            $@"
            DELETE [dbo].[PostTag] WHERE [PostId]= 11 AND [TagId]= 13
            ";

            using (var sqlConnection = new SqlConnection(fixture.Client1ConnectionString))
            {
                using (var sqlCmd = new SqlCommand(insertRowScript, sqlConnection))
                {
                    sqlConnection.Open();
                    sqlCmd.ExecuteNonQuery();
                    sqlConnection.Close();
                }
            }
            var session = await agent.SynchronizeAsync();

            Assert.Equal(0, session.TotalChangesDownloaded);
            Assert.Equal(1, session.TotalChangesUploaded);
        }

        [Fact, TestPriority(4)]
        public async Task DeleteFromServer()
        {
            var insertRowScript =
            $@"
            DELETE [dbo].[PostTag] WHERE [PostId]= 11 AND [TagId]= 12
            DELETE [dbo].[Tags] WHERE [TagId]= 12
            DELETE [dbo].[Posts] WHERE [PostId]= 11";

            using (var sqlConnection = new SqlConnection(fixture.ServerConnectionString))
            {
                using (var sqlCmd = new SqlCommand(insertRowScript, sqlConnection))
                {
                    sqlConnection.Open();
                    sqlCmd.ExecuteNonQuery();
                    sqlConnection.Close();
                }
            }
            var session = await agent.SynchronizeAsync();

            Assert.Equal(3, session.TotalChangesDownloaded);
            Assert.Equal(0, session.TotalChangesUploaded);
        }
    }
}
