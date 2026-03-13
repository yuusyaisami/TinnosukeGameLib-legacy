#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class WaitEventExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.WaitEvent;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not WaitEventCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "WaitEventCommandData is required.");

            if (string.IsNullOrWhiteSpace(typed.EventKey))
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "EventKey is required.");

            var (scope, error) = await ActorScopeResolver.ResolveAsync(typed.EventScope, ctx, ct);
            if (scope == null || scope.Resolver == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, error ?? "Event scope could not be resolved.");

            if (!scope.Resolver.TryResolve<IEventService>(out var ev) || ev == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "IEventService is missing.");

            var tcs = new UniTaskCompletionSource<IVarStore>();
            using var cancelReg = ct.Register(() => tcs.TrySetCanceled(ct));

            using var sub = ev.Subscribe(typed.EventKey, (payload, handlerCt) =>
            {
                if (handlerCt.IsCancellationRequested) return UniTask.CompletedTask;
                tcs.TrySetResult(payload ?? NullVarStore.Instance);
                return UniTask.CompletedTask;
            });

            var resultPayload = await tcs.Task;

            if (typed.CapturePayload && typed.CaptureMaps != null && ctx.Vars != null)
            {
                foreach (var map in typed.CaptureMaps)
                {
                    if (string.IsNullOrWhiteSpace(map.SourceKey) || string.IsNullOrWhiteSpace(map.TargetKey))
                        continue;

                    if (VarIdResolver.TryResolve(map.SourceKey, out var sourceId) &&
                        VarIdResolver.TryResolve(map.TargetKey, out var targetId))
                    {
                        var kind = resultPayload.GetVarKind(sourceId);
                        if (kind == ValueKind.ManagedRef)
                        {
                            if (resultPayload.TryGetManagedRef(sourceId, out var obj))
                                ctx.Vars.TrySetManagedRef(targetId, obj);
                        }
                        else if (kind != ValueKind.Null)
                        {
                            if (resultPayload.TryGetVariant(sourceId, out var variant))
                                ctx.Vars.TrySetVariant(targetId, variant);
                        }
                    }
                }
            }
        }
    }
}
