// Game.StatusEffect.StatusEffectService.cs
//
// StatusEffect 管理サービス実装

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Common;
using Game.Events.Generated;
using Game.Health;
using Game.Profile;
using Game.Scalar;
using UnityEngine;
using VContainer.Unity;

namespace Game.StatusEffect
{
    /// <summary>
    /// StatusEffect 管理サービス実装。
    /// </summary>
    public sealed class StatusEffectService : IStatusEffectService, ITickable, IDisposable
    {
        readonly EffectContext _context;
        readonly Dictionary<string, BaseEffectRuntime> _activeEffects = new(StringComparer.Ordinal);
        readonly List<string> _removeQueue = new(8);
        readonly BoolLayer _effectFlagLayer;
        readonly IScopeBindingRegistry _profileRegistry;

        bool _disposed;

        // EventKey（直接参照）
        // TODO: EventKeys.GameLib.StatusEffect.OnApplied/OnRemoved を生成後に使用
        const string OnEffectAppliedKey = EventKeys.GameLib.StatusEffect.OnApplied;
        const string OnEffectRemovedKey = EventKeys.GameLib.StatusEffect.OnRemoved;

        public int ActiveEffectCount => _activeEffects.Count;
        public BoolLayer EffectFlagLayer => _effectFlagLayer;

        /// <summary>
        /// ProfileRegistry（Effect が ProfileSO を取得するために公開）
        /// </summary>
        public IScopeBindingRegistry ProfileRegistry => _profileRegistry;

        public StatusEffectService(
            IHealthService healthService,
            IBaseScalarService scalarService,
            IBlackboardService blackboardService,
            IEntityEventService eventService,
            IScopeBindingRegistry profileRegistry,
            Transform transform)
        {
            _effectFlagLayer = new BoolLayer(BoolCompositionMode.AnyTrue);
            _profileRegistry = profileRegistry;

            _context = new EffectContext(
                statusEffectService: this,
                healthService: healthService,
                scalarService: scalarService ?? throw new ArgumentNullException(nameof(scalarService)),
                blackboardService: blackboardService,
                eventService: eventService ?? throw new ArgumentNullException(nameof(eventService)),
                profileRegistry: profileRegistry,
                effectFlagLayer: _effectFlagLayer,
                transform: transform
            );
        }

        // ================================================================
        // Effect 適用
        // ================================================================

        public string ApplyEffect<T>(EffectConfig config) where T : BaseEffectRuntime, new()
        {
            if (_disposed)
                return null;

            var effect = new T();
            return ApplyEffectInternal(effect, config);
        }

        public string ApplyEffect(BaseEffectRuntime effect, EffectConfig config)
        {
            if (_disposed || effect == null)
                return null;

            return ApplyEffectInternal(effect, config);
        }

        string ApplyEffectInternal(BaseEffectRuntime effect, EffectConfig config)
        {
            string effectId = effect.EffectId;

            // 既存の同一 Effect があるか確認
            if (_activeEffects.TryGetValue(effectId, out var existing))
            {
                // スタッキング処理
                if (config.StackMode == EffectStackMode.Ignore)
                    return effectId;

                existing.Stack(config);
                return effectId;
            }

            // 新規適用
            _context.Config = config;
            effect.Initialize(_context);
            _activeEffects[effectId] = effect;
            effect.Apply();

            // イベント発行
            PublishEffectApplied(effect);

            return effectId;
        }

        // ================================================================
        // Effect 削除
        // ================================================================

        public bool RemoveEffect(string effectId)
        {
            if (_disposed || string.IsNullOrEmpty(effectId))
                return false;

            if (!_activeEffects.TryGetValue(effectId, out var effect))
                return false;

            RemoveEffectInternal(effect);
            return true;
        }

        public int RemoveEffects<T>() where T : BaseEffectRuntime
        {
            if (_disposed)
                return 0;

            int count = 0;
            foreach (var kvp in _activeEffects)
            {
                if (kvp.Value is T)
                {
                    _removeQueue.Add(kvp.Key);
                    count++;
                }
            }

            ProcessRemoveQueue();
            return count;
        }

        public int RemoveEffects(EffectType type)
        {
            if (_disposed)
                return 0;

            int count = 0;
            foreach (var kvp in _activeEffects)
            {
                if (kvp.Value.Type == type)
                {
                    _removeQueue.Add(kvp.Key);
                    count++;
                }
            }

            ProcessRemoveQueue();
            return count;
        }

        public void ClearAllEffects()
        {
            if (_disposed)
                return;

            foreach (var kvp in _activeEffects)
            {
                _removeQueue.Add(kvp.Key);
            }

            ProcessRemoveQueue();
        }

        void RemoveEffectInternal(BaseEffectRuntime effect)
        {
            if (effect == null)
                return;

            effect.Remove();
            _activeEffects.Remove(effect.EffectId);

            // イベント発行
            PublishEffectRemoved(effect);
        }

        void ProcessRemoveQueue()
        {
            for (int i = 0; i < _removeQueue.Count; i++)
            {
                if (_activeEffects.TryGetValue(_removeQueue[i], out var effect))
                {
                    RemoveEffectInternal(effect);
                }
            }
            _removeQueue.Clear();
        }

        // ================================================================
        // Effect クエリ
        // ================================================================

        public bool HasEffect(string effectId)
        {
            return !string.IsNullOrEmpty(effectId) && _activeEffects.ContainsKey(effectId);
        }

        public bool HasEffect<T>() where T : BaseEffectRuntime
        {
            foreach (var effect in _activeEffects.Values)
            {
                if (effect is T)
                    return true;
            }
            return false;
        }

        public bool TryGetEffect(string effectId, out BaseEffectRuntime effect)
        {
            effect = null;
            if (string.IsNullOrEmpty(effectId))
                return false;

            return _activeEffects.TryGetValue(effectId, out effect);
        }

        public bool TryGetEffect<T>(out T effect) where T : BaseEffectRuntime
        {
            effect = null;
            foreach (var e in _activeEffects.Values)
            {
                if (e is T typed)
                {
                    effect = typed;
                    return true;
                }
            }
            return false;
        }

        public void GetActiveEffectStates(List<EffectState> output)
        {
            if (output == null)
                return;

            output.Clear();
            foreach (var effect in _activeEffects.Values)
            {
                output.Add(effect.GetState());
            }
        }

        public void GetEffectStates(EffectType type, List<EffectState> output)
        {
            if (output == null)
                return;

            output.Clear();
            foreach (var effect in _activeEffects.Values)
            {
                if (effect.Type == type)
                {
                    output.Add(effect.GetState());
                }
            }
        }

        // ================================================================
        // ITickable
        // ================================================================

        public void Tick()
        {
            if (_disposed)
                return;

            float dt = Time.deltaTime;

            // 全 Effect の Tick
            foreach (var kvp in _activeEffects)
            {
                var effect = kvp.Value;
                effect.Tick(dt);

                if (effect.IsRemoveRequested)
                {
                    _removeQueue.Add(kvp.Key);
                }
            }

            // 削除キューを処理
            ProcessRemoveQueue();
        }

        // ================================================================
        // イベント発行
        // ================================================================

        void PublishEffectApplied(BaseEffectRuntime effect)
        {
            var payload = new VarStore();
            SetVariant(payload, "EffectId", DynamicVariant.FromString(effect.EffectId ?? string.Empty));
            SetVariant(payload, "DisplayName", DynamicVariant.FromString(effect.DisplayName ?? string.Empty));
            SetVariant(payload, "Type", DynamicVariant.FromInt((int)effect.Type));
            SetVariant(payload, "Duration", DynamicVariant.FromFloat(effect.TotalDuration));
            SetVariant(payload, "Intensity", DynamicVariant.FromFloat(effect.Intensity));

            effect.EventPayload?.MergeInto(payload, overwrite: true);

            _context.EventService.PublishAsync(OnEffectAppliedKey, payload).Forget(ex => Debug.LogException(ex));
        }

        void PublishEffectRemoved(BaseEffectRuntime effect)
        {
            var payload = new VarStore();
            SetVariant(payload, "EffectId", DynamicVariant.FromString(effect.EffectId ?? string.Empty));
            SetVariant(payload, "DisplayName", DynamicVariant.FromString(effect.DisplayName ?? string.Empty));
            SetVariant(payload, "Type", DynamicVariant.FromInt((int)effect.Type));

            effect.EventPayload?.MergeInto(payload, overwrite: true);

            _context.EventService.PublishAsync(OnEffectRemovedKey, payload).Forget(ex => Debug.LogException(ex));
        }

        static void SetVariant(IVarStore vars, string stableKey, in DynamicVariant variant)
        {
            if (vars == null) return;
            if (!VarIdResolver.TryResolve(stableKey, out var varId) || varId == 0) return;
            vars.TrySetVariant(varId, variant);
        }

        // ================================================================
        // IDisposable
        // ================================================================

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            ClearAllEffects();
            _activeEffects.Clear();
            _removeQueue.Clear();
        }
    }
}
