// Game.Common.IDynamicValueAsset
//
// Preset を返す薄い asset wrapper の共通 interface。
// DynamicValue の generic asset source から利用される。

#nullable enable

namespace Game.Common
{
    /// <summary>
    /// ScriptableObject asset wrapper が Preset を返すための共通 interface。
    /// <see cref="ManagedRefAssetSource{TAsset,TValue}"/> で統一的に扱える。
    /// </summary>
    public interface IDynamicValueAsset<out TValue> where TValue : class
    {
        TValue? Preset { get; }
    }
}
