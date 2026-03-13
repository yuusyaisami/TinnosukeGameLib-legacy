#if UNITY_EDITOR
using UnityEngine;

namespace Game.Editor.Registry
{
    /// <summary>
    /// VariableKey の Explorer / CodeGen 統合設定。
    /// </summary>
    [CreateAssetMenu(
        fileName = "VariableKeySettings",
        menuName = "Game/Registry/Settings/Variable Key Settings")]
    public sealed class VariableKeySettings : RegistrySettingsBase
    {
        void Reset()
        {
            windowTitle = "Variable Key Explorer";
            namespaceName = "Game.Variables.Generated";
            rootClassName = "VariableKeys";
            outputPath = "Assets/GameLib/Script/Generated/VariableKeys.g.cs";
        }
    }
}
#endif
