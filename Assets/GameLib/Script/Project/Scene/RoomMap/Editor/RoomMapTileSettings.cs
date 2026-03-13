#if UNITY_EDITOR
using Game.Editor.Registry;
using UnityEngine;

namespace Game.RoomMap.Editor
{
    [CreateAssetMenu(
        fileName = "RoomMapTileSettings",
        menuName = "Game/Registry/Settings/RoomMap Tile Settings")]
    public sealed class RoomMapTileSettings : RegistrySettingsBase
    {
        void Reset()
        {
            windowTitle = "RoomMap Tile Explorer";
            namespaceName = "Game.RoomMap.Generated";
            rootClassName = "RoomMapTileIds";
            outputPath = "Assets/GameLib/Script/Generated/RoomMapTileIds.g.cs";
        }
    }
}
#endif
