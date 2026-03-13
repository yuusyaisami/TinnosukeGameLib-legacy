#nullable enable

namespace Game.Commands.VNext
{
    public interface ICommandData
    {
        int CommandId { get; }
        string DebugData { get; }
    }

    public interface ICommandRuntimeStateFactory
    {
        object CreateState();
    }
}
