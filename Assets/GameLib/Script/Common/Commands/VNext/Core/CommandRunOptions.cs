#nullable enable

namespace Game.Commands.VNext
{
    public readonly struct CommandRunOptions
    {
        public readonly CommandFailurePolicy FailurePolicy;
        public readonly bool AllowActorFallback;
        public readonly bool AllowRuntimeKeyFallback;
        public readonly CommandTracePolicy TracePolicy;
        public readonly int MaxTraceDepth;
        public readonly int MaxTraceFrames;
        public readonly bool SuppressCancelLog;

        public CommandRunOptions(
            CommandFailurePolicy failurePolicy,
            bool allowActorFallback,
            bool allowRuntimeKeyFallback,
            CommandTracePolicy tracePolicy,
            int maxTraceDepth,
            int maxTraceFrames,
            bool suppressCancelLog)
        {
            FailurePolicy = failurePolicy;
            AllowActorFallback = allowActorFallback;
            AllowRuntimeKeyFallback = allowRuntimeKeyFallback;
            TracePolicy = tracePolicy;
            MaxTraceDepth = maxTraceDepth;
            MaxTraceFrames = maxTraceFrames;
            SuppressCancelLog = suppressCancelLog;
        }

        public static CommandRunOptions Default => new(
            CommandFailurePolicy.FailFast,
            allowActorFallback: false,
            allowRuntimeKeyFallback: false,
            CommandTracePolicy.OnFailure,
            maxTraceDepth: 32,
            maxTraceFrames: 256,
            suppressCancelLog: false);

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
                AllowActorFallback,
                AllowRuntimeKeyFallback,
                TracePolicy,
                MaxTraceDepth,
                MaxTraceFrames,
                suppress);
        }
    }
}
