using DotmimSyncLegacy.Manager;
using System.Data.Common;

namespace DotmimSyncLegacy.SqlServer.Manager
{
    public class SqlManager : DbManager
    {

        public SqlManager(string tableName): base(tableName)
        {

        }

        public override IDbManagerTable CreateManagerTable(DbConnection connection, DbTransaction transaction = null)
        {
            return new SqlManagerTable(connection, transaction);
        }

      
    }
}
