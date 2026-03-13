#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using Game.Actions;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class ChangeGameStateExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.ChangeGameState;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not ChangeGameStateCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "ChangeGameStateCommandData is required.");

            var svc = GameStateMachineCommandExecutorUtility.ResolveServiceOrThrow(ctx);
            svc.ChangeState(typed.State);
            return UniTask.CompletedTask;
        }
    }

    static class GameStateMachineCommandExecutorUtility
    {
        public static IGameStateMachineService ResolveServiceOrThrow(CommandContext ctx)
        {
            if (ctx == null)
                throw new CommandExecutionException(CommandRunFailureKind.Exception, "CommandContext is null.");

            var origin = ctx.Scope;
            if (origin == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "Scope is null.");

            var candidates = new List<LifetimeScopeKind>
            {
                LifetimeScopeKind.Scene,
                LifetimeScopeKind.Field,
                LifetimeScopeKind.Project
            };

            foreach (var kind in candidates)
            {
                var node = ScopeNodeHierarchy.FindNearestAncestorByKind(origin, kind, includeSelf: true);
                if (node == null)
                    continue;
                var resolver = node.Resolver;
                if (resolver == null)
                    continue;
                if (resolver.TryResolve<IGameStateMachineService>(out var svc) && svc != null)
                    return svc;
            }

            var originResolver = origin.Resolver;
            if (originResolver != null && originResolver.TryResolve<IGameStateMachineService>(out var originSvc) && originSvc != null)
                return originSvc;

            throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed,
                "IGameStateMachineService is not registered in the nearest Scene/Field/Project scope. Add GameStateMachineMB to the appropriate scope.");
        }
    }
}
