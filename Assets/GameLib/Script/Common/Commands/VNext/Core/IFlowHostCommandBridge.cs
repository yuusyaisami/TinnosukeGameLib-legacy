#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using Game;

namespace Game.Commands.VNext
{
    public interface IFlowHostCommandBridge
    {
        UniTask<CommandHostCallResult> InvokeAsync(IScopeNode scope, IVarStore vars, int sysId, DynamicVariant[] args, int argCount, CancellationToken ct);
    }
}
