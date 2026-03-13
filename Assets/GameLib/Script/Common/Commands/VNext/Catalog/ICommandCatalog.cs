#nullable enable

namespace Game.Commands.VNext
{
    public interface ICommandCatalog
    {
        bool TryResolve(CommandKeyId keyId, out ICommandData data);
        bool TryResolve(CommandKeyRef key, out ICommandData data);
        bool TryGetMeta(CommandKeyRef key, out CommandCatalogMeta meta);
    }
}
