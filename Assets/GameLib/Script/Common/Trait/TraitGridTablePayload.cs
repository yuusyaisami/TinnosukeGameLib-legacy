#nullable enable
using System;
using System.Collections.Generic;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Trait
{
    [Serializable]
    public sealed class TraitGridTablePayload
    {
        [SerializeField]
        [LabelText("Rows")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
        List<RowPayload> _rows = new();

        public IReadOnlyList<RowPayload> Rows => _rows;

        public bool HasTable
        {
            get
            {
                if (_rows == null || _rows.Count == 0)
                    return false;

                for (int row = 0; row < _rows.Count; row++)
                {
                    var columns = _rows[row]?.Columns;
                    if (columns == null)
                        continue;

                    for (int column = 0; column < columns.Count; column++)
                    {
                        var vars = columns[column]?.Vars;
                        if (vars != null && vars.Count > 0)
                            return true;
                    }
                }

                return false;
            }
        }

        [Serializable]
        public sealed class RowPayload
        {
            [SerializeField]
            [LabelText("Columns")]
            [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
            List<CellPayload> _columns = new();

            public IReadOnlyList<CellPayload> Columns => _columns;
        }

        [Serializable]
        public sealed class CellPayload
        {
            [SerializeField]
            [LabelText("Vars")]
            [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
            List<VarPayload> _vars = new();

            public IReadOnlyList<VarPayload> Vars => _vars;
        }

        [Serializable]
        public sealed class VarPayload
        {
            [SerializeField, LabelText("Var Key"), VarIdDropdown]
            public int VarId;

            [SerializeField]
            [LabelText("Kind")]
            public Common.VarStorePayload.EntryValueKind Kind = Common.VarStorePayload.EntryValueKind.Auto;

            [SerializeField]
            [LabelText("Store Mode")]
            public Common.VarStoreWriteMode StoreMode = Common.VarStoreWriteMode.Immediate;

            [SerializeField]
            [LabelText("Value")]
            public Common.DynamicValue Value;

            public bool TryToVariant(out Common.DynamicVariant value)
            {
                if (StoreMode == Common.VarStoreWriteMode.DeferredDynamic)
                {
                    if (!Value.HasSource)
                    {
                        value = Common.DynamicVariant.Null;
                        return true;
                    }

                    var deferred = new Common.DeferredDynamicVarValue(Value, Kind, VarId, nameof(TraitGridTablePayload));
                    value = Common.DynamicVariant.FromManagedRef(deferred);
                    return true;
                }

                var entry = new Common.VarStorePayload.Entry
                {
                    Kind = Kind,
                    Value = Value,
                };

                return Common.VarStoreEntryValueKindConverter.TryConvertToVariant(in entry, out value);
            }
        }
    }
}