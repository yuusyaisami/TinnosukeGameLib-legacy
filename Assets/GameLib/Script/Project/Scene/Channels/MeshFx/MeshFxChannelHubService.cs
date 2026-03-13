#nullable enable
using System;
using System.Collections.Generic;
using Game.Collision;
using Game.MaterialFx;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Channel
{
    public interface IMeshFxChannelHubService : IChannelHubService
    {
        IReadOnlyList<IMeshFxChannelPlayer> Players { get; }

        IMeshFxChannelPlayer GetPlayer(string tag);
        bool TryGetPlayer(string tag, out IMeshFxChannelPlayer player);
    }

    public sealed class MeshFxChannelHubService :
        IMeshFxChannelHubService,
        IScopeAcquireHandler,
        IScopeReleaseHandler,
        ITickable,
        IDisposable
    {
        const string DefaultTag = "default";

        readonly Dictionary<string, MeshFxChannelDef> _defsByTag = new(StringComparer.Ordinal);
        readonly Dictionary<string, MeshFxChannelPlayer> _playersByTag = new(StringComparer.Ordinal);
        readonly HashSet<string> _disabledDuplicateTags = new(StringComparer.Ordinal);

        readonly List<IMeshFxChannelPlayer> _playerList = new();
        readonly List<ChannelDefBase> _defsSnapshot = new();

        readonly IScopeNode _ownerScope;
        readonly IMaterialFxServiceFactory? _materialFxFactory;
        readonly ICollisionService? _collisionService;
        readonly IHitColliderScopeRegistry? _hitScopeRegistry;
        readonly IHitColliderChannelHub? _hitChannelHub;

        bool _defsDirty = true;
        int _frameIndex;

        public IReadOnlyList<IMeshFxChannelPlayer> Players => _playerList;

        public IReadOnlyList<ChannelDefBase> ChannelDefs
        {
            get
            {
                if (_defsDirty)
                {
                    _defsSnapshot.Clear();
                    foreach (var def in _defsByTag.Values)
                    {
                        _defsSnapshot.Add(def);
                    }

                    _defsDirty = false;
                }

                return _defsSnapshot;
            }
        }

        public MeshFxChannelHubService(
            MeshFxChannelDef[] channelDefs,
            IScopeNode ownerScope,
            IMaterialFxServiceFactory? materialFxFactory = null,
            ICollisionService? collisionService = null,
            IHitColliderScopeRegistry? hitScopeRegistry = null,
            IHitColliderChannelHub? hitChannelHub = null)
        {
            _ownerScope = ownerScope;

            var resolver = ownerScope?.Resolver;
            if (materialFxFactory == null && resolver != null)
                resolver.TryResolve(out materialFxFactory);
            if (collisionService == null && resolver != null)
                resolver.TryResolve(out collisionService);
            if (hitScopeRegistry == null && resolver != null)
                resolver.TryResolve(out hitScopeRegistry);
            if (hitChannelHub == null && resolver != null)
                resolver.TryResolve(out hitChannelHub);

            _materialFxFactory = materialFxFactory;
            _collisionService = collisionService;
            _hitScopeRegistry = hitScopeRegistry;
            _hitChannelHub = hitChannelHub;

            if (channelDefs == null)
                return;

            for (int i = 0; i < channelDefs.Length; i++)
            {
                RegisterChannelInternal(channelDefs[i], overwrite: false, channelIndex: i);
            }
        }

        public IMeshFxChannelPlayer GetPlayer(string tag)
        {
            tag = NormalizeTag(tag);

            if (_playersByTag.TryGetValue(tag, out var player) && player != null)
                return player;

            throw new KeyNotFoundException($"[MeshFxHub] Channel '{tag}' not found.");
        }

        public bool TryGetPlayer(string tag, out IMeshFxChannelPlayer player)
        {
            tag = NormalizeTag(tag);

            if (_playersByTag.TryGetValue(tag, out var p) && p != null)
            {
                player = p;
                return true;
            }

            player = null!;
            return false;
        }

        public bool TryGetChannelDef(string tag, out ChannelDefBase def)
        {
            tag = NormalizeTag(tag);

            if (_defsByTag.TryGetValue(tag, out var meshDef) && meshDef != null)
            {
                def = meshDef;
                return true;
            }

            def = null!;
            return false;
        }

        public bool RegisterChannel(ChannelDefBase def, bool overwrite = false)
        {
            if (def is not MeshFxChannelDef meshDef)
                return false;

            return RegisterChannelInternal(meshDef, overwrite, channelIndex: _playerList.Count);
        }

        public bool UnregisterChannel(string tag)
        {
            tag = NormalizeTag(tag);
            return RemoveChannelInternal(tag);
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            for (int i = 0; i < _playerList.Count; i++)
            {
                if (_playerList[i] is MeshFxChannelPlayer player)
                    player.OnAcquire();
            }
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            for (int i = 0; i < _playerList.Count; i++)
            {
                if (_playerList[i] is MeshFxChannelPlayer player)
                    player.OnRelease();
            }
        }

        public void Tick()
        {
            _frameIndex++;
            var dt = Time.deltaTime;
            for (int i = 0; i < _playerList.Count; i++)
            {
                if (_playerList[i] is MeshFxChannelPlayer player)
                    player.Tick(_frameIndex, dt);
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < _playerList.Count; i++)
            {
                _playerList[i]?.Dispose();
            }

            _playerList.Clear();
            _playersByTag.Clear();
            _defsByTag.Clear();
            _defsSnapshot.Clear();
            _disabledDuplicateTags.Clear();
            _defsDirty = true;
        }

        bool RegisterChannelInternal(MeshFxChannelDef def, bool overwrite, int channelIndex)
        {
            if (def == null)
                return false;

            var tag = NormalizeTag(def.Tag);
            if (string.IsNullOrWhiteSpace(tag))
                return false;

            if (_disabledDuplicateTags.Contains(tag) && !overwrite)
                return false;

            if (_playersByTag.ContainsKey(tag))
            {
                if (!overwrite)
                {
                    // 仕様: duplicate tag は後勝ち禁止。両方を無効化。
                    RemoveChannelInternal(tag);
                    _disabledDuplicateTags.Add(tag);
                    Debug.LogWarning($"[MeshFxHub] Duplicate tag '{tag}' detected. Both channels are disabled.");
                    return false;
                }

                RemoveChannelInternal(tag);
                _disabledDuplicateTags.Remove(tag);
            }

            var player = new MeshFxChannelPlayer(
                def,
                _ownerScope,
                channelIndex,
                _materialFxFactory,
                _collisionService,
                _hitScopeRegistry,
                _hitChannelHub);

            _defsByTag[tag] = def;
            _playersByTag[tag] = player;
            _playerList.Add(player);
            _defsDirty = true;

            return true;
        }

        bool RemoveChannelInternal(string tag)
        {
            if (!_playersByTag.TryGetValue(tag, out var player) || player == null)
                return false;

            player.Dispose();

            _playersByTag.Remove(tag);
            _defsByTag.Remove(tag);
            _playerList.Remove(player);

            _defsDirty = true;
            return true;
        }

        static string NormalizeTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return DefaultTag;
            return tag;
        }
    }
}
