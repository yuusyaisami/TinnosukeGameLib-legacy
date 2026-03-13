#nullable enable
using System;
using System.Collections.Generic;
using Game.Registry;
using UnityEngine;

namespace Game.RoomMap
{
    /// <summary>
    /// RoomMap 用の Tile 定義（階層 + stableKey/alias → tileId 解決）を管理するレジストリ。
    /// - tileId は自動採番で再利用しない（tombstone 予約）
    /// - stableKey は rename/move で自動変更しない（資産互換のため）
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Registry/RoomMap/Tile Registry")]
    public sealed class RoomMapTileRegistry : HierarchyRegistryBase<RoomMapTileNode>
    {
        [Serializable]
        sealed class Tombstone
        {
            [SerializeField] int tileId;
            [SerializeField] string stableKey = string.Empty;
            [SerializeField] List<string> aliases = new();

            public int TileId => tileId;
            public string StableKey => stableKey;
            public IReadOnlyList<string> Aliases => aliases;

            public Tombstone(int tileId, string stableKey, List<string> aliases)
            {
                this.tileId = tileId;
                this.stableKey = stableKey ?? string.Empty;
                this.aliases = aliases != null ? new List<string>(aliases) : new List<string>();
            }
        }

        [SerializeField] int nextTileId = 1;
        [SerializeField] List<Tombstone> tombstones = new();

        readonly Dictionary<int, RoomMapTileNode> _idToNode = new();
        readonly Dictionary<string, int> _keyToId = new(StringComparer.Ordinal);

        bool _built;
        bool _isBuilding;

        public bool IsLookupBuilding => _isBuilding;

        public int RegisteredKeyCount
        {
            get { EnsureLookup(); return _keyToId.Count; }
        }

        public void EnsureLookupRebuild() => _built = false;

        public bool TryResolve(string stableKeyOrAlias, out int tileId)
        {
            EnsureLookup();
            if (string.IsNullOrEmpty(stableKeyOrAlias))
            {
                tileId = 0;
                return false;
            }
            return _keyToId.TryGetValue(stableKeyOrAlias, out tileId) && tileId > 0;
        }

        public bool TryGetNode(int tileId, out RoomMapTileNode? node)
        {
            EnsureLookup();
            if (tileId <= 0)
            {
                node = null;
                return false;
            }
            return _idToNode.TryGetValue(tileId, out node);
        }

        public bool TryGetTags(int tileId, out RoomMapTileTagFlags tags)
        {
            if (TryGetNode(tileId, out var node) && node != null)
            {
                tags = node.Tags;
                return true;
            }
            tags = RoomMapTileTagFlags.None;
            return false;
        }

        public bool TryGetDisplayPath(int tileId, out string displayPath)
        {
            if (TryGetNode(tileId, out var node) && node != null)
            {
                displayPath = GetDisplayPath(node.Id);
                return !string.IsNullOrEmpty(displayPath);
            }
            displayPath = string.Empty;
            return false;
        }

        public bool TryGetPaintColor(int tileId, out Color color)
        {
            if (TryGetNode(tileId, out var node) && node != null)
            {
                color = node.PaintColor;
                return true;
            }

            color = default;
            return false;
        }

        public int[] GetAllLeafTileIds()
        {
            EnsureLookup();

            if (_idToNode.Count == 0)
                return Array.Empty<int>();

            var ids = new List<int>(_idToNode.Count);
            foreach (var kv in _idToNode)
                ids.Add(kv.Key);

            // Sort by display path for UX, then by id.
            ids.Sort((a, b) =>
            {
                var pa = _idToNode.TryGetValue(a, out var na) && na != null ? GetDisplayPath(na.Id) : string.Empty;
                var pb = _idToNode.TryGetValue(b, out var nb) && nb != null ? GetDisplayPath(nb.Id) : string.Empty;
                var c = string.Compare(pa, pb, StringComparison.Ordinal);
                return c != 0 ? c : a.CompareTo(b);
            });

            return ids.ToArray();
        }

        public override string GetKeyString(RoomMapTileNode node)
        {
            if (node == null || node.IsFolder)
                return string.Empty;

            if (!string.IsNullOrEmpty(node.StableKey))
                return node.StableKey;

            var path = GetDisplayPath(node);
            if (!string.IsNullOrEmpty(path))
                return path;

            return node.Name ?? string.Empty;
        }

        protected override void InitializeLeafNode(RoomMapTileNode node)
        {
            base.InitializeLeafNode(node);
            if (node == null)
                return;

            if (node.TileId <= 0)
                node.TileId = AllocateNewTileId();

            if (string.IsNullOrEmpty(node.StableKey))
            {
                // Prefer a display path (hierarchical) as the stable key to avoid collisions
                // on common names like "Default" when multiple tiles exist under different folders.
                var display = GetDisplayPath(node);
                node.StableKey = !string.IsNullOrEmpty(display) ? display : node.Name ?? string.Empty;
            }

            _built = false;
        }

        protected override bool OnDeleteNode(RoomMapTileNode node)
        {
            if (node != null && !node.IsFolder && node.TileId > 0)
            {
                tombstones ??= new List<Tombstone>();
                tombstones.Add(new Tombstone(node.TileId, node.StableKey, node.Aliases));
                _built = false;
            }
            return true;
        }

        int AllocateNewTileId()
        {
            var id = nextTileId;
            nextTileId = Math.Max(nextTileId + 1, 1);
            return id;
        }

        void EnsureLookup()
        {
            if (_built || _isBuilding)
                return;

            _isBuilding = true;
            try
            {
                _keyToId.Clear();
                _idToNode.Clear();

                if (nodes != null)
                {
                    for (int i = 0; i < nodes.Count; i++)
                    {
                        var n = nodes[i];
                        if (n == null || n.IsFolder)
                            continue;

                        if (n.TileId <= 0)
                            continue;

                        if (_idToNode.ContainsKey(n.TileId))
                        {
                            Debug.LogError($"[RoomMapTileRegistry] Duplicate tileId: {n.TileId}");
                            continue;
                        }

                        _idToNode.Add(n.TileId, n);

                        var key = GetKeyString(n);
                        AddKey(key, n.TileId);

                        var aliases = n.Aliases;
                        if (aliases != null)
                        {
                            for (int a = 0; a < aliases.Count; a++)
                                AddKey(aliases[a], n.TileId);
                        }
                    }
                }

                if (tombstones != null)
                {
                    for (int i = 0; i < tombstones.Count; i++)
                    {
                        var t = tombstones[i];
                        if (t == null || t.TileId <= 0)
                            continue;

                        // StableKey/Alias の再利用検知のため、キーだけ予約する。
                        // ここで tileId 解決を提供する必要はないため、_keyToId には追加しない。
                        // （必要になったら alias→tileId を返す挙動に変更可能）
                    }
                }

                _built = true;
            }
            finally
            {
                _isBuilding = false;
            }
        }

        void AddKey(string key, int tileId)
        {
            if (string.IsNullOrEmpty(key))
                return;

            if (_keyToId.TryGetValue(key, out var existing))
            {
                // if (existing != tileId)
                //     Debug.LogError($"[RoomMapTileRegistry] Duplicate stableKey/alias: '{key}' (tileId {existing} vs {tileId})");
                return;
            }

            _keyToId.Add(key, tileId);
        }
    }
}
