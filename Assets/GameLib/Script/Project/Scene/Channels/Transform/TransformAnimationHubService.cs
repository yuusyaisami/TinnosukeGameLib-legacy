// Game.Channel.TransformAnimationHubService.cs

using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace Game.Channel
{
    public interface ITransformAnimationHubService : IChannelHubService
    {
        IReadOnlyList<ITransformAnimationChannelPlayer> Players { get; }
        bool EnableDebugLog { get; }

        ITransformAnimationChannelPlayer GetPlayer(string tag);
        bool TryGetPlayer(string tag, out ITransformAnimationChannelPlayer player);
    }

    public sealed class TransformAnimationHubService : ITransformAnimationHubService, IScopeAcquireHandler, IScopeReleaseHandler, ITickable
    {
        readonly Dictionary<string, ITransformAnimationChannelPlayer> _players =
            new(StringComparer.Ordinal);

        readonly Dictionary<string, TransformChannelDef> _defsByTag =
            new(StringComparer.Ordinal);

        readonly List<ITransformAnimationChannelPlayer> _playerList = new();
        readonly List<ChannelDefBase> _defsSnapshot = new();
        readonly IScopeNode _scope;
        readonly bool _enableDebugLog;

        bool _defsDirty = true;

        public IReadOnlyList<ITransformAnimationChannelPlayer> Players => _playerList;
        public bool EnableDebugLog => _enableDebugLog;

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

        public TransformAnimationHubService(TransformChannelDef[] defs, IScopeNode scope, bool enableDebugLog = false)
        {
            _scope = scope;
            _enableDebugLog = enableDebugLog;

            if (defs == null)
                return;

            foreach (var def in defs)
            {
                RegisterChannelInternal(def, overwrite: false);
            }
        }

        // ===== ITransformAnimationHubService =====

        public ITransformAnimationChannelPlayer GetPlayer(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                tag = "default";

            if (_players.TryGetValue(tag, out var player))
                return player;

            throw new KeyNotFoundException($"[TransformAnimationHub] Channel '{tag}' not found.");
        }

        public bool TryGetPlayer(string tag, out ITransformAnimationChannelPlayer player)
        {

            if (string.IsNullOrWhiteSpace(tag))
                tag = "default";

            if (_players.ContainsKey(tag) == false)
            {
                player = null!;
                return false;
            }
            _players.TryGetValue(tag, out player);


            return player != null;
        }

        // ===== IChannelHubService =====

        public bool TryGetChannelDef(string tag, out ChannelDefBase def)
        {
            if (string.IsNullOrWhiteSpace(tag))
                tag = "default";

            if (_defsByTag.TryGetValue(tag, out var tDef))
            {
                def = tDef;
                return true;
            }

            def = null;
            return false;
        }

        public bool RegisterChannel(ChannelDefBase def, bool overwrite = false)
        {
            if (def is not TransformChannelDef tDef)
                return false;

            return RegisterChannelInternal(tDef, overwrite);
        }

        public bool UnregisterChannel(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                tag = "default";

            return RemoveChannelInternal(tag);
        }

        // ===== Start =====
        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            // 特に何もしない
            foreach (var player in _players.Values)
            {
                if (player is ITransformAnimationChannelLifecycle lifecycle)
                    lifecycle.OnAcquire();
            }
        }
        public void OnRelease(IScopeNode scope, bool isReset)
        {
            // 全チャネル停止
            foreach (var player in _players.Values)
            {
                if (player is ITransformAnimationChannelLifecycle lifecycle)
                    lifecycle.OnRelease();
                else
                    player.Stop();
            }
        }

        public void Tick()
        {
            var deltaTime = Time.deltaTime;
            // 以前は tick の経路が分散しており、buffer だけ更新されて見た目への反映が抜けることがあった。
            // hub が毎フレーム全 player を tick して、同じ更新系で director まで流す。
            foreach (var player in _players.Values)
            {
                player.Tick(deltaTime);
            }
        }

        // ===== 内部処理 =====

        bool RegisterChannelInternal(TransformChannelDef def, bool overwrite)
        {
            if (def == null)
                return false;

            var tag = def.Tag;
            if (string.IsNullOrWhiteSpace(tag))
                return false;

            if (_players.ContainsKey(tag))
            {
                if (!overwrite)
                    return false;

                RemoveChannelInternal(tag);
            }

            var player = new TransformAnimationChannelPlayer(def, _scope, _enableDebugLog);
            _players[tag] = player;
            _playerList.Add(player);
            _defsByTag[tag] = def;
            _defsDirty = true;

            return true;
        }

        bool RemoveChannelInternal(string tag)
        {
            if (!_players.TryGetValue(tag, out var player))
                return false;

            player.Stop();
            _players.Remove(tag);
            _playerList.Remove(player);

            if (_defsByTag.Remove(tag))
            {
                _defsDirty = true;
            }

            return true;
        }
    }
}
