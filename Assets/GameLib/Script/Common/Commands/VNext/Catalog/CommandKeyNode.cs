#nullable enable
using System;
using System.Collections.Generic;
using Game.Registry;
using UnityEngine;

namespace Game.Commands.VNext
{
    [Serializable]
    public sealed class CommandKeyNode : HierarchyNodeBase
    {
        [SerializeField] int keyId;
        [SerializeField] string stableKey = string.Empty;
        [SerializeField] List<string> aliases = new();

        public int KeyId { get => keyId; set => keyId = value; }
        public string StableKey { get => stableKey; set => stableKey = value ?? string.Empty; }
        public List<string> Aliases => aliases;
    }
}
