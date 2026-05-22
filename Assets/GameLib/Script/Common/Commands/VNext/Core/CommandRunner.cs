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
        ICommandDetachedRunner,
        ICommandRunnerDefaultVarsProvider,
        ICommandRunnerActivity
    {
        readonly IScopeNode _scope;
        readonly ICommandExecutorCatalog _executorCatalog;
        readonly ICommandCatalog _catalog;
        readonly ICommandKeyResolver _keyResolver;
        readonly ICommandResolveLogger _logger;
        readonly CommandPayloadValidationContext _payloadValidationContext;
        readonly VarStorePayload? _defaultVarsPayload;
        protected readonly VarStore _runnerVars = new();
        readonly Dictionary<IScopeNode, int> _runningByScope = new();
        readonly Dictionary<IScopeNode, UniTaskCompletionSource> _scopeIdleWaiters = new();
        UniTaskCompletionSource? _idleWaiter;
        int _runningExecutionCount;
        int _nextFrameId;

        public IScopeNode Scope => _scope;
        public int RunningExecutionCount => _runningExecutionCount;
        public bool IsExecuting => _runningExecutionCount > 0;

        public CommandRunner(
            IScopeNode scope,
            ICommandExecutorCatalog executorCatalog,
            ICommandCatalog catalog,
            ICommandKeyResolver keyResolver,
            ICommandResolveLogger logger,
            ICommandPayloadFieldReaderProvider payloadFieldReaderProvider,
            ICommandPayloadReferenceValidator payloadReferenceValidator,
            VarStorePayload? defaultVars = null)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));
            _executorCatalog = executorCatalog ?? throw new ArgumentNullException(nameof(executorCatalog));
            _catalog = catalog ?? NullCommandCatalog.Instance;
            _keyResolver = keyResolver ?? NullCommandKeyResolver.Instance;
            _logger = logger ?? NullCommandResolveLogger.Instance;
            _payloadValidationContext = new CommandPayloadValidationContext(
                _catalog,
                payloadFieldReaderProvider ?? SelfCommandPayloadFieldReaderProvider.Instance,
                payloadReferenceValidator ?? MissingCommandPayloadReferenceValidator.Instance);
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
                return new CommandRunResult(CommandRunStatus.Error, CommandRunFailureKind.ResolveFailed, 1, -1, 0, "Command data is null.", trace.Trace, null, effectiveCtx.CurrentFrame, normalized.FailureBoundary);
            }

            if (data.CommandId <= 0)
            {
                trace.CaptureOnFailure();
                return new CommandRunResult(CommandRunStatus.Error, CommandRunFailureKind.InvalidArgs, 1, 0, 0, "CommandId is invalid.", trace.Trace, null, effectiveCtx.CurrentFrame, normalized.FailureBoundary);
            }

            if (!_executorCatalog.TryGet(data.CommandId, out var executor) || executor == null)
            {
                var contextInfo = BuildExecutionContextDescription(effectiveCtx, data, source: null);
                _logger.LogExecutorMissing(data.CommandId, $"Executor is missing. {contextInfo}");
                trace.CaptureOnFailure();
                return new CommandRunResult(CommandRunStatus.Error, CommandRunFailureKind.ExecutorMissing, 1, 0, 0, $"Executor not found for CommandId={data.CommandId}. CmdData={data.DebugData}", trace.Trace, null, effectiveCtx.CurrentFrame, normalized.FailureBoundary);
            }

            CommandPayloadValidationResult payloadValidation = CommandPayloadValidator.Validate(data, _payloadValidationContext);
            if (!payloadValidation.IsValid)
            {
                var contextInfo = BuildExecutionContextDescription(effectiveCtx, data, source: null);
                string validationMessage = BuildPayloadValidationMessage(contextInfo, payloadValidation, listInfo: string.Empty);
                _logger.LogPayloadInvalid(data.CommandId, validationMessage);
                trace.CaptureOnFailure();
                return new CommandRunResult(CommandRunStatus.Error, CommandRunFailureKind.PayloadInvalid, 1, 0, 0, payloadValidation.Message, trace.Trace, null, effectiveCtx.CurrentFrame, normalized.FailureBoundary);
            }

            var frameCancellationSource = CreateFrameCancellationSource(ct);
            var commandFrame = CreateFrame(effectiveCtx, commandIndex: 0, data, sourceType: "Direct", dataType: data.GetType().Name, debugData: data.DebugData, frameCancellationSource, normalized, CommandLocalScope.Frame);
            var frameCtx = effectiveCtx.AttachFrame(commandFrame, normalized.FailureBoundary, isDetached: false, isTimedOut: false);
            var frame = new CommandRunFrame(commandFrame.ToSnapshot(normalized.FailureBoundary, isDetached: false, isTimedOut: false));
            trace.Push(frame);
            using var runtimeTrace = CommandExecutionTrace.Push(
                BuildExecutionTraceSnapshot(
                    frameCtx,
                    data,
                    source: null,
                    commandIndex: 0,
                    listLabel: "Direct",
                    listFunctionName: null));
            try
            {
                var outcome = await ExecuteDataAsync(executor, data, frameCtx, commandFrame.CancellationToken, normalized);

                if (outcome.Canceled)
                {
                    trace.CaptureOnFailure();
                    trace.Pop();
                    return new CommandRunResult(CommandRunStatus.Canceled, CommandRunFailureKind.Canceled, 1, 0, 0, outcome.Message, trace.Trace, null, frameCtx.CurrentFrame, normalized.FailureBoundary, outcome.TimedOut);
                }

                if (outcome.Broken)
                {
                    trace.CaptureAlways();
                    trace.Pop();
                    return new CommandRunResult(CommandRunStatus.Break, CommandRunFailureKind.None, 0, 0, -1, string.Empty, trace.Trace, null, frameCtx.CurrentFrame, normalized.FailureBoundary);
                }

                if (!outcome.Success)
                {
                    trace.CaptureOnFailure();
                    trace.Pop();
                    return new CommandRunResult(CommandRunStatus.Error, outcome.FailureKind, 1, 0, 0, outcome.Message, trace.Trace, outcome.Exception, frameCtx.CurrentFrame, normalized.FailureBoundary, outcome.TimedOut);
                }

                trace.CaptureAlways();
                trace.Pop();
                return new CommandRunResult(CommandRunStatus.Completed, CommandRunFailureKind.None, 0, 0, -1, string.Empty, trace.Trace, null, frameCtx.CurrentFrame, normalized.FailureBoundary);
            }
            finally
            {
                frameCtx.Local.Dispose();
            }
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
                return new CommandRunResult(CommandRunStatus.Completed, CommandRunFailureKind.None, 0, -1, -1, string.Empty, null, null, effectiveCtx.CurrentFrame, normalized.FailureBoundary);

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
                    return new CommandRunResult(CommandRunStatus.Canceled, CommandRunFailureKind.Canceled, 1, lastIndex, i, "Canceled.", trace.Trace, null, effectiveCtx.CurrentFrame, normalized.FailureBoundary);
                }

                var source = commandList.GetAt(i);
                if (source == null)
                {
                    var fail = HandleFailure(CommandRunFailureKind.ResolveFailed, i, "Command source is null.");
                    if (fail.IsTerminated)
                        return new CommandRunResult(fail.Result.Status, fail.Result.FailureKind, fail.Result.FailureCount, fail.Result.LastIndex, fail.Result.ErrorIndex, fail.Result.Message, fail.Result.Trace, fail.Result.Exception, effectiveCtx.CurrentFrame, normalized.FailureBoundary, fail.Result.TimedOut);
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
                    var contextInfo = BuildExecutionContextDescription(effectiveCtx, null, source);
                    _logger.LogResolveFailed(source, $"Command source threw: {ex.GetType().Name} | {contextInfo} | {listInfo}");
                    var info = CommandExceptionInfo.FromException(ex, includeStackTrace: ShouldIncludeStackTrace(normalized));
                    var fail = HandleFailure(CommandRunFailureKind.Exception, i, ex.Message, info);
                    if (fail.IsTerminated)
                        return new CommandRunResult(fail.Result.Status, fail.Result.FailureKind, fail.Result.FailureCount, fail.Result.LastIndex, fail.Result.ErrorIndex, fail.Result.Message, fail.Result.Trace, fail.Result.Exception, effectiveCtx.CurrentFrame, normalized.FailureBoundary, fail.Result.TimedOut);
                    continue;
                }

                if (!resolved || data == null)
                {
                    var listInfo = BuildListInfo(i, listLabel, listFunctionName);
                    var contextInfo = BuildExecutionContextDescription(effectiveCtx, null, source);
                    _logger.LogResolveFailed(source, $"Command source failed to resolve. {contextInfo} | {listInfo}");
                    var sourceName = source?.DebugName ?? "<null>";
                    var fail = HandleFailure(
                        CommandRunFailureKind.ResolveFailed,
                        i,
                        $"Command source failed to resolve. Source={sourceName} {listInfo}");
                    if (fail.IsTerminated)
                        return new CommandRunResult(fail.Result.Status, fail.Result.FailureKind, fail.Result.FailureCount, fail.Result.LastIndex, fail.Result.ErrorIndex, fail.Result.Message, fail.Result.Trace, fail.Result.Exception, effectiveCtx.CurrentFrame, normalized.FailureBoundary, fail.Result.TimedOut);
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
                        return new CommandRunResult(fail.Result.Status, fail.Result.FailureKind, fail.Result.FailureCount, fail.Result.LastIndex, fail.Result.ErrorIndex, fail.Result.Message, fail.Result.Trace, fail.Result.Exception, effectiveCtx.CurrentFrame, normalized.FailureBoundary, fail.Result.TimedOut);
                    continue;
                }

                if (!_executorCatalog.TryGet(data.CommandId, out var executor) || executor == null)
                {
                    var listInfo = BuildListInfo(i, listLabel, listFunctionName);
                    var contextInfo = BuildExecutionContextDescription(effectiveCtx, data, source);
                    _logger.LogExecutorMissing(data.CommandId, $"Executor is missing. {contextInfo} | {listInfo}");
                    var fail = HandleFailure(CommandRunFailureKind.ExecutorMissing, i, $"Executor not found for CommandId={data.CommandId}. CmdData={data.DebugData}: これが出力される場合、CommandExecutorCatalogに該当のコマンドExecutorが登録されていない可能性があります。");
                    if (fail.IsTerminated)
                        return new CommandRunResult(fail.Result.Status, fail.Result.FailureKind, fail.Result.FailureCount, fail.Result.LastIndex, fail.Result.ErrorIndex, fail.Result.Message, fail.Result.Trace, fail.Result.Exception, effectiveCtx.CurrentFrame, normalized.FailureBoundary, fail.Result.TimedOut);
                    continue;
                }

                CommandPayloadValidationResult payloadValidation = CommandPayloadValidator.Validate(data, _payloadValidationContext);
                if (!payloadValidation.IsValid)
                {
                    var listInfo = BuildListInfo(i, listLabel, listFunctionName);
                    var contextInfo = BuildExecutionContextDescription(effectiveCtx, data, source);
                    string validationMessage = BuildPayloadValidationMessage(contextInfo, payloadValidation, listInfo);
                    _logger.LogPayloadInvalid(data.CommandId, validationMessage);
                    var fail = HandleFailure(CommandRunFailureKind.PayloadInvalid, i, payloadValidation.Message);
                    if (fail.IsTerminated)
                        return new CommandRunResult(fail.Result.Status, fail.Result.FailureKind, fail.Result.FailureCount, fail.Result.LastIndex, fail.Result.ErrorIndex, fail.Result.Message, fail.Result.Trace, fail.Result.Exception, effectiveCtx.CurrentFrame, normalized.FailureBoundary, fail.Result.TimedOut);
                    continue;
                }

                using var frameCancellationSource = CreateFrameCancellationSource(ct);
                var commandFrame = CreateFrame(effectiveCtx, i, data, source?.DebugName ?? "<null>", GetCommandDisplayName(data), data.DebugData, frameCancellationSource, normalized, CommandLocalScope.Frame);
                var frameCtx = effectiveCtx.AttachFrame(commandFrame, normalized.FailureBoundary, isDetached: false, isTimedOut: false);
                var frame = new CommandRunFrame(commandFrame.ToSnapshot(normalized.FailureBoundary, isDetached: false, isTimedOut: false));
                trace.Push(frame);
                using var runtimeTrace = CommandExecutionTrace.Push(
                    BuildExecutionTraceSnapshot(
                    frameCtx,
                        data,
                        source,
                        commandIndex: i,
                        listLabel,
                        listFunctionName));

                var outcome = await ExecuteDataAsync(executor, data, frameCtx, commandFrame.CancellationToken, normalized);

                if (outcome.Canceled)
                {
                    if (!normalized.SuppressCancelLog)
                    {
                        var cancelMsg = BuildCancellationMessage(effectiveCtx, data, source, BuildListInfo(i, listLabel, listFunctionName), outcome.Message);
                        _logger.LogExecutionCanceled(data.CommandId, cancelMsg);
                    }
                    trace.CaptureOnFailure();
                    trace.Pop();
                    return new CommandRunResult(CommandRunStatus.Canceled, CommandRunFailureKind.Canceled, 1, lastIndex, i, outcome.Message, trace.Trace, null, frameCtx.CurrentFrame, normalized.FailureBoundary);
                }

                if (outcome.Broken)
                {
                    trace.CaptureAlways();
                    trace.Pop();
                    return new CommandRunResult(CommandRunStatus.Break, CommandRunFailureKind.None, 0, lastIndex, -1, string.Empty, trace.Trace, null, frameCtx.CurrentFrame, normalized.FailureBoundary);
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
                        return new CommandRunResult(fail.Result.Status, fail.Result.FailureKind, fail.Result.FailureCount, fail.Result.LastIndex, fail.Result.ErrorIndex, fail.Result.Message, fail.Result.Trace, fail.Result.Exception, effectiveCtx.CurrentFrame, normalized.FailureBoundary, fail.Result.TimedOut);
                    continue;
                }

                trace.CaptureAlways();
                trace.Pop();
            }

            return new CommandRunResult(CommandRunStatus.Completed, failureKind, failureCount, lastIndex, errorIndex, message, trace.Trace, exceptionInfo, effectiveCtx.CurrentFrame, normalized.FailureBoundary);

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
                    return FailureDecision.Terminate(new CommandRunResult(CommandRunStatus.Error, kind, failureCount, lastIndex, index, reason ?? string.Empty, trace.Trace, exception, effectiveCtx.CurrentFrame, normalized.FailureBoundary));
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
                using var cleanupCts = options.TimeoutMilliseconds > 0
                    ? new CancellationTokenSource(options.TimeoutMilliseconds)
                    : null;
                var cleanupToken = cleanupCts != null ? cleanupCts.Token : CancellationToken.None;
                _ = await ExecuteListAsync(onCanceled, ctx, cleanupToken, options);
            }

            return result;
        }

        public CommandRunResult StartDetached(
            CommandContext ctx,
            CommandDetachedExecutionPolicy policy,
            CancellationToken callerToken,
            Func<CommandContext, CancellationToken, UniTask<CommandRunResult>> work)
        {
            var effectiveCtx = EnsureContext(ctx, ctx?.Options ?? CommandRunOptions.Default);

            if (!policy.IsAllowed || !policy.OwnerFrameId.IsValid || work == null)
            {
                return new CommandRunResult(
                    CommandRunStatus.Error,
                    CommandRunFailureKind.DetachedPolicyMissing,
                    1,
                    -1,
                    -1,
                    "Detached execution policy is missing or invalid.",
                    null,
                    null,
                    effectiveCtx.CurrentFrame,
                    effectiveCtx.Options.FailureBoundary,
                    detachedFailure: true);
            }
            if (!effectiveCtx.Options.AllowDetachedExecution)
            {
                return new CommandRunResult(
                    CommandRunStatus.Error,
                    CommandRunFailureKind.DetachedPolicyMissing,
                    1,
                    -1,
                    -1,
                    "Detached execution is not enabled for this command.",
                    null,
                    null,
                    effectiveCtx.CurrentFrame,
                    effectiveCtx.Options.FailureBoundary,
                    detachedFailure: true);
            }

            var detachedToken = policy.CancellationMode == CommandDetachedCancellationMode.DetachFromCaller
                ? CancellationToken.None
                : callerToken;
            RunDetachedAsync(effectiveCtx, policy, work, detachedToken).Forget();

            return new CommandRunResult(
                CommandRunStatus.Completed,
                CommandRunFailureKind.None,
                0,
                -1,
                -1,
                string.Empty,
                null,
                null,
                effectiveCtx.CurrentFrame,
                effectiveCtx.Options.FailureBoundary,
                timedOut: false,
                detachedFailure: false);
        }

        public CommandRunResult StartDetachedList(
            CommandListData list,
            CommandListData onCanceled,
            CommandContext ctx,
            CommandDetachedExecutionPolicy policy,
            CancellationToken callerToken,
            CommandRunOptions options)
        {
            var detachedOptions = CommandRunOptions.ResolveOrDefault(options).WithDetachedExecution(true, policy.CancellationMode);
            return StartDetached(
                ctx.WithOptions(detachedOptions),
                policy,
                callerToken,
                (detachedCtx, detachedCt) => onCanceled != null && onCanceled.Count > 0
                    ? ExecuteWithCancelAsync(list, onCanceled, detachedCtx, detachedCt, detachedOptions)
                    : ExecuteListAsync(list, detachedCtx, detachedCt, detachedOptions));
        }

        async UniTaskVoid RunDetachedAsync(
            CommandContext ctx,
            CommandDetachedExecutionPolicy policy,
            Func<CommandContext, CancellationToken, UniTask<CommandRunResult>> work,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await work(ctx, cancellationToken);
                if (result.Status == CommandRunStatus.Error)
                {
                    var commandId = ctx.CurrentFrame.CommandId;
                    var message = $"Detached command failed. OwnerFrame={policy.OwnerFrameId.Value} Destination={policy.DiagnosticDestination} Debug={policy.DebugName} Failure={result.FailureKind} Message={result.Message}";
                    _logger.LogExecutionFailed(commandId, message);
                }
            }
            catch (OperationCanceledException)
            {
                if (!ctx.Options.SuppressCancelLog)
                {
                    var commandId = ctx.CurrentFrame.CommandId;
                    _logger.LogExecutionCanceled(commandId, $"Detached command canceled. OwnerFrame={policy.OwnerFrameId.Value} Debug={policy.DebugName}");
                }
            }
            catch (Exception ex)
            {
                var commandId = ctx.CurrentFrame.CommandId;
                _logger.LogExecutionFailed(commandId, $"Detached command threw. OwnerFrame={policy.OwnerFrameId.Value} Debug={policy.DebugName} Exception={ex.GetType().Name}: {ex.Message}");
            }
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

        static CancellationTokenSource CreateFrameCancellationSource(CancellationToken cancellationToken)
        {
            return cancellationToken.CanBeCanceled
                ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                : new CancellationTokenSource();
        }

        CommandFrame CreateFrame(
            CommandContext ctx,
            int commandIndex,
            ICommandData data,
            string sourceType,
            string dataType,
            string debugData,
            CancellationTokenSource cancellationSource,
            CommandRunOptions options,
            CommandLocalScope localScope)
        {
            var frameId = new CommandFrameId(Interlocked.Increment(ref _nextFrameId));
            var parentFrameId = ctx.CurrentFrame.FrameId;
            var local = new CommandLocal(localScope, frameId, ctx.Local, cancellationSource);
            return new CommandFrame(
                frameId,
                parentFrameId,
                options.ExecutionDomain,
                commandIndex,
                data?.CommandId ?? -1,
                GetPayloadSchemaId(data?.CommandId ?? -1),
                sourceType,
                dataType,
                debugData,
                ctx.Scope,
                ctx.Actor,
                ctx.CommandRootScope,
                ctx.RootActor,
                ctx.CallerActor,
                cancellationSource.Token,
                local);
        }

        int GetPayloadSchemaId(int commandId)
        {
            if (commandId <= 0)
                return 0;

            return _catalog.TryGetPayloadSchema(commandId, out var schema) && schema != null
                ? schema.SchemaId
                : 0;
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

        static string BuildExecutionContextDescription(CommandContext ctx, ICommandData? data, ICommandSource? source)
        {
            var actor = CommandExecutionTrace.DescribeScope(ctx.Actor);
            var scope = CommandExecutionTrace.DescribeScope(ctx.Scope);
            var commandRoot = CommandExecutionTrace.DescribeScope(ctx.CommandRootScope);
            var rootActor = CommandExecutionTrace.DescribeScope(ctx.RootActor);
            var callerActor = CommandExecutionTrace.DescribeScope(ctx.CallerActor);
            var sourceName = source?.DebugName ?? "<null>";
            var commandName = data != null ? GetCommandDisplayName(data) : "<unresolved>";
            var commandId = data?.CommandId ?? -1;
            var debugData = data?.DebugData ?? string.Empty;

            var sb = new StringBuilder();
            sb.Append($"Actor={actor} Scope={scope} CommandRoot={commandRoot} RootActor={rootActor} CallerActor={callerActor} Source={sourceName} Cmd={commandName}(Id={commandId})");

            if (!string.IsNullOrEmpty(debugData))
                sb.Append(" CmdData=").Append(debugData);

            var slotsInfo = BuildContextSlotsDescription(ctx);
            if (!string.IsNullOrEmpty(slotsInfo))
                sb.Append('\n').Append(slotsInfo);

            return sb.ToString();
        }

        static string BuildContextSlotsDescription(CommandContext ctx)
        {
            var sb = new StringBuilder();
            AppendContextSlot(sb, ctx, CommandLtsSlot.ContextA);
            AppendContextSlot(sb, ctx, CommandLtsSlot.ContextB);
            AppendContextSlot(sb, ctx, CommandLtsSlot.ContextC);
            AppendContextSlot(sb, ctx, CommandLtsSlot.ContextD);

            if (sb.Length == 0)
                return string.Empty;

            sb.Insert(0, "ContextSlots:");
            return sb.ToString();
        }

        static void AppendContextSlot(StringBuilder sb, CommandContext ctx, CommandLtsSlot slot)
        {
            var scope = ctx.GetScope(slot);
            if (scope == null)
                return;

            sb.Append(' ').Append(slot).Append('=').Append(CommandExecutionTrace.DescribeScope(scope));
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

        static string BuildPayloadValidationMessage(string contextInfo, CommandPayloadValidationResult result, string listInfo)
        {
            var sb = new StringBuilder(contextInfo);
            if (!string.IsNullOrEmpty(listInfo))
                sb.Append(" | ").Append(listInfo);

            sb.Append("\nDetail: ").Append(result.Message);
            sb.Append("\nPayloadSchemaId: ").Append(result.SchemaId);
            if (!string.IsNullOrEmpty(result.FieldPath))
                sb.Append("\nField: ").Append(result.FieldPath);
            sb.Append("\nExpectedKind: ").Append(result.ExpectedKind);
            sb.Append("\nActualKind: ").Append(result.ActualKind);
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
                        .Append(") FrameId=")
                        .Append(frame.FrameId.Value)
                        .Append(" Parent=")
                        .Append(frame.ParentFrameId.Value)
                        .Append(" Domain=")
                        .Append(frame.Domain)
                        .Append(" Boundary=")
                        .Append(frame.FailureBoundary)
                        .Append(" Local=")
                        .Append(frame.LocalScope);
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

            CancellationTokenSource? timeoutCts = null;
            var executionToken = ct;
            if (options.TimeoutMilliseconds > 0)
            {
                timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(options.TimeoutMilliseconds);
                executionToken = timeoutCts.Token;
            }

            try
            {
                // Scopeが既に破棄されている場合はキャンセル扱いにする
                if (ctx.Scope is UnityEngine.Object scopeObj && !scopeObj)
                    throw new OperationCanceledException("Scope has been destroyed.");

                if (ctx.Actor is UnityEngine.Object actorObj && !actorObj)
                    throw new OperationCanceledException("Actor has been destroyed.");

                if (executionToken.IsCancellationRequested)
                    return ExecutionOutcome.FromCanceled("Canceled.");

                if (executor != null)
                {
                    await executor.Execute(data, ctx, executionToken);
                }
                else
                {
                    return ExecutionOutcome.FromFailure(CommandRunFailureKind.ExecutorMissing, "Executor is null.", null);
                }

                if (ctx.TryConsumeCancelRequest())
                    return ExecutionOutcome.FromCanceled("Canceled by command.");

                if (ctx.TryConsumeBreakRequest())
                    return ExecutionOutcome.FromBreak();

                return ExecutionOutcome.FromSuccess();
            }
            catch (OperationCanceledException) when (timeoutCts != null && timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
            {
                return ExecutionOutcome.FromTimeout($"Timeout. TimeoutMilliseconds={options.TimeoutMilliseconds}.");
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
            finally
            {
                timeoutCts?.Dispose();
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
            public readonly bool TimedOut;
            public readonly CommandRunFailureKind FailureKind;
            public readonly string Message;
            public readonly CommandExceptionInfo? Exception;

            ExecutionOutcome(bool success, bool canceled, bool broken, bool timedOut, CommandRunFailureKind failureKind, string message, CommandExceptionInfo? exception)
            {
                Success = success;
                Canceled = canceled;
                Broken = broken;
                TimedOut = timedOut;
                FailureKind = failureKind;
                Message = message ?? string.Empty;
                Exception = exception;
            }

            public static ExecutionOutcome FromSuccess() => new(true, false, false, false, CommandRunFailureKind.None, string.Empty, null);
            public static ExecutionOutcome FromCanceled(string message) => new(false, true, false, false, CommandRunFailureKind.Canceled, message, null);
            public static ExecutionOutcome FromTimeout(string message) => new(false, false, false, true, CommandRunFailureKind.Timeout, message, null);
            public static ExecutionOutcome FromBreak() => new(false, false, true, false, CommandRunFailureKind.None, string.Empty, null);
            public static ExecutionOutcome FromFailure(CommandRunFailureKind kind, string message, CommandExceptionInfo? exception)
                => new(false, false, false, false, kind, message, exception);
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
