#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using Game.Common;
using Game.Times;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class TimerControlExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.TimerControl;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not TimerCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "TimerCommandData is required.");

            var origin = ctx.Actor ?? ctx.Scope;
            var scope = ActorSourceFastResolver.Resolve(ctx, typed.ActorSource, origin);
            if (scope?.Resolver == null)
                return UniTask.CompletedTask;

            if (!scope.Resolver.TryResolve<ITimerHubService>(out var hub) || hub == null)
            {
                Debug.LogWarning("[TimerControlExecutor] ITimerHubService not found.");
                return UniTask.CompletedTask;
            }

            if (string.IsNullOrWhiteSpace(typed.TimerKey))
                return UniTask.CompletedTask;

            if (!hub.TryGetRuntime(typed.TimerKey.Trim(), out var runtime) || runtime == null)
                return UniTask.CompletedTask;

            switch (typed.Mode)
            {
                case TimerCommandMode.Start:
                    runtime.Start();
                    break;
                case TimerCommandMode.Stop:
                    runtime.Stop();
                    break;
                case TimerCommandMode.Reset:
                    runtime.Reset();
                    break;
                case TimerCommandMode.SetTime:
                    runtime.SetTime(typed.Time.GetOrDefault(ctx, runtime.CurrentTime));
                    break;
                case TimerCommandMode.SetTimeScale:
                    runtime.SetTimeScale(typed.TimeScale.GetOrDefault(ctx, runtime.TimeScale));
                    break;
                case TimerCommandMode.GetTime:
                    WriteTime(typed, ctx, scope, runtime.CurrentTime);
                    break;
            }

            return UniTask.CompletedTask;
        }

        static void WriteTime(TimerCommandData typed, CommandContext ctx, IScopeNode scope, float time)
        {
            var varId = typed.OutputVar.VarId;
            if (varId == 0)
                return;

            IBlackboardService? blackboard = null;
            TryResolveBlackboard(scope, out blackboard);

            var value = DynamicVariant.FromFloat(time);
            if (!TrySetVariant(typed.OutputTarget, ctx, scope, blackboard, varId, value))
                Debug.LogWarning($"[TimerControlExecutor] Failed to write output var. target={typed.OutputTarget} varId={varId}");
        }

        static bool TryResolveBlackboard(IScopeNode? scope, out IBlackboardService? blackboard)
        {
            blackboard = null;
            if (scope?.Resolver == null)
                return false;

            if (!scope.Resolver.TryResolve(out blackboard) || blackboard == null)
                return false;

            return true;
        }

        static bool TrySetVariant(VarStoreTarget target, CommandContext ctx, IScopeNode? scope, IBlackboardService? blackboard, int varId, DynamicVariant value)
        {
            switch (target)
            {
                case VarStoreTarget.CommandVars:
                    return ctx.Vars != null && ctx.Vars.TrySetVariant(varId, in value);
                case VarStoreTarget.BlackboardLocal:
                    return blackboard != null && blackboard.TryLocalSetVariant(varId, in value);
                case VarStoreTarget.BlackboardGlobal:
                    return blackboard != null && blackboard.TryGlobalSetVariant(varId, in value);
                default:
                    return false;
            }
        }
    }
}
