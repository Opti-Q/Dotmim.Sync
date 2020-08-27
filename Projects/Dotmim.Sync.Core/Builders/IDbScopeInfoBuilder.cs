using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Text;

namespace DotmimSyncLegacy.Builders
{
    public interface IDbScopeInfoBuilder
    {
        bool NeedToCreateScopeInfoTable();
        void CreateScopeInfoTable();
        List<ScopeInfo> GetAllScopes(string scopeName, Guid? clientScopeId);
        ScopeInfo InsertOrUpdateScopeInfo(ScopeInfo scopeInfo);
        long GetLocalTimestamp();
        void DropScopeInfoTable();

    }
}
