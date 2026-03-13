#if UNITY_EDITOR
using System.IO;
using Game.Editor.Registry;
using UnityEditor;
using UnityEngine;

namespace Game.StateMachine.Editor
{
    /// <summary>
    /// StateKeyRegistry / StateKeySettings を取得／生成するヘルパー。
    /// </summary>
    public static class StateKeyRegistryLocator
    {
        public const string DefaultRegistryPath = "Assets/GameLib/SO/StateMachine/StateKeyRegistry.asset";
        public const string DefaultSettingsPath = "Assets/GameLib/SO/StateMachine/StateKeySettings.asset";
        
        static StateKeyRegistry _cachedRegistry;
        static StateKeySettings _cachedSettings;
        
        public static StateKeyRegistry GetOrCreate()
        {
            if (_cachedRegistry != null)
                return _cachedRegistry;
            
            var guids = AssetDatabase.FindAssets("t:StateKeyRegistry");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                _cachedRegistry = AssetDatabase.LoadAssetAtPath<StateKeyRegistry>(path);
            }
            
            if (_cachedRegistry == null)
            {
                _cachedRegistry = ScriptableObject.CreateInstance<StateKeyRegistry>();
                EnsureDirectoryExists(DefaultRegistryPath);
                AssetDatabase.CreateAsset(_cachedRegistry, DefaultRegistryPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[StateKeyRegistryLocator] Created: {DefaultRegistryPath}");
            }
            
            return _cachedRegistry;
        }
        
        public static StateKeySettings GetOrCreateSettings()
        {
            if (_cachedSettings != null)
                return _cachedSettings;
            
            var guids = AssetDatabase.FindAssets("t:StateKeySettings");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                _cachedSettings = AssetDatabase.LoadAssetAtPath<StateKeySettings>(path);
            }
            
            if (_cachedSettings == null)
            {
                _cachedSettings = ScriptableObject.CreateInstance<StateKeySettings>();
                EnsureDirectoryExists(DefaultSettingsPath);
                AssetDatabase.CreateAsset(_cachedSettings, DefaultSettingsPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[StateKeyRegistryLocator] Created Settings: {DefaultSettingsPath}");
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
