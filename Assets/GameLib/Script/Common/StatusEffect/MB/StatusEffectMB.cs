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
    public interface IStatusEffectServiceOptions
    {
        DynamicValue<StatusEffectGlobalLifetimeSettings> GlobalLifetimeSettingsValue { get; }
        DynamicValue<StatusEffectGlobalUseCooldownSettings> GlobalUseCooldownSettingsValue { get; }
        DynamicValue<StatusEffectGlobalCountSettings> GlobalCountSettingsValue { get; }
    }

    [Serializable]
    public sealed class StatusEffectGlobalLifetimeSettings : IDynamicManagedRefValue
    {
        [LabelText("Enabled")]
        [Tooltip("有効な場合、この service は shared lifetime timer を保持します。global lifetime sync runtime はこの値を参照します。")]
        public bool Enabled;

        [ShowIf(nameof(Enabled))]
        [LabelText("Duration")]
        [Tooltip("service acquire/reset 時に初期化される global lifetime 秒数です。-1 なら無期限です。")]
        public DynamicValue<float> Duration;

        public StatusEffectGlobalLifetimeSettings CreateRuntimeCopy()
        {
            return new StatusEffectGlobalLifetimeSettings
            {
                Enabled = Enabled,
                Duration = Duration,
            };
        }

        public static StatusEffectGlobalLifetimeSettings CreateDisabled()
        {
            return new StatusEffectGlobalLifetimeSettings
            {
                Enabled = false,
                Duration = default,
            };
        }
    }

    [Serializable]
    public sealed class StatusEffectGlobalUseCooldownSettings : IDynamicManagedRefValue
    {
        [LabelText("Enabled")]
        [Tooltip("有効な場合、この service は shared use cooldown を保持します。UseGlobal 実行時に開始されます。")]
        public bool Enabled;

        [ShowIf(nameof(Enabled))]
        [LabelText("Duration")]
        [Tooltip("UseGlobal 実行後に再使用可能になるまでの shared cooldown 秒数です。")]
        public DynamicValue<float> Duration;

        public StatusEffectGlobalUseCooldownSettings CreateRuntimeCopy()
        {
            return new StatusEffectGlobalUseCooldownSettings
            {
                Enabled = Enabled,
                Duration = Duration,
            };
        }

        public static StatusEffectGlobalUseCooldownSettings CreateDisabled()
        {
            return new StatusEffectGlobalUseCooldownSettings
            {
                Enabled = false,
                Duration = default,
            };
        }
    }

    [Serializable]
    public sealed class StatusEffectGlobalCountSettings : IDynamicManagedRefValue
    {
        [LabelText("Enabled")]
        [Tooltip("有効な場合、この service は shared count を保持します。UseGlobal 実行で 1 減少します。0 以下なら無制限です。")]
        public bool Enabled;

        [ShowIf(nameof(Enabled))]
        [LabelText("Max Count")]
        [Tooltip("service acquire/reset 時に初期化する shared count 上限です。0 以下なら無制限です。")]
        public DynamicValue<int> MaxCount;

        public StatusEffectGlobalCountSettings CreateRuntimeCopy()
        {
            return new StatusEffectGlobalCountSettings
            {
                Enabled = Enabled,
                MaxCount = MaxCount,
            };
        }

        public static StatusEffectGlobalCountSettings CreateDisabled()
        {
            return new StatusEffectGlobalCountSettings
            {
                Enabled = false,
                MaxCount = default,
            };
        }
    }

    [DisallowMultipleComponent]
    public sealed class StatusEffectMB :
        MonoBehaviour,
        IFeatureInstaller,
        IScopeAcquireHandler,
        IScopeReleaseHandler,
        IStatusEffectServiceOptions
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
            public float RemainingUseCooldown;
            public float IntensityA;
            public float IntensityB;
            public float IntensityC;
            public float IntensityD;
            public float IntensityE;
            public float IntensityF;
            public float IntensityG;
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

        [Header("Global Runtime")]
        [SerializeField, InlineProperty]
        [Tooltip("service 全体で共有する lifetime timer 設定です。global lifetime sync runtime が参照します。")]
        DynamicValue<StatusEffectGlobalLifetimeSettings> _globalLifetimeSettings =
            DynamicValue<StatusEffectGlobalLifetimeSettings>.FromSource(
                new ManagedRefLiteralSource<StatusEffectGlobalLifetimeSettings>(new StatusEffectGlobalLifetimeSettings()));

        [SerializeField, InlineProperty]
        [Tooltip("service 全体で共有する use cooldown 設定です。UseGlobal 実行時に開始されます。")]
        DynamicValue<StatusEffectGlobalUseCooldownSettings> _globalUseCooldownSettings =
            DynamicValue<StatusEffectGlobalUseCooldownSettings>.FromSource(
                new ManagedRefLiteralSource<StatusEffectGlobalUseCooldownSettings>(new StatusEffectGlobalUseCooldownSettings()));

        [SerializeField, InlineProperty]
        [Tooltip("service 全体で共有する count 設定です。UseGlobal 実行で消費されます。")]
        DynamicValue<StatusEffectGlobalCountSettings> _globalCountSettings =
            DynamicValue<StatusEffectGlobalCountSettings>.FromSource(
                new ManagedRefLiteralSource<StatusEffectGlobalCountSettings>(new StatusEffectGlobalCountSettings()));

        [Header("Debug Apply")]
        [SerializeField]
        global::Game.StatusEffect.StatusEffectDefinitionSO _debugDefinition;

        [SerializeField]
        float _debugIntensityA;

        [SerializeField]
        float _debugIntensityB;

        [SerializeField]
        float _debugIntensityC;

        [SerializeField]
        float _debugIntensityD;

        [SerializeField]
        float _debugIntensityE;

        [SerializeField]
        float _debugIntensityF;

        [SerializeField]
        float _debugIntensityG;

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
        public DynamicValue<StatusEffectGlobalLifetimeSettings> GlobalLifetimeSettingsValue => _globalLifetimeSettings;
        public DynamicValue<StatusEffectGlobalUseCooldownSettings> GlobalUseCooldownSettingsValue => _globalUseCooldownSettings;
        public DynamicValue<StatusEffectGlobalCountSettings> GlobalCountSettingsValue => _globalCountSettings;

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            builder.RegisterComponent(this)
                .AsSelf()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();

            builder.RegisterInstance<IStatusEffectServiceOptions>(this);

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
                    RemainingUseCooldown = state.RemainingUseCooldown,
                    IntensityA = state.IntensityA,
                    IntensityB = state.IntensityB,
                    IntensityC = state.IntensityC,
                    IntensityD = state.IntensityD,
                    IntensityE = state.IntensityE,
                    IntensityF = state.IntensityF,
                    IntensityG = state.IntensityG,
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
                IntensityA = DynamicValue<float>.FromSource(LiteralSource.FromFloat(_debugIntensityA)),
                IntensityB = DynamicValue<float>.FromSource(LiteralSource.FromFloat(_debugIntensityB)),
                IntensityC = DynamicValue<float>.FromSource(LiteralSource.FromFloat(_debugIntensityC)),
                IntensityD = DynamicValue<float>.FromSource(LiteralSource.FromFloat(_debugIntensityD)),
                IntensityE = DynamicValue<float>.FromSource(LiteralSource.FromFloat(_debugIntensityE)),
                IntensityF = DynamicValue<float>.FromSource(LiteralSource.FromFloat(_debugIntensityF)),
                IntensityG = DynamicValue<float>.FromSource(LiteralSource.FromFloat(_debugIntensityG)),
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

        [Button("Use Global")]
        void DebugUseGlobal()
        {
            if (_scopeNode == null)
                return;

            _statusEffectService?.UseGlobal(_scopeNode);
        }

        [Button("Clear All Effects")]
        void DebugClearAll()
        {
            _statusEffectService?.ClearAll();
        }
#endif
    }
}
