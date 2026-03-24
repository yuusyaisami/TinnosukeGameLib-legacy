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
        Refresh = 10,

        /// <summary>Duration を加算（残り時間に追加）</summary>
        ExtendDuration = 20,

        /// <summary>Intensity を加算（スタック数増加）</summary>
        StackIntensity = 30,

        /// <summary>両方加算（Duration + Intensity）</summary>
        StackBoth = 40,

        /// <summary>新しい効果を無視（既存を維持）</summary>
        Ignore = 50,

        /// <summary>既存を上書き</summary>
        Replace = 60,
    }
}
