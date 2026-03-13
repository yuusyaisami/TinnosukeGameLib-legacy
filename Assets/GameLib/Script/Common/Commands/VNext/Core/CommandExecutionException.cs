#nullable enable
using System;

namespace Game.Commands.VNext
{
    public sealed class CommandExecutionException : Exception
    {
        public CommandRunFailureKind FailureKind { get; }

        public CommandExecutionException(CommandRunFailureKind failureKind, string message)
            : base(message)
        {
            FailureKind = failureKind;
        }
    }
}
