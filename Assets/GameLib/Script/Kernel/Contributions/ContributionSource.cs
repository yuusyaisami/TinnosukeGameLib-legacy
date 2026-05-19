#nullable enable

namespace Game.Kernel.Contributions
{
    public enum ContributionSource
    {
        Unknown = 0,
        SceneObject = 10,
        PrefabAsset = 20,
        PrefabInstance = 30,
        PrefabVariant = 40,
        ScriptableObjectAsset = 50,
        GeneratedAsset = 60,
        CodeDefinedModule = 70,
        LegacyBridge = 80,
    }
}