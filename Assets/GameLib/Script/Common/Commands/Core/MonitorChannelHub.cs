// MonitorChannelHub.cs
// 
// MonitorChannelHub v2.0
// 繝ｫ繝ｼ繝ｫ逶｣隕悶す繧ｹ繝・Β縺ｮ繝上ヶ縲・
// Runtime 縺ｮ繝ｩ繧､繝輔し繧､繧ｯ繝ｫ邂｡逅・〃arStore 邂｡逅・ゝick 縺ｮ蟋碑ｭｲ繧呈球蠖薙・
// 螳滄圀縺ｮ譚｡莉ｶ隧穂ｾ｡繝ｻ繧ｳ繝槭Φ繝牙ｮ溯｡後・ MonitorChannelRuntime 縺梧球蠖薙・
//
// 險ｭ險域ｱｺ螳・
// - Hub: Tick縲〃arStore 邂｡逅・ヽuntime 繝ｩ繧､繝輔し繧､繧ｯ繝ｫ邂｡逅・・縺ｿ
// - Runtime: 譚｡莉ｶ隧穂ｾ｡縲∫憾諷矩・遘ｻ縲√さ繝槭Φ繝牙ｮ溯｡後√う繝吶Φ繝亥・逅・
// - fire-and-forget 遖∵ｭ｢・亥ｿ・★ await, 繧ｿ繧ｹ繧ｯ霑ｽ霍｡・倶ｾ句､悶Ο繧ｰ・・
// - IL2CPP / WebGL 蟇ｾ蠢懶ｼ・ecord/required 遖∵ｭ｢・・
// - GC譛蟆丞喧・医Μ繧ｹ繝医く繝｣繝・す繝･縲，lear蜀榊茜逕ｨ・・

#nullable enable
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;
using Game.Common;
using Game.Scalar;
using VContainer.Unity;
using System.Linq;
using VContainer;
using VNext = Game.Commands.VNext;

namespace Game.Commands
{
    /// <summary>
    /// 隧穂ｾ｡繝｢繝ｼ繝峨・
    /// </summary>
    public enum MonitorEvaluationMode
    {
        /// <summary>豈弱ヵ繝ｬ繝ｼ繝譚｡莉ｶ繧定ｩ穂ｾ｡縲・/summary>
        Polling = 0,
        /// <summary>萓晏ｭ倥く繝ｼ螟画峩譎ゅ・縺ｿ隧穂ｾ｡縲・/summary>
        EventDriven = 1,
        /// <summary>螟夜Κ縺九ｉ譏守､ｺ逧・↓隧穂ｾ｡繧貞他縺ｶ縲・/summary>
        Manual = 2,
    }

    /// <summary>
    /// 繝ｫ繝ｼ繝ｫ縺ｮ隧穂ｾ｡譁ｹ蠑上・
    /// </summary>
    public enum MonitorRuleKind
    {
        /// <summary>
        /// 譚｡莉ｶ縺ｮ縺ｿ縺ｧ隧穂ｾ｡縲・nter/Exit/WhileTrue 繧呈擅莉ｶ驕ｷ遘ｻ縺ｧ逋ｺ轣ｫ縲・
        /// </summary>
        ConditionOnly = 0,

        /// <summary>
        /// 繧､繝吶Φ繝医・縺ｿ縺ｧ逋ｺ轣ｫ縲よ擅莉ｶ縺ｯ辟｡隕悶＠縲∝､夜Κ繧､繝吶Φ繝茨ｼ・otifyEvent・峨〒蜊ｳ譎ょｮ溯｡後・
        /// </summary>
        EventOnly = 1,

        /// <summary>
        /// 繧､繝吶Φ繝茨ｼ区擅莉ｶ縲ゅう繝吶Φ繝亥女菫｡譎ゅ↓譚｡莉ｶ繧定ｩ穂ｾ｡縺励》rue 縺ｪ繧牙ｮ溯｡後・
        /// </summary>
        EventAndCondition = 2,

        /// <summary>
        /// 迚ｹ螳壹・蛟､縺悟､牙喧縺励◆譎ゅ↓螳溯｡後・
        /// VarStore / Blackboard / Scalar 縺ｮ縺ｿ蟇ｾ蠢懊・
        /// </summary>
        ValueChanged = 3,
    }

    public enum MonitorValueSourceKind
    {
        VarStore = 0,
        Blackboard = 1,
        Scalar = 2,
    }

    public enum MonitorValueChangeMode
    {
        AnyChange = 0,
        Increased = 1,
        Decreased = 2,
    }

    public enum MonitorValueChangedMode
    {
        Simple = 0,
        AnyTarget = 1,
    }

    [Serializable]
    public sealed class MonitorValueChangedTarget
    {
        [PropertyOrder(0)]
        [LabelText("Value Source")]
        public MonitorValueSourceKind ValueSource = MonitorValueSourceKind.VarStore;

        [PropertyOrder(10)]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Value Target\", ValueTarget)")]
        public VNext.ActorSource ValueTarget;

        [PropertyOrder(20)]
        [LabelText("Change Mode")]
        public MonitorValueChangeMode ValueChangeMode = MonitorValueChangeMode.AnyChange;

        [PropertyOrder(30)]
        [ShowIf(nameof(IsVarStoreSource))]
        [LabelText("VarStore Var Id"), VarIdDropdown]
        public int VarStoreVarId;

        [PropertyOrder(40)]
        [ShowIf(nameof(IsBlackboardSource))]
        [LabelText("Blackboard Var Id"), VarIdDropdown]
        public int BlackboardVarId;

        [PropertyOrder(50)]
        [ShowIf(nameof(IsBlackboardSource))]
        [LabelText("Blackboard Read Scope")]
        public BlackboardReadScope BlackboardReadScope;

        [PropertyOrder(60)]
        [ShowIf(nameof(IsScalarSource))]
        [LabelText("Scalar Key")]
        public ScalarKey ScalarKey;

        [PropertyOrder(70)]
        [LabelText("Change Epsilon")]
        [MinValue(0f)]
        public float ChangeEpsilon;

        bool IsVarStoreSource => ValueSource == MonitorValueSourceKind.VarStore;
        bool IsBlackboardSource => ValueSource == MonitorValueSourceKind.Blackboard;
        bool IsScalarSource => ValueSource == MonitorValueSourceKind.Scalar;
    }

    /// <summary>
    /// Command 螳溯｡後・繝昴Μ繧ｷ繝ｼ・・hileTrue 縺ｮ謖吝虚縺ｪ縺ｩ・峨・
    /// </summary>
    public enum ExecutionBehavior
    {
        SkipIfRunning = 0,
        CancelAndRun = 1,
        AllowConcurrent = 2,
    }

    /// <summary>
    /// While 繧ｳ繝槭Φ繝峨・蜀榊ｮ溯｡梧婿豕輔・
    /// </summary>
    public enum MonitorRuleWhileRepeatMode
    {
        Interval = 0,
        AfterAllCompleted = 1,
    }

    /// <summary>
    /// 險ｭ螳壽ｸ医∩縺ｮ While 繧ｳ繝槭Φ繝臥ｾ､・・rue/false 縺昴ｌ縺槭ｌ・・
    /// </summary>
    [Serializable]
    public struct MonitorRuleWhileCommandSet
    {
        [LabelText("Repeat Mode"), LabelWidth(120)]
        [EnumToggleButtons]
        [Tooltip("Inspector setting.")]
        public MonitorRuleWhileRepeatMode RepeatMode;

        [LabelText("Commands"), LabelWidth(120)]
        [VNext.CommandListFunctionName("MonitorRule.While")]
        [Tooltip("Inspector setting.")]
        public VNext.CommandListData Commands;

        [ShowIf("@RepeatMode == MonitorRuleWhileRepeatMode.Interval")]
        [LabelText("Interval (sec)"), LabelWidth(120)]
        [MinValue(0f)]
        [Tooltip("Inspector setting.")]
        public float IntervalSeconds;
    }

    /// <summary>
    /// 繝ｫ繝ｼ繝ｫ螳夂ｾｩ・・nter/Exit/While・峨・
    /// IL2CPP 蟇ｾ蠢懊・縺溘ａ record struct 荳堺ｽｿ逕ｨ縲・
    /// </summary>
    [Serializable]
    public struct MonitorRule
    {
        [PropertyOrder(0)]
        [LabelText("Rule Key")]
        [Tooltip("Inspector setting.")]
        public string RuleName;

        [PropertyOrder(10)]
        [LabelText("Rule Kind")]
        [EnumToggleButtons]
        [Tooltip("Inspector setting.")]
        public MonitorRuleKind RuleKind;

        [PropertyOrder(20)]
        [ShowIf("@RuleKind == MonitorRuleKind.EventOnly || RuleKind == MonitorRuleKind.EventAndCondition")]
        [EventKeyDropdown]
        [LabelText("Event Name")]
        [Tooltip("Inspector setting.")]
        public string EventName;

        [PropertyOrder(21)]
        [ShowIf("@RuleKind == MonitorRuleKind.EventOnly || RuleKind == MonitorRuleKind.EventAndCondition")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Event Target\", EventTarget)")]
        [Tooltip("Inspector setting.")]
        public VNext.ActorSource EventTarget;

        [PropertyOrder(30)]
        [ShowIf("@RuleKind == MonitorRuleKind.ConditionOnly || RuleKind == MonitorRuleKind.EventAndCondition")]
        [LabelText("Condition")]
        [Tooltip("Inspector setting.")]
        public DynamicValue<bool> Condition;

        [PropertyOrder(40)]
        [ShowIf("@RuleKind == MonitorRuleKind.ConditionOnly")]
        [LabelText("Execute Initial Condition")]
        [Tooltip("Inspector setting.")]
        public bool ExecuteInitialCondition;

        [PropertyOrder(40)]
        [ShowIf("@RuleKind == MonitorRuleKind.ValueChanged")]
        [LabelText("Value Changed Mode")]
        [EnumToggleButtons]
        [Tooltip("Inspector setting.")]
        public MonitorValueChangedMode ValueChangedMode;

        [PropertyOrder(41)]
        [ShowIf("@RuleKind == MonitorRuleKind.ValueChanged")]
        [InlineProperty]
        [HideLabel]
        [LabelText("Simple Target")]
        [ShowIf("@RuleKind == MonitorRuleKind.ValueChanged && ValueChangedMode == MonitorValueChangedMode.Simple")]
        [Tooltip("Inspector setting.")]
        public MonitorValueChangedTarget SimpleValueChangedTarget;

        [PropertyOrder(42)]
        [ShowIf("@RuleKind == MonitorRuleKind.ValueChanged && ValueChangedMode == MonitorValueChangedMode.AnyTarget")]
        [LabelText("Value Targets")]
        [ListDrawerSettings(ShowFoldout = true, DraggableItems = false, DefaultExpandedState = true)]
        [Tooltip("Inspector setting.")]
        public List<MonitorValueChangedTarget> ValueChangedTargets;

        [PropertyOrder(48)]
        [ShowIf("@RuleKind == MonitorRuleKind.ValueChanged")]
        [LabelText("Execute Initial Enter")]
        [Tooltip("Inspector setting.")]
        public bool ExecuteInitialValueChangedEnter;

        [PropertyOrder(49)]
        [ShowIf("@RuleKind == MonitorRuleKind.ValueChanged && ExecuteInitialValueChangedEnter")]
        [LabelText("Initial Enter Delay Seconds")]
        [MinValue(0f)]
        public float InitialValueChangedEnterDelaySeconds;

        [PropertyOrder(100)]
        [LabelText("On Enter Commands")]
        [VNext.CommandListFunctionName("MonitorRule.OnEnter")]
        [Tooltip("Inspector setting.")]
        public VNext.CommandListData OnEnterCommands;

        [PropertyOrder(110)]
        [ShowIf("@RuleKind == MonitorRuleKind.ConditionOnly || RuleKind == MonitorRuleKind.EventAndCondition")]
        [LabelText("On Exit Commands")]
        [VNext.CommandListFunctionName("MonitorRule.OnExit")]
        [Tooltip("Inspector setting.")]
        public VNext.CommandListData OnExitCommands;

        [PropertyOrder(120)]
        [ShowIf("@RuleKind == MonitorRuleKind.ConditionOnly || RuleKind == MonitorRuleKind.EventAndCondition")]
        [LabelText("While True Commands")]
        [InlineProperty]
        [Tooltip("Inspector setting.")]
        public MonitorRuleWhileCommandSet WhileTrueCommands;

        [PropertyOrder(130)]
        [ShowIf("@RuleKind == MonitorRuleKind.ConditionOnly || RuleKind == MonitorRuleKind.EventAndCondition")]
        [LabelText("While False Commands")]
        [InlineProperty]
        [Tooltip("Inspector setting.")]
        public MonitorRuleWhileCommandSet WhileFalseCommands;

        [PropertyOrder(135)]
        [ShowIf("@RuleKind == MonitorRuleKind.ConditionOnly || RuleKind == MonitorRuleKind.EventAndCondition")]
        [LabelText("Cancel Running On Change")]
        [Tooltip("Inspector setting.")]
        public bool CancelRunningOnConditionChange;

        [HideInInspector]
        public bool CancelRunningOnConditionChangeInitialized;

        [PropertyOrder(140)]
        [LabelText("Execution Behavior")]
        [Tooltip("Inspector setting.")]
        public ExecutionBehavior Behavior;

        public void EnsureDefaults()
        {
            if (!CancelRunningOnConditionChangeInitialized)
            {
                CancelRunningOnConditionChange = true;
                CancelRunningOnConditionChangeInitialized = true;
            }

            SimpleValueChangedTarget ??= new MonitorValueChangedTarget();
            ValueChangedTargets ??= new List<MonitorValueChangedTarget>();
        }

        public int GetValueChangedTargetCount()
        {
            if (RuleKind != MonitorRuleKind.ValueChanged)
                return 0;

            if (ValueChangedMode == MonitorValueChangedMode.AnyTarget)
                return ValueChangedTargets?.Count ?? 0;

            return SimpleValueChangedTarget != null ? 1 : 0;
        }

        public MonitorValueChangedTarget? GetValueChangedTarget(int index)
        {
            if (index < 0 || RuleKind != MonitorRuleKind.ValueChanged)
                return null;

            if (ValueChangedMode == MonitorValueChangedMode.AnyTarget)
            {
                if (ValueChangedTargets == null || index >= ValueChangedTargets.Count)
                    return null;
                return ValueChangedTargets[index];
            }

            return index == 0 ? SimpleValueChangedTarget : null;
        }
    }

    /// <summary>
    /// 螳溯｡御ｸｭ繧ｿ繧ｹ繧ｯ諠・ｱ・亥盾辣ｧ蝙具ｼ峨・
    /// </summary>
    public sealed class RunningEntry
    {
        public readonly string RuleName;
        public readonly string Phase;
        public readonly CancellationTokenSource Cts;
        public Cysharp.Threading.Tasks.UniTask Task;
        public bool Completed;
        public bool Disposed;

        public RunningEntry(string ruleName, string phase, CancellationTokenSource cts)
        {
            RuleName = ruleName;
            Phase = phase;
            Cts = cts;
            Completed = false;
            Disposed = false;
        }
    }

    /// <summary>Running entry snapshot for telemetry.</summary>
    public readonly struct MonitorRunningSnapshot
    {
        public readonly string RuleName;
        public readonly string Phase;
        public readonly bool Completed;

        public MonitorRunningSnapshot(string ruleName, string phase, bool completed)
        {
            RuleName = ruleName;
            Phase = phase;
            Completed = completed;
        }
    }

    /// <summary>Rule snapshot for telemetry (蠕梧婿莠呈鋤逕ｨ).</summary>
    public readonly struct MonitorRuleSnapshot
    {
        public readonly string RuleName;
        public readonly bool IsTrue;
        public readonly ExecutionBehavior Behavior;
        public readonly bool CancelRunningOnConditionChange;
        public readonly string? Condition;
        public readonly IReadOnlyList<string> DependentKeys;

        public MonitorRuleSnapshot(
            string ruleName,
            bool isTrue,
            ExecutionBehavior behavior,
            bool cancelRunningOnConditionChange,
            string? condition,
            IReadOnlyList<string> dependentKeys)
        {
            RuleName = ruleName;
            IsTrue = isTrue;
            Behavior = behavior;
            CancelRunningOnConditionChange = cancelRunningOnConditionChange;
            Condition = condition;
            DependentKeys = dependentKeys;
        }
    }

    /// <summary>Variable snapshot for telemetry.</summary>
    public readonly struct MonitorVariableSnapshot
    {
        public readonly string Key;
        public readonly string Type;
        public readonly string Value;
        public readonly int Version;

        public MonitorVariableSnapshot(string key, string type, string value, int version)
        {
            Key = key;
            Type = type;
            Value = value;
            Version = version;
        }
    }

    /// <summary>Hub snapshot for telemetry.</summary>
    public readonly struct MonitorHubSnapshot
    {
        public readonly int Version;
        public readonly MonitorEvaluationMode EvaluationMode;
        public readonly ExecutionBehavior DefaultExecutionBehavior;
        public readonly IReadOnlyList<MonitorRuleSnapshot> Rules;
        public readonly IReadOnlyList<MonitorRunningSnapshot> RunningEntries;
        public readonly IReadOnlyList<MonitorVariableSnapshot> Variables;

        public MonitorHubSnapshot(
            int version,
            MonitorEvaluationMode evaluationMode,
            ExecutionBehavior defaultExecutionBehavior,
            IReadOnlyList<MonitorRuleSnapshot> rules,
            IReadOnlyList<MonitorRunningSnapshot> runningEntries,
            IReadOnlyList<MonitorVariableSnapshot> variables)
        {
            Version = version;
            EvaluationMode = evaluationMode;
            DefaultExecutionBehavior = defaultExecutionBehavior;
            Rules = rules;
            RunningEntries = runningEntries;
            Variables = variables;
        }
    }

    public interface IMonitorChannelHubTelemetry
    {
        int TelemetryVersion { get; }
        MonitorHubSnapshot GetSnapshot();
    }

    /// <summary>
    /// MonitorChannelHub 繧､繝ｳ繧ｿ繝ｼ繝輔ぉ繝ｼ繧ｹ縲・
    /// </summary>
    public interface IMonitorChannelHub : IDisposable
    {
        MonitorEvaluationMode EvaluationMode { get; set; }
        ExecutionBehavior DefaultExecutionBehavior { get; set; }

        void AddRule(MonitorRule rule);
        void RemoveRule(string ruleName);
        void ClearRules();

        void AttachToVars(IVarStore vars);
        void DetachFromVars(IVarStore? vars);

        IVarStore? CurrentVarStore { get; }

        void NotifyEvent(string eventName);

        void SetVariable<T>(string key, T value);

        /// <summary>
        /// 蜈ｨ Runtime 縺ｮ螳溯｡御ｸｭ繧ｿ繧ｹ繧ｯ繧貞叙蠕・
        /// </summary>
        IReadOnlyList<RunningEntry> GetAllRunningEntries();
    }

    /// <summary>
    /// MonitorChannelHub - Runtime 縺ｮ繝ｩ繧､繝輔し繧､繧ｯ繝ｫ邂｡逅・→ Tick 蟋碑ｭｲ縺ｮ縺ｿ繧呈球蠖薙・
    /// 螳滄圀縺ｮ逶｣隕悶・繧ｳ繝槭Φ繝牙ｮ溯｡後・ MonitorChannelRuntime 縺瑚｡後≧縲・
    /// </summary>
    public sealed class MonitorChannelHub : IMonitorChannelHub, IScopeTickHandler, IMonitorChannelHubTelemetry
    {
        // ================================================================
        // 螳壽焚
        // ================================================================

        const int InitialRuntimeCapacity = 8;

        // ================================================================
        // DI 萓晏ｭ・
        // ================================================================

        readonly IScopeNode _scope;
        readonly VNext.ICommandRunner _runner;

        // ================================================================
        // 繝輔ぅ繝ｼ繝ｫ繝・
        // ================================================================

        /// <summary>Runtime 繝ｪ繧ｹ繝・/summary>
        readonly List<MonitorChannelRuntime> _runtimes = new(InitialRuntimeCapacity);

        /// <summary>霑ｽ蜉蠕・■繝ｫ繝ｼ繝ｫ</summary>
        readonly List<MonitorRule> _pendingAdds = new(4);

        /// <summary>蜑企勁蠕・■繝ｫ繝ｼ繝ｫ蜷・/summary>
        readonly List<string> _pendingRemoves = new(4);

        /// <summary>Vars・・ub 縺檎ｮ｡逅・ｼ・/summary>
        IVarStore? _vars;

        // Scalar -> VarStore bridge subscriptions (scope chain)
        readonly List<IDisposable> _scalarBridgeSubscriptions = new(2);

        /// <summary>蜀・Κ CTS</summary>
        CancellationTokenSource? _cts;
        readonly Dictionary<string, ScalarBridgeCacheEntry> _scalarBridgeCache = new(StringComparer.Ordinal);

        bool _debugTickLogged;
        bool _isTicking;
        bool _isApplyingPending;

        MonitorEvaluationMode _evaluationMode = MonitorEvaluationMode.EventDriven;
        ExecutionBehavior _defaultExecutionBehavior = ExecutionBehavior.SkipIfRunning;
        int _telemetryVersion;
        bool _disposed;

        readonly struct ScalarBridgeCacheEntry
        {
            public readonly int FullVarId;
            public readonly int LeafVarId;

            public ScalarBridgeCacheEntry(int fullVarId, int leafVarId)
            {
                FullVarId = fullVarId;
                LeafVarId = leafVarId;
            }
        }

        // ================================================================
        // 繝励Ο繝代ユ繧｣
        // ================================================================

        public MonitorEvaluationMode EvaluationMode
        {
            get => _evaluationMode;
            set
            {
                _evaluationMode = value;
                BumpTelemetry();
            }
        }

        public ExecutionBehavior DefaultExecutionBehavior
        {
            get => _defaultExecutionBehavior;
            set
            {
                _defaultExecutionBehavior = value;
                BumpTelemetry();
            }
        }

        public int TelemetryVersion => _telemetryVersion;

        public IVarStore? CurrentVarStore => _vars;

        // ================================================================
        // 繧ｳ繝ｳ繧ｹ繝医Λ繧ｯ繧ｿ
        // ================================================================

        public MonitorChannelHub(IScopeNode scope, VNext.ICommandRunner runner)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
            _cts = new CancellationTokenSource();

            // Always have a VarStore so Monitor rules can tick even if nobody explicitly attaches one.
            // This prevents MonitorRuleMB-only usage from becoming a no-op.
            _vars = new VarStore();
            _vars.OnVarChanged += OnVarChanged;
            ApplyRunnerDefaultVars(_vars);

            // Bridge scalar changes into VarStore so EventDriven condition rules can react to scalars
            // (expressions resolve identifiers via VarIdResolver/VarStore by default).
            TryEnsureScalarBridge();

            // Force-visible one-shot breadcrumb: if you don't see this, the hub isn't being constructed at all.
            //Debug.LogError($"[MonitorChannelHub] Constructed. ScopeKind={_scope.Kind}, ScopeId={_scope.Identity?.Id ?? "(none)"}");
        }

        // ================================================================
        // IScopeTickHandler・・Container 縺梧ｯ弱ヵ繝ｬ繝ｼ繝蜻ｼ縺ｳ蜃ｺ縺呻ｼ・
        // ================================================================

        void IScopeTickHandler.Tick()
        {
            if (_disposed) return;
            if (_cts == null) return;

            Tick(_cts.Token);
        }

        void Tick(CancellationToken ct)
        {
            if (ct.IsCancellationRequested) return;

            // 繝ｩ繝ｳ繧ｿ繧､繝繧ゆｿ晉蕗繧ゅ↑縺・ｴ蜷医・譌ｩ譛溘Μ繧ｿ繝ｼ繝ｳ
            if (_runtimes.Count == 0 && _pendingAdds.Count == 0 && _pendingRemoves.Count == 0)
                return;

            _isTicking = true;
            try
            {
                // Pending 蜃ｦ逅・
                ApplyPending();

                // 蜈ｨ Runtime 縺ｮ Tick
                for (int i = 0; i < _runtimes.Count; i++)
                {
                    if (!_runtimes[i].RequiresTick(_evaluationMode))
                        continue;

                    _runtimes[i].Tick(_evaluationMode, ct);
                }
            }
            finally
            {
                _isTicking = false;
            }
        }

        // ================================================================
        // 繝ｫ繝ｼ繝ｫ邂｡逅・
        // ================================================================

        public void AddRule(MonitorRule rule)
        {
            if (string.IsNullOrEmpty(rule.RuleName))
            {
                Debug.LogWarning("[MonitorChannelHub] RuleName is empty. Skipping.");
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Debug.Log($"[MonitorChannelHub] AddRule: '{rule.RuleName}' Scope={DescribeScope(_scope)}");
#endif
            _pendingAdds.Add(rule);

            // Apply immediately so rules become active even if the first tick already happened
            // (Acquire handlers can run after IScopeTickHandler order depending on dispatcher timing).
            if (!_isTicking)
                ApplyPending();
        }

        public void RemoveRule(string ruleName)
        {
            if (string.IsNullOrEmpty(ruleName)) return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            //Debug.Log($"[MonitorChannelHub] RemoveRule: '{ruleName}' Scope={DescribeScope(_scope)}");
#endif
            if (!_pendingRemoves.Contains(ruleName))
                _pendingRemoves.Add(ruleName);

            // pendingAdds 縺九ｉ繧ょ炎髯､
            for (int i = _pendingAdds.Count - 1; i >= 0; i--)
            {
                if (_pendingAdds[i].RuleName == ruleName)
                    _pendingAdds.RemoveAt(i);
            }

            if (!_isTicking)
                ApplyPending();
        }

        public void ClearRules()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            //Debug.Log($"[MonitorChannelHub] ClearRules Scope={DescribeScope(_scope)} Count={_runtimes.Count}");
#endif
            for (int i = 0; i < _runtimes.Count; i++)
            {
                _pendingRemoves.Add(_runtimes[i].RuleName);
            }
            _pendingAdds.Clear();

            if (!_isTicking)
                ApplyPending();
        }

        void ApplyPending()
        {
            if (_isApplyingPending)
                return;

            _isApplyingPending = true;
            bool changed = false;
            var beforeCount = _runtimes.Count;

            try
            {
                // 蜑企勁蜃ｦ逅・
                if (_pendingRemoves.Count > 0)
                {
                    for (int r = 0; r < _pendingRemoves.Count; r++)
                    {
                        var name = _pendingRemoves[r];
                        for (int i = _runtimes.Count - 1; i >= 0; i--)
                        {
                            if (_runtimes[i].RuleName == name)
                            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                                //Debug.Log($"[MonitorChannelHub] Remove runtime: '{name}' Scope={DescribeScope(_scope)}");
#endif
                                try { /* Debug: Removing runtime - suppressed */ } catch { }
                                _runtimes[i].Dispose();
                                _runtimes.RemoveAt(i);
                                changed = true;
                            }
                        }
                    }
                    _pendingRemoves.Clear();
                }

                // 霑ｽ蜉蜃ｦ逅・
                if (_pendingAdds.Count > 0)
                {
                    for (int a = 0; a < _pendingAdds.Count; a++)
                    {
                        var rule = _pendingAdds[a];
                        var name = rule.RuleName;

                        // Remove any existing runtime with the same rule name to avoid duplicate instances/subscriptions
                        for (int i = _runtimes.Count - 1; i >= 0; i--)
                        {
                            if (_runtimes[i].RuleName == name)
                            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                                //Debug.Log($"[MonitorChannelHub] Replace runtime: '{name}' Scope={DescribeScope(_scope)}");
#endif
                                try { /* Debug: Replacing existing runtime - suppressed */ } catch { }
                                _runtimes[i].Dispose();
                                _runtimes.RemoveAt(i);
                                changed = true;
                            }
                        }

                        var runtime = new MonitorChannelRuntime(rule, _scope, _runner);
                        runtime.SetVars(_vars);
                        _runtimes.Add(runtime);
                        changed = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        //Debug.Log($"[MonitorChannelHub] Added runtime: '{name}' Scope={DescribeScope(_scope)}");
#endif
                    }
                    _pendingAdds.Clear();
                }

                if (changed)
                    BumpTelemetry();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (changed)
                {
                    // Debug: ApplyPending change log suppressed
                    // Debug.LogError(
                    //    $"[MonitorChannelHub] ApplyPending. ScopeKind={_scope.Kind}, ScopeId={_scope.Identity?.Id ?? "(none)"}, Runtimes {beforeCount} -> {_runtimes.Count}");
                }
#endif
            }
            finally
            {
                _isApplyingPending = false;
            }
        }

        // ================================================================
        // Vars 邂｡逅・
        // ================================================================

        public void AttachToVars(IVarStore vars)
        {
            if (vars == null) return;
            if (ReferenceEquals(_vars, vars)) return;

            DetachFromVars(_vars);

            _vars = vars;
            _vars.OnVarChanged += OnVarChanged;

            for (int i = 0; i < _runtimes.Count; i++)
                _runtimes[i].SetVars(_vars);

            ApplyRunnerDefaultVars(_vars);

            BumpTelemetry();
        }

        public void DetachFromVars(IVarStore? vars)
        {
            if (_vars == null) return;
            if (vars != null && !ReferenceEquals(_vars, vars)) return;

            _vars.OnVarChanged -= OnVarChanged;

            // Keep the hub running with a fresh empty VarStore.
            _vars = new VarStore();
            _vars.OnVarChanged += OnVarChanged;

            for (int i = 0; i < _runtimes.Count; i++)
                _runtimes[i].SetVars(null);

            // Ensure runtimes have a non-null context vars after detaching.
            for (int i = 0; i < _runtimes.Count; i++)
                _runtimes[i].SetVars(_vars);

            ApplyRunnerDefaultVars(_vars);

            BumpTelemetry();
        }

        void ApplyRunnerDefaultVars(IVarStore vars)
        {
            if (vars == null)
                return;

            if (_runner is VNext.ICommandRunnerDefaultVarsProvider provider)
                provider.ApplyDefaultVars(vars, overwrite: false);
        }

        void OnVarChanged(int varId)
        {
            for (int i = 0; i < _runtimes.Count; i++)
                _runtimes[i].NotifyVarChanged(varId);
        }

        void TryEnsureScalarBridge()
        {
            if (_scalarBridgeSubscriptions.Count > 0)
                return;

            // Subscribe to scalar changes on this scope and its parents.
            // This approximates "GlobalSubscribeAll" by chaining LocalSubscribeAll.
            for (IScopeNode? node = _scope; node != null; node = node.Parent)
            {
                var resolver = node.Resolver;
                if (resolver == null)
                    continue;

                if (!resolver.TryResolve<IBaseScalarService>(out var svc) || svc == null)
                    continue;

                try
                {
                    var sub = svc.LocalSubscribeAll(OnAnyScalarChanged);
                    if (sub != null)
                        _scalarBridgeSubscriptions.Add(sub);
                }
                catch
                {
                    // Avoid throwing from constructor; bridge is best-effort.
                }
            }
        }

        void DisposeScalarBridge()
        {
            if (_scalarBridgeSubscriptions.Count == 0)
                return;

            for (int i = _scalarBridgeSubscriptions.Count - 1; i >= 0; i--)
            {
                try { _scalarBridgeSubscriptions[i]?.Dispose(); } catch { }
            }
            _scalarBridgeSubscriptions.Clear();
            _scalarBridgeCache.Clear();
        }

        void OnAnyScalarChanged(ScalarValueChangedArgs args)
        {
            if (_disposed)
                return;

            var vars = _vars;
            if (vars == null)
                return;

            var name = args.Key.Name;
            if (string.IsNullOrEmpty(name))
                return;

            if (!_scalarBridgeCache.TryGetValue(name, out var cache))
            {
                var fullVarId = 0;
                if (VarIdResolver.TryResolve(name, out var resolvedFullVarId) && resolvedFullVarId != 0)
                    fullVarId = resolvedFullVarId;

                var leafVarId = 0;
                for (int i = name.Length - 1; i >= 0; i--)
                {
                    var c = name[i];
                    if (c != '.' && c != '/' && c != '\\')
                        continue;

                    if (i + 1 < name.Length)
                    {
                        var leaf = name.Substring(i + 1);
                        if (!string.IsNullOrEmpty(leaf) &&
                            !string.Equals(leaf, name, StringComparison.Ordinal) &&
                            VarIdResolver.TryResolve(leaf, out var resolvedLeafVarId) &&
                            resolvedLeafVarId != 0)
                        {
                            leafVarId = resolvedLeafVarId;
                        }
                    }

                    break;
                }

                cache = new ScalarBridgeCacheEntry(fullVarId, leafVarId);
                _scalarBridgeCache[name] = cache;
            }

            var value = DynamicVariant.FromFloat(args.NewValue);
            if (cache.FullVarId != 0)
                vars.TrySetVariant(cache.FullVarId, value);
            if (cache.LeafVarId != 0 && cache.LeafVarId != cache.FullVarId)
                vars.TrySetVariant(cache.LeafVarId, value);
        }

        // ================================================================
        // 繧､繝吶Φ繝磯夂衍
        // ================================================================

        public void NotifyEvent(string eventName)
        {
            if (string.IsNullOrEmpty(eventName)) return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            //Debug.Log($"[MonitorChannelHub] NotifyEvent: '{eventName}' Scope={DescribeScope(_scope)} Runtimes={_runtimes.Count}");
#endif
            // 蜈ｨ Runtime 縺ｫ繧､繝吶Φ繝医ｒ騾夂衍
            for (int i = 0; i < _runtimes.Count; i++)
            {
                _runtimes[i].NotifyEvent(eventName);
            }

            BumpTelemetry();
        }

        // ================================================================
        // 螟画焚險ｭ螳夲ｼ域立莠呈鋤 string key 邨檎罰・・
        // ================================================================

        public void SetVariable<T>(string key, T value)
        {
            if (_vars == null)
            {
                Debug.LogWarning("[MonitorChannelHub] SetVariable: No vars attached.");
                return;
            }
            if (string.IsNullOrEmpty(key)) return;

            if (!VarIdResolver.TryResolve(key, out var varId) || varId == 0)
                return;

            // IL2CPP/JIT 縺ｯ縺薙％縺ｮ typeof(T) 豈碑ｼ・ｒ繧ｳ繝ｳ繝代う繝ｫ譎ょｮ壽焚縺ｫ螟画鋤縺吶ｋ
            // Unsafe.As 縺ｧ繝懊け繧ｷ繝ｳ繧ｰ繧貞ｮ悟・縺ｫ蝗樣∩
            if (typeof(T) == typeof(int)) { _vars.TrySetVariant(varId, DynamicVariant.FromInt(Unsafe.As<T, int>(ref value))); return; }
            if (typeof(T) == typeof(float)) { _vars.TrySetVariant(varId, DynamicVariant.FromFloat(Unsafe.As<T, float>(ref value))); return; }
            if (typeof(T) == typeof(bool)) { _vars.TrySetVariant(varId, DynamicVariant.FromBool(Unsafe.As<T, bool>(ref value))); return; }
            if (typeof(T) == typeof(string)) { _vars.TrySetVariant(varId, DynamicVariant.FromString(Unsafe.As<T, string>(ref value))); return; }
            if (typeof(T) == typeof(Vector2)) { _vars.TrySetVariant(varId, DynamicVariant.FromVector2(Unsafe.As<T, Vector2>(ref value))); return; }
            if (typeof(T) == typeof(Vector3)) { _vars.TrySetVariant(varId, DynamicVariant.FromVector3(Unsafe.As<T, Vector3>(ref value))); return; }
            if (typeof(T) == typeof(Vector4)) { _vars.TrySetVariant(varId, DynamicVariant.FromVector4(Unsafe.As<T, Vector4>(ref value))); return; }
            if (typeof(T) == typeof(Color)) { _vars.TrySetVariant(varId, DynamicVariant.FromColor(Unsafe.As<T, Color>(ref value))); return; }

            // 蜿ら・蝙九・繝輔か繝ｼ繝ｫ繝舌ャ繧ｯ・医・繧ｯ繧ｷ繝ｳ繧ｰ荳榊庄驕ｿ・・
            object boxed = value!;
            if (boxed is UnityEngine.Object uo) { _vars.TrySetVariant(varId, DynamicVariant.FromUnityObject(uo)); return; }

            _vars.TrySetManagedRef(varId, boxed);
        }

        // ================================================================
        // 螳溯｡御ｸｭ繧ｿ繧ｹ繧ｯ蜿門ｾ・
        // ================================================================

        public IReadOnlyList<RunningEntry> GetAllRunningEntries()
        {
            var result = new List<RunningEntry>();
            for (int i = 0; i < _runtimes.Count; i++)
            {
                var entries = _runtimes[i].RunningEntries;
                for (int j = 0; j < entries.Count; j++)
                {
                    result.Add(entries[j]);
                }
            }
            return result;
        }

        // ================================================================
        // 繝・Ξ繝｡繝医Μ
        // ================================================================

        void BumpTelemetry()
        {
            unchecked { _telemetryVersion++; }
        }

        public MonitorHubSnapshot GetSnapshot()
        {
            // 譌ｧ API 莠呈鋤: RuntimeSnapshot 縺九ｉ RuleSnapshot 縺ｨ RunningSnapshot 繧呈ｧ狗ｯ・
            var ruleSnapshots = new List<MonitorRuleSnapshot>(_runtimes.Count);
            var runningSnapshots = new List<MonitorRunningSnapshot>();

            for (int i = 0; i < _runtimes.Count; i++)
            {
                var runtime = _runtimes[i];
                var runtimeSnapshot = runtime.GetSnapshot();

                // RuleSnapshot 繧呈ｧ狗ｯ・
                ruleSnapshots.Add(new MonitorRuleSnapshot(
                    runtimeSnapshot.RuleName,
                    runtimeSnapshot.IsTrue,
                    runtimeSnapshot.Behavior,
                    runtimeSnapshot.CancelRunningOnConditionChange,
                    runtimeSnapshot.Condition,
                    runtimeSnapshot.DependentKeys));

                // RunningSnapshot 繧定ｿｽ蜉
                if (runtimeSnapshot.RunningEntries != null)
                {
                    for (int j = 0; j < runtimeSnapshot.RunningEntries.Count; j++)
                    {
                        runningSnapshots.Add(runtimeSnapshot.RunningEntries[j]);
                    }
                }
            }

            var variableSnapshots = new List<MonitorVariableSnapshot>();
            if (_vars != null)
            {
                foreach (var varId in _vars.EnumerateVarIds())
                {
                    if (varId == 0) continue;

                    var key = VarIdResolver.TryGetStableKey(varId, out var stableKey) ? stableKey : $"varId:{varId}";
                    var kind = _vars.GetVarKind(varId);
                    var typeName = kind.ToString();
                    var version = _vars.GetVarVersion(varId);

                    string valueStr = string.Empty;
                    if (kind == ValueKind.ManagedRef)
                    {
                        if (_vars.TryGetManagedRef(varId, out var managed) && managed != null)
                            valueStr = managed.ToString();
                    }
                    else if (_vars.TryGetVariant(varId, out var variant))
                    {
                        valueStr = variant.ToString();
                    }

                    variableSnapshots.Add(new MonitorVariableSnapshot(
                        key, typeName, valueStr, version));
                }
            }

            return new MonitorHubSnapshot(
                _telemetryVersion,
                _evaluationMode,
                _defaultExecutionBehavior,
                ruleSnapshots,
                runningSnapshots,
                variableSnapshots);
        }

        // ================================================================
        // Dispose
        // ================================================================

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            //Debug.Log($"[MonitorChannelHub] Dispose Scope={DescribeScope(_scope)}");
#endif
            DisposeScalarBridge();

            // CTS 繧ｭ繝｣繝ｳ繧ｻ繝ｫ
            try
            {
                _cts?.Cancel();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            try
            {
                _cts?.Dispose();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            _cts = null;

            // Vars 繧堤峩謗･繧ｯ繝ｪ繝ｼ繝ｳ繧｢繝・・・・etachFromVars 縺ｯ譁ｰ VarStore 繧剃ｽ懈・縺・
            // runtime 縺ｫ SetVars 繧貞他縺ｳ逶ｴ縺吶◆繧√．ispose 荳ｭ縺ｫ遐ｴ譽・ｸ医∩繧ｹ繧ｳ繝ｼ繝励〒
            // 繧ｵ繝ｼ繝薙せ隗｣豎ｺ繧定ｩｦ縺ｿ縺ｦ繝輔Μ繝ｼ繧ｺ縺吶ｋ蜿ｯ閭ｽ諤ｧ縺後≠繧具ｼ・
            if (_vars != null)
            {
                try { _vars.OnVarChanged -= OnVarChanged; } catch { }
                _vars = null;
            }

            // 蜈ｨ Runtime 繧・Dispose
            for (int i = _runtimes.Count - 1; i >= 0; i--)
            {
                try { _runtimes[i].Dispose(); } catch (Exception ex) { Debug.LogException(ex); }
            }
            _runtimes.Clear();

            _pendingAdds.Clear();
            _pendingRemoves.Clear();

            BumpTelemetry();
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static string DescribeScope(IScopeNode? scope)
        {
            if (scope == null)
                return "<null>";
            if (scope is UnityEngine.Object unityObj && !unityObj)
                return "<destroyed>";
            var id = scope.Identity?.Id;
            if (!string.IsNullOrEmpty(id))
                return $"{id} ({scope.Kind})";
            return scope.GetType().Name;
        }
#endif
    }

}
