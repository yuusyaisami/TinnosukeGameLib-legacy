#nullable enable
using System;
using Game.Common;
using Game.Direction;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    public enum SetDirectionInputMode
    {
        LiteralVector2 = 0,
        DynamicVector2 = 1,
        DynamicVector3 = 2,
    }

    [Serializable]
    public sealed class SetDirectionCommandData : ICommandData
    {
        public int CommandId => CommandIds.SetDirection;
        public string DebugData
        {
            get
            {
                var layer = string.IsNullOrEmpty(LayerTag) ? "<none>" : LayerTag;
                return $"Layer={layer} Mode={InputMode}";
            }
        }

        [BoxGroup("Target")]
        [LabelText("Layer Tag")]
        [SerializeField]
        public string LayerTag = "input";

        [BoxGroup("Direction")]
        [LabelText("Input Mode")]
        [EnumToggleButtons]
        [SerializeField]
        public SetDirectionInputMode InputMode = SetDirectionInputMode.LiteralVector2;

        [BoxGroup("Direction")]
        [LabelText("Direction")]
        [ShowIf(nameof(IsLiteralInput))]
        [SerializeField]
        public Vector2 Direction = Vector2.up;

        [BoxGroup("Direction")]
        [LabelText("Direction (Vector2)")]
        [ShowIf(nameof(IsDynamicVector2Input))]
        [SerializeField]
        public DynamicValue<Vector2> DynamicDirection2 = DynamicValueExtensions.FromLiteral(Vector2.up);

        [BoxGroup("Direction")]
        [LabelText("Direction (Vector3)")]
        [ShowIf(nameof(IsDynamicVector3Input))]
        [SerializeField]
        public DynamicValue<Vector3> DynamicDirection3 = DynamicValueExtensions.FromLiteral(Vector3.up);

        [BoxGroup("Direction")]
        [LabelText("Normalize")]
        [SerializeField]
        public bool Normalize = true;

        [BoxGroup("Create If Missing")]
        [LabelText("Auto Create Layer")]
        [SerializeField]
        public bool AutoCreateLayerIfMissing = true;

        [BoxGroup("Create If Missing")]
        [ShowIf(nameof(AutoCreateLayerIfMissing))]
        [LabelText("Create Def")]
        [SerializeField, InlineProperty, HideLabel]
        public DirectionLayerDef CreateIfMissing = new DirectionLayerDef("input");

        bool IsLiteralInput() => InputMode == SetDirectionInputMode.LiteralVector2;
        bool IsDynamicVector2Input() => InputMode == SetDirectionInputMode.DynamicVector2;
        bool IsDynamicVector3Input() => InputMode == SetDirectionInputMode.DynamicVector3;
    }
}
