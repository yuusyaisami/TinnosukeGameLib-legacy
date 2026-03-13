using Game.Project;
using Game.Platform.Common.Cloud;
using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
#if STEAMWORKS_NET
using Steamworks;
#endif

namespace Game.Platform
{
    public interface ISteamPlatformService : IPlatformService, IAchievementPlatform, IRichPresencePlatform, ICloudSavePlatform
    {
        // Save API (Steam Cloud using SteamRemoteStorage)
        // Backwards-compatible Steam helpers. Proxy will map the common cloud API
        // (ICloudSavePlatform) to these handlers.
        bool SaveToCloud(string filename, byte[] bytes);
        bool LoadFromCloud(string filename, out byte[] data);
        bool DeleteCloudFile(string filename);
        bool CloudFileExists(string filename);
        string[] ListCloudFiles();

        // Convenience helpers for text saves
        bool SaveTextToCloud(string filename, string text);
        bool LoadTextFromCloud(string filename, out string text);
    }
    public class SteamPlatformService : ISteamPlatformService, IDisposable
    {
        public PlatformKind Kind => PlatformKind.Steam;

        public bool IsAvailable { get; private set; }
        public string UserId { get; private set; } = "";
        public string DisplayName { get; private set; } = "";

        public bool SupportsAchievements => true;
        public bool SupportsRichPresence => true;

#pragma warning disable CS0067 // event is intentionally left for external subscriptions (may be unused in some builds)
        public event System.Action OnInitialized;
        public event System.Action OnShutdown;
#pragma warning restore CS0067

        private bool _initialized;
        private bool _disposed;
        private bool _isShutdownSubscribed;
        private bool _isShutdown;
        private IApplicationShutdownService _shutdownService;
        private readonly SteamCloudSaveValidator _cloudValidator = new();

        [Inject]
        public void Construct(IApplicationShutdownService shutdownService)
        {
            _shutdownService = shutdownService;
        }

        public bool TryInitialize()
        {
            // すでに初期化済みならその状態を返すだけ
            if (_initialized)
                return IsAvailable;

            _initialized = true;

#if STEAMWORKS_NET
            try
            {
                if (!SteamAPI.Init())
                {
                    LogInitializationFailure("SteamAPI.Init() returned false.");
                    return false;
                }

                IsAvailable = true;
                UserId = SteamUser.GetSteamID().ToString();
                DisplayName = SteamFriends.GetPersonaName();
                Debug.Log($"Steam initialized: {DisplayName} ({UserId})");

                SubscribeToShutdownService();
                OnInitialized?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                LogInitializationFailure(ex.Message);
                return false;
            }
#else
            LogInitializationFailure("STEAMWORKS_NET is not defined for this build.");
            return false;
#endif
        }

        public IPlatformService GetActivePlatformService()
        {
            return this;
        }

        private void SubscribeToShutdownService()
        {
            if (_shutdownService == null || _isShutdownSubscribed) return;
            _shutdownService.OnShutdownRequested += HandleShutdownRequest;
            _isShutdownSubscribed = true;
        }

        private void HandleShutdownRequest(ShutdownReason reason)
        {
            Shutdown();
        }

        public void RunCallbacks()
        {
#if STEAMWORKS_NET
            if (IsAvailable)
                SteamAPI.RunCallbacks();
#endif
        }

        public void Shutdown()
        {
            if (!IsAvailable) return;
            if (_isShutdown) return;
            _isShutdown = true;

            try
            {
#if STEAMWORKS_NET
                SteamAPI.Shutdown();
                Debug.Log("SteamAPI Shutdown");
#endif
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                IsAvailable = false;
                OnShutdown?.Invoke();
            }
        }

        public bool SetAchievement(string apiName)
        {
#if STEAMWORKS_NET
            if (!IsAvailable) return false;
            return SteamUserStats.SetAchievement(apiName);
#else
            return false;
#endif
        }

        public bool GetAchievement(string apiName, out bool achieved)
        {
#if STEAMWORKS_NET
            achieved = false;
            if (!IsAvailable) return false;
            return SteamUserStats.GetAchievement(apiName, out achieved);
#else
            achieved = false;
            return false;
#endif
        }

        public bool StoreStats()
        {
#if STEAMWORKS_NET
            if (!IsAvailable) return false;
            return SteamUserStats.StoreStats();
#else
            return false;
#endif
        }

        public void SetRichPresence(string key, string value)
        {
#if STEAMWORKS_NET
            if (!IsAvailable) return;
            SteamFriends.SetRichPresence(key, value);
#endif
        }

        public void ClearRichPresence()
        {
#if STEAMWORKS_NET
            if (!IsAvailable) return;
            SteamFriends.ClearRichPresence();
#endif
        }

        // --------------------------------------------------
        // Save API (Steam Cloud using SteamRemoteStorage)
        // These are convenience helpers; not part of IPlatformService.
        // --------------------------------------------------

        public bool SaveToCloud(string filename, byte[] bytes)
        {
            var validation = ValidateBytes(filename, bytes);
            if (!validation.IsValid)
            {
                Debug.LogWarning($"Steam cloud save validation failed for '{filename}': {validation.Error} {validation.Message}");
                return false;
            }

#if STEAMWORKS_NET
            if (!IsAvailable) return false;
            return SteamRemoteStorage.FileWrite(filename, bytes, bytes.Length);
#else
            Debug.LogWarning("STEAMWORKS_NET not defined - SaveToCloud is a no-op");
            return false;
#endif
        }

        public bool LoadFromCloud(string filename, out byte[] data)
        {
            data = null;
            var validation = ValidateKey(filename);
            if (!validation.IsValid)
            {
                Debug.LogWarning($"Steam cloud load validation failed for '{filename}': {validation.Error} {validation.Message}");
                return false;
            }

#if STEAMWORKS_NET
            if (!IsAvailable) return false;
            if (!SteamRemoteStorage.FileExists(filename)) return false;
            int size = SteamRemoteStorage.GetFileSize(filename);
            if (size <= 0) return false;
            data = new byte[size];
            int read = SteamRemoteStorage.FileRead(filename, data, size);
            return read == size;
#else
            return false;
#endif
        }

        public bool DeleteCloudFile(string filename)
        {
            var validation = ValidateKey(filename);
            if (!validation.IsValid)
            {
                Debug.LogWarning($"Steam cloud delete validation failed for '{filename}': {validation.Error} {validation.Message}");
                return false;
            }

#if STEAMWORKS_NET
            if (!IsAvailable) return false;
            return SteamRemoteStorage.FileDelete(filename);
#else
            return false;
#endif
        }

        public bool CloudFileExists(string filename)
        {
            var validation = ValidateKey(filename);
            if (!validation.IsValid)
            {
                Debug.LogWarning($"Steam cloud exists validation failed for '{filename}': {validation.Error} {validation.Message}");
                return false;
            }

#if STEAMWORKS_NET
            if (!IsAvailable) return false;
            return SteamRemoteStorage.FileExists(filename);
#else
            return false;
#endif
        }

        // Map Steam cloud helpers to the standard ICloudSavePlatform API
        public bool SupportsCloudSave => true;

        public bool SaveBytes(string key, byte[] data)
        {
            return SaveToCloud(key, data);
        }

        public bool TryLoadBytes(string key, out byte[] data)
        {
            return LoadFromCloud(key, out data);
        }

        public bool Delete(string key)
        {
            return DeleteCloudFile(key);
        }

        public string[] ListCloudFiles()
        {
#if STEAMWORKS_NET
            if (!IsAvailable) return Array.Empty<string>();
            int count = SteamRemoteStorage.GetFileCount();
            var files = new List<string>(count);
            for (int i = 0; i < count; i++)
            {
                var name = SteamRemoteStorage.GetFileNameAndSize(i, out _);
                files.Add(name);
            }
            return files.ToArray();
#else
            return Array.Empty<string>();
#endif
        }

        public CloudSaveValidationResult ValidateKey(string key)
        {
            return _cloudValidator.ValidateKey(key);
        }

        public CloudSaveValidationResult ValidateSave(string key, int byteLength)
        {
            return _cloudValidator.ValidateSave(key, byteLength);
        }

        public CloudSaveValidationResult ValidateBytes(string key, byte[] data)
        {
            return _cloudValidator.ValidateBytes(key, data);
        }

        // --------------------------------------------------
        // Dispose pattern
        // --------------------------------------------------
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Shutdown();
        }


        // Small helper for text saves
        public bool SaveTextToCloud(string filename, string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            var bytes = global::System.Text.Encoding.UTF8.GetBytes(text);
            return SaveToCloud(filename, bytes);
        }

        public bool LoadTextFromCloud(string filename, out string text)
        {
            text = null;
            if (LoadFromCloud(filename, out var data))
            {
                text = global::System.Text.Encoding.UTF8.GetString(data);
                return true;
            }
            return false;
        }

        private void LogInitializationFailure(string reason)
        {
            Debug.LogWarning($"Steam initialization failed: {reason}");
            IsAvailable = false;
        }
    }
}
