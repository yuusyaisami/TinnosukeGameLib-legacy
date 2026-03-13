// Game.StatusEffect.AttackBoostEffect.cs
//
// 攻撃力上昇効果

using Game.Health;
using Game.Scalar;
using Game.Scalar.Generated;

namespace Game.StatusEffect
{
    /// <summary>
    /// 攻撃力上昇効果。
    /// Scalar の AttackPower に加算を適用する。
    /// </summary>
    public sealed class AttackBoostEffect : BaseEffectRuntime
    {
        public const string FlagKey = "AttackBoost";

        // ScalarKey
        static readonly ScalarKey AttackPowerKey = new(ScalarKeys.GameLib.Combat.AttackPower);
        static readonly ScalarKey AttackBoostAmountKey = new(ScalarKeys.GameLib.StatusEffect.AttackBoost.Amount);

        ScalarHandle _scalarHandle;

        public override string EffectId => "StatusEffect.AttackBoost";
        public override EffectType Type => EffectType.Buff;

        // インライン VisualData
        public override EffectVisualData VisualData => _visualData;
        static readonly EffectVisualData _visualData = new()
        {
            DisplayName = "攻撃力アップ",
            Description = "攻撃力が上昇する",
            EffectType = EffectType.Buff,
            SortOrder = 20
        };

        /// <summary>
        /// 攻撃力加算値（ScalarKey から取得、または Intensity を使用）
        /// </summary>
        float AttackBoostAmount
        {
            get
            {
                float baseAmount = GetScalar(AttackBoostAmountKey);
                if (baseAmount <= 0f) baseAmount = 10f; // フォールバック
                return baseAmount * Intensity;
            }
        }

        protected override void OnApply()
        {
            SetFlag(FlagKey, true);

            _scalarHandle = AddScalar(AttackPowerKey, EffectId, AttackBoostAmount);
        }

        protected override void OnRemove()
        {
            RemoveFlag(FlagKey);
            _scalarHandle.Dispose();
        }

        protected override void OnStackIntensity(EffectConfig newConfig)
        {
            _scalarHandle.Dispose();
            _scalarHandle = AddScalar(AttackPowerKey, EffectId, AttackBoostAmount);
        }
    }
}
