#if UNITY_EDITOR
using UnityEngine;

namespace Game.Editor.Registry
{
    /// <summary>
    /// EventKey の Explorer / CodeGen 統合設定。
    /// </summary>
    [CreateAssetMenu(
        fileName = "EventKeySettings",
        menuName = "Game/Registry/Settings/Event Key Settings")]
    public sealed class EventKeySettings : RegistrySettingsBase
    {
        void Reset()
        {
            windowTitle = "Event Key Explorer";
            namespaceName = "Game.Events.Generated";
            rootClassName = "EventKeys";
            outputPath = "Assets/GameLib/Script/Generated/EventKeys.g.cs";
        }
    }
}
#endif
