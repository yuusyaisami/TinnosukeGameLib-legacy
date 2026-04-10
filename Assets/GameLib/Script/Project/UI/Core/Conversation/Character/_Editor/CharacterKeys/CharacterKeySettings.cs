#if UNITY_EDITOR
using Game.Editor.Registry;
using UnityEngine;

namespace Game.Conversation.Editor
{
    [CreateAssetMenu(
        fileName = "CharacterKeySettings",
        menuName = "Game/Conversation/Registry/Settings/Character Key Settings")]
    public sealed class CharacterKeySettings : RegistrySettingsBase
    {
        void Reset()
        {
            windowTitle = "Character Key Explorer";
            namespaceName = "Game.Conversation.Generated";
            rootClassName = "CharacterIds";
            outputPath = "Assets/GameLib/Script/Generated/CharacterIds.g.cs";
        }
    }
}
#endif
