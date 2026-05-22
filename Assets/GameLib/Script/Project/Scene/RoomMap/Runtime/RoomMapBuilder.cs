#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using Game.Common;
using Game.Spawn;
using Game.Vars.Generated;
using UnityEngine;
using VContainer;

namespace Game.RoomMap
{
    public interface IRoomMapBuilder
    {
        UniTask<RoomMapInstance> BuildAsync(RoomMapProfileSO profile, Transform parent, IScopeNode? lifetimeScopeParent, CancellationToken ct);
    }

    public sealed class RoomMapBuilder : IRoomMapBuilder
    {
        readonly ISceneSpawnerRegistry _registry;
        readonly IRoomMapSystemOptions _options;

        public RoomMapBuilder(ISceneSpawnerRegistry registry, IRoomMapSystemOptions options)
        {
            _registry = registry;
            _options = options;
        }

        public async UniTask<RoomMapInstance> BuildAsync(RoomMapProfileSO profile, Transform parent, IScopeNode? lifetimeScopeParent, CancellationToken ct)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            var layout = profile.Layout;
            var def = profile.Definition;
            var dynamicLayout = profile.DynamicLayout;

            var instance = new RoomMapInstance(layout.Layers);
            var dynamicContext = new SimpleDynamicContext(NullVarStore.Instance, lifetimeScopeParent!);

            await UniTask.SwitchToMainThread();

            var yieldEvery = 128;
            var spawned = 0;

            var layerCount = layout.LayerCount;
            for (int layerIndex = 0; layerIndex < layerCount; layerIndex++)
            {
                if (!layout.TryGetLayer(layerIndex, out var layer) || layer == null)
                    continue;

                var width = Mathf.Max(1, layer.Width);
                var height = Mathf.Max(1, layer.Height);
                var layerName = string.IsNullOrEmpty(layer.DisplayName) ? $"Layer {layerIndex}" : layer.DisplayName;
                var cells = layer.CellsUnsafe ?? Array.Empty<int>();

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        ct.ThrowIfCancellationRequested();

                        var idx = y * width + x;
                        var tileId = idx >= 0 && idx < cells.Length ? cells[idx] : 0;
                        if (tileId <= 0)
                            continue;

                        if (!def.TryGetDef(tileId, out var spawnDef) || spawnDef == null)
                            continue;

                        if (!spawnDef.IsValidForSpawn(out var reason))
                            throw new InvalidOperationException($"Invalid spawn def for tileId={tileId} at ({x},{y}): {reason}");

                        var pos = RoomMapTransformUtility.CellToWorld(profile, x, y);
                        var rot = Quaternion.Euler(0f, 0f, profile.BaseRotationDegZ);

                        var tag = spawnDef.NormalizedSpawnerTag;
                        if (_options.RuntimeSpawnerTag != "")
                        {
                            tag = _options.RuntimeSpawnerTag;
                        }

                        var spawner = _registry.TryGet<IAsyncSpawnerService>(spawnDef.Kind, tag)
                            ?? _registry.TryGet<IAsyncSpawnerService>(spawnDef.Kind, "");

                        if (spawner == null)
                            throw new InvalidOperationException($"Spawner not found. kind={spawnDef.Kind}, tag='{tag}'");

                        SpawnParams p;
                        if (spawnDef.Source == RoomMapSpawnSource.Prefab)
                        {
                            if (spawnDef.Prefab == null)
                                throw new InvalidOperationException("Prefab is null.");

                            p = SpawnParams.ForLTS(spawnDef.Prefab, pos, rot, new Vector3(1f, 1f, 1f), transformParent: parent);
                        }
                        else if (spawnDef.Source == RoomMapSpawnSource.RuntimeTemplate)
                        {
                            if (!spawnDef.TryResolveRuntimeTemplate(dynamicContext, out var runtimeTemplate) || runtimeTemplate == null)
                                throw new InvalidOperationException("Template is null.");

                            p = SpawnParams.ForRuntime(
                                runtimeTemplate,
                                pos,
                                rot,
                                Vector3.one,
                                identity: null,
                                transformParent: parent,
                                lifetimeScopeParent: null,
                                worldSpace: profile.WorldSpace,
                                allowPooling: spawnDef.AllowPooling);
                        }
                        else
                        {
                            continue;
                        }

                        p.WorldSpace = profile.WorldSpace;
                        p.LifetimeScopeParent = lifetimeScopeParent;
                        p.AllowPooling = spawnDef.AllowPooling;

                        var resolver = await spawner.SpawnAsync(p, ct);

                        var lifetime = SpawnedLifetimeHandle.FromResolver(resolver);

                        // Diagnostic: warn if runtime scope identity appears unset (helps debug missing LTS identity reports)
                        if (lifetime.HasMissingRuntimeIdentity)
                        {
                        }

                        var record = new RoomMapInstance.CellRecord(
                            layerIndex,
                            layerName,
                            x,
                            y,
                            tileId,
                            pos,
                            lifetime);

                        instance.Set(layerIndex, x, y, record);

                        spawned++;
                        if (spawned % yieldEvery == 0)
                            await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    }
                }
            }

            if (dynamicLayout != null)
            {
                await BuildDynamicAsync(instance, profile, parent, def, dynamicLayout, lifetimeScopeParent, ct);
            }

            return instance;
        }

        async UniTask BuildDynamicAsync(
            RoomMapInstance instance,
            RoomMapProfileSO profile,
            Transform parent,
            RoomMapTileDefinitionSO def,
            RoomMapDynamicLayoutSO dynamicLayout,
            IScopeNode? lifetimeScopeParent,
            CancellationToken ct)
        {
            await UniTask.SwitchToMainThread();
            var dynamicContext = new SimpleDynamicContext(NullVarStore.Instance, lifetimeScopeParent!);

            // Safety: dynamic spawns can execute template lifecycle commands. If those hang, RoomMap build would never finish.
            // Apply a conservative timeout and treat it according to the profile FailurePolicy.
            const float spawnTimeoutSeconds = 10f;
            var yieldEvery = 64;
            var spawned = 0;

            var entryIndex = 0;
            foreach (var entry in dynamicLayout.EnumerateValidEntries())
            {
                entryIndex++;
                ct.ThrowIfCancellationRequested();

                var tileId = entry.TileId;
                if (tileId <= 0)
                    continue;

                if (!def.TryGetDef(tileId, out var spawnDef) || spawnDef == null)
                    continue;

                if (!spawnDef.IsValidForSpawn(out var reason))
                    throw new InvalidOperationException($"Invalid spawn def for dynamic tileId={tileId} at cell=({entry.Cell.x},{entry.Cell.y}): {reason}");

                var basePos = RoomMapTransformUtility.CellToWorld(profile, entry.Cell.x, entry.Cell.y);
                var baseRot = Quaternion.Euler(0f, 0f, profile.BaseRotationDegZ);
                var extraRot = Quaternion.Euler(0f, 0f, entry.RotationDegZ);

                var pos = basePos + (baseRot * entry.LocalOffset);
                var rot = baseRot * extraRot;

                var tag = spawnDef.NormalizedSpawnerTag;
                if (_options.RuntimeSpawnerTag != "")
                {
                    tag = _options.RuntimeSpawnerTag;
                }

                var spawner = _registry.TryGet<IAsyncSpawnerService>(spawnDef.Kind, tag)
                    ?? _registry.TryGet<IAsyncSpawnerService>(spawnDef.Kind, "");

                if (spawner == null)
                    throw new InvalidOperationException($"Spawner not found. kind={spawnDef.Kind}, tag='{tag}'");

                // Dynamic entry resolved spawner.

                SpawnParams p;
                if (spawnDef.Source == RoomMapSpawnSource.Prefab)
                {
                    if (spawnDef.Prefab == null)
                        throw new InvalidOperationException("Prefab is null.");

                    p = SpawnParams.ForLTS(spawnDef.Prefab, pos, rot, new Vector3(1f, 1f, 1f), transformParent: parent);
                }
                else if (spawnDef.Source == RoomMapSpawnSource.RuntimeTemplate)
                {
                    if (!spawnDef.TryResolveRuntimeTemplate(dynamicContext, out var runtimeTemplate) || runtimeTemplate == null)
                        throw new InvalidOperationException("Template is null.");

                    p = SpawnParams.ForRuntime(
                        runtimeTemplate,
                        pos,
                        rot,
                        entry.Scale,
                        identity: null,
                        transformParent: parent,
                        lifetimeScopeParent: null,
                        worldSpace: profile.WorldSpace,
                        allowPooling: spawnDef.AllowPooling);
                }
                else
                {
                    continue;
                }

                p.WorldSpace = profile.WorldSpace;
                p.LifetimeScopeParent = lifetimeScopeParent;
                p.AllowPooling = spawnDef.AllowPooling;

                IRuntimeResolver? resolver = null;
                using (var spawnCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    var spawnTask = spawner.SpawnAsync(p, spawnCts.Token);
                    var timeoutTask = UniTask.Delay(TimeSpan.FromSeconds(spawnTimeoutSeconds), ignoreTimeScale: true, cancellationToken: CancellationToken.None);

                    var (spawnCompleted, _) = await UniTask.WhenAny(spawnTask, timeoutTask);
                    if (!spawnCompleted)
                    {
                        spawnCts.Cancel();

                        UniTask.Void(async () =>
                        {
                            try { await spawnTask; }
                            catch (Exception ex) { Debug.LogException(ex); }
                        });

                        var msg = $"[RoomMapBuilder] Dynamic spawn timed out after {spawnTimeoutSeconds:0.0}s. profile={profile.name} cell=({entry.Cell.x},{entry.Cell.y}) tileId={tileId} kind={spawnDef.Kind} tag='{tag}' source={spawnDef.Source} prefab={(spawnDef.Prefab != null ? spawnDef.Prefab.name : "<null>")} templateSource={spawnDef.TemplatePreset.SourceTypeName}:{spawnDef.TemplatePreset.SourceDebugData}";

                        if (profile.FailurePolicy == RoomMapFailurePolicy.ContinueOnError)
                        {
                            continue;
                        }

                        throw new TimeoutException(msg);
                    }

                    resolver = await spawnTask;
                }

                if (resolver == null)
                    continue;

                var lifetime = SpawnedLifetimeHandle.FromResolver(resolver);
                var dynWorldPos = lifetime.Root != null ? lifetime.Root.transform.position : pos;
                var dynamicRecord = new RoomMapInstance.DynamicRecord(
                    tileId,
                    entry.Cell,
                    dynWorldPos,
                    lifetime);
                instance.AddDynamic(dynamicRecord);

                spawned++;
                if (spawned % yieldEvery == 0)
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

        }
    }
}
