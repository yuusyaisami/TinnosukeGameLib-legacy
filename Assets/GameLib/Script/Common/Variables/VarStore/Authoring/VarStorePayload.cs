#nullable enable
using System;
using System.Collections.Generic;
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
        [SerializeField, ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
        List<Entry> entries = new();

        public IReadOnlyList<Entry> Entries => entries;

        [Serializable]
        public struct Entry
        {
            [SerializeField, VarIdDropdown]
            public int VarId;

            [SerializeField]
            public ValueKind Kind;

            [SerializeField, ShowIf(nameof(ShowBool))]
            public bool BoolValue;

            [SerializeField, ShowIf(nameof(ShowInt))]
            public int IntValue;

            [SerializeField, ShowIf(nameof(ShowFloat))]
            public float FloatValue;

            [SerializeField, ShowIf(nameof(ShowString))]
            public string StringValue;

            [SerializeField, ShowIf(nameof(ShowVector2))]
            public Vector2 Vector2Value;

            [SerializeField, ShowIf(nameof(ShowVector3))]
            public Vector3 Vector3Value;

            [SerializeField, ShowIf(nameof(ShowVector4))]
            public Vector4 Vector4Value;

            [SerializeField, ShowIf(nameof(ShowColor))]
            public Color ColorValue;

            [SerializeField, ShowIf(nameof(ShowUnityObject))]
            public UnityEngine.Object? UnityObjectValue;

            /// <summary>
            /// 非UnityEngine.Object の参照型を格納。
            /// SerializeReference により任意の [Serializable] クラスを登録可能。
            /// </summary>
            [SerializeReference, ShowIf(nameof(ShowManagedRef))]
            [LabelText("Managed Reference")]
            public object? ManagedRefValue;

            bool ShowBool() => Kind == ValueKind.Bool;
            bool ShowInt() => Kind == ValueKind.Int;
            bool ShowFloat() => Kind == ValueKind.Float;
            bool ShowString() => Kind == ValueKind.String;
            bool ShowVector2() => Kind == ValueKind.Vector2;
            bool ShowVector3() => Kind == ValueKind.Vector3;
            bool ShowVector4() => Kind == ValueKind.Vector4;
            bool ShowColor() => Kind == ValueKind.Color;
            bool ShowUnityObject() => Kind == ValueKind.UnityObject;
            bool ShowManagedRef() => Kind == ValueKind.ManagedRef;
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

                switch (e.Kind)
                {
                    case ValueKind.Bool:
                        dest.TrySetVariant(varId, DynamicVariant.FromBool(e.BoolValue));
                        break;
                    case ValueKind.Int:
                        dest.TrySetVariant(varId, DynamicVariant.FromInt(e.IntValue));
                        break;
                    case ValueKind.Float:
                        dest.TrySetVariant(varId, DynamicVariant.FromFloat(e.FloatValue));
                        break;
                    case ValueKind.String:
                        dest.TrySetVariant(varId, DynamicVariant.FromString(e.StringValue ?? string.Empty));
                        break;
                    case ValueKind.Vector2:
                        dest.TrySetVariant(varId, DynamicVariant.FromVector2(e.Vector2Value));
                        break;
                    case ValueKind.Vector3:
                        dest.TrySetVariant(varId, DynamicVariant.FromVector3(e.Vector3Value));
                        break;
                    case ValueKind.Vector4:
                        dest.TrySetVariant(varId, DynamicVariant.FromVector4(e.Vector4Value));
                        break;
                    case ValueKind.Color:
                        dest.TrySetVariant(varId, DynamicVariant.FromColor(e.ColorValue));
                        break;
                    case ValueKind.UnityObject:
                        dest.TrySetVariant(varId, DynamicVariant.FromUnityObject(e.UnityObjectValue));
                        break;
                    case ValueKind.ManagedRef:
                        if (e.ManagedRefValue != null)
                            dest.TrySetManagedRef(varId, e.ManagedRefValue);
                        break;
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
