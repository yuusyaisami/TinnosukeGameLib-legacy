#nullable enable
using System.Collections.Generic;
using System.IO;
using Sirenix.OdinInspector;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Game.Collision
{
    public static class CollisionIdCatalogLocator
    {
        public const string DefaultCatalogPath = "Assets/Resources/CollisionIdCatalog.asset";
        const string ResourceName = "CollisionIdCatalog";

        static CollisionIdCatalogSO? cachedCatalog;

        public static CollisionIdCatalogSO? Get()
        {
#if UNITY_EDITOR
            return GetOrCreate();
#else
            if (cachedCatalog != null)
                return cachedCatalog;

            cachedCatalog = Resources.Load<CollisionIdCatalogSO>(ResourceName);
            return cachedCatalog;
#endif
        }

#if UNITY_EDITOR
        public static CollisionIdCatalogSO GetOrCreate()
        {
            if (cachedCatalog != null)
                return cachedCatalog;

            cachedCatalog = AssetDatabase.LoadAssetAtPath<CollisionIdCatalogSO>(DefaultCatalogPath);
            if (cachedCatalog != null)
            {
                EnsureSeeded(cachedCatalog);
                return cachedCatalog;
            }

            var guids = AssetDatabase.FindAssets("t:CollisionIdCatalogSO");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                cachedCatalog = AssetDatabase.LoadAssetAtPath<CollisionIdCatalogSO>(path);
                if (cachedCatalog != null)
                {
                    EnsureSeeded(cachedCatalog);
                    return cachedCatalog;
                }
            }

            cachedCatalog = ScriptableObject.CreateInstance<CollisionIdCatalogSO>();
            cachedCatalog.ResetToDefault();
            EnsureDirectoryExists(DefaultCatalogPath);
            AssetDatabase.CreateAsset(cachedCatalog, DefaultCatalogPath);
            EditorUtility.SetDirty(cachedCatalog);
            AssetDatabase.SaveAssets();
            return cachedCatalog;
        }

        static void EnsureSeeded(CollisionIdCatalogSO catalog)
        {
            if (!catalog.EnsureDefaultsIfNeeded())
                return;

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        static void EnsureDirectoryExists(string assetPath)
        {
            var directory = Path.GetDirectoryName(assetPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
        }
#endif

        public static bool TryResolveStaticProxySet(StaticColliderKind kind, out DynamicColliderSetId setId)
        {
            var catalog = Get();
            if (catalog != null && catalog.TryResolveStaticProxySet(kind, out setId))
                return true;

            setId = DynamicColliderSetId.Obstacle;
            return false;
        }

        public static string GetDynamicDisplayName(byte value)
        {
            var catalog = Get();
            if (catalog != null && catalog.TryGetDynamicName(value, out var displayName) && !string.IsNullOrEmpty(displayName))
                return displayName;

            return DynamicColliderSetId.GetBuiltinName(value);
        }

        public static string GetStaticDisplayName(byte value)
        {
            var catalog = Get();
            if (catalog != null && catalog.TryGetStaticName(value, out var displayName) && !string.IsNullOrEmpty(displayName))
                return displayName;

            return StaticColliderKind.GetBuiltinName(value);
        }

        public static IEnumerable<ValueDropdownItem<byte>> GetDynamicOptions()
        {
            var seen = new HashSet<byte>();
            var catalog = Get();
            if (catalog != null)
            {
                var entries = catalog.DynamicSets;
                for (int i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    if (entry == null || !seen.Add(entry.Value))
                        continue;

                    yield return new ValueDropdownItem<byte>($"{entry.Value}: {entry.DisplayName}", entry.Value);
                }
            }

            var builtins = DynamicColliderSetId.BuiltinValues;
            for (int i = 0; i < builtins.Length; i++)
            {
                var setId = builtins[i];
                var raw = setId.Value;
                if (!seen.Add(raw))
                    continue;

                yield return new ValueDropdownItem<byte>($"{raw}: {setId}", raw);
            }
        }

        public static IEnumerable<ValueDropdownItem<byte>> GetStaticOptions()
        {
            var seen = new HashSet<byte>();
            var catalog = Get();
            if (catalog != null)
            {
                var entries = catalog.StaticKinds;
                for (int i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    if (entry == null || !seen.Add(entry.Value))
                        continue;

                    yield return new ValueDropdownItem<byte>($"{entry.Value}: {entry.DisplayName}", entry.Value);
                }
            }

            var builtins = StaticColliderKind.BuiltinValues;
            for (int i = 0; i < builtins.Length; i++)
            {
                var kind = builtins[i];
                var raw = kind.Value;
                if (!seen.Add(raw))
                    continue;

                yield return new ValueDropdownItem<byte>($"{raw}: {kind}", raw);
            }
        }
    }
}
