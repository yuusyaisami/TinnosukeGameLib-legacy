// Game.Health.HealthModifierPreset
using System;
using Game.Common;
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
        [Tooltip("豈堤憾諷九・遘帝俣繝繝｡繝ｼ繧ｸ")]
        [SerializeField]
        ProfileFloatValue _poisonDamagePerSecond = new()
        {
            Value = 5f,
            ScalarKeyValue = new ScalarKey(ScalarKeys.GameLib.Health.Modifier.Poison.DamagePerSecond),
            ScalarPolicyValue = ScalarBindPolicy.ReplaceRuntime,
            UseEffectMod = true,
            UseClampMod = true,
            Clamp = new ScalarClamp { UseMin = true, Min = DynamicValueExtensions.FromLiteral(0f) }
        };

        [BoxGroup("Poison")]
        [LabelText("Tick Interval")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        ProfileFloatValue _poisonTickInterval = new()
        {
            Value = 0.5f,
            ScalarKeyValue = new ScalarKey(ScalarKeys.GameLib.Health.Modifier.Poison.TickInterval),
            ScalarPolicyValue = ScalarBindPolicy.ReplaceRuntime,
            UseClampMod = true,
            Clamp = new ScalarClamp { UseMin = true, Min = DynamicValueExtensions.FromLiteral(0.1f) }
        };

        [BoxGroup("DamageReduction")]
        [LabelText("Reduction Rate")]
        [Tooltip("繝繝｡繝ｼ繧ｸ霆ｽ貂帷紫 (0.0 - 1.0)")]
        [SerializeField]
        ProfileFloatValue _damageReductionRate = new()
        {
            Value = 0.3f,
            ScalarKeyValue = new ScalarKey(ScalarKeys.GameLib.Health.Modifier.DamageReduction.Rate),
            ScalarPolicyValue = ScalarBindPolicy.ReplaceRuntime,
            UseEffectMod = true,
            UseClampMod = true,
            Clamp = new ScalarClamp
            {
                UseMin = true,
                Min = DynamicValueExtensions.FromLiteral(0f),
                UseMax = true,
                Max = DynamicValueExtensions.FromLiteral(1f)
            }
        };

        [BoxGroup("HealBoost")]
        [LabelText("Boost Rate")]
        [Tooltip("蝗槫ｾｩ驥丞｢怜刈邇・(1.0 = 100% 蠅怜刈)")]
        [SerializeField]
        ProfileFloatValue _healBoostRate = new()
        {
            Value = 0.5f,
            ScalarKeyValue = new ScalarKey(ScalarKeys.GameLib.Health.Modifier.HealBoost.Rate),
            ScalarPolicyValue = ScalarBindPolicy.ReplaceRuntime,
            UseEffectMod = true,
            UseClampMod = true,
            Clamp = new ScalarClamp { UseMin = true, Min = DynamicValueExtensions.FromLiteral(0f) }
        };

        [BoxGroup("Critical")]
        [LabelText("Critical Multiplier")]
        [Tooltip("繧ｯ繝ｪ繝・ぅ繧ｫ繝ｫ譎ゅ・繝繝｡繝ｼ繧ｸ蛟咲紫")]
        [SerializeField]
        ProfileFloatValue _criticalIncomingMultiplier = new()
        {
            Value = 2f,
            ScalarKeyValue = new ScalarKey(ScalarKeys.GameLib.Health.Modifier.Critical.IncomingMultiplier),
            ScalarPolicyValue = ScalarBindPolicy.ReplaceRuntime,
            UseEffectMod = true,
            UseClampMod = true,
            Clamp = new ScalarClamp { UseMin = true, Min = DynamicValueExtensions.FromLiteral(1f) }
        };

        [BoxGroup("Critical")]
        [LabelText("Incoming Critical Chance")]
        [Tooltip("陲ｫ繧ｯ繝ｪ繝・ぅ繧ｫ繝ｫ邇・(0.0 - 1.0)")]
        [SerializeField]
        ProfileFloatValue _criticalIncomingChance = new()
        {
            Value = 0.05f,
            ScalarKeyValue = new ScalarKey(ScalarKeys.GameLib.Health.Modifier.Critical.IncomingChance),
            ScalarPolicyValue = ScalarBindPolicy.ReplaceRuntime,
            UseEffectMod = true,
            UseClampMod = true,
            Clamp = new ScalarClamp
            {
                UseMin = true,
                Min = DynamicValueExtensions.FromLiteral(0f),
                UseMax = true,
                Max = DynamicValueExtensions.FromLiteral(1f)
            }
        };

        [BoxGroup("Critical")]
        [LabelText("Outgoing Critical Multiplier")]
        [Tooltip("荳弱け繝ｪ繝・ぅ繧ｫ繝ｫ譎ゅ・繝繝｡繝ｼ繧ｸ蛟咲紫")]
        [SerializeField]
        ProfileFloatValue _criticalOutgoingMultiplier = new()
        {
            Value = 2f,
            ScalarKeyValue = new ScalarKey(ScalarKeys.GameLib.Health.Modifier.Critical.OutgoingMultiplier),
            ScalarPolicyValue = ScalarBindPolicy.ReplaceRuntime,
            UseEffectMod = true,
            UseClampMod = true,
            Clamp = new ScalarClamp { UseMin = true, Min = DynamicValueExtensions.FromLiteral(1f) }
        };

        [BoxGroup("Critical")]
        [LabelText("Outgoing Critical Chance")]
        [Tooltip("荳弱け繝ｪ繝・ぅ繧ｫ繝ｫ邇・(0.0 - 1.0)")]
        [SerializeField]
        ProfileFloatValue _criticalOutgoingChance = new()
        {
            Value = 0.1f,
            ScalarKeyValue = new ScalarKey(ScalarKeys.GameLib.Health.Modifier.Critical.OutgoingChance),
            ScalarPolicyValue = ScalarBindPolicy.ReplaceRuntime,
            UseEffectMod = true,
            UseClampMod = true,
            Clamp = new ScalarClamp
            {
                UseMin = true,
                Min = DynamicValueExtensions.FromLiteral(0f),
                UseMax = true,
                Max = DynamicValueExtensions.FromLiteral(1f)
            }
        };

        public override Type ProfileType => typeof(HealthModifierPreset);
    }
}
