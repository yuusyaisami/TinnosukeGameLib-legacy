#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.DI;
using Game.Spawn;
using UnityEngine;
using VContainer;

namespace Game.Entity
{
    [DisallowMultipleComponent]
    public sealed class EntityLifetimeScopeSpawnerMB : MonoBehaviour
    {
        [Header("Spawner")]
        [SerializeField] string spawnerTag = "";

        [Tooltip("Spawn parent. Null の場合�Eこ�E GameObject 直下に生�E")]
        [SerializeField] Transform? root;

        public void InstallEntitySpawnerRuntime(IRuntimeContainerBuilder builder, IScopeNode owner)
        {
            _ = owner ?? throw new ArgumentNullException(nameof(owner));

            builder.RegisterInstance(this);

            var resolvedRoot = root != null ? root : transform;

            builder.Register<EntityLifetimeScopeSpawnerService>(RuntimeLifetime.Singleton)
                .WithParameter(resolvedRoot)
                .WithParameter(spawnerTag)
                .AsSelf()
                .As<IAsyncSpawnerService>();

            // Ensure the spawner service is instantiated so it can register itself into SceneSpawnerRegistry.
            builder.RegisterBuildCallback(resolver =>
            {
                try
                {
                    resolver.Resolve<EntityLifetimeScopeSpawnerService>();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            });
        }
    }

    public sealed class EntityLifetimeScopeSpawnerService : IAsyncSpawnerService
    {
        readonly IScopeSpawner _scopeSpawner;
        readonly Transform _root;
        readonly ISceneSpawnerRegistry _registry;

        public SpawnerKind Kind => SpawnerKind.Entity;
        public string Tag { get; }

        public EntityLifetimeScopeSpawnerService(
            IScopeSpawner scopeSpawner,
            Transform root,
            string tag,
            ISceneSpawnerRegistry registry)
        {
            _scopeSpawner = scopeSpawner ?? throw new ArgumentNullException(nameof(scopeSpawner));
            _root = root != null ? root : throw new ArgumentNullException(nameof(root));
            Tag = tag ?? "";
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _registry.Register(this);
        }

        public async UniTask<IRuntimeResolver?> SpawnAsync(SpawnParams p, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            if (p.Prefab == null)
                throw new ArgumentException("SpawnParams.Prefab is required for Entity spawns.", nameof(p));

            var prefabScope = p.Prefab.GetComponent<KernelScopeHost>();
            if (prefabScope == null)
                throw new ArgumentException("Prefab must have a KernelScopeHost root.", nameof(p));

            if (p.Prefab.TryGetComponent<ScopeIdentityMB>(out var identity) &&
                identity != null &&
                identity.kind != LifetimeScopeKind.None &&
                identity.kind != LifetimeScopeKind.Entity)
            {
                throw new ArgumentException("Prefab scope kind must be Entity for entity spawns.", nameof(p));
            }

            var parent = p.TransformParent != null ? p.TransformParent : _root;

            var scopeParams = new ScopeSpawnParams
            {
                Parent = parent,
                Position = p.Position,
                Rotation = p.Rotation,
                WorldSpace = p.WorldSpace,
                BuildSynchronously = true,
            };

            var instance = await _scopeSpawner.SpawnAsync(prefabScope, scopeParams, ct);

            if (instance != null)
            {
                var scale = p.Scale == default ? Vector3.one : p.Scale;
                instance.transform.localScale = scale;
                return instance.Container;
            }

            return null;
        }

        public UniTask WarmupAsync<T>(T template, int count, CancellationToken ct = default)
            where T : BaseRuntimeTemplateSO
            => UniTask.CompletedTask;
    }
}


