#nullable enable

using System;
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

                case StatusEffectCommandOp.UseGlobal:
                    service.UseGlobal(ctx.Actor ?? ctx.Scope, ctx);
                    break;

                case StatusEffectCommandOp.Reset:
                    service.Reset(typed.BuildFilter(), typed.ResetGlobalStateOnReset);
                    break;

                case StatusEffectCommandOp.ClearAll:
                    service.ClearAll();
                    break;

                case StatusEffectCommandOp.ConfigureServiceSettings:
                    service.ConfigureServiceSettings(typed.BuildServiceSettingsRequest(ctx), ctx);
                    break;
            }

            return UniTask.CompletedTask;
        }

        static void ExecuteApply(IStatusEffectService service, StatusEffectCommandData typed, CommandContext ctx)
        {
            var request = typed.BuildApplyRequest();
            if (service.TryApply(request, ctx, out var instanceId))
                return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var targetScope = ResolveTargetScope(typed, ctx);
            var definition = request.Definition.HasSource
                ? request.Definition.GetOrDefault(ctx, default!)
                : default;

            Debug.LogWarning(
                "[StatusEffectExecutor] Failed to apply status effect. " +
                $"Op={typed.Op} ServiceScope={typed.ServiceScope} " +
                $"TargetScope={DescribeScope(targetScope)} Actor={DescribeScope(ctx.Actor)} Scope={DescribeScope(ctx.Scope)} " +
                $"Request={DescribeRequest(request, ctx)} " +
                $"DefinitionResolved={(definition != null ? definition.DefinitionId : "<null>")} " +
                $"InstanceId={(string.IsNullOrEmpty(instanceId) ? "<none>" : instanceId)}");
#endif
        }

        static IScopeNode? ResolveTargetScope(StatusEffectCommandData typed, CommandContext ctx)
        {
            if (typed.ServiceScope == StatusEffectServiceScope.Scope)
                return ctx.Scope;

            return ActorSourceFastResolver.Resolve(ctx, typed.TargetActorSource);
        }

        static string DescribeScope(IScopeNode? scope)
        {
            if (scope == null)
                return "<null>";

            if (scope.Identity != null)
                return $"{scope.Identity.Id}:{scope.Identity.Kind}";

            return scope.GetType().Name;
        }

        static string DescribeRequest(StatusEffectApplyRequest request, CommandContext ctx)
        {
            var definitionText = request.Definition.HasSource
                ? $"{request.Definition.SourceTypeName}:{request.Definition.SourceDebugData}"
                : "<none>";
            var stackPresetText = request.StackPreset.HasSource
                ? $"{request.StackPreset.SourceTypeName}:{request.StackPreset.SourceDebugData}"
                : "<duration-refresh-default>";
            var hookText = request.HookMutations != null ? request.HookMutations.GetType().Name : "<null>";

            return $"Definition={definitionText} StackPreset={stackPresetText} RuntimeTag={(string.IsNullOrEmpty(request.RuntimeTag) ? "<empty>" : request.RuntimeTag)} Hooks={hookText} CmdScope={DescribeScope(ctx.Scope)} CmdActor={DescribeScope(ctx.Actor)}";
        }
    }
}
