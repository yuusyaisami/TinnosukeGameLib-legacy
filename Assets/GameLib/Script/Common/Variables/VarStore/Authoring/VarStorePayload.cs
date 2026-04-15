#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using Game.Commands.VNext;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Common
{
    /// <summary>
    /// Unity でシリアライズ可能な VarStore 初期値コンテナ。
    /// </summary>
    /// <remarks>
    /// - VarStore 自体は Dictionary を持つため Unity のシリアライズに向かない。
    /// - 旧「VariableBag を SO/Inspector に保持していた」箇所を置換するための型。
    /// - Runtime では stableKey → varId 解決を行い、IVarStore へ書き込む。
    /// - ManagedRef: SerializeReference を使って任意の参照型（非UnityEngine.Object）を格納可能。
    /// </remarks>
    [Serializable]
    public sealed class VarStorePayload
    {
        public enum EntryValueKind : byte
        {
            Null = 0,
            Bool = 1,
            Int = 2,
            Float = 3,
            String = 4,
            Vector2 = 5,
            Vector3 = 6,
            Vector4 = 7,
            Color = 8,
            UnityObject = 9,
            ManagedRef = 10,
            CommandListData = 11,
            Table = 12,
            Auto = 255,
        }

        [SerializeField, ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
        List<Entry> entries = new();

        [SerializeField, ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = false)]
        List<TableEntry> tables = new();

        public IReadOnlyList<Entry> Entries => entries;
        public IReadOnlyList<TableEntry> Tables => tables;

        [Serializable]
        public struct Entry
        {
            [SerializeField, VarIdDropdown]
            public int VarId;

            [SerializeField]
            [LabelText("Kind")]
            public EntryValueKind Kind;

            [SerializeField]
            [LabelText("Store Mode")]
            public VarStoreWriteMode StoreMode;

            [SerializeField]
            [LabelText("Value")]
            public DynamicValue Value;
        }

        [Serializable]
        public sealed class TableEntry
        {
            [SerializeField, VarIdDropdown]
            public int TableVarId;

            [SerializeField, ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = false)]
            public List<TableRow> Rows = new();
        }

        [Serializable]
        public sealed class TableRow
        {
            [SerializeField, ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = false)]
            public List<TableCell> Cells = new();
        }

        [Serializable]
        public sealed class TableCell
        {
            [SerializeField, InlineProperty]
            public VarStoreCellPayload Vars = new();
        }

        public static VarStorePayload FromTables(IEnumerable<TableEntry>? tableEntries)
        {
            var payload = new VarStorePayload();
            if (tableEntries != null)
                payload.tables.AddRange(tableEntries);
            return payload;
        }

        public void ApplyTo(IVarStore dest, bool overwrite)
            => ApplyTo(dest, context: null, overwrite);

        public void ApplyTo(IVarStore dest, IDynamicContext? context, bool overwrite)
        {
            if (dest == null)
                return;

            ApplyEntries(entries, dest, context, overwrite, allowTableEntries: true, tableVarIdOverride: null, ownerName: nameof(VarStorePayload));
            ApplyTables(dest, overwrite, context);
        }

        internal void ApplyTables(IVarStore dest, bool overwrite, IDynamicContext? context, int? tableVarIdOverride = null)
        {
            if (dest == null || tables == null || tables.Count == 0)
                return;

            for (var t = 0; t < tables.Count; t++)
            {
                var table = tables[t];
                if (table == null)
                    continue;

                var tableVarId = table.TableVarId;
                if (tableVarId == 0 && tableVarIdOverride.HasValue)
                    tableVarId = tableVarIdOverride.Value;

                if (tableVarId == 0)
                    continue;

                if (!overwrite && dest.ContainsTable(tableVarId))
                    continue;

                if (overwrite && dest.ContainsTable(tableVarId))
                    dest.TryClearTable(tableVarId);

                var rows = table.Rows;
                if (rows == null || rows.Count == 0)
                    continue;

                LogLiteralTableRowContextBegin(tableVarId, rows.Count, context);

                for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
                {
                    if (!dest.TryEnsureTableRow(tableVarId, rowIndex))
                        continue;

                    var row = rows[rowIndex];
                    var cells = row?.Cells;
                    if (cells == null || cells.Count == 0)
                        continue;

                    for (var columnIndex = 0; columnIndex < cells.Count; columnIndex++)
                    {
                        if (!dest.TryAppendTableCell(tableVarId, rowIndex, out _))
                            break;

                        if (!dest.TryGetTableCellStore(tableVarId, rowIndex, columnIndex, out var cellStore))
                            continue;

                        var cell = cells[columnIndex];
                        if (cell?.Vars == null)
                            continue;

                        cell.Vars.ApplyTo(cellStore, context, overwrite: true);
                        LogLiteralTableCellWrite(tableVarId, rowIndex, columnIndex, cellStore, context);
                    }
                }
            }
        }

        internal static void ApplyEntries(
            IReadOnlyList<Entry>? entries,
            IVarStore dest,
            IDynamicContext? context,
            bool overwrite,
            bool allowTableEntries,
            int? tableVarIdOverride,
            string ownerName)
        {
            if (dest == null || entries == null || entries.Count == 0)
                return;

            for (var i = 0; i < entries.Count; i++)
                ApplyEntry(entries[i], dest, context, overwrite, allowTableEntries, tableVarIdOverride, ownerName);
        }

        internal static bool ApplyEntry(
            Entry entry,
            IVarStore dest,
            IDynamicContext? context,
            bool overwrite,
            bool allowTableEntries,
            int? tableVarIdOverride,
            string ownerName)
        {
            if (entry.VarId == 0)
                return false;

            if (!overwrite && dest.Contains(entry.VarId))
                return true;

            if (entry.Kind == EntryValueKind.Table)
            {
                if (!entry.Value.HasSource)
                {
                    dest.TryUnset(entry.VarId);
                    return true;
                }

                var evaluated = entry.Value.Evaluate(context ?? EmptyDynamicContext.Instance);
                if (evaluated.Kind == ValueKind.Null)
                {
                    dest.TryUnset(entry.VarId);
                    return true;
                }

                if (evaluated.Kind == ValueKind.ManagedRef && evaluated.AsManagedRef is Table table)
                {
                    if (!dest.TrySetManagedRef(entry.VarId, table))
                        return false;
                    LogLiteralTableManagedRefWrite(ownerName, entry.VarId, table, entry.Value, context, "Table");
                    return true;
                }

                if (evaluated.Kind == ValueKind.ManagedRef && evaluated.AsManagedRef is VarStorePayload legacyPayload)
                {
                    var converted = Table.FromLegacy(legacyPayload);
                    if (!dest.TrySetManagedRef(entry.VarId, converted))
                        return false;
                    LogLiteralTableManagedRefWrite(ownerName, entry.VarId, converted, entry.Value, context, "LegacyVarStorePayload");
                    return true;
                }

                return false;
            }

            if (entry.StoreMode == VarStoreWriteMode.DeferredDynamic)
            {
                if (!entry.Value.HasSource)
                {
                    dest.TryUnset(entry.VarId);
                    return true;
                }

                var deferred = new DeferredDynamicVarValue(entry.Value, entry.Kind, entry.VarId, ownerName);
                return dest.TrySetManagedRef(entry.VarId, deferred);
            }

            if (!VarStoreEntryValueKindConverter.TryConvertToVariant(in entry, context ?? EmptyDynamicContext.Instance, out var value))
                return false;

            if (value.Kind == ValueKind.ManagedRef)
            {
                if (value.AsManagedRef != null)
                    return dest.TrySetManagedRef(entry.VarId, value.AsManagedRef);
                return true;
            }

            return dest.TrySetVariant(entry.VarId, value);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        static void LogLiteralTableManagedRefWrite(
            string ownerName,
            int varId,
            Table table,
            in DynamicValue source,
            IDynamicContext? context,
            string origin)
        {
            Debug.Log(
                $"[VarStorePayload] LiteralTableManagedRefWrite Owner={ownerName} VarId={varId} Origin={origin} SourceType={source.SourceTypeName} SourceData={source.SourceDebugData} Scope={DescribeContextScope(context)} Payload={BuildLiteralTableSummary(table)}");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        static void LogLiteralTableRowContextBegin(int tableVarId, int rowCount, IDynamicContext? context)
        {
            Debug.Log($"[VarStorePayload] LiteralTableRowContextBegin TableVarId={tableVarId} Rows={rowCount} Scope={DescribeContextScope(context)}");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        static void LogLiteralTableCellWrite(int tableVarId, int rowIndex, int columnIndex, IVarStore cellStore, IDynamicContext? context)
        {
            Debug.Log(
                $"[VarStorePayload] LiteralTableCellWrite TableVarId={tableVarId} Row={rowIndex} Column={columnIndex} Scope={DescribeContextScope(context)} Values=[{BuildCellStoreSummary(cellStore)}]");
        }

        static string DescribeContextScope(IDynamicContext? context)
        {
            if (context?.Scope?.Identity != null)
                return $"{context.Scope.Identity.Id}:{context.Scope.Identity.Kind}";

            return context?.Scope?.GetType().Name ?? "<null>";
        }

        static string BuildLiteralTableSummary(Table table)
        {
            var sb = new StringBuilder();
            var rowCount = table.RowCount;
            sb.Append($"rows={rowCount}");

            var maxRows = Math.Min(rowCount, 4);
            for (var rowIndex = 0; rowIndex < maxRows; rowIndex++)
            {
                if (!table.TryGetColumnCount(rowIndex, out var columnCount))
                {
                    sb.Append($" row[{rowIndex}]cols=?");
                    continue;
                }

                sb.Append($" row[{rowIndex}]cols={columnCount}");
                var maxColumns = Math.Min(columnCount, 3);
                for (var columnIndex = 0; columnIndex < maxColumns; columnIndex++)
                {
                    if (!table.TryGetCellVars(rowIndex, columnIndex, out var vars) || vars == null)
                    {
                        sb.Append($" cell[{rowIndex},{columnIndex}]=<missing>");
                        continue;
                    }

                    sb.Append($" cell[{rowIndex},{columnIndex}]vars={vars.Entries.Count}");
                }

                if (columnCount > maxColumns)
                    sb.Append(" ...");
            }

            if (rowCount > maxRows)
                sb.Append(" ...");

            return sb.ToString();
        }

        static string BuildCellStoreSummary(IVarStore cellStore)
        {
            var sb = new StringBuilder();
            var count = 0;
            foreach (var varId in cellStore.EnumerateVarIds())
            {
                if (varId == 0)
                    continue;

                if (count > 0)
                    sb.Append(", ");

                var key = VarIdResolver.TryGetStableKey(varId, out var stableKey) ? stableKey : "<runtime>";
                var kind = cellStore.GetVarKind(varId);
                sb.Append($"varId={varId} key={key} kind={kind} value={DescribeVarStoreValue(cellStore, varId, kind)}");
                count++;

                if (count >= 16)
                {
                    sb.Append(", ...");
                    break;
                }
            }

            return count == 0 ? "(empty)" : sb.ToString();
        }

        static string DescribeVarStoreValue(IVarStore vars, int varId, ValueKind kind)
        {
            if (kind == ValueKind.ManagedRef)
            {
                if (!vars.TryGetManagedRef(varId, out var managedRef) || managedRef == null)
                    return "null";

                return ManagedRefDebugTextFormatter.Format(managedRef);
            }

            if (vars.TryGetVariant(varId, out var variant))
            {
                if (variant.Kind == ValueKind.ManagedRef)
                    return ManagedRefDebugTextFormatter.Format(variant.AsManagedRef);

                return variant.ToString();
            }

            return "<unknown>";
        }

        public VarStore ToVarStore()
        {
            var vars = new VarStore();
            ApplyTo(vars, overwrite: true);
            return vars;
        }
    }

    [Serializable]
    public sealed class VarStoreCellPayload
    {
        [SerializeField, ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
        List<VarStorePayload.Entry> entries = new();

        public IReadOnlyList<VarStorePayload.Entry> Entries => entries;

        public void ApplyTo(IVarStore dest, IDynamicContext? context, bool overwrite)
        {
            VarStorePayload.ApplyEntries(entries, dest, context, overwrite, allowTableEntries: true, tableVarIdOverride: null, ownerName: nameof(VarStoreCellPayload));
        }
    }

    [Serializable]
    public sealed class Table
    {
        [SerializeField, ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
        List<RowPayload> rows = new();

        public IReadOnlyList<RowPayload> Rows => rows;

        public int RowCount => rows?.Count ?? 0;

        public static Table FromLegacy(VarStorePayload? payload)
        {
            var table = new Table();
            if (payload?.Tables == null || payload.Tables.Count == 0)
                return table;

            var legacyTable = payload.Tables[0];
            if (legacyTable?.Rows == null || legacyTable.Rows.Count == 0)
                return table;

            table.rows.Clear();
            for (var rowIndex = 0; rowIndex < legacyTable.Rows.Count; rowIndex++)
            {
                var sourceRow = legacyTable.Rows[rowIndex];
                var row = new RowPayload();

                if (sourceRow?.Cells != null)
                {
                    for (var columnIndex = 0; columnIndex < sourceRow.Cells.Count; columnIndex++)
                    {
                        var sourceCell = sourceRow.Cells[columnIndex];
                        row.Cells.Add(new CellPayload
                        {
                            Vars = sourceCell?.Vars ?? new VarStoreCellPayload(),
                        });
                    }
                }

                table.rows.Add(row);
            }

            return table;
        }

        public bool TryGetColumnCount(int rowIndex, out int columnCount)
        {
            columnCount = 0;

            if (rows == null || rowIndex < 0 || rowIndex >= rows.Count)
                return false;

            var row = rows[rowIndex];
            if (row?.Cells == null)
                return false;

            columnCount = row.Cells.Count;
            return true;
        }

        public bool TryGetCellVars(int rowIndex, int columnIndex, out VarStoreCellPayload? vars)
        {
            vars = null;

            if (rows == null || rowIndex < 0 || rowIndex >= rows.Count)
                return false;

            var row = rows[rowIndex];
            if (row?.Cells == null || columnIndex < 0 || columnIndex >= row.Cells.Count)
                return false;

            vars = row.Cells[columnIndex]?.Vars;
            return vars != null;
        }

        [Serializable]
        public sealed class RowPayload
        {
            [SerializeField, ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
            public List<CellPayload> Cells = new();
        }

        [Serializable]
        public sealed class CellPayload
        {
            [SerializeField, InlineProperty]
            public VarStoreCellPayload Vars = new();
        }
    }
}
