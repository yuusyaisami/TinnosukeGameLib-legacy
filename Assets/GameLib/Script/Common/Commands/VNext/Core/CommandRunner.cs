#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using UnityEngine;
using Game;

namespace Game.Commands.VNext
{
    public class CommandRunner : ICommandRunner,
        IProjectCommandRunner,
        IPlatformCommandRunner,
        IGlobalCommandRunner,
        ISceneCommandRunner,
        IFieldCommandRunner,
        IEntityCommandRunner,
        IUICommandRunner,
        IScopeAcquireHandler,
        IScopeReleaseHandler,
        ICommandRunnerDefaultVarsProvider,
        ICommandRunnerActivity
    {
        readonly IScopeNode _scope;
        readonly CommandExecutorRegistry _registry;
        readonly ICommandCatalog _catalog;
        readonly ICommandKeyResolver _keyResolver;
        readonly ICommandResolveLogger _logger;
        readonly VarStorePayload? _defaultVarsPayload;
        protected readonly VarStore _runnerVars = new();
        readonly Dictionary<IScopeNode, int> _runningByScope = new();
        readonly Dictionary<IScopeNode, UniTaskCompletionSource> _scopeIdleWaiters = new();
        UniTaskCompletionSource? _idleWaiter;
        int _runningExecutionCount;

        public IScopeNode Scope => _scope;
        public int RunningExecutionCount => _runningExecutionCount;
        public bool IsExecuting => _runningExecutionCount > 0;

        public CommandRunner(
            IScopeNode scope,
            CommandExecutorRegistry registry,
            ICommandCatalog catalog,
            ICommandKeyResolver keyResolver,
            ICommandResolveLogger logger,
            VarStorePayload? defaultVars = null)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _catalog = catalog ?? NullCommandCatalog.Instance;
            _keyResolver = keyResolver ?? NullCommandKeyResolver.Instance;
            _logger = logger ?? NullCommandResolveLogger.Instance;
            _defaultVarsPayload = defaultVars;
            ApplyDefaultRunnerVars();
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _ = isReset;
            ApplyDefaultRunnerVars();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _runnerVars.Clear();
            _runningByScope.Clear();
            foreach (var kv in _scopeIdleWaiters)
            {
                kv.Value.TrySetResult();
            }
            _scopeIdleWaiters.Clear();
            _idleWaiter?.TrySetResult();
            _idleWaiter = null;
            _runningExecutionCount = 0;
        }

        public async UniTask<CommandRunResult> ExecuteSingleAsync(ICommandData data, CommandContext ctx, CancellationToken ct, CommandRunOptions options)
        {
            var normalized = CommandRunOptions.ResolveOrDefault(options);
            var effectiveCtx = EnsureContext(ctx, normalized);
            using var execution = BeginExecution(effectiveCtx.Scope);
            InjectContextVars(effectiveCtx, effectiveCtx.Vars);
            MergeRunnerVars(effectiveCtx.Vars);

            var trace = new TraceBuilder(normalized);
            if (data == null)
            {
                trace.CaptureOnFailure();
                return CommandRunResult.Error(lastIndex: -1, errorIndex: 0, CommandRunFailureKind.ResolveFailed, "Command data is null.", trace.Trace, null);
            }

            if (data.CommandId <= 0)
            {
                trace.CaptureOnFailure();
                return CommandRunResult.Error(lastIndex: 0, errorIndex: 0, CommandRunFailureKind.InvalidArgs, "CommandId is invalid.", trace.Trace, null);
            }

            if (!_registry.TryGet(data.CommandId, out var executor) || executor == null)
            {
                var contextInfo = BuildExecutionContextDescription(effectiveCtx, data, source: null);
                _logger.LogExecutorMissing(data.CommandId, $"Executor is missing. {contextInfo}");
                trace.CaptureOnFailure();
                return CommandRunResult.Error(lastIndex: 0, errorIndex: 0, CommandRunFailureKind.ExecutorMissing, $"Executor not found for CommandId={data.CommandId}. CmdData={data.DebugData}", trace.Trace, null);
            }

            var frame = new CommandRunFrame(0, data.CommandId, "Direct", data.GetType().Name, data.DebugData);
            trace.Push(frame);
            using var runtimeTrace = CommandExecutionTrace.Push(
                BuildExecutionTraceSnapshot(
                    effectiveCtx,
                    data,
                    source: null,
                    commandIndex: 0,
                    listLabel: "Direct",
                    listFunctionName: null));
            var outcome = await ExecuteDataAsync(executor, data, effectiveCtx, ct, normalized);

            if (outcome.Canceled)
            {
                trace.CaptureOnFailure();
                trace.Pop();
                return CommandRunResult.Canceled(lastIndex: 0, errorIndex: 0, outcome.Message, trace.Trace);
            }

            if (outcome.Broken)
            {
                trace.CaptureAlways();
                trace.Pop();
                return CommandRunResult.Break(0, trace.Trace);
            }

            if (!outcome.Success)
            {
                trace.CaptureOnFailure();
                trace.Pop();
                return new CommandRunResult(CommandRunStatus.Error, outcome.FailureKind, 1, 0, 0, outcome.Message, trace.Trace, outcome.Exception);
            }

            trace.CaptureAlways();
            trace.Pop();
            return CommandRunResult.Completed(0, 0, CommandRunFailureKind.None, -1, string.Empty, trace.Trace, null);
        }

        public async UniTask<CommandRunResult> ExecuteListAsync(CommandListData list, CommandContext ctx, CancellationToken ct, CommandRunOptions options)
        {
            var normalized = CommandRunOptions.ResolveOrDefault(options);
            var effectiveCtx = EnsureContext(ctx, normalized);
            using var execution = BeginExecution(effectiveCtx.Scope);
            InjectContextVars(effectiveCtx, effectiveCtx.Vars);
            MergeRunnerVars(effectiveCtx.Vars);

            var commandList = list;
            if (commandList == null || commandList.Count == 0)
                return CommandRunResult.Completed(-1, 0, CommandRunFailureKind.None, -1, string.Empty, null, null);

            var listLabel = commandList.GetDebugLabel();
            var listFunctionName = commandList.FunctionName;
            var resolveContext = new CommandResolveContext(
                effectiveCtx.Scope,
                effectiveCtx.Vars,
                effectiveCtx.CommandRootScope,
                effectiveCtx.Scope?.Resolver,
                _catalog,
                _keyResolver,
                _logger,
                normalized.AllowRuntimeKeyFallback,
                effectiveCtx);

            var trace = new TraceBuilder(normalized);
            var failureCount = 0;
            var failureKind = CommandRunFailureKind.None;
            var errorIndex = -1;
            var lastIndex = -1;
            var message = string.Empty;
            CommandExceptionInfo? exceptionInfo = null;

            for (int i = 0; i < commandList.Count; i++)
            {
                lastIndex = i;
                if (ct.IsCancellationRequested)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (!normalized.SuppressCancelLog)
                    {
                        var cancelLabel = commandList.GetDebugLabel();
                        if (string.IsNullOrEmpty(cancelLabel))
                            cancelLabel = "<none>";
                        Debug.LogWarning($"[CommandRunner] ExecuteListAsync canceled before idx={i} list={cancelLabel}");
                    }
#endif
                    trace.CaptureOnFailure();
                    return CommandRunResult.Canceled(lastIndex, i, "Canceled.", trace.Trace);
                }

                var source = commandList.GetAt(i);
                if (source == null)
                {
                    var fail = HandleFailure(CommandRunFailureKind.ResolveFailed, i, "Command source is null.");
                    if (fail.IsTerminated)
                        return fail.Result;
                    continue;
                }

                if (source is ICommandSourceExecutionControl executionControl && !executionControl.IsExecutionEnabled)
                {
                    continue;
                }

                bool resolved;
                ICommandData? data;
                try
                {
                    resolved = source.TryResolve(resolveContext, out data);
                }
                catch (Exception ex)
                {
                    var listInfo = BuildListInfo(i, listLabel, listFunctionName);
                    _logger.LogResolveFailed(source, $"Command source threw: {ex.GetType().Name} | {listInfo}");
                    var info = CommandExceptionInfo.FromException(ex, includeStackTrace: ShouldIncludeStackTrace(normalized));
                    var fail = HandleFailure(CommandRunFailureKind.Exception, i, ex.Message, info);
                    if (fail.IsTerminated)
                        return fail.Result;
                    continue;
                }

                if (!resolved || data == null)
                {
                    var listInfo = BuildListInfo(i, listLabel, listFunctionName);
                    _logger.LogResolveFailed(source, $"Command source failed to resolve. | {listInfo}");
                    var sourceName = source?.DebugName ?? "<null>";
                    var fail = HandleFailure(
                        CommandRunFailureKind.ResolveFailed,
                        i,
                        $"Command source failed to resolve. Source={sourceName} {listInfo}");
                    if (fail.IsTerminated)
                        return fail.Result;
                    continue;
                }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (data is TextChannelCommandData textData)
                {
                    var listLabel1 = string.IsNullOrEmpty(listLabel) ? "<none>" : listLabel;
                    var contextInfo = BuildExecutionContextDescription(effectiveCtx, data, source);
                    //Debug.Log($"[CommandRunner] Resolved TextChannel idx={i} tag={textData.ChannelTag} mode={textData.Mode} action={textData.Action} list={listLabel1} {contextInfo}");
                }
#endif

                if (data.CommandId <= 0)
                {
                    var fail = HandleFailure(CommandRunFailureKind.InvalidArgs, i, "CommandId is invalid.");
                    if (fail.IsTerminated)
                        return fail.Result;
                    continue;
                }

                if (!_registry.TryGet(data.CommandId, out var executor) || executor == null)
                {
                    var listInfo = BuildListInfo(i, listLabel, listFunctionName);
                    var contextInfo = BuildExecutionContextDescription(effectiveCtx, data, source);
                    _logger.LogExecutorMissing(data.CommandId, $"Executor is missing. {contextInfo} | {listInfo}");
                    var fail = HandleFailure(CommandRunFailureKind.ExecutorMissing, i, $"Executor not found for CommandId={data.CommandId}. CmdData={data.DebugData}: これが出力される場合、CommandExecutorRegistryに該当のコマンドExecutorが登録されていない可能性があります。");
                    if (fail.IsTerminated)
                        return fail.Result;
                    continue;
                }

                var frame = new CommandRunFrame(i, data.CommandId, source?.DebugName ?? "<null>", GetCommandDisplayName(data), data.DebugData);
                trace.Push(frame);
                using var runtimeTrace = CommandExecutionTrace.Push(
                    BuildExecutionTraceSnapshot(
                        effectiveCtx,
                        data,
                        source,
                        commandIndex: i,
                        listLabel,
                        listFunctionName));

                // 各コマンドごとに独立したリンク付きトークンを作成し、連鎖的なキャンセルを防止する
                // これにより、リスト内の1つのコマンドのキャンセルが他のコマンドに影響を与えないようにする
                using var commandCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                var outcome = await ExecuteDataAsync(executor, data, effectiveCtx, commandCts.Token, normalized);

                if (outcome.Canceled)
                {
                    if (!normalized.SuppressCancelLog)
                    {
                        var cancelMsg = BuildCancellationMessage(effectiveCtx, data, source, BuildListInfo(i, listLabel, listFunctionName), outcome.Message);
                        _logger.LogExecutionCanceled(data.CommandId, cancelMsg);
                    }
                    trace.CaptureOnFailure();
                    trace.Pop();
                    return CommandRunResult.Canceled(lastIndex, i, outcome.Message, trace.Trace);
                }

                if (outcome.Broken)
                {
                    trace.CaptureAlways();
                    trace.Pop();
                    return CommandRunResult.Break(lastIndex, trace.Trace);
                }

                if (!outcome.Success)
                {
                    trace.CaptureOnFailure();
                    var listInfo = BuildListInfo(i, listLabel, listFunctionName);
                    var contextInfo = BuildExecutionContextDescription(effectiveCtx, data, source) + " " + listInfo;
                    var logMessage = BuildFailureLogMessage(contextInfo, outcome.Message, outcome.Exception, trace.Trace);
                    _logger.LogExecutionFailed(data.CommandId, logMessage);
                    var fail = HandleFailure(outcome.FailureKind, i, outcome.Message ?? string.Empty, outcome.Exception);
                    trace.Pop();
                    if (fail.IsTerminated)
                        return fail.Result;
                    continue;
                }

                trace.CaptureAlways();
                trace.Pop();
            }

            return CommandRunResult.Completed(lastIndex, failureCount, failureKind, errorIndex, message, trace.Trace, exceptionInfo);

            static string BuildListInfo(int index, string label, string functionName)
            {
                var listInfo = string.IsNullOrEmpty(label) ? $"ListIndex={index}" : $"ListIndex={index} List={label}";
                if (!string.IsNullOrEmpty(functionName))
                    listInfo += $" FN={functionName}";
                return listInfo;
            }

            FailureDecision HandleFailure(CommandRunFailureKind kind, int index, string reason, CommandExceptionInfo? exception = null)
            {
                failureCount++;
                if (failureCount == 1)
                {
                    failureKind = kind;
                    errorIndex = index;
                    message = reason ?? string.Empty;
                    exceptionInfo = exception;
                    trace.CaptureOnFailure();
                }

                if (normalized.FailurePolicy == CommandFailurePolicy.FailFast)
                {
                    return FailureDecision.Terminate(new CommandRunResult(CommandRunStatus.Error, kind, failureCount, lastIndex, index, reason ?? string.Empty, trace.Trace, exception));
                }

                return FailureDecision.Continue();
            }
        }

        public async UniTask<CommandRunResult> ExecuteWithCancelAsync(CommandListData list, CommandListData onCanceled, CommandContext ctx, CancellationToken ct, CommandRunOptions options)
        {
            var result = await ExecuteListAsync(list, ctx, ct, options);
            if (result.Status != CommandRunStatus.Canceled)
                return result;

            if (onCanceled != null && onCanceled.Count > 0)
            {
                _ = await ExecuteListAsync(onCanceled, ctx, CancellationToken.None, options);
            }

            return result;
        }

        public UniTask WaitUntilIdleAsync(CancellationToken ct = default)
        {
            if (_runningExecutionCount <= 0)
                return UniTask.CompletedTask;

            _idleWaiter ??= new UniTaskCompletionSource();
            var task = _idleWaiter.Task;
            return ct.CanBeCanceled ? task.AttachExternalCancellation(ct) : task;
        }

        public UniTask WaitUntilScopeIdleAsync(IScopeNode scope, CancellationToken ct = default)
        {
            if (scope == null)
                return WaitUntilIdleAsync(ct);

            if (!_runningByScope.TryGetValue(scope, out var running) || running <= 0)
                return UniTask.CompletedTask;

            if (!_scopeIdleWaiters.TryGetValue(scope, out var waiter) || waiter == null)
            {
                waiter = new UniTaskCompletionSource();
                _scopeIdleWaiters[scope] = waiter;
            }

            var task = waiter.Task;
            return ct.CanBeCanceled ? task.AttachExternalCancellation(ct) : task;
        }

        CommandContext EnsureContext(CommandContext ctx, CommandRunOptions options)
        {
            if (ctx == null)
                return new CommandContext(_scope, NullVarStore.Instance, this, _scope, options, _scope, _scope, _scope).CreateExecutionContext();

            if (!ReferenceEquals(ctx.Runner, this))
                return new CommandContext(ctx.Scope, ctx.Vars, this, ctx.Actor, options, ctx.CommandRootScope, ctx.RootActor, ctx.CallerActor, ctx).CreateExecutionContext();

            if (!ctx.Options.Equals(options))
                return ctx.WithOptions(options).CreateExecutionContext();

            return ctx.CreateExecutionContext();
        }

        protected virtual void InjectContextVars(CommandContext ctx, IVarStore vars)
        {
        }

        protected void MergeRunnerVars(IVarStore dest)
        {
            ApplyRunnerVars(dest, overwrite: false);
        }

        public void ApplyDefaultVars(IVarStore dest, bool overwrite = false)
        {
            ApplyRunnerVars(dest, overwrite);
        }

        void ApplyRunnerVars(IVarStore dest, bool overwrite)
        {
            if (dest == null)
                return;

            foreach (var varId in _runnerVars.EnumerateVarIds())
            {
                if (varId == 0)
                    continue;

                if (!overwrite && dest.Contains(varId))
                    continue;

                var kind = _runnerVars.GetVarKind(varId);
                if (kind == ValueKind.ManagedRef)
                {
                    if (_runnerVars.TryGetManagedRef(varId, out var managed))
                        dest.TrySetManagedRef(varId, managed);
                    continue;
                }

                if (_runnerVars.TryGetVariant(varId, out var variant))
                {
                    if (variant.Kind == ValueKind.Null)
                        continue;
                    dest.TrySetVariant(varId, variant);
                }
            }
        }

        void ApplyDefaultRunnerVars()
        {
            _runnerVars.Clear();
            _defaultVarsPayload?.ApplyTo(_runnerVars, overwrite: true);
        }

        static string BuildExecutionContextDescription(CommandContext ctx, ICommandData data, ICommandSource? source)
        {
            var actor = CommandExecutionTrace.DescribeScope(ctx.Actor);
            var scope = CommandExecutionTrace.DescribeScope(ctx.Scope);
            var sourceName = source?.DebugName ?? "<null>";
            var commandName = GetCommandDisplayName(data);
            var commandId = data?.CommandId ?? -1;
            var debugData = data?.DebugData ?? string.Empty;
            if (string.IsNullOrEmpty(debugData))
                return $"Actor={actor} Scope={scope} Source={sourceName} Cmd={commandName}(Id={commandId})";

            return $"Actor={actor} Scope={scope} Source={sourceName} Cmd={commandName}(Id={commandId}) CmdData={debugData}";
        }

        static CommandExecutionTraceSnapshot BuildExecutionTraceSnapshot(
            CommandContext ctx,
            ICommandData data,
            ICommandSource? source,
            int commandIndex,
            string? listLabel,
            string? listFunctionName)
        {
            return new CommandExecutionTraceSnapshot(
                commandIndex,
                data?.CommandId ?? -1,
                source?.DebugName ?? "<null>",
                GetCommandDisplayName(data),
                data?.GetType().Name ?? "<null>",
                data?.DebugData ?? string.Empty,
                listLabel ?? string.Empty,
                listFunctionName ?? string.Empty,
                ctx.Scope,
                ctx.Actor,
                ctx.CommandRootScope,
                ctx.RootActor,
                ctx.CallerActor);
        }

        static string GetCommandDisplayName(ICommandData? data)
        {
            if (data == null)
                return "<null>";

            var name = data.GetType().Name;
            const string suffix = "CommandData";
            if (name.EndsWith(suffix, StringComparison.Ordinal))
                name = name.Substring(0, name.Length - suffix.Length);
            return name;
        }

        static string BuildCancellationMessage(CommandContext ctx, ICommandData data, ICommandSource? source, string listInfo, string? outcomeMessage)
        {
            var contextInfo = BuildExecutionContextDescription(ctx, data, source);
            var sb = new StringBuilder(contextInfo);

            if (!string.IsNullOrEmpty(outcomeMessage))
            {
                sb.Append(" | ").Append(outcomeMessage);
            }

            if (!string.IsNullOrEmpty(listInfo))
            {
                sb.Append(" | ").Append(listInfo);
            }

            return sb.ToString();
        }

        static string BuildFailureLogMessage(
            string contextInfo,
            string? detailMessage,
            CommandExceptionInfo? exception,
            IReadOnlyList<CommandRunFrame> trace)
        {
            var sb = new StringBuilder(contextInfo);

            if (!string.IsNullOrEmpty(detailMessage))
            {
                sb.Append("\nDetail: ").Append(detailMessage);
            }

            if (exception != null)
            {
                var typeName = string.IsNullOrEmpty(exception.TypeName) ? "<unknown>" : exception.TypeName;
                var message = exception.Message ?? string.Empty;
                sb.Append("\nExceptionType: ").Append(typeName);
                sb.Append("\nExceptionMessage: ").Append(message);
                if (!string.IsNullOrEmpty(exception.StackTrace))
                {
                    sb.Append("\nExceptionStack:");
                    AppendMultiline(sb, exception.StackTrace);
                }
            }

            if (trace != null && trace.Count > 0)
            {
                sb.Append("\nTrace:");
                for (int i = 0; i < trace.Count; i++)
                {
                    var frame = trace[i];
                    sb.Append("\n- [")
                        .Append(i)
                        .Append("] Index=")
                        .Append(frame.CommandIndex)
                        .Append(" Cmd=")
                        .Append(frame.DataType)
                        .Append("(Id=")
                        .Append(frame.CommandId)
                        .Append(")");
                    if (!string.IsNullOrEmpty(frame.DebugData))
                        sb.Append(" CmdData=").Append(frame.DebugData);
                    sb.Append(" Source=")
                        .Append(frame.SourceType);
                }
            }

            return sb.ToString();
        }

        static void AppendMultiline(StringBuilder sb, string block)
        {
            if (string.IsNullOrEmpty(block))
                return;

            var normalized = block.Replace("\r\n", "\n").Replace("\r", "\n");
            var lines = normalized.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Length == 0)
                    continue;
                sb.Append("\n").Append(lines[i]);
            }
        }

        static async UniTask<ExecutionOutcome> ExecuteDataAsync(ICommandExecutor executor, ICommandData data, CommandContext ctx, CancellationToken ct, CommandRunOptions options)
        {
            return await ExecuteDataAsyncInternal(data.CommandId, data, ctx, ct, options, executor);
        }

        ExecutionLease BeginExecution(IScopeNode? scope)
        {
            _runningExecutionCount++;
            if (scope != null)
            {
                if (_runningByScope.TryGetValue(scope, out var current))
                    _runningByScope[scope] = current + 1;
                else
                    _runningByScope[scope] = 1;
            }

            return new ExecutionLease(this, scope);
        }

        void EndExecution(IScopeNode? scope)
        {
            if (_runningExecutionCount > 0)
            {
                _runningExecutionCount--;
                if (_runningExecutionCount == 0)
                {
                    _idleWaiter?.TrySetResult();
                    _idleWaiter = null;
                }
            }

            if (scope == null)
                return;

            if (!_runningByScope.TryGetValue(scope, out var current))
                return;

            if (current <= 1)
            {
                _runningByScope.Remove(scope);
                if (_scopeIdleWaiters.TryGetValue(scope, out var waiter) && waiter != null)
                {
                    waiter.TrySetResult();
                    _scopeIdleWaiters.Remove(scope);
                }
                return;
            }

            _runningByScope[scope] = current - 1;
        }

        readonly struct ExecutionLease : IDisposable
        {
            readonly CommandRunner _owner;
            readonly IScopeNode? _scope;

            public ExecutionLease(CommandRunner owner, IScopeNode? scope)
            {
                _owner = owner;
                _scope = scope;
            }

            public void Dispose()
            {
                _owner.EndExecution(_scope);
            }
        }

        static async UniTask<ExecutionOutcome> ExecuteDataAsyncInternal(int commandId, ICommandData data, CommandContext ctx, CancellationToken ct, CommandRunOptions options, ICommandExecutor? executor = null)
        {
            if (commandId <= 0)
                return ExecutionOutcome.FromFailure(CommandRunFailureKind.InvalidArgs, "CommandId is invalid.", null);

            try
            {
                // Scopeが既に破棄されている場合はキャンセル扱いにする
                if (ctx.Scope is UnityEngine.Object scopeObj && !scopeObj)
                    throw new OperationCanceledException("Scope has been destroyed.");

                if (ctx.Actor is UnityEngine.Object actorObj && !actorObj)
                    throw new OperationCanceledException("Actor has been destroyed.");

                if (ct.IsCancellationRequested)
                    return ExecutionOutcome.FromCanceled("Canceled.");

                if (executor != null)
                {
                    await executor.Execute(data, ctx, ct);
                }
                else
                {
                    return ExecutionOutcome.FromFailure(CommandRunFailureKind.ExecutorMissing, "Executor is null.", null);
                }

                if (ctx.TryConsumeBreakRequest())
                    return ExecutionOutcome.FromBreak();

                return ExecutionOutcome.FromSuccess();
            }
            catch (OperationCanceledException)
            {
                return ExecutionOutcome.FromCanceled("Canceled.");
            }
            catch (CommandExecutionException ex)
            {
                var info = CommandExceptionInfo.FromException(ex, includeStackTrace: ShouldIncludeStackTrace(options));
                return ExecutionOutcome.FromFailure(ex.FailureKind, ex.Message, info);
            }
            catch (Exception ex)
            {
                var info = CommandExceptionInfo.FromException(ex, includeStackTrace: ShouldIncludeStackTrace(options));
                return ExecutionOutcome.FromFailure(CommandRunFailureKind.Exception, ex.Message, info);
            }
        }

        static bool ShouldIncludeStackTrace(CommandRunOptions options)
        {
            if (options.TracePolicy == CommandTracePolicy.None)
                return false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return true;
#else
            return Debug.isDebugBuild;
#endif
        }

        readonly struct ExecutionOutcome
        {
            public readonly bool Success;
            public readonly bool Canceled;
            public readonly bool Broken;
            public readonly CommandRunFailureKind FailureKind;
            public readonly string Message;
            public readonly CommandExceptionInfo? Exception;

            ExecutionOutcome(bool success, bool canceled, bool broken, CommandRunFailureKind failureKind, string message, CommandExceptionInfo? exception)
            {
                Success = success;
                Canceled = canceled;
                Broken = broken;
                FailureKind = failureKind;
                Message = message ?? string.Empty;
                Exception = exception;
            }

            public static ExecutionOutcome FromSuccess() => new(true, false, false, CommandRunFailureKind.None, string.Empty, null);
            public static ExecutionOutcome FromCanceled(string message) => new(false, true, false, CommandRunFailureKind.Canceled, message, null);
            public static ExecutionOutcome FromBreak() => new(false, false, true, CommandRunFailureKind.None, string.Empty, null);
            public static ExecutionOutcome FromFailure(CommandRunFailureKind kind, string message, CommandExceptionInfo? exception)
                => new(false, false, false, kind, message, exception);
        }

        readonly struct FailureDecision
        {
            public readonly bool IsTerminated;
            public readonly CommandRunResult Result;

            FailureDecision(bool isTerminated, CommandRunResult result)
            {
                IsTerminated = isTerminated;
                Result = result;
            }

            public static FailureDecision Terminate(CommandRunResult result) => new(true, result);
            public static FailureDecision Continue() => new(false, default);
        }

        sealed class TraceBuilder
        {
            readonly CommandRunOptions _options;
            readonly List<CommandRunFrame> _stack = new();
            List<CommandRunFrame>? _captured;

            public TraceBuilder(CommandRunOptions options)
            {
                _options = options;
            }

            bool TraceEnabled => _options.TracePolicy != CommandTracePolicy.None &&
                                 _options.MaxTraceDepth > 0 &&
                                 _options.MaxTraceFrames > 0;

            public IReadOnlyList<CommandRunFrame> Trace => _captured ?? (IReadOnlyList<CommandRunFrame>)Array.Empty<CommandRunFrame>();

            public void Push(CommandRunFrame frame)
            {
                if (!TraceEnabled)
                    return;

                if (_stack.Count >= _options.MaxTraceDepth)
                    return;

                _stack.Add(frame);
            }

            public void Pop()
            {
                if (!TraceEnabled || _stack.Count == 0)
                    return;

                _stack.RemoveAt(_stack.Count - 1);
            }

            public void CaptureOnFailure()
            {
                if (!TraceEnabled)
                    return;

                if (_options.TracePolicy == CommandTracePolicy.OnFailure && _captured != null)
                    return;

                Capture();
            }

            public void CaptureAlways()
            {
                if (!TraceEnabled)
                    return;

                if (_options.TracePolicy != CommandTracePolicy.Always)
                    return;

                Capture();
            }

            void Capture()
            {
                var count = Math.Min(_stack.Count, _options.MaxTraceFrames);
                if (count <= 0)
                    return;

                var start = _stack.Count - count;
                _captured ??= new List<CommandRunFrame>(count);
                _captured.Clear();

                for (int i = start; i < _stack.Count; i++)
                    _captured.Add(_stack[i]);
            }
        }
    }
}
