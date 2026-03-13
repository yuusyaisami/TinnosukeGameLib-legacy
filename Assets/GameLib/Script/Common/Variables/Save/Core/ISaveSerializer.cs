#nullable enable
namespace Game.Save
{
    public enum SaveSerializerStatus : byte
    {
        Success = 0,
        SerializeError = 1,
        DeserializeError = 2,
        InvalidInput = 3,
    }

    public readonly struct SaveSerializeResult
    {
        public readonly SaveSerializerStatus Status;
        public readonly byte[]? Bytes;
        public readonly string Message;

        public SaveSerializeResult(SaveSerializerStatus status, byte[]? bytes, string message = "")
        {
            Status = status;
            Bytes = bytes;
            Message = message ?? string.Empty;
        }
    }

    public readonly struct SaveDeserializeResult
    {
        public readonly SaveSerializerStatus Status;
        public readonly string Message;

        public SaveDeserializeResult(SaveSerializerStatus status, string message = "")
        {
            Status = status;
            Message = message ?? string.Empty;
        }
    }

    public interface ISaveSerializer
    {
        SaveSerializeResult TrySerialize<T>(in T value);
        SaveDeserializeResult TryDeserialize<T>(byte[] bytes, out T value);
    }
}
