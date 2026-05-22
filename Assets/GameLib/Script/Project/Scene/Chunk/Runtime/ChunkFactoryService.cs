#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using Game.Common;
using Game.DI;
using Game.Spawn;
using Game.Vars.Generated;
using UnityEngine;
using VContainer;

namespace Game.Chunk
{
    public sealed class ChunkFactoryService : IChunkFactory
    {
        readonly ISceneSpawnerRegistry _registry;
        readonly ChunkStreamerConfig _config;
        readonly IScopeNode _owner;

        public ChunkFactoryService(ISceneSpawnerRegistry registry, ChunkStreamerConfig config, IScopeNode owner)
        {
            _registry = registry;
            _config = config;
            _owner = owner;
        }

        public async UniTask<ChunkHandle?> SpawnAsync(ChunkContext context, ChunkPlan plan, CancellationToken ct)
        {
            var dynamicContext = new SimpleDynamicContext(NullVarStore.Instance, _owner);
            if (!_config.TryResolveChunkRuntimeTemplate(dynamicContext, out var template) || template == null)
            {
                Debug.LogWarning("[ChunkFactoryService] ChunkRuntimeTemplate is null.");
                return null;
            }

            var spawner = _registry.TryGet<IAsyncSpawnerService>(_config.SpawnerKind, _config.SpawnerTag);
            if (spawner == null)
            {
                Debug.LogWarning($"[ChunkFactoryService] Spawner not found. kind={_config.SpawnerKind} tag={_config.SpawnerTag}");
                return null;
            }

            await UniTask.SwitchToMainThread();

            var spawnPos = ResolveSpawnPosition(context);
            var p = SpawnParams.ForRuntime(
                template,
                spawnPos,
                Quaternion.identity,
                Vector3.one,
                identity: null,
                transformParent: _config.ChunkParent,
                lifetimeScopeParent: _owner,
                worldSpace: true,
                allowPooling: true);

            p.WorldSpace = true;
            p.LifetimeScopeParent = _owner;

            IRuntimeResolver? resolver = null;
            try
            {
                resolver = await spawner.SpawnAsync(p, ct);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ChunkFactoryService] SpawnAsync failed: {ex.Message}");
                Debug.LogException(ex);
                return null;
            }

            if (resolver == null)
                return null;

            var lifetime = ScopeFeatureInstallerUtility.CaptureSpawnedLifetime(resolver);
            var handle = new ChunkHandle(context.Coord, context.WorldBounds, lifetime);

            var scopeNode = handle.ScopeNode;
            if (scopeNode?.Resolver != null && scopeNode.Resolver.TryResolve<IChunkAdapter>(out var adapter) && adapter != null)
            {
                try
                {
                    await adapter.InitializeAsync(context, plan, ct);
                }
                catch (OperationCanceledException)
                {
                    return handle;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ChunkFactoryService] Adapter Initialize failed: {ex.Message}");
                    Debug.LogException(ex);
                }
            }

            return handle;
        }

        public async UniTask ReleaseAsync(ChunkHandle handle, CancellationToken ct)
        {
            if (handle == null)
                return;

            try
            {
                await ScopeFeatureInstallerUtility.ReleaseSpawnedLifetimeAsync(
                    handle.Resolver,
                    ct,
                    ex =>
                    {
                        Debug.LogWarning($"[ChunkFactoryService] Pool release failed: {ex.Message}");
                        Debug.LogException(ex);
                    });
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ChunkFactoryService] Release failed: {ex.Message}");
                Debug.LogException(ex);
            }
        }

        Vector3 ResolveSpawnPosition(ChunkContext context)
        {
            if (_config.SpawnPivot == ChunkSpawnPivot.ChunkOriginCell)
            {
                var cell = new Vector2Int(context.CellRect.xMin, context.CellRect.yMin);
                var world = ChunkCoordUtility.CellToWorldCenter(cell, context.OriginSettings);
                return new Vector3(world.x, world.y, 0f);
            }

            var center = context.WorldBounds.center;
            return new Vector3(center.x, center.y, 0f);
        }

    }
}
