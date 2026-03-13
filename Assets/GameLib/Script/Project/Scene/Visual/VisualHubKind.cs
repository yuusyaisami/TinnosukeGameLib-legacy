#nullable enable

namespace Game.Visual
{
    /// <summary>
    /// VisualSystem が扱う Hub の分類。
    /// v1 は最小限（必要なら後で増やす）。
    /// </summary>
    public enum VisualHubKind
    {
        SpriteAnimation = 0,
        UI = 1,
        WorldUI = 2,
        Background = 3,
        PostProcess = 4,
    }
}
