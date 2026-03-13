#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Commands.VNext
{
    [Serializable]
    public sealed class CommandCatalogMeta
    {
        [SerializeField] string category = string.Empty;
        [SerializeField, TextArea] string description = string.Empty;
        [SerializeField] List<string> tags = new();

        public string Category => category ?? string.Empty;
        public string Description => description ?? string.Empty;
        public IReadOnlyList<string> Tags => tags;
    }
}
