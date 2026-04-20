// Game.Health.HealthPreset.cs
//
// Health 髢｢騾｣縺ｮ險ｭ螳壹ｒ菫晄戟縺吶ｋ inline preset縲・

#nullable enable

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
    public sealed class HealthPreset : BaseProfileData
    {
        [BoxGroup("Health")]
        [LabelText("Max HP")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        ProfileFloatValue maxHP = CreateDefaultMaxHP();

        [BoxGroup("Health")]
        [LabelText("Initial HP Mode")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        HealthInitialHPMode initialHPMode = HealthInitialHPMode.InitialHPRatio;

        [BoxGroup("Health")]
        [LabelText("Initial HP Ratio")]
        [ShowIf(nameof(ShowInitialHPRatio))]
        [Range(0f, 1f)]
        [Tooltip("繧ｹ繝昴・繝ｳ譎ゅ・ HP 蜑ｲ蜷・(0.0 - 1.0)")]
        [SerializeField]
        float initialHPRatio = 1f;

        [BoxGroup("Health")]
        [LabelText("Initial HP Value")]
        [ShowIf(nameof(ShowInitialHPValue))]
        [MinValue(0f)]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        float initialHPValue = 100f;

        [BoxGroup("Spawn")]
        [LabelText("Invincible Duration On Spawn")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        float invincibleDurationOnSpawn = 0f;

        [BoxGroup("Damage Invincible")]
        [LabelText("Enable On Damaged")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        bool enableInvincibleOnDamaged;

        [BoxGroup("Damage Invincible")]
        [ShowIf(nameof(enableInvincibleOnDamaged))]
        [LabelText("Duration")]
        [MinValue(0f)]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        float invincibleDurationOnDamaged = 0.1f;

        [BoxGroup("Death")]
        [LabelText("Death Delay")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        float deathDelay = 0f;

        public override Type ProfileType => typeof(HealthPreset);
        public float MaxHPFallback => maxHP.Value;
        public HealthInitialHPMode InitialHPMode => initialHPMode;
        public float InitialHPRatio => initialHPRatio;
        public float InitialHPValue => initialHPValue;
        public float InvincibleDurationOnSpawn => invincibleDurationOnSpawn;
        public bool EnableInvincibleOnDamaged => enableInvincibleOnDamaged;
        public float InvincibleDurationOnDamaged => invincibleDurationOnDamaged;
        public float DeathDelay => deathDelay;

        bool ShowInitialHPRatio() => initialHPMode == HealthInitialHPMode.InitialHPRatio;
        bool ShowInitialHPValue() => initialHPMode == HealthInitialHPMode.CustomValue;

        static ProfileFloatValue CreateDefaultMaxHP()
        {
            return new ProfileFloatValue
            {
                Value = 100f,
                ScalarKeyValue = new ScalarKey(ScalarKeys.GameLib.Health.Max),
                ScalarPolicyValue = ScalarBindPolicy.ReplaceRuntime,
                UseEffectMod = false,
                UseClampMod = true,
                Clamp = new ScalarClamp
                {
                    UseMin = true,
                    Min = DynamicValueExtensions.FromLiteral(1f),
                    UseMax = false
                }
            };
        }
    }
}
