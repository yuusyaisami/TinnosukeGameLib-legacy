#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;
using Game.StatusEffect;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class StatusEffectExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.StatusEffectControl;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            _ = ct;

            if (data is not StatusEffectCommandData typed)
                return UniTask.CompletedTask;

            var targetScope = ResolveTargetScope(typed, ctx);
            if (targetScope?.Resolver == null)
                return UniTask.CompletedTask;

            if (!targetScope.Resolver.TryResolve<IStatusEffectService>(out var service) || service == null)
                return UniTask.CompletedTask;

            switch (typed.Op)
            {
                case StatusEffectCommandOp.Apply:
                    ExecuteApply(service, typed, ctx);
                    break;

                case StatusEffectCommandOp.Remove:
                    service.Remove(typed.BuildFilter());
                    break;

                case StatusEffectCommandOp.Enable:
                    service.SetEnabled(typed.BuildFilter(), true);
                    break;

                case StatusEffectCommandOp.Disable:
                    service.SetEnabled(typed.BuildFilter(), false);
                    break;

                case StatusEffectCommandOp.Use:
                    service.Use(typed.BuildFilter(), ctx.Actor ?? ctx.Scope, ctx);
                    break;

                case StatusEffectCommandOp.Reset:
                    service.Reset(typed.BuildFilter());
                    break;

                case StatusEffectCommandOp.ClearAll:
                    service.ClearAll();
                    break;
            }

            return UniTask.CompletedTask;
        }

        static void ExecuteApply(IStatusEffectService service, StatusEffectCommandData typed, CommandContext ctx)
        {
            var request = typed.BuildApplyRequest();
            if (service.TryApply(request, ctx, out _))
                return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[StatusEffectExecutor] Failed to apply status effect.");
#endif
        }

        static IScopeNode? ResolveTargetScope(StatusEffectCommandData typed, CommandContext ctx)
        {
            if (typed.ServiceScope == StatusEffectServiceScope.Scope)
                return ctx.Scope;

            return ActorSourceFastResolver.Resolve(ctx, typed.TargetActorSource);
        }
    }
}
