#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Channel;
using Game.Common;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class BindGridObjectChannelExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.BindGridObjectChannel;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not BindGridObjectChannelCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "BindGridObjectChannelCommandData is required.");

            var targetScope = await GridObjectChannelExecutorUtility.ResolveTargetScopeAsync(typed.Target, ctx, ct);
            GridObjectChannelExecutorUtility.EnsureScopeBuiltIfNeeded(targetScope);

            if (!GridObjectChannelExecutorUtility.TryResolve(targetScope, out IGridObjectChannelHubService? hub) || hub == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "IGridObjectChannelHubService is missing on target scope.");

            if (!await hub.BindAsync(typed.ChannelTag, typed.Request, typed.Rebuild, ct))
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"GridObjectChannel bind failed. tag='{typed.ChannelTag}'");
        }
    }

    public sealed class RefreshGridObjectChannelExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.RefreshGridObjectChannel;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not RefreshGridObjectChannelCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "RefreshGridObjectChannelCommandData is required.");

            var targetScope = await GridObjectChannelExecutorUtility.ResolveTargetScopeAsync(typed.Target, ctx, ct);
            GridObjectChannelExecutorUtility.EnsureScopeBuiltIfNeeded(targetScope);

            if (!GridObjectChannelExecutorUtility.TryResolve(targetScope, out IGridObjectChannelHubService? hub) || hub == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "IGridObjectChannelHubService is missing on target scope.");

            if (!await hub.RefreshAsync(typed.ChannelTag, typed.RefreshMode, ct))
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"GridObjectChannel refresh failed. tag='{typed.ChannelTag}'");
        }
    }

    public sealed class ClearGridObjectChannelExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.ClearGridObjectChannel;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not ClearGridObjectChannelCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "ClearGridObjectChannelCommandData is required.");

            var targetScope = await GridObjectChannelExecutorUtility.ResolveTargetScopeAsync(typed.Target, ctx, ct);
            GridObjectChannelExecutorUtility.EnsureScopeBuiltIfNeeded(targetScope);

            if (!GridObjectChannelExecutorUtility.TryResolve(targetScope, out IGridObjectChannelHubService? hub) || hub == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "IGridObjectChannelHubService is missing on target scope.");

            if (!await hub.ClearAsync(typed.ChannelTag, typed.KeepBinding, ct))
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"GridObjectChannel clear failed. tag='{typed.ChannelTag}'");
        }
    }

    static class GridObjectChannelExecutorUtility
    {
        public static async UniTask<IScopeNode> ResolveTargetScopeAsync(ActorSource target, CommandContext ctx, CancellationToken ct)
        {
            var (targetScope, error) = await ActorScopeResolver.ResolveAsync(target, ctx, ct);
            if (targetScope != null)
                return targetScope;

            if (AllowFallback(ctx.Options) && ctx.Scope != null)
            {
                Debug.LogWarning($"[GridObjectChannelExecutor] Target resolve failed: {error} Falling back to current scope.");
                return ctx.Scope;
            }

            throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, error);
        }

        public static void EnsureScopeBuiltIfNeeded(IScopeNode scope)
        {
            if (scope is BaseLifetimeScope baseScope)
            {
                baseScope.EnsureScopeBuilt();
                return;
            }

            if (scope is RuntimeLifetimeScope runtimeScope)
                runtimeScope.EnsureScopeBuilt();
        }

        public static bool TryResolve<T>(IScopeNode scope, out T? value) where T : class
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
