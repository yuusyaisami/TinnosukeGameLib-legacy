#nullable enable
namespace Game.Save
{
    public sealed class DefaultSaveBackupPolicy : ISaveBackupPolicy
    {
        public bool ShouldBackup(in SaveContext ctx) => true;

        public SaveBackupMode GetBackupMode(in SaveContext ctx)
        {
            // Default: keep a slotted backup on disk.
            return SaveBackupMode.Slotted;
        }

        public int GetMaxSlots(in SaveContext ctx) => 3;
    }
}
