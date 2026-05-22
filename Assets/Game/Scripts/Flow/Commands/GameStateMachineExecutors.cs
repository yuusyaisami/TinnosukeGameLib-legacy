#nullable enable
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
                throw new CommandExecutionException(
                    CommandRunFailureKind.ResolveFailed,
                    $"{GameStateMachineCommandExecutorUtility.DiagnosticCode} {error ?? "State machine source could not be resolved."}");

            var svc = GameStateMachineCommandExecutorUtility.ResolveServiceOrThrow(originScope);
            svc.ChangeState(typed.State);
        }
    }

    static class GameStateMachineCommandExecutorUtility
    {
        internal const string DiagnosticCode = "[V22-M4-GSM-001]";

        public static IGameStateMachineService ResolveServiceOrThrow(IScopeNode origin)
        {
            if (origin == null)
                throw CreateResolveFailed("Resolved state-machine source scope is null.");

            var originResolver = origin.Resolver;
            if (originResolver != null && originResolver.TryResolve<IGameStateMachineService>(out var originSvc) && originSvc != null)
                return originSvc;

            throw CreateResolveFailed(
                "IGameStateMachineService must be registered in the resolved state-machine source scope. Update StateMachineSource or add GameStateMachineMB to that scope.");
        }

        static CommandExecutionException CreateResolveFailed(string message)
        {
            return new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"{DiagnosticCode} {message}");
        }
    }
}
