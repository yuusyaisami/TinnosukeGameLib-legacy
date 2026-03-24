// MonitorChannelRuntime.cs
#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Runtime.CompilerServices;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Game.Common;
using Game.Scalar;
using VContainer;
using VContainer.Unity;
using VNext = Game.Commands.VNext;

namespace Game.Commands
{
    /// <summary>
    /// 単一ルールのランタイム状態と実行を管理。
    /// MonitorChannelHub が VarStore と Tick を提供し、
    /// Runtime が条件評価・状態遷移・コマンド実行を担当する。
    /// </summary>
    public sealed class MonitorChannelRuntime : IDisposable
    {
        // ================================================================
        // 定数
        // ================================================================

        const int InitialRunningCapacity = 2;

        sealed class ValueChangedWatchState
        {
            public readonly MonitorValueChangedTarget Target;
            public bool HasLastValue;
            public DynamicVariant LastValue;
            public IDisposable? ScalarSubscription;
            public Action<int>? VarChangedHandler;
            public IVarStore? ObservedVarStore;
            public Action<int>? BlackboardVarChangedHandler;
            public IVarStore? ObservedBlackboardVars;
            public bool PendingRefresh;
            public bool PendingFire;

            public ValueChangedWatchState(MonitorValueChangedTarget target)
            {
                Target = target ?? throw new ArgumentNullException(nameof(target));
                HasLastValue = false;
                LastValue = DynamicVariant.Null;
            }
        }

        // ================================================================
        // フィールド
        // ================================================================

        readonly MonitorRule _rule;
        readonly VNext.ICommandRunner _runner;
        readonly IScopeNode _scope;
        readonly List<RunningEntry> _runningEntries = new(InitialRunningCapacity);
        readonly VNext.CommandRunOptions _runOptions;

        /// <summary>Hub が管理する Vars への参照</summary>
        IVarStore? _vars;

        /// <summary>評価用コンテキストのキャッシュ（毎回 new しない）</summary>
        VNext.CommandContext? _ctx;

        /// <summary>前回の条件評価結果</summary>
        bool _previousState;

        /// <summary>イベントが今フレーム発火したか</summary>
        bool _eventFiredThisFrame;

        /// <summary>依存キー（stableKey）のキャッシュ（デバッグ/テレメトリ用）</summary>
        IReadOnlyList<string>? _cachedDependentKeys;

        /// <summary>依存 varId のキャッシュ（EventDriven 用）</summary>
        int[]? _cachedDependentVarIds;

        /// <summary>依存キーが変更されたか（EventDriven 用）</summary>
        bool _dependentKeyChanged;

        // ConditionOnly: in EventDriven mode, evaluate at least once to initialize state.
        bool _conditionEvaluatedOnce;

        // ValueChanged rule state
        readonly List<ValueChangedWatchState> _valueChangedStates = new(4);
        bool _valueChangedTriggeredThisFrame;
        int _valueChangedVersion;
        CancellationTokenSource? _initialValueChangedEnterCts;
        bool _initialValueChangedEnterScheduled;
        bool _initialValueChangedEnterExecuted;

        // Instance id for diagnostics (stable during object lifetime)
        public int InstanceId => RuntimeHelpers.GetHashCode(this);


        float _lastWhileTrueExecution = float.NegativeInfinity;
        float _lastWhileFalseExecution = float.NegativeInfinity;

        bool _disposed;
        int _telemetryVersion;
        IDisposable? _eventSubscription;
        IScopeNode? _subscribedScope;

        // ================================================================
        // プロパティ
        // ================================================================

        /// <summary>ルール定義</summary>
        public MonitorRule Rule => _rule;

        /// <summary>ルール名</summary>
        public string RuleName => _rule.RuleName;

        /// <summary>前回の条件評価結果</summary>
        public bool PreviousState => _previousState;

        /// <summary>実行中タスク一覧</summary>
        public IReadOnlyList<RunningEntry> RunningEntries => _runningEntries;

        /// <summary>依存キーのキャッシュ</summary>
        public IReadOnlyList<string>? DependentKeys => _cachedDependentKeys;

        /// <summary>テレメトリバージョン</summary>
        public int TelemetryVersion => _telemetryVersion;

        // ================================================================
        // コンストラクタ
        // ================================================================

        public MonitorChannelRuntime(MonitorRule rule, IScopeNode scope, VNext.ICommandRunner runner)
        {
            _rule = rule;
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
            _runOptions = new VNext.CommandRunOptions(
                VNext.CommandFailurePolicy.Skip,
                allowActorFallback: false,
                allowRuntimeKeyFallback: false,
                VNext.CommandTracePolicy.OnFailure,
                maxTraceDepth: 32,
                maxTraceFrames: 256,
                suppressCancelLog: false);

            _previousState = false;
            _eventFiredThisFrame = false;
            _dependentKeyChanged = false;

            _conditionEvaluatedOnce = false;

            _valueChangedTriggeredThisFrame = false;
            _initialValueChangedEnterScheduled = false;
            _initialValueChangedEnterExecuted = false;

            // 依存キーをキャッシュ
            _cachedDependentKeys = rule.Condition.GetDependentKeys();
            _cachedDependentVarIds = ResolveDependentVarIds(_cachedDependentKeys);

            BuildValueChangedStates();
        }

        // ================================================================
        // Hub からの設定
        // ================================================================

        /// <summary>
        /// Hub が管理する Vars を設定
        /// </summary>
        public void SetVars(IVarStore? vars)
        {
            _vars = vars;
            _ctx = new VNext.CommandContext(_scope, _vars ?? NullVarStore.Instance, _runner, _scope, _runOptions);
            // (re)subscribe to event if needed
            if (_vars != null)
                TryEnsureEventSubscription();
            else
                UnsubscribeEvent();

            // (re)subscribe to value change sources if needed
            if (_vars != null)
                TryEnsureValueChangedSubscription();
            else
                UnsubscribeValueChanged();

            // Initialize last watched value (do not trigger)
            if (_rule.RuleKind == MonitorRuleKind.ValueChanged)
            {
                TryInitializeLastWatchedValue();
                TryStartInitialValueChangedEnter();
            }
        }

        // ================================================================
        // キー変更通知
        // ================================================================

        /// <summary>
        /// 依存キーが変更されたことを通知
        /// </summary>
        public void NotifyVarChanged(int varId)
        {
            if (varId == 0) return;

            NotifyValueChangedVarStore(varId);

            if (_cachedDependentVarIds == null || _cachedDependentVarIds.Length == 0) return;

            for (int i = 0; i < _cachedDependentVarIds.Length; i++)
            {
                if (_cachedDependentVarIds[i] == varId)
                {
                    _dependentKeyChanged = true;
                    return;
                }
            }
        }

        public void NotifyKeyChanged(string key)
        {
            if (!VarIdResolver.TryResolve(key, out var varId) || varId == 0)
                return;
            NotifyVarChanged(varId);
        }

        void MarkValueChangedTriggered()
        {
            _valueChangedTriggeredThisFrame = true;
            unchecked { _valueChangedVersion++; }
        }
        // Debug: MarkValueChangedTriggered - suppressed to reduce log spam
        // Debug.Log($"[MonitorChannelRuntime] MarkValueChangedTriggered: id={InstanceId}, vver:{before} -> {_valueChangedVersion}");
        public void NotifyEvent(string eventName)
        {
            if (_rule.RuleKind == MonitorRuleKind.ConditionOnly) return;
            if (string.IsNullOrEmpty(_rule.EventName)) return;
            // If there's an active event subscription to IEventService, Skip hub-level notifications
            if (_eventSubscription != null) return;

            if (string.Equals(_rule.EventName, eventName, StringComparison.Ordinal))
            {
                _eventFiredThisFrame = true;
            }
        }

        void TryEnsureEventSubscription()
        {
            // Already subscribed
            if (_eventSubscription != null) return;
            if (string.IsNullOrEmpty(_rule.EventName)) return;
            if (_rule.RuleKind != MonitorRuleKind.EventOnly && _rule.RuleKind != MonitorRuleKind.EventAndCondition) return;

            // resolve target scope
            var targetScope = ResolveActorSourceScope(_rule.EventTarget);
            if (targetScope == null || targetScope.Resolver == null) return;

            // Resolve an IEntityEventService or IEventService from the target scope
            IEntityEventService entityEv;
            if (targetScope.Resolver.TryResolve(out entityEv))
            {
                _subscribedScope = targetScope;
                _eventSubscription = entityEv.Subscribe(_rule.EventName, (payload, ct) => OnEventHandlerAsync(targetScope, payload, ct));
            }
            else
            {
                IEventService ev;
                if (targetScope.Resolver.TryResolve(out ev))
                {
                    _subscribedScope = targetScope;
                    _eventSubscription = ev.Subscribe(_rule.EventName, (payload, ct) => OnEventHandlerAsync(targetScope, payload, ct));
                }
            }
        }

        void UnsubscribeEvent()
        {
            try { _eventSubscription?.Dispose(); } catch { }
            _eventSubscription = null;
            _subscribedScope = null;
        }

        UniTask OnEventHandlerAsync(IScopeNode eventScope, IVarStore payload, CancellationToken ct)
        {
            if (_disposed) return UniTask.CompletedTask;
            try
            {
                // create merged vars: _vars + payload (payload override)
                var merged = new VarStore();
                _vars?.MergeInto(merged, overwrite: true);
                payload?.MergeInto(merged, overwrite: true);

                // NOTE:
                // We may subscribe to events from another scope, but commands should run as "self" (the scope that owns this runtime).
                // This is used to "watch other's events" but execute locally.
                var ctx = new VNext.CommandContext(_scope, merged, _runner, _scope, _runOptions);

                if (_rule.RuleKind == MonitorRuleKind.EventOnly)
                {
                    TryExecuteCommands("Enter", _rule.OnEnterCommands, ctx, ct);
                }
                else if (_rule.RuleKind == MonitorRuleKind.EventAndCondition)
                {
                    if (!_rule.Condition.HasSource)
                    {
                        TryExecuteCommands("Enter", _rule.OnEnterCommands, ctx, ct);
                    }
                    else
                    {
                        bool ok = false;
                        try
                        {
                            ok = _rule.Condition.EvaluateBool(ctx);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogException(ex);
                            ok = false;
                        }
                        if (ok)
                            TryExecuteCommands("Enter", _rule.OnEnterCommands, ctx, ct);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            return UniTask.CompletedTask;
        }

        IScopeNode? ResolveActorSourceScope(in VNext.ActorSource source)
        {
            switch (source.Kind)
            {
                case VNext.ActorSourceKind.Current:
                    return _scope;
                case VNext.ActorSourceKind.GameLogicRoot:
                    return ScopeNodeHierarchy.FindNearestGameLogicRoot(_scope, includeSelf: true);
                case VNext.ActorSourceKind.Player:
                    return VNext.ActorSourceFastResolver.Resolve(_scope, source);
                case VNext.ActorSourceKind.Global:
                    return VNext.ActorSourceFastResolver.Resolve(_scope, source);
                case VNext.ActorSourceKind.ByIdentity:
                    {
                        if (!TryResolveScopeRegistry(_scope, out var registry) || registry == null)
                            return null;
                        return registry.Resolve(source.Identity, _scope);
                    }
                case VNext.ActorSourceKind.FromUnityObject:
                    return TryResolveScopeFromUnityObject(source.UnityObject);
                default:
                    return null;
            }
        }

        IScopeNode? ResolveValueTargetScope(in MonitorValueChangedTarget target)
        {
            return ResolveActorSourceScope(target.ValueTarget);
        }

        void BuildValueChangedStates()
        {
            _valueChangedStates.Clear();

            if (_rule.RuleKind != MonitorRuleKind.ValueChanged)
                return;

            var targetCount = _rule.GetValueChangedTargetCount();
            for (int i = 0; i < targetCount; i++)
            {
                var target = _rule.GetValueChangedTarget(i);
                if (target == null)
                    continue;

                _valueChangedStates.Add(new ValueChangedWatchState(target));
            }
        }

        void NotifyValueChangedVarStore(int varId)
        {
            if (_rule.RuleKind != MonitorRuleKind.ValueChanged)
                return;

            for (int i = 0; i < _valueChangedStates.Count; i++)
            {
                var state = _valueChangedStates[i];
                var target = state.Target;
                if (target.ValueSource != MonitorValueSourceKind.VarStore)
                    continue;
                if (target.VarStoreVarId == 0 || target.VarStoreVarId != varId)
                    continue;

                MarkValueChangedRefresh(state);
            }
        }

        static bool TryResolveScopeRegistry(IScopeNode? origin, out IBaseLifetimeScopeRegistry? registry)
        {
            var current = origin;
            while (current != null)
            {
                var resolver = current.Resolver;
                if (resolver != null && resolver.TryResolve<IBaseLifetimeScopeRegistry>(out var resolved) && resolved != null)
                {
                    registry = resolved;
                    return true;
                }
                current = current.Parent;
            }
            registry = null;
            return false;
        }

        static IScopeNode? TryResolveScopeFromUnityObject(UnityEngine.Object? unityObject)
        {
            if (unityObject == null)
                return null;

            if (unityObject is IScopeNode node)
                return node;

            if (unityObject is Component comp)
                return FindScopeNode(comp.gameObject);

            if (unityObject is GameObject go)
                return FindScopeNode(go);

            return null;
        }

        static IScopeNode? FindScopeNode(GameObject go)
        {
            if (go == null)
                return null;

            // NOTE:
            // Monitor 側も FromUnityObject 相当の解決を行うため、優先順は VNext と揃える。
            // 近傍 Transform から RuntimeScope を先に取り、Scope取り違えを防ぐ。
            for (var t = go.transform; t != null; t = t.parent)
            {
                var runtimeScope = t.GetComponent<RuntimeLifetimeScope>();
                if (runtimeScope != null)
                    return runtimeScope;

                var baseScope = t.GetComponent<BaseLifetimeScope>();
                if (baseScope != null)
                    return baseScope;

                var sameLevel = t.GetComponents<Component>();
                for (var i = 0; i < sameLevel.Length; i++)
                {
                    if (sameLevel[i] is IScopeNode node)
                        return node;
                }
            }

            return null;
        }

        // ================================================================
        // Tick 処理
        // ================================================================

        /// <summary>
        /// フレームごとの評価処理
        /// </summary>
        /// <param name="mode">評価モード</param>
        /// <param name="ct">キャンセルトークン</param>
        public void Tick(MonitorEvaluationMode mode, CancellationToken ct)
        {
            if (_disposed) return;
            if (ct.IsCancellationRequested) return;
            int valueChangedVersionAtStart = _valueChangedVersion;

            // イベント駆動ルールの処理
            if (_eventFiredThisFrame)
            {
                ProcessEventDrivenRule(ct);
            }

            // 条件ベースルールの処理（ConditionOnly のみ）
            if (_rule.RuleKind == MonitorRuleKind.ConditionOnly)
            {
                bool hasDependencies = _cachedDependentVarIds != null && _cachedDependentVarIds.Length > 0;
                bool shouldEvaluate = mode switch
                {
                    MonitorEvaluationMode.Polling => true,
                    MonitorEvaluationMode.EventDriven => !hasDependencies || _dependentKeyChanged || !_conditionEvaluatedOnce,
                    MonitorEvaluationMode.Manual => false, // Manual は EvaluateManual で処理
                    _ => false,
                };

                if (shouldEvaluate)
                {
                    EvaluateCondition(ct);
                }
            }

            // 値変更監視ルールの処理（ValueChanged のみ）
            if (_rule.RuleKind == MonitorRuleKind.ValueChanged)
            {
                bool shouldPoll = ShouldPollValueChanged(mode);
                bool triggered = _valueChangedTriggeredThisFrame;
                if (triggered || shouldPoll)
                {
                    ProcessValueChangedRule(ct);
                }
            }

            // 完了タスクのクリーンアップ
            CleanupCompletedTasks();

            // フラグリセット
            _eventFiredThisFrame = false;
            _dependentKeyChanged = false;
            if (_rule.RuleKind != MonitorRuleKind.ValueChanged || _valueChangedVersion == valueChangedVersionAtStart)
            {
                _valueChangedTriggeredThisFrame = false;
            }
        }

        bool ShouldPollValueChanged(MonitorEvaluationMode mode)
        {
            if (_rule.RuleKind != MonitorRuleKind.ValueChanged || _valueChangedStates.Count == 0)
                return false;

            if (mode == MonitorEvaluationMode.Polling)
                return HasConfiguredValueChangedTarget();

            for (int i = 0; i < _valueChangedStates.Count; i++)
            {
                var state = _valueChangedStates[i];
                if (!IsValueChangedTargetConfigured(state.Target))
                    continue;

                switch (state.Target.ValueSource)
                {
                    case MonitorValueSourceKind.VarStore:
                        if (state.ObservedVarStore == null)
                            return true;
                        break;
                    case MonitorValueSourceKind.Blackboard:
                        if (state.Target.BlackboardReadScope == BlackboardReadScope.Global || state.ObservedBlackboardVars == null)
                            return true;
                        break;
                    case MonitorValueSourceKind.Scalar:
                        if (state.ScalarSubscription == null)
                            return true;
                        break;
                }
            }

            return false;
        }

        void ProcessValueChangedRule(CancellationToken ct)
        {
            bool anyTriggered = false;

            for (int i = 0; i < _valueChangedStates.Count; i++)
            {
                if (TryProcessValueChangedState(_valueChangedStates[i]))
                    anyTriggered = true;
            }

            if (!anyTriggered)
                return;

            BumpTelemetry();
            TryExecuteCommands("Change", _rule.OnEnterCommands, ct);
        }

        bool TryProcessValueChangedState(ValueChangedWatchState state)
        {
            if (!IsValueChangedTargetConfigured(state.Target))
            {
                state.PendingFire = false;
                state.PendingRefresh = false;
                return false;
            }

            if (state.PendingFire)
            {
                state.PendingFire = false;
                state.PendingRefresh = false;
                return true;
            }

            if (!TryGetWatchedValue(state, out var current))
            {
                state.PendingRefresh = false;
                return false;
            }

            if (!state.HasLastValue)
            {
                state.LastValue = current;
                state.HasLastValue = true;
                state.PendingRefresh = false;
                return false;
            }

            var previous = state.LastValue;
            state.PendingRefresh = false;

            if (!TryDetectValueDelta(previous, current, state.Target.ChangeEpsilon, out var deltaSign, out var changed))
            {
                if (previous == current)
                    return false;

                state.LastValue = current;
                state.HasLastValue = true;
                return state.Target.ValueChangeMode == MonitorValueChangeMode.AnyChange;
            }

            if (!changed)
            {
                if (state.Target.ValueChangeMode == MonitorValueChangeMode.AnyChange && previous != current)
                {
                    state.LastValue = current;
                    state.HasLastValue = true;
                    return true;
                }
                return false;
            }

            state.LastValue = current;
            state.HasLastValue = true;
            return ShouldFireForValueChanged(state.Target.ValueChangeMode, deltaSign);
        }

        void TryStartInitialValueChangedEnter()
        {
            if (_disposed)
                return;

            if (_rule.RuleKind != MonitorRuleKind.ValueChanged)
                return;

            if (!_rule.ExecuteInitialValueChangedEnter)
                return;

            if (!HasConfiguredValueChangedTarget())
                return;

            if (_initialValueChangedEnterScheduled || _initialValueChangedEnterExecuted)
                return;

            var delaySeconds = Mathf.Max(0f, _rule.InitialValueChangedEnterDelaySeconds);
            _initialValueChangedEnterScheduled = true;
            _initialValueChangedEnterCts = new CancellationTokenSource();
            RunInitialValueChangedEnterAsync(delaySeconds, _initialValueChangedEnterCts.Token).Forget(ex =>
            {
                Debug.LogException(ex);
            });
        }

        async UniTask RunInitialValueChangedEnterAsync(float delaySeconds, CancellationToken ct)
        {
            try
            {
                if (delaySeconds > 0f)
                    await UniTask.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken: ct);

                if (_disposed || ct.IsCancellationRequested)
                    return;

                _initialValueChangedEnterExecuted = true;
                TryExecuteCommands("Change", _rule.OnEnterCommands, ct);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _initialValueChangedEnterScheduled = false;
                if (_initialValueChangedEnterCts != null)
                {
                    _initialValueChangedEnterCts.Dispose();
                    _initialValueChangedEnterCts = null;
                }
            }
        }

        bool TryGetWatchedValue(ValueChangedWatchState state, out DynamicVariant value)
        {
            value = DynamicVariant.Null;

            if (_rule.RuleKind != MonitorRuleKind.ValueChanged || !IsValueChangedTargetConfigured(state.Target))
                return false;

            var target = state.Target;
            switch (target.ValueSource)
            {
                case MonitorValueSourceKind.VarStore:
                    {
                        if (!TryResolveValueTargetVarStore(state, out var vars) || vars == null)
                            return false;
                        var varId = target.VarStoreVarId;
                        if (varId == 0)
                            return false;
                        return vars.TryGetVariant(varId, out value) || (value = DynamicVariant.Null) == DynamicVariant.Null;
                    }
                case MonitorValueSourceKind.Blackboard:
                    {
                        var targetScope = ResolveValueTargetScope(target);
                        if (targetScope?.Resolver == null) return false;
                        if (!targetScope.Resolver.TryResolve<IBlackboardService>(out var bb) || bb == null) return false;
                        var varId = target.BlackboardVarId;
                        if (varId == 0) return false;

                        if (target.BlackboardReadScope == BlackboardReadScope.Global)
                            return bb.TryGlobalGetVariant(varId, out value) || (value = DynamicVariant.Null) == DynamicVariant.Null;

                        return bb.TryLocalGetVariant(varId, out value) || (value = DynamicVariant.Null) == DynamicVariant.Null;
                    }
                case MonitorValueSourceKind.Scalar:
                    {
                        var targetScope = ResolveValueTargetScope(target);
                        if (targetScope?.Resolver == null) return false;
                        if (!targetScope.Resolver.TryResolve<IBaseScalarService>(out var svc) || svc == null) return false;
                        var key = target.ScalarKey;
                        if (key.Id == 0 && string.IsNullOrEmpty(key.Name)) return false;

                        float f;
                        if (svc.GlobalTryGet(key, out f))
                        {
                            value = DynamicVariant.FromFloat(f);
                            return true;
                        }
                        if (svc.LocalTryGet(key, out f))
                        {
                            value = DynamicVariant.FromFloat(f);
                            return true;
                        }
#if UNITY_EDITOR
                        // Debug: Scalar key not found - suppressed
                        // Debug.LogWarning($"[MonitorChannelRuntime] TryGetWatchedValue: Scalar key '{key.FormatLabel()}' not found for rule '{_rule.RuleName}'");
#endif

                        value = DynamicVariant.Null;
                        return true;
                    }
                default:
                    return false;
            }
        }

        static bool TryDetectValueDelta(in DynamicVariant prev, in DynamicVariant current, float epsilon, out int deltaSign, out bool changed)
        {
            deltaSign = 0;
            changed = false;

            // numeric (int/float/bool)
            if (IsNumeric(prev.Kind) && IsNumeric(current.Kind))
            {
                var a = ToDouble(prev);
                var b = ToDouble(current);
                var d = b - a;
                if (Math.Abs(d) <= epsilon)
                    return true;

                changed = true;
                deltaSign = d > 0 ? 1 : -1;
                return true;
            }

            // Vector2 (use magnitude)
            if (prev.Kind == ValueKind.Vector2 && current.Kind == ValueKind.Vector2)
            {
                var a = prev.AsVector2.sqrMagnitude;
                var b = current.AsVector2.sqrMagnitude;
                var d = b - a;
                var eps = epsilon * epsilon;
                if (Math.Abs(d) <= eps)
                    return true;

                changed = true;
                deltaSign = d > 0 ? 1 : -1;
                return true;
            }

            // Other kinds: only AnyChange via Equals
            return false;
        }

        static bool IsNumeric(ValueKind kind)
            => kind == ValueKind.Bool || kind == ValueKind.Int || kind == ValueKind.Float;

        static double ToDouble(in DynamicVariant v)
        {
            return v.Kind switch
            {
                ValueKind.Bool => v.AsBool ? 1.0 : 0.0,
                ValueKind.Int => v.AsInt,
                ValueKind.Float => v.AsFloat,
                _ => 0.0,
            };
        }

        void TryEnsureValueChangedSubscription()
        {
            if (_rule.RuleKind != MonitorRuleKind.ValueChanged || _valueChangedStates.Count == 0)
                return;

            for (int i = 0; i < _valueChangedStates.Count; i++)
            {
                TryEnsureValueChangedSubscription(_valueChangedStates[i]);
            }
        }

        void TryEnsureValueChangedSubscription(ValueChangedWatchState state)
        {
            if (!IsValueChangedTargetConfigured(state.Target))
                return;

            var targetScope = ResolveValueTargetScope(state.Target);
            if (targetScope?.Resolver == null)
                return;

            if (state.Target.ValueSource == MonitorValueSourceKind.VarStore)
            {
                if (state.VarChangedHandler != null)
                    return;

                if (!TryResolveValueTargetVarStore(state, out var vars) || vars == null)
                    return;

                state.ObservedVarStore = vars;
                state.VarChangedHandler = varId => OnValueTargetVarChanged(state, varId);
                vars.OnVarChanged += state.VarChangedHandler;
                return;
            }

            if (state.Target.ValueSource == MonitorValueSourceKind.Blackboard)
            {
                if (state.BlackboardVarChangedHandler != null)
                    return;

                if (!targetScope.Resolver.TryResolve<IBlackboardService>(out var bb) || bb == null)
                    return;

                state.ObservedBlackboardVars = bb.LocalVars;
                state.BlackboardVarChangedHandler = varId => OnBlackboardVarChanged(state, varId);
                state.ObservedBlackboardVars.OnVarChanged += state.BlackboardVarChangedHandler;
                return;
            }

            if (state.Target.ValueSource == MonitorValueSourceKind.Scalar)
            {
                if (state.ScalarSubscription != null)
                    return;

                if (!targetScope.Resolver.TryResolve<IBaseScalarService>(out var svc) || svc == null)
                    return;

                var key = state.Target.ScalarKey;
                if (key.Id == 0 && string.IsNullOrEmpty(key.Name))
                    return;

                try
                {
                    state.ScalarSubscription = svc.GlobalSubscribe(key, args => OnScalarValueChanged(state, args));
                }
                catch (Exception ex)
                {
#if UNITY_EDITOR
                    Debug.LogWarning($"[MonitorChannelRuntime] Subscribing to scalar key '{key.FormatLabel()}' failed for rule '{_rule.RuleName}' (id={InstanceId}): {ex.Message}. Falling back to polling.");
#endif
                    _ = ex;
                    state.ScalarSubscription = null;
                }
            }
        }

        void UnsubscribeValueChanged()
        {
            for (int i = 0; i < _valueChangedStates.Count; i++)
            {
                var state = _valueChangedStates[i];

                if (state.ObservedVarStore != null && state.VarChangedHandler != null)
                {
                    try { state.ObservedVarStore.OnVarChanged -= state.VarChangedHandler; } catch { }
                }
                state.ObservedVarStore = null;
                state.VarChangedHandler = null;

                if (state.ObservedBlackboardVars != null && state.BlackboardVarChangedHandler != null)
                {
                    try { state.ObservedBlackboardVars.OnVarChanged -= state.BlackboardVarChangedHandler; } catch { }
                }
                state.ObservedBlackboardVars = null;
                state.BlackboardVarChangedHandler = null;

                try { state.ScalarSubscription?.Dispose(); } catch { }
                state.ScalarSubscription = null;
                state.PendingFire = false;
                state.PendingRefresh = false;
            }
        }

        void OnValueTargetVarChanged(ValueChangedWatchState state, int varId)
        {
            if (_disposed) return;
            if (_rule.RuleKind != MonitorRuleKind.ValueChanged) return;
            if (state.Target.ValueSource != MonitorValueSourceKind.VarStore) return;
            if (varId == 0 || state.Target.VarStoreVarId == 0) return;
            if (state.Target.VarStoreVarId != varId) return;
            MarkValueChangedRefresh(state);
        }

        void OnBlackboardVarChanged(ValueChangedWatchState state, int varId)
        {
            if (_disposed) return;
            if (_rule.RuleKind != MonitorRuleKind.ValueChanged) return;
            if (state.Target.ValueSource != MonitorValueSourceKind.Blackboard) return;
            if (varId == 0 || state.Target.BlackboardVarId == 0) return;
            if (state.Target.BlackboardVarId != varId) return;
            MarkValueChangedRefresh(state);
        }

        void OnScalarValueChanged(ValueChangedWatchState state, ScalarValueChangedArgs args)
        {
            if (_disposed) return;
            if (_rule.RuleKind != MonitorRuleKind.ValueChanged) return;
            if (state.Target.ValueSource != MonitorValueSourceKind.Scalar) return;

            var oldV = DynamicVariant.FromFloat(args.OldValue);
            var newV = DynamicVariant.FromFloat(args.NewValue);

            if (TryShouldFireForValueChanged(state.Target, oldV, newV, out var shouldFire) && shouldFire)
            {
                state.LastValue = newV;
                state.HasLastValue = true;
                MarkValueChangedFired(state);
                return;
            }

            state.LastValue = newV;
            state.HasLastValue = true;
        }

        bool TryResolveValueTargetVarStore(ValueChangedWatchState state, out IVarStore? vars)
        {
            vars = null;

            var targetScope = ResolveValueTargetScope(state.Target);
            if (targetScope?.Resolver == null)
                return false;

            if (targetScope.Resolver.TryResolve<IMonitorChannelHub>(out var hub) && hub?.CurrentVarStore != null)
            {
                vars = hub.CurrentVarStore;
                return true;
            }

            if (targetScope.Resolver.TryResolve<IVarStore>(out var resolvedVars) && resolvedVars != null)
            {
                vars = resolvedVars;
                return true;
            }

            return false;
        }

        void TryInitializeLastWatchedValue()
        {
            for (int i = 0; i < _valueChangedStates.Count; i++)
            {
                var state = _valueChangedStates[i];
                if (state.HasLastValue)
                    continue;

                if (TryGetWatchedValue(state, out var current))
                {
                    state.LastValue = current;
                    state.HasLastValue = true;
                }
            }
        }

        void MarkValueChangedRefresh(ValueChangedWatchState state)
        {
            state.PendingRefresh = true;
            MarkValueChangedTriggered();
        }

        void MarkValueChangedFired(ValueChangedWatchState state)
        {
            state.PendingFire = true;
            state.PendingRefresh = false;
            MarkValueChangedTriggered();
        }

        bool HasConfiguredValueChangedTarget()
        {
            for (int i = 0; i < _valueChangedStates.Count; i++)
            {
                if (IsValueChangedTargetConfigured(_valueChangedStates[i].Target))
                    return true;
            }

            return false;
        }

        static bool IsValueChangedTargetConfigured(MonitorValueChangedTarget? target)
        {
            if (target == null)
                return false;

            return target.ValueSource switch
            {
                MonitorValueSourceKind.VarStore => target.VarStoreVarId != 0,
                MonitorValueSourceKind.Blackboard => target.BlackboardVarId != 0,
                MonitorValueSourceKind.Scalar => target.ScalarKey.Id != 0 || !string.IsNullOrEmpty(target.ScalarKey.Name),
                _ => false,
            };
        }

        static bool TryShouldFireForValueChanged(
            MonitorValueChangedTarget target,
            in DynamicVariant previous,
            in DynamicVariant current,
            out bool shouldFire)
        {
            shouldFire = false;

            if (!TryDetectValueDelta(previous, current, target.ChangeEpsilon, out var deltaSign, out var changed))
            {
                shouldFire = target.ValueChangeMode == MonitorValueChangeMode.AnyChange && previous != current;
                return true;
            }

            if (!changed)
            {
                shouldFire = target.ValueChangeMode == MonitorValueChangeMode.AnyChange && previous != current;
                return true;
            }

            shouldFire = ShouldFireForValueChanged(target.ValueChangeMode, deltaSign);
            return true;
        }

        static bool ShouldFireForValueChanged(MonitorValueChangeMode mode, int deltaSign)
        {
            return mode switch
            {
                MonitorValueChangeMode.AnyChange => true,
                MonitorValueChangeMode.Increased => deltaSign > 0,
                MonitorValueChangeMode.Decreased => deltaSign < 0,
                _ => false,
            };
        }

        /// <summary>
        /// 手動評価
        /// </summary>
        public void EvaluateManual(CancellationToken ct)
        {
            if (_disposed) return;
            if (_rule.RuleKind != MonitorRuleKind.ConditionOnly) return;

            _conditionEvaluatedOnce = true;
            EvaluateCondition(ct);
            CleanupCompletedTasks();
        }

        // ================================================================
        // イベント駆動ルール処理
        // ================================================================

        void ProcessEventDrivenRule(CancellationToken ct)
        {
            if (_rule.RuleKind == MonitorRuleKind.EventOnly)
            {
                // EventOnly: 即時実行
                TryExecuteCommands("Enter", _rule.OnEnterCommands, ct);
            }
            else if (_rule.RuleKind == MonitorRuleKind.EventAndCondition)
            {
                // EventAndCondition: 条件評価して true なら実行
                if (!_rule.Condition.HasSource)
                {
                    TryExecuteCommands("Enter", _rule.OnEnterCommands, ct);
                }
                else
                {
                    try
                    {
                        var ctx = CreateContext();
                        if (ctx != null && _rule.Condition.EvaluateBool(ctx))
                        {
                            TryExecuteCommands("Enter", _rule.OnEnterCommands, ct);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[MonitorChannelRuntime] EventAndCondition evaluation failed for '{_rule.RuleName}': {ex.Message}");
                    }
                }
            }
        }

        // ================================================================
        // 条件評価
        // ================================================================

        void EvaluateCondition(CancellationToken ct)
        {
            if (!_rule.Condition.HasSource) return;

            bool isFirstEvaluation = !_conditionEvaluatedOnce;
            _conditionEvaluatedOnce = true;

            var ctx = CreateContext();
            if (ctx == null) return;

            bool current;
            try
            {
                current = _rule.Condition.EvaluateBool(ctx);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MonitorChannelRuntime] Condition evaluation failed for '{_rule.RuleName}': {ex.Message}");
                return;
            }

            // ExecuteInitialCondition: 初回評価時は遷移判定をスキップし、
            // 現在の条件に合ったコマンドを一度だけ実行する
            if (isFirstEvaluation && _rule.ExecuteInitialCondition)
            {
                _previousState = current;
                BumpTelemetry();
                if (current)
                {
                    ResetWhileTimers();
                    TryExecuteCommands("Enter", _rule.OnEnterCommands, ct);
                }
                else
                {
                    ResetWhileTimers();
                    TryExecuteCommands("Exit", _rule.OnExitCommands, ct);
                }
                return;
            }

            bool prev = _previousState;
            _previousState = current;

            if (prev != current)
            {
                BumpTelemetry();
                if (_rule.CancelRunningOnConditionChange)
                    CancelRunningEntriesOnConditionChange();
            }

            // Enter: false -> true
            if (!prev && current)
            {
                ResetWhileTimers();
                TryExecuteCommands("Enter", _rule.OnEnterCommands, ct);
            }
            // Exit: true -> false
            else if (prev && !current)
            {
                ResetWhileTimers();
                TryExecuteCommands("Exit", _rule.OnExitCommands, ct);
            }
            // WhileTrue: true 維持中
            else if (prev && current)
            {
                TryExecuteWhileCommands("WhileTrue", _rule.WhileTrueCommands, ref _lastWhileTrueExecution, ct);
            }
            // WhileFalse: false 維持中
            else
            {
                TryExecuteWhileCommands("WhileFalse", _rule.WhileFalseCommands, ref _lastWhileFalseExecution, ct);
            }
        }

        VNext.CommandContext? CreateContext()
        {
            return _ctx;
        }

        void ResetWhileTimers()
        {
            _lastWhileTrueExecution = float.NegativeInfinity;
            _lastWhileFalseExecution = float.NegativeInfinity;
        }

        void TryExecuteWhileCommands(string phase, MonitorRuleWhileCommandSet whileSet, ref float lastExecution, CancellationToken ct)
        {
            var commands = whileSet.Commands;
            if (commands == null || commands.Count == 0) return;

            float interval = whileSet.IntervalSeconds;
            if (interval < 0f) interval = 0f;

            float now = Time.realtimeSinceStartup;
            if (interval > 0f && now - lastExecution < interval)
            {
                return;
            }

            lastExecution = now;
            // While commands are interval-polled and frequently canceled by design.
            // Suppress per-command cancel warnings to avoid noisy logs.
            TryExecuteCommands(phase, commands, ct, suppressCancelLog: true);
        }

        void CancelRunningEntriesOnConditionChange()
        {
            for (int i = 0; i < _runningEntries.Count; i++)
            {
                var entry = _runningEntries[i];
                if (entry.Completed)
                    continue;

                try
                {
                    entry.Cts?.Cancel();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        // ================================================================
        // コマンド実行
        // ================================================================

        void TryExecuteCommands(string phase, VNext.CommandListData? commands, CancellationToken ct, bool suppressCancelLog = false)
        {
            if (commands == null || commands.Count == 0) return;

            if (ct.IsCancellationRequested)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                //Debug.Log($"[MonitorChannelRuntime] TryExecuteCommands skipped (canceled). Rule='{_rule.RuleName}' Phase='{phase}' Scope={DescribeScope(_scope)}");
#endif
                return;
            }

            var behavior = _rule.Behavior;

            // SkipIfRunning: 同じ phase が実行中ならスキップ
            if (behavior == ExecutionBehavior.SkipIfRunning)
            {
                for (int i = 0; i < _runningEntries.Count; i++)
                {
                    var entry = _runningEntries[i];
                    if (!entry.Completed && entry.Phase == phase)
                    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        //Debug.Log($"[MonitorChannelRuntime] SkipIfRunning: Rule='{_rule.RuleName}' Phase='{phase}' Scope={DescribeScope(_scope)}");
#endif
                        return;
                    }
                }
            }

            // CancelAndRun: 同じ phase をキャンセル
            if (behavior == ExecutionBehavior.CancelAndRun)
            {
                for (int i = 0; i < _runningEntries.Count; i++)
                {
                    var entry = _runningEntries[i];
                    if (!entry.Completed && entry.Phase == phase)
                    {
                        LogCancelAndRun(entry, phase);
                        try
                        {
                            entry.Cts?.Cancel();
                        }
                        catch (Exception ex)
                        {
                            Debug.LogException(ex);
                        }
                    }
                }
            }

            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var runningEntry = new RunningEntry(_rule.RuleName, phase, cts);
            _runningEntries.Add(runningEntry);
            BumpTelemetry();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            //Debug.Log($"[MonitorChannelRuntime] Start commands: Rule='{_rule.RuleName}' Phase='{phase}' Behavior={behavior} Scope={DescribeScope(_scope)}");
#endif
            var options = suppressCancelLog ? _runOptions.WithSuppressCancelLog(true) : _runOptions;
            runningEntry.Task = ExecuteCommandsAsync(runningEntry, commands, cts.Token, options);
        }

        void LogCancelAndRun(RunningEntry entry, string phase)
        {
            Debug.LogWarning(
                $"[MonitorChannelRuntime] CancelAndRun: stopping previous run '{entry.RuleName}' phase '{entry.Phase}' so rule '{_rule.RuleName}' can start a new '{phase}' phase.");
        }

        void LogCancelDueToDispose(RunningEntry entry)
        {
            //Debug.LogWarning(
            //    $"[MonitorChannelRuntime] Runtime disposing: canceling run '{entry.RuleName}' phase '{entry.Phase}' because the rule is being torn down.");
        }

        // Overload: execute with provided CommandContext
        void TryExecuteCommands(string phase, VNext.CommandListData commands, VNext.CommandContext ctx, CancellationToken ct, bool suppressCancelLog = false)
        {
            if (commands == null || commands.Count == 0) return;

            var behavior = _rule.Behavior;

            if (behavior == ExecutionBehavior.SkipIfRunning)
            {
                for (int i = 0; i < _runningEntries.Count; i++)
                {
                    var entry = _runningEntries[i];
                    if (!entry.Completed && entry.Phase == phase)
                        return;
                }
            }

            if (behavior == ExecutionBehavior.CancelAndRun)
            {
                for (int i = 0; i < _runningEntries.Count; i++)
                {
                    var entry = _runningEntries[i];
                    if (!entry.Completed && entry.Phase == phase)
                    {
                        LogCancelAndRun(entry, phase);
                        try { entry.Cts?.Cancel(); } catch { }
                    }
                }
            }

            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var runningEntry = new RunningEntry(_rule.RuleName, phase, cts);
            _runningEntries.Add(runningEntry);
            BumpTelemetry();

            var options = suppressCancelLog ? _runOptions.WithSuppressCancelLog(true) : _runOptions;
            runningEntry.Task = ExecuteCommandsWithContextAsync(runningEntry, commands, ctx, cts.Token, options);
        }

        async UniTask ExecuteCommandsWithContextAsync(RunningEntry entry, VNext.CommandListData commands, VNext.CommandContext ctx, CancellationToken ct, VNext.CommandRunOptions runOptions)
        {
            try
            {
                if (ctx?.Runner == null)
                {
                    Debug.LogError($"[MonitorChannelRuntime] ExecuteCommandsWithContextAsync: ctx.Runner is null for '{_rule.RuleName}'");
                    return;
                }

                var result = await ctx.Runner.ExecuteListAsync(commands, ctx, ct, runOptions);
                if (result.Status == VNext.CommandRunStatus.Error)
                {
                    Debug.LogError($"[MonitorChannelRuntime] Command execution failed: Rule='{entry.RuleName}', Phase='{entry.Phase}'");
                }
            }
            catch (OperationCanceledException)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[MonitorChannelRuntime] ExecuteCommandsWithContextAsync canceled: Rule='{entry.RuleName}' Phase='{entry.Phase}' Scope={DescribeScope(_scope)}");
#endif
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MonitorChannelRuntime] Command execution failed: Rule='{entry.RuleName}', Phase='{entry.Phase}'");
                Debug.LogException(ex);
            }
            finally
            {
                entry.Completed = true;
            }
        }

        async UniTask ExecuteCommandsAsync(RunningEntry entry, VNext.CommandListData commands, CancellationToken ct, VNext.CommandRunOptions runOptions)
        {
            try
            {
                var ctx = CreateContext();
                if (ctx == null)
                {
                    Debug.LogError($"[MonitorChannelRuntime] ExecuteCommandsAsync: Context is null for '{_rule.RuleName}'");
                    return;
                }

                var result = await _runner.ExecuteListAsync(commands, ctx, ct, runOptions);
                if (result.Status == VNext.CommandRunStatus.Error)
                {
                    Debug.LogError($"[MonitorChannelRuntime] Command execution failed: Rule='{entry.RuleName}', Phase='{entry.Phase}'");
                }
            }
            catch (OperationCanceledException)
            {
                // 正常キャンセル
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[MonitorChannelRuntime] ExecuteCommandsAsync canceled: Rule='{entry.RuleName}' Phase='{entry.Phase}' Scope={DescribeScope(_scope)}");
#endif
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MonitorChannelRuntime] Command execution failed: Rule='{entry.RuleName}', Phase='{entry.Phase}'");
                Debug.LogException(ex);
            }
            finally
            {
                entry.Completed = true;
            }
        }

        void CleanupCompletedTasks()
        {
            bool changed = false;
            for (int i = _runningEntries.Count - 1; i >= 0; i--)
            {
                var entry = _runningEntries[i];
                if (entry.Completed && !entry.Disposed)
                {
                    try
                    {
                        entry.Cts?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                    entry.Disposed = true;
                    _runningEntries.RemoveAt(i);
                    changed = true;
                }
            }

            if (changed)
                BumpTelemetry();
        }

        // ================================================================
        // テレメトリ
        // ================================================================

        void BumpTelemetry()
        {
            unchecked { _telemetryVersion++; }
        }

        /// <summary>
        /// スナップショットを作成
        /// </summary>
        public MonitorRuntimeSnapshot GetSnapshot()
        {
            var runningSnapshots = new List<MonitorRunningSnapshot>(_runningEntries.Count);
            for (int i = 0; i < _runningEntries.Count; i++)
            {
                var entry = _runningEntries[i];
                runningSnapshots.Add(new MonitorRunningSnapshot(
                    entry.RuleName,
                    entry.Phase,
                    entry.Completed));
            }

            return new MonitorRuntimeSnapshot(
                _rule.RuleName,
                _previousState,
                _rule.Behavior,
                _rule.CancelRunningOnConditionChange,
                _rule.Condition.HasSource ? _rule.Condition.SourceTypeName : null,
                _cachedDependentKeys ?? Array.Empty<string>(),
                _rule.RuleKind,
                _rule.EventName,
                runningSnapshots);
        }

        // ================================================================
        // Dispose
        // ================================================================

        public void Dispose()
        {
            if (_disposed) return;
            // Debug: Disposing runtime - suppressed
            // Debug.Log($"[MonitorChannelRuntime] Disposing runtime id={InstanceId} rule='{_rule.RuleName}'");
            _disposed = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            //Debug.Log($"[MonitorChannelRuntime] Dispose: Rule='{_rule.RuleName}' Scope={DescribeScope(_scope)} Running={_runningEntries.Count}");
#endif
            // 実行中タスクをキャンセル
            for (int i = _runningEntries.Count - 1; i >= 0; i--)
            {
                var entry = _runningEntries[i];
                LogCancelDueToDispose(entry);
                try
                {
                    entry.Cts?.Cancel();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
                if (!entry.Disposed)
                {
                    try
                    {
                        entry.Cts?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                    entry.Disposed = true;
                }
            }
            _runningEntries.Clear();

            // unsubscribe from event if any
            UnsubscribeEvent();

            // unsubscribe from value-changed sources
            UnsubscribeValueChanged();

            if (_initialValueChangedEnterCts != null)
            {
                try
                {
                    _initialValueChangedEnterCts.Cancel();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }

                try
                {
                    _initialValueChangedEnterCts.Dispose();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }

                _initialValueChangedEnterCts = null;
            }

            _vars = null;
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

        static int[]? ResolveDependentVarIds(IReadOnlyList<string>? dependentKeys)
        {
            if (dependentKeys == null || dependentKeys.Count == 0)
                return Array.Empty<int>();

            var ids = new List<int>(dependentKeys.Count);
            for (int i = 0; i < dependentKeys.Count; i++)
            {
                var key = dependentKeys[i];
                if (!VarIdResolver.TryResolve(key, out var varId) || varId == 0)
                    continue;
                ids.Add(varId);
            }
            return ids.ToArray();
        }
    }

    /// <summary>
    /// Runtime スナップショット（テレメトリ用）
    /// </summary>
    public readonly struct MonitorRuntimeSnapshot
    {
        public readonly string RuleName;
        public readonly bool IsTrue;
        public readonly ExecutionBehavior Behavior;
        public readonly bool CancelRunningOnConditionChange;
        public readonly string? Condition;
        public readonly IReadOnlyList<string> DependentKeys;
        public readonly MonitorRuleKind RuleKind;
        public readonly string? EventName;
        public readonly IReadOnlyList<MonitorRunningSnapshot> RunningEntries;

        public MonitorRuntimeSnapshot(
            string ruleName,
            bool isTrue,
            ExecutionBehavior behavior,
            bool cancelRunningOnConditionChange,
            string? condition,
            IReadOnlyList<string> dependentKeys,
            MonitorRuleKind ruleKind,
            string? eventName,
            IReadOnlyList<MonitorRunningSnapshot> runningEntries)
        {
            RuleName = ruleName;
            IsTrue = isTrue;
            Behavior = behavior;
            CancelRunningOnConditionChange = cancelRunningOnConditionChange;
            Condition = condition;
            DependentKeys = dependentKeys;
            RuleKind = ruleKind;
            EventName = eventName;
            RunningEntries = runningEntries;
        }
    }
}
