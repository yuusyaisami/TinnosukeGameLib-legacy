#nullable enable
using System;
using System.Collections.Generic;
using Game.Registry;
using UnityEngine;

namespace Game.RoomMap
{
    [Serializable]
    public sealed class RoomMapTileNode : HierarchyNodeBase
    {
        [SerializeField] int tileId;

        [Tooltip("Stable identity key. Must not change once created.")]
        [SerializeField] string stableKey = string.Empty;

        [SerializeField] List<string> aliases = new();

        [SerializeField] bool deprecated;

        [SerializeField] RoomMapTileTagFlags tags;

        [SerializeField]
        Color paintColor = new Color(0.62f, 0.80f, 0.95f, 1f);

        public int TileId { get => tileId; set => tileId = value; }
        public string StableKey { get => stableKey; set => stableKey = value ?? string.Empty; }
        public List<string> Aliases => aliases;
        public bool Deprecated { get => deprecated; set => deprecated = value; }
        public RoomMapTileTagFlags Tags { get => tags; set => tags = value; }
        public Color PaintColor { get => paintColor; set => paintColor = value; }
    }
}
