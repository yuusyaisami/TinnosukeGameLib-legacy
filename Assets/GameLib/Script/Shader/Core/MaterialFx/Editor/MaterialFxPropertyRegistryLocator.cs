#if UNITY_EDITOR
using System.IO;
using Game.Editor.Registry;
using UnityEditor;
using UnityEngine;

namespace Game.MaterialFx.Editor
{
    /// <summary>
    /// MaterialFxPropertyRegistrySO / MaterialFxSettings を自動検索/生成するユーティリティ。
    /// </summary>
    public static class MaterialFxPropertyRegistryLocator
    {
        public const string DefaultRegistryPath = "Assets/GameLib/SO/MaterialFx/MaterialFxPropertyRegistry.asset";
        public const string DefaultSettingsPath = "Assets/GameLib/SO/MaterialFx/MaterialFxSettings.asset";

        static MaterialFxPropertyRegistrySO _cachedRegistry;
        static MaterialFxSettings _cachedSettings;

        public static MaterialFxPropertyRegistrySO GetOrCreate()
        {
            if (_cachedRegistry != null) return _cachedRegistry;

            var guids = AssetDatabase.FindAssets("t:MaterialFxPropertyRegistrySO");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                _cachedRegistry = AssetDatabase.LoadAssetAtPath<MaterialFxPropertyRegistrySO>(path);
            }

            if (_cachedRegistry == null)
            {
                _cachedRegistry = ScriptableObject.CreateInstance<MaterialFxPropertyRegistrySO>();
                EnsureDirectoryExists(DefaultRegistryPath);
                AssetDatabase.CreateAsset(_cachedRegistry, DefaultRegistryPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[MaterialFxPropertyRegistryLocator] Created: {DefaultRegistryPath}");
            }

            return _cachedRegistry;
        }

        public static MaterialFxSettings GetOrCreateSettings()
        {
            if (_cachedSettings != null) return _cachedSettings;

            var guids = AssetDatabase.FindAssets("t:MaterialFxSettings");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                _cachedSettings = AssetDatabase.LoadAssetAtPath<MaterialFxSettings>(path);
            }

            if (_cachedSettings == null)
            {
                _cachedSettings = ScriptableObject.CreateInstance<MaterialFxSettings>();
                EnsureDirectoryExists(DefaultSettingsPath);
                AssetDatabase.CreateAsset(_cachedSettings, DefaultSettingsPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[MaterialFxPropertyRegistryLocator] Created Settings: {DefaultSettingsPath}");
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
