#if UNITY_EDITOR
using UnityEngine;
using Game.Editor.Registry;

namespace Game.Actions.Editor
{
    /// <summary>
    /// GameState の Explorer / CodeGen 統合設定。
    /// </summary>
    [CreateAssetMenu(
        fileName = "GameStateSettings",
        menuName = "Game/Registry/Settings/Game State Settings")]
    public sealed class GameStateSettings : RegistrySettingsBase
    {
        void Reset()
        {
            windowTitle = "Game State Explorer";
            namespaceName = "Game.Actions";
            rootClassName = "GameState";
            outputPath = "Assets/Game/Scripts/Generated/GameStates.g.cs";
        }
    }
}
#endif
