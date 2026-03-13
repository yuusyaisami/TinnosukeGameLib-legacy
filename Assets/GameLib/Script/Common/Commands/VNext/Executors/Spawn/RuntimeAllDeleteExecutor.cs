#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Project.Scene.Runtime;
using Game.Spawn;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class RuntimeAllDeleteExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.RuntimeAllDelete;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not RuntimeAllDeleteCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "RuntimeAllDeleteCommandData is required.");

            var originResolver = ctx.Scope?.Resolver;
            if (originResolver == null || !originResolver.TryResolve<ISceneSpawnerRegistry>(out var registry) || registry == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "ISceneSpawnerRegistry is not available in current scope.");

            var allowTagFallback = string.IsNullOrEmpty(typed.SpawnerTag);
            var resolved = SceneSpawnerResolver.TryResolveSpawner(
                registry,
                typed.SpawnerKind,
                typed.SpawnerTag,
                allowTagFallback,
                allowRuntimeUiFallback: true);
            var spawnerService = resolved.Spawner;
            if (spawnerService == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"Runtime spawner not found. Kind={typed.SpawnerKind} Tag={typed.SpawnerTag}");

            if (spawnerService is Game.Project.Scene.Runtime.IRuntimeLifetimeScopeSpawnerService runtimeSpawner)
            {
                runtimeSpawner.AllDelete(typed.Filter);
                return UniTask.CompletedTask;
            }

            throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"Spawner resolved but is not a {nameof(Game.Project.Scene.Runtime.IRuntimeLifetimeScopeSpawnerService)}. Kind={typed.SpawnerKind} Tag={typed.SpawnerTag}");
        }
    }
}
