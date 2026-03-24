#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using Game.SelectRuntime;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Commands.VNext
{
    public sealed class WorldPointerTargetControlExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.WorldPointerTargetControl;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not WorldPointerTargetControlCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "WorldPointerTargetControlCommandData is required.");

            ct.ThrowIfCancellationRequested();

            var (targetScope, error) = await ActorScopeResolver.ResolveAsync(typed.Target, ctx, ct);
            if (targetScope == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, error);

            EnsureScopeBuiltIfNeeded(targetScope);

            if (!TryResolve(targetScope, out WorldPointerTargetBridgeService? bridge) || bridge == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "WorldPointerTargetBridgeService is missing on target scope.");

            TryResolve(targetScope, out ICommandListRuntimeMutationService? mutationService);
            bridge.ApplyCommandMutations(typed.EventCommandProgram, mutationService);
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