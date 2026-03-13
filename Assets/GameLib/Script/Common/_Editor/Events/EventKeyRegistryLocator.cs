#if UNITY_EDITOR
using System.IO;
using Game.Editor.Registry;
using UnityEditor;
using UnityEngine;

namespace Game.EventKey
{
    /// <summary>EventKeyRegistry / EventKeySettings を取得／生成するヘルパー。</summary>
    public static class EventKeyRegistryLocator
    {
        public const string DefaultRegistryPath = "Assets/GameLib/SO/Event/EventKeyRegistry.asset";
        public const string DefaultSettingsPath = "Assets/GameLib/SO/Event/EventKeySettings.asset";

        static EventKeyRegistry _cachedRegistry;
        static EventKeySettings _cachedSettings;

        public static EventKeyRegistry GetOrCreate()
        {
            if (_cachedRegistry != null)
                return _cachedRegistry;

            var guids = AssetDatabase.FindAssets("t:EventKeyRegistry");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                _cachedRegistry = AssetDatabase.LoadAssetAtPath<EventKeyRegistry>(path);
            }

            if (_cachedRegistry == null)
            {
                _cachedRegistry = ScriptableObject.CreateInstance<EventKeyRegistry>();
                EnsureDirectoryExists(DefaultRegistryPath);
                AssetDatabase.CreateAsset(_cachedRegistry, DefaultRegistryPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[EventKeyRegistryLocator] Created: {DefaultRegistryPath}");
            }

            return _cachedRegistry;
        }

        public static EventKeySettings GetOrCreateSettings()
        {
            if (_cachedSettings != null)
                return _cachedSettings;

            var guids = AssetDatabase.FindAssets("t:EventKeySettings");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                _cachedSettings = AssetDatabase.LoadAssetAtPath<EventKeySettings>(path);
            }

            if (_cachedSettings == null)
            {
                _cachedSettings = ScriptableObject.CreateInstance<EventKeySettings>();
                EnsureDirectoryExists(DefaultSettingsPath);
                AssetDatabase.CreateAsset(_cachedSettings, DefaultSettingsPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[EventKeyRegistryLocator] Created Settings: {DefaultSettingsPath}");
            }

            return _cachedSettings;
        }

        static void EnsureDirectoryExists(string assetPath)
        {
            var dir = Path.GetDirectoryName(assetPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
    }
}
#endif
