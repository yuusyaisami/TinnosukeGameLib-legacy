using System.IO;
using Game.Platform.Common.Cloud;

namespace Game.Platform
{
    internal sealed class SteamCloudSaveValidator
    {
        public const int MaxFileNameLength = 260; // SteamRemoteStorage::k_cchMaxCloudStorageFilename
        public const int MaxFileSizeBytes = 100 * 1024 * 1024; // Steam recommends 100 MiB per file

        private static readonly char[] s_invalidFileNameChars = Path.GetInvalidFileNameChars();

        public CloudSaveValidationResult ValidateKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return CloudSaveValidationResult.FromError(CloudSaveValidationError.KeyEmpty, "Cloud save key cannot be empty.");
            }

            if (key.Length > MaxFileNameLength)
            {
                return CloudSaveValidationResult.FromError(
                    CloudSaveValidationError.KeyTooLong,
                    $"Cloud save key exceeds Steam's limit of {MaxFileNameLength} characters.",
                    MaxFileNameLength);
            }

            if (key.Contains("/") || key.Contains("\\"))
            {
                return CloudSaveValidationResult.FromError(
                    CloudSaveValidationError.ContainsSeparator,
                    "Cloud save key cannot contain path separator characters.");
            }

            if (key.IndexOfAny(s_invalidFileNameChars) >= 0)
            {
                return CloudSaveValidationResult.FromError(
                    CloudSaveValidationError.InvalidCharacters,
                    "Cloud save key contains characters that SteamRemoteStorage rejects.");
            }

            return CloudSaveValidationResult.Success;
        }

        public CloudSaveValidationResult ValidateSave(string key, int byteLength)
        {
            var keyValidation = ValidateKey(key);
            if (!keyValidation.IsValid)
            {
                return keyValidation;
            }

            if (byteLength < 0)
            {
                return CloudSaveValidationResult.FromError(
                    CloudSaveValidationError.SizeTooLarge,
                    "Cloud save size cannot be negative.");
            }

            if (byteLength > MaxFileSizeBytes)
            {
                return CloudSaveValidationResult.FromError(
                    CloudSaveValidationError.SizeTooLarge,
                    $"Cloud save exceeds Steam's {MaxFileSizeBytes / (1024 * 1024)} MiB limit.",
                    MaxFileSizeBytes);
            }

            return CloudSaveValidationResult.Success;
        }

        public CloudSaveValidationResult ValidateBytes(string key, byte[] data)
        {
            if (data == null)
            {
                return CloudSaveValidationResult.FromError(
                    CloudSaveValidationError.DataMissing,
                    "Cloud save data cannot be null.");
            }

            return ValidateSave(key, data.Length);
        }
    }
}
