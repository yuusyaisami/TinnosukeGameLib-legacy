#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using Game.Flow;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class RunFlowExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.RunFlow;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not RunFlowCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "RunFlowCommandData is required.");

            if (typed.Program == null)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "Flow Program is null.");

            var entry = typed.EntryFunctionName ?? string.Empty;
            if (string.IsNullOrWhiteSpace(entry))
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "EntryFunctionName is empty.");

            if (!ctx.Resolver.TryResolve<IFlowHost>(out var host) || host == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "IFlowHost is missing.");

            var runTask = RunFlowAsync(typed.Program, entry, ctx, host, ct);
            return typed.AwaitMode == FlowRunAwaitMode.WaitForCompletion
                ? runTask
                : RunInBackground(runTask);
        }

        static UniTask RunInBackground(UniTask task)
        {
            UniTask.Void(async () =>
            {
                try { await task; }
                catch (OperationCanceledException) { }
                catch (ObjectDisposedException) { }
                catch (Exception e) { Debug.LogException(e); }
            });
            return UniTask.CompletedTask;
        }

        static async UniTask RunFlowAsync(
            IFlowProgramData program,
            string entry,
            CommandContext ctx,
            IFlowHost host,
            CancellationToken ct)
        {
            var result = await FlowRunner.RunAsync(
                program,
                ctx.Scope,
                ctx.Vars ?? NullVarStore.Instance,
                host,
                entry,
                options: null,
                ct);

            if (result.Status == FlowRunStatus.Completed)
                return;

            if (result.Status == FlowRunStatus.Canceled)
                throw new OperationCanceledException();

            throw new CommandExecutionException(CommandRunFailureKind.Exception, $"Flow error: {result}");
        }
    }
}
