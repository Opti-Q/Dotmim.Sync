using DotmimSyncLegacy.Manager;
using System;
using System.Collections.Generic;
using System.Text;
using System.Data.Common;


namespace DotmimSyncLegacy.MySql
{
    public class MySqlManager : DbManager
    {
        public MySqlManager(string tableName) : base(tableName)
        {

        }

        public override IDbManagerTable CreateManagerTable(DbConnection connection, DbTransaction transaction = null)
        {
            return new MySqlManagerTable(connection, transaction);
        }


    }
}
