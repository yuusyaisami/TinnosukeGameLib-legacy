#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Flow
{
    /// <summary>
    /// Flow 実行時にホスト側で提供される機能（システムコール）のインターフェース。
    /// </summary>
    public interface IFlowHost
    {
        /// <summary>
        /// システムコールを非同期で実行します。
        /// </summary>
        UniTask<FlowSyscallResult> InvokeAsync(
            FlowContext context,
            FlowSyscallRequest request,
            CancellationToken ct);
    }
}
