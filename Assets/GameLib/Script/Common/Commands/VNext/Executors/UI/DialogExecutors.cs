#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Channel;
using Game.Common;
using Game.Dialogue;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class DialogueChannelExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.DialogueChannel;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not DialogueChannelCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "DialogueChannelCommandData is required.");

            var targetScope = await DialogueExecutorUtility.ResolveTargetScopeAsync(typed.Target, ctx, ct);
            DialogueExecutorUtility.EnsureScopeBuiltIfNeeded(targetScope);

            if (!DialogueExecutorUtility.TryResolve(targetScope, out IDialogueService? service) || service == null)
            {
                if (!typed.Strict)
                    return;
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "IDialogueService is missing on target scope.");
            }

            var tag = typed.NormalizedChannelTag;
            switch (typed.Operation)
            {
                case DialogueChannelOperation.Setup:
                    {
                        var ok = await service.SetupAsync(tag, typed.SetupRequest, ct);
                        EnsureStrict(typed.Strict, ok, CommandRunFailureKind.ResolveFailed, $"Dialogue setup failed. tag='{tag}'");
                        return;
                    }

                case DialogueChannelOperation.ShowMessage:
                    {
                        var result = await service.ShowMessageAsync(tag, typed.MessageRequest, ct);
                        EnsureStrict(typed.Strict, result.Success, CommandRunFailureKind.ResolveFailed,
                            string.IsNullOrWhiteSpace(result.Message)
                                ? $"Dialogue show message failed. tag='{tag}'"
                                : result.Message);
                        return;
                    }

                case DialogueChannelOperation.ShowChoiceAndWait:
                    {
                        var result = await service.ShowChoiceAndWaitAsync(tag, typed.ChoiceRequest, ct);
                        EnsureStrict(typed.Strict, result.Success, CommandRunFailureKind.ResolveFailed,
                            string.IsNullOrWhiteSpace(result.Message)
                                ? $"Dialogue show choice failed. tag='{tag}'"
                                : result.Message);

                        if (!result.Success)
                            return;

                        await HandleChoiceBranchesAsync(typed, result, ctx, ct);
                        if (typed.FailWhenChoiceNotSelected && result.SourceResult.CompletionKind != GridObjectChoiceCompletionKind.Selected)
                        {
                            throw new CommandExecutionException(
                                CommandRunFailureKind.ResolveFailed,
                                $"Dialogue choice completed without selection. tag='{tag}' completion={result.SourceResult.CompletionKind} message={result.SourceResult.Message}");
                        }

                        return;
                    }

                case DialogueChannelOperation.ApplyCharacters:
                    {
                        var ok = await service.ApplyCharactersAsync(tag, typed.CharacterFrameRequest, ct);
                        EnsureStrict(typed.Strict, ok, CommandRunFailureKind.ResolveFailed, $"Dialogue apply characters failed. tag='{tag}'");
                        return;
                    }

                case DialogueChannelOperation.RefreshLayout:
                    {
                        var ok = await service.RefreshLayoutAsync(tag, typed.LayoutRequest, ct);
                        EnsureStrict(typed.Strict, ok, CommandRunFailureKind.ResolveFailed, $"Dialogue refresh layout failed. tag='{tag}'");
                        return;
                    }

                case DialogueChannelOperation.End:
                    {
                        var ok = await service.EndAsync(tag, typed.EndRequest, ct);
                        EnsureStrict(typed.Strict, ok, CommandRunFailureKind.ResolveFailed, $"Dialogue end failed. tag='{tag}'");
                        return;
                    }

                case DialogueChannelOperation.SetVisible:
                    {
                        var changed = service.SetVisible(tag, typed.Visible);
                        EnsureStrict(typed.Strict, changed, CommandRunFailureKind.ResolveFailed, $"Dialogue set visible failed. tag='{tag}'");
                        return;
                    }

                case DialogueChannelOperation.SetActive:
                    {
                        var changed = service.SetActive(tag, typed.Active);
                        EnsureStrict(typed.Strict, changed, CommandRunFailureKind.ResolveFailed, $"Dialogue set active failed. tag='{tag}'");
                        return;
                    }

                case DialogueChannelOperation.SetInputEnabled:
                    {
                        var changed = service.SetInputEnabled(tag, typed.InputEnabled);
                        EnsureStrict(typed.Strict, changed, CommandRunFailureKind.ResolveFailed, $"Dialogue set input failed. tag='{tag}'");
                        return;
                    }

                case DialogueChannelOperation.RequestAdvance:
                    {
                        var accepted = service.TryRequestAdvance(tag);
                        EnsureStrict(typed.Strict, accepted, CommandRunFailureKind.ResolveFailed, $"Dialogue request advance failed. tag='{tag}'");
                        return;
                    }

                case DialogueChannelOperation.CancelChoice:
                    {
                        var accepted = service.TryCancelChoice(tag, typed.CancelChoiceReason);
                        EnsureStrict(typed.Strict, accepted, CommandRunFailureKind.ResolveFailed, $"Dialogue cancel choice failed. tag='{tag}'");
                        return;
                    }

                case DialogueChannelOperation.RegisterOrReplaceChannel:
                    {
                        if (!DialogueExecutorUtility.TryResolve(targetScope, out IDialogueChannelHubService? hub) || hub == null)
                        {
                            EnsureStrict(typed.Strict, false, CommandRunFailureKind.ExecutorMissing, "IDialogueChannelHubService is missing on target scope.");
                            return;
                        }

                        var dynamicContext = new SimpleDynamicContext(ctx.Vars ?? NullVarStore.Instance, targetScope);
                        if (!typed.RegisterPreset.TryGet(dynamicContext, out DialogueChannelPreset? preset) || preset == null)
                        {
                            EnsureStrict(typed.Strict, false, CommandRunFailureKind.ResolveFailed, "DialogueChannel preset could not be resolved.");
                            return;
                        }

                        var ok = hub.RegisterOrReplace(tag, preset);
                        EnsureStrict(typed.Strict, ok, CommandRunFailureKind.ResolveFailed, $"Dialogue channel register/replace failed. tag='{tag}'");
                        return;
                    }

                case DialogueChannelOperation.UnregisterChannel:
                    {
                        if (!DialogueExecutorUtility.TryResolve(targetScope, out IDialogueChannelHubService? hub) || hub == null)
                        {
                            EnsureStrict(typed.Strict, false, CommandRunFailureKind.ExecutorMissing, "IDialogueChannelHubService is missing on target scope.");
                            return;
                        }

                        var ok = hub.Unregister(tag);
                        EnsureStrict(typed.Strict, ok, CommandRunFailureKind.ResolveFailed, $"Dialogue channel unregister failed. tag='{tag}'");
                        return;
                    }

                default:
                    throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Unsupported Dialogue operation: {typed.Operation}");
            }
        }

        static async UniTask HandleChoiceBranchesAsync(
            DialogueChannelCommandData typed,
            DialogueChoiceResult result,
            CommandContext ctx,
            CancellationToken ct)
        {
            var completion = result.SourceResult.CompletionKind;
            switch (completion)
            {
                case GridObjectChoiceCompletionKind.Canceled:
                    await ExecuteBranchCommandsAsync(typed.OnChoiceCanceledCommands, ctx, ct, "canceled");
                    return;

                case GridObjectChoiceCompletionKind.Timeout:
                    await ExecuteBranchCommandsAsync(typed.OnChoiceTimeoutCommands, ctx, ct, "timeout");
                    return;

                case GridObjectChoiceCompletionKind.Replaced:
                    await ExecuteBranchCommandsAsync(typed.OnChoiceReplacedCommands, ctx, ct, "replaced");
                    return;

                default:
                    return;
            }
        }

        static async UniTask ExecuteBranchCommandsAsync(CommandListData commands, CommandContext ctx, CancellationToken ct, string branch)
        {
            if (commands == null || commands.Count == 0)
                return;

            if (ctx.Runner == null)
                return;

            var result = await ctx.Runner.ExecuteListAsync(commands, ctx, ct, ctx.Options);
            if (result.Status == CommandRunStatus.Canceled)
                throw new OperationCanceledException();

            if (result.Status == CommandRunStatus.Error)
                throw new CommandExecutionException(result.FailureKind, $"Dialogue choice {branch} branch failed: {result.Message}");
        }

        static void EnsureStrict(bool strict, bool success, CommandRunFailureKind kind, string message)
        {
            if (!strict || success)
                return;
            throw new CommandExecutionException(kind, message);
        }
    }

    static class DialogueExecutorUtility
    {
        public static async UniTask<IScopeNode> ResolveTargetScopeAsync(ActorSource target, CommandContext ctx, CancellationToken ct)
        {
            var (targetScope, error) = await ActorScopeResolver.ResolveAsync(target, ctx, ct);
            if (targetScope != null)
                return targetScope;

            if (AllowFallback(ctx.Options) && ctx.Scope != null)
            {
                Debug.LogWarning($"[DialogueChannelExecutor] Target resolve failed: {error} Falling back to current scope.");
                return ctx.Scope;
            }

            throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, error);
        }

        public static void EnsureScopeBuiltIfNeeded(IScopeNode scope)
        {
            ScopeFeatureInstallerUtility.EnsureScopeBuiltIfNeeded(scope);
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
