#nullable enable

namespace Game.Commands.VNext
{
    public readonly struct CommandRunOptions
    {
        public readonly CommandFailurePolicy FailurePolicy;
        public readonly CommandFailureBoundary FailureBoundary;
        public readonly CommandExecutionDomain ExecutionDomain;
        public readonly bool AllowActorFallback;
        public readonly bool AllowRuntimeKeyFallback;
        public readonly CommandTracePolicy TracePolicy;
        public readonly int MaxTraceDepth;
        public readonly int MaxTraceFrames;
        public readonly bool SuppressCancelLog;
        public readonly int TimeoutMilliseconds;
        public readonly bool AllowDetachedExecution;
        public readonly CommandDetachedCancellationMode DetachedCancellationMode;

        public CommandRunOptions(
            CommandFailurePolicy failurePolicy,
            bool allowActorFallback,
            bool allowRuntimeKeyFallback,
            CommandTracePolicy tracePolicy,
            int maxTraceDepth,
            int maxTraceFrames,
            bool suppressCancelLog)
            : this(
                failurePolicy,
                CommandFailureBoundary.FailFrame,
                CommandExecutionDomain.Project,
                allowActorFallback,
                allowRuntimeKeyFallback,
                tracePolicy,
                maxTraceDepth,
                maxTraceFrames,
                suppressCancelLog,
                timeoutMilliseconds: 0,
                allowDetachedExecution: false,
                CommandDetachedCancellationMode.FollowCaller)
        {
        }

        public CommandRunOptions(
            CommandFailurePolicy failurePolicy,
            CommandFailureBoundary failureBoundary,
            CommandExecutionDomain executionDomain,
            bool allowActorFallback,
            bool allowRuntimeKeyFallback,
            CommandTracePolicy tracePolicy,
            int maxTraceDepth,
            int maxTraceFrames,
            bool suppressCancelLog,
            int timeoutMilliseconds,
            bool allowDetachedExecution,
            CommandDetachedCancellationMode detachedCancellationMode)
        {
            FailurePolicy = failurePolicy;
            FailureBoundary = failureBoundary == default ? CommandFailureBoundary.FailFrame : failureBoundary;
            ExecutionDomain = executionDomain == default ? CommandExecutionDomain.Project : executionDomain;
            AllowActorFallback = allowActorFallback;
            AllowRuntimeKeyFallback = allowRuntimeKeyFallback;
            TracePolicy = tracePolicy;
            MaxTraceDepth = maxTraceDepth;
            MaxTraceFrames = maxTraceFrames;
            SuppressCancelLog = suppressCancelLog;
            TimeoutMilliseconds = timeoutMilliseconds > 0 ? timeoutMilliseconds : 0;
            AllowDetachedExecution = allowDetachedExecution;
            DetachedCancellationMode = detachedCancellationMode == default
                ? CommandDetachedCancellationMode.FollowCaller
                : detachedCancellationMode;
        }

        public static CommandRunOptions Default => new(
            CommandFailurePolicy.FailFast,
                CommandFailureBoundary.FailFrame,
                CommandExecutionDomain.Project,
            allowActorFallback: false,
            allowRuntimeKeyFallback: false,
            CommandTracePolicy.OnFailure,
            maxTraceDepth: 32,
            maxTraceFrames: 256,
                suppressCancelLog: false,
                timeoutMilliseconds: 0,
                allowDetachedExecution: false,
                CommandDetachedCancellationMode.FollowCaller);

        public static CommandRunOptions ResolveOrDefault(CommandRunOptions options)
        {
            if (options.Equals(default(CommandRunOptions)))
                return Default;
            return options;
        }

        public bool IsTraceEnabled => TracePolicy != CommandTracePolicy.None &&
                                      MaxTraceDepth > 0 &&
                                      MaxTraceFrames > 0;

        public CommandRunOptions WithSuppressCancelLog(bool suppress)
        {
            if (SuppressCancelLog == suppress)
                return this;

            return new CommandRunOptions(
                FailurePolicy,
                FailureBoundary,
                ExecutionDomain,
                AllowActorFallback,
                AllowRuntimeKeyFallback,
                TracePolicy,
                MaxTraceDepth,
                MaxTraceFrames,
                suppress,
                TimeoutMilliseconds,
                AllowDetachedExecution,
                DetachedCancellationMode);
        }

        public CommandRunOptions WithDetachedExecution(bool allowDetachedExecution, CommandDetachedCancellationMode cancellationMode)
        {
            return new CommandRunOptions(
                FailurePolicy,
                FailureBoundary,
                ExecutionDomain,
                AllowActorFallback,
                AllowRuntimeKeyFallback,
                TracePolicy,
                MaxTraceDepth,
                MaxTraceFrames,
                SuppressCancelLog,
                TimeoutMilliseconds,
                allowDetachedExecution,
                cancellationMode);
        }

        public CommandRunOptions WithFramePolicy(CommandExecutionDomain executionDomain, CommandFailureBoundary failureBoundary, int timeoutMilliseconds)
        {
            return new CommandRunOptions(
                FailurePolicy,
                failureBoundary,
                executionDomain,
                AllowActorFallback,
                AllowRuntimeKeyFallback,
                TracePolicy,
                MaxTraceDepth,
                MaxTraceFrames,
                SuppressCancelLog,
                timeoutMilliseconds,
                AllowDetachedExecution,
                DetachedCancellationMode);
        }
    }
}
