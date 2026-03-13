#nullable enable
using System;

namespace Game.Commands.VNext
{
    public sealed class CommandExceptionInfo
    {
        public string TypeName { get; }
        public string Message { get; }
        public string StackTrace { get; }

        public CommandExceptionInfo(string typeName, string message, string stackTrace)
        {
            TypeName = typeName ?? string.Empty;
            Message = message ?? string.Empty;
            StackTrace = stackTrace ?? string.Empty;
        }

        public static CommandExceptionInfo FromException(Exception ex, bool includeStackTrace)
        {
            if (ex == null)
                return new CommandExceptionInfo(string.Empty, string.Empty, string.Empty);

            var stack = includeStackTrace ? (ex.StackTrace ?? string.Empty) : string.Empty;
            return new CommandExceptionInfo(ex.GetType().Name, ex.Message, stack);
        }
    }
}
