#if UNITY_EDITOR
using UnityEngine;

namespace Game.Editor.Registry
{
    /// <summary>
    /// StateKey の Explorer / CodeGen 統合設定。
    /// </summary>
    [CreateAssetMenu(
        fileName = "StateKeySettings",
        menuName = "Game/Registry/Settings/State Key Settings")]
    public sealed class StateKeySettings : RegistrySettingsBase
    {
        void Reset()
        {
            windowTitle = "State Key Explorer";
            namespaceName = "Game.StateMachine.Generated";
            rootClassName = "StateKeys";
            outputPath = "Assets/GameLib/Script/Generated/StateKeys.g.cs";
        }
    }
}
#endif
