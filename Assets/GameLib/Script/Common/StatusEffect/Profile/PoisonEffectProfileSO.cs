// Game.StatusEffect.PoisonEffectProfileSO.cs
//
// 毒エフェクト用のパラメータを定義する ProfileSO

using Game.Profile;
using Game.Scalar;
using Game.Scalar.Generated;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.StatusEffect
{
    /// <summary>
    /// 毒エフェクト用のパラメータを定義する ProfileSO。
    /// StatusEffect/Effects/PoisonEffect と連携する。
    /// </summary>
    [CreateAssetMenu(menuName = "Game/StatusEffect/PoisonEffectProfile", fileName = "PoisonEffectProfile")]
    public sealed class PoisonEffectProfileSO : BaseProfileSO
    {
        // ================================================================
        // 基本パラメータ
        // ================================================================

        [BoxGroup("Base")]
        [LabelText("Default Duration")]
        [Tooltip("毒エフェクトのデフォルト持続時間（秒）")]
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
        [Tooltip("毒エフェクトのデフォルト強度")]
        [SerializeField]
        ProfileFloatValue _defaultIntensity = new()
        {
            Value = 1f,
            ScalarKeyValue = new ScalarKey(ScalarKeys.GameLib.StatusEffect.Poison.DefaultIntensity),
            ScalarPolicyValue = ScalarBindPolicy.ReplaceRuntime,
            UseClampMod = true,
            Clamp = new ScalarClamp { UseMin = true, Min = 0f }
        };

        // ================================================================
        // ダメージパラメータ
        // ================================================================

        [BoxGroup("Damage")]
        [LabelText("Damage Per Second")]
        [Tooltip("毒状態の秒間ダメージ（Intensity で乗算される）")]
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
        [Tooltip("毒ダメージの適用間隔（秒）")]
        [SerializeField]
        ProfileFloatValue _tickInterval = new()
        {
            Value = 0.5f,
            ScalarKeyValue = new ScalarKey(ScalarKeys.GameLib.Health.Modifier.Poison.TickInterval),
            ScalarPolicyValue = ScalarBindPolicy.ReplaceRuntime,
            UseClampMod = true,
            Clamp = new ScalarClamp { UseMin = true, Min = 0.1f }
        };

        // ================================================================
        // ビジュアル
        // ================================================================

        [BoxGroup("Visual")]
        [LabelText("Visual Data")]
        [Tooltip("毒エフェクトの表示データ")]
        [SerializeField]
        EffectVisualData _visualData = new()
        {
            IconAnimation = null,
            DisplayName = "毒",
            Description = "時間経過でダメージを受ける"
        };

        // ================================================================
        // プロパティ
        // ================================================================

        /// <summary>デフォルト持続時間</summary>
        public float DefaultDuration => _defaultDuration.Value;

        /// <summary>デフォルト強度</summary>
        public float DefaultIntensity => _defaultIntensity.Value;

        /// <summary>秒間ダメージ</summary>
        public float DamagePerSecond => _damagePerSecond.Value;

        /// <summary>ダメージ間隔</summary>
        public float TickInterval => _tickInterval.Value;

        /// <summary>ビジュアルデータ</summary>
        public EffectVisualData VisualData => _visualData;

        // ================================================================
        // メソッド
        // ================================================================

        /// <summary>
        /// 現在の設定で EffectConfig を生成する。
        /// </summary>
        /// <param name="source">エフェクトの発生源</param>
        /// <param name="overrideDuration">持続時間の上書き（null で ProfileSO のデフォルト値を使用）</param>
        /// <param name="overrideIntensity">強度の上書き（null で ProfileSO のデフォルト値を使用）</param>
        /// <returns>生成された EffectConfig</returns>
        public EffectConfig CreateConfig(
            object source = null,
            float? overrideDuration = null,
            float? overrideIntensity = null)
        {
            return new EffectConfig
            {
                Duration = overrideDuration ?? DefaultDuration,
                Intensity = overrideIntensity ?? DefaultIntensity,
                StackMode = EffectStackMode.Refresh, // 毒は Refresh
                Source = source,
                Tag = "Poison"
            };
        }
    }
}
