#nullable enable

namespace Game.Commands.VNext
{
    public sealed class NullCommandResolveLogger : ICommandResolveLogger
    {
        public static readonly NullCommandResolveLogger Instance = new();

        NullCommandResolveLogger() { }

        public void LogResolveFailed(ICommandSource source, string message) { }
        public void LogExecutorMissing(int commandId, string message) { }
        public void LogPayloadInvalid(int commandId, string message) { }
        public void LogExecutionFailed(int commandId, string message) { }
        public void LogExecutionCanceled(int commandId, string message) { }
    }
}
