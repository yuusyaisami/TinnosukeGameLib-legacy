using System.IO;
using Game.VariableKeys;
#if UNITY_EDITOR
using Game.Editor.Registry;
using UnityEditor;
#endif
using UnityEngine;

/// <summary>VariableKeyRegistry / VariableKeySettings を取得／生成するヘルパー。</summary>
public static class VariableKeyRegistryLocator
{
    public const string DefaultRegistryPath = "Assets/GameLib/SO/Variable/VariableKeyRegistry.asset";
    public const string DefaultSettingsPath = "Assets/GameLib/SO/Variable/VariableKeySettings.asset";

    static VariableKeyRegistry _cachedRegistry;
#if UNITY_EDITOR
    static VariableKeySettings _cachedSettings;
#endif

    public static VariableKeyRegistry GetOrCreate()
    {
#if UNITY_EDITOR
        if (_cachedRegistry != null)
            return _cachedRegistry;

        // 既存を検索（複数ある場合は先頭を採用）
        var guids = AssetDatabase.FindAssets("t:VariableKeyRegistry");
        if (guids.Length > 0)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            _cachedRegistry = AssetDatabase.LoadAssetAtPath<VariableKeyRegistry>(path);
        }

        // なければ作成
        if (_cachedRegistry == null)
        {
            _cachedRegistry = ScriptableObject.CreateInstance<VariableKeyRegistry>();
            EnsureDirectoryExists(DefaultRegistryPath);
            AssetDatabase.CreateAsset(_cachedRegistry, DefaultRegistryPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[VariableKeyRegistryLocator] Created: {DefaultRegistryPath}");
        }

        return _cachedRegistry;
#else
        // ランタイムでは Resources などからのロードを想定
        if (_cachedRegistry == null)
        {
            _cachedRegistry = Resources.Load<VariableKeyRegistry>("VariableKeyRegistry");
            if (_cachedRegistry == null)
            {
                Debug.LogError("[VariableKeyRegistryLocator] Could not load VariableKeyRegistry at runtime.");
            }
        }
        return _cachedRegistry;
#endif
    }

#if UNITY_EDITOR
    public static VariableKeySettings GetOrCreateSettings()
    {
        if (_cachedSettings != null)
            return _cachedSettings;

        var guids = AssetDatabase.FindAssets("t:VariableKeySettings");
        if (guids.Length > 0)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            _cachedSettings = AssetDatabase.LoadAssetAtPath<VariableKeySettings>(path);
        }

        if (_cachedSettings == null)
        {
            _cachedSettings = ScriptableObject.CreateInstance<VariableKeySettings>();
            EnsureDirectoryExists(DefaultSettingsPath);
            AssetDatabase.CreateAsset(_cachedSettings, DefaultSettingsPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[VariableKeyRegistryLocator] Created Settings: {DefaultSettingsPath}");
        }

        return _cachedSettings;
    }

    static void EnsureDirectoryExists(string assetPath)
    {
        var dir = Path.GetDirectoryName(assetPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }
#endif
}
