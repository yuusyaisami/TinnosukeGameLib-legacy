#nullable enable
using UnityEngine;

namespace Game.RoomMap
{
    /// <summary>RoomMapTileRegistry を取得／生成するヘルパー。</summary>
    public static class RoomMapTileRegistryLocator
    {
        public const string DefaultRegistryPath = "Assets/Resources/RoomMapTileRegistry.asset";
        const string EditorPrefsOverrideKey = "RoomMapTileRegistryLocator.OverridePath";

        static RoomMapTileRegistry? _cached;
#if UNITY_EDITOR
        static RoomMapTileRegistry? _editorOverride;
#endif

        public static void ClearCache() => _cached = null;

#if UNITY_EDITOR
        public static RoomMapTileRegistry GetOrCreate()
        {
            if (_editorOverride != null)
                return _editorOverride;

            if (_cached != null)
                return _cached;

            var overridePath = UnityEditor.EditorPrefs.GetString(EditorPrefsOverrideKey, string.Empty);
            if (!string.IsNullOrEmpty(overridePath))
            {
                var overrideRegistry = UnityEditor.AssetDatabase.LoadAssetAtPath<RoomMapTileRegistry>(overridePath);
                if (overrideRegistry != null)
                {
                    _editorOverride = overrideRegistry;
                    return _editorOverride;
                }

                UnityEditor.EditorPrefs.DeleteKey(EditorPrefsOverrideKey);
            }

            _cached = UnityEditor.AssetDatabase.LoadAssetAtPath<RoomMapTileRegistry>(DefaultRegistryPath);
            if (_cached != null)
                return _cached;

            var guids = UnityEditor.AssetDatabase.FindAssets("t:RoomMapTileRegistry");
            if (guids != null)
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    var p = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
                    var loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<RoomMapTileRegistry>(p);
                    if (loaded != null)
                    {
                        _cached = loaded;
                        return _cached;
                    }
                }
            }

            _cached = ScriptableObject.CreateInstance<RoomMapTileRegistry>();

            var dir = System.IO.Path.GetDirectoryName(DefaultRegistryPath);
            if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);

            UnityEditor.AssetDatabase.CreateAsset(_cached, DefaultRegistryPath);
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.Refresh();
            Debug.Log($"[RoomMapTileRegistryLocator] Created: {DefaultRegistryPath}");
            return _cached;
        }

        public static void SetEditorOverride(RoomMapTileRegistry? registry)
        {
            _editorOverride = registry;
            if (registry == null)
            {
                UnityEditor.EditorPrefs.DeleteKey(EditorPrefsOverrideKey);
                return;
            }

            var path = UnityEditor.AssetDatabase.GetAssetPath(registry);
            if (!string.IsNullOrEmpty(path))
                UnityEditor.EditorPrefs.SetString(EditorPrefsOverrideKey, path);
        }
#else
        public static RoomMapTileRegistry GetOrCreate()
        {
            if (_cached != null)
                return _cached;

            var loaded = Resources.Load<RoomMapTileRegistry>("RoomMapTileRegistry");
            if (loaded != null)
            {
                _cached = loaded;
                return _cached;
            }

            _cached = ScriptableObject.CreateInstance<RoomMapTileRegistry>();
            Debug.LogError("[RoomMapTileRegistryLocator] Could not load RoomMapTileRegistry at runtime (Resources/RoomMapTileRegistry). Created a runtime fallback instance.");
            return _cached;
        }
#endif
    }
}
