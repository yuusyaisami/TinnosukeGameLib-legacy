#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
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

            [SerializeField]
            [LabelText("Revision")]
            public int Revision = 1;

            [SerializeField, ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = false)]
            public List<TableRow> Rows = new();
        }

        [Serializable]
        public sealed class TableRow
        {
            [SerializeField]
            [LabelText("Row Id")]
            public int RowId;

            [SerializeField]
            [LabelText("Revision")]
            public int Revision = 1;

            [SerializeField, ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = false)]
            public List<TableCell> Cells = new();
        }

        [Serializable]
        public sealed class TableCell
        {
            [SerializeField]
            [LabelText("Column Id")]
            public int ColumnId;

            [SerializeField]
            [LabelText("Cell Id")]
            public int CellId;

            [SerializeField]
            [LabelText("Revision")]
            public int Revision = 1;

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
        [SerializeField]
        [LabelText("Revision")]
        public int Revision = 1;

        [SerializeField, ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
        List<RowPayload> rows = new();

        public IReadOnlyList<RowPayload> Rows => rows;

        public int RowCount => rows?.Count ?? 0;

        public bool TryGetRowIdentity(int rowIndex, out int rowId, out int revision)
        {
            rowId = 0;
            revision = 0;

            if (rows == null || rowIndex < 0 || rowIndex >= rows.Count)
                return false;

            var row = rows[rowIndex];
            if (row == null || row.RowId == 0)
                return false;

            rowId = row.RowId;
            revision = row.Revision;
            return true;
        }

        public bool TryGetCellIdentity(int rowIndex, int columnIndex, out int rowId, out int columnId, out int cellId, out int revision)
        {
            rowId = 0;
            columnId = 0;
            cellId = 0;
            revision = 0;

            if (rows == null || rowIndex < 0 || rowIndex >= rows.Count)
                return false;

            var row = rows[rowIndex];
            if (row?.Cells == null || columnIndex < 0 || columnIndex >= row.Cells.Count)
                return false;

            var cell = row.Cells[columnIndex];
            if (cell == null || cell.CellId == 0)
                return false;

            rowId = row.RowId;
            columnId = cell.ColumnId;
            cellId = cell.CellId;
            revision = cell.Revision;
            return true;
        }

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
                            ColumnId = sourceCell?.ColumnId ?? 0,
                            CellId = sourceCell?.CellId ?? 0,
                            Revision = sourceCell?.Revision > 0 ? sourceCell.Revision : 1,
                            Vars = sourceCell?.Vars ?? new VarStoreCellPayload(),
                        });
                    }
                }

                row.RowId = sourceRow?.RowId ?? 0;
                row.Revision = sourceRow?.Revision > 0 ? sourceRow.Revision : 1;

                table.rows.Add(row);
            }

            table.Revision = legacyTable.Revision > 0 ? legacyTable.Revision : 1;
            table.NormalizeIdentities();

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

        public void NormalizeIdentities()
        {
            if (rows == null)
                rows = new List<RowPayload>();

            HashSet<int> usedRowIds = new HashSet<int>();
            HashSet<int> usedCellIds = new HashSet<int>();

            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                RowPayload row = rows[rowIndex] ?? throw new InvalidOperationException("Table rows must not contain null entries.");
                NormalizeIdentity(row, usedRowIds, usedCellIds);
            }

            ValidateIdentityIntegrity();
        }

        public void ValidateIdentityIntegrity()
        {
            if (rows == null)
                return;

            HashSet<int> seenRowIds = new HashSet<int>();
            HashSet<int> seenCellIds = new HashSet<int>();

            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                RowPayload row = rows[rowIndex] ?? throw new InvalidOperationException("Table rows must not contain null entries.");

                if (row.RowId == 0)
                    throw new InvalidOperationException("Table rows must provide a non-zero row identity.");

                if (!seenRowIds.Add(row.RowId))
                    throw new InvalidOperationException("Table rows must use unique row identities.");

                if (row.Cells == null)
                    continue;

                HashSet<int> seenColumnIds = new HashSet<int>();
                for (int columnIndex = 0; columnIndex < row.Cells.Count; columnIndex++)
                {
                    CellPayload cell = row.Cells[columnIndex] ?? throw new InvalidOperationException("Table cells must not contain null entries.");

                    if (cell.ColumnId == 0)
                        throw new InvalidOperationException("Table cells must provide a non-zero column identity.");

                    if (!seenColumnIds.Add(cell.ColumnId))
                        throw new InvalidOperationException("Table rows must use unique column identities.");

                    if (cell.CellId == 0)
                        throw new InvalidOperationException("Table cells must provide a non-zero cell identity.");

                    if (!seenCellIds.Add(cell.CellId))
                        throw new InvalidOperationException("Table cells must use unique cell identities.");
                }
            }
        }

        static void NormalizeIdentity(RowPayload row, HashSet<int> usedRowIds, HashSet<int> usedCellIds)
        {
            if (row.Cells == null)
                row.Cells = new List<CellPayload>();

            if (row.RowId == 0)
            {
                row.RowId = AllocateStableIdentity("Table.Row", BuildRowIdentitySignature(row), usedRowIds);
            }
            else if (!usedRowIds.Add(row.RowId))
            {
                throw new InvalidOperationException("Table rows must use unique row identities.");
            }

            HashSet<int> usedColumnIds = new HashSet<int>();
            for (int columnIndex = 0; columnIndex < row.Cells.Count; columnIndex++)
            {
                CellPayload cell = row.Cells[columnIndex] ?? throw new InvalidOperationException("Table cells must not contain null entries.");
                NormalizeIdentity(cell, usedColumnIds, usedCellIds);
            }
        }

        static void NormalizeIdentity(CellPayload cell, HashSet<int> usedColumnIds, HashSet<int> usedCellIds)
        {
            string signature = BuildCellIdentitySignature(cell);

            if (cell.ColumnId == 0)
            {
                cell.ColumnId = AllocateStableIdentity("Table.Column", signature, usedColumnIds);
            }
            else if (!usedColumnIds.Add(cell.ColumnId))
            {
                throw new InvalidOperationException("Table rows must use unique column identities.");
            }

            if (cell.CellId == 0)
            {
                cell.CellId = AllocateStableIdentity("Table.Cell", signature, usedCellIds);
            }
            else if (!usedCellIds.Add(cell.CellId))
            {
                throw new InvalidOperationException("Table cells must use unique cell identities.");
            }
        }

        static string BuildRowIdentitySignature(RowPayload row)
        {
            StringBuilder sb = new StringBuilder(64);
            sb.Append("Revision=").Append(row.Revision);
            sb.Append("|CellCount=").Append(row.Cells?.Count ?? 0);

            if (row.Cells != null)
            {
                for (int columnIndex = 0; columnIndex < row.Cells.Count; columnIndex++)
                {
                    CellPayload cell = row.Cells[columnIndex];
                    if (cell == null)
                    {
                        sb.Append("|Cell=<null>");
                        continue;
                    }

                    sb.Append("|Cell[").Append(columnIndex).Append("]=");
                    sb.Append(BuildCellIdentitySignature(cell));
                }
            }

            return sb.ToString();
        }

        static string BuildCellIdentitySignature(CellPayload cell)
        {
            StringBuilder sb = new StringBuilder(96);
            sb.Append("ColumnId=").Append(cell.ColumnId);
            sb.Append("|Revision=").Append(cell.Revision);
            sb.Append("|ValueCount=").Append(cell.Vars?.Entries?.Count ?? 0);

            if (cell.Vars?.Entries != null)
            {
                for (int entryIndex = 0; entryIndex < cell.Vars.Entries.Count; entryIndex++)
                {
                    VarStorePayload.Entry entry = cell.Vars.Entries[entryIndex];
                    sb.Append("|Entry[").Append(entryIndex).Append("]=");
                    sb.Append(entry.VarId).Append(',');
                    sb.Append((int)entry.Kind).Append(',');
                    sb.Append((int)entry.StoreMode).Append(',');
                    sb.Append(entry.Value.HasSource ? entry.Value.SourceTypeName : "<no-source>").Append(',');
                    sb.Append(entry.Value.HasSource ? entry.Value.SourceDebugData : string.Empty);
                }
            }

            return sb.ToString();
        }

        static int AllocateStableIdentity(string identityKind, string signature, HashSet<int> usedIds)
        {
            int salt = 0;
            while (salt < int.MaxValue)
            {
                int candidate = ComposeStableIdentity(identityKind, signature, salt);
                if (usedIds.Add(candidate))
                    return candidate;

                salt++;
            }

            throw new InvalidOperationException("Unable to allocate a unique stable identity.");
        }

        static int ComposeStableIdentity(string identityKind, string signature, int salt)
        {
            unchecked
            {
                ulong hash = 1469598103934665603UL;
                hash = MixHash(hash, identityKind);
                hash = MixHash(hash, "\u001f");
                hash = MixHash(hash, signature);

                if (salt != 0)
                {
                    hash = MixHash(hash, "\u001f");
                    hash = MixHash(hash, salt.ToString(CultureInfo.InvariantCulture));
                }

                int candidate = (int)((hash ^ (hash >> 32)) & 0x7fffffff);
                return candidate == 0 ? 1 : candidate;
            }
        }

        static ulong MixHash(ulong hash, string text)
        {
            unchecked
            {
                for (int i = 0; i < text.Length; i++)
                {
                    hash ^= text[i];
                    hash *= 1099511628211UL;
                }

                return hash;
            }
        }

        [Serializable]
        public sealed class RowPayload
        {
            [SerializeField]
            public int RowId;

            [SerializeField]
            public int Revision = 1;

            [SerializeField, ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
            public List<CellPayload> Cells = new();
        }

        [Serializable]
        public sealed class CellPayload
        {
            [SerializeField]
            public int ColumnId;

            [SerializeField]
            public int CellId;

            [SerializeField]
            public int Revision = 1;

            [SerializeField, InlineProperty]
            public VarStoreCellPayload Vars = new();
        }

        static int ComposeTableCellId(int rowIndex, int columnIndex)
        {
            unchecked
            {
                return ((rowIndex + 1) * 100000) + (columnIndex + 1);
            }
        }
    }

    [Serializable]
    public sealed class RecordPayload
    {
        [SerializeField, VarIdDropdown]
        public int RecordVarId;

        [SerializeField]
        public int Revision = 1;

        [SerializeField, ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
        public List<RecordFieldPayload> Fields = new();
    }

    [Serializable]
    public sealed class RecordFieldPayload
    {
        [SerializeField, VarIdDropdown]
        public int FieldId;

        [SerializeField]
        public ValueKind Kind = ValueKind.Null;

        [SerializeField]
        public bool Required = true;

        [SerializeField]
        public int Revision = 1;

        [SerializeField, InlineProperty]
        public VarStoreCellPayload Vars = new();
    }

    [Serializable]
    public sealed class RecordListPayload
    {
        [SerializeField, VarIdDropdown]
        public int RecordListVarId;

        [SerializeField]
        public int Revision = 1;

        [SerializeField, ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
        public List<RecordListElementPayload> Elements = new();
    }

    [Serializable]
    public sealed class RecordListElementPayload
    {
        [SerializeField]
        public int ElementId;

        [SerializeField]
        public int Order;

        [SerializeField]
        public int Revision = 1;

        [SerializeField, InlineProperty]
        public VarStoreCellPayload Vars = new();
    }
}
