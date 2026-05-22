#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Channel;
using Game.Common;
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

            if (!GridObjectChannelExecutorUtility.TryResolve(targetScope, out IGridObjectChannelHubService? hub) || hub == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, $"{GridObjectChannelExecutorUtility.DiagnosticCode} IGridObjectChannelHubService is missing on target scope.");

            if (!await hub.BindAsync(typed.ChannelTag, typed.Request, typed.Rebuild, ct))
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"{GridObjectChannelExecutorUtility.DiagnosticCode} GridObjectChannel bind failed. tag='{typed.ChannelTag}'");
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

            if (!GridObjectChannelExecutorUtility.TryResolve(targetScope, out IGridObjectChannelHubService? hub) || hub == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, $"{GridObjectChannelExecutorUtility.DiagnosticCode} IGridObjectChannelHubService is missing on target scope.");

            if (!await hub.RefreshAsync(typed.ChannelTag, typed.RefreshMode, ct))
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"{GridObjectChannelExecutorUtility.DiagnosticCode} GridObjectChannel refresh failed. tag='{typed.ChannelTag}'");
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

            if (!GridObjectChannelExecutorUtility.TryResolve(targetScope, out IGridObjectChannelHubService? hub) || hub == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, $"{GridObjectChannelExecutorUtility.DiagnosticCode} IGridObjectChannelHubService is missing on target scope.");

            if (!await hub.ClearAsync(typed.ChannelTag, typed.KeepBinding, ct))
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"{GridObjectChannelExecutorUtility.DiagnosticCode} GridObjectChannel clear failed. tag='{typed.ChannelTag}'");
        }
    }

    public sealed class ShowGridObjectChoiceAndWaitExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.ShowGridObjectChoiceAndWait;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not ShowGridObjectChoiceAndWaitCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "ShowGridObjectChoiceAndWaitCommandData is required.");

            var targetScope = await GridObjectChannelExecutorUtility.ResolveTargetScopeAsync(typed.Target, ctx, ct);

            if (!GridObjectChannelExecutorUtility.TryResolve(targetScope, out IChoiceChannelHubService? hub) || hub == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, $"{GridObjectChannelExecutorUtility.DiagnosticCode} IChoiceChannelHubService is missing on target scope.");

            if (typed.Request == null || typed.Request.Entries == null || typed.Request.Entries.Count == 0)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "Choice request requires at least one entry.");

            var result = await hub.ShowChoiceAndWaitAsync(typed.ChannelTag, typed.Request, ct);
            var destinationVars = ctx.Vars ?? NullVarStore.Instance;

            if (result.CompletionKind == GridObjectChoiceCompletionKind.Selected)
            {
                if (typed.WriteSelectedIndexToVars)
                {
                    var selectedIndexVarId = GridObjectChannelExecutorUtility.ResolveVarId(typed.SelectedIndexVar, 0);
                    if (selectedIndexVarId > 0)
                        destinationVars.TrySetVariant(selectedIndexVarId, DynamicVariant.FromInt(result.SelectedIndex));
                }

                if (result.SelectedIndex < 0 || result.SelectedIndex >= typed.Request.Entries.Count)
                    throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"Choice selected index is out of range: {result.SelectedIndex}");

                var selectedEntry = typed.Request.Entries[result.SelectedIndex];
                if (selectedEntry != null)
                {
                    selectedEntry.SelectedVars.ApplyTo(destinationVars, ctx, overwrite: true);
                    await ExecuteBranchCommandsAsync(selectedEntry.SelectedCommands, ctx, ct, "selected");
                }

                return;
            }

            if (result.CompletionKind == GridObjectChoiceCompletionKind.Timeout)
            {
                await ExecuteBranchCommandsAsync(typed.OnTimeoutCommands, ctx, ct, "timeout");
                return;
            }

            if (result.CompletionKind == GridObjectChoiceCompletionKind.Canceled ||
                (typed.TreatReplacedAsCanceled && result.CompletionKind == GridObjectChoiceCompletionKind.Replaced))
            {
                await ExecuteBranchCommandsAsync(typed.OnCanceledCommands, ctx, ct, "canceled");
                return;
            }

            var failureKind = result.Message.Contains("[GOC-CHOICE-002]", StringComparison.Ordinal)
                ? CommandRunFailureKind.InvalidArgs
                : CommandRunFailureKind.ResolveFailed;
            throw new CommandExecutionException(failureKind, string.IsNullOrEmpty(result.Message)
                ? $"GridObject choice failed. tag='{typed.ChannelTag}'"
                : result.Message);
        }

        static async UniTask ExecuteBranchCommandsAsync(CommandListData commands, CommandContext ctx, CancellationToken ct, string branchName)
        {
            if (commands == null || commands.Count == 0)
                return;

            var runner = ctx.Runner;
            if (runner == null)
                return;

            var runResult = await runner.ExecuteListAsync(commands, ctx, ct, ctx.Options);
            if (runResult.Status == CommandRunStatus.Canceled)
                throw new OperationCanceledException();

            if (runResult.Status == CommandRunStatus.Error)
                throw new CommandExecutionException(runResult.FailureKind, $"GridObject choice {branchName} branch failed: {runResult.Message}");
        }
    }

    static class GridObjectChannelExecutorUtility
    {
        internal const string DiagnosticCode = "[V22-M4-GRID-001]";

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

        public static int ResolveVarId(VarKeyRef key, int fallback)
        {
            if (!string.IsNullOrEmpty(key.StableKey) && VarIdResolver.TryResolve(key.StableKey, out var resolved) && resolved > 0)
                return resolved;

            return key.VarId > 0 ? key.VarId : fallback;
        }
    }
}
