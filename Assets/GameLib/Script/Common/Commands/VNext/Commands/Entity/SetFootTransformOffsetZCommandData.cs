#nullable enable
using System;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    [Serializable]
    public sealed class SetFootTransformOffsetZCommandData : ICommandData
    {
        public int CommandId => CommandIds.SetFootTransformOffsetZ;
        public string DebugData
        {
            get
            {
                var value = CommandDebugDataHelper.GetDynamicDebugData(Value);
                return $"Mode={Mode} Value={value}";
            }
        }

        [BoxGroup("Mode")]
        [EnumToggleButtons]
        [LabelText("Mode")]
        [Tooltip("Apply Value as an absolute OffsetZ or add it on top of the current value.")]
        public FootTransformOffsetZMode Mode = FootTransformOffsetZMode.Absolute;

        [BoxGroup("Value")]
        [LabelText("Amount")]
        [Tooltip("Numeric input (variable or literal) that drives the OffsetZ adjustment.")]
        public DynamicValue<float> Value = new();
    }

    public enum FootTransformOffsetZMode
    {
        [LabelText("Absolute")]
        Absolute,

        [LabelText("Additive")]
        Add,
    }
}
