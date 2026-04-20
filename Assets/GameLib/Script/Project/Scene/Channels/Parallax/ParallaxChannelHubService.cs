#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace Game.Channel
{
    public interface IParallaxChannelHubService : IChannelHubService
    {
        IReadOnlyList<IParallaxChannelPlayer> Players { get; }
        IParallaxChannelPlayer GetPlayer(string tag);
        bool TryGetPlayer(string tag, out IParallaxChannelPlayer player);
    }

    public sealed class ParallaxChannelHubService :
        IParallaxChannelHubService,
        IScopeTickHandler,
        IScopeLateTickHandler,
        IScopeAcquireHandler,
        IScopeReleaseHandler
    {
        sealed class NullParallaxChannelPlayer : IParallaxChannelPlayer
        {
            public static readonly NullParallaxChannelPlayer Instance = new();
            public string Tag => "(null)";
            public bool Enabled => false;
            public void SetEnabled(bool enabled) { }
            public void ToggleEnabled() { }
            public void SetWriteMode(ParallaxWriteMode mode) { }
            public void SetFactor(Vector3 factor) { }
            public void SetExtraOffset(Vector3 offset) { }
            public void SetAffectAxes(bool affectX, bool affectY, bool affectZ) { }
            public void SetSmoothing(bool enabled, float smoothTime) { }
            public void SetMaxOffsetMagnitude(float maxMagnitude) { }
            public void SetUpdateEveryNFrames(int value) { }
            public void SetAllowUnsafeRigidbody2DWrite(bool allow) { }
            public void SetDriverMode(ParallaxDriverMode mode) { }
            public void SetCameraBindMode(ParallaxCameraBindMode mode) { }
            public void SetDirectTarget(Transform? target) { }
            public void SetAnimationChannelTag(string tag) { }
            public void ResetCameraOrigin() { }
            public void ResetRuntimeOverrides() { }
            public void OnAcquire() { }
            public void OnRelease() { }
            public void Tick(float deltaTime, int frameCount) { }
        }

        readonly Dictionary<string, ParallaxChannelDef> _defsByTag = new(StringComparer.Ordinal);
        readonly Dictionary<string, IParallaxChannelPlayer> _playersByTag = new(StringComparer.Ordinal);
        readonly List<IParallaxChannelPlayer> _players = new();
        readonly List<ChannelDefBase> _defsSnapshot = new();
        readonly IScopeNode _scope;
        readonly bool _runInLateUpdate;
        readonly bool _forceTickInRuntime;
        bool _defsDirty = true;

        public IReadOnlyList<IParallaxChannelPlayer> Players => _players;

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

        public ParallaxChannelHubService(ParallaxChannelDef[] defs, IScopeNode scope, bool runInLateUpdate, bool forceTickInRuntime)
        {
            _scope = scope;
            _runInLateUpdate = runInLateUpdate;
            _forceTickInRuntime = forceTickInRuntime;

            if (defs == null)
                return;

            for (int i = 0; i < defs.Length; i++)
            {
                RegisterChannelInternal(defs[i], overwrite: false);
            }
        }

        public IParallaxChannelPlayer GetPlayer(string tag)
        {
            if (TryGetPlayer(tag, out var player) && player != null)
                return player;

            Debug.LogError($"[ParallaxChannelHub] Channel not found: '{tag}'.");
            return NullParallaxChannelPlayer.Instance;
        }

        public bool TryGetPlayer(string tag, out IParallaxChannelPlayer player)
        {
            if (string.IsNullOrWhiteSpace(tag))
                tag = "default";

            if (_playersByTag.TryGetValue(tag, out player) && player != null)
                return true;

            player = NullParallaxChannelPlayer.Instance;
            return false;
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
            if (def is not ParallaxChannelDef typed)
                return false;

            return RegisterChannelInternal(typed, overwrite);
        }

        public bool UnregisterChannel(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                tag = "default";

            return RemoveChannelInternal(tag);
        }

        public void Tick()
        {
            if (_runInLateUpdate && !_forceTickInRuntime)
                return;

            TickInternal();
        }

        public void LateTick()
        {
            if (_forceTickInRuntime)
                return;

            if (!_runInLateUpdate)
                return;

            TickInternal();
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            for (int i = 0; i < _players.Count; i++)
                _players[i].OnAcquire();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            for (int i = 0; i < _players.Count; i++)
                _players[i].OnRelease();
        }

        void TickInternal()
        {
            var deltaTime = Time.deltaTime;
            var frame = Time.frameCount;
            for (int i = 0; i < _players.Count; i++)
            {
                _players[i].Tick(deltaTime, frame);
            }
        }

        bool RegisterChannelInternal(ParallaxChannelDef def, bool overwrite)
        {
            if (def == null || string.IsNullOrWhiteSpace(def.Tag))
                return false;

            if (_playersByTag.ContainsKey(def.Tag))
            {
                if (!overwrite)
                    return false;

                RemoveChannelInternal(def.Tag);
            }

            var player = new ParallaxChannelPlayer(def, _scope);
            _playersByTag[def.Tag] = player;
            _defsByTag[def.Tag] = def;
            _players.Add(player);
            _defsDirty = true;
            return true;
        }

        bool RemoveChannelInternal(string tag)
        {
            if (!_playersByTag.TryGetValue(tag, out var player) || player == null)
                return false;

            player.OnRelease();
            _playersByTag.Remove(tag);
            _defsByTag.Remove(tag);
            _players.Remove(player);
            _defsDirty = true;
            return true;
        }
    }
}
