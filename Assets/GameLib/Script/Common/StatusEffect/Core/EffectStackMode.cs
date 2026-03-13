// Game.StatusEffect.EffectStackMode.cs
//
// 同一効果が重複した場合の挙動

namespace Game.StatusEffect
{
    /// <summary>
    /// 同一効果が重複した場合の挙動
    /// </summary>
    public enum EffectStackMode
    {
        /// <summary>Duration をリフレッシュ（残り時間をリセット）</summary>
        Refresh,

        /// <summary>Duration を加算（残り時間に追加）</summary>
        ExtendDuration,

        /// <summary>Intensity を加算（スタック数増加）</summary>
        StackIntensity,

        /// <summary>両方加算（Duration + Intensity）</summary>
        StackBoth,

        /// <summary>新しい効果を無視（既存を維持）</summary>
        Ignore,

        /// <summary>既存を上書き</summary>
        Replace,
    }
}
