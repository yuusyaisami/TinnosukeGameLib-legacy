#if UNITY_EDITOR
using UnityEngine;

namespace Game.Editor.Registry
{
    /// <summary>
    /// Option の Explorer / CodeGen 統合設定。
    /// </summary>
    [CreateAssetMenu(
        fileName = "OptionSettings",
        menuName = "Game/Registry/Settings/Option Settings")]
    public sealed class OptionSettings : RegistrySettingsBase
    {
        void Reset()
        {
            windowTitle = "Option Explorer";
            namespaceName = "Game.StateMachine.Generated";
            rootClassName = "Options";
            outputPath = "Assets/GameLib/Script/Generated/Options.g.cs";
        }
    }
}
#endif
