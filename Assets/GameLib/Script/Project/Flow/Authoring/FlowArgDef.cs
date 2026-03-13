#nullable enable

using System;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Flow
{
    [Serializable]
    public struct FlowArgDef
    {
        [HorizontalGroup("Kind", Width = 0.22f)]
        [HideLabel]
        public FlowArgKind Kind;

        [HorizontalGroup("Kind")]
        [LabelText("Int")]
        [ShowIf(nameof(ShowInt))]
        public int IntValue;

        [HorizontalGroup("Kind")]
        [LabelText("Float")]
        [ShowIf(nameof(ShowFloat))]
        public float FloatValue;

        [HorizontalGroup("Kind")]
        [LabelText("String")]
        [ShowIf(nameof(ShowString))]
        public string StringValue;

        [HorizontalGroup("Kind")]
        [LabelText("StableKey")]
        [ShowIf(nameof(ShowStableKey))]
        [VariableKeyPicker]
        public string StableKey;

        [HorizontalGroup("Kind")]
        [LabelText("Local")]
        [ShowIf(nameof(ShowLocalName))]
        public string LocalName;

        [HorizontalGroup("Kind")]
        [LabelText("Vec4")]
        [ShowIf(nameof(ShowVec4))]
        public Vector4 Vec4;

        [HorizontalGroup("Kind")]
        [LabelText("Obj")]
        [ShowIf(nameof(ShowObj))]
        public UnityEngine.Object? Obj;

        [HorizontalGroup("Kind")]
        [LabelText("Dynamic")]
        [ShowIf(nameof(ShowDynamic))]
        [SerializeReference]
        public IDynamicSource? DynamicSource;

        bool ShowInt() => Kind == FlowArgKind.ConstInt || Kind == FlowArgKind.ConstBool;
        bool ShowFloat() => Kind == FlowArgKind.ConstFloat;
        bool ShowString() => Kind == FlowArgKind.ConstString;
        bool ShowStableKey() => Kind == FlowArgKind.VarShared;
        bool ShowLocalName() => Kind == FlowArgKind.VarLocal;
        bool ShowVec4() => Kind == FlowArgKind.ConstVector2 || Kind == FlowArgKind.ConstVector3 || Kind == FlowArgKind.ConstVector4 || Kind == FlowArgKind.ConstColor;
        bool ShowObj() => Kind == FlowArgKind.UnityObject;
        bool ShowDynamic() => Kind == FlowArgKind.Dynamic;

        public void EnsureIntegrity()
        {
            StringValue ??= string.Empty;
            StableKey ??= string.Empty;
            LocalName ??= string.Empty;
        }
    }
}
