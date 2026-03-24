#nullable enable

using System;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.StatusEffect
{
    public enum StatusEffectStackOperation
    {
        Set = 10,
        Add = 20,
        Mul = 30,
    }

    [Serializable]
    public sealed class StatusEffectStackRule
    {
        [LabelText("Operation")]
        [EnumToggleButtons]
        public StatusEffectStackOperation Operation = StatusEffectStackOperation.Add;

        [LabelText("Local")]
        public DynamicValue<float> LocalValue;

        [LabelText("Use Global")]
        public bool UseGlobalValue;

        [ShowIf(nameof(UseGlobalValue))]
        [LabelText("Global")]
        public DynamicValue<float> GlobalValue;

        [ShowIf(nameof(UseGlobalValue))]
        [LabelText("Ignore Global On -1")]
        public bool IgnoreGlobalWhenMinusOne;

        public static StatusEffectStackRule Disabled()
            => new()
            {
                Operation = StatusEffectStackOperation.Add,
                LocalValue = DynamicValueExtensions.FromLiteral(0f),
                UseGlobalValue = false,
                GlobalValue = DynamicValueExtensions.FromLiteral(0f),
            };
    }

    [Serializable]
    public sealed class StatusEffectStackPreset : IDynamicManagedRefValue
    {
        [LabelText("Ignore If Existing")]
        [Tooltip("同じ slot に既存 effect がある場合、この preset 適用をスキップします。")]
        public bool IgnoreIfExisting;

        [BoxGroup("Rules")]
        [LabelText("Apply Intensity")]
        public bool ApplyIntensity;

        [BoxGroup("Rules")]
        [LabelText("Intensity")]
        [ShowIf(nameof(ApplyIntensity))]
        [InlineProperty]
        public StatusEffectStackRule Intensity = StatusEffectStackRule.Disabled();

        [BoxGroup("Rules")]
        [LabelText("Apply Duration")]
        public bool ApplyDuration = true;

        [BoxGroup("Rules")]
        [LabelText("Duration")]
        [ShowIf(nameof(ApplyDuration))]
        [InlineProperty]
        public StatusEffectStackRule Duration = CreateDefaultDurationRule();

        [BoxGroup("Rules")]
        [LabelText("Apply Current Count")]
        public bool ApplyCurrentCount;

        [BoxGroup("Rules")]
        [LabelText("Current Count")]
        [ShowIf(nameof(ApplyCurrentCount))]
        [InlineProperty]
        public StatusEffectStackRule CurrentCount = StatusEffectStackRule.Disabled();

        [BoxGroup("Rules")]
        [LabelText("Apply Max Count")]
        public bool ApplyMaxCount;

        [BoxGroup("Rules")]
        [LabelText("Max Count")]
        [ShowIf(nameof(ApplyMaxCount))]
        [InlineProperty]
        public StatusEffectStackRule MaxCount = StatusEffectStackRule.Disabled();

        public static StatusEffectStackPreset CreateDurationRefreshPreset()
            => new()
            {
                IgnoreIfExisting = false,
                ApplyIntensity = false,
                Intensity = StatusEffectStackRule.Disabled(),
                ApplyDuration = true,
                Duration = CreateDefaultDurationRule(),
                ApplyCurrentCount = false,
                CurrentCount = StatusEffectStackRule.Disabled(),
                ApplyMaxCount = false,
                MaxCount = StatusEffectStackRule.Disabled(),
            };

        static StatusEffectStackRule CreateDefaultDurationRule()
        {
            return new StatusEffectStackRule
            {
                Operation = StatusEffectStackOperation.Set,
                LocalValue = DynamicValueExtensions.FromLiteral(0f),
                UseGlobalValue = false,
                GlobalValue = DynamicValueExtensions.FromLiteral(0f),
                IgnoreGlobalWhenMinusOne = true,
            };
        }

        public override string ToString()
        {
            return $"Ignore={IgnoreIfExisting} Intensity={ApplyIntensity}:{Intensity.Operation} Duration={ApplyDuration}:{Duration.Operation} Count={ApplyCurrentCount}:{CurrentCount.Operation} Max={ApplyMaxCount}:{MaxCount.Operation}";
        }
    }
}