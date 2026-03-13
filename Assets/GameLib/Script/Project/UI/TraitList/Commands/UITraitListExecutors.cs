#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using Game.Common;
using Game.Trait;
using Game.UI.TraitList;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Commands.VNext
{
    public sealed class BuildUITraitListExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.BuildUITraitList;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not BuildUITraitListCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "BuildUITraitListCommandData is required.");

            if (!typed.ProfileSource.TryResolve(ctx.Vars, out var profile) || profile == null)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "Profile is null (and could not be resolved from vars).");

            var svc = UITraitListCommandExecutorUtility.ResolvePlayerServiceOrThrow(ctx, out var svcScope, out var options);

            var (hubScope, hubError) = await ActorScopeResolver.ResolveAsync(typed.HolderHubSource, ctx, ct);
            if (hubScope == null)
            {
                var reason = string.IsNullOrEmpty(hubError) ? "HolderHubSource could not be resolved." : hubError;
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, reason);
            }

            UITraitListCommandExecutorUtility.EnsureScopeBuiltIfNeeded(hubScope);
            if (hubScope.Resolver == null ||
                !hubScope.Resolver.TryResolve<ITraitHolderHubService>(out var hub) ||
                hub == null)
            {
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "TraitHolderHubService was not found.");
            }

            if (!hub.TryGetHolder(typed.HolderKey, out var holder) || holder == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"Holder '{typed.HolderKey}' not found.");

            IScopeNode? buildScope = null;
            Transform? parent = null;
            var (resolvedBuildScope, _) = await ActorScopeResolver.ResolveAsync(typed.BuildScope, ctx, ct);
            if (resolvedBuildScope != null)
            {
                buildScope = resolvedBuildScope;
                UITraitListCommandExecutorUtility.EnsureScopeBuiltIfNeeded(buildScope);
                parent = (buildScope as Component)?.transform;
            }

            if (parent == null && options != null)
                parent = options.DefaultParentTransform;
            if (parent == null && svcScope is Component svcComponent)
                parent = svcComponent.transform;
            if (parent == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "Parent transform is null.");

            var scopeParent = buildScope ?? svcScope ?? ctx.Scope;
            if (scopeParent == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "Scope parent is null.");

            try
            {
                await svc.BuildAsync(profile, holder, typed.HolderKey, typed.Range, parent, scopeParent, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                throw;
            }
        }
    }

    public sealed class RefreshUITraitListExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.RefreshUITraitList;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not RefreshUITraitListCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "RefreshUITraitListCommandData is required.");

            var svc = UITraitListCommandExecutorUtility.ResolvePlayerServiceOrThrow(ctx, out _, out _);
            await svc.RefreshAsync(typed.RefreshMode, ct);
        }
    }

    public sealed class SetUITraitListRangeExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.SetUITraitListRange;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not SetUITraitListRangeCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "SetUITraitListRangeCommandData is required.");

            var svc = UITraitListCommandExecutorUtility.ResolvePlayerServiceOrThrow(ctx, out _, out _);
            await svc.SetRangeAsync(typed.Range, typed.Rebuild, ct);
        }
    }

    public sealed class ClearUITraitListExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.ClearUITraitList;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not ClearUITraitListCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "ClearUITraitListCommandData is required.");

            var svc = UITraitListCommandExecutorUtility.ResolvePlayerServiceOrThrow(ctx, out _, out _);
            await svc.ClearAsync(typed.KeepBinding, ct);
        }
    }

    public sealed class AddTraitToHolderExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.AddTraitToHolder;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not AddTraitToHolderCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "AddTraitToHolderCommandData is required.");

            if (!typed.TraitDefinitionSource.TryResolve(ctx.Vars, out var definition) || definition == null)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "TraitDefinition could not be resolved.");

            var holder = await UITraitListCommandExecutorUtility.ResolveHolderAsync(typed.UseBoundHolder, typed.HolderHubSource, typed.HolderKey, ctx, ct);
            if (holder == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "TraitHolderService could not be resolved.");

            if (!holder.TryRegister(definition, out var _))
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "Trait could not be registered.");
        }
    }

    public sealed class RemoveTraitFromHolderExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.RemoveTraitFromHolder;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not RemoveTraitFromHolderCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "RemoveTraitFromHolderCommandData is required.");

            var holder = await UITraitListCommandExecutorUtility.ResolveHolderAsync(typed.UseBoundHolder, typed.HolderHubSource, typed.HolderKey, ctx, ct);
            if (holder == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "TraitHolderService could not be resolved.");

            var dynCtx = new SimpleDynamicContext(ctx.Vars, ctx.Scope);
            if (!UITraitListCommandExecutorUtility.TryResolveTargetInstance(
                    typed.Target,
                    holder,
                    UITraitListCommandExecutorUtility.TryResolvePlayerService(ctx),
                    dynCtx,
                    out var instance,
                    out var error))
            {
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, error ?? "Trait target could not be resolved.");
            }

            if (!holder.TryRemove(instance))
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "Trait could not be removed.");
        }
    }

    public sealed class UseTraitFromHolderExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.UseTraitFromHolder;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not UseTraitFromHolderCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "UseTraitFromHolderCommandData is required.");

            var holder = await UITraitListCommandExecutorUtility.ResolveHolderAsync(typed.UseBoundHolder, typed.HolderHubSource, typed.HolderKey, ctx, ct);
            if (holder == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "TraitHolderService could not be resolved.");

            var dynCtx = new SimpleDynamicContext(ctx.Vars, ctx.Scope);
            if (!UITraitListCommandExecutorUtility.TryResolveTargetInstance(
                    typed.Target,
                    holder,
                    UITraitListCommandExecutorUtility.TryResolvePlayerService(ctx),
                    dynCtx,
                    out var instance,
                    out var error))
            {
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, error ?? "Trait target could not be resolved.");
            }

            if (!holder.TryUse(instance))
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "Trait could not be used.");
        }
    }

    static class UITraitListCommandExecutorUtility
    {
        public static IUITraitListPlayerService ResolvePlayerServiceOrThrow(
            CommandContext ctx,
            out IScopeNode ownerScope,
            out IUITraitListSystemOptions? options)
        {
            if (ctx == null)
                throw new CommandExecutionException(CommandRunFailureKind.Exception, "CommandContext is null.");

            var origin = ctx.Scope;
            if (origin == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "Scope is null.");

            var candidates = new List<LifetimeScopeKind>
            {
                LifetimeScopeKind.UIElement,
                LifetimeScopeKind.UI,
                LifetimeScopeKind.Scene,
                LifetimeScopeKind.Field,
                LifetimeScopeKind.Project
            };

            foreach (var kind in candidates)
            {
                var node = ScopeNodeHierarchy.FindNearestAncestorByKind(origin, kind, includeSelf: true);
                if (node == null)
                    continue;
                var resolver = node.Resolver;
                if (resolver == null)
                    continue;
                if (resolver.TryResolve<IUITraitListPlayerService>(out var svc) && svc != null)
                {
                    ownerScope = node;
                    options = resolver.TryResolve<IUITraitListSystemOptions>(out var resolved) ? resolved : null;
                    return svc;
                }
            }

            var originResolver = origin.Resolver;
            if (originResolver != null &&
                originResolver.TryResolve<IUITraitListPlayerService>(out var originSvc) &&
                originSvc != null)
            {
                ownerScope = origin;
                options = originResolver.TryResolve<IUITraitListSystemOptions>(out var resolved) ? resolved : null;
                return originSvc;
            }

            throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed,
                "IUITraitListPlayerService is not registered in the nearest UI/UIElement/Scene/Field/Project scope. Add UITraitListSystemMB to the appropriate scope.");
        }

        public static IUITraitListPlayerService? TryResolvePlayerService(CommandContext ctx)
        {
            try
            {
                return ResolvePlayerServiceOrThrow(ctx, out _, out _);
            }
            catch
            {
                return null;
            }
        }

        public static async UniTask<ITraitHolderService?> ResolveHolderAsync(
            bool useBoundHolder,
            ActorSource holderHubSource,
            string holderKey,
            CommandContext ctx,
            CancellationToken ct)
        {
            if (useBoundHolder)
            {
                var svc = TryResolvePlayerService(ctx);
                if (svc != null && svc.BoundHolder != null)
                    return svc.BoundHolder;
                return null;
            }

            var (hubScope, _) = await ActorScopeResolver.ResolveAsync(holderHubSource, ctx, ct);
            if (hubScope == null)
                return null;

            EnsureScopeBuiltIfNeeded(hubScope);
            if (hubScope.Resolver == null ||
                !hubScope.Resolver.TryResolve<ITraitHolderHubService>(out var hub) ||
                hub == null)
                return null;

            if (!hub.TryGetHolder(holderKey, out var holder) || holder == null)
                return null;

            return holder;
        }

        public static bool TryResolveTargetInstance(
            UITraitTarget target,
            ITraitHolderService holder,
            IUITraitListPlayerService? player,
            IDynamicContext dynCtx,
            out ITraitInstance? instance,
            out string? error)
        {
            instance = null;
            error = null;
            if (holder == null)
            {
                error = "Holder is null.";
                return false;
            }

            switch (target.Kind)
            {
                case UITraitTargetKind.ByDefinition:
                    if (!target.DefinitionSource.TryResolve(dynCtx.Vars, out var definition) || definition == null)
                    {
                        error = "TraitDefinition could not be resolved.";
                        return false;
                    }

                    if (!holder.TryGetInstance(definition, out instance) || instance == null)
                    {
                        error = "Trait instance not found for definition.";
                        return false;
                    }

                    return true;

                case UITraitTargetKind.ByInstanceId:
                    if (string.IsNullOrEmpty(target.InstanceId))
                    {
                        error = "InstanceId is empty.";
                        return false;
                    }

                    var traits = holder.Traits;
                    for (int i = 0; i < traits.Count; i++)
                    {
                        var trait = traits[i];
                        if (trait != null && trait.InstanceId == target.InstanceId)
                        {
                            instance = trait;
                            return true;
                        }
                    }

                    error = "Trait instance not found by InstanceId.";
                    return false;

                case UITraitTargetKind.ByIndex:
                    if (!target.TraitIndex.TryGet(dynCtx, out var index))
                    {
                        error = "TraitIndex could not be resolved.";
                        return false;
                    }

                    if (index < 0 || index >= holder.Traits.Count)
                    {
                        error = "TraitIndex is out of range.";
                        return false;
                    }

                    instance = holder.Traits[index];
                    return instance != null;

                case UITraitTargetKind.ByRowAndColumn:
                    if (player == null)
                    {
                        error = "Player service is not available for row/column resolution.";
                        return false;
                    }

                    if (!target.Row.TryGet(dynCtx, out var row) || !target.Column.TryGet(dynCtx, out var column))
                    {
                        error = "Row/Column could not be resolved.";
                        return false;
                    }

                    if (!player.TryResolveInstanceByRowColumn(row, column, out instance) || instance == null)
                    {
                        error = "Trait instance not found by row/column.";
                        return false;
                    }

                    return true;
            }

            error = "Unknown target kind.";
            return false;
        }

        public static void EnsureScopeBuiltIfNeeded(IScopeNode scope)
        {
            if (scope is BaseLifetimeScope baseScope)
            {
                baseScope.EnsureScopeBuilt();
                return;
            }

            if (scope is RuntimeLifetimeScope runtimeScope)
            {
                runtimeScope.EnsureScopeBuilt();
            }
        }
    }
}
