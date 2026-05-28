#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.DI;
using Game.Kernel.Layers;
using Game.Spawn;
using Game.Project.Scene.Runtime;
using UnityEngine;
using VContainer;

namespace Game.UI
{
    // ================================================================
    // UIElementSpawner - BaseLifetimeScopeSpawner経由のUIElement生�E
    // ================================================================

    public interface IUIElementSpawnerService : IAsyncSpawnerService
    {
    }

    public sealed class UIElementSpawnerService : IUIElementSpawnerService
    {
        readonly IScopeSpawner _scopeSpawner;
        readonly Transform _root;
        readonly ISceneSpawnerRegistry _registry;

        public SpawnerKind Kind => SpawnerKind.UIElement;
        public string Tag { get; }

        public UIElementSpawnerService(
            IScopeSpawner scopeSpawner,
            Transform root,
            string tag,
            ISceneSpawnerRegistry registry)
        {
            _scopeSpawner = scopeSpawner ?? throw new ArgumentNullException(nameof(scopeSpawner));
            _root = root != null ? root : throw new ArgumentNullException(nameof(root));
            Tag = tag ?? string.Empty;
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _registry.Register(this);

        }

        public async UniTask<IRuntimeResolver?> SpawnAsync(SpawnParams p, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            if (p.Prefab == null)
                throw new ArgumentException("SpawnParams.Prefab is required for UIElement spawns.", nameof(p));

            var prefabScope = p.Prefab.GetComponent<UIElementLifetimeScope>();
            if (prefabScope == null)
                throw new ArgumentException($"Prefab must contain {nameof(UIElementLifetimeScope)}.", nameof(p));

            var parent = p.TransformParent != null ? p.TransformParent : _root;

            var scopeParams = new ScopeSpawnParams
            {
                Parent = parent,
                Position = p.Position,
                Rotation = p.Rotation,
                WorldSpace = p.WorldSpace,
                BuildSynchronously = false,
            };

            var instance = await _scopeSpawner.SpawnAsync(prefabScope, scopeParams, ct);
            if (instance == null)
            {
                return null;
            }

            instance.transform.localScale = p.Scale == default ? Vector3.one : p.Scale;
            return instance.Container;
        }

        public UniTask WarmupAsync<T>(T template, int count, CancellationToken ct = default)
            where T : BaseRuntimeTemplateSO
            => UniTask.CompletedTask;
    }

    // MB installers moved to dedicated files to ensure Unity recognizes their class-file mapping.

    public interface IUIElementRuntimeSpawnerService : IAsyncSpawnerService { }

    public sealed class UIElementRuntimeSpawnerService : IUIElementRuntimeSpawnerService, IFilteredReleaseSpawnerService, ISceneKernelSpawnPool, ISceneKernelSpawnRouteHandler
    {
        readonly IAsyncSpawnerService _runtimeSpawner;
        readonly Transform _root;
        readonly ISceneSpawnerRegistry _registry;
        bool _sceneKernelRegistered;

        public SpawnerKind Kind => SpawnerKind.RuntimeUIElement;
        public string Tag { get; }
        public SceneKernelSpawnRouteId RouteId => SceneKernelSpawnRouteId.FromParts(Kind.ToString(), Tag);
        public SceneKernelSpawnPoolId PoolId => SceneKernelSpawnPoolId.FromParts(Kind.ToString(), Tag);

        public UIElementRuntimeSpawnerService(
            IAsyncSpawnerService runtimeSpawner,
            Transform root,
            string tag,
            ISceneSpawnerRegistry registry)
        {
            _runtimeSpawner = runtimeSpawner ?? throw new ArgumentNullException(nameof(runtimeSpawner));
            _root = root != null ? root : throw new ArgumentNullException(nameof(root));
            Tag = tag ?? string.Empty;
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _registry.Register(this);
            EnsureSceneKernelBinding();
            //try { Debug.Log($"[MataUIElementRuntimeSpawnerService] Registered RuntimeUIElement spawner (Tag='{Tag}')"); } catch { }
        }

        public UniTask<IRuntimeResolver?> SpawnAsync(SpawnParams p, CancellationToken ct = default)
        {
            EnsureSceneKernelBinding();
            if (p.TransformParent == null)
                p.TransformParent = _root;

            return _runtimeSpawner.SpawnAsync(p, ct);
        }

        public UniTask WarmupAsync<T>(T template, int count, CancellationToken ct = default)
            where T : BaseRuntimeTemplateSO
            => _runtimeSpawner.WarmupAsync(template, count, ct);

        public int ReleaseAll(RuntimeLifetimeScopeDeleteFilter filter)
        {
            EnsureSceneKernelBinding();
            if (_runtimeSpawner is IFilteredReleaseSpawnerService releaseSpawner)
                return releaseSpawner.ReleaseAll(filter);

            throw new InvalidOperationException($"{nameof(UIElementRuntimeSpawnerService)} backing spawner does not support filtered release.");
        }

        int ISceneKernelSpawnPool.ReleaseAll(object filter)
        {
            if (filter is RuntimeLifetimeScopeDeleteFilter typedFilter)
                return ReleaseAll(typedFilter);

            throw new ArgumentException($"{nameof(UIElementRuntimeSpawnerService)} requires {nameof(RuntimeLifetimeScopeDeleteFilter)}.", nameof(filter));
        }

        async ValueTask<object?> ISceneKernelSpawnRouteHandler.SpawnAsync(object spawnRequest, CancellationToken cancellationToken)
        {
            if (spawnRequest is not SpawnParams spawnParams)
                throw new ArgumentException($"{nameof(UIElementRuntimeSpawnerService)} requires {nameof(SpawnParams)}.", nameof(spawnRequest));

            return await SpawnAsync(spawnParams, cancellationToken);
        }

        async ValueTask ISceneKernelSpawnRouteHandler.WarmupAsync(object template, int count, CancellationToken cancellationToken)
        {
            if (template is not BaseRuntimeTemplateSO runtimeTemplate)
                throw new ArgumentException($"{nameof(UIElementRuntimeSpawnerService)} requires {nameof(BaseRuntimeTemplateSO)}.", nameof(template));

            await _runtimeSpawner.WarmupAsync(runtimeTemplate, count, cancellationToken);
        }

        void EnsureSceneKernelBinding()
        {
            if (_sceneKernelRegistered)
                return;

            SceneKernelSpawnBindingHub.Register(this, this);
            _sceneKernelRegistered = true;
        }
    }
}
