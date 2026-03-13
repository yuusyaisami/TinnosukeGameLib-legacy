#if UNITY_EDITOR
using UnityEngine;

namespace Game.Editor.Registry
{
    /// <summary>
    /// VarKey の Explorer / CodeGen 統合設定。
    /// </summary>
    [CreateAssetMenu(
        fileName = "VarKeySettings",
        menuName = "Game/Registry/Settings/Var Key Settings")]
    public sealed class VarKeySettings : RegistrySettingsBase
    {
        void Reset()
        {
            windowTitle = "Var Key Explorer";
            namespaceName = "Game.Vars.Generated";
            rootClassName = "VarIds";
            outputPath = "Assets/GameLib/Script/Generated/VarIds.g.cs";
        }
    }
}
#endif

