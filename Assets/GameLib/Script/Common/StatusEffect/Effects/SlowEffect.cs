// Game.StatusEffect.SlowEffect.cs
//
// 鈍足効果

using Game.Health;
using Game.Scalar;
using Game.Scalar.Generated;
using UnityEngine;

namespace Game.StatusEffect
{
    /// <summary>
    /// 鈍足効果。
    /// Scalar の DefaultSpeed に減速倍率を適用する。
    /// </summary>
    public sealed class SlowEffect : BaseEffectRuntime
    {
        public const string FlagKey = "Slow";

        static readonly ScalarKey DefaultSpeedKey = new(ScalarKeys.GameLib.Movement.DefaultSpeed);

        ScalarHandle _scalarHandle;

        public override string EffectId => "StatusEffect.Slow";
        public override EffectType Type => EffectType.Debuff;

        // インライン VisualData
        public override EffectVisualData VisualData => _visualData;
        static readonly EffectVisualData _visualData = new()
        {
            DisplayName = "鈍足",
            Description = "移動速度が低下する",
            EffectType = EffectType.Debuff,
            SortOrder = 100
        };

        /// <summary>
        /// 減速倍率。Intensity から計算（例: Intensity 0.3 → 0.7 倍速）
        /// </summary>
        float SlowMultiplier => Mathf.Clamp01(1f - Intensity);

        protected override void OnApply()
        {
            SetFlag(FlagKey, true);

            _scalarHandle = MulScalar(
                DefaultSpeedKey,
                EffectId,
                SlowMultiplier,
                ScalarMulPhase.PreAdd
            );
        }

        protected override void OnRemove()
        {
            RemoveFlag(FlagKey);
            _scalarHandle.Dispose();
        }

        protected override void OnStackIntensity(EffectConfig newConfig)
        {
            _scalarHandle.Dispose();

            _scalarHandle = MulScalar(
                DefaultSpeedKey,
                EffectId,
                SlowMultiplier,
                ScalarMulPhase.PreAdd
            );
        }
    }
}
