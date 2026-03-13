// Game.StateMachine.StateMachineService.cs

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.StateMachine
{
    /// <summary>
    /// StateMachine のランタイム実装。
    /// </summary>
    /// <remarks>
    /// <para>設計方針:</para>
    /// <list type="bullet">
    ///   <item>Layer/State はオンデマンドで自動登録される</item>
    ///   <item>StateKey → LayerKey は一発導出（総当たり探索禁止）</item>
    ///   <item>同優先度のタイブレークは「最後に Acquire されたものが勝つ (last-activated-wins)」</item>
    ///   <item>Pulse は State が非 Active になった時点でクリア</item>
    /// </list>
    /// </remarks>
    public sealed class StateMachineService : IStateMachine
    {
        // ════════════════════════════════════════════════════════════════
        //  Internal Runtime Types
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// State のランタイム情報。
        /// </summary>
        sealed class StateRuntime
        {
            public readonly string StateKey;
            public readonly string StateLeaf;
            public int Priority;
            public readonly List<StateToken> Tokens = new();
            public uint PulseCount;
            public ulong LastActivationOrder;

            public bool IsActive => Tokens.Count > 0;

            public StateRuntime(string stateKey, string stateLeaf, int priority)
            {
                StateKey = stateKey;
                StateLeaf = stateLeaf;
                Priority = priority;
            }
        }

        /// <summary>
        /// Layer のランタイム情報。
        /// </summary>
        sealed class LayerRuntime
        {
            public readonly string LayerKey;
            public int Priority;
            public readonly Dictionary<string, StateRuntime> States = new(StringComparer.Ordinal);
            public readonly Dictionary<string, string> LocalOptions = new(StringComparer.Ordinal);
            public uint OptionRevision;
            public ulong LastActivationOrder;

            // Cached selected state
            StateRuntime _selectedState;
            bool _selectedDirty = true;

            public LayerRuntime(string layerKey, int priority)
            {
                LayerKey = layerKey;
                Priority = priority;
            }

            public StateRuntime GetOrCreateState(string stateKey, string stateLeaf, int defaultPriority)
            {
                if (States.TryGetValue(stateKey, out var state))
                    return state;

                state = new StateRuntime(stateKey, stateLeaf, defaultPriority);
                States[stateKey] = state;
                _selectedDirty = true;
                return state;
            }

            public void MarkSelectedDirty() => _selectedDirty = true;

            public StateRuntime GetSelectedState()
            {
                if (!_selectedDirty && _selectedState != null && _selectedState.IsActive)
                    return _selectedState;

                _selectedState = null;
                int bestPriority = int.MinValue;
                ulong bestOrder = 0;

                foreach (var kvp in States)
                {
                    var state = kvp.Value;
                    if (!state.IsActive)
                        continue;

                    // 優先度比較 → タイブレーク (last-activated-wins)
                    bool isBetter = state.Priority > bestPriority
                        || (state.Priority == bestPriority && state.LastActivationOrder > bestOrder);

                    if (isBetter)
                    {
                        bestPriority = state.Priority;
                        bestOrder = state.LastActivationOrder;
                        _selectedState = state;
                    }
                }

                _selectedDirty = false;
                return _selectedState;
            }

            public bool HasActiveState()
            {
                foreach (var kvp in States)
                {
                    if (kvp.Value.IsActive)
                        return true;
                }
                return false;
            }
        }

        /// <summary>
        /// State 制御トークン実装。
        /// </summary>
        sealed class StateToken : IStateToken
        {
            readonly StateMachineService _owner;
            bool _valid = true;

            public string StateKey { get; }
            public string OwnerId { get; }
            public string Tag { get; }
            public bool IsValid => _valid;

            public StateToken(StateMachineService owner, string stateKey, string ownerId, string tag)
            {
                _owner = owner ?? throw new ArgumentNullException(nameof(owner));
                StateKey = stateKey ?? throw new ArgumentNullException(nameof(stateKey));
                OwnerId = ownerId ?? string.Empty;
                Tag = tag ?? string.Empty;
            }

            public void Release()
            {
                if (_valid)
                    _owner.ReleaseState(this);
            }

            public void Invalidate() => _valid = false;

            public void Dispose() => Release();
        }

        // ════════════════════════════════════════════════════════════════
        //  Fields
        // ════════════════════════════════════════════════════════════════

        StateMachinePreset _profile;

        // LayerKey → LayerRuntime
        readonly Dictionary<string, LayerRuntime> _layers = new(StringComparer.Ordinal);

        // Tag → (StateKey → StateToken)
        readonly Dictionary<string, Dictionary<string, StateToken>> _taggedTokens = new(StringComparer.Ordinal);

        // GlobalOptions: OptionKey → OptionValue
        readonly Dictionary<string, string> _globalOptions = new(StringComparer.Ordinal);

        // Revision tracking
        uint _machineRevision;
        uint _globalOptionRevision;

        // Activation order counter (monotonically increasing)
        ulong _activationOrderCounter;

        // Current state cache
        string _currentState;
        string _currentLayer;
        bool _currentStateDirty = true;

        // ════════════════════════════════════════════════════════════════
        //  Properties
        // ════════════════════════════════════════════════════════════════

        public string CurrentState
        {
            get
            {
                RecomputeCurrentStateIfDirty();
                return _currentState;
            }
        }

        public string CurrentLayer
        {
            get
            {
                RecomputeCurrentStateIfDirty();
                return _currentLayer;
            }
        }

        public uint MachineRevision => _machineRevision;
        public uint GlobalOptionRevision => _globalOptionRevision;

        /// <summary>現在の Preset（null の場合もある）</summary>
        public StateMachinePreset Profile => _profile;

        // ════════════════════════════════════════════════════════════════
        //  Constructor
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// StateMachineService を生成する。
        /// </summary>
        /// <param name="profile">プロファイル（null の場合はデフォルト値で動作）</param>
        public StateMachineService(StateMachinePreset profile = null)
        {
            _profile = profile;

            // GlobalOption のデフォルト値を設定
            if (profile != null)
            {
                foreach (var entry in profile.GlobalOptionDefaults)
                {
                    if (!string.IsNullOrEmpty(entry.OptionKey) && !string.IsNullOrEmpty(entry.DefaultValue))
                    {
                        _globalOptions[entry.OptionKey] = entry.DefaultValue;
                    }
                }
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  Profile Hot-Swap
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Profile を動的に差し替える。
        /// Layer/State の Priority を再計算し、MachineRevision を進める。
        /// </summary>
        /// <param name="profile">新しいプロファイル（null 可）</param>
        /// <param name="applyGlobalDefaults">true の場合、profile の GlobalOptionDefaults を反映する</param>
        /// <param name="overwriteExistingGlobals">true の場合、既存の GlobalOption を上書きする。false なら未設定キーのみ追加</param>
        public void SetProfile(StateMachinePreset profile, bool applyGlobalDefaults = true, bool overwriteExistingGlobals = false)
        {
            _profile = profile;

            // Layer/State の Priority を再計算
            foreach (var layerKvp in _layers)
            {
                var layer = layerKvp.Value;

                // Layer Priority 更新
                int newLayerPriority = profile?.GetLayerPriority(layer.LayerKey) ?? 0;
                layer.Priority = newLayerPriority;

                // State Priority 更新
                foreach (var stateKvp in layer.States)
                {
                    var state = stateKvp.Value;
                    int newStatePriority = profile?.GetStatePriority(state.StateKey) ?? 0;
                    state.Priority = newStatePriority;
                }

                // 選択状態を再評価
                layer.MarkSelectedDirty();
            }

            // GlobalOptionDefaults 適用
            if (applyGlobalDefaults && profile != null)
            {
                foreach (var entry in profile.GlobalOptionDefaults)
                {
                    if (string.IsNullOrEmpty(entry.OptionKey))
                        continue;

                    bool exists = _globalOptions.ContainsKey(entry.OptionKey);
                    if (!exists || overwriteExistingGlobals)
                    {
                        if (!string.IsNullOrEmpty(entry.DefaultValue))
                        {
                            _globalOptions[entry.OptionKey] = entry.DefaultValue;
                        }
                    }
                }

                _globalOptionRevision++;
            }

            // 全体の状態を dirty にして Revision を進める
            _currentStateDirty = true;
            _machineRevision++;
        }

        // ════════════════════════════════════════════════════════════════
        //  IStateMachine Implementation - State Control
        // ════════════════════════════════════════════════════════════════

        public IStateToken AcquireState(string stateKey, string ownerId)
        {
            return AcquireStateInternal(stateKey, ownerId, string.Empty);
        }

        /// <summary>
        /// Fire-and-forget で State を有効化する。tag でひも付け、ReleaseStatesByTag/ReleaseState(stateKey, tag) で解放する。
        /// </summary>
        public void SetState(string stateKey, string tag, string ownerId = "")
        {
            if (string.IsNullOrEmpty(stateKey))
                return;

            AcquireStateInternal(stateKey, ownerId ?? string.Empty, tag ?? string.Empty);
        }

        /// <summary>
        /// tag に紐付いた State をすべて解放する。
        /// </summary>
        public void ReleaseStatesByTag(string tag)
        {
            if (string.IsNullOrEmpty(tag))
                return;

            if (!_taggedTokens.TryGetValue(tag, out var map))
                return;

            var snapshot = new List<StateToken>(map.Values);
            for (int i = 0; i < snapshot.Count; i++)
            {
                InternalReleaseToken(snapshot[i]);
            }
        }

        /// <summary>
        /// 指定した tag かつ stateKey に紐付いた State を解放する。
        /// </summary>
        public void ReleaseState(string stateKey, string tag)
        {
            if (string.IsNullOrEmpty(stateKey) || string.IsNullOrEmpty(tag))
                return;

            if (_taggedTokens.TryGetValue(tag, out var map) && map.TryGetValue(stateKey, out var token))
            {
                InternalReleaseToken(token);
            }
        }

        public void ReleaseState(IStateToken token)
        {
            if (token is not StateToken stateToken)
                return;

            InternalReleaseToken(stateToken);
        }

        // ════════════════════════════════════════════════════════════════
        //  IStateMachine Implementation - Pulse
        // ════════════════════════════════════════════════════════════════

        public void FirePulse(string stateKey)
        {
            FirePulseInternal(stateKey, null);
        }

        public void FirePulse(string stateKey, string requiredTag)
        {
            FirePulseInternal(stateKey, requiredTag);
        }

        public uint GetPulseCount(string stateKey)
        {
            if (string.IsNullOrEmpty(stateKey))
                return 0;

            if (!StateKeyUtils.SplitLayerAndLeaf(stateKey, out var layerKey, out _))
                return 0;

            if (!_layers.TryGetValue(layerKey, out var layer))
                return 0;

            if (!layer.States.TryGetValue(stateKey, out var state))
                return 0;

            return state.PulseCount;
        }

        // ════════════════════════════════════════════════════════════════
        //  IStateMachine Implementation - Query
        // ════════════════════════════════════════════════════════════════

        public string GetSelectedStateLeaf(string layerKey)
        {
            if (string.IsNullOrEmpty(layerKey))
                return null;

            if (!_layers.TryGetValue(layerKey, out var layer))
                return null;

            var selected = layer.GetSelectedState();
            return selected?.StateLeaf;
        }

        public bool IsStateActive(string stateKey)
        {
            if (string.IsNullOrEmpty(stateKey))
                return false;

            if (!StateKeyUtils.SplitLayerAndLeaf(stateKey, out var layerKey, out _))
                return false;

            if (!_layers.TryGetValue(layerKey, out var layer))
                return false;

            if (!layer.States.TryGetValue(stateKey, out var state))
                return false;

            return state.IsActive;
        }

        public bool IsStateActive(string stateKey, string tag)
        {
            if (string.IsNullOrEmpty(tag))
                return IsStateActive(stateKey);

            return HasTaggedToken(stateKey, tag);
        }

        public bool IsLayerActive(string layerKey)
        {
            if (string.IsNullOrEmpty(layerKey))
                return false;

            if (!_layers.TryGetValue(layerKey, out var layer))
                return false;

            return layer.HasActiveState();
        }

        // ════════════════════════════════════════════════════════════════
        //  IStateMachine Implementation - Options
        // ════════════════════════════════════════════════════════════════

        public string GetGlobalOption(string optionKey)
        {
            if (string.IsNullOrEmpty(optionKey))
                return null;

            return _globalOptions.TryGetValue(optionKey, out var value) ? value : null;
        }

        public string GetLocalOption(string layerKey, string optionKey)
        {
            if (string.IsNullOrEmpty(layerKey) || string.IsNullOrEmpty(optionKey))
                return null;

            if (!_layers.TryGetValue(layerKey, out var layer))
                return null;

            return layer.LocalOptions.TryGetValue(optionKey, out var value) ? value : null;
        }

        public string ResolveOption(string optionKey)
        {
            // Use current layer for local option lookup
            RecomputeCurrentStateIfDirty();
            return ResolveOption(_currentLayer, optionKey);
        }

        public string ResolveOption(string layerKey, string optionKey)
        {
            if (string.IsNullOrEmpty(optionKey))
                return null;

            // 1. Try Local option first
            if (!string.IsNullOrEmpty(layerKey))
            {
                var local = GetLocalOption(layerKey, optionKey);
                if (local != null)
                    return local;
            }

            // 2. Fall back to Global option
            return GetGlobalOption(optionKey);
        }

        public void SetGlobalOption(string optionKey, string value)
        {
            if (string.IsNullOrEmpty(optionKey))
                return;

            if (string.IsNullOrEmpty(value))
            {
                _globalOptions.Remove(optionKey);
            }
            else
            {
                _globalOptions[optionKey] = value;
            }

            _globalOptionRevision++;
            _machineRevision++;
        }

        public void SetLocalOption(string layerKey, string optionKey, string value)
        {
            if (string.IsNullOrEmpty(layerKey) || string.IsNullOrEmpty(optionKey))
                return;

            // Ensure layer exists (for local option storage)
            var layer = EnsureLayerExists(layerKey);

            if (string.IsNullOrEmpty(value))
            {
                layer.LocalOptions.Remove(optionKey);
            }
            else
            {
                layer.LocalOptions[optionKey] = value;
            }

            layer.OptionRevision++;
            _machineRevision++;
        }

        public uint GetLayerOptionRevision(string layerKey)
        {
            if (string.IsNullOrEmpty(layerKey))
                return 0;

            if (!_layers.TryGetValue(layerKey, out var layer))
                return 0;

            return layer.OptionRevision;
        }

        // ════════════════════════════════════════════════════════════════
        //  Internal - Activation / Tag Helpers
        // ════════════════════════════════════════════════════════════════

        StateToken AcquireStateInternal(string stateKey, string ownerId, string tag)
        {
            // Validate stateKey
            if (!StateKeyUtils.SplitLayerAndLeaf(stateKey, out var layerKey, out var stateLeaf))
            {
                throw new ArgumentException(
                    $"Invalid stateKey format: '{stateKey}'. " +
                    "StateKey must be dot-separated with at least two segments (e.g. 'Movement.Idle').",
                    nameof(stateKey));
            }

            // Ensure Layer exists
            var layer = EnsureLayerExists(layerKey);

            // Ensure State exists
            var state = EnsureStateExists(layer, stateKey, stateLeaf);

            // Track activation order
            var wasActive = state.IsActive;
            _activationOrderCounter++;
            state.LastActivationOrder = _activationOrderCounter;

            if (!wasActive)
            {
                // First activation - update layer's activation order too
                layer.LastActivationOrder = _activationOrderCounter;
            }

            // Create token
            var token = new StateToken(this, stateKey, ownerId, tag ?? string.Empty);
            state.Tokens.Add(token);
            RegisterTaggedToken(token);

            // Mark dirty
            layer.MarkSelectedDirty();
            _currentStateDirty = true;
            _machineRevision++;

            return token;
        }

        void RegisterTaggedToken(StateToken token)
        {
            if (token == null)
                return;

            if (string.IsNullOrEmpty(token.Tag))
                return;

            if (!_taggedTokens.TryGetValue(token.Tag, out var map))
            {
                map = new Dictionary<string, StateToken>(StringComparer.Ordinal);
                _taggedTokens[token.Tag] = map;
            }

            if (map.TryGetValue(token.StateKey, out var existing) && existing != null && !ReferenceEquals(existing, token))
            {
                InternalReleaseToken(existing);
            }

            map[token.StateKey] = token;
        }

        void RemoveTaggedToken(StateToken token)
        {
            if (token == null || string.IsNullOrEmpty(token.Tag))
                return;

            if (!_taggedTokens.TryGetValue(token.Tag, out var map))
                return;

            if (map.TryGetValue(token.StateKey, out var existing) && ReferenceEquals(existing, token))
            {
                map.Remove(token.StateKey);
                if (map.Count == 0)
                    _taggedTokens.Remove(token.Tag);
            }
        }

        bool HasTaggedToken(string stateKey, string tag)
        {
            if (string.IsNullOrEmpty(tag))
                return true;

            return _taggedTokens.TryGetValue(tag, out var map)
                && map.TryGetValue(stateKey, out var token)
                && token != null
                && token.IsValid;
        }

        bool InternalReleaseToken(StateToken stateToken)
        {
            if (stateToken == null || !stateToken.IsValid)
                return false;

            var stateKey = stateToken.StateKey;

            // Find layer and state
            if (!StateKeyUtils.SplitLayerAndLeaf(stateKey, out var layerKey, out _))
                return false;

            if (!_layers.TryGetValue(layerKey, out var layer))
                return false;

            if (!layer.States.TryGetValue(stateKey, out var state))
                return false;

            // Remove token
            bool wasActive = state.IsActive;
            state.Tokens.Remove(stateToken);
            RemoveTaggedToken(stateToken);
            stateToken.Invalidate();

            // If state became inactive, clear pulse
            if (wasActive && !state.IsActive)
            {
                state.PulseCount = 0;
            }

            // Mark dirty
            layer.MarkSelectedDirty();
            _currentStateDirty = true;
            _machineRevision++;
            return true;
        }

        void FirePulseInternal(string stateKey, string requiredTag)
        {
            if (string.IsNullOrEmpty(stateKey))
                return;

            if (!StateKeyUtils.SplitLayerAndLeaf(stateKey, out var layerKey, out _))
                return;

            if (!_layers.TryGetValue(layerKey, out var layer))
                return;

            if (!layer.States.TryGetValue(stateKey, out var state))
                return;

            if (!state.IsActive)
                return;

            if (!string.IsNullOrEmpty(requiredTag) && !HasTaggedToken(stateKey, requiredTag))
                return;

            state.PulseCount++;
            _machineRevision++;
        }

        // ════════════════════════════════════════════════════════════════
        //  Internal - Layer/State Management
        // ════════════════════════════════════════════════════════════════

        LayerRuntime EnsureLayerExists(string layerKey)
        {
            if (_layers.TryGetValue(layerKey, out var layer))
                return layer;

            int priority = _profile?.GetLayerPriority(layerKey) ?? 0;
            layer = new LayerRuntime(layerKey, priority);
            _layers[layerKey] = layer;

            _currentStateDirty = true;
            return layer;
        }

        StateRuntime EnsureStateExists(LayerRuntime layer, string stateKey, string stateLeaf)
        {
            if (layer.States.TryGetValue(stateKey, out var state))
                return state;

            int priority = _profile?.GetStatePriority(stateKey) ?? 0;
            state = new StateRuntime(stateKey, stateLeaf, priority);
            layer.States[stateKey] = state;

            layer.MarkSelectedDirty();
            return state;
        }

        // ════════════════════════════════════════════════════════════════
        //  Internal - Current State Calculation
        // ════════════════════════════════════════════════════════════════

        void RecomputeCurrentStateIfDirty()
        {
            if (!_currentStateDirty)
                return;

            _currentStateDirty = false;
            _currentState = null;
            _currentLayer = null;

            LayerRuntime bestLayer = null;
            StateRuntime bestState = null;
            int bestLayerPriority = int.MinValue;
            ulong bestLayerOrder = 0;

            foreach (var kvp in _layers)
            {
                var layer = kvp.Value;
                var selected = layer.GetSelectedState();

                if (selected == null)
                    continue;

                // Layer priority comparison with tie-breaking
                bool isBetter = layer.Priority > bestLayerPriority
                    || (layer.Priority == bestLayerPriority && layer.LastActivationOrder > bestLayerOrder);

                if (isBetter)
                {
                    bestLayerPriority = layer.Priority;
                    bestLayerOrder = layer.LastActivationOrder;
                    bestLayer = layer;
                    bestState = selected;
                }
            }

            if (bestState != null)
            {
                _currentState = bestState.StateKey;
                _currentLayer = bestLayer.LayerKey;
            }
        }
    }
}
