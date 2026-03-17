// Game.StatusEffect.PoisonEffectPreset
using System;
using Game.Profile;
using Game.Scalar;
using Game.Scalar.Generated;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.StatusEffect
{
    [Serializable]
    public sealed class PoisonEffectPreset : BaseProfileData
    {
        [BoxGroup("Base")]
        [LabelText("Default Duration")]
        [SerializeField]
        ProfileFloatValue _defaultDuration = new()
        {
            Value = 5f,
            ScalarKeyValue = new ScalarKey(ScalarKeys.GameLib.StatusEffect.Poison.DefaultDuration),
            ScalarPolicyValue = ScalarBindPolicy.ReplaceRuntime,
            UseClampMod = true,
            Clamp = new ScalarClamp { UseMin = true, Min = 0f }
        };

        [BoxGroup("Base")]
        [LabelText("Default Intensity")]
        [SerializeField]
        ProfileFloatValue _defaultIntensity = new()
        {
            Value = 1f,
            ScalarKeyValue = new ScalarKey(ScalarKeys.GameLib.StatusEffect.Poison.DefaultIntensity),
            ScalarPolicyValue = ScalarBindPolicy.ReplaceRuntime,
            UseClampMod = true,
            Clamp = new ScalarClamp { UseMin = true, Min = 0f }
        };

        [BoxGroup("Damage")]
        [LabelText("Damage Per Second")]
        [SerializeField]
        ProfileFloatValue _damagePerSecond = new()
        {
            Value = 5f,
            ScalarKeyValue = new ScalarKey(ScalarKeys.GameLib.Health.Modifier.Poison.DamagePerSecond),
            ScalarPolicyValue = ScalarBindPolicy.ReplaceRuntime,
            UseEffectMod = true,
            UseClampMod = true,
            Clamp = new ScalarClamp { UseMin = true, Min = 0f }
        };

        [BoxGroup("Damage")]
        [LabelText("Tick Interval")]
        [SerializeField]
        ProfileFloatValue _tickInterval = new()
        {
            Value = 0.5f,
            ScalarKeyValue = new ScalarKey(ScalarKeys.GameLib.Health.Modifier.Poison.TickInterval),
            ScalarPolicyValue = ScalarBindPolicy.ReplaceRuntime,
            UseClampMod = true,
            Clamp = new ScalarClamp { UseMin = true, Min = 0.1f }
        };

        [BoxGroup("Visual")]
        [LabelText("Visual Data")]
        [SerializeField]
        EffectVisualData _visualData = new()
        {
            IconAnimation = null,
            DisplayName = "毒",
            Description = "時間経過でダメージを受ける"
        };

        public override Type ProfileType => typeof(PoisonEffectPreset);

        public float DefaultDuration => _defaultDuration.Value;
        public float DefaultIntensity => _defaultIntensity.Value;
        public float DamagePerSecond => _damagePerSecond.Value;
        public float TickInterval => _tickInterval.Value;
        public EffectVisualData VisualData => _visualData;

        public EffectConfig CreateConfig(
            object source = null, float? overrideDuration = null, float? overrideIntensity = null)
        {
            return new EffectConfig
            {
                Duration = overrideDuration ?? DefaultDuration,
                Intensity = overrideIntensity ?? DefaultIntensity,
                StackMode = EffectStackMode.Refresh,
                Source = source,
                Tag = "Poison"
            };
        }
    }
}
