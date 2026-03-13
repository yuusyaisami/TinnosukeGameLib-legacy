#nullable enable
using Game.MaterialFx;

namespace Game.SharedTexture
{
    /// <summary>
    /// tag ベースで MaterialFx / IMaterialFxReceiver を提供する interface。
    /// AnimationSpriteHubService 等に実装させる。
    /// </summary>
    public interface ITaggedMaterialFxProvider
    {
        bool TryGetMaterialFxReceiver(string tag, out IMaterialFxReceiver? receiver);
        bool TryGetMaterialFx(string tag, out IMaterialFxService? materialFx);
    }
}
