#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Game.Common
{
    /// <summary>
    /// 軽量な varId(int) ベースのストア実装。
    /// - schema がある場合は一致しない書き込みを拒否する
    /// - GlobalVersion 単調増加、slot.Version は「最後に変更された GlobalVersion」を保持する
    /// </summary>
    public sealed class VarStore : IVarStore
    {
        struct VarSlot
        {
            public ValueKind Kind;
            public int Version;
            public DynamicVariant Variant;
            public object? ManagedRef;
        }

        sealed class TableSlot
        {
            public int Version;
            public readonly List<List<TableCell>> Rows = new();
        }

        sealed class TableCell
        {
            readonly VarStore _owner;
            readonly int _tableVarId;
            readonly Action<int> _onChanged;

            public readonly VarStore Vars;

            public TableCell(VarStore owner, int tableVarId)
            {
                _owner = owner;
                _tableVarId = tableVarId;
                _onChanged = HandleCellVarChanged;
                Vars = new VarStore();
                Vars.OnVarChanged += _onChanged;
            }

            public void Dispose()
            {
                Vars.OnVarChanged -= _onChanged;
            }

            void HandleCellVarChanged(int _)
            {
                _owner.NotifyTableCellChanged(_tableVarId);
            }
        }

        readonly Dictionary<int, VarSlot> _slots;
        readonly Dictionary<int, TableSlot> _tables;
        readonly IVarSchema? _schema;
        int _globalVersion;
        int _lastVarId;
        VarSlot _lastSlot;
        bool _hasLastSlot;
        int _lastTableVarId;
        TableSlot? _lastTableSlot;
        bool _hasLastTableSlot;

        public int GlobalVersion => _globalVersion;
        public event Action<int>? OnVarChanged;

        public VarStore(int initialCapacity = 0, IVarSchema? schema = null)
        {
            _schema = schema;
            _slots = initialCapacity > 0 ? new Dictionary<int, VarSlot>(initialCapacity) : new Dictionary<int, VarSlot>();
            _tables = initialCapacity > 0 ? new Dictionary<int, TableSlot>(initialCapacity) : new Dictionary<int, TableSlot>();
            _globalVersion = 0;
        }

        public IEnumerable<int> EnumerateVarIds() => _slots.Keys;

        public IEnumerable<int> EnumerateTableVarIds() => _tables.Keys;

        public bool Contains(int varId)
        {
            return TryGetSlot(varId, out _);
        }

        public bool ContainsTable(int tableVarId)
        {
            return TryGetTableSlot(tableVarId, out _);
        }

        public int GetVarVersion(int varId)
        {
            return TryGetSlot(varId, out var slot) ? slot.Version : 0;
        }

        public int GetTableVersion(int tableVarId)
        {
            return TryGetTableSlot(tableVarId, out var slot) ? slot.Version : 0;
        }

        public ValueKind GetVarKind(int varId)
        {
            return TryGetSlot(varId, out var slot) ? slot.Kind : ValueKind.Null;
        }

        public bool TryGetTableRowCount(int tableVarId, out int rowCount)
        {
            if (!TryGetTableSlot(tableVarId, out var table))
            {
                rowCount = 0;
                return false;
            }

            rowCount = table.Rows.Count;
            return true;
        }

        public bool TryGetTableColumnCount(int tableVarId, int rowIndex, out int columnCount)
        {
            if (!TryGetTableSlot(tableVarId, out var table) || rowIndex < 0 || rowIndex >= table.Rows.Count)
            {
                columnCount = 0;
                return false;
            }

            columnCount = table.Rows[rowIndex].Count;
            return true;
        }

        public bool TryHasTableCell(int tableVarId, int rowIndex, int columnIndex)
        {
            if (!TryGetTableSlot(tableVarId, out var table) || rowIndex < 0 || columnIndex < 0)
                return false;

            if (rowIndex >= table.Rows.Count)
                return false;

            var row = table.Rows[rowIndex];
            return columnIndex < row.Count;
        }

        public bool TryEnsureTableRow(int tableVarId, int rowIndex)
        {
            if (rowIndex < 0 || !TryGetOrCreateTableSlot(tableVarId, createIfMissing: true, out var table))
                return false;

            var changed = false;
            while (table.Rows.Count <= rowIndex)
            {
                table.Rows.Add(new List<TableCell>());
                changed = true;
            }

            if (changed)
                BumpTableVersionAndNotify(tableVarId, table);

            return true;
        }

        public bool TryInsertTableRow(int tableVarId, int rowIndex)
        {
            if (rowIndex < 0 || !TryGetOrCreateTableSlot(tableVarId, createIfMissing: true, out var table))
                return false;

            if (rowIndex > table.Rows.Count)
                return false;

            table.Rows.Insert(rowIndex, new List<TableCell>());
            BumpTableVersionAndNotify(tableVarId, table);
            return true;
        }

        public bool TryRemoveTableRow(int tableVarId, int rowIndex)
        {
            if (!TryGetTableSlot(tableVarId, out var table) || rowIndex < 0 || rowIndex >= table.Rows.Count)
                return false;

            DisposeRow(table.Rows[rowIndex]);
            table.Rows.RemoveAt(rowIndex);
            BumpTableVersionAndNotify(tableVarId, table);
            return true;
        }

        public bool TryAppendTableCell(int tableVarId, int rowIndex, out int columnIndex)
        {
            if (!TryGetTableSlot(tableVarId, out var table) || rowIndex < 0 || rowIndex >= table.Rows.Count)
            {
                columnIndex = -1;
                return false;
            }

            var row = table.Rows[rowIndex];
            var cell = CreateTableCell(tableVarId);
            row.Add(cell);
            columnIndex = row.Count - 1;
            BumpTableVersionAndNotify(tableVarId, table);
            return true;
        }

        public bool TryInsertTableCell(int tableVarId, int rowIndex, int columnIndex)
        {
            if (!TryGetTableSlot(tableVarId, out var table) || rowIndex < 0 || rowIndex >= table.Rows.Count)
                return false;

            var row = table.Rows[rowIndex];
            if (columnIndex < 0 || columnIndex > row.Count)
                return false;

            row.Insert(columnIndex, CreateTableCell(tableVarId));
            BumpTableVersionAndNotify(tableVarId, table);
            return true;
        }

        public bool TryRemoveTableCell(int tableVarId, int rowIndex, int columnIndex)
        {
            if (!TryGetTableSlot(tableVarId, out var table) || rowIndex < 0 || rowIndex >= table.Rows.Count)
                return false;

            var row = table.Rows[rowIndex];
            if (columnIndex < 0 || columnIndex >= row.Count)
                return false;

            row[columnIndex].Dispose();
            row.RemoveAt(columnIndex);
            BumpTableVersionAndNotify(tableVarId, table);
            return true;
        }

        public bool TryClearTable(int tableVarId)
        {
            if (!TryGetTableSlot(tableVarId, out var table))
                return false;

            if (table.Rows.Count == 0)
                return true;

            DisposeRows(table.Rows);
            table.Rows.Clear();
            BumpTableVersionAndNotify(tableVarId, table);
            return true;
        }

        public bool TryGetTableCellStore(int tableVarId, int rowIndex, int columnIndex, out IVarStore cellStore)
        {
            if (TryGetTableCell(tableVarId, rowIndex, columnIndex, out var cell))
            {
                cellStore = cell.Vars;
                return true;
            }

            cellStore = NullVarStore.Instance;
            return false;
        }

        public bool TryGetOrEnsureTableCellStore(int tableVarId, int rowIndex, int columnIndex, bool autoCreateRow, out IVarStore cellStore)
        {
            if (rowIndex < 0 || columnIndex < 0)
            {
                cellStore = NullVarStore.Instance;
                return false;
            }

            if (autoCreateRow)
            {
                if (!TryEnsureTableRow(tableVarId, rowIndex))
                {
                    cellStore = NullVarStore.Instance;
                    return false;
                }
            }

            if (TryGetTableCell(tableVarId, rowIndex, columnIndex, out var cell))
            {
                cellStore = cell.Vars;
                return true;
            }

            cellStore = NullVarStore.Instance;
            return false;
        }

        public bool TrySetVariant(int varId, in DynamicVariant value)
        {

            if (varId == 0)
                return false;

            if (ContainsTable(varId))
                return false;

            if (value.Kind == ValueKind.Null)
                return TryUnset(varId);

            var storeValue = value;

            var hasExpectedKind = false;
            var expectedKind = ValueKind.Null;

            if (_schema != null && _schema.TryGetDecl(varId, out var decl))
            {
                if (decl.Kind == ValueKind.ManagedRef)
                    return false;
                expectedKind = decl.Kind;
                hasExpectedKind = true;

            }

            var hasSlot = TryGetSlot(varId, out var slot);
            if (hasSlot)
            {
                if (slot.Kind == ValueKind.ManagedRef)
                    return false;
                if (hasExpectedKind && slot.Kind != expectedKind)
                {

                    return false;
                }
                if (!hasExpectedKind)
                {
                    expectedKind = slot.Kind;
                    hasExpectedKind = true;

                }
            }

            if (hasExpectedKind && expectedKind != storeValue.Kind)
            {

                // CRITICAL FIX: Use temporary variable for out parameter!
                // Cannot pass same variable as both in and out parameter with struct types.
                DynamicVariant coercedValue;
                if (!TryCoerceVariant(expectedKind, storeValue, out coercedValue))
                {
                    Debug.LogWarning($"[VarStore.TrySetVariant] varId={varId}: coercion from {value.Kind} to {expectedKind} failed.");
                    return false;
                }
                storeValue = coercedValue;
            }

            if (hasExpectedKind && expectedKind != storeValue.Kind)
            {
                Debug.LogError($"[VarStore.TrySetVariant] varId={varId}: CRITICAL! After coercion, expectedKind={expectedKind} but storeValue.Kind={storeValue.Kind}. This should not happen!");
                return false;
            }

            if (_schema != null && _schema.TryGetDecl(varId, out var schemaDecl) &&
                schemaDecl.Kind != storeValue.Kind)
            {
                Debug.LogWarning($"[VarStore.TrySetVariant] varId={varId}: schema check failed after coercion (schema={schemaDecl.Kind}, value={storeValue.Kind}).");
                return false;
            }

            if (!hasSlot)
            {
                slot = default;
                slot.Kind = storeValue.Kind;
            }
            else
            {
                if (slot.Kind != storeValue.Kind)
                {
                    Debug.LogError($"[VarStore.TrySetVariant] varId={varId}: CRITICAL! slot.Kind={slot.Kind} != storeValue.Kind={storeValue.Kind} after all coercion. This should not happen!");
                    return false;
                }
            }

            // 値が変わっていない場合は通知をスキップ
            if (hasSlot && slot.Variant.Equals(storeValue))
            {
                return true;
            }

            slot.Variant = storeValue;
            slot.ManagedRef = null;

            BumpVersionAndNotify(varId, ref slot);
            _slots[varId] = slot;
            CacheSlot(varId, slot);
            return true;
        }

        public bool TryGetVariant(int varId, out DynamicVariant value)
        {
            if (TryGetSlot(varId, out var slot))
            {
                if (slot.Kind != ValueKind.ManagedRef)
                {
                    value = slot.Variant;
                    return true;
                }

                LogVarKindMismatch(varId, slot.Kind, nameof(TryGetVariant), "variant");
            }
            else
            {
                LogVarMissing(varId, nameof(TryGetVariant));
            }

            value = default;
            return false;
        }

        public bool TrySetManagedRef(int varId, object value)
        {
            if (varId == 0)
                return false;

            if (ContainsTable(varId))
                return false;

            if (value == null)
                return TryUnset(varId);

            if (_schema != null && _schema.TryGetDecl(varId, out var decl))
            {
                if (decl.Kind != ValueKind.ManagedRef)
                    return false;
            }

            VarSlot slot;
            if (TryGetSlot(varId, out var existing))
            {
                if (existing.Kind != ValueKind.ManagedRef)
                    return false;
                // 参照が同一なら通知をスキップ
                if (ReferenceEquals(existing.ManagedRef, value))
                    return true;
                slot = existing;
            }
            else
            {
                slot = default;
                slot.Kind = ValueKind.ManagedRef;
            }

            slot.ManagedRef = value;
            slot.Variant = default;

            BumpVersionAndNotify(varId, ref slot);
            _slots[varId] = slot;
            CacheSlot(varId, slot);
            return true;
        }

        public bool TryGetManagedRef(int varId, out object value)
        {
            if (TryGetSlot(varId, out var slot))
            {
                if (slot.Kind == ValueKind.ManagedRef && slot.ManagedRef != null)
                {
                    value = slot.ManagedRef;
                    return true;
                }

                LogVarKindMismatch(varId, slot.Kind, nameof(TryGetManagedRef), "managed ref");
            }
            else
            {
                LogVarMissing(varId, nameof(TryGetManagedRef));
            }

            value = null!;
            return false;
        }

        public bool TryUnset(int varId)
        {
            if (varId == 0)
                return false;

            if (!_slots.Remove(varId))
                return false;

            // remove first to make Contains false immediately, then notify.
            InvalidateCache(varId);
            _globalVersion++;
            OnVarChanged?.Invoke(varId);
            return true;
        }

        public void Clear()
        {
            if (_slots.Count == 0 && _tables.Count == 0)
                return;

            _slots.Clear();
            foreach (var pair in _tables)
                DisposeRows(pair.Value.Rows);

            _tables.Clear();
            _globalVersion++;
            ClearCache();
            ClearTableCache();
        }

        bool TryGetSlot(int varId, out VarSlot slot)
        {
            if (varId == 0)
            {
                slot = default;
                return false;
            }

            if (_hasLastSlot && _lastVarId == varId)
            {
                slot = _lastSlot;
                return true;
            }

            if (_slots.TryGetValue(varId, out slot))
            {
                CacheSlot(varId, slot);
                return true;
            }

            return false;
        }

        void CacheSlot(int varId, in VarSlot slot)
        {
            _lastVarId = varId;
            _lastSlot = slot;
            _hasLastSlot = true;
        }

        void InvalidateCache(int varId)
        {
            if (_hasLastSlot && _lastVarId == varId)
                ClearCache();
        }

        void ClearCache()
        {
            _lastVarId = 0;
            _lastSlot = default;
            _hasLastSlot = false;
        }

        void BumpVersionAndNotify(int varId, ref VarSlot slot)
        {
            _globalVersion++;
            slot.Version = _globalVersion;
            OnVarChanged?.Invoke(varId);
        }

        bool TryGetTableSlot(int tableVarId, out TableSlot table)
        {
            if (tableVarId == 0)
            {
                table = null!;
                return false;
            }

            if (_hasLastTableSlot && _lastTableVarId == tableVarId && _lastTableSlot != null)
            {
                table = _lastTableSlot;
                return true;
            }

            if (_tables.TryGetValue(tableVarId, out table))
            {
                CacheTableSlot(tableVarId, table);
                return true;
            }

            return false;
        }

        bool TryGetOrCreateTableSlot(int tableVarId, bool createIfMissing, out TableSlot table)
        {
            if (TryGetTableSlot(tableVarId, out table))
                return true;

            if (!createIfMissing || tableVarId == 0)
                return false;

            if (Contains(tableVarId))
                return false;

            table = new TableSlot();
            _tables[tableVarId] = table;
            CacheTableSlot(tableVarId, table);
            return true;
        }

        bool TryGetTableCell(int tableVarId, int rowIndex, int columnIndex, out TableCell cell)
        {
            if (!TryGetTableSlot(tableVarId, out var table) || rowIndex < 0 || columnIndex < 0 || rowIndex >= table.Rows.Count)
            {
                cell = null!;
                return false;
            }

            var row = table.Rows[rowIndex];
            if (columnIndex >= row.Count)
            {
                cell = null!;
                return false;
            }

            cell = row[columnIndex];
            return true;
        }

        TableCell CreateTableCell(int tableVarId)
        {
            return new TableCell(this, tableVarId);
        }

        void CacheTableSlot(int tableVarId, TableSlot table)
        {
            _lastTableVarId = tableVarId;
            _lastTableSlot = table;
            _hasLastTableSlot = true;
        }

        void InvalidateTableCache(int tableVarId)
        {
            if (_hasLastTableSlot && _lastTableVarId == tableVarId)
                ClearTableCache();
        }

        void ClearTableCache()
        {
            _lastTableVarId = 0;
            _lastTableSlot = null;
            _hasLastTableSlot = false;
        }

        void NotifyTableCellChanged(int tableVarId)
        {
            if (!TryGetTableSlot(tableVarId, out var table))
                return;

            BumpTableVersionAndNotify(tableVarId, table);
        }

        void BumpTableVersionAndNotify(int tableVarId, TableSlot table)
        {
            _globalVersion++;
            table.Version = _globalVersion;
            OnVarChanged?.Invoke(tableVarId);
        }

        static void DisposeRows(List<List<TableCell>> rows)
        {
            for (var i = 0; i < rows.Count; i++)
                DisposeRow(rows[i]);
        }

        static void DisposeRow(List<TableCell> row)
        {
            for (var i = 0; i < row.Count; i++)
                row[i].Dispose();
        }

        internal static bool TryCoerceVariant(ValueKind expectedKind, in DynamicVariant value, out DynamicVariant coerced, bool logOnFailure = true)
        {
            coerced = DynamicVariant.Null;

            switch (expectedKind)
            {
                case ValueKind.Bool:
                    if (TryGetBool(value, out var b))
                    {
                        coerced = DynamicVariant.FromBool(b);
                        return true;
                    }
                    if (logOnFailure)
                        Debug.LogWarning($"[VarStore.TryCoerceVariant] Bool coercion failed for {value.Kind}.");
                    return false;
                case ValueKind.Int:
                    if (TryGetInt(value, out var i))
                    {
                        coerced = DynamicVariant.FromInt(i);
                        return true;
                    }
                    if (logOnFailure)
                        Debug.LogWarning($"[VarStore.TryCoerceVariant] Int coercion failed for {value.Kind}.");
                    return false;
                case ValueKind.Float:
                    if (TryGetFloat(value, out var f))
                    {
                        coerced = DynamicVariant.FromFloat(f);
                        return true;
                    }
                    if (logOnFailure)
                        Debug.LogWarning($"[VarStore.TryCoerceVariant] Float coercion failed for {value.Kind}.");
                    return false;
                case ValueKind.String:
                    coerced = DynamicVariant.FromString(value.ToString());
                    return true;
                case ValueKind.Vector2:
                    if (TryGetVector2(value, out var v2))
                    {
                        coerced = DynamicVariant.FromVector2(v2);
                        return true;
                    }
                    if (logOnFailure)
                        Debug.LogWarning($"[VarStore.TryCoerceVariant] Vector2 coercion failed for {value.Kind}.");
                    return false;
                case ValueKind.Vector3:
                    if (TryGetVector3(value, out var v3))
                    {
                        coerced = DynamicVariant.FromVector3(v3);
                        return true;
                    }
                    if (logOnFailure)
                        Debug.LogWarning($"[VarStore.TryCoerceVariant] Vector3 coercion failed for {value.Kind}.");
                    return false;
                case ValueKind.Vector4:
                    if (TryGetVector4(value, out var v4))
                    {
                        coerced = DynamicVariant.FromVector4(v4);
                        return true;
                    }
                    if (logOnFailure)
                        Debug.LogWarning($"[VarStore.TryCoerceVariant] Vector4 coercion failed for {value.Kind}.");
                    return false;
                case ValueKind.Color:
                    if (TryGetColor(value, out var c))
                    {
                        coerced = DynamicVariant.FromColor(c);
                        return true;
                    }
                    if (logOnFailure)
                        Debug.LogWarning($"[VarStore.TryCoerceVariant] Color coercion failed for {value.Kind}.");
                    return false;
                case ValueKind.UnityObject:
                    if (value.Kind == ValueKind.UnityObject)
                    {
                        coerced = value;
                        return true;
                    }
                    if (logOnFailure)
                        Debug.LogWarning($"[VarStore.TryCoerceVariant] UnityObject coercion failed for {value.Kind}.");
                    return false;
                default:
                    if (logOnFailure)
                        Debug.LogWarning($"[VarStore.TryCoerceVariant] Unknown expected kind: {expectedKind}");
                    return false;
            }
        }

        static bool TryGetBool(in DynamicVariant value, out bool result)
        {
            switch (value.Kind)
            {
                case ValueKind.Bool:
                    result = value.AsBool;
                    return true;
                case ValueKind.Int:
                    result = value.AsInt != 0;
                    return true;
                case ValueKind.Float:
                    result = Math.Abs(value.AsFloat) > float.Epsilon;
                    return true;
                case ValueKind.String:
                    var s = value.AsString;
                    if (bool.TryParse(s, out result))
                        return true;
                    if (TryParseFloat(s, out var f))
                    {
                        result = Math.Abs(f) > float.Epsilon;
                        return true;
                    }
                    return false;
                default:
                    result = default;
                    return false;
            }
        }

        static bool TryGetInt(in DynamicVariant value, out int result)
        {
            switch (value.Kind)
            {
                case ValueKind.Int:
                    result = value.AsInt;
                    return true;
                case ValueKind.Float:
                    result = (int)value.AsFloat;
                    return true;
                case ValueKind.Bool:
                    result = value.AsBool ? 1 : 0;
                    return true;
                case ValueKind.String:
                    var s = value.AsString;
                    if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
                        return true;
                    if (TryParseFloat(s, out var f))
                    {
                        result = (int)f;
                        return true;
                    }
                    return false;
                default:
                    result = default;
                    return false;
            }
        }

        static bool TryGetFloat(in DynamicVariant value, out float result)
        {
            switch (value.Kind)
            {
                case ValueKind.Float:
                    result = value.AsFloat;
                    return true;
                case ValueKind.Int:
                    result = value.AsInt;
                    return true;
                case ValueKind.Bool:
                    result = value.AsBool ? 1f : 0f;
                    return true;
                case ValueKind.String:
                    return TryParseFloat(value.AsString, out result);
                default:
                    result = default;
                    return false;
            }
        }

        static bool TryParseFloat(string value, out float result)
        {
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }

        static bool TryGetVector2(in DynamicVariant value, out Vector2 result)
        {
            switch (value.Kind)
            {
                case ValueKind.Vector2:
                    result = value.AsVector2;
                    return true;
                case ValueKind.Vector3:
                    result = new Vector2(value.AsVector3.x, value.AsVector3.y);
                    return true;
                case ValueKind.Vector4:
                    result = new Vector2(value.AsVector4.x, value.AsVector4.y);
                    return true;
                case ValueKind.Color:
                    var c = value.AsColor;
                    result = new Vector2(c.r, c.g);
                    return true;
                default:
                    result = default;
                    return false;
            }
        }

        static bool TryGetVector3(in DynamicVariant value, out Vector3 result)
        {
            switch (value.Kind)
            {
                case ValueKind.Vector2:
                    var v2 = value.AsVector2;
                    result = new Vector3(v2.x, v2.y, 0f);
                    return true;
                case ValueKind.Vector3:
                    result = value.AsVector3;
                    return true;
                case ValueKind.Vector4:
                    var v4 = value.AsVector4;
                    result = new Vector3(v4.x, v4.y, v4.z);
                    return true;
                case ValueKind.Color:
                    var c = value.AsColor;
                    result = new Vector3(c.r, c.g, c.b);
                    return true;
                default:
                    result = default;
                    return false;
            }
        }

        static bool TryGetVector4(in DynamicVariant value, out Vector4 result)
        {
            switch (value.Kind)
            {
                case ValueKind.Vector2:
                    var v2 = value.AsVector2;
                    result = new Vector4(v2.x, v2.y, 0f, 0f);
                    return true;
                case ValueKind.Vector3:
                    var v3 = value.AsVector3;
                    result = new Vector4(v3.x, v3.y, v3.z, 0f);
                    return true;
                case ValueKind.Vector4:
                    result = value.AsVector4;
                    return true;
                case ValueKind.Color:
                    var c = value.AsColor;
                    result = new Vector4(c.r, c.g, c.b, c.a);
                    return true;
                default:
                    result = default;
                    return false;
            }
        }

        static bool TryGetColor(in DynamicVariant value, out Color result)
        {
            switch (value.Kind)
            {
                case ValueKind.Vector2:
                    var v2 = value.AsVector2;
                    result = new Color(v2.x, v2.y, 0f, 1f);
                    return true;
                case ValueKind.Vector3:
                    var v3 = value.AsVector3;
                    result = new Color(v3.x, v3.y, v3.z, 1f);
                    return true;
                case ValueKind.Vector4:
                    var v4 = value.AsVector4;
                    result = new Color(v4.x, v4.y, v4.z, v4.w);
                    return true;
                case ValueKind.Color:
                    result = value.AsColor;
                    return true;
                default:
                    result = default;
                    return false;
            }
        }

        static void LogVarMissing(int varId, string operation)
        {
            if (varId == 0)
                return;

            if (VarIdResolver.TryGetStableKey(varId, out var stableKey) && !string.IsNullOrEmpty(stableKey))
            {
                //Debug.LogWarning($"[VarStore] {operation} varId={varId} stable={stableKey} not found.");
            }
            else
            {
                //Debug.LogWarning($"[VarStore] {operation} varId={varId} not found.");
            }
        }

        static void LogVarKindMismatch(int varId, ValueKind kind, string operation, string expectation)
        {
            if (varId == 0)
                return;

            if (VarIdResolver.TryGetStableKey(varId, out var stableKey) && !string.IsNullOrEmpty(stableKey))
            {
                Debug.LogWarning($"[VarStore] {operation} varId={varId} stable={stableKey} stores {kind} when {expectation} was requested.");
            }
            else
            {
                Debug.LogWarning($"[VarStore] {operation} varId={varId} stores {kind} when {expectation} was requested.");
            }
        }
    }
}
