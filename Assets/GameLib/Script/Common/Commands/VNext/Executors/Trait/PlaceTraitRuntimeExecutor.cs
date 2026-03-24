#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using Game.Trait;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class PlaceTraitRuntimeExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.PlaceTraitRuntime;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not PlaceTraitRuntimeCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "PlaceTraitRuntimeCommandData is required.");

            var (holderScope, holderError) = await ActorScopeResolver.ResolveAsync(typed.HolderActorSource, ctx, ct);
            if (holderScope == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, holderError ?? "Holder scope could not be resolved.");

            EnsureScopeBuiltIfNeeded(holderScope);
            if (holderScope.Resolver == null ||
                !holderScope.Resolver.TryResolve<ITraitPlacementService>(out var placementService) ||
                placementService == null)
            {
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "TraitPlacementService was not found on holder scope.");
            }

            if (!holderScope.Resolver.TryResolve<ITraitHolderHubService>(out var holderHub) || holderHub == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "TraitHolderHubService was not found on holder scope.");

            if (!holderHub.TryGetPlacementSettings(typed.HolderKey, out var placementSettings) || placementSettings == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"Placement defaults were not found for holder '{typed.HolderKey}'.");

            var dynamicContext = new SimpleDynamicContext(ctx.Vars, ctx.Scope);
            if (!holderHub.TryGetHolder(typed.HolderKey, out var holder) || holder == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"Trait holder '{typed.HolderKey}' was not found.");

            if (!typed.Selector.TryResolve(holder, dynamicContext, out var traitInstance, out var selectorError) || traitInstance == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, string.IsNullOrEmpty(selectorError) ? "Trait selector could not be resolved." : selectorError);

            if (!placementSettings.TryResolvePosition(dynamicContext, out var position))
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"Placement position could not be resolved for holder '{typed.HolderKey}'.");

            if (!placementSettings.TryResolveRotationEuler(dynamicContext, out var rotationEuler))
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"Placement rotation could not be resolved for holder '{typed.HolderKey}'.");

            if (!placementSettings.TryResolveScale(dynamicContext, out var scale))
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"Placement scale could not be resolved for holder '{typed.HolderKey}'.");

            if (typed.OverridePosition)
            {
                if (!typed.Position.TryGet(dynamicContext, out position))
                    throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "Override position could not be resolved.");
            }

            if (typed.OverrideRotation)
            {
                if (!typed.RotationEuler.TryGet(dynamicContext, out rotationEuler))
                    throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "Override rotation could not be resolved.");
            }

            if (typed.OverrideScale)
            {
                if (!typed.Scale.TryGet(dynamicContext, out scale))
                    throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "Override scale could not be resolved.");
            }

            var useParent = placementSettings.UseParent;
            var parentSource = placementSettings.ParentActorSource;
            if (typed.OverrideParent)
            {
                useParent = typed.UseParent;
                parentSource = typed.ParentActorSource;
            }

            Transform? transformParent = null;
            if (useParent)
            {
                transformParent = await ResolveTransformParentFromActorSourceAsync(parentSource, ctx, ct);
                if (transformParent == null)
                    throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "Placement parent could not be resolved.");
            }

            var runtime = await placementService.PlaceAsync(
                typed.HolderKey,
                typed.Selector,
                dynamicContext,
                position,
                Quaternion.Euler(rotationEuler),
                scale,
                transformParent,
                ct);
            if (runtime == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "Trait runtime could not be placed.");

            if (placementSettings.RunOnPlacedCommands && placementSettings.OnPlacedCommands != null && placementSettings.OnPlacedCommands.Count > 0)
                await ExecuteOnPlacedCommandsAsync(holderScope, runtime, traitInstance, ctx, placementSettings.OnPlacedCommands, ct);
        }

        static void EnsureScopeBuiltIfNeeded(IScopeNode scope)
        {
            if (scope is BaseLifetimeScope baseScope)
            {
                baseScope.EnsureScopeBuilt();
                return;
            }

            if (scope is RuntimeLifetimeScope runtimeScope)
                runtimeScope.EnsureScopeBuilt();
        }

        static async UniTask<Transform?> ResolveTransformParentFromActorSourceAsync(ActorSource source, CommandContext ctx, CancellationToken ct)
        {
            var (actorScope, error) = await ActorScopeResolver.ResolveAsync(source, ctx, ct);
            if (actorScope == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"Placement parent actor resolve failed: {error}");

            var transform = actorScope.Identity?.SelfTransform;
            if (transform == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "Resolved placement parent scope does not expose a Transform.");

            return transform;
        }

        static async UniTask ExecuteOnPlacedCommandsAsync(
            IScopeNode ownerScope,
            RuntimeLifetimeScope runtimeScope,
            ITraitInstance traitInstance,
            CommandContext sourceContext,
            CommandListData commands,
            CancellationToken ct)
        {
            var runner = sourceContext.Runner;
            if (runner == null)
                return;

            var vars = new VarStore();
            sourceContext.Vars?.MergeInto(vars, overwrite: true);
            traitInstance?.Context?.Vars?.MergeInto(vars, overwrite: true);
            if (runtimeScope.Resolver != null &&
                runtimeScope.Resolver.TryResolve<IBlackboardService>(out var blackboard) &&
                blackboard != null)
            {
                blackboard.MergeInto(vars, overwrite: true);
            }

            var placedCtx = new CommandContext(
                ownerScope,
                vars,
                runner,
                runtimeScope,
                sourceContext.Options,
                ownerScope,
                runtimeScope,
                runtimeScope,
                sourceContext);

            await runner.ExecuteListAsync(commands, placedCtx, ct, placedCtx.Options);
        }
    }
}
