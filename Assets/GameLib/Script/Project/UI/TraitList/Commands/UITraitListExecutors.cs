#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using Game.Common;
using Game.Trait;
using Game.UI.TraitList;
using Game.Vars.Generated;
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

            if (options?.LayoutRectTransform != null)
                parent = options.LayoutRectTransform;
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

            var dynCtx = new SimpleDynamicContext(ctx.Vars, ctx.Scope);
            if (!typed.TraitDefinition.TryGet(dynCtx, out var definition) || definition == null)
            {
                var detail = DescribeTraitDefinitionResolveFailure(typed, ctx);
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"TraitDefinition could not be resolved. {detail}");
            }

            var holder = await UITraitListCommandExecutorUtility.ResolveHolderAsync(typed.UseBoundHolder, typed.HolderActorSource, typed.HolderKey, ctx, ct);
            if (holder == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "TraitHolderService could not be resolved.");

            if (!holder.TryRegister(definition, out var _))
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "Trait could not be registered.");
        }

        static string DescribeTraitDefinitionResolveFailure(AddTraitToHolderCommandData typed, CommandContext ctx)
        {
            var sourceType = typed.TraitDefinition.SourceTypeName;
            var sourceDebug = typed.TraitDefinition.SourceDebugData;
            var scope = ctx.Scope;
            var scopeInfo = scope == null
                ? "scope=(null)"
                : $"scopeKind={scope.Kind} scopeId={scope.Identity?.Id ?? "(none)"}";

            if (scope?.Resolver == null || !scope.Resolver.TryResolve<IBlackboardService>(out var blackboard) || blackboard == null)
                return $"Source={sourceType}:{sourceDebug} {scopeInfo} blackboard=missing";

            var vars = blackboard.LocalVars;
            var varId = VarIds.GameLib.Base.Trait.Element.definitionAsset;
            if (vars == null || varId == 0)
                return $"Source={sourceType}:{sourceDebug} {scopeInfo} blackboardLocalVars=missing";

            var contains = vars.Contains(varId);
            var kind = contains ? vars.GetVarKind(varId).ToString() : "Missing";
            var managedType = "(null)";
            var variantValue = "(null)";
            if (contains && vars.TryGetManagedRef(varId, out var managed) && managed != null)
                managedType = managed.GetType().FullName ?? managed.GetType().Name;
            if (contains && vars.TryGetVariant(varId, out var variant))
                variantValue = variant.ToString();

            return $"Source={sourceType}:{sourceDebug} {scopeInfo} blackboard.definitionAsset.Contains={contains} Kind={kind} ManagedType={managedType} Variant={variantValue}";
        }
    }

    public sealed class RemoveTraitFromHolderExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.RemoveTraitFromHolder;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not RemoveTraitFromHolderCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "RemoveTraitFromHolderCommandData is required.");

            var holder = await UITraitListCommandExecutorUtility.ResolveHolderAsync(typed.UseBoundHolder, typed.HolderActorSource, typed.HolderKey, ctx, ct);
            if (holder == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "TraitHolderService could not be resolved.");

            var dynCtx = new SimpleDynamicContext(ctx.Vars, ctx.Scope);
            if (!typed.Selector.TryResolve(
                    holder,
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

            var holder = await UITraitListCommandExecutorUtility.ResolveHolderAsync(typed.UseBoundHolder, typed.HolderActorSource, typed.HolderKey, ctx, ct);
            if (holder == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "TraitHolderService could not be resolved.");

            var dynCtx = new SimpleDynamicContext(ctx.Vars, ctx.Scope);
            if (!typed.Selector.TryResolve(
                    holder,
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
            ActorSource holderActorSource,
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

            var holderScope = await ResolveHolderScopeAsync(holderActorSource, ctx, ct);
            if (holderScope == null)
                return null;

            EnsureScopeBuiltIfNeeded(holderScope);
            if (holderScope.Resolver == null ||
                !holderScope.Resolver.TryResolve<ITraitHolderHubService>(out var hub) ||
                hub == null)
                return null;

            if (!hub.TryGetHolder(holderKey, out var holder) || holder == null)
                return null;

            return holder;
        }

        static async UniTask<IScopeNode?> ResolveHolderScopeAsync(ActorSource holderSource, CommandContext ctx, CancellationToken ct)
        {
            var (scope, _) = await ActorScopeResolver.ResolveAsync(holderSource, ctx, ct);
            return scope;
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
