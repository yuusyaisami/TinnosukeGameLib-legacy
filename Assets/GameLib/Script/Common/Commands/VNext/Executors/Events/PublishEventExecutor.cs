#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class PublishEventExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.PublishEvent;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not PublishEventCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "PublishEventCommandData is required.");

            if (string.IsNullOrWhiteSpace(typed.EventKey))
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "EventKey is required.");

            var (scope, error) = await ActorScopeResolver.ResolveAsync(typed.EventScope, ctx, ct);
            if (scope == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, error ?? "Event scope could not be resolved.");

            var resolver = scope.Resolver;
            if (resolver == null || !resolver.TryResolve<IEventService>(out var ev) || ev == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "IEventService is missing.");

            var vars = new VarStore();
            if (typed.UseContextVars)
            {
                (ctx.Vars ?? NullVarStore.Instance).MergeInto(vars, overwrite: typed.OverwriteExistingVars);
            }

            typed.Payload?.ApplyTo(vars, overwrite: typed.OverwriteExistingVars);

            var runTask = ev.PublishAsync(typed.EventKey, vars, ct);
            if (typed.AwaitMode == FlowRunAwaitMode.WaitForCompletion)
                await runTask;
            else
                _ = RunInBackground(runTask);
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
    }
}
