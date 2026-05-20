#nullable enable

namespace Game.Commands.VNext
{
    public interface ICommandResolveLogger
    {
        void LogResolveFailed(ICommandSource source, string message);
        void LogExecutorMissing(int commandId, string message);
        void LogPayloadInvalid(int commandId, string message);
        void LogExecutionFailed(int commandId, string message);
        void LogExecutionCanceled(int commandId, string message);
    }
}
