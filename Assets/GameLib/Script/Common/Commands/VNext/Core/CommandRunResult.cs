#nullable enable
using System.Collections.Generic;

namespace Game.Commands.VNext
{
    public readonly struct CommandRunResult
    {
        public CommandRunStatus Status { get; }
        public CommandRunFailureKind FailureKind { get; }
        public int FailureCount { get; }
        public int LastIndex { get; }
        public int ErrorIndex { get; }
        public string Message { get; }
        public IReadOnlyList<CommandRunFrame> Trace { get; }
        public CommandExceptionInfo? Exception { get; }

        public CommandRunResult(
            CommandRunStatus status,
            CommandRunFailureKind failureKind,
            int failureCount,
            int lastIndex,
            int errorIndex,
            string message,
            IReadOnlyList<CommandRunFrame>? trace,
            CommandExceptionInfo? exception)
        {
            Status = status;
            FailureKind = failureKind;
            FailureCount = failureCount;
            LastIndex = lastIndex;
            ErrorIndex = errorIndex;
            Message = message ?? string.Empty;
            Trace = trace ?? System.Array.Empty<CommandRunFrame>();
            Exception = exception;
        }

        public static CommandRunResult Completed(int lastIndex, int failureCount, CommandRunFailureKind failureKind, int errorIndex, string message, IReadOnlyList<CommandRunFrame>? trace, CommandExceptionInfo? exception)
        {
            return new CommandRunResult(CommandRunStatus.Completed, failureKind, failureCount, lastIndex, errorIndex, message, trace, exception);
        }

        public static CommandRunResult Error(int lastIndex, int errorIndex, CommandRunFailureKind failureKind, string message, IReadOnlyList<CommandRunFrame>? trace, CommandExceptionInfo? exception)
        {
            return new CommandRunResult(CommandRunStatus.Error, failureKind, 1, lastIndex, errorIndex, message, trace, exception);
        }

        public static CommandRunResult Canceled(int lastIndex, int errorIndex, string message, IReadOnlyList<CommandRunFrame>? trace)
        {
            return new CommandRunResult(CommandRunStatus.Canceled, CommandRunFailureKind.Canceled, 1, lastIndex, errorIndex, message, trace, null);
        }
    }
}
