#if UNITY_EDITOR
using UnityEngine;

namespace Game.Editor.Registry
{
    /// <summary>
    /// ScalarKey の Explorer / CodeGen 統合設定。
    /// </summary>
    [CreateAssetMenu(
        fileName = "ScalarKeySettings",
        menuName = "Game/Registry/Settings/Scalar Key Settings")]
    public sealed class ScalarKeySettings : RegistrySettingsBase
    {
        void Reset()
        {
            windowTitle = "Scalar Key Explorer";
            namespaceName = "Game.Scalar.Generated";
            rootClassName = "ScalarKeys";
            outputPath = "Assets/GameLib/Script/Generated/ScalarKeys.g.cs";
        }
    }
}
#endif
