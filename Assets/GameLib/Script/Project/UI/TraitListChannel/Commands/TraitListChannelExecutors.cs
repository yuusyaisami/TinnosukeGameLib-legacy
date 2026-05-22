#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using Game.UI;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class BindTraitListChannelExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.BindTraitListChannel;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not BindTraitListChannelCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "BindTraitListChannelCommandData is required.");

            var targetScope = await TraitListChannelExecutorUtility.ResolveTargetScopeAsync(typed.Target, ctx, ct);

            if (!TraitListChannelExecutorUtility.TryResolve(targetScope, out ITraitListChannelHubService? hub) || hub == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, $"{TraitListChannelExecutorUtility.DiagnosticCode} ITraitListChannelHubService is missing on target scope.");

            if (!await hub.BindAsync(typed.ChannelTag, typed.Request, typed.Rebuild, ct))
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"{TraitListChannelExecutorUtility.DiagnosticCode} TraitListChannel bind failed. tag='{typed.ChannelTag}'");
        }
    }

    public sealed class RefreshTraitListChannelExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.RefreshTraitListChannel;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not RefreshTraitListChannelCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "RefreshTraitListChannelCommandData is required.");

            var targetScope = await TraitListChannelExecutorUtility.ResolveTargetScopeAsync(typed.Target, ctx, ct);

            if (!TraitListChannelExecutorUtility.TryResolve(targetScope, out ITraitListChannelHubService? hub) || hub == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, $"{TraitListChannelExecutorUtility.DiagnosticCode} ITraitListChannelHubService is missing on target scope.");

            if (!await hub.RefreshAsync(typed.ChannelTag, typed.RefreshMode, ct))
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"{TraitListChannelExecutorUtility.DiagnosticCode} TraitListChannel refresh failed. tag='{typed.ChannelTag}'");
        }
    }

    public sealed class SetTraitListChannelRangeExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.SetTraitListChannelRange;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not SetTraitListChannelRangeCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "SetTraitListChannelRangeCommandData is required.");

            var targetScope = await TraitListChannelExecutorUtility.ResolveTargetScopeAsync(typed.Target, ctx, ct);

            if (!TraitListChannelExecutorUtility.TryResolve(targetScope, out ITraitListChannelHubService? hub) || hub == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, $"{TraitListChannelExecutorUtility.DiagnosticCode} ITraitListChannelHubService is missing on target scope.");

            if (!await hub.SetRangeAsync(typed.ChannelTag, typed.UseRange, typed.Range, typed.Rebuild, ct))
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"{TraitListChannelExecutorUtility.DiagnosticCode} TraitListChannel range update failed. tag='{typed.ChannelTag}'");
        }
    }

    public sealed class ClearTraitListChannelExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.ClearTraitListChannel;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not ClearTraitListChannelCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "ClearTraitListChannelCommandData is required.");

            var targetScope = await TraitListChannelExecutorUtility.ResolveTargetScopeAsync(typed.Target, ctx, ct);

            if (!TraitListChannelExecutorUtility.TryResolve(targetScope, out ITraitListChannelHubService? hub) || hub == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, $"{TraitListChannelExecutorUtility.DiagnosticCode} ITraitListChannelHubService is missing on target scope.");

            if (!await hub.ClearAsync(typed.ChannelTag, typed.KeepBinding, ct))
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"{TraitListChannelExecutorUtility.DiagnosticCode} TraitListChannel clear failed. tag='{typed.ChannelTag}'");
        }
    }

    static class TraitListChannelExecutorUtility
    {
        internal const string DiagnosticCode = "[V22-M4-TRAIT-001]";

        public static async UniTask<IScopeNode> ResolveTargetScopeAsync(ActorSource target, CommandContext ctx, CancellationToken ct)
        {
            var (targetScope, error) = await ActorScopeResolver.ResolveAsync(target, ctx, ct);
            if (targetScope != null)
                return targetScope;

            throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"{DiagnosticCode} {error}");
        }

        public static bool TryResolve<T>(IScopeNode scope, out T? value) where T : class
        {
            value = null;
            var resolver = scope?.Resolver;
            if (resolver == null)
                return false;

            return resolver.TryResolve(out value) && value != null;
        }
    }
}
