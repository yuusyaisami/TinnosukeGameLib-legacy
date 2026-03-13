// Game.StatusEffect.EffectConfig.cs
//
// StatusEffect を適用する際の設定

namespace Game.StatusEffect
{
    /// <summary>
    /// StatusEffect を適用する際の設定。
    /// </summary>
    public struct EffectConfig
    {
        /// <summary>効果の持続時間（秒）。-1 で永続。</summary>
        public float Duration;

        /// <summary>効果の強度（Effect によって意味が異なる）</summary>
        public float Intensity;

        /// <summary>スタッキングモード</summary>
        public EffectStackMode StackMode;

        /// <summary>効果の発生源</summary>
        public object Source;

        /// <summary>追加タグ</summary>
        public string Tag;

        /// <summary>
        /// デフォルト設定を作成
        /// </summary>
        public static EffectConfig Default(float duration = -1f, float intensity = 1f)
        {
            return new EffectConfig
            {
                Duration = duration,
                Intensity = intensity,
                StackMode = EffectStackMode.Refresh,
                Source = null,
                Tag = null
            };
        }
    }
}
