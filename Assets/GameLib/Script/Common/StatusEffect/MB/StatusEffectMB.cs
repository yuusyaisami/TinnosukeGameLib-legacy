using System;
using System.Collections.Generic;
using Game.Common;
using Game.Health;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.StatusEffect
{
    [DisallowMultipleComponent]
    public sealed class StatusEffectMB :
        MonoBehaviour,
        IFeatureInstaller,
        IScopeAcquireHandler,
        IScopeReleaseHandler
    {
        [Serializable]
        struct EffectDebugEntry
        {
            public string EffectId;
            public string InstanceId;
            public string RuntimeTag;
            public string DisplayName;
            public EffectType Type;
            public float RemainingTime;
            public float RemainingInverseInterval;
            public float Intensity;
            public int StackCount;
            public bool IsEnabled;
            public bool IsApplied;
            public bool IsActive;
            public bool IsUseBlocked;
            public int UsedCount;
            public int RemainingUseCount;
            public int MaxUseCount;
        }

        [Header("Debug")]
        [SerializeField, ReadOnly]
        int _activeEffectCount;

        [SerializeField]
        List<EffectDebugEntry> _activeEffects = new();

        [Header("Debug Apply")]
        [SerializeField]
        global::Game.StatusEffect.StatusEffectDefinitionSO _debugDefinition;

        [SerializeField]
        float _debugIntensity = 1f;

        [SerializeField]
        bool _debugOverrideDuration;

        [SerializeField, ShowIf(nameof(_debugOverrideDuration))]
        float _debugDuration = -1f;

        [SerializeField]
        string _debugRuntimeTag = string.Empty;

        readonly List<EffectState> _tempStates = new();
        IStatusEffectService _statusEffectService;
        IScopeNode _scopeNode;
        float _nextDebugRefreshTime;

        public IStatusEffectService StatusEffectService => _statusEffectService;

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            builder.RegisterComponent(this)
                .AsSelf()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();

            builder.Register<StatusEffectService>(Lifetime.Singleton)
                .WithParameter(scope)
                .As<IStatusEffectService>()
                .As<ITickable>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _ = isReset;
            var resolver = scope?.Resolver;
            _scopeNode = scope;
            if (resolver != null && resolver.TryResolve<IStatusEffectService>(out var service) && service != null)
                _statusEffectService = service;
            _nextDebugRefreshTime = 0f;
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;
            _statusEffectService = null;
            _scopeNode = null;
            _activeEffects.Clear();
            _tempStates.Clear();
            _activeEffectCount = 0;
            _nextDebugRefreshTime = 0f;
        }

        void Update()
        {
            if (_statusEffectService == null)
                return;

            if (Time.unscaledTime < _nextDebugRefreshTime)
                return;

            _nextDebugRefreshTime = Time.unscaledTime + 0.2f;
            _statusEffectService.GetActiveEffectStates(_tempStates);
            _activeEffectCount = _tempStates.Count;
            _activeEffects.Clear();

            for (int i = 0; i < _tempStates.Count; i++)
            {
                var state = _tempStates[i];
                _activeEffects.Add(new EffectDebugEntry
                {
                    EffectId = state.EffectId,
                    InstanceId = state.InstanceId,
                    RuntimeTag = state.RuntimeTag,
                    DisplayName = state.DisplayName,
                    Type = state.Type,
                    RemainingTime = state.RemainingTime,
                    RemainingInverseInterval = state.RemainingInverseInterval,
                    Intensity = state.Intensity,
                    StackCount = state.StackCount,
                    IsEnabled = state.IsEnabled,
                    IsApplied = state.IsApplied,
                    IsActive = state.IsActive,
                    IsUseBlocked = state.IsUseBlocked,
                    UsedCount = state.UsedCount,
                    RemainingUseCount = state.RemainingUseCount,
                    MaxUseCount = state.MaxUseCount
                });
            }
        }

#if UNITY_EDITOR
        [Button("Apply Debug Definition")]
        void DebugApplyDefinition()
        {
            if (_statusEffectService == null || _debugDefinition == null)
                return;
            if (_scopeNode == null)
                return;

            var request = new StatusEffectApplyRequest
            {
                Definition = DynamicValue<BaseStatusEffectDefinitionData>.FromSource(
                    ManagedRefSource.FromValue(_debugDefinition.Preset)),
                Intensity = DynamicValue<float>.FromSource(LiteralSource.FromFloat(_debugIntensity)),
                OverrideDuration = _debugOverrideDuration,
                DurationOverride = DynamicValue<float>.FromSource(LiteralSource.FromFloat(_debugDuration)),
                RuntimeTag = _debugRuntimeTag ?? string.Empty,
                HookMutations = new StatusEffectHookMutationSet(),
            };

            var context = new SimpleDynamicContext(NullVarStore.Instance, _scopeNode);
            _statusEffectService.TryApply(request, context, out _);
        }

        [Button("Use All")]
        void DebugUseAll()
        {
            if (_scopeNode == null)
                return;

            _statusEffectService?.Use(StatusEffectRuntimeFilter.All, _scopeNode);
        }

        [Button("Clear All Effects")]
        void DebugClearAll()
        {
            _statusEffectService?.ClearAll();
        }
#endif
    }
}
