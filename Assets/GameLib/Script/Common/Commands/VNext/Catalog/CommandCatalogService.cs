#nullable enable
using Game;
using UnityEngine;

namespace Game.Commands.VNext
{
    public sealed class CommandCatalogService : ICommandCatalog, IScopeAcquireHandler, IScopeReleaseHandler
    {
        CommandCatalogSO? _catalog;

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _catalog = CommandCatalogLocator.GetOrCreate();
            if (_catalog == null)
                Debug.LogError("[CommandCatalogService] CommandCatalog could not be loaded.");
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _catalog = null;
        }

        public bool TryResolve(CommandKeyId keyId, out ICommandData data)
        {
            data = null!;
            return _catalog != null && _catalog.TryResolve(keyId, out data);
        }

        public bool TryResolve(CommandKeyRef key, out ICommandData data)
        {
            data = null!;
            return _catalog != null && _catalog.TryResolve(key, out data);
        }

        public bool TryGetMeta(CommandKeyRef key, out CommandCatalogMeta meta)
        {
            meta = null!;
            return _catalog != null && _catalog.TryGetMeta(key, out meta);
        }

        public bool TryGetPayloadSchema(int commandId, out CommandPayloadSchema schema)
        {
            schema = null!;
            return _catalog != null && _catalog.TryGetPayloadSchema(commandId, out schema);
        }
    }
}
