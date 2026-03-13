using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Input
{
    /// <summary>
    /// Central ref-count action block manager. Similar to InputBlocker but for categorized action kinds.
    /// Services that are blockable register/unregister themselves and can query block state.
    /// </summary>
    public sealed class ActionBlockService : IActionBlockService
    {
        readonly struct BlockEntry
        {
            public BlockEntry(string kind, string layerKey)
            {
                Kind = kind;
                LayerKey = layerKey;
            }

            public string Kind { get; }
            public string LayerKey { get; }
        }

        readonly Dictionary<int, BlockEntry> _active = new();
        readonly Dictionary<string, HashSet<string>> _flagKeys = new(StringComparer.Ordinal);
        readonly List<IActionBlockable> _blockables = new();
        int _nextId;

        public IReadOnlyList<IActionBlockable> RegisteredBlockables => _blockables;

        public IDisposable Block(string kinds, string reason = null)
        {
            if (string.IsNullOrEmpty(kinds))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (!string.IsNullOrEmpty(reason))
                {
                    Debug.LogWarning($"[ActionBlockService] Tried to block empty kinds. reason={reason}");
                }
#endif
                return EmptyToken.Instance;
            }

            var id = ++_nextId;
            var layerKey = $"token:{id}";
            _active[id] = new BlockEntry(kinds, layerKey);

            ApplyLayer(kinds, layerKey, true);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!string.IsNullOrEmpty(reason))
            {
                Debug.Log($"[ActionBlockService] Blocked (id={id}, kinds={kinds}) reason={reason}");
            }
#endif

            return new BlockToken(this, id);
        }

        public bool IsBlocked(string kind)
        {
            if (string.IsNullOrEmpty(kind))
                return false;

            foreach (var entry in _active.Values)
            {
                if (string.Equals(entry.Kind, kind, StringComparison.Ordinal))
                    return true;
            }

            if (_flagKeys.TryGetValue(kind, out var keys) && keys.Count > 0)
                return true;

            return false;
        }

        public void SetBlockFlag(string kinds, bool blocked, string reason = null)
        {
            if (string.IsNullOrEmpty(kinds))
                return;

            var key = string.IsNullOrEmpty(reason) ? "flag:manual" : $"flag:{reason}";

            if (blocked)
            {
                if (!_flagKeys.TryGetValue(kinds, out var set))
                {
                    set = new HashSet<string>(StringComparer.Ordinal);
                    _flagKeys[kinds] = set;
                }

                if (set.Add(key))
                {
                    ApplyLayer(kinds, key, true);
                }
            }
            else
            {
                if (_flagKeys.TryGetValue(kinds, out var set) && set.Remove(key))
                {
                    ApplyLayer(kinds, key, false);
                    if (set.Count == 0)
                        _flagKeys.Remove(kinds);
                }
            }
        }

        public void RegisterBlockable(IActionBlockable blockable)
        {
            if (blockable == null)
                return;

            if (!_blockables.Contains(blockable))
            {
                _blockables.Add(blockable);
                ReapplyActiveBlocks(blockable);
            }
        }

        public void UnregisterBlockable(IActionBlockable blockable)
        {
            if (blockable == null)
                return;

            _blockables.Remove(blockable);
        }

        internal void Release(int id)
        {
            if (_active.TryGetValue(id, out var entry) && _active.Remove(id))
            {
                ApplyLayer(entry.Kind, entry.LayerKey, false);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[ActionBlockService] Released (id={id})");
#endif
            }
        }

        void ApplyLayer(string kind, string layerKey, bool blocked)
        {
            if (string.IsNullOrEmpty(kind) || string.IsNullOrEmpty(layerKey))
                return;

            for (var i = 0; i < _blockables.Count; i++)
            {
                var blockable = _blockables[i];
                if (!string.Equals(blockable.ActionBlockKind, kind, StringComparison.Ordinal))
                    continue;

                var layer = blockable.BlockLayer;
                if (layer == null)
                    continue;

                if (blocked)
                    layer.Set(layerKey, true);
                else
                    layer.Remove(layerKey);
            }
        }

        void ReapplyActiveBlocks(IActionBlockable blockable)
        {
            if (blockable == null || blockable.BlockLayer == null)
                return;

            var kind = blockable.ActionBlockKind;
            if (string.IsNullOrEmpty(kind))
                return;

            foreach (var entry in _active.Values)
            {
                if (string.Equals(entry.Kind, kind, StringComparison.Ordinal))
                {
                    blockable.BlockLayer.Set(entry.LayerKey, true);
                }
            }

            if (_flagKeys.TryGetValue(kind, out var set))
            {
                foreach (var key in set)
                {
                    blockable.BlockLayer.Set(key, true);
                }
            }
        }

        sealed class BlockToken : IDisposable
        {
            readonly ActionBlockService _owner;
            readonly int _id;
            bool _disposed;

            public BlockToken(ActionBlockService owner, int id)
            {
                _owner = owner;
                _id = id;
            }

            public void Dispose()
            {
                if (_disposed)
                    return;

                _disposed = true;
                _owner.Release(_id);
            }
        }

        sealed class EmptyToken : IDisposable
        {
            public static readonly EmptyToken Instance = new();
            public void Dispose() { }
        }
    }
}
