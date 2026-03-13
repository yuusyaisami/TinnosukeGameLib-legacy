#nullable enable
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Game.Commands.VNext
{
    public static class CommandKeyRegistryLocator
    {
        public const string DefaultRegistryPath = "Assets/Resources/CommandKeyRegistry.asset";

        static CommandKeyRegistry? _cachedRegistry;

        public static CommandKeyRegistry GetOrCreate()
        {
#if UNITY_EDITOR
            if (_cachedRegistry != null)
                return _cachedRegistry;

            var guids = AssetDatabase.FindAssets("t:CommandKeyRegistry");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                _cachedRegistry = AssetDatabase.LoadAssetAtPath<CommandKeyRegistry>(path);
            }

            if (_cachedRegistry == null)
            {
                _cachedRegistry = ScriptableObject.CreateInstance<CommandKeyRegistry>();
                EnsureDirectoryExists(DefaultRegistryPath);
                AssetDatabase.CreateAsset(_cachedRegistry, DefaultRegistryPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[CommandKeyRegistryLocator] Created: {DefaultRegistryPath}");
            }

            return _cachedRegistry;
#else
            if (_cachedRegistry == null)
            {
                _cachedRegistry = Resources.Load<CommandKeyRegistry>("CommandKeyRegistry");
                if (_cachedRegistry == null)
                {
                    _cachedRegistry = ScriptableObject.CreateInstance<CommandKeyRegistry>();
                    Debug.LogError("[CommandKeyRegistryLocator] Could not load CommandKeyRegistry at runtime (Resources/CommandKeyRegistry). Created a runtime fallback instance.");
                }
            }
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
}
