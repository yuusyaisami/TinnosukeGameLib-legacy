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

        public IReadOnlyList<Entry> Entries => entries;

        [Serializable]
        public struct Entry
        {
            [SerializeField, VarIdDropdown]
            public int VarId;

            [SerializeField]
            [LabelText("Kind")]
            public EntryValueKind Kind;

            [SerializeField]
            [LabelText("Value")]
            public DynamicValue Value;
        }

        public void ApplyTo(IVarStore dest, bool overwrite)
        {
            if (dest == null || entries == null || entries.Count == 0)
                return;

            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.VarId == 0)
                    continue;
                var varId = e.VarId;

                if (!overwrite && dest.Contains(e.VarId))
                    continue;

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
        }

        public VarStore ToVarStore()
        {
            var vars = new VarStore();
            ApplyTo(vars, overwrite: true);
            return vars;
        }
    }
}
