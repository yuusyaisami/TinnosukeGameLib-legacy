#nullable enable
using System;
using System.Collections.Generic;
using Game;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using VNext = Game.Commands.VNext;

namespace Game.Collision
{
    public enum HitColliderCommandTarget
    {
        Self = 0,
        Other = 1,
        Both = 2,
    }

    public enum HitColliderRuleCommandListSlot
    {
        OnEnter = 0,
        OnStay = 1,
        OnExit = 2,
        OnEnterSelf = 3,
        OnStaySelf = 4,
        OnExitSelf = 5,
        OnEnterOther = 6,
        OnStayOther = 7,
        OnExitOther = 8,
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(HitColliderChannelHubMB))]
    public sealed class HitColliderControllerMB : MonoBehaviour, IFeatureInstaller
    {
        [Header("Self")]
        [SerializeField]
        [Tooltip("Inspector setting.")]
        Component? _selfProvider;

        [Header("Rules")]
        [SerializeField]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
        List<HitColliderControllerRule> _rules = new();

        [Header("Debug Runtime (ReadOnly)")]
        [SerializeField, ReadOnly] bool _debugSelfHandleValid;
        [SerializeField, ReadOnly] int _debugSelfHandleId = -1;
        [SerializeField, ReadOnly] int _debugSelfHandleGeneration = -1;
        [SerializeField, ReadOnly] int _debugBindingsCount;
        [SerializeField, ReadOnly] string _debugBindState = "Idle";
        [SerializeField, ReadOnly] int _debugEnterEventCount;
        [SerializeField, ReadOnly] int _debugStayEventCount;
        [SerializeField, ReadOnly] int _debugExitEventCount;
        [SerializeField, ReadOnly] int _debugExecutedSelfCount;
        [SerializeField, ReadOnly] int _debugExecutedOtherCount;
        [SerializeField, ReadOnly] int _debugSkippedCount;
        [SerializeField, ReadOnly] string _debugLastRule = string.Empty;
        [SerializeField, ReadOnly] string _debugLastEventType = string.Empty;
        [SerializeField, ReadOnly] string _debugLastSkipReason = string.Empty;
        [SerializeField, ReadOnly] int _debugLastOtherHandleId = -1;
        [SerializeField, ReadOnly] int _debugLastOtherHandleGeneration = -1;

        public Component? SelfProvider => _selfProvider;
        public IReadOnlyList<HitColliderControllerRule> Rules => _rules;

        void Awake()
        {
            ValidateRuleSettings(logWarning: true);
            BindDebugOwners();
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            ValidateRuleSettings(logWarning: true);
            BindDebugOwners();
        }
#endif

        public void InstallFeature(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            builder.Register<HitColliderControllerService>(RuntimeLifetime.Singleton)
                .AsSelf()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .WithParameter(this)
                .WithParameter(scope);
        }

        public bool TryGetSelfHandle(out DynamicColliderHandle handle)
        {
            handle = default;

            var p = _selfProvider;
            if (p == null)
            {
                if (TryGetComponent<ColliderObjectMB>(out var colObj) && colObj != null)
                    p = colObj;
                else if (TryGetComponent<UnityColliderObjectMB>(out var unityObj) && unityObj != null)
                    p = unityObj;
            }

            if (p is ColliderObjectMB cob)
            {
                handle = cob.DynamicHandle;
                return handle.IsValid;
            }

            if (p is UnityColliderObjectMB uob)
            {
                handle = uob.DynamicHandle;
                return handle.IsValid;
            }

            return false;
        }

        void BindDebugOwners()
        {
            if (_rules == null || _rules.Count == 0)
                return;

            for (int i = 0; i < _rules.Count; i++)
            {
                var rule = _rules[i];
                if (rule == null)
                    continue;

                rule.OnEnterCommands?.BindDebugOwner(this, $"_rules[{i}].onEnterCommands");
                rule.OnStayCommands?.BindDebugOwner(this, $"_rules[{i}].onStayCommands");
                rule.OnExitCommands?.BindDebugOwner(this, $"_rules[{i}].onExitCommands");
                rule.OnEnterCommandsSelf?.BindDebugOwner(this, $"_rules[{i}].onEnterCommandsSelf");
                rule.OnStayCommandsSelf?.BindDebugOwner(this, $"_rules[{i}].onStayCommandsSelf");
                rule.OnExitCommandsSelf?.BindDebugOwner(this, $"_rules[{i}].onExitCommandsSelf");
                rule.OnEnterCommandsOther?.BindDebugOwner(this, $"_rules[{i}].onEnterCommandsOther");
                rule.OnStayCommandsOther?.BindDebugOwner(this, $"_rules[{i}].onStayCommandsOther");
                rule.OnExitCommandsOther?.BindDebugOwner(this, $"_rules[{i}].onExitCommandsOther");
            }
        }

        void ValidateRuleSettings(bool logWarning)
        {
            if (_rules == null || _rules.Count == 0)
                return;

            for (int i = 0; i < _rules.Count; i++)
            {
                var rule = _rules[i];
                if (rule == null)
                    continue;

                if (!rule.HasInvalidCounterpartContextSlot())
                    continue;

                if (logWarning)
                {
                    var ruleName = string.IsNullOrWhiteSpace(rule.Name) ? $"Rule[{i}]" : rule.Name;
                    Debug.LogWarning($"[HitColliderControllerMB] Counterpart Context Slot should use ContextA-D. Rule='{ruleName}' Slot={rule.CounterpartContextSlot}. It will fall back to ContextA at runtime.", this);
                }
            }
        }

        internal void ResetDebugRuntime()
        {
            _debugSelfHandleValid = false;
            _debugSelfHandleId = -1;
            _debugSelfHandleGeneration = -1;
            _debugBindingsCount = 0;
            _debugBindState = "Reset";
            _debugEnterEventCount = 0;
            _debugStayEventCount = 0;
            _debugExitEventCount = 0;
            _debugExecutedSelfCount = 0;
            _debugExecutedOtherCount = 0;
            _debugSkippedCount = 0;
            _debugLastRule = string.Empty;
            _debugLastEventType = string.Empty;
            _debugLastSkipReason = string.Empty;
            _debugLastOtherHandleId = -1;
            _debugLastOtherHandleGeneration = -1;
        }

        internal void SetDebugBindingState(DynamicColliderHandle self, int bindingsCount, string state)
        {
            _debugSelfHandleValid = self.IsValid;
            _debugSelfHandleId = self.IsValid ? self.Id : -1;
            _debugSelfHandleGeneration = self.IsValid ? self.Generation : -1;
            _debugBindingsCount = Mathf.Max(0, bindingsCount);
            _debugBindState = string.IsNullOrEmpty(state) ? "Unknown" : state;
        }

        internal void SetDebugBindingState(string state)
        {
            _debugBindState = string.IsNullOrEmpty(state) ? "Unknown" : state;
        }

        internal void RecordDebugEvent(string ruleName, HitEventType eventType, in RoutedHit routedHit)
        {
            _debugLastRule = ruleName ?? string.Empty;
            _debugLastEventType = eventType.ToString();
            _debugLastSkipReason = string.Empty;

            switch (eventType)
            {
                case HitEventType.Enter:
                    _debugEnterEventCount++;
                    break;
                case HitEventType.Stay:
                    _debugStayEventCount++;
                    break;
                case HitEventType.Exit:
                    _debugExitEventCount++;
                    break;
            }

            var other = routedHit.Hit.OtherDynamic;
            _debugLastOtherHandleId = other.IsValid ? other.Id : -1;
            _debugLastOtherHandleGeneration = other.IsValid ? other.Generation : -1;
        }

        internal void RecordDebugSkip(string ruleName, HitEventType eventType, string reason, in RoutedHit routedHit)
        {
            _debugSkippedCount++;
            _debugLastRule = ruleName ?? string.Empty;
            _debugLastEventType = eventType.ToString();
            _debugLastSkipReason = reason ?? string.Empty;

            var other = routedHit.Hit.OtherDynamic;
            _debugLastOtherHandleId = other.IsValid ? other.Id : -1;
            _debugLastOtherHandleGeneration = other.IsValid ? other.Generation : -1;
        }

        internal void RecordDebugExecuted(bool selfExecuted, bool otherExecuted)
        {
            if (selfExecuted)
                _debugExecutedSelfCount++;
            if (otherExecuted)
                _debugExecutedOtherCount++;
        }
    }

    [Serializable]
    public sealed class HitColliderControllerRule
    {
        [SerializeField] bool enabled = true;
        [SerializeField] string name = "default";

        [Header("Watch")]
        [SerializeField] HitWatchFlags watchFlags = HitWatchFlags.SelfAndOther;
        [SerializeField] HitEventFlags eventMask = HitEventFlags.All;

        [Header("Include / Exclude")]
        [SerializeField] bool matchAnyInclude = true;

        // Toggle-based ShowIf for clarity in the inspector
        [SerializeField] bool useIncludeStaticKinds = false;
        [ShowIf(nameof(useIncludeStaticKinds))]
        [SerializeField] StaticColliderKindRef[] includeStaticKinds = Array.Empty<StaticColliderKindRef>();

        [SerializeField] bool useIncludeDynamicSets = false;
        [ShowIf(nameof(useIncludeDynamicSets))]
        [SerializeField] DynamicColliderSetRef[] includeDynamicSets = Array.Empty<DynamicColliderSetRef>();

        [SerializeField] bool useExcludeStaticKinds = false;
        [ShowIf(nameof(useExcludeStaticKinds))]
        [SerializeField] StaticColliderKindRef[] excludeStaticKinds = Array.Empty<StaticColliderKindRef>();

        [SerializeField] bool useExcludeDynamicSets = false;
        [ShowIf(nameof(useExcludeDynamicSets))]
        [SerializeField] DynamicColliderSetRef[] excludeDynamicSets = Array.Empty<DynamicColliderSetRef>();

        [Header("Filter")]
        [SerializeField] bool useFilter = false;
        [ShowIf(nameof(useFilter))]
        [SerializeField] HitFilter filter;

        [Header("Advanced")]
        [SerializeField] bool useStaleFrameThreshold = false;
        [ShowIf(nameof(useStaleFrameThreshold))]
        [SerializeField]
        [MinValue(0)]
        [Tooltip(">=1: Exit蜿悶ｊ縺薙⊂縺怜屓蠕ｩ縺ｮ縺溘ａ縲∵怙蠕後↓隕ｳ貂ｬ縺励◆繝輔Ξ繝ｼ繝縺九ｉ荳螳壻ｻ･荳顔ｵ碁℃縺励◆謗･隗ｦ繧定・蜍募炎髯､")]
        int staleFrameThreshold = 2;

        [Header("Commands")]
        [SerializeField]
        [LabelText("Command Target")]
        [Tooltip("Self: 閾ｪ蛻・・ICommandRunner縺ｧ螳溯｡・/ Other: 繝偵ャ繝育嶌謇九・ICommandRunner縺ｧ螳溯｡・/ Both: 荳｡譁ｹ")]
        HitColliderCommandTarget commandTarget = HitColliderCommandTarget.Self;

        [ShowIf("@commandTarget == HitColliderCommandTarget.Both")]
        [SerializeField]
        [LabelText("Parallel When Both")]
        [Tooltip("Inspector setting.")]
        bool parallelWhenBoth = true;

        [SerializeField]
        [LabelText("Counterpart Context Slot")]
        [Tooltip("Inspector setting.")]
        VNext.CommandLtsSlot counterpartContextSlot = VNext.CommandLtsSlot.ContextA;

        [ShowIf(nameof(HasInvalidCounterpartContextSlot))]
        [InfoBox("Inspector info.")]
        [SerializeField, HideInInspector]
        bool _invalidCounterpartContextSlotWarning;

        [ShowIf("@HasEnter && commandTarget != HitColliderCommandTarget.Both")]
        [SerializeField] VNext.CommandListData onEnterCommands = new();
        [ShowIf("@HasStay && commandTarget != HitColliderCommandTarget.Both")]
        [SerializeField] VNext.CommandListData onStayCommands = new();
        [ShowIf("@HasExit && commandTarget != HitColliderCommandTarget.Both")]
        [SerializeField] VNext.CommandListData onExitCommands = new();

        [ShowIf(nameof(HasStay))]
        [SerializeField]
        [LabelText("Stay Interval Seconds")]
        [MinValue(0f)]
        [Tooltip("Inspector setting.")]
        float stayIntervalSeconds = 0f;

        [ShowIf("@HasEnter || HasExit")]
        [SerializeField]
        [LabelText("Enter/Exit Duplicate Interval Seconds")]
        [MinValue(0f)]
        [Tooltip("Inspector setting.")]
        float enterExitDuplicateIntervalSeconds = 0f;

        [ShowIf("@HasEnter && commandTarget == HitColliderCommandTarget.Both")]
        [SerializeField] VNext.CommandListData onEnterCommandsSelf = new();
        [ShowIf("@HasStay && commandTarget == HitColliderCommandTarget.Both")]
        [SerializeField] VNext.CommandListData onStayCommandsSelf = new();
        [ShowIf("@HasExit && commandTarget == HitColliderCommandTarget.Both")]
        [SerializeField] VNext.CommandListData onExitCommandsSelf = new();

        [ShowIf("@HasEnter && commandTarget == HitColliderCommandTarget.Both")]
        [SerializeField] VNext.CommandListData onEnterCommandsOther = new();
        [ShowIf("@HasStay && commandTarget == HitColliderCommandTarget.Both")]
        [SerializeField] VNext.CommandListData onStayCommandsOther = new();
        [ShowIf("@HasExit && commandTarget == HitColliderCommandTarget.Both")]
        [SerializeField] VNext.CommandListData onExitCommandsOther = new();

        public bool Enabled => enabled;
        public string Name => name;

        public HitWatchFlags WatchFlags => watchFlags;
        public HitEventFlags EventMask => eventMask;

        public bool MatchAnyInclude => matchAnyInclude;
        public bool UseIncludeStaticKinds => useIncludeStaticKinds;
        public StaticColliderKind[] IncludeStaticKinds => CollisionAuthoringArrayUtility.ConvertStaticKinds(includeStaticKinds);
        public bool UseIncludeDynamicSets => useIncludeDynamicSets;
        public DynamicColliderSetId[] IncludeDynamicSets => CollisionAuthoringArrayUtility.ConvertDynamicSets(includeDynamicSets);
        public bool UseExcludeStaticKinds => useExcludeStaticKinds;
        public StaticColliderKind[] ExcludeStaticKinds => CollisionAuthoringArrayUtility.ConvertStaticKinds(excludeStaticKinds);
        public bool UseExcludeDynamicSets => useExcludeDynamicSets;
        public DynamicColliderSetId[] ExcludeDynamicSets => CollisionAuthoringArrayUtility.ConvertDynamicSets(excludeDynamicSets);

        public bool UseFilter => useFilter;
        public HitFilter Filter => filter;
        public bool UseStaleFrameThreshold => useStaleFrameThreshold;
        public int StaleFrameThreshold => staleFrameThreshold;
        public HitColliderCommandTarget CommandTarget => commandTarget;
        public bool ParallelWhenBoth => parallelWhenBoth;
        public VNext.CommandLtsSlot CounterpartContextSlot => counterpartContextSlot;

        public VNext.CommandListData OnEnterCommands => onEnterCommands;
        public VNext.CommandListData OnStayCommands => onStayCommands;
        public VNext.CommandListData OnExitCommands => onExitCommands;
        public VNext.CommandListData OnEnterCommandsSelf => onEnterCommandsSelf;
        public VNext.CommandListData OnStayCommandsSelf => onStayCommandsSelf;
        public VNext.CommandListData OnExitCommandsSelf => onExitCommandsSelf;
        public VNext.CommandListData OnEnterCommandsOther => onEnterCommandsOther;
        public VNext.CommandListData OnStayCommandsOther => onStayCommandsOther;
        public VNext.CommandListData OnExitCommandsOther => onExitCommandsOther;
        public float StayIntervalSeconds => stayIntervalSeconds <= 0f ? 0f : stayIntervalSeconds;
        public float EnterExitDuplicateIntervalSeconds => enterExitDuplicateIntervalSeconds <= 0f ? 0f : enterExitDuplicateIntervalSeconds;

        public bool HasEnter => (eventMask & HitEventFlags.Enter) != 0;
        public bool HasStay => (eventMask & HitEventFlags.Stay) != 0;
        public bool HasExit => (eventMask & HitEventFlags.Exit) != 0;
        public bool HasCommandsProp => HasAnyCommands();

        public HitContactWatchSpec ToSpec()
        {
            var incS = (useIncludeStaticKinds && includeStaticKinds != null && includeStaticKinds.Length > 0)
                ? CollisionAuthoringArrayUtility.ConvertStaticKinds(includeStaticKinds)
                : null;
            var incD = (useIncludeDynamicSets && includeDynamicSets != null && includeDynamicSets.Length > 0)
                ? CollisionAuthoringArrayUtility.ConvertDynamicSets(includeDynamicSets)
                : null;
            var excS = (useExcludeStaticKinds && excludeStaticKinds != null && excludeStaticKinds.Length > 0)
                ? CollisionAuthoringArrayUtility.ConvertStaticKinds(excludeStaticKinds)
                : null;
            var excD = (useExcludeDynamicSets && excludeDynamicSets != null && excludeDynamicSets.Length > 0)
                ? CollisionAuthoringArrayUtility.ConvertDynamicSets(excludeDynamicSets)
                : null;

            var specFilter = useFilter ? filter : default;
            var stale = useStaleFrameThreshold ? staleFrameThreshold : 0;

            return HitContactWatchSpec.Create(
                watchFlags: watchFlags,
                eventMask: eventMask,
                includeStaticKinds: incS,
                includeDynamicSets: incD,
                excludeStaticKinds: excS,
                excludeDynamicSets: excD,
                matchAnyInclude: matchAnyInclude,
                filter: specFilter,
                staleFrameThreshold: stale);
        }

        public bool HasAnyCommands()
        {
            return (onEnterCommands != null && onEnterCommands.Count > 0)
                || (onStayCommands != null && onStayCommands.Count > 0)
                || (onExitCommands != null && onExitCommands.Count > 0)
                || (onEnterCommandsSelf != null && onEnterCommandsSelf.Count > 0)
                || (onStayCommandsSelf != null && onStayCommandsSelf.Count > 0)
                || (onExitCommandsSelf != null && onExitCommandsSelf.Count > 0)
                || (onEnterCommandsOther != null && onEnterCommandsOther.Count > 0)
                || (onStayCommandsOther != null && onStayCommandsOther.Count > 0)
                || (onExitCommandsOther != null && onExitCommandsOther.Count > 0);
        }

        public void SetEnabledRuntime(bool value) => enabled = value;
        public void SetWatchFlagsRuntime(HitWatchFlags value) => watchFlags = value;
        public void SetEventMaskRuntime(HitEventFlags value) => eventMask = value;
        public void SetMatchAnyIncludeRuntime(bool value) => matchAnyInclude = value;
        public void SetUseFilterRuntime(bool value) => useFilter = value;
        public void SetFilterRuntime(in HitFilter value) => filter = value;
        public void SetUseStaleFrameThresholdRuntime(bool value) => useStaleFrameThreshold = value;
        public void SetStaleFrameThresholdRuntime(int value) => staleFrameThreshold = Mathf.Max(0, value);
        public void SetCommandTargetRuntime(HitColliderCommandTarget value) => commandTarget = value;
        public void SetParallelWhenBothRuntime(bool value) => parallelWhenBoth = value;
        public void SetStayIntervalSecondsRuntime(float value) => stayIntervalSeconds = Mathf.Max(0f, value);
        public void SetCounterpartContextSlotRuntime(VNext.CommandLtsSlot value) => counterpartContextSlot = value;

        public bool HasInvalidCounterpartContextSlot()
        {
            return !VNext.CommandLtsSlotUtility.IsContextSlot(counterpartContextSlot);
        }

        public VNext.CommandLtsSlot GetEffectiveCounterpartContextSlot()
        {
            return VNext.CommandLtsSlotUtility.IsContextSlot(counterpartContextSlot)
                ? counterpartContextSlot
                : VNext.CommandLtsSlot.ContextA;
        }

        public void SetIncludeStaticKindsRuntime(bool use, StaticColliderKind[]? values)
        {
            useIncludeStaticKinds = use;
            includeStaticKinds = Convert(values);
        }

        public void SetIncludeDynamicSetsRuntime(bool use, DynamicColliderSetId[]? values)
        {
            useIncludeDynamicSets = use;
            includeDynamicSets = Convert(values);
        }

        public void SetExcludeStaticKindsRuntime(bool use, StaticColliderKind[]? values)
        {
            useExcludeStaticKinds = use;
            excludeStaticKinds = Convert(values);
        }

        public void SetExcludeDynamicSetsRuntime(bool use, DynamicColliderSetId[]? values)
        {
            useExcludeDynamicSets = use;
            excludeDynamicSets = Convert(values);
        }

        static DynamicColliderSetRef[] Convert(DynamicColliderSetId[]? values)
        {
            if (values == null || values.Length == 0)
                return Array.Empty<DynamicColliderSetRef>();

            var result = new DynamicColliderSetRef[values.Length];
            for (int i = 0; i < values.Length; i++)
                result[i] = new DynamicColliderSetRef(values[i]);

            return result;
        }

        static StaticColliderKindRef[] Convert(StaticColliderKind[]? values)
        {
            if (values == null || values.Length == 0)
                return Array.Empty<StaticColliderKindRef>();

            var result = new StaticColliderKindRef[values.Length];
            for (int i = 0; i < values.Length; i++)
                result[i] = new StaticColliderKindRef(values[i]);

            return result;
        }

        public VNext.CommandListData? ResolveCommandList(HitColliderRuleCommandListSlot slot)
        {
            return slot switch
            {
                HitColliderRuleCommandListSlot.OnEnter => onEnterCommands,
                HitColliderRuleCommandListSlot.OnStay => onStayCommands,
                HitColliderRuleCommandListSlot.OnExit => onExitCommands,
                HitColliderRuleCommandListSlot.OnEnterSelf => onEnterCommandsSelf,
                HitColliderRuleCommandListSlot.OnStaySelf => onStayCommandsSelf,
                HitColliderRuleCommandListSlot.OnExitSelf => onExitCommandsSelf,
                HitColliderRuleCommandListSlot.OnEnterOther => onEnterCommandsOther,
                HitColliderRuleCommandListSlot.OnStayOther => onStayCommandsOther,
                HitColliderRuleCommandListSlot.OnExitOther => onExitCommandsOther,
                _ => null,
            };
        }
    }
}
