#nullable enable

namespace Game.Commands.VNext
{
    public interface ICommandKeyResolver
    {
        bool TryResolve(string stableKey, out CommandKeyId keyId);
        bool TryGetStableKey(CommandKeyId keyId, out string stableKey);
    }
}
