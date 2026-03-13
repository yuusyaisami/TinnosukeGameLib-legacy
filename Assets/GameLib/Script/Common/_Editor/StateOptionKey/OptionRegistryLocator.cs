#if UNITY_EDITOR
using System.IO;
using Game.Editor.Registry;
using UnityEditor;
using UnityEngine;

namespace Game.StateMachine.Editor
{
    /// <summary>
    /// OptionRegistry / OptionSettings を取得／生成するヘルパー。
    /// </summary>
    public static class OptionRegistryLocator
    {
        public const string DefaultRegistryPath = "Assets/GameLib/SO/StateMachine/OptionRegistry.asset";
        public const string DefaultSettingsPath = "Assets/GameLib/SO/StateMachine/OptionSettings.asset";
        
        static OptionRegistry _cachedRegistry;
        static OptionSettings _cachedSettings;
        
        public static OptionRegistry GetOrCreate()
        {
            if (_cachedRegistry != null)
                return _cachedRegistry;
            
            var guids = AssetDatabase.FindAssets("t:OptionRegistry");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                _cachedRegistry = AssetDatabase.LoadAssetAtPath<OptionRegistry>(path);
            }
            
            if (_cachedRegistry == null)
            {
                _cachedRegistry = ScriptableObject.CreateInstance<OptionRegistry>();
                EnsureDirectoryExists(DefaultRegistryPath);
                AssetDatabase.CreateAsset(_cachedRegistry, DefaultRegistryPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[OptionRegistryLocator] Created: {DefaultRegistryPath}");
            }
            
            return _cachedRegistry;
        }
        
        public static OptionSettings GetOrCreateSettings()
        {
            if (_cachedSettings != null)
                return _cachedSettings;
            
            var guids = AssetDatabase.FindAssets("t:OptionSettings");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                _cachedSettings = AssetDatabase.LoadAssetAtPath<OptionSettings>(path);
            }
            
            if (_cachedSettings == null)
            {
                _cachedSettings = ScriptableObject.CreateInstance<OptionSettings>();
                EnsureDirectoryExists(DefaultSettingsPath);
                AssetDatabase.CreateAsset(_cachedSettings, DefaultSettingsPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[OptionRegistryLocator] Created Settings: {DefaultSettingsPath}");
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
