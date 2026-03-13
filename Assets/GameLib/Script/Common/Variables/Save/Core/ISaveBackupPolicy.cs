#nullable enable
namespace Game.Save
{
    public enum SaveBackupMode : byte
    {
        Slotted = 0,
        Export = 1,
        None = 2,
    }

    public interface ISaveBackupPolicy
    {
        bool ShouldBackup(in SaveContext ctx);
        SaveBackupMode GetBackupMode(in SaveContext ctx);
        int GetMaxSlots(in SaveContext ctx);
    }
}
