#nullable enable
using System;
using UnityEngine;
using Sirenix.OdinInspector;

namespace Game.Commands.VNext
{
    [Serializable]
    public sealed class CommandCatalogEntry
    {
        [SerializeField] CommandKeyRef key;
        [HideReferenceObjectPicker]
        [SerializeReference] ICommandData? data;
        [SerializeField] CommandCatalogMeta meta = new();

        public CommandKeyRef Key => key;
        public ICommandData? Data => data;
        public CommandCatalogMeta Meta => meta;
    }
}
