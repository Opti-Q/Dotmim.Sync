using DotmimSyncLegacy.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace DotmimSyncLegacy.Builders
{
    public enum DbCommandType
    {
        SelectChanges,
        SelectChangesWitFilters,
        SelectRow,
        InsertRow,
        UpdateRow,
        DeleteRow,
        InsertMetadata,
        UpdateMetadata,
        DeleteMetadata,
        InsertTrigger,
        UpdateTrigger,
        DeleteTrigger,
        BulkTableType,
        BulkInsertRows,
        BulkUpdateRows,
        BulkDeleteRows,
        Reset
    }
}
