// Game.StatusEffect.DamageReductionEffect.cs
//
// ダメージ軽減効果

using Game.Health;

namespace Game.StatusEffect
{
    /// <summary>
    /// ダメージ軽減効果。
    /// BoolLayer に "DamageReduction" フラグを立て、DamageReductionModifier を有効化する。
    /// </summary>
    public sealed class DamageReductionEffect : BaseEffectRuntime
    {
        public const string FlagKey = "DamageReduction";

        public override string EffectId => "StatusEffect.DamageReduction";
        public override EffectType Type => EffectType.Buff;

        // インライン VisualData
        public override EffectVisualData VisualData => _visualData;
        static readonly EffectVisualData _visualData = new()
        {
            DisplayName = "防御強化",
            Description = "受けるダメージが軽減される",
            EffectType = EffectType.Buff,
            SortOrder = 50
        };

        protected override void OnApply()
        {
            SetFlag(FlagKey, true);
        }

        protected override void OnRemove()
        {
            RemoveFlag(FlagKey);
        }
    }
}
