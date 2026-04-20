#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Channel
{
    public sealed class Light2DChannelHubService :
        ILight2DChannelHubService,
        IScopeAcquireHandler,
        IScopeReleaseHandler,
        IScopeTickHandler
    {
        readonly Dictionary<string, Light2DChannelDef> _defsByTag = new(StringComparer.Ordinal);
        readonly Dictionary<string, Light2DChannelPlayerRuntime> _playersByTag = new(StringComparer.Ordinal);
        readonly List<Light2DChannelPlayerRuntime> _playerList = new();
        readonly List<ChannelDefBase> _defsSnapshot = new();
        readonly Dictionary<string, Light2DChannelPlayerRuntime> _primaryGlobalProviders = new(StringComparer.Ordinal);

        readonly IScopeNode _ownerScope;
        bool _defsDirty = true;

        public IReadOnlyList<ILight2DChannelPlayer> Players => _playerList;
        public IScopeNode OwnerScope => _ownerScope;

        public IReadOnlyList<ChannelDefBase> ChannelDefs
        {
            get
            {
                if (_defsDirty)
                {
                    _defsSnapshot.Clear();
                    foreach (var def in _defsByTag.Values)
                        _defsSnapshot.Add(def);
                    _defsDirty = false;
                }

                return _defsSnapshot;
            }
        }

        public Light2DChannelHubService(Light2DChannelDef[] channelDefs, IScopeNode ownerScope)
        {
            _ownerScope = ownerScope ?? throw new ArgumentNullException(nameof(ownerScope));

            if (channelDefs == null)
                return;

            for (var i = 0; i < channelDefs.Length; i++)
                RegisterChannelInternal(channelDefs[i], overwrite: false);
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _ = scope;

            RebuildPrimaryGlobalProviders();

            for (var i = 0; i < _playerList.Count; i++)
            {
                var player = _playerList[i];
                player.PresetRuntime.OnAcquire(_ownerScope, isReset);
            }

            for (var i = 0; i < _playerList.Count; i++)
                _playerList[i].OnAcquire(_ownerScope, isReset);
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _ = scope;

            for (var i = _playerList.Count - 1; i >= 0; i--)
                _playerList[i].OnRelease(_ownerScope, isReset);

            for (var i = _playerList.Count - 1; i >= 0; i--)
                _playerList[i].PresetRuntime.OnRelease(_ownerScope, isReset);
        }

        public void Tick()
        {
            var deltaTime = Time.deltaTime;
            for (var i = 0; i < _playerList.Count; i++)
                _playerList[i].Tick(deltaTime);
        }

        public bool TryGetPlayer(string tag, out ILight2DChannelPlayer? player)
        {
            player = null;
            if (!_playersByTag.TryGetValue(NormalizeTag(tag), out var runtime))
                return false;

            player = runtime;
            return true;
        }

        public bool TryGetControl(string tag, out ILight2DChannelControlService? control)
        {
            control = null;
            if (!_playersByTag.TryGetValue(NormalizeTag(tag), out var runtime))
                return false;

            control = runtime;
            return true;
        }

        public bool SwapSourcePreset(string tag, Light2DPreset? preset)
        {
            if (!_playersByTag.TryGetValue(NormalizeTag(tag), out var runtime))
                return false;

            return runtime.PresetRuntime.SwapSourcePreset(preset);
        }

        public bool TryGetChannelDef(string tag, out ChannelDefBase def)
        {
            if (_defsByTag.TryGetValue(NormalizeTag(tag), out var resolved))
            {
                def = resolved;
                return true;
            }

            def = default!;
            return false;
        }

        public bool RegisterChannel(ChannelDefBase def, bool overwrite = false)
        {
            if (def is not Light2DChannelDef typed)
                return false;

            return RegisterChannelInternal(typed, overwrite);
        }

        public bool UnregisterChannel(string tag)
        {
            return RemoveChannelInternal(NormalizeTag(tag));
        }

        internal bool TryGetPrimaryGlobalProvider(string globalLinkKey, out Light2DChannelPlayerRuntime? player)
        {
            player = null;
            if (string.IsNullOrWhiteSpace(globalLinkKey))
                return false;

            return _primaryGlobalProviders.TryGetValue(globalLinkKey.Trim(), out player) && player != null;
        }

        internal void NotifyGlobalIntensityChanged(string globalLinkKey)
        {
            if (string.IsNullOrWhiteSpace(globalLinkKey))
                return;

            foreach (var node in ScopeNodeHierarchy.EnumerateSubtree(_ownerScope, includeSelf: false))
            {
                if (!TryResolveOwnedHub(node, out var hub) || hub == null)
                    continue;

                hub.MarkGlobalDirtyForLink(globalLinkKey);
            }
        }

        internal void MarkGlobalDirtyForLink(string globalLinkKey)
        {
            if (string.IsNullOrWhiteSpace(globalLinkKey))
                return;

            var normalized = globalLinkKey.Trim();
            for (var i = 0; i < _playerList.Count; i++)
            {
                var player = _playerList[i];
                if (!string.Equals(player.GlobalLinkKey, normalized, StringComparison.Ordinal))
                    continue;

                player.MarkInheritedGlobalDirty();
            }
        }

        bool RegisterChannelInternal(Light2DChannelDef def, bool overwrite)
        {
            if (def == null)
                return false;

            var ownerTransform = _ownerScope.Identity?.SelfTransform;
            if (ownerTransform != null)
                def.EnsureIntegrity(ownerTransform);

            if (def.TargetLight == null)
                return false;

            var tag = NormalizeTag(def.Tag);
            if (_playersByTag.ContainsKey(tag))
            {
                if (!overwrite)
                    return false;

                RemoveChannelInternal(tag);
            }

            var presetRuntime = new Light2DChannelPresetRuntime(def);
            var player = new Light2DChannelPlayerRuntime(_ownerScope, this, def, presetRuntime);
            _defsByTag[tag] = def;
            _playersByTag[tag] = player;
            _playerList.Add(player);
            _defsDirty = true;

            RebuildPrimaryGlobalProviders();
            return true;
        }

        bool RemoveChannelInternal(string tag)
        {
            if (!_playersByTag.TryGetValue(tag, out var player))
                return false;

            player.OnRelease(_ownerScope, false);
            player.PresetRuntime.OnRelease(_ownerScope, false);

            _playersByTag.Remove(tag);
            _playerList.Remove(player);
            _defsByTag.Remove(tag);
            _defsDirty = true;
            RebuildPrimaryGlobalProviders();
            return true;
        }

        void RebuildPrimaryGlobalProviders()
        {
            _primaryGlobalProviders.Clear();

            for (var i = 0; i < _playerList.Count; i++)
            {
                var player = _playerList[i];
                var key = player.GlobalLinkKey;
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                if (_primaryGlobalProviders.ContainsKey(key))
                {
                    Debug.LogWarning($"[Light2DChannelHub] Duplicate GlobalLinkKey '{key}' detected in scope '{DescribeScope(_ownerScope)}'. Only the first channel is used as ancestor provider.");
                    continue;
                }

                _primaryGlobalProviders.Add(key, player);
            }
        }

        internal static bool TryResolveOwnedHub(IScopeNode? scope, out Light2DChannelHubService? hub)
        {
            hub = null;
            var resolver = scope?.Resolver;
            if (resolver == null ||
                !resolver.TryResolve<ILight2DChannelHubService>(out var resolved) ||
                resolved is not Light2DChannelHubService typed ||
                !ReferenceEquals(typed.OwnerScope, scope))
            {
                return false;
            }

            hub = typed;
            return true;
        }

        static string NormalizeTag(string? tag)
        {
            return string.IsNullOrWhiteSpace(tag) ? "default" : tag.Trim();
        }

        static string DescribeScope(IScopeNode? scope)
        {
            var transform = scope?.Identity?.SelfTransform;
            if (transform != null)
                return transform.name;

            return scope?.GetType().Name ?? "(null)";
        }
    }
}
