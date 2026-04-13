#nullable enable
using System;
using System.Collections.Generic;
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
            public VarStorePayload Vars = new();
        }

        public void ApplyTo(IVarStore dest, bool overwrite)
        {
            if (dest == null)
                return;

            if (entries == null || entries.Count == 0)
            {
                ApplyTables(dest, overwrite, context: null);
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.VarId == 0)
                    continue;
                var varId = e.VarId;

                if (!overwrite && dest.Contains(e.VarId))
                    continue;

                if (e.StoreMode == VarStoreWriteMode.DeferredDynamic)
                {
                    if (!e.Value.HasSource)
                    {
                        dest.TryUnset(varId);
                        continue;
                    }

                    var deferred = new DeferredDynamicVarValue(e.Value, e.Kind, varId, nameof(VarStorePayload));
                    dest.TrySetManagedRef(varId, deferred);
                    continue;
                }

                if (!VarStoreEntryValueKindConverter.TryConvertToVariant(in e, out var value))
                    continue;

                if (value.Kind == ValueKind.ManagedRef)
                {
                    if (value.AsManagedRef != null)
                        dest.TrySetManagedRef(varId, value.AsManagedRef);
                    continue;
                }

                dest.TrySetVariant(varId, value);
            }

            ApplyTables(dest, overwrite, context: null);
        }

        internal void ApplyTables(IVarStore dest, bool overwrite, IDynamicContext? context)
        {
            if (dest == null || tables == null || tables.Count == 0)
                return;

            for (var t = 0; t < tables.Count; t++)
            {
                var table = tables[t];
                if (table == null || table.TableVarId == 0)
                    continue;

                if (!overwrite && dest.ContainsTable(table.TableVarId))
                    continue;

                if (overwrite && dest.ContainsTable(table.TableVarId))
                    dest.TryClearTable(table.TableVarId);

                var rows = table.Rows;
                if (rows == null || rows.Count == 0)
                    continue;

                for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
                {
                    if (!dest.TryEnsureTableRow(table.TableVarId, rowIndex))
                        continue;

                    var row = rows[rowIndex];
                    var cells = row?.Cells;
                    if (cells == null || cells.Count == 0)
                        continue;

                    for (var columnIndex = 0; columnIndex < cells.Count; columnIndex++)
                    {
                        if (!dest.TryAppendTableCell(table.TableVarId, rowIndex, out _))
                            break;

                        if (!dest.TryGetTableCellStore(table.TableVarId, rowIndex, columnIndex, out var cellStore))
                            continue;

                        var cell = cells[columnIndex];
                        if (cell?.Vars == null)
                            continue;

                        if (context == null)
                            cell.Vars.ApplyTo(cellStore, overwrite: true);
                        else
                            cell.Vars.ApplyTo(cellStore, context, overwrite: true);
                    }
                }
            }
        }

        public VarStore ToVarStore()
        {
            var vars = new VarStore();
            ApplyTo(vars, overwrite: true);
            return vars;
        }
    }
}
