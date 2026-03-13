#nullable enable

namespace Game.Commands.VNext
{
    public interface ICommandSource
    {
        string DebugName { get; }
        bool TryResolve(CommandResolveContext ctx, out ICommandData data);
    }
}
