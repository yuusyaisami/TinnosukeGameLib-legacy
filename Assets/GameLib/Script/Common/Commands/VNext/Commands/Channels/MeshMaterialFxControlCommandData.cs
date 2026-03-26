#nullable enable
using System;
using System.Collections.Generic;
using DG.Tweening;
using Game.Channel;
using Game.Common;
using Game.VariableLayer;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    public enum MeshMaterialFxControlOperation
    {
        SetEntry = 10,
        SetEntryFade = 20,
        RemoveTag = 30,
        ClearContext = 40,
        ClearNode = 50,
        ResetDefaults = 60,
    }

    public enum MeshMaterialFxCommandValueType
    {
        Float = 10,
        Int = 20,
        Bool = 30,
        Color = 40,
        Vector4 = 50,
    }

    [Serializable]
    public sealed class MeshMaterialFxCommandValue
    {
        [EnumToggleButtons]
        public MeshMaterialFxCommandValueType ValueType = MeshMaterialFxCommandValueType.Color;

        [ShowIf(nameof(IsFloat))]
        public DynamicValue<float> FloatValue = DynamicValueExtensions.FromLiteral(0f);

        [ShowIf(nameof(IsInt))]
        public DynamicValue<int> IntValue = DynamicValueExtensions.FromLiteral(0);

        [ShowIf(nameof(IsBool))]
        public DynamicValue<bool> BoolValue = DynamicValueExtensions.FromLiteral(false);

        [ShowIf(nameof(IsColor))]
        public DynamicValue<Color> ColorValue = DynamicValue<Color>.FromSource(new LiteralColorSource(Color.white));

        [ShowIf(nameof(IsVector4))]
        public DynamicValue<Vector4> Vector4Value = DynamicValue<Vector4>.FromSource(new LiteralVector4Source(Vector4.zero));

        public bool TryResolve(IDynamicContext context, out VariableLayerValue value)
        {
            switch (ValueType)
            {
                case MeshMaterialFxCommandValueType.Float:
                    if (FloatValue.TryGet(context, out float floatValue))
                    {
                        value = VariableLayerValue.FromFloat(floatValue);
                        return true;
                    }
                    break;

                case MeshMaterialFxCommandValueType.Int:
                    if (IntValue.TryGet(context, out int intValue))
                    {
                        value = VariableLayerValue.FromInt(intValue);
                        return true;
                    }
                    break;

                case MeshMaterialFxCommandValueType.Bool:
                    if (BoolValue.TryGet(context, out bool boolValue))
                    {
                        value = VariableLayerValue.FromBool(boolValue);
                        return true;
                    }
                    break;

                case MeshMaterialFxCommandValueType.Color:
                    if (ColorValue.TryGet(context, out Color colorValue))
                    {
                        value = VariableLayerValue.FromColor(colorValue);
                        return true;
                    }
                    break;

                case MeshMaterialFxCommandValueType.Vector4:
                    if (Vector4Value.TryGet(context, out Vector4 vector4Value))
                    {
                        value = VariableLayerValue.FromVector4(vector4Value);
                        return true;
                    }
                    break;
            }

            value = default;
            return false;
        }

        bool IsFloat => ValueType == MeshMaterialFxCommandValueType.Float;
        bool IsInt => ValueType == MeshMaterialFxCommandValueType.Int;
        bool IsBool => ValueType == MeshMaterialFxCommandValueType.Bool;
        bool IsColor => ValueType == MeshMaterialFxCommandValueType.Color;
        bool IsVector4 => ValueType == MeshMaterialFxCommandValueType.Vector4;
    }

    [Serializable]
    public sealed class MeshMaterialFxControlCommandData : ICommandData
    {
        public int CommandId => CommandIds.MeshMaterialFxControl;

        public string DebugData => $"Hub={HubSource.Kind} Channel={ChannelTag} Composite={CompositeTag} Op={Operation}";

        [BoxGroup("Target")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Hub Source\", HubSource)")]
        public ActorSource HubSource = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Target")]
        [LabelText("Channel Tag")]
        public string ChannelTag = "default";

        [BoxGroup("Target")]
        [LabelText("Composite Tag")]
        public string CompositeTag = "default";

        [BoxGroup("Operation")]
        [EnumToggleButtons]
        public MeshMaterialFxControlOperation Operation = MeshMaterialFxControlOperation.SetEntry;

        [BoxGroup("Node")]
        [ShowIf(nameof(UsesNode))]
        [ValueDropdown(nameof(GetNodeOptions))]
        public int NodeId = MeshMaterialPropertyCatalog.Ids.BaseTint;

        [BoxGroup("Node")]
        [ShowIf(nameof(UsesTag))]
        [LabelText("Layer Tag")]
        public string LayerTag = "default";

        [BoxGroup("Value")]
        [ShowIf(nameof(UsesValue))]
        [InlineProperty]
        [HideLabel]
        public MeshMaterialFxCommandValue Value = new();

        [BoxGroup("Value")]
        [ShowIf(nameof(UsesFade))]
        [MinValue(0f)]
        public float DurationSeconds = 0.2f;

        [BoxGroup("Value")]
        [ShowIf(nameof(UsesFade))]
        public Ease Ease = Ease.Linear;

        [BoxGroup("Value")]
        [ShowIf(nameof(UsesLifetime))]
        [LabelText("Lifetime Seconds (-1 = Infinite)")]
        public float LifetimeSeconds = -1f;

        bool UsesNode =>
            Operation == MeshMaterialFxControlOperation.SetEntry ||
            Operation == MeshMaterialFxControlOperation.SetEntryFade ||
            Operation == MeshMaterialFxControlOperation.RemoveTag ||
            Operation == MeshMaterialFxControlOperation.ClearNode;

        bool UsesTag =>
            Operation == MeshMaterialFxControlOperation.SetEntry ||
            Operation == MeshMaterialFxControlOperation.SetEntryFade ||
            Operation == MeshMaterialFxControlOperation.RemoveTag ||
            Operation == MeshMaterialFxControlOperation.ClearContext;

        bool UsesValue =>
            Operation == MeshMaterialFxControlOperation.SetEntry ||
            Operation == MeshMaterialFxControlOperation.SetEntryFade;

        bool UsesFade => Operation == MeshMaterialFxControlOperation.SetEntryFade;
        bool UsesLifetime => UsesValue;

        static IEnumerable<ValueDropdownItem<int>> GetNodeOptions()
        {
            var nodes = MeshMaterialPropertyCatalog.Instance.Nodes;
            for (var i = 0; i < nodes.Count; i++)
                yield return new ValueDropdownItem<int>($"{nodes[i].DisplayPath} [{nodes[i].Id}]", nodes[i].Id);
        }
    }
}
