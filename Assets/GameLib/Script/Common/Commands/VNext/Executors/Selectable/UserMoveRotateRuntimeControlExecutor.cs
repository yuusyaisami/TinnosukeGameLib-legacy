#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.SelectRuntime;
using VContainer;
using VContainer.Unity;

namespace Game.Commands.VNext
{
    public sealed class UserMoveRotateRuntimeControlExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.UserMoveRotateRuntimeControl;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not UserMoveRotateRuntimeControlCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "UserMoveRotateRuntimeControlCommandData is required.");

            ct.ThrowIfCancellationRequested();

            var (targetScope, error) = await ActorScopeResolver.ResolveAsync(typed.Target, ctx, ct);
            if (targetScope == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, error);

            EnsureScopeBuiltIfNeeded(targetScope);

            if (!TryResolve(targetScope, out UserMoveRotateRuntimeBridgeService? bridge) || bridge == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "UserMoveRotateRuntimeBridgeService is missing on target scope.");

            var result = typed.Mode switch
            {
                UserMoveRotateRuntimeControlMode.Enter => bridge.TryEnterEditorMode(),
                UserMoveRotateRuntimeControlMode.Exit => bridge.TryExitEditorMode(typed.RunExitCommands),
                UserMoveRotateRuntimeControlMode.Toggle => bridge.IsEditing()
                    ? bridge.TryExitEditorMode(typed.RunExitCommands)
                    : bridge.TryEnterEditorMode(),
                _ => false,
            };

            if (!result)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"UserMoveRotateRuntime control failed. mode={typed.Mode}");
        }

        static void EnsureScopeBuiltIfNeeded(IScopeNode scope)
        {
            if (scope is BaseLifetimeScope baseScope)
            {
                baseScope.EnsureScopeBuilt();
                return;
            }

            if (scope is RuntimeLifetimeScope runtimeScope)
                runtimeScope.EnsureScopeBuilt();
        }

        static bool TryResolve<T>(IScopeNode scope, out T? value) where T : class
        {
            value = null;
            var resolver = scope?.Resolver;
            if (resolver == null)
                return false;

            return resolver.TryResolve(out value) && value != null;
        }
    }
}
