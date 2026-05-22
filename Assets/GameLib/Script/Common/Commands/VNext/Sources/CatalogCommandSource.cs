#nullable enable
using System;
using UnityEngine;

namespace Game.Commands.VNext
{
    [Serializable]
    public sealed class CatalogCommandSource : ICommandSource, ICommandSourceExecutionControl
    {
        [SerializeField] bool enabled = true;
        [SerializeField] CommandKeyRef key;

        public string DebugName => BuildDebugName();
        public bool IsExecutionEnabled => enabled;

        public void SetExecutionEnabled(bool value)
        {
            enabled = value;
        }

        public bool TryResolve(CommandResolveContext ctx, out ICommandData data)
        {
            data = null!;
            if (string.IsNullOrEmpty(key.StableKey))
            {
                ctx.Logger.LogResolveFailed(this, "CommandKeyRef is empty.");
                return false;
            }

            if (!TryResolveKeyId(ctx, key.StableKey, out var keyId) || !keyId.IsValid)
            {
                ctx.Logger.LogResolveFailed(this, $"CommandKeyRef '{key.StableKey}' failed to resolve.");
                return false;
            }

            if (!ctx.Catalog.TryResolve(keyId, out data) || data == null)
            {
                ctx.Logger.LogResolveFailed(this, $"Catalog entry not found for keyId={keyId.Value}.");
                data = null!;
                return false;
            }

            return true;
        }

        string BuildDebugName()
        {
            var k = key.StableKey;
            if (string.IsNullOrEmpty(k))
                return "CatalogCommandSource (<empty>)";
            return $"CatalogCommandSource ('{k}')";
        }

        public override string ToString()
        {
            return DebugName;
        }

        static bool TryResolveKeyId(CommandResolveContext ctx, string stableKey, out CommandKeyId keyId)
        {
            keyId = default;
            return ctx.KeyResolver.TryResolve(stableKey, out keyId);
        }
    }
}
