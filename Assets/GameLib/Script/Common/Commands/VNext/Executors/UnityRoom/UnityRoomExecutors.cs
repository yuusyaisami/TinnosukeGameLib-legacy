#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.UnityRoom;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class UnityRoomSendScoreExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.UnityRoomSendScore;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not UnityRoomSendScoreCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "UnityRoomSendScoreCommandData is required.");

            if (!ctx.Resolver.TryResolve<IUnityRoomService>(out var service) || service == null)
                return;

            await service.SendScoreAsync(typed.Score.Resolve(ctx), ct);
        }
    }
}
