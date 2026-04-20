#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.DI;
using Game.Spawn;
using Game.UI;
using UnityEngine;
using VContainer;

namespace Game.Channel
{
    internal sealed class GridObjectChannelVisualSpawner
    {
        readonly string _tag;

        public GridObjectChannelVisualSpawner(string tag)
        {
            _tag = tag;
        }

        public async UniTask<GridObjectChannelVisualInstance?> SpawnRawAsync(
            GridObjectChannelRuntimeState state,
            GridObjectChannelResolvedItem item,
            CancellationToken ct)
        {
            if (state.ActiveScope == null || state.ListRoot == null || state.ResolvedRuntimeTemplate == null)
                return null;

            if (!GridObjectChannelRuntimeUtility.TryResolveFromScopeOrAncestors<ISceneSpawnerRegistry>(state.ActiveScope, out var registry) || registry == null)
            {
                Debug.LogWarning($"[GridObjectChannel] ISceneSpawnerRegistry is not available. Tag='{_tag}'");
                return null;
            }

            var spawner = ResolveSpawner(state.EnvironmentKind, registry);
            if (spawner == null)
            {
                Debug.LogWarning($"[GridObjectChannel] Runtime spawner is not available. Tag='{_tag}'");
                return null;
            }

            await UniTask.SwitchToMainThread();
            ct.ThrowIfCancellationRequested();

            var spawnParams = SpawnParams.ForRuntime(
                state.ResolvedRuntimeTemplate,
                Vector3.zero,
                Quaternion.identity,
                Vector3.one,
                identity: null,
                transformParent: state.ListRoot,
                lifetimeScopeParent: state.ActiveScope,
                worldSpace: false,
                allowPooling: state.ResolvedVisualizerPreset.AllowPooling);

            IRuntimeResolver? resolver = null;
            try
            {
                resolver = await spawner.SpawnAsync(spawnParams, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GridObjectChannel] Spawn failed. Tag='{_tag}' Message={ex.Message}");
                return null;
            }

            GridObjectChannelRuntimeUtility.ExtractSpawnedInfo(resolver, out var root, out var scopeNode);
            if (resolver == null || root == null || scopeNode == null)
            {
                await GridObjectChannelRuntimeUtility.ReleaseSpawnedInstanceAsync(root, scopeNode, resolver);
                Debug.LogError($"[GridObjectChannel] Spawned instance is missing root or scope. Tag='{_tag}'");
                return null;
            }

            return new GridObjectChannelVisualInstance(GridObjectChannelItemKey.Standalone(-1), root, scopeNode, resolver);
        }

        public async UniTask ClearSpawnedInstancesAsync(GridObjectChannelVisualCollection visuals, CancellationToken ct)
        {
            for (var i = visuals.Count - 1; i >= 0; i--)
            {
                ct.ThrowIfCancellationRequested();
                var instance = visuals[i];
                if (instance == null)
                    continue;

                await GridObjectChannelRuntimeUtility.ReleaseSpawnedInstanceAsync(instance.Root, instance.Scope, instance.Resolver);
            }

            visuals.Clear();
        }

        public static void SetInstancePresentationVisible(GridObjectChannelVisualInstance instance, bool visible)
        {
            if (instance == null)
                return;

            TransformGridSharedUtility.SetUiElementVisible(instance.Resolver, visible);
            instance.Scope.TrySetVisible(visible);

            var renderers = instance.Root.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                    continue;

                renderer.forceRenderingOff = !visible;
            }
        }

        static IAsyncSpawnerService? ResolveSpawner(TransformGridEnvironmentKind environmentKind, ISceneSpawnerRegistry registry)
        {
            var primary = environmentKind == TransformGridEnvironmentKind.ScreenUI
                ? SpawnerKind.RuntimeUIElement
                : SpawnerKind.RuntimeEntity;
            var fallback = primary == SpawnerKind.RuntimeUIElement
                ? SpawnerKind.RuntimeEntity
                : SpawnerKind.RuntimeUIElement;

            return registry.TryGet<IAsyncSpawnerService>(primary, "") ??
                   registry.TryGet<IAsyncSpawnerService>(fallback, "");
        }
    }
}
