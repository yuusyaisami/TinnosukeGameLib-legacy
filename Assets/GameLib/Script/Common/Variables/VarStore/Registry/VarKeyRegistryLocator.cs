#nullable enable
using System;
using System.IO;
using Game.VarStoreKeys;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

/// <summary>VarKeyRegistry を取得／生成するヘルパー。</summary>
public static class VarKeyRegistryLocator
{
    // Runtime でも Resources.Load で取得できるように、デフォルトは Resources 配下に置く。
    // （資産化済みデータは varId を保持する想定だが、段階移行中は stableKey 解決が残るため）
    public const string DefaultRegistryPath = "Assets/Resources/VarKeyRegistry.asset";

    // Conservative runtime caching controls to avoid repeated expensive Resources.Load
    // while still preventing stale empty fallbacks from persisting across play sessions.
    // _cachedRegistry is used at runtime when populated (>0 keys). If it's empty/null,
    // we attempt a reload no more often than _reloadThrottleSeconds.
    static VarKeyRegistry? _cachedRegistry;
#pragma warning disable CS0414
    static float _lastReloadAttemptTime = -Mathf.Infinity;
#pragma warning restore CS0414
    const float _reloadThrottleSeconds = 1.5f;

    public static VarKeyRegistry GetOrCreate()
    {
#if UNITY_EDITOR
        if (_cachedRegistry != null)
            return _cachedRegistry;

        _cachedRegistry = AssetDatabase.LoadAssetAtPath<VarKeyRegistry>(DefaultRegistryPath);
        if (_cachedRegistry != null)
            return _cachedRegistry;

        var guids = AssetDatabase.FindAssets("t:VarKeyRegistry");
        if (guids.Length > 0)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            _cachedRegistry = AssetDatabase.LoadAssetAtPath<VarKeyRegistry>(path);
            if (_cachedRegistry != null)
                return _cachedRegistry;
        }

        // If not found, create one at the default path so users can start from a seeded asset.
        _cachedRegistry = ScriptableObject.CreateInstance<VarKeyRegistry>();
        EnsureDirectoryExists(DefaultRegistryPath);
        AssetDatabase.CreateAsset(_cachedRegistry, DefaultRegistryPath);
        AssetDatabase.SaveAssets();
        return _cachedRegistry;
#else
        // Runtime behavior: use conservative cached instance to avoid repeated Resources.Load
        // calls on hot paths. If cache is populated, return it immediately.
        if (_cachedRegistry != null && _cachedRegistry.RegisteredKeyCount > 0)
            return _cachedRegistry;

        // Throttle reload attempts to avoid hammering Resources.Load repeatedly.
        var now = Time.realtimeSinceStartup;
        if (now - _lastReloadAttemptTime > _reloadThrottleSeconds)
        {
            _lastReloadAttemptTime = now;
            var loaded = Resources.Load<VarKeyRegistry>("VarKeyRegistry");
            if (loaded != null && loaded.RegisteredKeyCount > 0)
            {
                _cachedRegistry = loaded;
                return _cachedRegistry;
            }
        }

        // If we already have a cached instance (even if empty), return it to keep calls cheap.
        if (_cachedRegistry != null)
            return _cachedRegistry;

        // As a fallback, do a one-time immediate load (no throttle) before creating a runtime
        // fallback instance and caching it.
        var res = Resources.Load<VarKeyRegistry>("VarKeyRegistry");
        if (res != null && res.RegisteredKeyCount > 0)
        {
            _cachedRegistry = res;
            return _cachedRegistry;
        }

        // No asset found/populated: create and cache a runtime fallback so subsequent calls are cheap.
        _cachedRegistry = ScriptableObject.CreateInstance<VarKeyRegistry>();
        return _cachedRegistry;
#endif
    }

#if UNITY_EDITOR
    static void EnsureDirectoryExists(string assetPath)
    {
        var dir = Path.GetDirectoryName(assetPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }
#endif
}
