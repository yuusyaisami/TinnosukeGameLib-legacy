namespace Game.Platform.Common.Cloud
{
    public enum CloudSaveValidationError
    {
        None,
        KeyEmpty,
        KeyTooLong,
        ContainsSeparator,
        InvalidCharacters,
        SizeTooLarge,
        DataMissing,
        Unsupported,
        PlatformUnavailable
    }

    public readonly struct CloudSaveValidationResult
    {
        public static CloudSaveValidationResult Success => new(true, CloudSaveValidationError.None, string.Empty);
        public static CloudSaveValidationResult Unsupported => new(false, CloudSaveValidationError.Unsupported, "Cloud saves are not supported for the active platform.");
        public static CloudSaveValidationResult PlatformUnavailable => new(false, CloudSaveValidationError.PlatformUnavailable, "The platform is not available to handle cloud saves at this time.");

        public bool IsValid { get; }
        public CloudSaveValidationError Error { get; }
        public string Message { get; }
        public int LimitBytes { get; }

        public CloudSaveValidationResult(bool isValid, CloudSaveValidationError error, string message, int limitBytes = 0)
        {
            IsValid = isValid;
            Error = error;
            Message = message ?? string.Empty;
            LimitBytes = limitBytes;
        }

        public static CloudSaveValidationResult FromError(CloudSaveValidationError error, string message, int limitBytes = 0)
        {
            return new CloudSaveValidationResult(false, error, message, limitBytes);
        }
    }
}
