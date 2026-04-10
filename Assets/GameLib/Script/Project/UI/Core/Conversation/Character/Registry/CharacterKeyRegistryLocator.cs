#nullable enable

using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Game.Conversation
{
    public static class CharacterKeyRegistryLocator
    {
        public const string DefaultRegistryPath = "Assets/Resources/CharacterKeyRegistry.asset";

        static CharacterKeyRegistry? _cachedRegistry;

        public static CharacterKeyRegistry GetOrCreate()
        {
#if UNITY_EDITOR
            if (_cachedRegistry != null)
                return _cachedRegistry;

            _cachedRegistry = AssetDatabase.LoadAssetAtPath<CharacterKeyRegistry>(DefaultRegistryPath);
            if (_cachedRegistry != null)
                return _cachedRegistry;

            var guids = AssetDatabase.FindAssets("t:CharacterKeyRegistry");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                _cachedRegistry = AssetDatabase.LoadAssetAtPath<CharacterKeyRegistry>(path);
                if (_cachedRegistry != null)
                    return _cachedRegistry;
            }

            _cachedRegistry = ScriptableObject.CreateInstance<CharacterKeyRegistry>();
            EnsureDirectoryExists(DefaultRegistryPath);
            AssetDatabase.CreateAsset(_cachedRegistry, DefaultRegistryPath);
            AssetDatabase.SaveAssets();
            return _cachedRegistry;
#else
            if (_cachedRegistry != null)
                return _cachedRegistry;

            _cachedRegistry = Resources.Load<CharacterKeyRegistry>("CharacterKeyRegistry");
            if (_cachedRegistry != null)
                return _cachedRegistry;

            _cachedRegistry = ScriptableObject.CreateInstance<CharacterKeyRegistry>();
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
