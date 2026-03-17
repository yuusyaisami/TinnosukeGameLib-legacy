// Game.Channel.AnimationSpriteHubService.cs

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using VContainer;
using VNext = Game.Commands.VNext;
using UnityEngine;
using Game.MaterialFx;
using Game.Visual;
using VContainer.Unity;
using Game.SharedTexture;

namespace Game.Channel
{
    public interface IAnimationSpriteHubService : IChannelHubService
    {
        IReadOnlyList<IAnimationSpriteChannelPlayer> Players { get; }

        IAnimationSpriteChannelPlayer GetPlayer(string tag);
        bool TryGetPlayer(string tag, out IAnimationSpriteChannelPlayer player);

        void SetHubState(
            IReadOnlyList<MaterialFxPresetEntry> entries,
            bool clearMissingKeys = true,
            int basePriority = 0);

        void BroadcastMaterialFx(
            IReadOnlyList<MaterialFxPresetEntry> entries,
            int basePriority = 0);
    }

    public sealed class AnimationSpriteHubService : IAnimationSpriteHubService, ITaggedMaterialFxProvider, IVisualHub, IScopeAcquireHandler, IScopeReleaseHandler, ITickable, IDisposable
    {
        struct HubStateEntry
        {
            public string StableKey;
            public ValueKind Type;
            public MaterialFxTypedValue Value;
            public MaterialFxBlendMode BlendMode;
            public int Priority;
        }

        static int s_nextHubInstanceId;

        readonly Dictionary<string, AnimationSpriteChannelPlayer> _players =
            new(StringComparer.Ordinal);

        readonly Dictionary<string, AnimationSpriteChannelDef> _defsByTag =
            new(StringComparer.Ordinal);

        readonly List<AnimationSpriteChannelPlayer> _playerList = new();
        readonly List<ChannelDefBase> _defsSnapshot = new();

        readonly IScopeNode _ownerScope;
        readonly VNext.ICommandRunner _commandRunner;
        readonly IMaterialFxServiceFactory _materialFxFactory;
        readonly IMaterialFxPropertyRegistry _materialFxRegistry;

        readonly IVisualSystem _visualSystem;
        readonly string _hubTag;
        readonly int _hubInstanceId;
        bool _visualHubRegistered;

        readonly Dictionary<string, HubStateEntry> _hubStateByKey = new(StringComparer.Ordinal);
        readonly Dictionary<string, HubStateEntry> _hubStateScratchByKey = new(StringComparer.Ordinal);
        readonly List<string> _hubRemovedKeys = new();
        readonly List<HubStateEntry> _hubAddedOrModified = new();
        readonly HashSet<string> _warnedUnknownKeys = new(StringComparer.Ordinal);

        readonly string _hubStateContextTag;
        readonly string _hubBroadcastContextTag;
        readonly List<MaterialFxPresetEntry> _hubTimeBroadcastEntries = new();

        bool _defsDirty = true;

        public IReadOnlyList<IAnimationSpriteChannelPlayer> Players => _playerList;
        public IScopeNode OwnerScope => _ownerScope;

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

        public AnimationSpriteHubService(
            AnimationSpriteChannelDef[] channelDefs,
            IScopeNode ownerScope,
            VNext.ICommandRunner commandRunner,
            IMaterialFxServiceFactory materialFxFactory = null,
            IMaterialFxPropertyRegistry materialFxRegistry = null,
            string hubTag = "default")
        {
            _ownerScope = ownerScope;
            _commandRunner = commandRunner;
            _materialFxFactory = materialFxFactory;

            _hubTag = string.IsNullOrWhiteSpace(hubTag) ? "default" : hubTag;
            var resolver = ownerScope?.Resolver;
            if (resolver != null && resolver.TryResolve<IVisualSystem>(out var vs) && vs != null)
                _visualSystem = vs;

            // Fallback: VContainer の optional parameter injection が親スコープから
            // IMaterialFxPropertyRegistry を解決できない場合、scope resolver で再試行する
            if (materialFxRegistry == null && resolver != null)
                resolver.TryResolve(out materialFxRegistry);
            _materialFxRegistry = materialFxRegistry;

            var hubInstanceId = 0;
            var ownerTransform = _ownerScope?.Identity?.SelfTransform;
            if (ownerTransform != null)
            {
                hubInstanceId = ownerTransform.GetInstanceID();
            }
            if (hubInstanceId == 0)
            {
                hubInstanceId = Interlocked.Increment(ref s_nextHubInstanceId);
            }

            _hubInstanceId = hubInstanceId;

            _hubStateContextTag = "AnimationSpriteHub.State." + hubInstanceId;
            _hubBroadcastContextTag = "AnimationSpriteHub.Broadcast." + hubInstanceId;

            if (channelDefs != null)
            {
                foreach (var def in channelDefs)
                {
                    // 初期登録は overwrite=false で投入
                    RegisterChannelInternal(def, overwrite: false);
                }
            }
        }

        // ========= IVisualHub =========

        VisualHubKind IVisualHub.Kind => VisualHubKind.SpriteAnimation;
        string IVisualHub.HubTag => _hubTag;
        int IVisualHub.HubInstanceId => _hubInstanceId;

        // ========= Scope Acquire/Release =========

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            TryPlayOnSpawn();
            if (_visualHubRegistered)
            {
                return;
            }

            if (_visualSystem == null)
            {
                //if (scope.Parent.Identity.SelfTransform != null)
                //    Debug.LogWarning($"[AnimationSpriteHub] Cannot register hub: VisualSystem not found in scope '{scope.Parent.Identity.SelfTransform.name}'.");
                return;
            }

            _visualSystem.RegisterHub(this);

            _visualHubRegistered = true;
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            ResetPlayersToEmptyAnimation();
            ResetPlayOnSpawnFlags();

            if (!_visualHubRegistered)
                return;

            if (_visualSystem == null)
                return;

            _visualSystem.UnregisterHub(this);
            _visualHubRegistered = false;
        }

        void TryPlayOnSpawn()
        {
            for (int i = 0; i < _playerList.Count; i++)
            {
                _playerList[i]?.TryPlayOnSpawn();
            }
        }

        void ResetPlayOnSpawnFlags()
        {
            for (int i = 0; i < _playerList.Count; i++)
            {
                _playerList[i]?.ResetPlayOnSpawn();
            }
        }

        void ResetPlayersToEmptyAnimation()
        {
            var emptyClip = MaterialFxMB.EmptyAnimationData;
            if (emptyClip == null)
                return;

            for (int i = 0; i < _playerList.Count; i++)
            {
                var player = _playerList[i];
                if (player == null)
                    continue;

                player.Stop();
                player.PlayAsync(emptyClip, null, AnimationPlayMode.Once, false, CancellationToken.None).Forget();
            }
        }

        void ITickable.Tick()
        {
            var dt = Time.deltaTime;
            for (int i = 0; i < _playerList.Count; i++)
            {
                _playerList[i].Tick(dt);
            }
        }

        // =========  IAnimationSpriteHubService  =========

        public IAnimationSpriteChannelPlayer GetPlayer(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                tag = "default";

            if (_players.TryGetValue(tag, out var player))
                return player;

            throw new KeyNotFoundException($"[AnimationSpriteHub] Channel '{tag}' not found.");
        }

        public bool TryGetPlayer(string tag, out IAnimationSpriteChannelPlayer player)
        {
            if (string.IsNullOrWhiteSpace(tag))
                tag = "default";

            if (_players.TryGetValue(tag, out var p))
            {
                player = p;
                return true;
            }

            player = null;
            return false;
        }

        public bool TryGetMaterialFxReceiver(string tag, out IMaterialFxReceiver? receiver)
        {
            if (string.IsNullOrWhiteSpace(tag))
                tag = "default";

            if (_players.TryGetValue(tag, out var player))
            {
                receiver = player;
                return true;
            }

            receiver = null;
            return false;
        }

        public bool TryGetMaterialFx(string tag, out IMaterialFxService? materialFx)
        {
            if (string.IsNullOrWhiteSpace(tag))
                tag = "default";

            if (_players.TryGetValue(tag, out var player) && player.MaterialFx != null)
            {
                materialFx = player.MaterialFx;
                return true;
            }

            materialFx = null;
            return false;
        }

        // =========  IChannelHubService 実装  =========

        public bool TryGetChannelDef(string tag, out ChannelDefBase def)
        {
            if (string.IsNullOrWhiteSpace(tag))
                tag = "default";

            if (_defsByTag.TryGetValue(tag, out var spriteDef))
            {
                def = spriteDef;
                return true;
            }

            def = null;
            return false;
        }

        public bool RegisterChannel(ChannelDefBase def, bool overwrite = false)
        {
            // ChannelDefBase から自分の型にキャストして判定
            if (def is not AnimationSpriteChannelDef spriteDef)
            {
                Debug.LogWarning("[AnimationSpriteHub] Cannot register channel: invalid channel definition type.");
                return false;
            }

            return RegisterChannelInternal(spriteDef, overwrite);
        }

        public bool UnregisterChannel(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                tag = "default";

            return RemoveChannelInternal(tag);
        }

        // =========  HubMaterial v1.0  =========

        public void SetHubState(
            IReadOnlyList<MaterialFxPresetEntry> entries,
            bool clearMissingKeys = true,
            int basePriority = 0)
        {
            if (_materialFxRegistry == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[AnimationSpriteHub] SetHubState skipped: _materialFxRegistry is null. " +
                    $"Ensure MaterialFxMB is installed in an ancestor scope. HubTag='{_hubTag}'");
#endif
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            //Debug.Log($"[AnimationSpriteHub] SetHubState begin hubTag='{_hubTag}' hubId={_hubInstanceId} entries={entries?.Count ?? 0} clearMissing={clearMissingKeys} basePrio={basePriority} players={_playerList.Count}");
#endif

            _hubStateScratchByKey.Clear();
            _hubRemovedKeys.Clear();
            _hubAddedOrModified.Clear();
            _hubTimeBroadcastEntries.Clear();

            //Debug.Log($"[AnimationSpriteHub] SetHubState called with {entries?.Count ?? 0} entries.");

            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    var key = entry.Key;
                    if (string.IsNullOrWhiteSpace(key))
                        continue;

                    if (IsTimeDependent(entry))
                    {
                        _hubTimeBroadcastEntries.Add(entry);
                        continue;
                    }

                    if (!_materialFxRegistry.TryGetValueType(key, out var valueType))
                    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        if (_warnedUnknownKeys.Add(key))
                            Debug.LogWarning($"[AnimationSpriteHub] Unknown MaterialFx stableKey '{key}'. Skipped.");
#endif
                        continue;
                    }

                    if (!TryNormalizeValue(entry.Value, valueType, out var typedValue))
                        continue;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (key.Contains("BlendColor2D", StringComparison.Ordinal))
                    {
                        //Debug.Log($"[AnimationSpriteHub] SetHubState entry hubId={_hubInstanceId} key='{key}' type={valueType} value={typedValue} blend={entry.BlendMode}");
                    }
#endif

                    _hubStateScratchByKey[key] = new HubStateEntry
                    {
                        StableKey = key,
                        Type = valueType,
                        Value = typedValue,
                        BlendMode = entry.BlendMode,
                        Priority = basePriority
                    };
                }
            }

            if (clearMissingKeys)
            {
                foreach (var kv in _hubStateByKey)
                {
                    if (!_hubStateScratchByKey.ContainsKey(kv.Key))
                        _hubRemovedKeys.Add(kv.Key);
                }
            }

            foreach (var kv in _hubStateScratchByKey)
            {
                if (_hubStateByKey.TryGetValue(kv.Key, out var prev))
                {
                    if (!IsSameHubState(prev, kv.Value))
                        _hubAddedOrModified.Add(kv.Value);
                }
                else
                {
                    _hubAddedOrModified.Add(kv.Value);
                }
            }

            var hasStateChange = _hubRemovedKeys.Count != 0 || _hubAddedOrModified.Count != 0;
            var hasTimeBroadcast = _hubTimeBroadcastEntries.Count != 0;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            //Debug.Log($"[AnimationSpriteHub] SetHubState filter hubId={_hubInstanceId} scratch={_hubStateScratchByKey.Count} removed={_hubRemovedKeys.Count} modified={_hubAddedOrModified.Count} timeBroadcast={_hubTimeBroadcastEntries.Count} hasChange={hasStateChange}");
#endif
            if (!hasStateChange && !hasTimeBroadcast)
                return;

            if (hasStateChange)
            {
                for (int p = 0; p < _playerList.Count; p++)
                {
                    var fx = _playerList[p].MaterialFx;
                    if (fx == null)
                        continue;

                    for (int i = 0; i < _hubRemovedKeys.Count; i++)
                    {
                        fx.RemoveLayer(_hubRemovedKeys[i], _hubStateContextTag);
                    }

                    for (int i = 0; i < _hubAddedOrModified.Count; i++)
                    {
                        var e = _hubAddedOrModified[i];
                        fx.SetLayer(e.StableKey, _hubStateContextTag, e.Value, e.BlendMode, e.Priority, lifetimeSeconds: -1f);
                    }

                    // Ensure immediate apply if the global MaterialFx tick is missing.
                    fx.Tick(0f);
                }

                if (clearMissingKeys)
                {
                    for (int i = 0; i < _hubRemovedKeys.Count; i++)
                    {
                        _hubStateByKey.Remove(_hubRemovedKeys[i]);
                    }
                }

                foreach (var kv in _hubStateScratchByKey)
                {
                    _hubStateByKey[kv.Key] = kv.Value;
                }
            }

            if (hasTimeBroadcast)
            {
                BroadcastMaterialFx(_hubTimeBroadcastEntries, basePriority);
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            {
                int fxNullCount = 0;
                for (int p = 0; p < _playerList.Count; p++)
                {
                    if (_playerList[p].MaterialFx == null) fxNullCount++;
                }
                //Debug.Log($"[AnimationSpriteHub] SetHubState end hubId={_hubInstanceId} removed={_hubRemovedKeys.Count} modified={_hubAddedOrModified.Count} timeBroadcast={_hubTimeBroadcastEntries.Count} players={_playerList.Count} fxNull={fxNullCount}");

                if (_hubAddedOrModified.Count > 0 && _playerList.Count > 0)
                {
                    var fx = _playerList[0].MaterialFx;
                    if (fx != null)
                    {
                        for (int i = 0; i < _hubAddedOrModified.Count; i++)
                        {
                            var e = _hubAddedOrModified[i];
                            var count = fx.GetActiveLayerCount(e.StableKey, _hubStateContextTag);
                            //Debug.Log($"[AnimationSpriteHub] SetHubState layerCount hubId={_hubInstanceId} key='{e.StableKey}' count={count} ctx='{_hubStateContextTag}'");
                        }
                    }
                }
            }
#endif
        }

        public void BroadcastMaterialFx(
            IReadOnlyList<MaterialFxPresetEntry> entries,
            int basePriority = 0)
        {
            if (_materialFxRegistry == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[AnimationSpriteHub] BroadcastMaterialFx skipped: _materialFxRegistry is null. HubTag='{_hubTag}'");
#endif
                return;
            }
            if (entries == null || entries.Count == 0)
                return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Debug.Log($"[AnimationSpriteHub] BroadcastMaterialFx hubTag='{_hubTag}' hubId={_hubInstanceId} entries={entries.Count} basePrio={basePriority} players={_playerList.Count}");
#endif

            for (int p = 0; p < _playerList.Count; p++)
            {
                var fx = _playerList[p].MaterialFx;
                if (fx == null)
                    continue;

                for (int i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    var key = entry.Key;
                    if (string.IsNullOrWhiteSpace(key))
                        continue;

                    if (!_materialFxRegistry.TryGetValueType(key, out var valueType))
                    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        if (_warnedUnknownKeys.Add(key))
                            Debug.LogWarning($"[AnimationSpriteHub] Unknown MaterialFx stableKey '{key}'. Skipped.");
#endif
                        continue;
                    }

                    if (!TryNormalizeValue(entry.Value, valueType, out var typedValue))
                        continue;

                    fx.SetLayer(key, _hubBroadcastContextTag, typedValue, entry.BlendMode, basePriority, entry.LifetimeSeconds);

                    if (entry.ApplyWeightFade)
                    {
                        fx.SetLayerFade(key, _hubBroadcastContextTag, typedValue, entry.FadeDuration, entry.FadeEase, entry.BlendMode, basePriority, entry.LifetimeSeconds);
                    }
                }

                // Ensure immediate apply if the global MaterialFx tick is missing.
                fx.Tick(0f);
            }
        }

        // =========  内部共通ロジック  =========

        bool RegisterChannelInternal(AnimationSpriteChannelDef def, bool overwrite)
        {
            if (def == null)
            {
                Debug.LogWarning("[AnimationSpriteHub] Cannot register null channel definition.");
                return false;
            }

            var tag = def.Tag;
            if (string.IsNullOrWhiteSpace(tag))
            {
                Debug.LogWarning("[AnimationSpriteHub] Cannot register channel with null or empty tag.");
                return false;
            }

            if (_players.ContainsKey(tag))
            {
                if (!overwrite)
                {
                    Debug.LogWarning($"[AnimationSpriteHub] Channel '{tag}' already exists. Set overwrite=true to replace.");
                    return false;
                }

                // 上書き許可 → 既存チャネルを削除してから入れ替え
                RemoveChannelInternal(tag);
            }

            var player = new AnimationSpriteChannelPlayer(
                def,
                _ownerScope,
                _commandRunner,
                _materialFxFactory);

            _players[tag] = player;
            _playerList.Add(player);
            _defsByTag[tag] = def;
            _defsDirty = true;

            SyncHubStateToNewPlayer(player);
            if (_visualHubRegistered)
            {
                player.TryPlayOnSpawn();
            }

            return true;
        }

        void SyncHubStateToNewPlayer(AnimationSpriteChannelPlayer player)
        {
            if (_hubStateByKey.Count == 0)
                return;

            var fx = player?.MaterialFx;
            if (fx == null)
                return;

            foreach (var kv in _hubStateByKey)
            {
                var e = kv.Value;
                fx.SetLayer(e.StableKey, _hubStateContextTag, e.Value, e.BlendMode, e.Priority, lifetimeSeconds: -1f);
            }
        }

        static bool IsSameHubState(HubStateEntry a, HubStateEntry b)
        {
            if (a.Type != b.Type) return false;
            if (a.BlendMode != b.BlendMode) return false;
            if (a.Priority != b.Priority) return false;
            return ValueEquals(a.Type, a.Value, b.Value);
        }

        static bool IsTimeDependent(in MaterialFxPresetEntry e)
        {
            return e.LifetimeSeconds > 0f || e.ApplyWeightFade || e.FadeDuration > 0f;
        }

        static bool ValueEquals(ValueKind type, MaterialFxTypedValue a, MaterialFxTypedValue b)
        {
            switch (type)
            {
                case ValueKind.Float:
                    return a.Float == b.Float;
                case ValueKind.Int:
                case ValueKind.Bool:
                    return a.Int == b.Int;
                case ValueKind.Float2:
                    return a.Float2 == b.Float2;
                case ValueKind.Float3:
                    return a.Float3 == b.Float3;
                case ValueKind.Float4:
                    return a.Float4 == b.Float4;
                case ValueKind.Color:
                    return a.Color == b.Color;
                case ValueKind.Matrix4x4:
                    return a.Matrix == b.Matrix;
                case ValueKind.Texture:
                case ValueKind.TextureArray:
                    return a.Texture == b.Texture;
                case ValueKind.AnimationCurve:
                    return ReferenceEquals(a.Object, b.Object);
                default:
                    return false;
            }
        }

        static bool TryNormalizeValue(MaterialFxSerializedValue serialized, ValueKind expectedType, out MaterialFxTypedValue typed)
        {
            typed = default;

            if (expectedType == ValueKind.TextureArray)
            {
                if (serialized.Texture == null)
                {
                    typed = MaterialFxTypedValue.FromTextureArray(null);
                    return true;
                }

                if (serialized.Texture is Texture2DArray arr)
                {
                    typed = MaterialFxTypedValue.FromTextureArray(arr);
                    return true;
                }

                return false;
            }

            typed = serialized.ToTypedValue(expectedType);
            return true;
        }



        bool RemoveChannelInternal(string tag)
        {
            if (!_players.TryGetValue(tag, out var player))
                return false;

            _defsByTag.TryGetValue(tag, out var def);

            // 一応止めておく
            player.Stop();
            // Dispose があれば確実に破棄してリークを止める
            try
            {
                (player as IDisposable)?.Dispose();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            _players.Remove(tag);
            _playerList.Remove(player);

            if (_defsByTag.Remove(tag))
            {
                _defsDirty = true;
            }

            return true;
        }

        public void Dispose()
        {
            if (_visualHubRegistered && _visualSystem != null)
            {
                _visualSystem.UnregisterHub(this);
                _visualHubRegistered = false;
            }

            // Dispose all players
            foreach (var p in _playerList)
            {
                try
                {
                    (p as IDisposable)?.Dispose();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }

            _playerList.Clear();
            _players.Clear();
            _defsByTag.Clear();
            _defsDirty = true;
        }
    }
}
