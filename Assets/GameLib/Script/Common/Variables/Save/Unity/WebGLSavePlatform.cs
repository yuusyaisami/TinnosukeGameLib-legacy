#nullable enable
using System;
using UnityEngine;

namespace Game.Save
{
    /// <summary>
    /// WebGL向けのプラットフォーム実装。
    /// FlushでPlayerPrefs.Save()を呼び、永続化を促す。
    /// </summary>
    public sealed class WebGLSavePlatform : ISavePlatform
    {
        public SavePlatformCaps Caps => new SavePlatformCaps(
            isWebGL: true,
            maxBytesHint: null,
            supportsDownloadExport: false
        );

        public SavePlatformResult Flush()
        {
            try
            {
                PlayerPrefs.Save();
                return new SavePlatformResult(SavePlatformStatus.Success);
            }
            catch (Exception ex)
            {
                return SavePlatformResult.Failed(ex.Message);
            }
        }

        public SavePlatformResult ExportBytes(string exportName, byte[] bytes, string contentType)
        {
            return SavePlatformResult.Failed("Export is not supported on WebGL in this build.");
        }
    }
}
