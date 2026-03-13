#nullable enable

namespace Game.Commands.VNext
{
    public sealed class NullCommandKeyResolver : ICommandKeyResolver
    {
        public static readonly NullCommandKeyResolver Instance = new();

        NullCommandKeyResolver() { }

        public bool TryResolve(string stableKey, out CommandKeyId keyId)
        {
            keyId = default;
            return false;
        }

        public bool TryGetStableKey(CommandKeyId keyId, out string stableKey)
        {
            stableKey = string.Empty;
            return false;
        }
    }
}
