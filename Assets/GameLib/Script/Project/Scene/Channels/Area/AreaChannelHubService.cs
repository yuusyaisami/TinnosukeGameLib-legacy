#nullable enable
using System;
using System.Collections.Generic;
using Game;
using UnityEngine;

namespace Game.Channel
{
    public sealed class AreaChannelHubService : IAreaChannelHubService, IScopeAcquireHandler, IScopeReleaseHandler
    {
        readonly Dictionary<string, AreaChannelDefinition> _defsByTag = new(StringComparer.Ordinal);
        readonly Dictionary<string, AreaChannelRuntimePlayer> _runtimeByTag = new(StringComparer.Ordinal);
        readonly List<ChannelDefBase> _defsSnapshot = new();
        bool _defsDirty = true;

        IScopeNode? _ownerScope;

        public IReadOnlyList<ChannelDefBase> ChannelDefs
        {
            get
            {
                if (_defsDirty)
                {
                    _defsSnapshot.Clear();
                    foreach (var item in _defsByTag.Values)
                        _defsSnapshot.Add(item);
                    _defsDirty = false;
                }

                return _defsSnapshot;
            }
        }

        public AreaChannelHubService(AreaChannelDefinition[] definitions)
        {
            if (definitions == null)
                return;

            for (int i = 0; i < definitions.Length; i++)
                RegisterChannelInternal(definitions[i], overwrite: false);
        }

        public bool TrySamplePosition(string tag, in AreaSampleRequest request, out Vector3 position)
        {
            position = default;

            if (!TryGetPlayer(tag, out var player) || player == null)
                return false;

            if (!TryResolveBasePosition(player.Definition, _ownerScope, out var basePosition))
                return false;

            return player.TrySamplePosition(basePosition, in request, out position);
        }

        public bool TrySamplePosition(IReadOnlyList<string> tags, AreaTagSelectionMode selectionMode, in AreaSampleRequest request, out Vector3 position, out string selectedTag)
        {
            position = default;
            selectedTag = string.Empty;

            if (tags == null || tags.Count == 0)
                return false;

            if (selectionMode != AreaTagSelectionMode.RandomOne)
                selectionMode = AreaTagSelectionMode.RandomOne;

            var count = tags.Count;
            var start = UnityEngine.Random.Range(0, count);
            for (int i = 0; i < count; i++)
            {
                var idx = (start + i) % count;
                var tag = tags[idx];
                if (!TryGetPlayer(tag, out var player) || player == null)
                    continue;

                if (!TryResolveBasePosition(player.Definition, _ownerScope, out var basePosition))
                    continue;

                if (!player.TrySamplePosition(basePosition, in request, out position))
                    continue;

                selectedTag = string.IsNullOrWhiteSpace(tag) ? "default" : tag;
                return true;
            }

            return false;
        }

        public bool TryGetPlayer(string tag, out IAreaChannelPlayer player)
        {
            if (!TryGetRuntime(tag, out var runtime))
            {
                player = null!;
                return false;
            }

            player = runtime;
            return true;
        }

        public bool ContainsPosition(string tag, Vector3 worldPosition)
        {
            if (!TryGetPlayer(tag, out var player) || player == null)
                return false;

            if (!TryResolveBasePosition(player.Definition, _ownerScope, out var basePosition))
                return false;

            return player.ContainsPosition(basePosition, worldPosition);
        }

        public bool TryGetChannelDef(string tag, out ChannelDefBase def)
        {
            if (string.IsNullOrWhiteSpace(tag))
                tag = "default";

            if (_defsByTag.TryGetValue(tag, out var hit) && hit != null)
            {
                def = hit;
                return true;
            }

            def = null!;
            return false;
        }

        public bool RegisterChannel(ChannelDefBase def, bool overwrite = false)
        {
            if (def is not AreaChannelDefinition typed)
                return false;

            return RegisterChannelInternal(typed, overwrite);
        }

        public bool UnregisterChannel(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                tag = "default";

            if (!_defsByTag.Remove(tag))
                return false;

            _runtimeByTag.Remove(tag);
            _defsDirty = true;
            return true;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _ownerScope = scope;
            foreach (var runtime in _runtimeByTag.Values)
                runtime.ResetRuntime();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _ownerScope = null;
        }

        bool RegisterChannelInternal(AreaChannelDefinition? def, bool overwrite)
        {
            if (def == null)
                return false;

            if (string.IsNullOrWhiteSpace(def.Tag))
                return false;

            if (_defsByTag.ContainsKey(def.Tag))
            {
                if (!overwrite)
                    return false;

                _defsByTag.Remove(def.Tag);
                _runtimeByTag.Remove(def.Tag);
            }

            _defsByTag[def.Tag] = def;
            _runtimeByTag[def.Tag] = new AreaChannelRuntimePlayer(def);
            _defsDirty = true;
            return true;
        }

        bool TryGetRuntime(string tag, out AreaChannelRuntimePlayer runtime)
        {
            if (string.IsNullOrWhiteSpace(tag))
                tag = "default";

            if (_runtimeByTag.TryGetValue(tag, out runtime) && runtime != null && runtime.Definition.Enabled)
                return true;

            runtime = null!;
            return false;
        }

        static bool TryResolveBasePosition(AreaChannelDefinition def, IScopeNode? ownerScope, out Vector3 basePosition)
        {
            var anchor = def.Anchor;
            if (anchor == null)
            {
                var ownerTf = ownerScope?.Identity?.SelfTransform;
                if (ownerTf == null)
                {
                    basePosition = default;
                    return false;
                }
                anchor = ownerTf;
            }

            basePosition = anchor.position + def.CenterOffset;
            return true;
        }
    }
}
