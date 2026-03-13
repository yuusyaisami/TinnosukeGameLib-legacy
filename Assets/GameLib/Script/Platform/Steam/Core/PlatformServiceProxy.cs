using System;
using Game.Project;
using Game.Platform.Common.Cloud;
using UnityEngine;

namespace Game.Platform
{
    /// <summary>
    /// Bridges all <see cref="IPlatformService"/> calls to Steam when it works or to the null platform when it does not.
    /// </summary>
    public sealed class PlatformServiceProxy :
    IPlatformService, IAchievementPlatform,
    IRichPresencePlatform,
    ICloudSavePlatform,
    IDisposable
    {
        private readonly ISteamPlatformService _steamService;
        private readonly INullPlatformService _nullPlatformService;
        private readonly IPlatformOptions _options;
        private readonly IApplicationShutdownService _shutdownService;
        private IPlatformService _activeService;
        private bool _disposed;
        bool _isShutdown;

        private IAchievementPlatform AchievementImpl => _activeService as IAchievementPlatform ?? _nullPlatformService as IAchievementPlatform;

        private IRichPresencePlatform RichImpl => _activeService as IRichPresencePlatform ?? _nullPlatformService as IRichPresencePlatform;

        private ICloudSavePlatform CloudImpl => _activeService as ICloudSavePlatform ?? _nullPlatformService as ICloudSavePlatform;

        public PlatformServiceProxy(
            ISteamPlatformService steamService,
            INullPlatformService nullPlatformService,
            IPlatformOptions options,
            IApplicationShutdownService shutdownService)
        {
            _steamService = steamService ?? throw new ArgumentNullException(nameof(steamService));
            _nullPlatformService = nullPlatformService ?? throw new ArgumentNullException(nameof(nullPlatformService));
            _options = options;
            _shutdownService = shutdownService;

            ActivateBestPlatform();
        }

        public PlatformKind Kind => _activeService?.Kind ?? PlatformKind.Null;
        public bool IsAvailable => _activeService?.IsAvailable ?? false;
        public string UserId => _activeService?.UserId ?? string.Empty;
        public string DisplayName => _activeService?.DisplayName ?? string.Empty;
        public bool SupportsAchievements => AchievementImpl?.SupportsAchievements ?? false;
        public bool SupportsRichPresence => RichImpl?.SupportsRichPresence ?? false;
        public bool SupportsCloudSave => CloudImpl?.SupportsCloudSave ?? false;

        public bool TryInitialize()
        {
            // すでにコンストラクタでプラットフォーム選択済み
            // → 現在の状態をそのまま返す
            return _activeService?.IsAvailable ?? false;
        }


        public void RunCallbacks()
        {
            _activeService?.RunCallbacks();
        }

        public void Shutdown()
        {
            if (_isShutdown) return;
            _isShutdown = true;
            _activeService?.Shutdown();
        }
        public IPlatformService GetActivePlatformService()
        {
            return _activeService;
        }

        public bool SetAchievement(string apiName) => AchievementImpl?.SetAchievement(apiName) ?? false;

        public bool GetAchievement(string apiName, out bool achieved) => AchievementImpl?.GetAchievement(apiName, out achieved) ?? (achieved = false, false).Item2;

        public bool StoreStats() => AchievementImpl?.StoreStats() ?? false;

        public void SetRichPresence(string key, string value) => RichImpl?.SetRichPresence(key, value);

        public void ClearRichPresence() => RichImpl?.ClearRichPresence();

        // ICloudSavePlatform
        public bool SaveBytes(string key, byte[] data) => CloudImpl?.SaveBytes(key, data) ?? false;

        public bool TryLoadBytes(string key, out byte[] data) => CloudImpl?.TryLoadBytes(key, out data) ?? (data = null, false).Item2;

        public bool Delete(string key) => CloudImpl?.Delete(key) ?? false;

        public CloudSaveValidationResult ValidateKey(string key) => CloudImpl?.ValidateKey(key) ?? CloudSaveValidationResult.Unsupported;

        public CloudSaveValidationResult ValidateSave(string key, int byteLength) => CloudImpl?.ValidateSave(key, byteLength) ?? CloudSaveValidationResult.Unsupported;

        public CloudSaveValidationResult ValidateBytes(string key, byte[] data) => CloudImpl?.ValidateBytes(key, data) ?? CloudSaveValidationResult.Unsupported;

        private void ActivateBestPlatform()
        {
            bool isAvailablePlatform = false;

            switch (_options.PreferredPlatform)
            {
                case PlatformKind.Steam:
                    isAvailablePlatform = _steamService.TryInitialize();
                    if (isAvailablePlatform) _activeService = _steamService;
                    break;
                case PlatformKind.Null:
                    isAvailablePlatform = _nullPlatformService.TryInitialize();
                    if (isAvailablePlatform) _activeService = _nullPlatformService;
                    break;
            }



            if (!isAvailablePlatform)
            {
                // PreferredPlatformで指定したプラットフォームが利用できなかった場合のフォールバック処理
                if (_options.ExitIfPlatformUnavailable)
                {
                    TryRequestShutdown();
                }
                _activeService = _nullPlatformService;
                _nullPlatformService.TryInitialize();
            }
        }

        private void TryRequestShutdown()
        {
            if (_shutdownService == null)
                return;

            var exitApplication = _options.ExitIfPlatformUnavailable;
            var reason = _options.PreferredPlatform switch
            {
                PlatformKind.Steam => ShutdownReason.SteamUnavailable,
                PlatformKind.Null => ShutdownReason.PreferredPlatformUnavailable,
                _ => ShutdownReason.PreferredPlatformUnavailable,
            };

            _shutdownService.RequestShutdown(reason, exitApplication);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            if (_activeService != null)
            {
                _activeService.Dispose();
            }

            _disposed = true;
        }

    }
}
