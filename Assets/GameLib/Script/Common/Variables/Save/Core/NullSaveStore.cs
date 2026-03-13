#nullable enable
namespace Game.Save
{
    public sealed class NullSaveStore : ISaveStore
    {
        public bool KeyExists(string key) => false;
        public void DeleteKey(string key) { }
        public SaveStoreDeleteAllResult DeleteAll() => new SaveStoreDeleteAllResult(SaveStoreDeleteAllStatus.Success);

        public SaveStoreSaveResult Save(string key, byte[] bytes) => new SaveStoreSaveResult(SaveStoreSaveStatus.Success);
        public SaveStoreLoadResult Load(string key) => new SaveStoreLoadResult(SaveStoreLoadStatus.NotFound, null);
    }
}
