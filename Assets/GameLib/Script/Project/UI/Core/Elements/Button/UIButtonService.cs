#nullable enable
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using VNext = Game.Commands.VNext;
using Game.Common;
using VContainer;
namespace Game.UI
{
    // ================================================================
    // UIButtonKind: ボタンの種類
    // ================================================================

    /// <summary>
    /// ボタンの種類。
    /// 
    /// ## Instant
    /// 
    /// 即座に反応するボタン。
    /// SubmitDownでOnSubmitDownCommands、SubmitUpでOnSubmitUpCommandsを実行。
    /// 
    /// ## Hold
    /// 
    /// 長押しが必要なボタン。
    /// SubmitDownで長押し開始、_holdTime経過後にOnSubmitUpCommandsを実行。
    /// 途中でキャンセル条件が発生したらOnHoldCancelCommandsを実行。
    /// 
    /// ### キャンセル条件
    /// - マウスがDown中に移動した
    /// - ナビゲーション（方向入力）が発生した
    /// - ページが移動した（将来対応）
    /// - 選択が外れた
    /// - イベントが停止した（SubmitUpなしで終了）
    /// </summary>
    public enum UIButtonKind
    {
        /// <summary>即座に反応するボタン</summary>
        Instant,

        /// <summary>長押しが必要なボタン</summary>
        Hold,
    }

    public interface IUIButtonService
    {
        UIButtonKind Kind { get; set; }
        bool CanSubmit { get; set; }
        DynamicValue<bool> InputControlCondition { get; set; }
        float HoldTime { get; set; }
        float HoldInterval { get; set; }
        UIInputAction TriggerAction { get; set; }

        VNext.CommandListData OnSubmitDownCommands { get; }
        VNext.CommandListData OnSubmitUpCommands { get; }
        VNext.CommandListData OnHoldDecisionCommands { get; }
        VNext.CommandListData OnHoldIntervalCommands { get; }
        VNext.CommandListData OnHoldCancelCommands { get; }

        bool IsHoldDecisionExecuting { get; }
        bool IsSubmitUpExecuting { get; }
        bool IsHolding { get; }
        float HoldProgress { get; }

        bool GuardSelectionWhileHolding { get; set; }
        bool GuardDuringCommandExecution { get; set; }
        bool DisableSelectionDuringCommandExecution { get; set; }

        void AppendSubmitUpCommands(IReadOnlyList<VNext.ICommandSource> commands);
        void RefreshTelemetry();
    }

    // ================================================================
    // UIButtonService: ボタン処理サービス
    // ================================================================

    /// <summary>
    /// ボタン処理サービス。
    /// 
    /// ## 概要
    /// 
    /// UIElementにボタン機能を提供する。
    /// IUIInputConsumerを実装し、Submit入力を処理する。
    /// 
    /// ## 動作条件
    /// 
    /// - CanSubmit == true
    /// - 所属UIElementがActive状態
    /// - 所属UIElementが選択されている
    /// 
    /// ## Instantボタン
    /// 
    /// 1. SubmitDown → OnSubmitDownCommands実行
    /// 2. SubmitUp → OnSubmitUpCommands実行
    /// 
    /// ## Holdボタン
    /// 
    /// 1. SubmitDown → OnSubmitDownCommands実行、長押しタイマー開始
    /// 2. HoldTime経過 → OnSubmitUpCommands実行（成功）
    /// 3. キャンセル条件発生 → OnHoldCancelCommands実行、タイマーリセット
    /// 
    /// ## VarStore
    /// 
    /// コマンド実行時、以下の変数が VarStore に設定される:
    /// - "HoldTime": 設定されたホールド時間（Holdボタンのみ）
    /// - "HoldProgress": 長押し進捗率 0.0～1.0（Holdボタンのみ）
    /// </summary>
    public sealed class UIButtonService :
        IUIButtonService,
        IUIInputConsumer,
        IUIButtonTelemetry,
        IScopeAcquireHandler,
        IScopeReleaseHandler
    {
        // ----------------------------------------------------------------
        // 定数
        // ----------------------------------------------------------------

        /// <summary>入力優先度（ボタンは通常高め）</summary>
        const int ButtonInputPriority = 100;

        /// <summary>VarStore キー: ホールド時間</summary>
        public const string VarKeyHoldTime = "HoldTime";

        /// <summary>VarStore キー: ホールド進捗率</summary>
        public const string VarKeyHoldProgress = "HoldProgress";

        // ----------------------------------------------------------------
        // フィールド
        // ----------------------------------------------------------------

        /// <summary>所有者のUIElementLifetimeScope</summary>
        readonly IScopeNode _owner;

        /// <summary>ボタンの種類</summary>
        UIButtonKind _kind = UIButtonKind.Instant;

        /// <summary>Submit可能かどうか</summary>
        bool _canSubmit = true;

        /// <summary>どの入力アクションでこのボタンをトリガーするか</summary>
        UIInputAction _triggerAction = UIInputAction.Submit;

        /// <summary>長押し必要時間（秒）</summary>
        float _holdTime = 1.0f;

        /// <summary>Hold中に一定間隔で実行する間隔（秒）</summary>
        float _holdInterval = 0.1f;

        /// <summary>SubmitDown時に実行するコマンド</summary>
        readonly VNext.CommandListData _onSubmitDownCommands = new();

        /// <summary>SubmitUp/Hold成功時に実行するコマンド</summary>
        readonly VNext.CommandListData _onSubmitUpCommands = new();

        /// <summary>Holdの決定（HoldTime達成）時に実行するコマンド</summary>
        readonly VNext.CommandListData _onHoldDecisionCommands = new();

        /// <summary>Hold中に一定間隔で実行するコマンド</summary>
        readonly VNext.CommandListData _onHoldIntervalCommands = new();

        /// <summary>Hold中キャンセル時に実行するコマンド</summary>
        readonly VNext.CommandListData _onHoldCancelCommands = new();

        /// <summary>UIElementState参照</summary>
        IUIElementState? _elementState;

        /// <summary>UISelection参照</summary>
        IUISelectionState? _selectionState;
        IUISelectionBlockService? _selectionBlockService;

        /// <summary>Whether to guard selection/navigation while holding</summary>
        bool _guardSelectionWhileHolding;

        /// <summary>入力制御条件（DynamicValue<bool>）</summary>
        Game.Common.DynamicValue<bool> _inputControlCondition;

        /// <summary>Handle returned from AcquireBlock while holding</summary>
        IDisposable? _selectionBlockHandle;

        /// <summary>コマンド実行用Runner</summary>
        VNext.ICommandRunner? _commandRunner;

        IUIInputConsumerHub? _consumerHub;

        /// <summary>現在長押し中かどうか</summary>
        bool _isHolding;

        /// <summary>OnHoldDecision コマンド実行中かどうか</summary>
        bool _isHoldDecisionExecuting;

        /// <summary>OnSubmitUp コマンド実行中かどうか</summary>
        bool _isSubmitUpExecuting;

        UIButtonInputRejectReason _lastRejectReason = UIButtonInputRejectReason.None;
        UIInputEventType _lastInputEventType = UIInputEventType.None;
        UIInputPhase _lastInputPhase = UIInputPhase.Down;
        bool _lastInputMatched;
        bool _lastInputAccepted;
        bool _lastInputConditionHasSource;
        bool _lastInputConditionValue = true;
        UIButtonTelemetrySnapshot _lastSnapshot;

        public event Action<UIButtonTelemetrySnapshot>? OnTelemetryUpdated;
        public UIButtonTelemetrySnapshot LastSnapshot => _lastSnapshot;

        /// <summary>GuardDuringCommandExecution オプション</summary>
        bool _guardDuringCommandExecution;

        /// <summary>DisableSelectionDuringCommandExecution オプション</summary>
        bool _disableSelectionDuringCommandExecution;

        /// <summary>長押し開始時のポインター位置</summary>
        Vector2 _holdStartPosition;

        /// <summary>長押し経過時間</summary>
        float _holdElapsed;

        /// <summary>コマンド実行用CancellationTokenSource</summary>
        CancellationTokenSource? _commandCts;

        /// <summary>Hold中の定期実行ループ用CancellationTokenSource</summary>
        CancellationTokenSource? _holdIntervalCts;

        // ----------------------------------------------------------------
        // プロパティ
        // ----------------------------------------------------------------

        /// <summary>
        /// ボタンの種類。
        /// </summary>
        public UIButtonKind Kind
        {
            get => _kind;
            set => _kind = value;
        }

        /// <summary>
        /// Submit可能かどうか。
        /// </summary>
        public bool CanSubmit
        {
            get => _canSubmit;
            set => _canSubmit = value;
        }

        /// <summary>
        /// 長押し必要時間（秒）。Holdボタンでのみ使用。
        /// </summary>
        public float HoldTime
        {
            get => _holdTime;
            set => _holdTime = Mathf.Max(0.01f, value);
        }

        public float HoldInterval
        {
            get => _holdInterval;
            set => _holdInterval = Mathf.Max(0.01f, value);
        }

        public UIInputAction TriggerAction
        {
            get => _triggerAction;
            set => _triggerAction = value;
        }

        public DynamicValue<bool> InputControlCondition
        {
            get => _inputControlCondition;
            set => _inputControlCondition = value;
        }

        /// <summary>
        /// SubmitDown時に実行するコマンドリスト。
        /// </summary>
        public VNext.CommandListData OnSubmitDownCommands => _onSubmitDownCommands;

        /// <summary>
        /// SubmitUp/Hold成功時に実行するコマンドリスト。
        /// </summary>
        public VNext.CommandListData OnSubmitUpCommands => _onSubmitUpCommands;

        public VNext.CommandListData OnHoldDecisionCommands => _onHoldDecisionCommands;

        public VNext.CommandListData OnHoldIntervalCommands => _onHoldIntervalCommands;

        /// <summary>
        /// Hold中キャンセル時に実行するコマンドリスト。
        /// </summary>
        public VNext.CommandListData OnHoldCancelCommands => _onHoldCancelCommands;

        /// <summary>
        /// OnHoldDecision実行中かどうか。
        /// </summary>
        public bool IsHoldDecisionExecuting => _isHoldDecisionExecuting;

        public bool IsSubmitUpExecuting => _isSubmitUpExecuting;

        /// <summary>
        /// 現在長押し中かどうか。
        /// </summary>
        public bool IsHolding => _isHolding;

        /// <summary>
        /// 長押し進捗率（0.0～1.0）。
        /// </summary>
        public float HoldProgress => _holdTime > 0 ? Mathf.Clamp01(_holdElapsed / _holdTime) : 0f;

        /// <summary>
        /// Whether to guard selection/navigation while holding (exposed to runtime to allow inspector updates)
        /// </summary>
        public bool GuardSelectionWhileHolding
        {
            get => _guardSelectionWhileHolding;
            set => _guardSelectionWhileHolding = value;
        }

        public bool GuardDuringCommandExecution
        {
            get => _guardDuringCommandExecution;
            set => _guardDuringCommandExecution = value;
        }

        public bool DisableSelectionDuringCommandExecution
        {
            get => _disableSelectionDuringCommandExecution;
            set => _disableSelectionDuringCommandExecution = value;
        }

        public void AppendSubmitUpCommands(IReadOnlyList<VNext.ICommandSource> commands)
        {
            if (commands == null || commands.Count == 0)
                return;

            var existing = _onSubmitUpCommands.Commands;
            for (int i = 0; i < commands.Count; i++)
            {
                var command = commands[i];
                if (command != null)
                {
                    if (!ContainsCommand(existing, command))
                        _onSubmitUpCommands.Add(command);
                }
            }
        }

        static bool ContainsCommand(IReadOnlyList<VNext.ICommandSource> list, VNext.ICommandSource target)
        {
            if (list == null || target == null)
                return false;

            for (int i = 0; i < list.Count; i++)
            {
                if (ReferenceEquals(list[i], target))
                    return true;
            }

            return false;
        }

        // ----------------------------------------------------------------
        // IUIInputConsumer実装
        // ----------------------------------------------------------------

        /// <inheritdoc/>
        public int Priority => ButtonInputPriority;

        // ----------------------------------------------------------------
        // コンストラクタ
        // ----------------------------------------------------------------

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="owner">所有者のUIElementLifetimeScope</param>
        public UIButtonService(
            IScopeNode owner,
            IUIButtonOptions options,
            IUIInputConsumerHub consumerHub,
            IUIElementState elementState,
            IUISelectionState selectionState,
            IUISelectionBlockService? selectionBlockService,
            VNext.ICommandRunner commandRunner)
        {
            _owner = owner;
            _consumerHub = consumerHub;
            _elementState = elementState;
            _selectionState = selectionState;
            _selectionBlockService = selectionBlockService;
            _commandRunner = commandRunner;

            // オプション反映
            _kind = options.Kind;
            _canSubmit = options.CanSubmit;
            _holdTime = options.HoldTime;
            _holdInterval = Mathf.Max(0.01f, options.HoldInterval);
            _guardSelectionWhileHolding = options.GuardSelectionWhileHolding;
            _guardDuringCommandExecution = options.GuardDuringCommandExecution;
            _disableSelectionDuringCommandExecution = options.DisableSelectionDuringCommandExecution;
            _inputControlCondition = options.InputControlCondition;
            _triggerAction = options.TriggerAction;

            // command
            _onSubmitDownCommands.SetCommands(new System.Collections.Generic.List<VNext.ICommandSource>(options.OnSubmitDownCommands.Commands));
            _onSubmitUpCommands.SetCommands(new System.Collections.Generic.List<VNext.ICommandSource>(options.OnSubmitUpCommands.Commands));
            _onHoldDecisionCommands.SetCommands(new System.Collections.Generic.List<VNext.ICommandSource>(options.OnHoldDecisionCommands.Commands));
            _onHoldIntervalCommands.SetCommands(new System.Collections.Generic.List<VNext.ICommandSource>(options.OnHoldIntervalCommands.Commands));
            _onHoldCancelCommands.SetCommands(new System.Collections.Generic.List<VNext.ICommandSource>(options.OnHoldCancelCommands.Commands));

            // events removed
            PublishTelemetry();
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            // ConsumerHubに登録
            _consumerHub?.Register(this);
            // 選択変更を監視してHold中キャンセルを検出
            if (_selectionState != null)
            {
                _selectionState.OnSelectionChanged += HandleSelectionChanged;
            }
            PublishTelemetry();
        }
        public void OnRelease(IScopeNode scope, bool isDestroy)
        {
            // ConsumerHubから登録解除
            _consumerHub?.Unregister(this);
            // 選択変更監視解除
            if (_selectionState != null)
            {
                _selectionState.OnSelectionChanged -= HandleSelectionChanged;
            }

            // Ensure selection block released when service released
            try { _selectionBlockHandle?.Dispose(); } catch { }
            _selectionBlockHandle = null;

            StopHoldIntervalLoop();

            _commandCts?.Cancel();
            _commandCts?.Dispose();
            _commandCts = null;

            PublishTelemetry();
        }


        // ----------------------------------------------------------------
        // IUIInputConsumer実装
        // ----------------------------------------------------------------

        /// <inheritdoc/>
        public bool Consume(in UIInputEvent e)
        {
            _lastInputEventType = e.Type;
            _lastInputMatched = UIInputTriggerUtil.TryMatchPhase(in e, _triggerAction, out var phase);
            _lastInputPhase = _lastInputMatched ? phase : UIInputPhase.Down;

            // 条件チェック
            if (!CanProcessInput())
            {
                _lastInputAccepted = false;
                PublishTelemetry();
                return false;
            }

            // Trigger input (configurable)
            if (_lastInputMatched)
            {
                bool handled;
                switch (phase)
                {
                    case UIInputPhase.Down:
                        handled = HandleTriggerDown(e);
                        break;

                    case UIInputPhase.Held:
                        handled = HandleTriggerHeld(e);
                        break;

                    case UIInputPhase.Up:
                        handled = HandleTriggerUp(e);
                        break;

                    default:
                        handled = false;
                        break;
                }

                _lastInputAccepted = handled;
                PublishTelemetry();
                return handled;
            }

            switch (e.Type)
            {
                case UIInputEventType.Navigate:
                    // ナビゲーション入力でHoldをキャンセル
                    if (_isHolding && e.Direction.sqrMagnitude > 0.01f)
                    {
                        CancelHold();
                    }
                    _lastInputAccepted = false;
                    PublishTelemetry();
                    return false;

                case UIInputEventType.PointerMove:
                    // ポインター移動でHoldをキャンセル（閾値超え時）
                    if (_isHolding)
                    {
                        var delta = e.PointerPosition - _holdStartPosition;
                        if (delta.sqrMagnitude > 100f) // 10px以上移動
                        {
                            CancelHold();
                        }
                    }
                    _lastInputAccepted = false;
                    PublishTelemetry();
                    return false;

                default:
                    _lastInputAccepted = false;
                    PublishTelemetry();
                    return false;
            }
        }

        // ----------------------------------------------------------------
        // 入力ハンドラ
        // ----------------------------------------------------------------

        /// <summary>
        /// Submit入力を処理できる状態かチェック。
        /// </summary>
        bool CanProcessInput()
        {
            var ok = TryCheckCanProcessInput(out var reason, out var hasSource, out var value);
            _lastRejectReason = reason;
            _lastInputConditionHasSource = hasSource;
            _lastInputConditionValue = value;
            return ok;
        }

        bool TryCheckCanProcessInput(out UIButtonInputRejectReason reason, out bool conditionHasSource, out bool conditionValue)
        {
            conditionHasSource = false;
            conditionValue = true;

            // OnHoldDecision実行中は入力を受け付けない
            if (_guardDuringCommandExecution && (_isHoldDecisionExecuting || _isSubmitUpExecuting))
            {
                reason = UIButtonInputRejectReason.GuardDuringCommandExecution;
                return false;
            }

            // CanSubmitフラグ
            if (!_canSubmit)
            {
                reason = UIButtonInputRejectReason.CanSubmitFalse;
                return false;
            }

            // UIElementがActive
            if (_elementState != null && !_elementState.IsEffectivelyActive)
            {
                reason = UIButtonInputRejectReason.ElementNotActive;
                return false;
            }
            if (_elementState != null && !_elementState.IsVisible)
            {
                reason = UIButtonInputRejectReason.ElementNotVisible;
                return false;
            }

            // 自分が選択されている
            if (_selectionState == null)
            {
                reason = UIButtonInputRejectReason.SelectionStateMissing;
                return false;
            }
            if (!ReferenceEquals(_selectionState.CurrentElement, _owner))
            {
                reason = UIButtonInputRejectReason.NotSelected;
                return false;
            }

            // InputControlCondition評価
            if (_inputControlCondition.HasSource)
            {
                conditionHasSource = true;
                var varStore = _owner.Resolver?.TryResolve<IVarStore>(out var resolved) == true ? resolved : new VarStore();
                var context = new Game.Common.SimpleDynamicContext(varStore, _owner);
                conditionValue = _inputControlCondition.EvaluateBool(context);
                if (!conditionValue)
                {
                    reason = UIButtonInputRejectReason.InputControlConditionFalse;
                    return false;
                }
            }

            reason = UIButtonInputRejectReason.None;
            return true;
        }

        void PublishTelemetry()
        {
            var selected = _selectionState != null && ReferenceEquals(_selectionState.CurrentElement, _owner);
            var visible = _elementState != null ? _elementState.IsVisible : false;
            var active = _elementState != null ? _elementState.IsEffectivelyActive : false;

            _lastSnapshot = new UIButtonTelemetrySnapshot(
                ownerName: _owner?.Identity?.SelfTransform != null ? _owner.Identity.SelfTransform.name : "(none)",
                kind: _kind,
                triggerAction: _triggerAction,
                canSubmit: _canSubmit,
                isSelected: selected,
                isVisible: visible,
                isEffectivelyActive: active,
                guardSelectionWhileHolding: _guardSelectionWhileHolding,
                guardDuringCommandExecution: _guardDuringCommandExecution,
                disableSelectionDuringCommandExecution: _disableSelectionDuringCommandExecution,
                isHolding: _isHolding,
                holdProgress: HoldProgress,
                isHoldDecisionExecuting: _isHoldDecisionExecuting,
                isSubmitUpExecuting: _isSubmitUpExecuting,
                lastInputEventType: _lastInputEventType,
                lastInputPhase: _lastInputPhase,
                lastInputMatched: _lastInputMatched,
                lastInputAccepted: _lastInputAccepted,
                lastRejectReason: _lastRejectReason,
                inputConditionHasSource: _lastInputConditionHasSource,
                inputConditionValue: _lastInputConditionValue,
                timestampUtc: DateTime.UtcNow.ToOADate());

            OnTelemetryUpdated?.Invoke(_lastSnapshot);
        }

        public void RefreshTelemetry()
        {
            PublishTelemetry();
        }

        /// <summary>
        /// SubmitDown処理。
        /// </summary>
        bool HandleTriggerDown(in UIInputEvent e)
        {
            // OnSubmitDownCommands実行
            ExecuteCommands(_onSubmitDownCommands, createHoldVariables: _kind == UIButtonKind.Hold).Forget();

            // Capture values to avoid capturing the 'in' parameter inside lambda
            var pointerPosition = e.PointerPosition;

            if (_kind == UIButtonKind.Hold)
            {
                // 長押し開始
                _isHolding = true;
                _holdStartPosition = pointerPosition;
                _holdElapsed = 0f;

                StartHoldIntervalLoop();

                // If configured, acquire a selection block to prevent other UI selection/navigation while holding
                if (_guardSelectionWhileHolding && _selectionBlockService != null)
                {
                    try
                    {
                        _selectionBlockHandle = _selectionBlockService.AcquireBlock(this, UISelectionBlockMask.Navigation | UISelectionBlockMask.Pointer);
                    }
                    catch
                    {
                        // best-effort: ignore failures
                        _selectionBlockHandle = null;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// SubmitHeld処理（長押し中）。
        /// </summary>
        bool HandleTriggerHeld(in UIInputEvent e)
        {
            if (_kind != UIButtonKind.Hold) return false;
            if (!_isHolding) return false;

            // 経過時間を加算
            _holdElapsed += e.DeltaTime;

            // 長押し完了チェック
            if (_holdElapsed >= _holdTime)
            {
                // 成功
                _isHolding = false;
                StopHoldIntervalLoop();
                // Release selection block if any
                try { _selectionBlockHandle?.Dispose(); } catch { }
                _selectionBlockHandle = null;
                // HoldDecision があればそれを、なければ互換のため OnSubmitUp を使う
                var decisionCommands = _onHoldDecisionCommands.Count > 0 ? _onHoldDecisionCommands : _onSubmitUpCommands;
                ExecuteHoldDecisionCommands(decisionCommands).Forget();

            }

            return true;
        }

        /// <summary>
        /// SubmitUp処理。
        /// </summary>
        bool HandleTriggerUp(in UIInputEvent e)
        {
            if (_kind == UIButtonKind.Hold)
            {
                // Holdボタンの場合、タイマー完了前のUpはキャンセル扱い
                if (_isHolding)
                {
                    CancelHold();
                }
                return true;
            }
            else
            {
                // Instantボタンの場合、OnSubmitUpCommands実行
                ExecuteSubmitUpCommands(_onSubmitUpCommands).Forget();
                return true;
            }
        }

        // ----------------------------------------------------------------
        // キャンセル処理
        // ----------------------------------------------------------------

        /// <summary>
        /// 長押しをキャンセルする。
        /// </summary>
        void CancelHold()
        {
            if (!_isHolding) return;

            var progress = HoldProgress;

            _isHolding = false;
            _holdElapsed = 0f;
            StopHoldIntervalLoop();
            // Release selection block if any
            try { _selectionBlockHandle?.Dispose(); } catch { }
            _selectionBlockHandle = null;
            // OnHoldCancelCommands実行
            ExecuteCommands(_onHoldCancelCommands, createHoldVariables: true, forcedHoldProgress: progress).Forget();
        }

        /// <summary>
        /// 選択変更ハンドラ。
        /// 選択が外れたらHoldをキャンセル。
        /// </summary>
        void HandleSelectionChanged(IScopeNode? newSelection)
        {
            if (_isHolding && !ReferenceEquals(newSelection, _owner))
            {
                CancelHold();
            }
        }

        // ----------------------------------------------------------------
        // コマンド実行
        // ----------------------------------------------------------------

        void StartHoldIntervalLoop()
        {
            if (_holdInterval <= 0f) return;
            if (_onHoldIntervalCommands.Count == 0) return;

            StopHoldIntervalLoop();

            _holdIntervalCts = new CancellationTokenSource();
            RunHoldIntervalLoopAsync(_holdIntervalCts.Token).Forget();
        }

        void StopHoldIntervalLoop()
        {
            _holdIntervalCts?.Cancel();
            _holdIntervalCts?.Dispose();
            _holdIntervalCts = null;
        }

        async UniTaskVoid RunHoldIntervalLoopAsync(CancellationToken ct)
        {
            if (_commandRunner == null) return;

            while (_isHolding && !ct.IsCancellationRequested)
            {
                try
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(_holdInterval), ignoreTimeScale: true, cancellationToken: ct);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                if (!_isHolding) return;
                if (_onHoldIntervalCommands.Count == 0) continue;

                var variables = new VarStore();
                if (VarIdResolver.TryResolve(VarKeyHoldTime, out var varId) && varId != 0)
                    variables.TrySetVariant(varId, DynamicVariant.FromFloat(_holdTime));
                if (VarIdResolver.TryResolve(VarKeyHoldProgress, out varId) && varId != 0)
                    variables.TrySetVariant(varId, DynamicVariant.FromFloat(HoldProgress));

                var options = VNext.CommandRunOptions.Default;
                var ctx = new VNext.CommandContext(_owner, variables, _commandRunner, _owner, options);

                try
                {
                    await _commandRunner.ExecuteListAsync(_onHoldIntervalCommands, ctx, ct, options);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        /// <summary>
        /// コマンドを実行する。
        /// </summary>
        async UniTaskVoid ExecuteCommands(VNext.CommandListData commands, bool createHoldVariables, float? forcedHoldProgress = null)
        {
            await ExecuteCommandsAsync(commands, createHoldVariables, forcedHoldProgress);
        }

        async UniTask ExecuteCommandsAsync(VNext.CommandListData commands, bool createHoldVariables, float? forcedHoldProgress = null)
        {
            if (_commandRunner == null) return;
            if (commands.Count == 0) return;

            // 既存の実行をキャンセル
            _commandCts?.Cancel();
            _commandCts?.Dispose();
            _commandCts = new CancellationTokenSource();

            // Vars作成
            var variables = new VarStore();
            if (createHoldVariables)
            {
                if (VarIdResolver.TryResolve(VarKeyHoldTime, out var varId) && varId != 0)
                    variables.TrySetVariant(varId, DynamicVariant.FromFloat(_holdTime));
                if (VarIdResolver.TryResolve(VarKeyHoldProgress, out varId) && varId != 0)
                    variables.TrySetVariant(varId, DynamicVariant.FromFloat(forcedHoldProgress ?? HoldProgress));
            }

            var options = VNext.CommandRunOptions.Default;
            var ctx = new VNext.CommandContext(_owner, variables, _commandRunner, _owner, options);

            try
            {
                var result = await _commandRunner.ExecuteListAsync(commands, ctx, _commandCts.Token, options);
                if (result.Status == VNext.CommandRunStatus.Error)
                {
                    Debug.LogError($"[UIButtonService] Command execution failed: {result.Message}");
                }
            }
            catch (OperationCanceledException)
            {
                // キャンセルは正常終了
            }
        }

        /// <summary>
        /// OnHoldDecision用の実行ラッパー。
        /// GuardDuringCommandExecutionが有効な場合、実行中は入力をブロックし、
        /// 必要に応じて選択不可にする。
        /// </summary>
        async UniTaskVoid ExecuteHoldDecisionCommands(VNext.CommandListData commands)
        {
            _isHoldDecisionExecuting = true;

            // GuardDuringCommandExecutionが有効な場合、UIElementを一時的に選択不可にする
            IUIElementStateController? stateController = null;
            if (_guardDuringCommandExecution &&
                _disableSelectionDuringCommandExecution &&
                _elementState is IUIElementStateController controller)
            {
                stateController = controller;
                stateController.SetActive(false);
            }

            try
            {
                await ExecuteCommandsAsync(commands, createHoldVariables: true, forcedHoldProgress: 1f);
            }
            finally
            {
                // 復元
                if (stateController != null)
                {
                    stateController.SetActive(true);
                }

                _isHoldDecisionExecuting = false;
            }
        }

        /// <summary>
        /// OnSubmitUp用の実行ラッパー。
        /// GuardDuringCommandExecutionが有効な場合、実行中は入力をブロックし、
        /// 必要に応じて選択不可にする。
        /// </summary>
        async UniTaskVoid ExecuteSubmitUpCommands(VNext.CommandListData commands)
        {
            _isSubmitUpExecuting = true;

            IUIElementStateController? stateController = null;
            if (_guardDuringCommandExecution &&
                _disableSelectionDuringCommandExecution &&
                _elementState is IUIElementStateController controller)
            {
                stateController = controller;
                stateController.SetActive(false);
            }

            try
            {
                await ExecuteCommandsAsync(commands, createHoldVariables: false, forcedHoldProgress: null);
            }
            finally
            {
                if (stateController != null)
                {
                    stateController.SetActive(true);
                }

                _isSubmitUpExecuting = false;
            }
        }
    }
}
