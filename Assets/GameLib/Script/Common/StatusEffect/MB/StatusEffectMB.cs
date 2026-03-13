// Game.StatusEffect.StatusEffectMB.cs
//
// StatusEffect 管理用の MonoBehaviour

using System;
using System.Collections.Generic;
using Game.Health;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.StatusEffect
{
    /// <summary>
    /// StatusEffect 管理用の MonoBehaviour。
    /// Entity に配置して IStatusEffectService を DI 登録する。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StatusEffectMB : MonoBehaviour, IFeatureInstaller, IDisposable
    {
        [Header("Debug")]
        [SerializeField, ReadOnly]
        int _activeEffectCount;

        [SerializeField]
        List<EffectDebugEntry> _activeEffects = new();

        IStatusEffectService _statusEffectService;
        readonly List<EffectState> _tempStates = new();
        bool _disposed;

        [Serializable]
        struct EffectDebugEntry
        {
            public string EffectId;
            public string DisplayName;
            public EffectType Type;
            public float RemainingTime;
            public float Intensity;
            public int StackCount;
        }

        public IStatusEffectService StatusEffectService => _statusEffectService;

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            builder.Register<StatusEffectService>(Lifetime.Singleton)
                .WithParameter(transform)
                .As<IStatusEffectService>()
                .As<ITickable>();
        }

        void Start()
        {
            var lts = GetComponentInParent<BaseLifetimeScope>();
            if (lts?.Container != null)
            {
                lts.Container.TryResolve(out _statusEffectService);
            }
        }

        void Update()
        {
            if (_statusEffectService == null)
                return;

            _activeEffectCount = _statusEffectService.ActiveEffectCount;

            // デバッグ表示用
            _statusEffectService.GetActiveEffectStates(_tempStates);
            _activeEffects.Clear();
            foreach (var state in _tempStates)
            {
                _activeEffects.Add(new EffectDebugEntry
                {
                    EffectId = state.EffectId,
                    DisplayName = state.DisplayName,
                    Type = state.Type,
                    RemainingTime = state.RemainingTime,
                    Intensity = state.Intensity,
                    StackCount = state.StackCount
                });
            }
        }

        void OnDestroy() => Dispose();

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            (_statusEffectService as IDisposable)?.Dispose();
            _statusEffectService = null;
        }

#if UNITY_EDITOR
        [Button("Apply Poison (5s)")]
        void DebugApplyPoison()
        {
            _statusEffectService?.ApplyEffect<PoisonEffect>(EffectConfig.Default(5f, 1f));
        }

        [Button("Apply Speed Boost (3s)")]
        void DebugApplySpeedBoost()
        {
            _statusEffectService?.ApplyEffect<SpeedBoostEffect>(EffectConfig.Default(3f, 0.5f));
        }

        [Button("Apply Invincible (2s)")]
        void DebugApplyInvincible()
        {
            _statusEffectService?.ApplyEffect<InvincibleEffect>(EffectConfig.Default(2f, 1f));
        }

        [Button("Clear All Buffs")]
        void DebugClearBuffs()
        {
            _statusEffectService?.RemoveEffects(EffectType.Buff);
        }

        [Button("Clear All Debuffs")]
        void DebugClearDebuffs()
        {
            _statusEffectService?.RemoveEffects(EffectType.Debuff);
        }

        [Button("Clear All Effects")]
        void DebugClearAll()
        {
            _statusEffectService?.ClearAllEffects();
        }
#endif
    }
}
