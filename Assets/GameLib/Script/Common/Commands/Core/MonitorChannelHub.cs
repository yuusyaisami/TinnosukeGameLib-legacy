// MonitorChannelHub.cs
// 
// MonitorChannelHub v2.0
// ルール監視システムのハブ。
// Runtime のライフサイクル管理、VarStore 管理、Tick の委譲を担当。
// 実際の条件評価・コマンド実行は MonitorChannelRuntime が担当。
//
// 設計決定:
// - Hub: Tick、VarStore 管理、Runtime ライフサイクル管理のみ
// - Runtime: 条件評価、状態遷移、コマンド実行、イベント処理
// - fire-and-forget 禁止（必ず await, タスク追跡＋例外ログ）
// - IL2CPP / WebGL 対応（record/required 禁止）
// - GC最小化（リストキャッシュ、Clear再利用）

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
    /// 評価モード。
    /// </summary>
    public enum MonitorEvaluationMode
    {
        /// <summary>毎フレーム条件を評価。</summary>
        Polling,
        /// <summary>依存キー変更時のみ評価。</summary>
        EventDriven,
        /// <summary>外部から明示的に評価を呼ぶ。</summary>
        Manual,
    }

    /// <summary>
    /// ルールの評価方式。
    /// </summary>
    public enum MonitorRuleKind
    {
        /// <summary>
        /// 条件のみで評価。Enter/Exit/WhileTrue を条件遷移で発火。
        /// </summary>
        ConditionOnly,

        /// <summary>
        /// イベントのみで発火。条件は無視し、外部イベント（NotifyEvent）で即時実行。
        /// </summary>
        EventOnly,

        /// <summary>
        /// イベント＋条件。イベント受信時に条件を評価し、true なら実行。
        /// </summary>
        EventAndCondition,

        /// <summary>
        /// 特定の値が変化した時に実行。
        /// VarStore / Blackboard / Scalar のみ対応。
        /// </summary>
        ValueChanged,
    }

    public enum MonitorValueSourceKind
    {
        VarStore,
        Blackboard,
        Scalar,
    }

    public enum MonitorValueChangeMode
    {
        AnyChange,
        Increased,
        Decreased,
    }

    /// <summary>
    /// Command 実行のポリシー（WhileTrue の挙動など）。
    /// </summary>
    public enum ExecutionBehavior
    {
        SkipIfRunning,
        CancelAndRun,
        AllowConcurrent,
    }

    /// <summary>
    /// 設定済みの While コマンド群（true/false それぞれ）
    /// </summary>
    [Serializable]
    public struct MonitorRuleWhileCommandSet
    {
        [LabelText("Commands"), LabelWidth(120)]
        [VNext.CommandListFunctionName("MonitorRule.While")]
        public VNext.CommandListData Commands;

        [LabelText("Interval (sec)"), LabelWidth(120)]
        [MinValue(0f)]
        public float IntervalSeconds;
    }

    /// <summary>
    /// ルール定義（Enter/Exit/While）。
    /// IL2CPP 対応のため record struct 不使用。
    /// </summary>
    [Serializable]
    public struct MonitorRule
    {
        [PropertyOrder(0)]
        [LabelText("Rule Key")]
        public string RuleName;

        [PropertyOrder(10)]
        [LabelText("Rule Kind")]
        [EnumToggleButtons]
        public MonitorRuleKind RuleKind;

        [PropertyOrder(20)]
        [ShowIf("@RuleKind == MonitorRuleKind.EventOnly || RuleKind == MonitorRuleKind.EventAndCondition")]
        [EventKeyDropdown]
        [LabelText("Event Name")]
        public string EventName;

        [PropertyOrder(21)]
        [ShowIf("@RuleKind == MonitorRuleKind.EventOnly || RuleKind == MonitorRuleKind.EventAndCondition")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Event Target\", EventTarget)")]
        public VNext.ActorSource EventTarget;

        [PropertyOrder(30)]
        [ShowIf("@RuleKind == MonitorRuleKind.ConditionOnly || RuleKind == MonitorRuleKind.EventAndCondition")]
        [LabelText("Condition")]
        public DynamicValue<bool> Condition;

        [PropertyOrder(40)]
        [ShowIf("@RuleKind == MonitorRuleKind.ConditionOnly")]
        [LabelText("Execute Initial Condition")]
        [Tooltip("OnAcquire時に条件を評価し、条件に合ったコマンド（OnEnter/OnExit相当）を一度だけ実行します。")]
        public bool ExecuteInitialCondition;

        [PropertyOrder(40)]
        [ShowIf("@RuleKind == MonitorRuleKind.ValueChanged")]
        [LabelText("Value Source")]
        public MonitorValueSourceKind ValueSource;

        [PropertyOrder(41)]
        [ShowIf("@RuleKind == MonitorRuleKind.ValueChanged")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Value Target\", ValueTarget)")]
        public VNext.ActorSource ValueTarget;

        [PropertyOrder(42)]
        [ShowIf("@RuleKind == MonitorRuleKind.ValueChanged")]
        [LabelText("Change Mode")]
        public MonitorValueChangeMode ValueChangeMode;

        [PropertyOrder(43)]
        [ShowIf("@RuleKind == MonitorRuleKind.ValueChanged && ValueSource == MonitorValueSourceKind.VarStore")]
        [LabelText("VarStore Var Id"), VarIdDropdown]
        public int VarStoreVarId;

        [PropertyOrder(44)]
        [ShowIf("@RuleKind == MonitorRuleKind.ValueChanged && ValueSource == MonitorValueSourceKind.Blackboard")]
        [LabelText("Blackboard Var Id"), VarIdDropdown]
        public int BlackboardVarId;

        [PropertyOrder(45)]
        [ShowIf("@RuleKind == MonitorRuleKind.ValueChanged && ValueSource == MonitorValueSourceKind.Blackboard")]
        [LabelText("Blackboard Read Scope")]
        public BlackboardReadScope BlackboardReadScope;

        [PropertyOrder(46)]
        [ShowIf("@RuleKind == MonitorRuleKind.ValueChanged && ValueSource == MonitorValueSourceKind.Scalar")]
        [LabelText("Scalar Key")]
        public ScalarKey ScalarKey;

        [PropertyOrder(47)]
        [ShowIf("@RuleKind == MonitorRuleKind.ValueChanged")]
        [LabelText("Change Epsilon")]
        [MinValue(0f)]
        public float ChangeEpsilon;

        [PropertyOrder(48)]
        [ShowIf("@RuleKind == MonitorRuleKind.ValueChanged")]
        [LabelText("Execute Initial Enter")]
        [Tooltip("OnAcquire時に OnEnter Commands を一度だけ実行します。Value監視の初期化順序をずらしたい場合は Delay を併用します。")]
        public bool ExecuteInitialValueChangedEnter;

        [PropertyOrder(49)]
        [ShowIf("@RuleKind == MonitorRuleKind.ValueChanged && ExecuteInitialValueChangedEnter")]
        [LabelText("Initial Enter Delay Seconds")]
        [MinValue(0f)]
        public float InitialValueChangedEnterDelaySeconds;

        [PropertyOrder(100)]
        [LabelText("On Enter Commands")]
        [VNext.CommandListFunctionName("MonitorRule.OnEnter")]
        public VNext.CommandListData OnEnterCommands;

        [PropertyOrder(110)]
        [ShowIf("@RuleKind == MonitorRuleKind.ConditionOnly || RuleKind == MonitorRuleKind.EventAndCondition")]
        [LabelText("On Exit Commands")]
        [VNext.CommandListFunctionName("MonitorRule.OnExit")]
        public VNext.CommandListData OnExitCommands;

        [PropertyOrder(120)]
        [ShowIf("@RuleKind == MonitorRuleKind.ConditionOnly || RuleKind == MonitorRuleKind.EventAndCondition")]
        [LabelText("While True Commands")]
        [InlineProperty]
        public MonitorRuleWhileCommandSet WhileTrueCommands;

        [PropertyOrder(130)]
        [ShowIf("@RuleKind == MonitorRuleKind.ConditionOnly || RuleKind == MonitorRuleKind.EventAndCondition")]
        [LabelText("While False Commands")]
        [InlineProperty]
        public MonitorRuleWhileCommandSet WhileFalseCommands;

        [PropertyOrder(135)]
        [ShowIf("@RuleKind == MonitorRuleKind.ConditionOnly || RuleKind == MonitorRuleKind.EventAndCondition")]
        [LabelText("Cancel Running On Change")]
        public bool CancelRunningOnConditionChange;

        [HideInInspector]
        public bool CancelRunningOnConditionChangeInitialized;

        [PropertyOrder(140)]
        [LabelText("Execution Behavior")]
        public ExecutionBehavior Behavior;
    }

    /// <summary>
    /// 実行中タスク情報（参照型）。
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

    /// <summary>Rule snapshot for telemetry (後方互換用).</summary>
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
    /// MonitorChannelHub インターフェース。
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
        /// 全 Runtime の実行中タスクを取得
        /// </summary>
        IReadOnlyList<RunningEntry> GetAllRunningEntries();
    }

    /// <summary>
    /// MonitorChannelHub - Runtime のライフサイクル管理と Tick 委譲のみを担当。
    /// 実際の監視・コマンド実行は MonitorChannelRuntime が行う。
    /// </summary>
    public sealed class MonitorChannelHub : IMonitorChannelHub, ITickable, IMonitorChannelHubTelemetry
    {
        // ================================================================
        // 定数
        // ================================================================

        const int InitialRuntimeCapacity = 8;

        // ================================================================
        // DI 依存
        // ================================================================

        readonly IScopeNode _scope;
        readonly VNext.ICommandRunner _runner;

        // ================================================================
        // フィールド
        // ================================================================

        /// <summary>Runtime リスト</summary>
        readonly List<MonitorChannelRuntime> _runtimes = new(InitialRuntimeCapacity);

        /// <summary>追加待ちルール</summary>
        readonly List<MonitorRule> _pendingAdds = new(4);

        /// <summary>削除待ちルール名</summary>
        readonly List<string> _pendingRemoves = new(4);

        /// <summary>Vars（Hub が管理）</summary>
        IVarStore? _vars;

        // Scalar -> VarStore bridge subscriptions (scope chain)
        readonly List<IDisposable> _scalarBridgeSubscriptions = new(2);

        /// <summary>内部 CTS</summary>
        CancellationTokenSource? _cts;

        bool _debugTickLogged;
        bool _isTicking;
        bool _isApplyingPending;

        MonitorEvaluationMode _evaluationMode = MonitorEvaluationMode.Polling;
        ExecutionBehavior _defaultExecutionBehavior = ExecutionBehavior.SkipIfRunning;
        int _telemetryVersion;
        bool _disposed;

        // ================================================================
        // プロパティ
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
        // コンストラクタ
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
        // ITickable（VContainer が毎フレーム呼び出す）
        // ================================================================

        void ITickable.Tick()
        {
            if (_disposed) return;
            if (_cts == null) return;

            Tick(_cts.Token);
        }

        void Tick(CancellationToken ct)
        {
            if (ct.IsCancellationRequested) return;

            // ランタイムも保留もない場合は早期リターン
            if (_runtimes.Count == 0 && _pendingAdds.Count == 0 && _pendingRemoves.Count == 0)
                return;

            _isTicking = true;
            try
            {
                // Pending 処理
                ApplyPending();

                // 全 Runtime の Tick
                for (int i = 0; i < _runtimes.Count; i++)
                {
                    _runtimes[i].Tick(_evaluationMode, ct);
                }
            }
            finally
            {
                _isTicking = false;
            }
        }

        // ================================================================
        // ルール管理
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
            // (Acquire handlers can run after ITickable order depending on dispatcher timing).
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

            // pendingAdds からも削除
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
                // 削除処理
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

                // 追加処理
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
        // Vars 管理
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

            // Full key
            if (VarIdResolver.TryResolve(name, out var fullVarId) && fullVarId != 0)
            {
                vars.TrySetVariant(fullVarId, DynamicVariant.FromFloat(args.NewValue));
            }

            // Leaf key (best-effort convenience for expressions using short identifiers)
            var leaf = name;
            for (int i = name.Length - 1; i >= 0; i--)
            {
                var c = name[i];
                if (c == '.' || c == '/' || c == '\\')
                {
                    if (i + 1 < name.Length)
                        leaf = name.Substring(i + 1);
                    break;
                }
            }

            if (!string.IsNullOrEmpty(leaf) && !string.Equals(leaf, name, StringComparison.Ordinal))
            {
                if (VarIdResolver.TryResolve(leaf, out var leafVarId) && leafVarId != 0)
                {
                    vars.TrySetVariant(leafVarId, DynamicVariant.FromFloat(args.NewValue));
                }
            }
        }

        // ================================================================
        // イベント通知
        // ================================================================

        public void NotifyEvent(string eventName)
        {
            if (string.IsNullOrEmpty(eventName)) return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            //Debug.Log($"[MonitorChannelHub] NotifyEvent: '{eventName}' Scope={DescribeScope(_scope)} Runtimes={_runtimes.Count}");
#endif
            // 全 Runtime にイベントを通知
            for (int i = 0; i < _runtimes.Count; i++)
            {
                _runtimes[i].NotifyEvent(eventName);
            }

            BumpTelemetry();
        }

        // ================================================================
        // 変数設定（旧互換 string key 経由）
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

            // IL2CPP/JIT はここの typeof(T) 比較をコンパイル時定数に変換する
            // Unsafe.As でボクシングを完全に回避
            if (typeof(T) == typeof(int)) { _vars.TrySetVariant(varId, DynamicVariant.FromInt(Unsafe.As<T, int>(ref value))); return; }
            if (typeof(T) == typeof(float)) { _vars.TrySetVariant(varId, DynamicVariant.FromFloat(Unsafe.As<T, float>(ref value))); return; }
            if (typeof(T) == typeof(bool)) { _vars.TrySetVariant(varId, DynamicVariant.FromBool(Unsafe.As<T, bool>(ref value))); return; }
            if (typeof(T) == typeof(string)) { _vars.TrySetVariant(varId, DynamicVariant.FromString(Unsafe.As<T, string>(ref value))); return; }
            if (typeof(T) == typeof(Vector2)) { _vars.TrySetVariant(varId, DynamicVariant.FromVector2(Unsafe.As<T, Vector2>(ref value))); return; }
            if (typeof(T) == typeof(Vector3)) { _vars.TrySetVariant(varId, DynamicVariant.FromVector3(Unsafe.As<T, Vector3>(ref value))); return; }
            if (typeof(T) == typeof(Vector4)) { _vars.TrySetVariant(varId, DynamicVariant.FromVector4(Unsafe.As<T, Vector4>(ref value))); return; }
            if (typeof(T) == typeof(Color)) { _vars.TrySetVariant(varId, DynamicVariant.FromColor(Unsafe.As<T, Color>(ref value))); return; }

            // 参照型のフォールバック（ボクシング不可避）
            object boxed = value!;
            if (boxed is UnityEngine.Object uo) { _vars.TrySetVariant(varId, DynamicVariant.FromUnityObject(uo)); return; }

            _vars.TrySetManagedRef(varId, boxed);
        }

        // ================================================================
        // 実行中タスク取得
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
        // テレメトリ
        // ================================================================

        void BumpTelemetry()
        {
            unchecked { _telemetryVersion++; }
        }

        public MonitorHubSnapshot GetSnapshot()
        {
            // 旧 API 互換: RuntimeSnapshot から RuleSnapshot と RunningSnapshot を構築
            var ruleSnapshots = new List<MonitorRuleSnapshot>(_runtimes.Count);
            var runningSnapshots = new List<MonitorRunningSnapshot>();

            for (int i = 0; i < _runtimes.Count; i++)
            {
                var runtime = _runtimes[i];
                var runtimeSnapshot = runtime.GetSnapshot();

                // RuleSnapshot を構築
                ruleSnapshots.Add(new MonitorRuleSnapshot(
                    runtimeSnapshot.RuleName,
                    runtimeSnapshot.IsTrue,
                    runtimeSnapshot.Behavior,
                    runtimeSnapshot.CancelRunningOnConditionChange,
                    runtimeSnapshot.Condition,
                    runtimeSnapshot.DependentKeys));

                // RunningSnapshot を追加
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

            // CTS キャンセル
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

            // Vars を直接クリーンアップ（DetachFromVars は新 VarStore を作成し
            // runtime に SetVars を呼び直すため、Dispose 中に破棄済みスコープで
            // サービス解決を試みてフリーズする可能性がある）
            if (_vars != null)
            {
                try { _vars.OnVarChanged -= OnVarChanged; } catch { }
                _vars = null;
            }

            // 全 Runtime を Dispose
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
