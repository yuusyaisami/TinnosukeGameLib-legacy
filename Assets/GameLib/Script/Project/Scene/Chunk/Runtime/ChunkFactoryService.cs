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

            IObjectResolver? resolver = null;
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

            ExtractSpawnedInfo(resolver, out var root, out var scopeNode, out var runtimeScope, out var baseScope);

            var handle = new ChunkHandle(context.Coord, context.WorldBounds, scopeNode, runtimeScope, baseScope, root, resolver);

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

            await UniTask.SwitchToMainThread();

            try
            {
                if (handle.RuntimeScope != null)
                {
                    try
                    {
                        if (handle.RuntimeScope.Resolver != null &&
                            handle.RuntimeScope.Resolver.TryResolve<IRuntimeLifetimeScopePool>(out var pool) &&
                            pool != null)
                        {
                            pool.Release(handle.RuntimeScope);
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[ChunkFactoryService] Pool release failed: {ex.Message}");
                        Debug.LogException(ex);
                    }

                    if (handle.Root != null)
                    {
                        try { UnityEngine.Object.Destroy(handle.Root); } catch (Exception ex) { Debug.LogException(ex); }
                    }
                    else
                    {
                        try { UnityEngine.Object.Destroy(handle.RuntimeScope.gameObject); } catch (Exception ex) { Debug.LogException(ex); }
                    }

                    return;
                }

                if (handle.BaseScope != null)
                {
                    try
                    {
                        await handle.BaseScope.DespawnAsync(CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[ChunkFactoryService] Despawn failed: {ex.Message}");
                        Debug.LogException(ex);
                    }

                    return;
                }

                if (handle.Root != null)
                {
                    try { UnityEngine.Object.Destroy(handle.Root); } catch (Exception ex) { Debug.LogException(ex); }
                }
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

        static void ExtractSpawnedInfo(
            IObjectResolver? resolver,
            out GameObject? root,
            out IScopeNode? scopeNode,
            out RuntimeLifetimeScope? runtimeScope,
            out BaseLifetimeScope? baseScope)
        {
            root = null;
            scopeNode = null;
            runtimeScope = null;
            baseScope = null;

            if (resolver == null)
                return;

            resolver.TryResolve(out runtimeScope);

            if (runtimeScope != null)
                root = runtimeScope.gameObject;

            if (root == null)
            {
                if (resolver.TryResolve<Transform>(out var tr) && tr != null)
                    root = tr.gameObject;
                else if (resolver.TryResolve<GameObject>(out var go) && go != null)
                    root = go;
            }

            scopeNode = runtimeScope;
            if (scopeNode == null && resolver.TryResolve<IScopeNode>(out var resolved) && resolved != null)
                scopeNode = resolved;

            if (scopeNode == null && root != null)
            {
                var comps = root.GetComponents<Component>();
                for (int i = 0; i < comps.Length; i++)
                {
                    if (comps[i] is IScopeNode n)
                    {
                        scopeNode = n;
                        break;
                    }
                }
            }

            baseScope = scopeNode as BaseLifetimeScope;
        }
    }
}
