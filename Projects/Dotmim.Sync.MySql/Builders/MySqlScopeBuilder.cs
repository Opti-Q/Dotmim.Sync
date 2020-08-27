using DotmimSyncLegacy.Builders;
using System.Data.Common;
using MySql.Data.MySqlClient;

namespace DotmimSyncLegacy.MySql
{
    public class MySqlScopeBuilder : DbScopeBuilder
    {
        

        public override IDbScopeInfoBuilder CreateScopeInfoBuilder(string scopeTableName, DbConnection connection, DbTransaction transaction = null)
        {
            return new MySqlScopeInfoBuilder(scopeTableName, connection, transaction);
        }
    }
}
