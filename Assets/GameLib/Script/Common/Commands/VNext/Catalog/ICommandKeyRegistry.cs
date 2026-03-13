#nullable enable

namespace Game.Commands.VNext
{
    public interface ICommandKeyRegistry
    {
        bool TryResolve(string stableKeyOrAlias, out CommandKeyId keyId);
        bool TryGetStableKey(CommandKeyId keyId, out string stableKey);
    }
}
