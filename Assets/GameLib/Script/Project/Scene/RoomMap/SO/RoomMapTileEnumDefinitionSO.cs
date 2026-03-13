#nullable enable
using System;
using System.Collections.Generic;
using Game.EnumLike;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.RoomMap
{
    [Serializable]
    public sealed class RoomMapTileEnumEntry : BaseEnumEntry
    {
        [Tooltip("Stable identity key. Must never change once created.")]
        [SerializeField, ReadOnly]
        string stableKey = string.Empty;

        [Tooltip("Optional: mark as deprecated (do not delete in v0.1).")]
        [SerializeField]
        bool deprecated;

        [Tooltip("AutoTile matching tags.")]
        [SerializeField]
        RoomMapTileTagFlags tags;

        public string StableKey => stableKey;
        public bool Deprecated => deprecated;
        public RoomMapTileTagFlags Tags => tags;

        public void EnsureStableKey()
        {
            if (!string.IsNullOrEmpty(stableKey))
                return;

            stableKey = Guid.NewGuid().ToString("N");
        }
    }

    [CreateAssetMenu(
        fileName = "NewRoomMapTileEnum",
        menuName = "Game/RoomMap/Tile Enum Definition",
        order = 100)]
    public sealed class RoomMapTileEnumDefinitionSO : BaseEnumDefinitionSO<RoomMapTileEnumEntry>
    {
        [Header("RoomMap Stability")]
        [Tooltip("Player でも検証可能な安定 GUID（Asset GUID ではない）")]
        [SerializeField, ReadOnly]
        string stableGuid = string.Empty;

        [Tooltip("entries[].StableKey の配列順（=index順）から計算された schema hash")]
        [SerializeField, ReadOnly]
        ulong schemaHash;

        public string StableGuid => stableGuid;
        public ulong SchemaHash => schemaHash;

        void OnValidate()
        {
            if (string.IsNullOrEmpty(stableGuid))
                stableGuid = Guid.NewGuid().ToString("N");

            if (entries == null)
                return;

            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                e?.EnsureStableKey();
            }

            schemaHash = ComputeSchemaHash(entries);
        }

        static ulong ComputeSchemaHash(List<RoomMapTileEnumEntry> list)
        {
            if (list == null || list.Count == 0)
                return RoomMapHashUtility.Hash64(string.Empty);

            var keys = new string[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                var k = list[i]?.StableKey;
                keys[i] = string.IsNullOrEmpty(k) ? string.Empty : k;
            }

            return RoomMapHashUtility.HashSchemaStableKeys(keys);
        }

        public List<string> GetMissingStableKeys()
        {
            var missing = new List<string>();
            if (entries == null)
                return missing;

            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null)
                {
                    missing.Add($"Entry[{i}] is null");
                    continue;
                }

                if (string.IsNullOrEmpty(e.StableKey))
                    missing.Add($"Entry[{i}] '{e.name}' missing stableKey");
            }

            return missing;
        }
    }
}
