#nullable enable

namespace Game.Kernel.Abstractions
{
    public enum RuntimePathKind
    {
        HotPath = 10,
        WarmPath = 20,
        ColdPath = 30,
        BootPath = 40,
        EditorGenerationPath = 50,
        ValidationPath = 60,
        TestOnlyPath = 70,
        LegacyMigrationPath = 90,
    }
}