#nullable enable
namespace Game.Save
{
    public sealed class NullSavePlatform : ISavePlatform
    {
        public SavePlatformCaps Caps => new SavePlatformCaps(isWebGL: false, maxBytesHint: null, supportsDownloadExport: false);

        public SavePlatformResult Flush() => SavePlatformResult.Success();
        public SavePlatformResult ExportBytes(string suggestedFileName, byte[] bytes, string mimeType) => SavePlatformResult.Success();
    }
}
