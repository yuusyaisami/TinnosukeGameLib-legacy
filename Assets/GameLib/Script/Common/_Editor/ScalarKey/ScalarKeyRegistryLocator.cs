#if UNITY_EDITOR
using System.IO;
using Game.Editor.Registry;
using UnityEditor;
using UnityEngine;

namespace Game.Scalar.Editor
{
    /// <summary>
    /// ScalarKeyRegistry / ScalarKeySettings を自動検索/生成するユーティリティ。
    /// </summary>
    public static class ScalarKeyRegistryLocator
    {
        public const string DefaultRegistryPath = "Assets/GameLib/SO/Scalar/ScalarKeyRegistry.asset";
        public const string DefaultSettingsPath = "Assets/GameLib/SO/Scalar/ScalarKeySettings.asset";

        static ScalarKeyRegistry _cachedRegistry;
        static ScalarKeySettings _cachedSettings;

        public static ScalarKeyRegistry GetOrCreate()
        {
            if (_cachedRegistry != null) return _cachedRegistry;

            var guids = AssetDatabase.FindAssets("t:ScalarKeyRegistry");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                _cachedRegistry = AssetDatabase.LoadAssetAtPath<ScalarKeyRegistry>(path);
            }

            if (_cachedRegistry == null)
            {
                _cachedRegistry = ScriptableObject.CreateInstance<ScalarKeyRegistry>();
                EnsureDirectoryExists(DefaultRegistryPath);
                AssetDatabase.CreateAsset(_cachedRegistry, DefaultRegistryPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[ScalarKeyRegistryLocator] Created: {DefaultRegistryPath}");
            }

            return _cachedRegistry;
        }

        public static ScalarKeySettings GetOrCreateSettings()
        {
            if (_cachedSettings != null) return _cachedSettings;

            var guids = AssetDatabase.FindAssets("t:ScalarKeySettings");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                _cachedSettings = AssetDatabase.LoadAssetAtPath<ScalarKeySettings>(path);
            }

            if (_cachedSettings == null)
            {
                _cachedSettings = ScriptableObject.CreateInstance<ScalarKeySettings>();
                EnsureDirectoryExists(DefaultSettingsPath);
                AssetDatabase.CreateAsset(_cachedSettings, DefaultSettingsPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[ScalarKeyRegistryLocator] Created Settings: {DefaultSettingsPath}");
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
