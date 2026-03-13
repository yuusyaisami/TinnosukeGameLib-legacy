#nullable enable
namespace Game.Save
{
    public enum SavePlatformStatus : byte
    {
        Success = 0,
        Failed = 1,
    }

    public readonly struct SavePlatformResult
    {
        public readonly SavePlatformStatus Status;
        public readonly string Message;

        public SavePlatformResult(SavePlatformStatus status, string message = "")
        {
            Status = status;
            Message = message ?? string.Empty;
        }

        public static SavePlatformResult Success() => new SavePlatformResult(SavePlatformStatus.Success);
        public static SavePlatformResult Failed(string message) => new SavePlatformResult(SavePlatformStatus.Failed, message);
    }

    public readonly struct SavePlatformCaps
    {
        public readonly bool IsWebGL;
        public readonly int? MaxBytesHint;
        public readonly bool SupportsDownloadExport;

        public SavePlatformCaps(bool isWebGL, int? maxBytesHint, bool supportsDownloadExport)
        {
            IsWebGL = isWebGL;
            MaxBytesHint = maxBytesHint;
            SupportsDownloadExport = supportsDownloadExport;
        }
    }

    public interface ISavePlatform
    {
        SavePlatformCaps Caps { get; }

        SavePlatformResult Flush();
        SavePlatformResult ExportBytes(string suggestedFileName, byte[] bytes, string mimeType);
    }
}
