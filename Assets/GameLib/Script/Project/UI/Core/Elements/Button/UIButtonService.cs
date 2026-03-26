#nullable enable
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using VNext = Game.Commands.VNext;
using Game.Common;
using Game.Vars.Generated;
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
        Instant = 10,

        /// <summary>長押しが必要なボタン</summary>
        Hold = 20,

        /// <summary>短押しと長押しを分岐するボタン</summary>
        ShortLong = 30,
    }

    public enum UIButtonShortLongPhase
    {
        Idle = 0,
        Short = 10,
        Long = 20,
        LongMax = 30,
        CompletedWaitingRelease = 40,
    }

    public interface IUIButtonService
    {
        UIButtonKind Kind { get; set; }
        bool CanSubmit { get; set; }
        DynamicValue<bool> InputControlCondition { get; set; }
        float HoldTime { get; set; }
        float LongMaxTime { get; set; }
        bool AutoDecideOnLongMax { get; set; }
        float HoldInterval { get; set; }
        UIInputAction TriggerAction { get; set; }

        VNext.CommandListData OnSubmitDownCommands { get; }
        VNext.CommandListData OnSubmitUpCommands { get; }
        VNext.CommandListData OnHoldDecisionCommands { get; }
        VNext.CommandListData OnHoldIntervalCommands { get; }
        VNext.CommandListData OnHoldCancelCommands { get; }
        VNext.CommandListData OnGenericStartCommands { get; }
        VNext.CommandListData OnShortStartCommands { get; }
        VNext.CommandListData OnLongStartCommands { get; }
        VNext.CommandListData OnGenericDecisionCommands { get; }
        VNext.CommandListData OnShortDecisionCommands { get; }
        VNext.CommandListData OnLongDecisionCommands { get; }
        VNext.CommandListData OnLongMaxDecisionCommands { get; }
        VNext.CommandListData OnCancelCommands { get; }

        bool IsHoldDecisionExecuting { get; }
        bool IsSubmitUpExecuting { get; }
        bool IsHolding { get; }
        float HoldProgress { get; }
        UIButtonShortLongPhase CurrentPhase { get; }
        float ShortProgress { get; }
        float LongProgress { get; }
        bool IsLongMax { get; }

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

        public const string VarKeyShortLongState = "UIButton.ShortLong.State";
        public const string VarKeyShortLongShortProgress = "UIButton.ShortLong.ShortProgress";
        public const string VarKeyShortLongLongProgress = "UIButton.ShortLong.LongProgress";
        public const string VarKeyShortLongIsLong = "UIButton.ShortLong.IsLong";
        public const string VarKeyShortLongIsLongMax = "UIButton.ShortLong.IsLongMax";
        public const string VarKeyShortLongLongMaxTime = "UIButton.ShortLong.LongMaxTime";

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

        /// <summary>Long開始後にLongMaxへ達するまでの追加時間（秒）</summary>
        float _longMaxTime = 1.0f;

        bool _autoDecideOnLongMax;

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

        readonly VNext.CommandListData _onGenericStartCommands = new();
        readonly VNext.CommandListData _onShortStartCommands = new();
        readonly VNext.CommandListData _onLongStartCommands = new();
        readonly VNext.CommandListData _onGenericDecisionCommands = new();
        readonly VNext.CommandListData _onShortDecisionCommands = new();
        readonly VNext.CommandListData _onLongDecisionCommands = new();
        readonly VNext.CommandListData _onLongMaxDecisionCommands = new();
        readonly VNext.CommandListData _onCancelCommands = new();

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

        UIButtonShortLongPhase _currentPhase = UIButtonShortLongPhase.Idle;
        float _shortElapsed;
        float _longElapsed;

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

        /// <summary>サービス寿命に紐づくコマンド用CancellationTokenSource</summary>
        CancellationTokenSource? _serviceCommandCts;

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

        public float LongMaxTime
        {
            get => _longMaxTime;
            set => _longMaxTime = Mathf.Max(0.01f, value);
        }

        public bool AutoDecideOnLongMax
        {
            get => _autoDecideOnLongMax;
            set => _autoDecideOnLongMax = value;
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

        public VNext.CommandListData OnGenericStartCommands => _onGenericStartCommands;

        public VNext.CommandListData OnShortStartCommands => _onShortStartCommands;

        public VNext.CommandListData OnLongStartCommands => _onLongStartCommands;

        public VNext.CommandListData OnGenericDecisionCommands => _onGenericDecisionCommands;

        public VNext.CommandListData OnShortDecisionCommands => _onShortDecisionCommands;

        public VNext.CommandListData OnLongDecisionCommands => _onLongDecisionCommands;

        public VNext.CommandListData OnLongMaxDecisionCommands => _onLongMaxDecisionCommands;

        public VNext.CommandListData OnCancelCommands => _onCancelCommands;

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
        public float HoldProgress => _kind == UIButtonKind.ShortLong ? ShortProgress : (_holdTime > 0 ? Mathf.Clamp01(_holdElapsed / _holdTime) : 0f);

        public UIButtonShortLongPhase CurrentPhase => _currentPhase;

        public float ShortProgress => _holdTime > 0f ? Mathf.Clamp01(_shortElapsed / _holdTime) : 0f;

        public float LongProgress => _longMaxTime > 0f ? Mathf.Clamp01(_longElapsed / _longMaxTime) : 0f;

        public bool IsLongMax => _currentPhase == UIButtonShortLongPhase.LongMax || _currentPhase == UIButtonShortLongPhase.CompletedWaitingRelease;

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
            _longMaxTime = Mathf.Max(0.01f, options.LongMaxTime);
            _autoDecideOnLongMax = options.AutoDecideOnLongMax;
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
            _onGenericStartCommands.SetCommands(new System.Collections.Generic.List<VNext.ICommandSource>(options.OnGenericStartCommands.Commands));
            _onShortStartCommands.SetCommands(new System.Collections.Generic.List<VNext.ICommandSource>(options.OnShortStartCommands.Commands));
            _onLongStartCommands.SetCommands(new System.Collections.Generic.List<VNext.ICommandSource>(options.OnLongStartCommands.Commands));
            _onGenericDecisionCommands.SetCommands(new System.Collections.Generic.List<VNext.ICommandSource>(options.OnGenericDecisionCommands.Commands));
            _onShortDecisionCommands.SetCommands(new System.Collections.Generic.List<VNext.ICommandSource>(options.OnShortDecisionCommands.Commands));
            _onLongDecisionCommands.SetCommands(new System.Collections.Generic.List<VNext.ICommandSource>(options.OnLongDecisionCommands.Commands));
            _onLongMaxDecisionCommands.SetCommands(new System.Collections.Generic.List<VNext.ICommandSource>(options.OnLongMaxDecisionCommands.Commands));
            _onCancelCommands.SetCommands(new System.Collections.Generic.List<VNext.ICommandSource>(options.OnCancelCommands.Commands));

            // events removed
            PublishTelemetry();
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _serviceCommandCts?.Cancel();
            _serviceCommandCts?.Dispose();
            _serviceCommandCts = new CancellationTokenSource();
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
            if (_kind == UIButtonKind.Hold && _isHolding)
                CancelHold();
            else if (_kind == UIButtonKind.ShortLong && IsShortLongActive())
                CancelShortLong();

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

            _serviceCommandCts?.Cancel();
            _serviceCommandCts?.Dispose();
            _serviceCommandCts = null;

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
                        if (_kind == UIButtonKind.Hold)
                            CancelHold();
                        else if (_kind == UIButtonKind.ShortLong)
                            CancelShortLong();
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
                            if (_kind == UIButtonKind.Hold)
                                CancelHold();
                            else if (_kind == UIButtonKind.ShortLong)
                                CancelShortLong();
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
                currentPhase: _currentPhase,
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
                shortProgress: ShortProgress,
                longProgress: LongProgress,
                isLongMax: IsLongMax,
                longMaxTime: _longMaxTime,
                autoDecideOnLongMax: _autoDecideOnLongMax,
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
            var pointerPosition = e.PointerPosition;
            switch (_kind)
            {
                case UIButtonKind.Instant:
                    ExecuteCommands(_onSubmitDownCommands, UIButtonCommandVariableMode.None).Forget();
                    return true;

                case UIButtonKind.Hold:
                    ExecuteCommands(_onSubmitDownCommands, UIButtonCommandVariableMode.Hold).Forget();
                    _isHolding = true;
                    _holdStartPosition = pointerPosition;
                    _holdElapsed = 0f;
                    _shortElapsed = 0f;
                    _longElapsed = 0f;
                    _currentPhase = UIButtonShortLongPhase.Idle;
                    StartHoldIntervalLoop();
                    AcquireSelectionBlockIfNeeded();
                    return true;

                case UIButtonKind.ShortLong:
                    BeginShortLongPress(pointerPosition);
                    ExecuteShortLongStartCommands(CaptureShortLongSnapshot()).Forget();
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// SubmitHeld処理（長押し中）。
        /// </summary>
        bool HandleTriggerHeld(in UIInputEvent e)
        {
            switch (_kind)
            {
                case UIButtonKind.Hold:
                    if (!_isHolding)
                        return false;

                    _holdElapsed += e.DeltaTime;
                    if (_holdElapsed >= _holdTime)
                    {
                        _holdElapsed = _holdTime;
                        _isHolding = false;
                        StopHoldIntervalLoop();
                        ReleaseSelectionBlock();
                        var decisionCommands = _onHoldDecisionCommands.Count > 0 ? _onHoldDecisionCommands : _onSubmitUpCommands;
                        ExecuteHoldDecisionCommands(decisionCommands).Forget();
                    }
                    return true;

                case UIButtonKind.ShortLong:
                    return HandleShortLongHeld(e.DeltaTime);

                default:
                    return false;
            }
        }

        /// <summary>
        /// SubmitUp処理。
        /// </summary>
        bool HandleTriggerUp(in UIInputEvent e)
        {
            switch (_kind)
            {
                case UIButtonKind.Hold:
                    if (_isHolding)
                        CancelHold();
                    return true;

                case UIButtonKind.ShortLong:
                    return HandleShortLongUp();

                case UIButtonKind.Instant:
                    ExecuteSubmitUpCommands(_onSubmitUpCommands).Forget();
                    return true;

                default:
                    return false;
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
            ReleaseSelectionBlock();
            ExecuteCommands(_onHoldCancelCommands, UIButtonCommandVariableMode.Hold, forcedHoldProgress: progress).Forget();
        }

        void CancelShortLong()
        {
            if (!IsShortLongActive())
                return;

            var snapshot = CaptureShortLongSnapshot();
            _isHolding = false;
            _holdElapsed = 0f;
            ReleaseSelectionBlock();
            ResetShortLongPhase(UIButtonShortLongPhase.Idle);
            ExecuteCommands(_onCancelCommands, UIButtonCommandVariableMode.ShortLong, shortLongSnapshot: snapshot).Forget();
        }

        /// <summary>
        /// 選択変更ハンドラ。
        /// 選択が外れたらHoldをキャンセル。
        /// </summary>
        void HandleSelectionChanged(IScopeNode? newSelection)
        {
            if (ReferenceEquals(newSelection, _owner))
                return;

            if (_kind == UIButtonKind.Hold && _isHolding)
                CancelHold();
            else if (_kind == UIButtonKind.ShortLong && IsShortLongActive())
                CancelShortLong();
        }

        void BeginShortLongPress(Vector2 pointerPosition)
        {
            _isHolding = true;
            _holdStartPosition = pointerPosition;
            _holdElapsed = 0f;
            _shortElapsed = 0f;
            _longElapsed = 0f;
            ResetShortLongPhase(UIButtonShortLongPhase.Short);
            AcquireSelectionBlockIfNeeded();
        }

        bool HandleShortLongHeld(float deltaTime)
        {
            if (_currentPhase == UIButtonShortLongPhase.CompletedWaitingRelease)
                return true;
            if (!IsShortLongActive())
                return false;

            if (_currentPhase == UIButtonShortLongPhase.Short)
            {
                _shortElapsed += deltaTime;
                _holdElapsed = _shortElapsed;
                if (_shortElapsed >= _holdTime)
                {
                    var overflow = _shortElapsed - _holdTime;
                    _shortElapsed = _holdTime;
                    ResetShortLongPhase(UIButtonShortLongPhase.Long);
                    _longElapsed = Mathf.Max(0f, overflow);
                    ExecuteCommands(_onLongStartCommands, UIButtonCommandVariableMode.ShortLong, shortLongSnapshot: CaptureShortLongSnapshot()).Forget();
                }
            }
            else if (_currentPhase == UIButtonShortLongPhase.Long || _currentPhase == UIButtonShortLongPhase.LongMax)
            {
                _longElapsed += deltaTime;
            }

            if (_currentPhase == UIButtonShortLongPhase.Long && _longElapsed >= _longMaxTime)
            {
                _longElapsed = _longMaxTime;
                ResetShortLongPhase(UIButtonShortLongPhase.LongMax);
                if (_autoDecideOnLongMax)
                    AutoDecideShortLongAtLongMax();
            }
            else if (_currentPhase == UIButtonShortLongPhase.LongMax && _longElapsed > _longMaxTime)
            {
                _longElapsed = _longMaxTime;
            }

            return true;
        }

        bool HandleShortLongUp()
        {
            if (_currentPhase == UIButtonShortLongPhase.CompletedWaitingRelease)
            {
                ResetShortLongPhase(UIButtonShortLongPhase.Idle);
                _shortElapsed = 0f;
                _longElapsed = 0f;
                return true;
            }

            if (!IsShortLongActive())
                return false;

            var decisionPhase = _currentPhase == UIButtonShortLongPhase.LongMax
                ? UIButtonShortLongPhase.LongMax
                : _currentPhase == UIButtonShortLongPhase.Long
                    ? UIButtonShortLongPhase.Long
                    : UIButtonShortLongPhase.Short;

            CompleteShortLongDecision(decisionPhase);
            return true;
        }

        void CompleteShortLongDecision(UIButtonShortLongPhase decisionPhase)
        {
            var snapshot = CaptureShortLongSnapshot(decisionPhase);
            _isHolding = false;
            _holdElapsed = 0f;
            ReleaseSelectionBlock();
            ResetShortLongPhase(UIButtonShortLongPhase.Idle);
            _shortElapsed = 0f;
            _longElapsed = 0f;

            var specificCommands = decisionPhase switch
            {
                UIButtonShortLongPhase.Short => _onShortDecisionCommands,
                UIButtonShortLongPhase.LongMax => _onLongMaxDecisionCommands,
                _ => _onLongDecisionCommands,
            };

            ExecuteShortLongDecisionCommands(specificCommands, snapshot).Forget();
        }

        void AutoDecideShortLongAtLongMax()
        {
            var snapshot = CaptureShortLongSnapshot(UIButtonShortLongPhase.LongMax);
            _isHolding = false;
            _holdElapsed = 0f;
            ReleaseSelectionBlock();
            ResetShortLongPhase(UIButtonShortLongPhase.CompletedWaitingRelease);
            _shortElapsed = _holdTime;
            _longElapsed = _longMaxTime;
            ExecuteShortLongDecisionCommands(_onLongMaxDecisionCommands, snapshot).Forget();
        }

        bool IsShortLongActive()
        {
            return _currentPhase == UIButtonShortLongPhase.Short ||
                _currentPhase == UIButtonShortLongPhase.Long ||
                _currentPhase == UIButtonShortLongPhase.LongMax;
        }

        void ResetShortLongPhase(UIButtonShortLongPhase phase)
        {
            _currentPhase = phase;
        }

        void AcquireSelectionBlockIfNeeded()
        {
            if (!_guardSelectionWhileHolding || _selectionBlockService == null)
                return;

            try
            {
                _selectionBlockHandle = _selectionBlockService.AcquireBlock(this, UISelectionBlockMask.Navigation | UISelectionBlockMask.Pointer);
            }
            catch
            {
                _selectionBlockHandle = null;
            }
        }

        void ReleaseSelectionBlock()
        {
            try { _selectionBlockHandle?.Dispose(); } catch { }
            _selectionBlockHandle = null;
        }

        enum UIButtonCommandVariableMode
        {
            None = 0,
            Hold = 10,
            ShortLong = 20,
        }

        readonly struct UIButtonShortLongCommandSnapshot
        {
            public readonly UIButtonShortLongPhase Phase;
            public readonly float ShortProgress;
            public readonly float LongProgress;
            public readonly bool IsLong;
            public readonly bool IsLongMax;
            public readonly float LongMaxTime;

            public UIButtonShortLongCommandSnapshot(
                UIButtonShortLongPhase phase,
                float shortProgress,
                float longProgress,
                bool isLong,
                bool isLongMax,
                float longMaxTime)
            {
                Phase = phase;
                ShortProgress = shortProgress;
                LongProgress = longProgress;
                IsLong = isLong;
                IsLongMax = isLongMax;
                LongMaxTime = longMaxTime;
            }
        }

        UIButtonShortLongCommandSnapshot CaptureShortLongSnapshot(UIButtonShortLongPhase? forcedPhase = null)
        {
            var phase = forcedPhase ?? _currentPhase;
            var shortProgress = _holdTime > 0f ? Mathf.Clamp01(_shortElapsed / _holdTime) : 0f;
            var longProgress = _longMaxTime > 0f ? Mathf.Clamp01(_longElapsed / _longMaxTime) : 0f;
            var isLong = phase == UIButtonShortLongPhase.Long || phase == UIButtonShortLongPhase.LongMax;
            var isLongMax = phase == UIButtonShortLongPhase.LongMax;
            return new UIButtonShortLongCommandSnapshot(
                phase,
                shortProgress,
                longProgress,
                isLong,
                isLongMax,
                _longMaxTime);
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
                variables.TrySetVariant(VarIds.GameLib.UI.Button.HoldTime, DynamicVariant.FromFloat(_holdTime));
                variables.TrySetVariant(VarIds.GameLib.UI.Button.HoldProgress, DynamicVariant.FromFloat(HoldProgress));

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
        async UniTaskVoid ExecuteCommands(
            VNext.CommandListData commands,
            UIButtonCommandVariableMode variableMode,
            float? forcedHoldProgress = null,
            UIButtonShortLongCommandSnapshot? shortLongSnapshot = null)
        {
            await ExecuteCommandsAsync(commands, variableMode, forcedHoldProgress, shortLongSnapshot);
        }

        async UniTask ExecuteCommandsAsync(
            VNext.CommandListData commands,
            UIButtonCommandVariableMode variableMode,
            float? forcedHoldProgress = null,
            UIButtonShortLongCommandSnapshot? shortLongSnapshot = null)
        {
            if (_commandRunner == null) return;
            if (commands.Count == 0) return;

            // Vars作成
            var variables = BuildCommandVariables(variableMode, forcedHoldProgress, shortLongSnapshot);

            var options = VNext.CommandRunOptions.Default;
            var ctx = new VNext.CommandContext(_owner, variables, _commandRunner, _owner, options);
            var ct = _serviceCommandCts?.Token ?? CancellationToken.None;

            try
            {
                var result = await _commandRunner.ExecuteListAsync(commands, ctx, ct, options);
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

        VarStore BuildCommandVariables(
            UIButtonCommandVariableMode variableMode,
            float? forcedHoldProgress,
            UIButtonShortLongCommandSnapshot? shortLongSnapshot)
        {
            var variables = new VarStore();
            switch (variableMode)
            {
                case UIButtonCommandVariableMode.Hold:
                    variables.TrySetVariant(VarIds.GameLib.UI.Button.HoldTime, DynamicVariant.FromFloat(_holdTime));
                    variables.TrySetVariant(VarIds.GameLib.UI.Button.HoldProgress, DynamicVariant.FromFloat(forcedHoldProgress ?? HoldProgress));
                    break;

                case UIButtonCommandVariableMode.ShortLong:
                    var snapshot = shortLongSnapshot ?? CaptureShortLongSnapshot();
                    variables.TrySetVariant(VarIds.GameLib.UI.Button.HoldTime, DynamicVariant.FromFloat(_holdTime));
                    variables.TrySetVariant(VarIds.GameLib.UI.Button.HoldProgress, DynamicVariant.FromFloat(snapshot.ShortProgress));
                    variables.TrySetVariant(VarIds.GameLib.UI.Button.ShortLong.State, DynamicVariant.FromInt((int)snapshot.Phase));
                    variables.TrySetVariant(VarIds.GameLib.UI.Button.ShortLong.ShortProgress, DynamicVariant.FromFloat(snapshot.ShortProgress));
                    variables.TrySetVariant(VarIds.GameLib.UI.Button.ShortLong.LongProgress, DynamicVariant.FromFloat(snapshot.LongProgress));
                    variables.TrySetVariant(VarIds.GameLib.UI.Button.ShortLong.IsLong, DynamicVariant.FromBool(snapshot.IsLong));
                    variables.TrySetVariant(VarIds.GameLib.UI.Button.ShortLong.IsLongMax, DynamicVariant.FromBool(snapshot.IsLongMax));
                    variables.TrySetVariant(VarIds.GameLib.UI.Button.ShortLong.LongMaxTime, DynamicVariant.FromFloat(snapshot.LongMaxTime));
                    break;
            }

            return variables;
        }

        async UniTaskVoid ExecuteShortLongStartCommands(UIButtonShortLongCommandSnapshot snapshot)
        {
            await ExecuteCommandsAsync(_onGenericStartCommands, UIButtonCommandVariableMode.ShortLong, shortLongSnapshot: snapshot);
            await ExecuteCommandsAsync(_onShortStartCommands, UIButtonCommandVariableMode.ShortLong, shortLongSnapshot: snapshot);
        }

        async UniTaskVoid ExecuteShortLongDecisionCommands(
            VNext.CommandListData specificCommands,
            UIButtonShortLongCommandSnapshot snapshot)
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
                await ExecuteCommandsAsync(_onGenericDecisionCommands, UIButtonCommandVariableMode.ShortLong, shortLongSnapshot: snapshot);
                await ExecuteCommandsAsync(specificCommands, UIButtonCommandVariableMode.ShortLong, shortLongSnapshot: snapshot);
            }
            finally
            {
                if (stateController != null)
                    stateController.SetActive(true);

                _isSubmitUpExecuting = false;
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
                await ExecuteCommandsAsync(commands, UIButtonCommandVariableMode.Hold, forcedHoldProgress: 1f);
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
                await ExecuteCommandsAsync(commands, UIButtonCommandVariableMode.None);
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
