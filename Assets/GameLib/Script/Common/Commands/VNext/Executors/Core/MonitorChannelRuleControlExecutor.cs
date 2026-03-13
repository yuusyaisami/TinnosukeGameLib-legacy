#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Commands;
using Game.Common;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class MonitorChannelRuleControlExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.MonitorChannelRuleControl;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not MonitorChannelRuleControlCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "MonitorChannelRuleControlCommandData is required.");

            ct.ThrowIfCancellationRequested();

            var (targetScope, error) = await ActorScopeResolver.ResolveAsync(typed.Target, ctx, ct);
            if (targetScope == null)
            {
                if (AllowFallback(ctx.Options))
                {
                    Debug.LogWarning($"[MonitorChannelRuleControlExecutor] Target resolve failed: {error} Falling back to current scope.");
                    targetScope = ctx.Scope;
                }
                else
                {
                    throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, error);
                }
            }

            if (targetScope == null)
                return;

            EnsureScopeBuiltIfNeeded(targetScope);

            if (!TryResolve(targetScope, out IMonitorChannelHub? hub) || hub == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "IMonitorChannelHub is missing on target scope.");

            switch (typed.Operation)
            {
                case MonitorChannelRuleOperation.AddRule:
                    hub.AddRule(typed.Rule);
                    break;
                case MonitorChannelRuleOperation.RemoveRule:
                    if (string.IsNullOrEmpty(typed.RuleName))
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "RuleName is required for RemoveRule.");
                    hub.RemoveRule(typed.RuleName);
                    break;
                case MonitorChannelRuleOperation.ClearRules:
                    hub.ClearRules();
                    break;
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
            }
        }

        static bool TryResolve<T>(IScopeNode scope, out T? value) where T : class
        {
            value = null;
            var resolver = scope?.Resolver;
            if (resolver == null)
                return false;

            return resolver.TryResolve(out value) && value != null;
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
    }
}
