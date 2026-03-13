#nullable enable

namespace Game.Commands.VNext
{
    public interface ICommandSourceExecutionControl
    {
        bool IsExecutionEnabled { get; }
        void SetExecutionEnabled(bool enabled);
    }
}
