#nullable enable
using System;
using System.Collections.Generic;
using DG.Tweening;
using Game.Common;
using UnityEngine;

namespace Game.VariableLayer
{
    public interface IVariableLayerValueResolver
    {
        bool TryGetResolvedValue(int nodeId, out VariableLayerValue value);
    }

    public interface IVariableLayerService : IVariableLayerValueResolver
    {
        bool SetEntry(int nodeId, string tag, VariableLayerValue value, float fadeSeconds, float lifetimeSeconds, Ease ease);
        bool RemoveTag(int nodeId, string tag);
        bool ClearContext(string tag);
        bool ClearNode(int nodeId);
        void ResetDefaults();
        void Tick(float deltaTime);
        void GetDirtyNodeIds(List<int> output);
        void ClearDirtyNode(int nodeId);
    }

    public sealed class VariableLayerRuntime : IVariableLayerService
    {
        sealed class DefaultSnapshot
        {
            public VariableLayerValue Value;
        }

        sealed class EntryState
        {
            public string Tag = DefaultTag;
            public VariableLayerValue StartValue;
            public VariableLayerValue TargetValue;
            public float StartTimeSeconds;
            public float DurationSeconds;
            public float CreatedAtSeconds;
            public float LifetimeSeconds;
            public Ease Ease;

            public float EndTimeSeconds => StartTimeSeconds + DurationSeconds;
            public float ExpireTimeSeconds => LifetimeSeconds < 0f ? float.PositiveInfinity : CreatedAtSeconds + LifetimeSeconds;

            public VariableLayerValue Evaluate(float timeSeconds)
            {
                if (DurationSeconds <= 0f)
                    return TargetValue;

                var t = Mathf.Clamp01((timeSeconds - StartTimeSeconds) / DurationSeconds);
                var eased = DOVirtual.EasedValue(0f, 1f, t, Ease);
                return VariableLayerValueUtility.Lerp(StartValue, TargetValue, eased);
            }
        }

        sealed class NodeState
        {
            public VariableRegistryNode Node;
            public readonly Dictionary<string, EntryState> Entries = new(StringComparer.Ordinal);
            public DefaultSnapshot? InitialDefault;

            public NodeState(VariableRegistryNode node)
            {
                Node = node;
            }
        }

        const string DefaultTag = "default";

        readonly IVariablePropertyRegistry _registry;
        readonly Dictionary<int, NodeState> _nodes = new();
        readonly List<int> _dirtyNodeIds = new();
        readonly Dictionary<int, int> _dirtyNodeIndices = new();
        readonly List<string> _removeTags = new();

        float _currentTimeSeconds;

        public VariableLayerRuntime(IVariablePropertyRegistry registry)
        {
            _registry = registry;
        }

        public bool SetEntry(int nodeId, string tag, VariableLayerValue value, float fadeSeconds, float lifetimeSeconds, Ease ease)
        {
            if (!_registry.TryGetNode(nodeId, out var node))
                return false;
            if (node.ValueType != value.Kind)
                return false;

            var normalizedTag = NormalizeTag(tag);
            var nodeState = GetOrCreateNodeState(node);
            var startValue = value;
            if (nodeState.Entries.TryGetValue(normalizedTag, out var currentEntry))
                startValue = currentEntry.Evaluate(_currentTimeSeconds);

            var durationSeconds = Mathf.Max(0f, fadeSeconds);
            var nextEntry = new EntryState
            {
                Tag = normalizedTag,
                StartValue = durationSeconds > 0f ? startValue : value,
                TargetValue = value,
                StartTimeSeconds = _currentTimeSeconds,
                DurationSeconds = durationSeconds,
                CreatedAtSeconds = _currentTimeSeconds,
                LifetimeSeconds = lifetimeSeconds,
                Ease = ease,
            };

            nodeState.Entries[normalizedTag] = nextEntry;
            if (string.Equals(normalizedTag, DefaultTag, StringComparison.Ordinal) && nodeState.InitialDefault == null)
                nodeState.InitialDefault = new DefaultSnapshot { Value = value };

            MarkDirty(nodeId);
            return true;
        }

        public bool RemoveTag(int nodeId, string tag)
        {
            if (!_nodes.TryGetValue(nodeId, out var nodeState))
                return false;

            var removed = nodeState.Entries.Remove(NormalizeTag(tag));
            if (!removed)
                return false;

            EnsureRestoredDefault(nodeState);
            MarkDirty(nodeId);
            return true;
        }

        public bool ClearContext(string tag)
        {
            var normalizedTag = NormalizeTag(tag);
            var any = false;
            foreach (var pair in _nodes)
            {
                if (!pair.Value.Entries.Remove(normalizedTag))
                    continue;

                EnsureRestoredDefault(pair.Value);
                MarkDirty(pair.Key);
                any = true;
            }

            return any;
        }

        public bool ClearNode(int nodeId)
        {
            if (!_nodes.TryGetValue(nodeId, out var nodeState))
                return false;

            nodeState.Entries.Clear();
            EnsureRestoredDefault(nodeState);
            MarkDirty(nodeId);
            return true;
        }

        public void ResetDefaults()
        {
            foreach (var pair in _nodes)
            {
                pair.Value.Entries.Clear();
                EnsureRestoredDefault(pair.Value);
                MarkDirty(pair.Key);
            }
        }

        public void Tick(float deltaTime)
        {
            var previousTime = _currentTimeSeconds;
            _currentTimeSeconds += Mathf.Max(0f, deltaTime);

            foreach (var pair in _nodes)
            {
                var nodeId = pair.Key;
                var nodeState = pair.Value;
                var becameDirty = false;

                _removeTags.Clear();
                foreach (var entryPair in nodeState.Entries)
                {
                    var entry = entryPair.Value;
                    if (entry.LifetimeSeconds >= 0f && previousTime < entry.ExpireTimeSeconds && _currentTimeSeconds >= entry.ExpireTimeSeconds)
                    {
                        _removeTags.Add(entryPair.Key);
                        becameDirty = true;
                        continue;
                    }

                    if (entry.DurationSeconds <= 0f)
                        continue;

                    var wasAnimating = previousTime < entry.EndTimeSeconds;
                    var isAnimating = _currentTimeSeconds < entry.EndTimeSeconds;
                    if (wasAnimating || isAnimating)
                        becameDirty = true;
                }

                for (var i = 0; i < _removeTags.Count; i++)
                    nodeState.Entries.Remove(_removeTags[i]);

                if (_removeTags.Count > 0)
                    EnsureRestoredDefault(nodeState);

                if (becameDirty)
                    MarkDirty(nodeId);
            }
        }

        public void GetDirtyNodeIds(List<int> output)
        {
            if (output == null)
                return;

            output.Clear();
            output.AddRange(_dirtyNodeIds);
        }

        public void ClearDirtyNode(int nodeId)
        {
            if (!_dirtyNodeIndices.TryGetValue(nodeId, out var index))
                return;

            var lastIndex = _dirtyNodeIds.Count - 1;
            var lastNodeId = _dirtyNodeIds[lastIndex];
            _dirtyNodeIds[index] = lastNodeId;
            _dirtyNodeIndices[lastNodeId] = index;
            _dirtyNodeIds.RemoveAt(lastIndex);
            _dirtyNodeIndices.Remove(nodeId);
        }

        public bool TryGetResolvedValue(int nodeId, out VariableLayerValue value)
        {
            if (!_nodes.TryGetValue(nodeId, out var nodeState))
            {
                value = default;
                return false;
            }

            value = ResolveNodeValue(nodeState);
            return true;
        }

        NodeState GetOrCreateNodeState(VariableRegistryNode node)
        {
            if (_nodes.TryGetValue(node.Id, out var existing))
                return existing;

            var created = new NodeState(node);
            _nodes.Add(node.Id, created);
            return created;
        }

        VariableLayerValue ResolveNodeValue(NodeState nodeState)
        {
            if (nodeState.Entries.Count == 0)
                return VariableLayerValue.GetDefault(nodeState.Node.ValueType);

            var resolved = nodeState.Entries.TryGetValue(DefaultTag, out var defaultEntry)
                ? defaultEntry.Evaluate(_currentTimeSeconds)
                : VariableLayerValue.GetDefault(nodeState.Node.ValueType);

            foreach (var pair in nodeState.Entries)
            {
                if (string.Equals(pair.Key, DefaultTag, StringComparison.Ordinal))
                    continue;

                resolved = VariableLayerValueUtility.Add(resolved, pair.Value.Evaluate(_currentTimeSeconds));
            }

            return resolved;
        }

        void EnsureRestoredDefault(NodeState nodeState)
        {
            if (nodeState.Entries.Count > 0 || nodeState.InitialDefault == null)
                return;

            nodeState.Entries[DefaultTag] = new EntryState
            {
                Tag = DefaultTag,
                StartValue = nodeState.InitialDefault.Value,
                TargetValue = nodeState.InitialDefault.Value,
                StartTimeSeconds = _currentTimeSeconds,
                DurationSeconds = 0f,
                CreatedAtSeconds = _currentTimeSeconds,
                LifetimeSeconds = -1f,
                Ease = Ease.Linear,
            };
        }

        void MarkDirty(int nodeId)
        {
            if (_dirtyNodeIndices.ContainsKey(nodeId))
                return;

            _dirtyNodeIndices.Add(nodeId, _dirtyNodeIds.Count);
            _dirtyNodeIds.Add(nodeId);
        }

        static string NormalizeTag(string? tag)
        {
            return string.IsNullOrWhiteSpace(tag) ? DefaultTag : tag.Trim();
        }
    }
}
