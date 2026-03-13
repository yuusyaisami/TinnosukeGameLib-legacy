#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class WithActorDescendantRouterExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.WithActorDescendantRouter;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not WithActorDescendantRouterCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "WithActorDescendantRouterCommandData is required.");

            if (!typed.HasAnyCommands())
                return;

            var (actorScope, error) = await ActorScopeResolver.ResolveAsync(typed.ActorSource, ctx, ct);
            if (actorScope == null)
            {
                if (AllowFallback(ctx.Options))
                {
                    Debug.LogError($"[WithActorDescendantRouterExecutor] Actor resolve failed: {error} Falling back to current scope.");
                    actorScope = ctx.Scope;
                }
                else
                {
                    throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, error);
                }
            }

            if (actorScope == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "Actor scope could not be resolved (null).");

            var allowFallback = AllowFallback(ctx.Options);
            foreach (var targetScope in GetExecutionTargets(actorScope, typed.ExecutionScope))
            {
                if (targetScope == null)
                    continue;

                await ExecuteOnTargetAsync(typed, ctx, actorScope, targetScope, ct, allowFallback);
            }
        }

        static IEnumerable<IScopeNode> GetExecutionTargets(IScopeNode actorScope, WithActorExecutionScope executionScope)
        {
            return executionScope switch
            {
                WithActorExecutionScope.ActorAndDescendants => ScopeNodeHierarchy.EnumerateSubtree(actorScope, includeSelf: true),
                WithActorExecutionScope.DescendantsOnly => ScopeNodeHierarchy.EnumerateSubtree(actorScope, includeSelf: false),
                _ => new[] { actorScope }
            };
        }

        static async UniTask ExecuteOnTargetAsync(
            WithActorDescendantRouterCommandData typed,
            CommandContext ctx,
            IScopeNode actorScope,
            IScopeNode targetScope,
            CancellationToken ct,
            bool allowFallback)
        {
            var executionScope = targetScope;
            EnsureScopeBuiltIfNeeded(executionScope);

            if (!TryResolveRunner(executionScope, out var runner) || runner == null)
            {
                if (allowFallback)
                {
                    Debug.LogError($"[WithActorDescendantRouterExecutor] Target scope {DescribeScope(executionScope)} has no runner; falling back to current scope.");
                    executionScope = ctx.Scope ?? executionScope;
                    runner = ctx.Runner;
                }
                else
                {
                    throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing,
                        $"Target scope {DescribeScope(executionScope)} has no ICommandRunner.");
                }
            }

            if (runner == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "No ICommandRunner available in target or fallback scope.");

            var vars = ResolveVars(typed.VarsPolicy, ctx, executionScope);
            var targetCtx = new CommandContext(
                executionScope,
                vars,
                runner,
                executionScope,
                ctx.Options,
                commandRootScope: ctx.CommandRootScope,
                rootActor: ctx.RootActor,
                callerActor: ctx.Actor);

            var isMatch = DoesScopeMatchFilter(typed.DescendantFilter, targetScope, actorScope);
            if (typed.FilterMode == DescendantFilterMode.Exclude)
                isMatch = !isMatch;

            await ExecuteListIfAnyAsync(typed.Common, targetCtx, ct, "Common");
            if (isMatch)
                await ExecuteListIfAnyAsync(typed.OnMatched, targetCtx, ct, "OnMatched");
            else
                await ExecuteListIfAnyAsync(typed.OnUnmatched, targetCtx, ct, "OnUnmatched");
        }

        static async UniTask ExecuteListIfAnyAsync(CommandListData? list, CommandContext ctx, CancellationToken ct, string label)
        {
            if (list == null || list.Count == 0)
                return;

            var runner = ctx.Runner;
            if (runner == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "ICommandRunner is null.");

            var result = await runner.ExecuteListAsync(list, ctx, ct, ctx.Options);
            if (result.Status == CommandRunStatus.Canceled)
                throw new OperationCanceledException();

            if (result.Status == CommandRunStatus.Error || result.FailureCount > 0)
            {
                var msg = $"{label} command list failed for scope {DescribeScope(ctx.Scope)}. FailureCount={result.FailureCount}, ErrorIndex={result.ErrorIndex}, Message={result.Message}";
                throw new CommandExecutionException(result.FailureKind, msg);
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

        static bool DoesScopeMatchFilter(ActorSource filter, IScopeNode scope, IScopeNode actorScope)
        {
            switch (filter.Kind)
            {
                case ActorSourceKind.Current:
                    return ReferenceEquals(scope, actorScope);
                case ActorSourceKind.GameLogicRoot:
                    {
                        var logicRoot = ScopeNodeHierarchy.FindNearestGameLogicRoot(actorScope, includeSelf: true);
                        return logicRoot != null && ReferenceEquals(scope, logicRoot);
                    }
                case ActorSourceKind.Player:
                    {
                        var playerScope = ActorSourceFastResolver.Resolve(actorScope, filter);
                        return playerScope != null && ReferenceEquals(scope, playerScope);
                    }
                case ActorSourceKind.Global:
                    {
                        var globalScope = ActorSourceFastResolver.Resolve(actorScope, filter);
                        return globalScope != null && ReferenceEquals(scope, globalScope);
                    }
                case ActorSourceKind.ByIdentity:
                    return MatchesIdentity(scope, filter.Identity);
                case ActorSourceKind.FromUnityObject:
                    {
                        if (filter.UnityObject == null)
                            return false;
                        if (TryResolveFromUnityObject(filter.UnityObject, out var target) && target != null)
                            return ReferenceEquals(scope, target);
                        return false;
                    }
                default:
                    return false;
            }
        }

        static bool MatchesIdentity(IScopeNode scope, CommandTargetIdentityFilter filter)
        {
            var identity = scope.Identity;
            if (identity == null)
                return false;
            if (filter.requireActive && !identity.IsActive)
                return false;
            if (filter.kind != LifetimeScopeKind.None && filter.kind != identity.Kind)
                return false;
            if (!string.IsNullOrEmpty(filter.id))
            {
                if (string.IsNullOrEmpty(identity.Id))
                    return false;
                if (!string.Equals(filter.id, identity.Id, StringComparison.Ordinal))
                    return false;
            }
            if (!string.IsNullOrEmpty(filter.category))
            {
                if (string.IsNullOrEmpty(identity.Category))
                    return false;
                if (!string.Equals(filter.category, identity.Category, StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        static bool TryResolveFromUnityObject(UnityEngine.Object obj, out IScopeNode? scope)
        {
            scope = null;
            if (obj == null)
                return false;

            if (obj is IScopeNode node)
            {
                scope = node;
                return true;
            }

            if (obj is Component comp)
            {
                scope = FindScopeNode(comp.gameObject);
                return scope != null;
            }

            if (obj is GameObject go)
            {
                scope = FindScopeNode(go);
                return scope != null;
            }

            return false;
        }

        static IScopeNode? FindScopeNode(GameObject go)
        {
            if (go == null)
                return null;

            var baseScope = go.GetComponentInParent<BaseLifetimeScope>();
            if (baseScope != null)
                return baseScope;

            var runtimeScope = go.GetComponentInParent<RuntimeLifetimeScope>();
            if (runtimeScope != null)
                return runtimeScope;

            return null;
        }

        static IVarStore ResolveVars(VarsPolicy policy, CommandContext ctx, IScopeNode scope)
        {
            if (policy == VarsPolicy.UseActorScopeVars)
            {
                var resolver = scope.Resolver;
                if (resolver != null && resolver.TryResolve<IVarStore>(out var vars) && vars != null)
                    return vars;
                return NullVarStore.Instance;
            }

            return ctx.Vars ?? NullVarStore.Instance;
        }

        static bool AllowFallback(CommandRunOptions options)
        {
            if (!options.AllowActorFallback)
                return false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return true;
#else
            return Debug.isDebugBuild;
#endif
        }

        static string DescribeScope(IScopeNode? scope)
        {
            if (scope == null)
                return "null";
            try
            {
                var id = scope.Identity?.Id ?? "(no id)";
                return $"{scope.Kind}:{id}";
            }
            catch
            {
                return scope.Kind.ToString();
            }
        }
    }
}
