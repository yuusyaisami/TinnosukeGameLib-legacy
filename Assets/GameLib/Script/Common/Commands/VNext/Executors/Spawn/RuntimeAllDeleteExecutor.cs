#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Project.Scene.Runtime;
using Game.Spawn;

namespace Game.Commands.VNext
{
    public sealed class RuntimeAllDeleteExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.RuntimeAllDelete;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not RuntimeAllDeleteCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "RuntimeAllDeleteCommandData is required.");

            if (TryReleaseViaSceneKernel(typed, out _))
                return UniTask.CompletedTask;

            throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"SceneKernel spawn boundary could not resolve a release pool. Kind={typed.SpawnerKind} Tag={typed.SpawnerTag}");
        }

        static bool TryReleaseViaSceneKernel(RuntimeAllDeleteCommandData typed, out int releasedCount)
        {
            return SceneKernelSpawnBindingHub.TryReleaseAll(typed.SpawnerKind, typed.SpawnerTag, typed.Filter, out releasedCount);
        }
    }
}
