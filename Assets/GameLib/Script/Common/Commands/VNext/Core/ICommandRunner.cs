#nullable enable
using System;
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

    public interface ICommandDetachedRunner
    {
        CommandRunResult StartDetached(
            CommandContext ctx,
            CommandDetachedExecutionPolicy policy,
            CancellationToken callerToken,
            Func<CommandContext, CancellationToken, UniTask<CommandRunResult>> work);

        CommandRunResult StartDetachedList(
            CommandListData list,
            CommandListData onCanceled,
            CommandContext ctx,
            CommandDetachedExecutionPolicy policy,
            CancellationToken callerToken,
            CommandRunOptions options);
    }

    public interface ICommandRunnerDefaultVarsProvider
    {
        void ApplyDefaultVars(IVarStore dest, bool overwrite = false);
    }

    public interface ICommandRunnerService : ICommandRunner, ICommandRunnerActivity, ICommandDetachedRunner, ICommandRunnerDefaultVarsProvider
    {
        IScopeNode OwnerScope { get; }
        new IScopeNode? Scope { get; }
        bool IsStarted { get; }
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
