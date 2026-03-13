// Game.StatusEffect.SpeedModEffectProfileSO.cs
//
// スピード変更エフェクト（SpeedBoost / Slow）用のパラメータを定義する ProfileSO

using Game.Profile;
using Game.Scalar;
using Game.Scalar.Generated;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.StatusEffect
{
    /// <summary>
    /// スピード変更エフェクト（SpeedBoost / Slow）用のパラメータを定義する ProfileSO。
    /// StatusEffect/Effects/SpeedBoostEffect, SlowEffect と連携する。
    /// </summary>
    [CreateAssetMenu(menuName = "Game/StatusEffect/SpeedModEffectProfile", fileName = "SpeedModEffectProfile")]
    public sealed class SpeedModEffectProfileSO : BaseProfileSO
    {
        // ================================================================
        // SpeedBoost 設定
        // ================================================================

        [FoldoutGroup("SpeedBoost")]
        [LabelText("Default Duration")]
        [Tooltip("スピードブーストのデフォルト持続時間（秒）")]
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
        [Tooltip("スピードブーストのデフォルト強度（速度増加率）")]
        [SerializeField]
        ProfileFloatValue _boostDefaultIntensity = new()
        {
            Value = 0.5f, // 50% 増加
            ScalarKeyValue = new ScalarKey(ScalarKeys.GameLib.StatusEffect.SpeedMod.DefaultIntensity),
            ScalarPolicyValue = ScalarBindPolicy.ReplaceRuntime,
            UseClampMod = true,
            Clamp = new ScalarClamp { UseMin = true, Min = 0f }
        };

        [FoldoutGroup("SpeedBoost")]
        [LabelText("Base Multiplier")]
        [Tooltip("スピードブーストの基本乗算倍率")]
        [SerializeField]
        ProfileFloatValue _boostBaseMultiplier = new()
        {
            Value = 1.5f, // 1.5倍速
            ScalarKeyValue = new ScalarKey(ScalarKeys.GameLib.StatusEffect.SpeedMod.Multiplier),
            ScalarPolicyValue = ScalarBindPolicy.ReplaceRuntime,
            UseEffectMod = true,
            UseClampMod = true,
            Clamp = new ScalarClamp { UseMin = true, Min = 1f }
        };

        // ================================================================
        // Slow 設定
        // ================================================================

        [FoldoutGroup("Slow")]
        [LabelText("Default Duration")]
        [Tooltip("スローのデフォルト持続時間（秒）")]
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
        [Tooltip("スローのデフォルト強度（速度減少率、0.3 = 30% 減速）")]
        [SerializeField]
        ProfileFloatValue _slowDefaultIntensity = new()
        {
            Value = 0.3f, // 30% 減速
            ScalarKeyValue = new ScalarKey(ScalarKeys.GameLib.StatusEffect.Slow.DefaultIntensity),
            ScalarPolicyValue = ScalarBindPolicy.ReplaceRuntime,
            UseClampMod = true,
            Clamp = new ScalarClamp { UseMin = true, Min = 0f, UseMax = true, Max = 0.9f }
        };

        // ================================================================
        // ビジュアル
        // ================================================================

        [FoldoutGroup("Visual")]
        [LabelText("SpeedBoost Visual")]
        [Tooltip("スピードブーストの表示データ")]
        [SerializeField]
        EffectVisualData _boostVisualData = new()
        {
            IconAnimation = null,
            DisplayName = "加速",
            Description = "移動速度が上昇している"
        };

        [FoldoutGroup("Visual")]
        [LabelText("Slow Visual")]
        [Tooltip("スローの表示データ")]
        [SerializeField]
        EffectVisualData _slowVisualData = new()
        {
            IconAnimation = null,
            DisplayName = "減速",
            Description = "移動速度が低下している"
        };

        // ================================================================
        // プロパティ - SpeedBoost
        // ================================================================

        /// <summary>スピードブーストのデフォルト持続時間</summary>
        public float BoostDefaultDuration => _boostDefaultDuration.Value;

        /// <summary>スピードブーストのデフォルト強度</summary>
        public float BoostDefaultIntensity => _boostDefaultIntensity.Value;

        /// <summary>スピードブーストの基本乗算倍率</summary>
        public float BoostBaseMultiplier => _boostBaseMultiplier.Value;

        /// <summary>スピードブーストのビジュアルデータ</summary>
        public EffectVisualData BoostVisualData => _boostVisualData;

        // ================================================================
        // プロパティ - Slow
        // ================================================================

        /// <summary>スローのデフォルト持続時間</summary>
        public float SlowDefaultDuration => _slowDefaultDuration.Value;

        /// <summary>スローのデフォルト強度</summary>
        public float SlowDefaultIntensity => _slowDefaultIntensity.Value;

        /// <summary>スローのビジュアルデータ</summary>
        public EffectVisualData SlowVisualData => _slowVisualData;

        // ================================================================
        // メソッド
        // ================================================================

        /// <summary>
        /// SpeedBoost 用の EffectConfig を生成する。
        /// </summary>
        public EffectConfig CreateSpeedBoostConfig(
            object source = null,
            float? overrideDuration = null,
            float? overrideIntensity = null)
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

        /// <summary>
        /// Slow 用の EffectConfig を生成する。
        /// </summary>
        public EffectConfig CreateSlowConfig(
            object source = null,
            float? overrideDuration = null,
            float? overrideIntensity = null)
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
