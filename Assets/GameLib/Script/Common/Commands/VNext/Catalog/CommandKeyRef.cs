#nullable enable
using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    [Serializable]
    public struct CommandKeyRef
    {
        [SerializeField, LabelText("Stable Key")]
        string stableKey;

        public string StableKey => stableKey ?? string.Empty;

        public CommandKeyRef(string stableKey)
        {
            this.stableKey = stableKey ?? string.Empty;
        }

        public bool TryResolve(ICommandKeyResolver resolver, out CommandKeyId keyId)
        {
            keyId = default;
            if (resolver == null || string.IsNullOrEmpty(stableKey))
                return false;

            return resolver.TryResolve(stableKey, out keyId);
        }
    }
}
