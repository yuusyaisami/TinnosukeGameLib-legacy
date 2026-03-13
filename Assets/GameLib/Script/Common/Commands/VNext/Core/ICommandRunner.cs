#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using Game.Common;

namespace Game.Commands.VNext
{
    public interface ICommandRunner
    {
        IScopeNode Scope { get; }
        UniTask<CommandRunResult> ExecuteSingleAsync(ICommandData data, CommandContext ctx, CancellationToken ct, CommandRunOptions options);
        UniTask<CommandRunResult> ExecuteListAsync(CommandListData list, CommandContext ctx, CancellationToken ct, CommandRunOptions options);
        UniTask<CommandRunResult> ExecuteWithCancelAsync(CommandListData list, CommandListData onCanceled, CommandContext ctx, CancellationToken ct, CommandRunOptions options);
    }

    public interface ICommandRunnerActivity
    {
        int RunningExecutionCount { get; }
        bool IsExecuting { get; }
        UniTask WaitUntilIdleAsync(CancellationToken ct = default);
        UniTask WaitUntilScopeIdleAsync(IScopeNode scope, CancellationToken ct = default);
    }

    public interface ICommandRunnerDefaultVarsProvider
    {
        void ApplyDefaultVars(IVarStore dest, bool overwrite = false);
    }

    public interface IProjectCommandRunner : ICommandRunner { }
    public interface IPlatformCommandRunner : ICommandRunner { }
    public interface IGlobalCommandRunner : ICommandRunner { }
    public interface ISceneCommandRunner : ICommandRunner { }
    public interface IFieldCommandRunner : ICommandRunner { }
    public interface IEntityCommandRunner : ICommandRunner { }
    public interface IUICommandRunner : ICommandRunner { }
    public interface IUIElementCommandRunner : ICommandRunner { }
}
