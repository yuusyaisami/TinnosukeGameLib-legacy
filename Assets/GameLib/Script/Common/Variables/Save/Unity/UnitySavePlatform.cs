#nullable enable
using System;
using System.IO;
using UnityEngine;

namespace Game.Save
{
    public sealed class UnitySavePlatform : ISavePlatform
    {
        readonly string _exportRoot;

        public UnitySavePlatform(string exportRootDirectory = "")
        {
            _exportRoot = string.IsNullOrEmpty(exportRootDirectory)
                ? Path.Combine(Application.persistentDataPath, "SaveV2", "Exports")
                : exportRootDirectory;
        }

        public SavePlatformCaps Caps => new SavePlatformCaps
        (
            isWebGL: false,
            maxBytesHint: null,
            supportsDownloadExport: false
        );

        public SavePlatformResult Flush()
        {
            // File-based store is already durable on write; keep API surface for WebGL/other platforms.
            return new SavePlatformResult(SavePlatformStatus.Success);
        }

        public SavePlatformResult ExportBytes(string exportName, byte[] bytes, string contentType)
        {
            if (bytes == null)
                return SavePlatformResult.Failed("Bytes is null.");

            var name = string.IsNullOrEmpty(exportName) ? "Export" : exportName;
            try
            {
                if (!Directory.Exists(_exportRoot))
                    Directory.CreateDirectory(_exportRoot);

                var path = Path.Combine(_exportRoot, name + ".bin");
                File.WriteAllBytes(path, bytes);
                return new SavePlatformResult(SavePlatformStatus.Success, path);
            }
            catch (Exception ex)
            {
                return SavePlatformResult.Failed(ex.Message);
            }
        }
    }
}
