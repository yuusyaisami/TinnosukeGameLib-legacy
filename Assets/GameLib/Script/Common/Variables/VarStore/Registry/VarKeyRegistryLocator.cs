#nullable enable
using System;
using System.IO;
using Game.VarStoreKeys;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

/// <summary>VarKeyRegistry を explicit に取得するヘルパー。</summary>
public static class VarKeyRegistryLocator
{
    // Runtime では explicit に配置された VarKeyRegistry だけを使う。
    // Runtime fallback の asset lookup や空 registry 生成は行わない。
    public const string DefaultRegistryPath = "Assets/Resources/VarKeyRegistry.asset";

    // Runtime では明示的に登録された registry だけを使う。
    static VarKeyRegistry? _cachedRegistry;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetRuntimeState()
    {
        _cachedRegistry = null;
    }

    public static void SetExplicitRuntimeRegistry(VarKeyRegistry registry)
    {
        if (registry == null)
            throw new ArgumentNullException(nameof(registry));

        if (registry.RegisteredKeyCount <= 0)
        {
            throw new ArgumentException(
                "VarKeyRegistryLocator requires a populated explicit registry when registering runtime value-key authority.",
                nameof(registry));
        }

        _cachedRegistry = registry;
    }

    public static void ClearExplicitRuntimeRegistry()
    {
        _cachedRegistry = null;
    }

    public static bool TryGetExplicitRegistry(out VarKeyRegistry? registry)
    {
#if UNITY_EDITOR
        if (_cachedRegistry != null && _cachedRegistry.RegisteredKeyCount > 0)
        {
            registry = _cachedRegistry;
            return true;
        }

        var explicitRegistry = AssetDatabase.LoadAssetAtPath<VarKeyRegistry>(DefaultRegistryPath);
        if (explicitRegistry != null && explicitRegistry.RegisteredKeyCount > 0)
        {
            _cachedRegistry = explicitRegistry;
            registry = _cachedRegistry;
            return true;
        }

        registry = null;
        return false;
#else
        if (_cachedRegistry != null && _cachedRegistry.RegisteredKeyCount > 0)
        {
            registry = _cachedRegistry;
            return true;
        }

        registry = null;
        return false;
#endif
    }

    public static VarKeyRegistry GetOrCreate()
    {
#if UNITY_EDITOR
        if (_cachedRegistry != null && _cachedRegistry.RegisteredKeyCount > 0)
            return _cachedRegistry;

        var explicitRegistry = AssetDatabase.LoadAssetAtPath<VarKeyRegistry>(DefaultRegistryPath);

        if (explicitRegistry != null)
        {
            _cachedRegistry = explicitRegistry;
            if (EnsureRuntimeTraitPresentationStateSeed(_cachedRegistry))
                AssetDatabase.SaveAssets();

            return _cachedRegistry;
        }

        _cachedRegistry = ScriptableObject.CreateInstance<VarKeyRegistry>();
        EnsureDirectoryExists(DefaultRegistryPath);
        AssetDatabase.CreateAsset(_cachedRegistry, DefaultRegistryPath);
        EnsureRuntimeTraitPresentationStateSeed(_cachedRegistry);
        AssetDatabase.SaveAssets();
        return _cachedRegistry;
#else
        if (TryGetExplicitRegistry(out var registry) && registry != null)
            return registry;

        throw new InvalidOperationException("VarKeyRegistryLocator requires an explicit populated VarKeyRegistry asset at runtime.");
#endif
    }

#if UNITY_EDITOR
    static void EnsureDirectoryExists(string assetPath)
    {
        var dir = Path.GetDirectoryName(assetPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }

    const string RuntimeTraitPresentationStateStableKey = "traitRuntime.presentationState";
    const string RuntimeTraitPresentationStatePath = "GameLib/Base/Trait/Runtime/presentationState";
    const int RuntimeTraitPresentationStateVarId = 100125;

    static bool EnsureRuntimeTraitPresentationStateSeed(VarKeyRegistry registry)
    {
        if (registry == null)
            return false;

        var changed = false;
        var leaf = FindLeafByStableKey(registry, RuntimeTraitPresentationStateStableKey)
            ?? registry.FindNodeByPath(RuntimeTraitPresentationStatePath);

        if (leaf == null)
        {
            var parent = EnsureFolderByPath(registry, "GameLib/Base/Trait/Runtime", ref changed);
            leaf = registry.CreateLeaf(parent.Id, "presentationState");
            changed = true;
        }

        if (leaf.VarId != RuntimeTraitPresentationStateVarId)
        {
            leaf.VarId = RuntimeTraitPresentationStateVarId;
            changed = true;
        }

        if (!string.Equals(leaf.StableKey, RuntimeTraitPresentationStateStableKey, StringComparison.Ordinal))
        {
            if (!string.IsNullOrEmpty(leaf.StableKey) && !leaf.Aliases.Contains(leaf.StableKey))
                leaf.Aliases.Add(leaf.StableKey);

            leaf.StableKey = RuntimeTraitPresentationStateStableKey;
            changed = true;
        }

        if (string.IsNullOrEmpty(leaf.Description))
        {
            leaf.Description = "RuntimeTrait visible/hidden state.";
            changed = true;
        }

        if (changed)
            registry.EnsureLookupRebuild();

        return changed;
    }

    static VarKeyNode? FindLeafByStableKey(VarKeyRegistry registry, string stableKey)
    {
        if (registry == null || string.IsNullOrEmpty(stableKey))
            return null;

        var nodes = registry.Nodes;
        if (nodes == null)
            return null;

        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            if (node == null || node.IsFolder)
                continue;

            if (string.Equals(node.StableKey, stableKey, StringComparison.Ordinal))
                return node;
        }

        return null;
    }

    static VarKeyNode EnsureFolderByPath(VarKeyRegistry registry, string path, ref bool changed)
    {
        var existing = registry.FindNodeByPath(path);
        if (existing != null)
            return existing;

        var lastSlash = path.LastIndexOf('/');
        if (lastSlash < 0)
        {
            changed = true;
            return registry.CreateFolder(string.Empty, path);
        }

        var parentPath = path.Substring(0, lastSlash);
        var segment = path.Substring(lastSlash + 1);
        var parent = EnsureFolderByPath(registry, parentPath, ref changed);
        changed = true;
        return registry.CreateFolder(parent.Id, segment);
    }
#endif
}
