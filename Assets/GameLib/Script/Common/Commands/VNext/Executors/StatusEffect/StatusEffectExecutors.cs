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
        internal const string DiagnosticCode = "[V22-M4-STATUS-001]";

        public int CommandId => CommandIds.StatusEffectControl;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            _ = ct;

            if (data is not StatusEffectCommandData typed)
                return UniTask.CompletedTask;

            var targetScope = ResolveTargetScopeOrThrow(typed, ctx);
            if (targetScope.Resolver == null)
            {
                throw new CommandExecutionException(
                    CommandRunFailureKind.ResolveFailed,
                    $"{DiagnosticCode} Resolved status effect scope has no resolver. ServiceScope={typed.ServiceScope} TargetKind={typed.TargetActorSource.Kind}");
            }

            if (!targetScope.Resolver.TryResolve<IStatusEffectService>(out var service) || service == null)
            {
                throw new CommandExecutionException(
                    CommandRunFailureKind.ExecutorMissing,
                    $"{DiagnosticCode} IStatusEffectService is missing on the resolved status effect scope. ServiceScope={typed.ServiceScope} TargetKind={typed.TargetActorSource.Kind}");
            }

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

                case StatusEffectCommandOp.EnableOperation:
                    service.SetOperationEnabled(typed.BuildFilter(), typed.OperationId, true);
                    break;

                case StatusEffectCommandOp.Disable:
                    service.SetEnabled(typed.BuildFilter(), false);
                    break;

                case StatusEffectCommandOp.DisableOperation:
                    service.SetOperationEnabled(typed.BuildFilter(), typed.OperationId, false);
                    break;

                case StatusEffectCommandOp.Use:
                    service.Use(typed.BuildFilter(), ctx.Actor ?? ctx.Scope, ctx);
                    break;

                case StatusEffectCommandOp.UseGlobal:
                    service.UseGlobal(ctx.Actor ?? ctx.Scope, ctx);
                    break;

                case StatusEffectCommandOp.RestoreState:
                    service.RestoreState(typed.BuildFilter(), typed.ResetGlobalStateOnReset);
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

            throw new CommandExecutionException(
                CommandRunFailureKind.ResolveFailed,
                $"{DiagnosticCode} Status effect apply failed. ServiceScope={typed.ServiceScope} TargetKind={typed.TargetActorSource.Kind} Request={DescribeRequest(request, ctx)} InstanceId={(string.IsNullOrEmpty(instanceId) ? "<none>" : instanceId)}");
        }

        static IScopeNode ResolveTargetScopeOrThrow(StatusEffectCommandData typed, CommandContext ctx)
        {
            if (typed.ServiceScope == StatusEffectServiceScope.Scope)
            {
                if (ctx.Scope != null)
                    return ctx.Scope;

                throw new CommandExecutionException(
                    CommandRunFailureKind.ResolveFailed,
                    $"{DiagnosticCode} Status effect scope target could not be resolved. ServiceScope={typed.ServiceScope} TargetKind={typed.TargetActorSource.Kind}");
            }

            var targetScope = ActorSourceFastResolver.Resolve(ctx, typed.TargetActorSource);
            if (targetScope != null)
                return targetScope;

            throw new CommandExecutionException(
                CommandRunFailureKind.ResolveFailed,
                $"{DiagnosticCode} Status effect target scope could not be resolved. ServiceScope={typed.ServiceScope} TargetKind={typed.TargetActorSource.Kind}");
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
