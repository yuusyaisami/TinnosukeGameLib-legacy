#nullable enable

using Game.Commands.VNext;

namespace Game.Common
{
    public static class GridBlackboardWriteUtility
    {
        public static bool ApplyCellValues(
            IGridBlackboardService grid,
            int row,
            int column,
            VarStorePayload? payload,
            IDynamicContext? context,
            bool overwrite,
            bool upsert,
            int gridIdVarId = 0)
        {
            if (grid == null)
                return false;

            var appliedAny = false;
            var entries = payload?.Entries;
            if (entries != null && entries.Count > 0)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    if (entry.VarId == 0)
                        continue;

                    if (!VarStoreEntryValueKindConverter.TryConvertToVariant(in entry, context, out var value))
                        continue;

                    if (TryWriteCellValue(grid, row, column, entry.VarId, in value, overwrite, upsert))
                        appliedAny = true;
                }
            }

            if (gridIdVarId != 0)
            {
                var gridIdValue = DynamicVariant.FromBool(true);
                if (TryWriteGridId(grid, row, column, gridIdVarId, in gridIdValue))
                    appliedAny = true;
            }

            return appliedAny;
        }

        public static bool TryWriteGridId(IGridBlackboardService grid, int row, int column, int gridIdVarId, in DynamicVariant value)
        {
            if (grid == null || gridIdVarId == 0)
                return false;

            return TryWriteCellValue(grid, row, column, gridIdVarId, in value, overwrite: true, upsert: true);
        }

        public static bool TryWriteCellValue(
            IGridBlackboardService grid,
            int row,
            int column,
            int varId,
            in DynamicVariant value,
            bool overwrite,
            bool upsert)
        {
            if (grid == null || varId == 0)
                return false;

            if (!overwrite && grid.TryGetVariant(varId, row, column, out _))
                return false;

            if (value.Kind == ValueKind.Null)
            {
                if (!grid.TryGetVariant(varId, row, column, out _))
                    return true;

                return grid.TryUnsetVariant(varId, row, column);
            }

            if (upsert)
                return grid.SetOrExpandVariant(varId, row, column, in value);

            return grid.TrySetVariant(varId, row, column, in value);
        }
    }
}