#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using Game.Common;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class WithPlayerExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.WithPlayer;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not WithPlayerCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "WithPlayerCommandData is required.");

            if (typed.Body == null || typed.Body.Count == 0)
                return;

            var (playerScope, error) = await ResolvePlayerScopeAsync(ctx, ct);
            if (playerScope == null)
            {
                if (AllowFallback(ctx.Options))
                {
                    Debug.LogError($"[WithPlayerExecutor] Player resolve failed: {error} Falling back to current scope.");
                    playerScope = ctx.Scope;
                }
                else
                {
                    throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, error);
                }
            }

            if (playerScope == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "Player scope could not be resolved (null).");

            var allowFallback = AllowFallback(ctx.Options);
            foreach (var targetScope in GetExecutionTargets(playerScope, typed.ExecutionScope))
            {
                if (targetScope == null)
                    continue;

                await ExecuteBodyOnScope(typed, ctx, targetScope, playerScope, ct, allowFallback);
            }
        }

        static async UniTask<(IScopeNode? scope, string error)> ResolvePlayerScopeAsync(CommandContext ctx, CancellationToken ct)
        {
            var origin = ctx.Scope;
            if (origin == null)
                return (null, "Current scope is not available.");

            if (!TryResolvePlayerLocator(origin, out var locator) || locator == null)
                return (null, "IPlayerLocationService is not registered.");

            if (locator.TryGetPlayerScope(out var scope) && scope != null)
                return (scope, string.Empty);

            var resolved = await locator.GetPlayerScopeAsync(ct);
            if (resolved != null)
                return (resolved, string.Empty);

            return (null, "Player scope was not found.");
        }

        static bool TryResolvePlayerLocator(IScopeNode scope, out IPlayerLocationService? locator)
        {
            locator = null;
            if (scope == null)
                return false;

            var current = scope;
            while (current != null)
            {
                var resolver = current.Resolver;
                if (resolver != null && resolver.TryResolve<IPlayerLocationService>(out var found) && found != null)
                {
                    locator = found;
                    return true;
                }
                current = current.Parent;
            }
            return false;
        }

        static IEnumerable<IScopeNode> GetExecutionTargets(IScopeNode playerScope, WithActorExecutionScope executionScope)
        {
            return executionScope switch
            {
                WithActorExecutionScope.ActorAndDescendants => ScopeNodeHierarchy.EnumerateSubtree(playerScope, includeSelf: true),
                WithActorExecutionScope.DescendantsOnly => ScopeNodeHierarchy.EnumerateSubtree(playerScope, includeSelf: false),
                _ => new[] { playerScope }
            };
        }

        static async UniTask ExecuteBodyOnScope(
            WithPlayerCommandData typed,
            CommandContext ctx,
            IScopeNode targetScope,
            IScopeNode playerScope,
            CancellationToken ct,
            bool allowFallback)
        {
            var executionScope = targetScope;
            EnsureScopeBuiltIfNeeded(executionScope);

            if (typed.UseDescendantFilter && !DoesScopeMatchFilter(typed.DescendantFilter, executionScope, playerScope))
            {
                return;
            }

            if (!TryResolveRunner(executionScope, out var runner) || runner == null)
            {
                if (allowFallback)
                {
                    Debug.LogError($"[WithPlayerExecutor] Player scope {DescribeScope(executionScope)} has no runner; falling back to current scope.");
                    executionScope = ctx.Scope ?? executionScope;
                    runner = ctx.Runner;
                }
                else
                {
                    throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, $"Player scope {DescribeScope(executionScope)} has no ICommandRunner.");
                }
            }

            if (runner == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "No ICommandRunner available in player or fallback scope.");

            var vars = ResolveVars(typed.VarsPolicy, ctx, executionScope);
            var playerCtx = new CommandContext(
                executionScope,
                vars,
                runner,
                executionScope,
                ctx.Options,
                commandRootScope: ctx.CommandRootScope,
                rootActor: ctx.RootActor,
                callerActor: ctx.Actor);

            try
            {
                var result = await runner.ExecuteListAsync(typed.Body, playerCtx, ct, ctx.Options);
                if (result.Status == CommandRunStatus.Canceled)
                    throw new OperationCanceledException();

                if (result.Status == CommandRunStatus.Error || result.FailureCount > 0)
                {
                    var msg = $"Player command list failed for scope {DescribeScope(executionScope)}. FailureCount={result.FailureCount}, ErrorIndex={result.ErrorIndex}, Message={result.Message}";
                    throw new CommandExecutionException(result.FailureKind, msg);
                }
            }
            catch (OperationCanceledException)
            {
                Debug.Log($"[WithPlayerExecutor] Player command list execution was canceled for scope {DescribeScope(executionScope)}.");
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogException(new Exception($"[WithPlayerExecutor] Exception executing player commands. PlayerScope={DescribeScope(executionScope)}; Runner={(runner == null ? "null" : runner.GetType().Name)}", ex));
                throw;
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

        static bool DoesScopeMatchFilter(ActorSource filter, IScopeNode scope, IScopeNode playerScope)
        {
            switch (filter.Kind)
            {
                case ActorSourceKind.Current:
                    return ReferenceEquals(scope, playerScope);
                case ActorSourceKind.Parent:
                    return ReferenceEquals(scope, playerScope?.Parent);
                case ActorSourceKind.Root:
                    var path = playerScope?.GetPathFromRoot();
                    if (path == null || path.Count == 0)
                        return false;
                    return ReferenceEquals(scope, path[0]);
                case ActorSourceKind.GameLogicRoot:
                    var logicRoot = ScopeNodeHierarchy.FindNearestGameLogicRoot(playerScope, includeSelf: true);
                    return logicRoot != null && ReferenceEquals(scope, logicRoot);
                case ActorSourceKind.Player:
                    {
                        var resolvedPlayerScope = ActorSourceFastResolver.Resolve(playerScope, filter);
                        return resolvedPlayerScope != null && ReferenceEquals(scope, resolvedPlayerScope);
                    }
                case ActorSourceKind.ByIdentity:
                    return MatchesIdentity(scope, filter.Identity);
                case ActorSourceKind.FromUnityObject:
                    if (filter.UnityObject == null)
                        return false;
                    if (TryResolveFromUnityObject(filter.UnityObject, out var target))
                        return ReferenceEquals(scope, target);
                    return false;
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

        static IVarStore ResolveVars(VarsPolicy policy, CommandContext ctx, IScopeNode playerScope)
        {
            if (policy == VarsPolicy.UseActorScopeVars)
            {
                var resolver = playerScope?.Resolver;
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

        static string DescribeScope(IScopeNode scope)
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

        static bool TryResolveFromUnityObject(UnityEngine.Object obj, out IScopeNode? scope)
        {
            scope = null;
            // TODO: Implement resolution logic for UnityObject to IScopeNode
            return false;
        }
    }
}
