using Game.Platform.Common.Cloud;
using System;

namespace Game.Platform
{
    public interface INullPlatformService : IPlatformService, ICloudSavePlatform
    {
    }
    /// <summary>
    /// A simple no-op platform that keeps the feature graph happy when Steam cannot be initialized.
    /// </summary>
    public sealed class NullPlatformService : INullPlatformService, IDisposable
    {
        public PlatformKind Kind => PlatformKind.Null;
        public bool IsAvailable => false;
        public string UserId => string.Empty;
        public string DisplayName => string.Empty;
        public bool SupportsAchievements => false;
        public bool SupportsRichPresence => false;
        public bool SupportsCloudSave => false;
        public event System.Action OnInitialized;
        public event System.Action OnShutdown;
        bool _isShutdown;

        public bool TryInitialize()
        {
            OnInitialized?.Invoke();
            return true;
        }

        public void RunCallbacks() { }

        public void Shutdown()
        {
            if (_isShutdown) return;
            _isShutdown = true;
            OnShutdown?.Invoke();
        }

        public IPlatformService GetActivePlatformService()
        {
            return this;
        }

        public bool SetAchievement(string apiName) => false;
        public bool GetAchievement(string apiName, out bool achieved)
        {
            achieved = false;
            return false;
        }

        public bool StoreStats() => false;
        public void SetRichPresence(string key, string value) { }
        public void ClearRichPresence() { }

        public bool SaveBytes(string key, byte[] data) => false;
        public bool TryLoadBytes(string key, out byte[] data)
        {
            data = null;
            return false;
        }
        public bool Delete(string key) => false;

        public CloudSaveValidationResult ValidateKey(string key) => CloudSaveValidationResult.Unsupported;

        public CloudSaveValidationResult ValidateSave(string key, int byteLength) => CloudSaveValidationResult.Unsupported;

        public CloudSaveValidationResult ValidateBytes(string key, byte[] data) => CloudSaveValidationResult.Unsupported;

        public void Dispose()
        {
            Shutdown();
        }
    }
}
