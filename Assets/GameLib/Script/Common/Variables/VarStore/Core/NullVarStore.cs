#nullable enable
using System;
using System.Collections.Generic;

namespace Game.Common
{
    /// <summary>
    /// 何も保持しない IVarStore。
    /// vNext 対応のために Vars を非 null で返したいケースで使う。
    /// </summary>
    public sealed class NullVarStore : IVarStore
    {
        public static readonly NullVarStore Instance = new();

        NullVarStore() { }

        public int GlobalVersion => 0;
        public event Action<int>? OnVarChanged { add { } remove { } }

        public IEnumerable<int> EnumerateVarIds() => Array.Empty<int>();
        public bool Contains(int varId) => false;
        public int GetVarVersion(int varId) => 0;
        public ValueKind GetVarKind(int varId) => ValueKind.Null;
        public bool TrySetVariant(int varId, in DynamicVariant value) => false;
        public bool TryGetVariant(int varId, out DynamicVariant value) { value = default; return false; }
        public bool TrySetManagedRef(int varId, object value) => false;
        public bool TryGetManagedRef(int varId, out object value) { value = null!; return false; }
        public IEnumerable<int> EnumerateTableVarIds() => Array.Empty<int>();
        public bool ContainsTable(int tableVarId) => false;
        public int GetTableVersion(int tableVarId) => 0;
        public bool TryGetTableRowCount(int tableVarId, out int rowCount) { rowCount = 0; return false; }
        public bool TryGetTableColumnCount(int tableVarId, int rowIndex, out int columnCount) { columnCount = 0; return false; }
        public bool TryHasTableCell(int tableVarId, int rowIndex, int columnIndex) => false;
        public bool TryEnsureTableRow(int tableVarId, int rowIndex) => false;
        public bool TryInsertTableRow(int tableVarId, int rowIndex) => false;
        public bool TryRemoveTableRow(int tableVarId, int rowIndex) => false;
        public bool TryAppendTableCell(int tableVarId, int rowIndex, out int columnIndex) { columnIndex = -1; return false; }
        public bool TryInsertTableCell(int tableVarId, int rowIndex, int columnIndex) => false;
        public bool TryRemoveTableCell(int tableVarId, int rowIndex, int columnIndex) => false;
        public bool TryClearTable(int tableVarId) => false;
        public bool TryGetTableCellStore(int tableVarId, int rowIndex, int columnIndex, out IVarStore cellStore) { cellStore = Instance; return false; }
        public bool TryGetOrEnsureTableCellStore(int tableVarId, int rowIndex, int columnIndex, bool autoCreateRow, out IVarStore cellStore) { cellStore = Instance; return false; }
        public bool TryUnset(int varId) => false;
        public void Clear() { }
    }
}

