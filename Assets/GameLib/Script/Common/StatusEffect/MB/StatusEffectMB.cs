using System;
using System.Collections.Generic;
using Game.Commands.VNext;
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

    public interface IStatusEffectGlobalBlackboardBindingOptions
    {
        bool UseBlackboardBinding { get; }
        ActorSource BlackboardBindingSource { get; }
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

    [Serializable]
    public sealed class StatusEffectGlobalBlackboardBindingSettings
    {
        [LabelText("Enabled")]
        [Tooltip("有効な場合、StatusEffect の global state を Blackboard に書き込みます。")]
        public bool Enabled = true;

        [ShowIf(nameof(Enabled))]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Blackboard Source\", BlackboardBindingSource)")]
        [Tooltip("global state の読み書き先に使う Blackboard スコープです。Current ならこのコンポーネントの scope を使います。")]
        public ActorSource BlackboardBindingSource = new() { Kind = ActorSourceKind.Current };
    }

    [DisallowMultipleComponent]
    public sealed class StatusEffectMB :
        MonoBehaviour,
        IFeatureInstaller,
        IScopeAcquireHandler,
        IScopeReleaseHandler,
        IStatusEffectServiceOptions,
        IStatusEffectGlobalBlackboardBindingOptions
    {
        [Serializable]
        struct EffectDebugEntry
        {
            public string EffectId;
            public string InstanceId;
            public string RuntimeTag;
            public string DisplayName;
            public EffectType Type;
            public float TotalDuration;
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
            public bool UsesServiceGlobalLifetime;
            public bool UsesServiceGlobalUseCooldown;
            public bool UsesServiceGlobalCount;
            public bool UsesAnyServiceGlobalUseState;
            public int UsedCount;
            public int RemainingUseCount;
            public int MaxUseCount;
            public int SortOrder;
        }

        [Header("Runtime Debug")]
        [SerializeField, ReadOnly, LabelText("Debug Status")]
        string _debugStatus = "(unbound)";

        [SerializeField, ReadOnly, LabelText("Registered Effect Count")]
        int _registeredEffectCount;

        [SerializeField, ReadOnly, LabelText("Active Effect Count")]
        int _activeEffectCount;

        [SerializeField, ReadOnly, LabelText("Global Runtime State")]
        StatusEffectGlobalRuntimeState _globalRuntime;

        [SerializeField, ReadOnly, ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = false, DraggableItems = false)]
        [LabelText("Runtime Effects")]
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

        [Header("Blackboard Binding")]
        [SerializeField, InlineProperty]
        [Tooltip("StatusEffect の global state を書き込む Blackboard のバインディング設定です。")]
        StatusEffectGlobalBlackboardBindingSettings _globalBlackboardBinding = new();

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

        static int CompareEffectState(EffectState left, EffectState right)
        {
            var activeCompare = right.IsActive.CompareTo(left.IsActive);
            if (activeCompare != 0)
                return activeCompare;

            var sortOrderCompare = left.SortOrder.CompareTo(right.SortOrder);
            if (sortOrderCompare != 0)
                return sortOrderCompare;

            var displayNameCompare = string.Compare(left.DisplayName, right.DisplayName, StringComparison.Ordinal);
            if (displayNameCompare != 0)
                return displayNameCompare;

            var effectIdCompare = string.Compare(left.EffectId, right.EffectId, StringComparison.Ordinal);
            if (effectIdCompare != 0)
                return effectIdCompare;

            return string.Compare(left.InstanceId, right.InstanceId, StringComparison.Ordinal);
        }

        public IStatusEffectService StatusEffectService => _statusEffectService;
        public DynamicValue<StatusEffectGlobalLifetimeSettings> GlobalLifetimeSettingsValue => _globalLifetimeSettings;
        public DynamicValue<StatusEffectGlobalUseCooldownSettings> GlobalUseCooldownSettingsValue => _globalUseCooldownSettings;
        public DynamicValue<StatusEffectGlobalCountSettings> GlobalCountSettingsValue => _globalCountSettings;
        public bool UseBlackboardBinding => _globalBlackboardBinding.Enabled;
        public ActorSource BlackboardBindingSource => _globalBlackboardBinding.BlackboardBindingSource;

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            builder.RegisterComponent(this)
                .AsSelf()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();

            builder.RegisterInstance<IStatusEffectServiceOptions>(this);
            builder.RegisterInstance<IStatusEffectGlobalBlackboardBindingOptions>(this);

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
            {
                _statusEffectService = service;
                _debugStatus = "(bound)";
                _globalRuntime = _statusEffectService.GetDebugState();
                RefreshDebugView();
            }
            else
            {
                _debugStatus = "(service missing)";
                _globalRuntime = StatusEffectGlobalRuntimeState.CreateUnavailable(_debugStatus);
                _registeredEffectCount = 0;
                _activeEffectCount = 0;
                _activeEffects.Clear();
                _tempStates.Clear();
            }
            _nextDebugRefreshTime = 0f;
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;
            _statusEffectService = null;
            _scopeNode = null;
            _debugStatus = "(unbound)";
            _activeEffects.Clear();
            _tempStates.Clear();
            _registeredEffectCount = 0;
            _activeEffectCount = 0;
            _globalRuntime = StatusEffectGlobalRuntimeState.CreateUnavailable(_debugStatus);
            _nextDebugRefreshTime = 0f;
        }

        void Update()
        {
            if (_statusEffectService == null)
                return;

            if (Time.unscaledTime < _nextDebugRefreshTime)
                return;

            _nextDebugRefreshTime = Time.unscaledTime + 0.2f;
            RefreshDebugView();
        }

        void RefreshDebugView()
        {
            var service = _statusEffectService;
            if (service == null)
            {
                _debugStatus = _scopeNode == null ? "(unbound)" : "(service missing)";
                _registeredEffectCount = 0;
                _activeEffectCount = 0;
                _globalRuntime = StatusEffectGlobalRuntimeState.CreateUnavailable(_debugStatus);
                _activeEffects.Clear();
                _tempStates.Clear();
                return;
            }

            _debugStatus = "(bound)";
            service.GetStates(_tempStates, StatusEffectRuntimeFilter.All);
            _tempStates.Sort(CompareEffectState);
            _registeredEffectCount = _tempStates.Count;
            _activeEffectCount = 0;
            _activeEffects.Clear();

            for (int i = 0; i < _tempStates.Count; i++)
            {
                var state = _tempStates[i];
                if (state.IsActive)
                    _activeEffectCount++;

                _activeEffects.Add(new EffectDebugEntry
                {
                    EffectId = state.EffectId,
                    InstanceId = state.InstanceId,
                    RuntimeTag = state.RuntimeTag,
                    DisplayName = state.DisplayName,
                    Type = state.Type,
                    TotalDuration = state.TotalDuration,
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
                    UsesServiceGlobalLifetime = state.UsesServiceGlobalLifetime,
                    UsesServiceGlobalUseCooldown = state.UsesServiceGlobalUseCooldown,
                    UsesServiceGlobalCount = state.UsesServiceGlobalCount,
                    UsesAnyServiceGlobalUseState = state.UsesAnyServiceGlobalUseState,
                    UsedCount = state.UsedCount,
                    RemainingUseCount = state.RemainingUseCount,
                    MaxUseCount = state.MaxUseCount,
                    SortOrder = state.SortOrder
                });
            }

            _globalRuntime = service.GetDebugState();
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
            RefreshDebugView();
        }

        [Button("Use All")]
        void DebugUseAll()
        {
            if (_scopeNode == null)
                return;

            _statusEffectService?.Use(StatusEffectRuntimeFilter.All, _scopeNode);
            RefreshDebugView();
        }

        [Button("Use Global")]
        void DebugUseGlobal()
        {
            if (_scopeNode == null)
                return;

            _statusEffectService?.UseGlobal(_scopeNode);
            RefreshDebugView();
        }

        [Button("Clear All Effects")]
        void DebugClearAll()
        {
            _statusEffectService?.ClearAll();
            RefreshDebugView();
        }
#endif
    }
}
