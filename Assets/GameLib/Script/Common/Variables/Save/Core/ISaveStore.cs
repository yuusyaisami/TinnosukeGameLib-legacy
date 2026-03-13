#nullable enable
namespace Game.Save
{
    public enum SaveStoreSaveStatus : byte
    {
        Success = 0,
        StorageFull = 1,
        IOError = 2,
    }

    public readonly struct SaveStoreSaveResult
    {
        public readonly SaveStoreSaveStatus Status;
        public readonly string Message;

        public SaveStoreSaveResult(SaveStoreSaveStatus status, string message = "")
        {
            Status = status;
            Message = message ?? string.Empty;
        }
    }

    public enum SaveStoreLoadStatus : byte
    {
        Success = 0,
        NotFound = 1,
        IOError = 2,
    }

    public readonly struct SaveStoreLoadResult
    {
        public readonly SaveStoreLoadStatus Status;
        public readonly byte[]? Bytes;
        public readonly string Message;

        public SaveStoreLoadResult(SaveStoreLoadStatus status, byte[]? bytes, string message = "")
        {
            Status = status;
            Bytes = bytes;
            Message = message ?? string.Empty;
        }
    }

    public enum SaveStoreDeleteAllStatus : byte
    {
        Success = 0,
        IOError = 1,
    }

    public readonly struct SaveStoreDeleteAllResult
    {
        public readonly SaveStoreDeleteAllStatus Status;
        public readonly string Message;

        public SaveStoreDeleteAllResult(SaveStoreDeleteAllStatus status, string message = "")
        {
            Status = status;
            Message = message ?? string.Empty;
        }
    }

    public interface ISaveStore
    {
        bool KeyExists(string key);
        void DeleteKey(string key);
        SaveStoreDeleteAllResult DeleteAll();

        SaveStoreSaveResult Save(string key, byte[] bytes);
        SaveStoreLoadResult Load(string key);
    }
}
