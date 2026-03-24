#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using Game.DI;
using Game.Spawn;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class SpawnRuntimeTemplateExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.SpawnRuntimeTemplate;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not SpawnRuntimeTemplateCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "SpawnRuntimeTemplateCommandData is required.");

            if (!typed.Template.TryGet(ctx, out var templatePreset) || templatePreset == null)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "Template preset is null.");

            var runtimeTemplate = RuntimeTemplatePresetResolver.ResolveTemplateSO(templatePreset);
            if (runtimeTemplate == null)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "Template preset could not resolve runtime template.");

            var originResolver = ctx.Scope?.Resolver;
            if (originResolver == null || !originResolver.TryResolve<ISceneSpawnerRegistry>(out var registry) || registry == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "ISceneSpawnerRegistry is not available in current scope.");

            var allowTagFallback = string.IsNullOrEmpty(typed.SpawnerTag);
            var resolved = SceneSpawnerResolver.TryResolveAsyncSpawner(
                registry,
                typed.SpawnerKind,
                typed.SpawnerTag,
                allowTagFallback,
                allowRuntimeUiFallback: true);
            var spawner = resolved.Spawner;
            if (spawner == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"Spawner not found. Kind={typed.SpawnerKind} Tag={typed.SpawnerTag}");

            IScopeNode? lifetimeScopeParent = null;
            if (typed.OverrideLifetimeScopeParent)
            {
                var (parentResolved, error) = await ActorScopeResolver.ResolveAsync(typed.LifetimeScopeParent, ctx, ct);
                if (parentResolved == null)
                    throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"LifetimeScopeParent resolve failed: {error}");

                lifetimeScopeParent = parentResolved;
            }

            Transform? transformParent = null;
            if (typed.TransformParentPolicy == SpawnTransformParentPolicy.UseTransform)
            {
                transformParent = typed.TransformParent;
            }
            else if (typed.TransformParentPolicy == SpawnTransformParentPolicy.ActorSource)
            {
                transformParent = await ResolveTransformParentFromActorSourceAsync(typed.TransformParentActorSource, ctx, ct);
            }

            var dynamicVars = ctx.Vars ?? NullVarStore.Instance;
            CommandResolveContext CreateDynamicContext() => new(
                ctx.Scope!,
                dynamicVars,
                ctx.CommandRootScope,
                ctx.Resolver,
                NullCommandCatalog.Instance,
                NullCommandKeyResolver.Instance,
                NullCommandResolveLogger.Instance,
                allowRuntimeKeyFallback: ctx.Options.AllowRuntimeKeyFallback,
                runtimeContext: ctx);

            var targetCount = typed.Count.Resolve(CreateDynamicContext());
            var spawnCount = Mathf.Max(1, targetCount);
            var onSpawnedCommands = typed.OnSpawnedCommands;
            var shouldRunOnSpawnedCommands = typed.RunCommandsOnSpawned && onSpawnedCommands != null && onSpawnedCommands.Count > 0;

            for (int i = 0; i < spawnCount; i++)
            {
                ct.ThrowIfCancellationRequested();
                var dynCtx = CreateDynamicContext();
                var basePos = typed.Position.Resolve(dynCtx);
                var offset = typed.Offset.Resolve(dynCtx);
                var spawnPos = basePos + offset;

                var rotation = Quaternion.Euler(typed.RotationEuler.Resolve(dynCtx));
                var scale = typed.Scale.HasSource ? typed.Scale.Resolve(dynCtx) : Vector3.one;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log(
                    $"[SpawnRuntimeTemplateExecutor] Resolve idx={i}/{spawnCount - 1} " +
                    $"worldSpace={typed.WorldSpace} parent={DescribeTransform(transformParent)} " +
                    $"pos={basePos} offset={offset} finalPos={spawnPos} rot={rotation.eulerAngles} scale={scale} " +
                    $"posSrc={typed.Position.SourceTypeName}:{typed.Position.SourceDebugData} " +
                    $"offsetSrc={typed.Offset.SourceTypeName}:{typed.Offset.SourceDebugData}");
#endif

                var spawnParams = SpawnParams.ForRuntime(
                    runtimeTemplate,
                    spawnPos,
                    rotation,
                    scale,
                    identity: null,
                    transformParent: transformParent,
                    lifetimeScopeParent: lifetimeScopeParent,
                    worldSpace: typed.WorldSpace,
                    allowPooling: typed.AllowPooling);

                var spawnedResolver = await spawner.SpawnAsync(spawnParams, ct);
                if (spawnedResolver != null && shouldRunOnSpawnedCommands)
                {
                    if (!spawnedResolver.TryResolve<IScopeNode>(out var spawnedScope) || spawnedScope == null)
                        throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "Spawned container does not expose IScopeNode.");

                    EnsureScopeBuiltIfNeeded(spawnedScope);

                    if (!TryResolveRunner(spawnedScope, out var runner) || runner == null)
                        throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "Spawned scope has no ICommandRunner.");

                    var vars = ResolveVars(typed.VarsPolicy, ctx, spawnedScope);
                    var spawnedOptions = ctx.Options.WithSuppressCancelLog(true);
                    var spawnedCtx = new CommandContext(
                        spawnedScope,
                        vars,
                        runner,
                        actor: spawnedScope,
                        options: spawnedOptions,
                        commandRootScope: ctx.CommandRootScope,
                        rootActor: ctx.RootActor,
                        callerActor: ctx.Actor,
                        sourceContext: ctx);

                    if (typed.AwaitOnSpawnedCommands)
                    {
                        var result = await runner.ExecuteListAsync(onSpawnedCommands!, spawnedCtx, ct, spawnedOptions);
                        if (result.Status == CommandRunStatus.Canceled)
                            throw new OperationCanceledException();

                        if (result.Status == CommandRunStatus.Error || result.FailureCount > 0)
                        {
                            var msg = $"OnSpawned command list failed. FailureCount={result.FailureCount}, ErrorIndex={result.ErrorIndex}, Message={result.Message}";
                            throw new CommandExecutionException(result.FailureKind, msg);
                        }
                    }
                    else
                    {
                        RunOnSpawnedInBackground(runner, onSpawnedCommands!, spawnedCtx, ct, typed.SpawnerTag);
                    }
                }

                if (typed.DelayBetweenSpawns.HasSource && i < spawnCount - 1)
                {
                    var delay = typed.DelayBetweenSpawns.Resolve(dynCtx);
                    var delaySeconds = Mathf.Max(0f, delay);
                    if (delaySeconds > 0f)
                        await UniTask.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken: ct);
                }
            }
        }

        static void EnsureScopeBuiltIfNeeded(IScopeNode scope)
        {
            if (scope is BaseLifetimeScope baseScope)
            {
                baseScope.EnsureScopeBuilt();
                return;
            }

            if (scope is RuntimeLifetimeScope runtimeScope)
            {
                runtimeScope.EnsureScopeBuilt();
                return;
            }
        }

        static bool TryResolveRunner(IScopeNode scope, out ICommandRunner? runner)
        {
            runner = null;
            var resolver = scope?.Resolver;
            if (resolver == null)
                return false;

            return resolver.TryResolve(out runner) && runner != null;
        }

        static IVarStore ResolveVars(VarsPolicy policy, CommandContext ctx, IScopeNode targetScope)
        {
            if (policy == VarsPolicy.UseActorScopeVars)
            {
                var resolver = targetScope?.Resolver;
                if (resolver != null && resolver.TryResolve<IVarStore>(out var vars) && vars != null)
                    return vars;
                return NullVarStore.Instance;
            }

            return ctx.Vars ?? NullVarStore.Instance;
        }

        static async UniTask<Transform?> ResolveTransformParentFromActorSourceAsync(ActorSource source, CommandContext ctx, CancellationToken ct)
        {
            var (actorScope, error) = await ActorScopeResolver.ResolveAsync(source, ctx, ct);
            if (actorScope == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"Transform parent actor resolve failed: {error}");

            var transform = GetTransformFromScope(actorScope);
            if (transform == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "Resolved actor scope does not expose a Transform.");

            return transform;
        }

        static Transform? GetTransformFromScope(IScopeNode scope)
        {
            // Prefer Identity.SelfTransform when available
            try
            {
                var id = scope?.Identity;
                if (id != null && id.SelfTransform != null)
                    return id.SelfTransform;
            }
            catch
            {
                // ignore and fall back
                return null;
            }

            return null;
        }

        static void RunOnSpawnedInBackground(
            ICommandRunner runner,
            CommandListData list,
            CommandContext ctx,
            CancellationToken ct,
            string spawnerTag)
        {
            UniTask.Void(async () =>
            {
                try
                {
                    var options = ctx.Options.WithSuppressCancelLog(true);
                    var result = await runner.ExecuteListAsync(list, ctx, ct, options);
                    if (result.Status == CommandRunStatus.Canceled)
                        return;

                    if (result.Status == CommandRunStatus.Error || result.FailureCount > 0)
                    {
                        Debug.LogError($"[SpawnRuntimeTemplateExecutor] OnSpawned command list failed (background). SpawnerTag={spawnerTag} FailureCount={result.FailureCount} ErrorIndex={result.ErrorIndex} Message={result.Message}");
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    Debug.LogException(new Exception($"[SpawnRuntimeTemplateExecutor] OnSpawned command list exception (background). SpawnerTag={spawnerTag}", ex));
                }
            });
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static string DescribeTransform(Transform? t)
        {
            if (t == null)
                return "<null>";

            return $"{t.name} pos={t.position} local={t.localPosition}";
        }
#endif
    }
}
