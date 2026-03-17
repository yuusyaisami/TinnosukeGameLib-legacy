// Game.StatusEffect.SpeedModEffectPreset
using System;
using Game.Profile;
using Game.Scalar;
using Game.Scalar.Generated;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.StatusEffect
{
    [Serializable]
    public sealed class SpeedModEffectPreset : BaseProfileData
    {
        [FoldoutGroup("SpeedBoost")]
        [LabelText("Default Duration")]
        [SerializeField]
        ProfileFloatValue _boostDefaultDuration = new()
        {
            Value = 5f,
            ScalarKeyValue = new ScalarKey(ScalarKeys.GameLib.StatusEffect.SpeedMod.DefaultDuration),
            ScalarPolicyValue = ScalarBindPolicy.ReplaceRuntime,
            UseClampMod = true,
            Clamp = new ScalarClamp { UseMin = true, Min = 0f }
        };

        [FoldoutGroup("SpeedBoost")]
        [LabelText("Default Intensity")]
        [SerializeField]
        ProfileFloatValue _boostDefaultIntensity = new()
        {
            Value = 0.5f,
            ScalarKeyValue = new ScalarKey(ScalarKeys.GameLib.StatusEffect.SpeedMod.DefaultIntensity),
            ScalarPolicyValue = ScalarBindPolicy.ReplaceRuntime,
            UseClampMod = true,
            Clamp = new ScalarClamp { UseMin = true, Min = 0f }
        };

        [FoldoutGroup("SpeedBoost")]
        [LabelText("Base Multiplier")]
        [SerializeField]
        ProfileFloatValue _boostBaseMultiplier = new()
        {
            Value = 1.5f,
            ScalarKeyValue = new ScalarKey(ScalarKeys.GameLib.StatusEffect.SpeedMod.Multiplier),
            ScalarPolicyValue = ScalarBindPolicy.ReplaceRuntime,
            UseEffectMod = true,
            UseClampMod = true,
            Clamp = new ScalarClamp { UseMin = true, Min = 1f }
        };

        [FoldoutGroup("Slow")]
        [LabelText("Default Duration")]
        [SerializeField]
        ProfileFloatValue _slowDefaultDuration = new()
        {
            Value = 3f,
            ScalarKeyValue = new ScalarKey(ScalarKeys.GameLib.StatusEffect.Slow.DefaultDuration),
            ScalarPolicyValue = ScalarBindPolicy.ReplaceRuntime,
            UseClampMod = true,
            Clamp = new ScalarClamp { UseMin = true, Min = 0f }
        };

        [FoldoutGroup("Slow")]
        [LabelText("Default Intensity")]
        [SerializeField]
        ProfileFloatValue _slowDefaultIntensity = new()
        {
            Value = 0.3f,
            ScalarKeyValue = new ScalarKey(ScalarKeys.GameLib.StatusEffect.Slow.DefaultIntensity),
            ScalarPolicyValue = ScalarBindPolicy.ReplaceRuntime,
            UseClampMod = true,
            Clamp = new ScalarClamp { UseMin = true, Min = 0f, UseMax = true, Max = 0.9f }
        };

        [FoldoutGroup("Visual")]
        [LabelText("SpeedBoost Visual")]
        [SerializeField]
        EffectVisualData _boostVisualData = new()
        {
            IconAnimation = null,
            DisplayName = "加速",
            Description = "移動速度が上昇している"
        };

        [FoldoutGroup("Visual")]
        [LabelText("Slow Visual")]
        [SerializeField]
        EffectVisualData _slowVisualData = new()
        {
            IconAnimation = null,
            DisplayName = "減速",
            Description = "移動速度が低下している"
        };

        public override Type ProfileType => typeof(SpeedModEffectPreset);

        public float BoostDefaultDuration => _boostDefaultDuration.Value;
        public float BoostDefaultIntensity => _boostDefaultIntensity.Value;
        public float BoostBaseMultiplier => _boostBaseMultiplier.Value;
        public EffectVisualData BoostVisualData => _boostVisualData;
        public float SlowDefaultDuration => _slowDefaultDuration.Value;
        public float SlowDefaultIntensity => _slowDefaultIntensity.Value;
        public EffectVisualData SlowVisualData => _slowVisualData;

        public EffectConfig CreateSpeedBoostConfig(
            object source = null, float? overrideDuration = null, float? overrideIntensity = null)
        {
            return new EffectConfig
            {
                Duration = overrideDuration ?? BoostDefaultDuration,
                Intensity = overrideIntensity ?? BoostDefaultIntensity,
                StackMode = EffectStackMode.Refresh,
                Source = source,
                Tag = "SpeedBoost"
            };
        }

        public EffectConfig CreateSlowConfig(
            object source = null, float? overrideDuration = null, float? overrideIntensity = null)
        {
            return new EffectConfig
            {
                Duration = overrideDuration ?? SlowDefaultDuration,
                Intensity = overrideIntensity ?? SlowDefaultIntensity,
                StackMode = EffectStackMode.Refresh,
                Source = source,
                Tag = "Slow"
            };
        }
    }
}
