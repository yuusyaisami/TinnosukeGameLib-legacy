// Game.StatusEffect.InvincibleEffect.cs
//
// 無敵状態効果

using Game.Health;

namespace Game.StatusEffect
{
    /// <summary>
    /// 無敵状態効果。
    /// HealthService の InvincibleLayer にフラグを立てる。
    /// </summary>
    public sealed class InvincibleEffect : BaseEffectRuntime
    {
        public const string FlagKey = "Invincible";

        public override string EffectId => "StatusEffect.Invincible";
        public override EffectType Type => EffectType.Buff;

        // インライン定義の VisualData
        public override EffectVisualData VisualData => _visualData;
        static readonly EffectVisualData _visualData = new()
        {
            DisplayName = "無敵",
            Description = "ダメージを受けない",
            EffectType = EffectType.Buff,
            SortOrder = 0
        };

        protected override void OnApply()
        {
            // HealthService の InvincibleLayer にフラグを設定
            Context.HealthService?.InvincibleLayer?.Set(EffectId, true);
        }

        protected override void OnRemove()
        {
            Context.HealthService?.InvincibleLayer?.Remove(EffectId);
        }
    }
}
