#nullable enable
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using Game.Actions;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class ChangeGameStateExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.ChangeGameState;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not ChangeGameStateCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "ChangeGameStateCommandData is required.");

            var (originScope, error) = await ActorScopeResolver.ResolveAsync(typed.StateMachineSource, ctx, ct);
            if (originScope == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, error ?? "State machine source could not be resolved.");

            var svc = GameStateMachineCommandExecutorUtility.ResolveServiceOrThrow(originScope);
            svc.ChangeState(typed.State);
        }
    }

    static class GameStateMachineCommandExecutorUtility
    {
        public static IGameStateMachineService ResolveServiceOrThrow(IScopeNode origin)
        {
            if (origin == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "Scope is null.");

            var originResolver = origin.Resolver;
            if (originResolver != null && originResolver.TryResolve<IGameStateMachineService>(out var originSvc) && originSvc != null)
                return originSvc;

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

            throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed,
                "IGameStateMachineService is not registered in the nearest Scene/Field/Project scope. Add GameStateMachineMB to the appropriate scope.");
        }
    }
}
