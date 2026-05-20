#nullable enable
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Game.Commands.VNext
{
    public static class CommandCatalogLocator
    {
        public const string DefaultCatalogPath = "Assets/Resources/CommandCatalog.asset";

        static CommandCatalogSO? _cachedCatalog;

        public static CommandCatalogSO? GetOrCreate()
        {
#if UNITY_EDITOR
            if (_cachedCatalog != null)
                return _cachedCatalog;

            var guids = AssetDatabase.FindAssets("t:CommandCatalogSO");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                _cachedCatalog = AssetDatabase.LoadAssetAtPath<CommandCatalogSO>(path);
            }

            if (_cachedCatalog == null)
            {
                _cachedCatalog = ScriptableObject.CreateInstance<CommandCatalogSO>();
                EnsureDirectoryExists(DefaultCatalogPath);
                AssetDatabase.CreateAsset(_cachedCatalog, DefaultCatalogPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[CommandCatalogLocator] Created: {DefaultCatalogPath}");
            }

            return _cachedCatalog;
#else
            if (_cachedCatalog == null)
            {
                _cachedCatalog = Resources.Load<CommandCatalogSO>("CommandCatalog");
                if (_cachedCatalog == null)
                    Debug.LogError("[CommandCatalogLocator] Could not load CommandCatalog at runtime (Resources/CommandCatalog).");
            }
            return _cachedCatalog;
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
}
