// Game.Health.HealthPreset.cs
//
// Health 関連の設定を保持する inline preset。

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
        [Tooltip("最大体力。Scalar に自動登録される。")]
        [SerializeField]
        ProfileFloatValue maxHP = CreateDefaultMaxHP();

        [BoxGroup("Health")]
        [LabelText("Initial HP Mode")]
        [Tooltip("初期HPを割合で指定するか、固定値で指定するか。")]
        [SerializeField]
        HealthInitialHPMode initialHPMode = HealthInitialHPMode.InitialHPRatio;

        [BoxGroup("Health")]
        [LabelText("Initial HP Ratio")]
        [ShowIf(nameof(ShowInitialHPRatio))]
        [Range(0f, 1f)]
        [Tooltip("スポーン時の HP 割合 (0.0 - 1.0)")]
        [SerializeField]
        float initialHPRatio = 1f;

        [BoxGroup("Health")]
        [LabelText("Initial HP Value")]
        [ShowIf(nameof(ShowInitialHPValue))]
        [MinValue(0f)]
        [Tooltip("スポーン時の初期HP固定値。最大HPを超える場合は最大HPに丸める。")]
        [SerializeField]
        float initialHPValue = 100f;

        [BoxGroup("Spawn")]
        [LabelText("Invincible Duration On Spawn")]
        [Tooltip("スポーン時の無敵時間（秒）。0 で無効。")]
        [SerializeField]
        float invincibleDurationOnSpawn = 0f;

        [BoxGroup("Damage Invincible")]
        [LabelText("Enable On Damaged")]
        [Tooltip("ダメージ適用時に一定時間無敵を付与します。")]
        [SerializeField]
        bool enableInvincibleOnDamaged;

        [BoxGroup("Damage Invincible")]
        [ShowIf(nameof(enableInvincibleOnDamaged))]
        [LabelText("Duration")]
        [MinValue(0f)]
        [Tooltip("被弾時に付与する無敵時間（秒）。")]
        [SerializeField]
        float invincibleDurationOnDamaged = 0.1f;

        [BoxGroup("Death")]
        [LabelText("Death Delay")]
        [Tooltip("死亡判定後、OnDeath イベント発行までの遅延（秒）")]
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
