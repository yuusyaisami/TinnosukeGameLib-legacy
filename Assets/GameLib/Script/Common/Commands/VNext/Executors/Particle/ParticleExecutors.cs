#nullable enable
using System.Collections.Generic;
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using Game.Spawn;
using Game.DI;
using UnityEngine;
using VContainer;
using Game.Common;
namespace Game.Commands.VNext
{
    public sealed class SpawnParticleExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.SpawnParticle;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not SpawnParticleCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "SpawnParticleCommandData is required.");

            var runTask = ExecuteCoreAsync(typed, ctx, ct);
            return typed.AwaitMode == FlowRunAwaitMode.WaitForCompletion
                ? runTask
                : RunInBackground(runTask);
        }

        static UniTask RunInBackground(UniTask task)
        {
            UniTask.Void(async () =>
            {
                try { await task; }
                catch (OperationCanceledException) { }
                catch (ObjectDisposedException) { }
                catch (Exception e) { Debug.LogException(e); }
            });
            return UniTask.CompletedTask;
        }

        static async UniTask ExecuteCoreAsync(SpawnParticleCommandData typed, CommandContext ctx, CancellationToken ct)
        {

            if (ctx.Scope is not Component ownerComponent)
                return;

            IEmitterService? emitterService = null;
            RuntimeLifetimeScope? spawnedEmitterScope = null;
            BaseRuntimeTemplateSO? fireTemplate = null;
            FirePatternRuntimeTemplatePreset? fireTemplatePreset = null;
            IRuntimeLifetimeScopePool? pool = null;
            try
            {
                if (typed.FireTemplate.TryGet(ctx, out var resolvedFireTemplatePreset) && resolvedFireTemplatePreset != null)
                {
                    fireTemplatePreset = resolvedFireTemplatePreset;
                    fireTemplate = RuntimeTemplatePresetResolver.ResolveTemplateSO(fireTemplatePreset);
                    if (fireTemplate == null)
                    {
                        Debug.LogWarning(
                            $"[SpawnParticleExecutor] FireTemplate preset could not resolve TemplateSO. " +
                            $"TemplateId={fireTemplatePreset.TemplateId}, HasPrefab={(fireTemplatePreset.Prefab != null)}");
                    }
                }

                if (typed.UseExistingEmitterIfPresent &&
                    ctx.Resolver.TryResolve<IEmitterService>(out var existing) &&
                    existing != null)
                {
                    emitterService = existing;
                }

                if (emitterService == null)
                {
                    if (!typed.SpawnEmitterIfMissing)
                        return;

                    if (!typed.EmitterTemplate.TryGet(ctx, out var emitterPreset) || emitterPreset == null)
                    {
                        Debug.LogWarning("[SpawnParticleExecutor] EmitterTemplate is null/unresolved. SpawnParticle skipped.");
                        return;
                    }

                    var emitterTemplate = RuntimeTemplatePresetResolver.ResolveTemplateSO(emitterPreset);
                    if (emitterTemplate == null)
                    {
                        Debug.LogWarning(
                            $"[SpawnParticleExecutor] EmitterTemplate preset could not resolve TemplateSO. " +
                            $"TemplateId={emitterPreset.TemplateId}, HasPrefab={(emitterPreset.Prefab != null)}");
                        return;
                    }

                    if (!ctx.Resolver.TryResolve<IRuntimeLifetimeScopePool>(out pool) || pool == null)
                    {
                        Debug.LogWarning("[SpawnParticleExecutor] IRuntimeLifetimeScopePool not found. SpawnParticle skipped.");
                        return;
                    }

                    var ownerTransform = ownerComponent.transform;
                    var dynCtx = new SimpleDynamicContext(ctx.Vars ?? NullVarStore.Instance, ctx.Scope);
                    var basePos = typed.EmitterSpawnPosition.HasSource
                        ? typed.EmitterSpawnPosition.Resolve(dynCtx)
                        : ownerTransform.position;
                    var offset = typed.EmitterSpawnOffset.HasSource
                        ? typed.EmitterSpawnOffset.Resolve(dynCtx)
                        : typed.LegacyEmitterSpawnOffset;
                    var pos = basePos + offset;
                    var rot = typed.EmitterSpawnRotationEuler.HasSource
                        ? Quaternion.Euler(typed.EmitterSpawnRotationEuler.Resolve(dynCtx))
                        : ownerTransform.rotation * Quaternion.Euler(typed.LegacyEmitterSpawnRotationEulerOffset);

                    if (typed.EmitterParent == SpawnParticleCommandData.EmitterParentMode.SpawnerDefault)
                    {
                        var result = await TrySpawnEmitterViaSpawnerAsync(ctx, emitterTemplate, pos, rot, ct);
                        if (result.Success)
                        {
                            spawnedEmitterScope = result.Scope;
                            if (result.Resolver != null &&
                                result.Resolver.TryResolve<IEmitterService>(out var created) &&
                                created != null)
                            {
                                emitterService = created;
                            }
                            else if (result.Scope != null)
                            {
                                pool.Release(result.Scope);
                            }
                        }
                    }

                    if (emitterService == null)
                    {
                        var parent = ResolveEmitterParentTransform(typed, ctx, ownerTransform) ?? ownerTransform;
                        spawnedEmitterScope = await pool.AcquireAsync(emitterTemplate, parent, pos, rot, identity: null, ct: ct);
                        if (spawnedEmitterScope == null)
                        {
                            Debug.LogWarning("[SpawnParticleExecutor] Failed to spawn emitter scope (pool returned null).");
                            return;
                        }
                        if (spawnedEmitterScope.Container == null ||
                            !spawnedEmitterScope.Container.TryResolve<IEmitterService>(out var created) ||
                            created == null)
                        {
                            pool.Release(spawnedEmitterScope);
                            spawnedEmitterScope = null;
                            return;
                        }

                        emitterService = created;
                    }
                }

                if (emitterService == null)
                    return;

                if (typed.Patterns != null && typed.Patterns.Length > 0)
                {
                    var hasValidPattern = false;
                    for (int i = 0; i < typed.Patterns.Length; i++)
                    {
                        ct.ThrowIfCancellationRequested();
                        var pattern = typed.Patterns[i];
                        if (pattern == null)
                            continue;

                        hasValidPattern = true;
                        await emitterService.ExecutePatternAsync(pattern, fireTemplate, null, ct);
                    }

                    if (hasValidPattern)
                        return;
                }

                if (fireTemplate is not { } particleTemplate)
                {
                    var fireTemplateId = fireTemplatePreset?.TemplateId ?? "(null)";
                    var hasPrefab = fireTemplatePreset?.Prefab != null;
                    Debug.LogWarning(
                        $"[SpawnParticleExecutor] Direct spawn skipped because FireTemplate could not be resolved. " +
                        $"FireTemplateSource={typed.FireTemplate.SourceTypeName}, TemplateId={fireTemplateId}, HasPrefab={hasPrefab}.");
                    return;
                }

                var particleTemplateNonNull = particleTemplate;

                var count = Mathf.Max(1, typed.DirectSpawnCount);
                var posBase = emitterService.Origin + (emitterService.Rotation * typed.DirectSpawnLocalOffset);
                var rotBase = (typed.DirectSpawnUseEmitterRotation ? emitterService.Rotation : Quaternion.identity) *
                              Quaternion.Euler(typed.DirectSpawnRotationEulerOffset);

                IAsyncSpawnerService? directSpawner = null;
                if (ctx.Resolver.TryResolve<ISceneSpawnerRegistry>(out var registry) && registry != null)
                {
                    var resolved = SceneSpawnerResolver.TryResolveAsyncSpawner(
                        registry,
                        SpawnerKind.RuntimeEntity,
                        "",
                        allowTagFallback: true,
                        allowRuntimeUiFallback: false);
                    directSpawner = resolved.Spawner;
                }

                for (int i = 0; i < count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    if (directSpawner != null)
                    {
                        var spawnParams = SpawnParams.ForRuntime(
                            particleTemplateNonNull,
                            posBase,
                            rotBase,
                            Vector3.one,
                            identity: null,
                            transformParent: null,
                            lifetimeScopeParent: ctx.Scope,
                            worldSpace: true,
                            allowPooling: true);
                        var unitResolver = await directSpawner.SpawnAsync(spawnParams, ct);
                        if (unitResolver != null)
                        {
                            NotifyDirectSpawn(emitterService, unitResolver, spawnParams, posBase, rotBase, i, fireTemplatePreset);
                        }
                        else
                        {
                            Debug.LogWarning(
                                $"[SpawnParticleExecutor] Direct spawn returned null resolver. " +
                                $"Template={(particleTemplate != null ? particleTemplate.TemplateId : "(null)")}, Index={i}");
                        }
                        continue;
                    }

                    if (pool == null)
                    {
                        if (!ctx.Resolver.TryResolve<IRuntimeLifetimeScopePool>(out pool) || pool == null)
                        {
                            Debug.LogWarning("[SpawnParticleExecutor] IRuntimeLifetimeScopePool not found. Direct spawn skipped.");
                            return;
                        }
                    }

                    var scope = await pool.AcquireAsync(particleTemplateNonNull, ownerComponent.transform, posBase, rotBase, identity: null, ct: ct);
                    var resolver = scope != null ? scope.Container : null;
                    if (resolver != null)
                    {
                        var spawnParams = SpawnParams.ForRuntime(
                            particleTemplateNonNull,
                            posBase,
                            rotBase,
                            Vector3.one,
                            identity: null,
                            transformParent: ownerComponent.transform,
                            lifetimeScopeParent: ctx.Scope,
                            worldSpace: true,
                            allowPooling: true);
                        NotifyDirectSpawn(emitterService, resolver, spawnParams, posBase, rotBase, i, fireTemplatePreset);
                    }
                    else
                    {
                        Debug.LogWarning(
                            $"[SpawnParticleExecutor] Direct spawn via pool failed (scope/container null). " +
                            $"Template={(particleTemplate != null ? particleTemplate.TemplateId : "(null)")}, Index={i}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SpawnParticleExecutor] Execute failed: {ex}");
            }
            finally
            {
                if (typed.ReleaseSpawnedEmitterAfter && spawnedEmitterScope != null && pool != null)
                {
                    pool.Release(spawnedEmitterScope);
                }
            }
        }

        static Transform? ResolveEmitterParentTransform(
            SpawnParticleCommandData data,
            CommandContext ctx,
            Transform ownerTransform)
        {
            if (data.EmitterParent == SpawnParticleCommandData.EmitterParentMode.CommandRunner)
                return ownerTransform;

            if (data.EmitterParent == SpawnParticleCommandData.EmitterParentMode.Specify)
            {
                var resolved = data.EmitterParentTransform.GetOrDefault(ctx, default(Transform)!);
                if (resolved != null)
                    return resolved;
            }

            return ownerTransform;
        }

        readonly struct SpawnEmitterResult
        {
            public readonly bool Success;
            public readonly IRuntimeResolver? Resolver;
            public readonly RuntimeLifetimeScope? Scope;

            public SpawnEmitterResult(bool success, IRuntimeResolver? resolver, RuntimeLifetimeScope? scope)
            {
                Success = success;
                Resolver = resolver;
                Scope = scope;
            }
        }

        static async UniTask<SpawnEmitterResult> TrySpawnEmitterViaSpawnerAsync(
            CommandContext ctx,
            BaseRuntimeTemplateSO template,
            Vector3 position,
            Quaternion rotation,
            CancellationToken ct)
        {
            if (!ctx.Resolver.TryResolve<ISceneSpawnerRegistry>(out var registry) || registry == null)
                return new SpawnEmitterResult(false, null, null);

            var resolved = SceneSpawnerResolver.TryResolveAsyncSpawner(
                registry,
                SpawnerKind.RuntimeEntity,
                "",
                allowTagFallback: true,
                allowRuntimeUiFallback: false);

            var spawner = resolved.Spawner;
            if (spawner == null)
                return new SpawnEmitterResult(false, null, null);

            var spawnParams = SpawnParams.ForRuntime(
                template,
                position,
                rotation,
                Vector3.one,
                identity: null,
                transformParent: null,
                lifetimeScopeParent: ctx.Scope,
                worldSpace: true,
                allowPooling: true);

            var resolver = await spawner.SpawnAsync(spawnParams, ct);
            if (resolver == null)
                return new SpawnEmitterResult(false, null, null);

            RuntimeLifetimeScope? scope = null;
            if (resolver.TryResolve<RuntimeLifetimeScope>(out var resolvedScope) && resolvedScope != null)
                scope = resolvedScope;

            return new SpawnEmitterResult(true, resolver, scope);
        }

        static void NotifyDirectSpawn(
            IEmitterService emitterService,
            IRuntimeResolver unitResolver,
            SpawnParams spawnParams,
            Vector3 position,
            Quaternion rotation,
            int waveIndex,
            FirePatternRuntimeTemplatePreset? fireTemplatePreset)
        {
            if (emitterService == null || unitResolver == null)
                return;

            var hasConsumer = false;
            if (unitResolver.TryResolve<IReadOnlyList<ISpawnContextConsumer>>(out var consumers) &&
                consumers != null &&
                consumers.Count > 0)
            {
                hasConsumer = true;
            }
            else if (unitResolver.TryResolve<ISpawnContextConsumer>(out var singleConsumer) && singleConsumer != null)
            {
                hasConsumer = true;
            }

            if (!hasConsumer)
            {
                Debug.LogWarning(
                    $"[SpawnParticleExecutor] Spawned FireTemplate has no ISpawnContextConsumer. " +
                    $"TemplateId={(fireTemplatePreset != null ? fireTemplatePreset.TemplateId : "(null)")}. " +
                    "FirePatternServiceMB may be missing on the spawned prefab.");
            }

            var dir = rotation * Vector3.up;
            var delta = position - emitterService.Origin;
            var dist = delta.magnitude;
            var dirFromEmitter = dist > 0f ? (delta / dist) : dir;

            var data = new SpawnData
            {
                Direction = dir,
                Speed = 1f,
                DelayTime = 0f,
                RandomVerticalOffset = 0f,
                RandomHorizontalOffset = 0f,
                CustomFloat0 = 0f,
                CustomFloat1 = 0f
            };

            var context = new SpawnContext(
                index: 0,
                position: position,
                emitterPosition: emitterService.Origin,
                directionFromEmitter: dirFromEmitter,
                distanceFromEmitter: dist,
                tangentDirection: Vector3.right,
                spawnCount: 1,
                data: data,
                spawnParams: spawnParams,
                emitIndex: 0,
                emitCount: 1);

            emitterService.NotifySpawnedUnit(unitResolver, in context, waveIndex);
        }
    }
}
