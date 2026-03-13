#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.DI;
using Game.Spawn;
using Game.Project.Scene.Runtime;
using UnityEngine;
using VContainer;

namespace Game.UI
{
    // ================================================================
    // UIElementSpawner - BaseLifetimeScopeSpawner経由のUIElement生成
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

        public async UniTask<IObjectResolver?> SpawnAsync(SpawnParams p, CancellationToken ct = default)
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

    public sealed class UIElementRuntimeSpawnerService : IUIElementRuntimeSpawnerService
    {
        readonly IRuntimeLifetimeScopeSpawnerService _runtimeSpawner;
        readonly ISceneSpawnerRegistry _registry;

        public SpawnerKind Kind => SpawnerKind.RuntimeUIElement;
        public string Tag { get; }

        public UIElementRuntimeSpawnerService(
            IRuntimeLifetimeScopeSpawnerService runtimeSpawner,
            string tag,
            ISceneSpawnerRegistry registry)
        {
            _runtimeSpawner = runtimeSpawner ?? throw new ArgumentNullException(nameof(runtimeSpawner));
            Tag = tag ?? string.Empty;
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _registry.Register(this);
            //try { Debug.Log($"[MataUIElementRuntimeSpawnerService] Registered RuntimeUIElement spawner (Tag='{Tag}')"); } catch { }
        }

        public UniTask<IObjectResolver?> SpawnAsync(SpawnParams p, CancellationToken ct = default)
            => _runtimeSpawner.SpawnAsync(p, ct);

        public UniTask WarmupAsync<T>(T template, int count, CancellationToken ct = default)
            where T : BaseRuntimeTemplateSO
            => _runtimeSpawner.WarmupAsync(template, count, ct);
    }
}
