// Game.StatusEffect.SpeedBoostEffect.cs
//
// 移動速度上昇効果

using Game.Health;
using Game.Scalar;
using Game.Scalar.Generated;

namespace Game.StatusEffect
{
    /// <summary>
    /// 移動速度上昇効果。
    /// Scalar の DefaultSpeed に倍率を適用する。
    /// 倍率は SpeedModEffectProfileSO で定義された ScalarKey から取得。
    /// </summary>
    public sealed class SpeedBoostEffect : BaseEffectRuntime
    {
        public const string FlagKey = "SpeedBoost";

        // ScalarKey（ProfileSO で定義）
        static readonly ScalarKey SpeedMultiplierKey = new(ScalarKeys.GameLib.StatusEffect.SpeedMod.Multiplier);
        static readonly ScalarKey DefaultSpeedKey = new(ScalarKeys.GameLib.Movement.DefaultSpeed);

        SpeedModEffectProfileSO _profile;
        ScalarHandle _scalarHandle;

        public override string EffectId => "StatusEffect.SpeedBoost";
        public override EffectType Type => EffectType.Buff;

        // ProfileSO から表示データを取得
        public override EffectVisualData VisualData => _profile?.BoostVisualData ?? _defaultVisualData;
        static readonly EffectVisualData _defaultVisualData = new()
        {
            DisplayName = "加速",
            Description = "移動速度が上昇する",
            EffectType = EffectType.Buff,
            SortOrder = 20
        };

        /// <summary>
        /// 移動速度倍率（ScalarKey から取得）。ProfileSO で設定された値 + EffectConfig.Intensity による修正。
        /// Mul セマンティクス: baseMul * (1 + Intensity)
        /// </summary>
        float SpeedMultiplier
        {
            get
            {
                float baseMul = GetScalar(SpeedMultiplierKey);
                if (baseMul <= 0f) baseMul = 1.5f; // フォールバック
                // Intensity を追加倍率として乗算（例: base 1.3, Intensity 0.2 → 1.3 * (1+0.2) = 1.56）
                return baseMul * (1f + Intensity);
            }
        }

        protected override void OnInitialize()
        {
            // ProfileRegistry から ProfileSO を取得
            Context.ProfileRegistry?.TryResolve(out _profile);
        }

        protected override void OnApply()
        {
            SetFlag(FlagKey, true);

            // Scalar に Mul 効果を適用
            _scalarHandle = MulScalar(
                DefaultSpeedKey,
                EffectId,
                SpeedMultiplier,
                ScalarMulPhase.PreAdd
            );
        }

        protected override void OnRemove()
        {
            RemoveFlag(FlagKey);

            // Scalar 効果を解除
            _scalarHandle.Dispose();
        }

        protected override void OnStackIntensity(EffectConfig newConfig)
        {
            // スタック時は Scalar 効果を更新
            _scalarHandle.Dispose();

            _scalarHandle = MulScalar(
                DefaultSpeedKey,
                EffectId,
                SpeedMultiplier,
                ScalarMulPhase.PreAdd
            );
        }
    }
}
