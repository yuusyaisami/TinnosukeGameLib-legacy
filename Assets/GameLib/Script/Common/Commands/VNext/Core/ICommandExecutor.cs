#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Commands.VNext
{
    public interface ICommandExecutor
    {
        int CommandId { get; }
        UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct);
    }
}
