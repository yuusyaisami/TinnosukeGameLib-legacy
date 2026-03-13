// Assets/Game/Script/Platform/IPlatform.cs
using Game.Platform.Common.Cloud;
using System;

namespace Game.Platform
{
    public interface IAchievementPlatform
    {
        bool SupportsAchievements { get; }
        bool SetAchievement(string apiName);
        bool GetAchievement(string apiName, out bool achieved);
        bool StoreStats();
    }

    public interface IRichPresencePlatform
    {
        bool SupportsRichPresence { get; }
        void SetRichPresence(string key, string value);
        void ClearRichPresence();
    }
    public interface ICloudSavePlatform
    {
        bool SupportsCloudSave { get; }
        bool SaveBytes(string key, byte[] data);
        bool TryLoadBytes(string key, out byte[] data);
        bool Delete(string key);
        CloudSaveValidationResult ValidateKey(string key);
        CloudSaveValidationResult ValidateSave(string key, int byteLength);
        CloudSaveValidationResult ValidateBytes(string key, byte[] data);
    }


    public interface IPlatformService : IDisposable
    {
        PlatformKind Kind { get; }     // どのプラットフォームか
        bool IsAvailable { get; }      // そのプラットフォーム固有APIが有効か（Steam API 等）
        string UserId { get; }         // 安定なユーザーID（SteamID64文字列 or 端末安定ID）
        string DisplayName { get; }    // 表示名（PersonaName 等）

        bool SupportsAchievements { get; }
        bool SupportsRichPresence { get; }

        bool TryInitialize();
        void RunCallbacks();
        void Shutdown();

        // 他のサービスのためのインターフェース
        IPlatformService GetActivePlatformService();
    }
}
