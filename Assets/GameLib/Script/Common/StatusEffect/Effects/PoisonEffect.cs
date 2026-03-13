// Game.StatusEffect.PoisonEffect.cs
//
// 毒状態効果

using Game.Health;

namespace Game.StatusEffect
{
    /// <summary>
    /// 毒状態効果。
    /// BoolLayer に "Poison" フラグを立て、PoisonDamageModifier を有効化する。
    /// パラメータは PoisonEffectProfileSO で定義された ScalarKey から取得。
    /// </summary>
    public sealed class PoisonEffect : BaseEffectRuntime
    {
        public const string FlagKey = "Poison";

        // ProfileSO から取得する VisualData
        PoisonEffectProfileSO _profile;

        public override string EffectId => "StatusEffect.Poison";
        public override EffectType Type => EffectType.Debuff;

        // ProfileSO から表示データを取得
        public override EffectVisualData VisualData => _profile?.VisualData ?? base.VisualData;

        protected override void OnInitialize()
        {
            // ProfileRegistry から ProfileSO を取得
            Context.ProfileRegistry?.TryResolve(out _profile);
        }

        protected override void OnApply()
        {
            // BoolLayer にフラグを設定
            // → PoisonDamageModifier が有効化される
            SetFlag(FlagKey, true);
        }

        protected override void OnRemove()
        {
            // フラグを解除
            // → PoisonDamageModifier が無効化される
            RemoveFlag(FlagKey);
        }

        protected override void OnStackRefresh(EffectConfig newConfig)
        {
            // 時間がリフレッシュされただけなのでフラグはそのまま
        }

        protected override void OnStackExtend(EffectConfig newConfig)
        {
            // 時間が延長されただけなのでフラグはそのまま
        }
    }
}
