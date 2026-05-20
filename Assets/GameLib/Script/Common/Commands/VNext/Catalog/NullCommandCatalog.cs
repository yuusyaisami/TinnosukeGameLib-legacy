#nullable enable

namespace Game.Commands.VNext
{
    public sealed class NullCommandCatalog : ICommandCatalog
    {
        public static readonly NullCommandCatalog Instance = new();

        NullCommandCatalog() { }

        public bool TryResolve(CommandKeyId keyId, out ICommandData data)
        {
            data = null!;
            return false;
        }

        public bool TryResolve(CommandKeyRef key, out ICommandData data)
        {
            data = null!;
            return false;
        }

        public bool TryGetMeta(CommandKeyRef key, out CommandCatalogMeta meta)
        {
            meta = null!;
            return false;
        }

        public bool TryGetPayloadSchema(int commandId, out CommandPayloadSchema schema)
        {
            schema = null!;
            return false;
        }
    }
}
