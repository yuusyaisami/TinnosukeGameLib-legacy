// Game.Health.HealthModifierPreset
using System;
using Game.Profile;
using Game.Scalar;
using Game.Scalar.Generated;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Health
{
    [Serializable]
    public sealed class HealthModifierPreset : BaseProfileData
    {
        [BoxGroup("Poison")]
        [LabelText("Damage Per Second")]
        [Tooltip("毒状態の秒間ダメージ")]
        [SerializeField]
        ProfileFloatValue _poisonDamagePerSecond = new()
        {
            Value = 5f,
            ScalarKeyValue = new ScalarKey(ScalarKeys.GameLib.Health.Modifier.Poison.DamagePerSecond),
            ScalarPolicyValue = ScalarBindPolicy.ReplaceRuntime,
            UseEffectMod = true,
            UseClampMod = true,
            Clamp = new ScalarClamp { UseMin = true, Min = 0f }
        };

        [BoxGroup("Poison")]
        [LabelText("Tick Interval")]
        [Tooltip("毒ダメージの適用間隔（秒）")]
        [SerializeField]
        ProfileFloatValue _poisonTickInterval = new()
        {
            Value = 0.5f,
            ScalarKeyValue = new ScalarKey(ScalarKeys.GameLib.Health.Modifier.Poison.TickInterval),
            ScalarPolicyValue = ScalarBindPolicy.ReplaceRuntime,
            UseClampMod = true,
            Clamp = new ScalarClamp { UseMin = true, Min = 0.1f }
        };

        [BoxGroup("DamageReduction")]
        [LabelText("Reduction Rate")]
        [Tooltip("ダメージ軽減率 (0.0 - 1.0)")]
        [SerializeField]
        ProfileFloatValue _damageReductionRate = new()
        {
            Value = 0.3f,
            ScalarKeyValue = new ScalarKey(ScalarKeys.GameLib.Health.Modifier.DamageReduction.Rate),
            ScalarPolicyValue = ScalarBindPolicy.ReplaceRuntime,
            UseEffectMod = true,
            UseClampMod = true,
            Clamp = new ScalarClamp { UseMin = true, Min = 0f, UseMax = true, Max = 1f }
        };

        [BoxGroup("HealBoost")]
        [LabelText("Boost Rate")]
        [Tooltip("回復量増加率 (1.0 = 100% 増加)")]
        [SerializeField]
        ProfileFloatValue _healBoostRate = new()
        {
            Value = 0.5f,
            ScalarKeyValue = new ScalarKey(ScalarKeys.GameLib.Health.Modifier.HealBoost.Rate),
            ScalarPolicyValue = ScalarBindPolicy.ReplaceRuntime,
            UseEffectMod = true,
            UseClampMod = true,
            Clamp = new ScalarClamp { UseMin = true, Min = 0f }
        };

        [BoxGroup("Critical")]
        [LabelText("Critical Multiplier")]
        [Tooltip("クリティカル時のダメージ倍率")]
        [SerializeField]
        ProfileFloatValue _criticalIncomingMultiplier = new()
        {
            Value = 2f,
            ScalarKeyValue = new ScalarKey(ScalarKeys.GameLib.Health.Modifier.Critical.IncomingMultiplier),
            ScalarPolicyValue = ScalarBindPolicy.ReplaceRuntime,
            UseEffectMod = true,
            UseClampMod = true,
            Clamp = new ScalarClamp { UseMin = true, Min = 1f }
        };

        [BoxGroup("Critical")]
        [LabelText("Incoming Critical Chance")]
        [Tooltip("被クリティカル率 (0.0 - 1.0)")]
        [SerializeField]
        ProfileFloatValue _criticalIncomingChance = new()
        {
            Value = 0.05f,
            ScalarKeyValue = new ScalarKey(ScalarKeys.GameLib.Health.Modifier.Critical.IncomingChance),
            ScalarPolicyValue = ScalarBindPolicy.ReplaceRuntime,
            UseEffectMod = true,
            UseClampMod = true,
            Clamp = new ScalarClamp { UseMin = true, Min = 0f, UseMax = true, Max = 1f }
        };

        [BoxGroup("Critical")]
        [LabelText("Outgoing Critical Multiplier")]
        [Tooltip("与クリティカル時のダメージ倍率")]
        [SerializeField]
        ProfileFloatValue _criticalOutgoingMultiplier = new()
        {
            Value = 2f,
            ScalarKeyValue = new ScalarKey(ScalarKeys.GameLib.Health.Modifier.Critical.OutgoingMultiplier),
            ScalarPolicyValue = ScalarBindPolicy.ReplaceRuntime,
            UseEffectMod = true,
            UseClampMod = true,
            Clamp = new ScalarClamp { UseMin = true, Min = 1f }
        };

        [BoxGroup("Critical")]
        [LabelText("Outgoing Critical Chance")]
        [Tooltip("与クリティカル率 (0.0 - 1.0)")]
        [SerializeField]
        ProfileFloatValue _criticalOutgoingChance = new()
        {
            Value = 0.1f,
            ScalarKeyValue = new ScalarKey(ScalarKeys.GameLib.Health.Modifier.Critical.OutgoingChance),
            ScalarPolicyValue = ScalarBindPolicy.ReplaceRuntime,
            UseEffectMod = true,
            UseClampMod = true,
            Clamp = new ScalarClamp { UseMin = true, Min = 0f, UseMax = true, Max = 1f }
        };

        public override Type ProfileType => typeof(HealthModifierPreset);

        internal static HealthModifierPreset CreateFromLegacyFields(
            ProfileFloatValue poisonDPS, ProfileFloatValue poisonTick,
            ProfileFloatValue dmgReduction, ProfileFloatValue healBoost,
            ProfileFloatValue critInMul, ProfileFloatValue critInChance,
            ProfileFloatValue critOutMul, ProfileFloatValue critOutChance)
        {
            return new HealthModifierPreset
            {
                _poisonDamagePerSecond = poisonDPS,
                _poisonTickInterval = poisonTick,
                _damageReductionRate = dmgReduction,
                _healBoostRate = healBoost,
                _criticalIncomingMultiplier = critInMul,
                _criticalIncomingChance = critInChance,
                _criticalOutgoingMultiplier = critOutMul,
                _criticalOutgoingChance = critOutChance
            };
        }
    }
}
