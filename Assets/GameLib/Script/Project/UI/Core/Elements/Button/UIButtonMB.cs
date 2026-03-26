#nullable enable
using UnityEngine;
using VContainer;
using System.Collections.Generic;
using VNext = Game.Commands.VNext;
using Sirenix.OdinInspector;
using Game.Common;

namespace Game.UI
{
    // ================================================================
    // UIButtonMB - UIElementにボタン機能を追加するMonoBehaviour
    // ================================================================
    //
    // ## 概要
    //
    // UIButtonMBは、UIElementにボタン機能を提供するMonoBehaviour。
    // IFeatureInstallerを実装し、UIButtonServiceをDIコンテナに登録する。
    //
    // ## ボタンの種類
    //
    // ### Instant
    // 即座に反応するボタン。Submit押下/解放でコマンドを実行。
    //
    // ### Hold
    // 長押しが必要なボタン。指定時間押し続けると成功。
    // 途中でキャンセル条件が発生するとキャンセルコマンドを実行。
    //
    // ## キャンセル条件（Holdボタン）
    //
    // - マウスがDown中に移動した（閾値以上）
    // - ナビゲーション入力が発生した
    // - 選択が外れた
    // - SubmitUpが早すぎた（Hold時間未達）
    //
    // ================================================================

    public interface IUIButtonOptions
    {
        UIButtonKind Kind { get; }
        bool CanSubmit { get; }
        float HoldTime { get; }
        float LongMaxTime { get; }
        bool AutoDecideOnLongMax { get; }

        float HoldInterval { get; }

        UIInputAction TriggerAction { get; }

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

        // Guard: when true, selection/navigation/pointer changes are blocked while holding
        bool GuardSelectionWhileHolding { get; }

        // Guard: when true, block new input while command execution is running
        bool GuardDuringCommandExecution { get; }

        // When guarding, optionally disable selection while commands execute
        bool DisableSelectionDuringCommandExecution { get; }

        // Input Control (enabled by DynamicValue<bool>)
        Game.Common.DynamicValue<bool> InputControlCondition { get; }
    }

    /// <summary>
    /// UIElementにボタン機能を追加するMonoBehaviour。
    /// </summary>
    public sealed class UIButtonMB : MonoBehaviour, IFeatureInstaller, IUIButtonOptions
    {
        // ================================================================
        // Inspector設定 - 基本
        // ================================================================

        [Header("基本設定")]
        [Tooltip("ボタンの種類。\n" +
                 "Instant: 即座に反応\n" +
                 "Hold: 長押しが必要\n" +
                 "ShortLong: 短押しと長押しを分ける")]
        [SerializeField]
        UIButtonKind _kind = UIButtonKind.Instant;

        [Tooltip("Submit入力を受け付けるかどうか。\n" +
                 "falseの場合、ボタンとして機能しない。")]
        [SerializeField]
        bool _canSubmit = true;

        [Tooltip("このボタンが反応する入力アクション。\n" +
             "デフォルトは Submit。\n" +
             "Cancel/Attack/Interact 等にも切り替え可能。")]
        [SerializeField]
        UIInputAction _triggerAction = UIInputAction.Submit;

        // ================================================================
        // Inspector設定 - Hold専用
        // ================================================================

        [Header("時間設定")]
        [Tooltip("Hold では成功までの時間。\nShortLong では Short から Long に切り替わるまでの時間。")]
        [ShowIf(nameof(UsesTimedPressSettings))]
        [SerializeField]
        float _holdTime = 1.0f;

        [ShowIf(nameof(IsHoldMode))]
        [Tooltip("Hold中に一定間隔で実行するコマンドの間隔（秒）。\nデフォルト: 0.1")]
        [SerializeField]
        float _holdInterval = 0.1f;

        [ShowIf(nameof(IsShortLongMode))]
        [Tooltip("Long に入ってから LongMax に到達するまでの追加時間。")]
        [SerializeField]
        float _longMaxTime = 1.0f;

        [ShowIf(nameof(IsShortLongMode))]
        [Tooltip("LongMax に到達した瞬間に自動で決定するか。")]
        [SerializeField]
        bool _autoDecideOnLongMax;

        [ShowIf(nameof(UsesTimedPressSettings))]
        [Tooltip("入力継続中は他のUIへの選択移動をブロックします。Hold は hold 中、ShortLong は press 全体に適用します。")]
        [SerializeField]
        bool _guardSelectionWhileHolding = true;

        // ================================================================
        // Inspector設定 - コマンド
        // ================================================================

        [Header("コマンド")]
        [Tooltip("Submit 押下時に実行するコマンド。\nInstant/Hold で使用。")]
        [SerializeField]
        [ShowIf(nameof(UsesLegacyStartCommands))]
        [VNext.CommandListFunctionName("UIButton.OnSubmitDown")]
        VNext.CommandListData _onSubmitDownCommands = new();

        [Tooltip("Submit 解放時に実行するコマンド。\nInstant で使用し、Hold では互換用 fallback としてのみ使います。")]
        [SerializeField]
        [ShowIf(nameof(UsesLegacyDecisionCommands))]
        [VNext.CommandListFunctionName("UIButton.OnSubmitUp")]
        VNext.CommandListData _onSubmitUpCommands = new();

        [Tooltip("Hold時間達成時（Decision）に実行するコマンド。\n" +
             "空の場合は互換のため OnSubmitUpCommands を使用する。")]
        [ShowIf(nameof(IsHoldMode))]
        [SerializeField]
        [VNext.CommandListFunctionName("UIButton.OnHoldDecision")]
        VNext.CommandListData _onHoldDecisionCommands = new();

        [Tooltip("コマンド実行中は新しい入力を受け付けないようにします。\n" +
                 "HoldはOnHoldDecision、InstantはOnSubmitUpの実行中に適用されます。")]
        [SerializeField]
        bool _guardDuringCommandExecution = true;

        [Tooltip("コマンド実行中に選択状態を解除します。\n" +
                 "GuardDuringCommandExecutionが有効なときのみ適用されます。")]
        [SerializeField, ShowIf(nameof(_guardDuringCommandExecution))]
        bool _disableSelectionDuringCommandExecution = false;

        [Tooltip("Hold中に一定間隔で実行するコマンド。\n" +
            "Holdボタンでのみ使用。")]
        [ShowIf(nameof(IsHoldMode))]
        [SerializeField]
        [VNext.CommandListFunctionName("UIButton.OnHoldInterval")]
        VNext.CommandListData _onHoldIntervalCommands = new();

        [Tooltip("Hold中キャンセル時に実行するコマンド。\n" +
                 "Holdボタンでのみ使用。")]
        [ShowIf(nameof(IsHoldMode))]
        [SerializeField]
        [VNext.CommandListFunctionName("UIButton.OnHoldCancel")]
        VNext.CommandListData _onHoldCancelCommands = new();

        [Tooltip("入力開始時に必ず実行する共通コマンド。ShortLong 専用。")]
        [ShowIf(nameof(IsShortLongMode))]
        [SerializeField]
        [VNext.CommandListFunctionName("UIButton.OnGenericStart")]
        VNext.CommandListData _onGenericStartCommands = new();

        [Tooltip("Short 入力開始時に実行するコマンド。ShortLong 専用。")]
        [ShowIf(nameof(IsShortLongMode))]
        [SerializeField]
        [VNext.CommandListFunctionName("UIButton.OnShortStart")]
        VNext.CommandListData _onShortStartCommands = new();

        [Tooltip("Short を超えて Long に入った瞬間に 1 回だけ実行するコマンド。ShortLong 専用。")]
        [ShowIf(nameof(IsShortLongMode))]
        [SerializeField]
        [VNext.CommandListFunctionName("UIButton.OnLongStart")]
        VNext.CommandListData _onLongStartCommands = new();

        [Tooltip("最終決定時に必ず実行する共通コマンド。ShortLong 専用。")]
        [ShowIf(nameof(IsShortLongMode))]
        [SerializeField]
        [VNext.CommandListFunctionName("UIButton.OnGenericDecision")]
        VNext.CommandListData _onGenericDecisionCommands = new();

        [Tooltip("Short のまま離したときに実行するコマンド。ShortLong 専用。")]
        [ShowIf(nameof(IsShortLongMode))]
        [SerializeField]
        [VNext.CommandListFunctionName("UIButton.OnShortDecision")]
        VNext.CommandListData _onShortDecisionCommands = new();

        [Tooltip("Long 状態で離したときに実行するコマンド。LongMax では使いません。ShortLong 専用。")]
        [ShowIf(nameof(IsShortLongMode))]
        [SerializeField]
        [VNext.CommandListFunctionName("UIButton.OnLongDecision")]
        VNext.CommandListData _onLongDecisionCommands = new();

        [Tooltip("LongMax 状態で決定したときにだけ実行するコマンド。ShortLong 専用。")]
        [ShowIf(nameof(IsShortLongMode))]
        [SerializeField]
        [VNext.CommandListFunctionName("UIButton.OnLongMaxDecision")]
        VNext.CommandListData _onLongMaxDecisionCommands = new();

        [Tooltip("入力途中で中断されたときに実行するコマンド。短押し release は cancel ではありません。ShortLong 専用。")]
        [ShowIf(nameof(IsShortLongMode))]
        [SerializeField]
        [VNext.CommandListFunctionName("UIButton.OnCancel")]
        VNext.CommandListData _onCancelCommands = new();

        // ================================================================
        // Inspector設定 - Input Control
        // ================================================================

        [Header("Input Control")]
        [Tooltip("このボタンの入力受け付けを制御するDynamicValue条件。\n" +
                 "true の場合のみ入力を受け付けます。\n" +
                 "Blackboard, Scalar, VarStore, Expression など複数の値源をサポートします。")]
        [SerializeField]
        [DynamicValueDefaultLiteral(true)]
        DynamicValue<bool> _inputControlCondition = DynamicValueExtensions.FromLiteral(true);

        // ================================================================
        // キャッシュ
        // ================================================================

        /// <summary>登録されたService</summary>
        IUIButtonService? _service;

        // ================================================================
        // プロパティ
        // ================================================================

        /// <summary>
        /// ボタンの種類。
        /// </summary>
        public UIButtonKind Kind => _kind;

        /// <summary>
        /// Submit可能かどうか。
        /// </summary>
        public bool CanSubmit => _canSubmit;

        /// <summary>
        /// 長押し必要時間。
        /// </summary>
        public float HoldTime => _holdTime;

        public float LongMaxTime => _longMaxTime;

        public bool AutoDecideOnLongMax => _autoDecideOnLongMax;

        public float HoldInterval => _holdInterval;

        public UIInputAction TriggerAction => _triggerAction;

        /// <summary>
        /// SubmitDownコマンドリスト。
        /// </summary>
        public VNext.CommandListData OnSubmitDownCommands => _onSubmitDownCommands;

        /// <summary>
        /// SubmitUp/Hold成功コマンドリスト。
        /// </summary>
        public VNext.CommandListData OnSubmitUpCommands => _onSubmitUpCommands;

        public VNext.CommandListData OnHoldDecisionCommands => _onHoldDecisionCommands;

        public VNext.CommandListData OnHoldIntervalCommands => _onHoldIntervalCommands;

        /// <summary>
        /// Holdキャンセルコマンドリスト。
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
        // Guard
        [ShowIf(nameof(UsesTimedPressSettings))]
        public bool GuardSelectionWhileHolding => _guardSelectionWhileHolding;

        public bool GuardDuringCommandExecution => _guardDuringCommandExecution;

        public bool DisableSelectionDuringCommandExecution => _disableSelectionDuringCommandExecution;

        // ================================================================
        // Input Control Properties
        // ================================================================

        /// <summary>
        /// 入力制御条件。true の場合のみ入力を受け付けます。
        /// </summary>
        public DynamicValue<bool> InputControlCondition => _inputControlCondition;

        bool UsesTimedPressSettings() => _kind == UIButtonKind.Hold || _kind == UIButtonKind.ShortLong;
        bool IsHoldMode() => _kind == UIButtonKind.Hold;
        bool IsShortLongMode() => _kind == UIButtonKind.ShortLong;
        bool UsesLegacyStartCommands() => _kind == UIButtonKind.Instant || _kind == UIButtonKind.Hold;
        bool UsesLegacyDecisionCommands() => _kind == UIButtonKind.Instant || _kind == UIButtonKind.Hold;

        // ================================================================
        // Debug Info (Runtime Only)
        // ================================================================

        [FoldoutGroup("Telemetry")]
        [SerializeField, ShowInInspector, ReadOnly]
        InspectorTelemetryState _inspectorTelemetry = new InspectorTelemetryState();

        [System.Serializable]
        public sealed class InspectorTelemetryState
        {
            [ShowInInspector, ReadOnly]
            public string OwnerName = "(none)";

            [ShowInInspector, ReadOnly]
            public UIButtonKind Kind = UIButtonKind.Instant;

            [ShowInInspector, ReadOnly]
            public UIButtonShortLongPhase CurrentPhase = UIButtonShortLongPhase.Idle;

            [ShowInInspector, ReadOnly]
            public UIInputAction TriggerAction = UIInputAction.Submit;

            [ShowInInspector, ReadOnly]
            public bool CanSubmit = true;

            [ShowInInspector, ReadOnly]
            public bool IsSelected;

            [ShowInInspector, ReadOnly]
            public bool IsVisible;

            [ShowInInspector, ReadOnly]
            public bool IsEffectivelyActive;

            [ShowInInspector, ReadOnly]
            public bool GuardSelectionWhileHolding;

            [ShowInInspector, ReadOnly]
            public bool GuardDuringCommandExecution;

            [ShowInInspector, ReadOnly]
            public bool DisableSelectionDuringCommandExecution;

            [ShowInInspector, ReadOnly]
            public bool IsHolding;

            [ShowInInspector, ReadOnly]
            public float HoldProgress;

            [ShowInInspector, ReadOnly]
            public float ShortProgress;

            [ShowInInspector, ReadOnly]
            public float LongProgress;

            [ShowInInspector, ReadOnly]
            public bool IsLongMax;

            [ShowInInspector, ReadOnly]
            public float LongMaxTime;

            [ShowInInspector, ReadOnly]
            public bool AutoDecideOnLongMax;

            [ShowInInspector, ReadOnly]
            public bool IsHoldDecisionExecuting;

            [ShowInInspector, ReadOnly]
            public bool IsSubmitUpExecuting;

            [ShowInInspector, ReadOnly]
            public UIInputEventType LastInputEventType = UIInputEventType.None;

            [ShowInInspector, ReadOnly]
            public UIInputPhase LastInputPhase = UIInputPhase.Down;

            [ShowInInspector, ReadOnly]
            public bool LastInputMatched;

            [ShowInInspector, ReadOnly]
            public bool LastInputAccepted;

            [ShowInInspector, ReadOnly]
            public UIButtonInputRejectReason LastRejectReason = UIButtonInputRejectReason.None;

            [ShowInInspector, ReadOnly]
            public bool InputConditionHasSource;

            [ShowInInspector, ReadOnly]
            public bool InputConditionValue;

            [ShowInInspector, ReadOnly]
            public double TimestampUtc;

            public void UpdateFrom(UIButtonTelemetrySnapshot s)
            {
                OwnerName = s.OwnerName;
                Kind = s.Kind;
                CurrentPhase = s.CurrentPhase;
                TriggerAction = s.TriggerAction;
                CanSubmit = s.CanSubmit;
                IsSelected = s.IsSelected;
                IsVisible = s.IsVisible;
                IsEffectivelyActive = s.IsEffectivelyActive;
                GuardSelectionWhileHolding = s.GuardSelectionWhileHolding;
                GuardDuringCommandExecution = s.GuardDuringCommandExecution;
                DisableSelectionDuringCommandExecution = s.DisableSelectionDuringCommandExecution;
                IsHolding = s.IsHolding;
                HoldProgress = s.HoldProgress;
                ShortProgress = s.ShortProgress;
                LongProgress = s.LongProgress;
                IsLongMax = s.IsLongMax;
                LongMaxTime = s.LongMaxTime;
                AutoDecideOnLongMax = s.AutoDecideOnLongMax;
                IsHoldDecisionExecuting = s.IsHoldDecisionExecuting;
                IsSubmitUpExecuting = s.IsSubmitUpExecuting;
                LastInputEventType = s.LastInputEventType;
                LastInputPhase = s.LastInputPhase;
                LastInputMatched = s.LastInputMatched;
                LastInputAccepted = s.LastInputAccepted;
                LastRejectReason = s.LastRejectReason;
                InputConditionHasSource = s.InputConditionHasSource;
                InputConditionValue = s.InputConditionValue;
                TimestampUtc = s.TimestampUtc;
            }
        }

        [ShowInInspector]
        [LabelText("OnHoldDecision実行中")]
        [ReadOnly]
        bool IsHoldDecisionExecuting => _service != null ? _service.IsHoldDecisionExecuting : false;

        [ShowInInspector]
        [LabelText("OnSubmitUp実行中")]
        [ReadOnly]
        bool IsSubmitUpExecuting => _service != null ? _service.IsSubmitUpExecuting : false;

        [ShowInInspector]
        [LabelText("長押し中")]
        [ReadOnly]
        bool IsHolding => _service != null ? _service.IsHolding : false;

        [ShowInInspector]
        [LabelText("長押し経過時間")]
        [ReadOnly]
        float HoldProgress => _service != null ? _service.HoldProgress : 0f;

        // ================================================================
        // IFeatureInstaller実装
        // ================================================================

        /// <summary>
        /// UIButtonServiceをDIコンテナに登録する。
        /// </summary>
        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {

            // UIButtonServiceを登録
            builder.Register<IUIButtonService, UIButtonService>(Lifetime.Singleton)
                .WithParameter(scope)
                .WithParameter<IUIButtonOptions>(this)
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .As<IUIInputConsumer>()
                .As<IUIButtonTelemetry>();

            builder.RegisterBuildCallback(container =>
            {
                if (container.TryResolve<IUIButtonService>(out var service))
                {
                    _service = service;
                    ApplyInspectorSettingsWithoutInitialize(service);
                }
            });

            builder.Register<global::Game.UI.UIButtonTelemetryInspectorBridge>(Lifetime.Singleton)
                .WithParameter(this)
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();
        }

        // ================================================================
        // MonoBehaviourライフサイクル
        // ================================================================
        void Awake()
        {
            BindDebugOwners();
        }

        /// <summary>
        /// OnValidateはInspector値変更時に呼ばれる。
        /// </summary>
        void OnValidate()
        {
            BindDebugOwners();
            // 実行時のみ
            if (!Application.isPlaying) return;

            // Serviceが登録済みなら設定を反映
            if (_service != null)
            {
                ApplyInspectorSettingsWithoutInitialize(_service);
            }
        }

        void BindDebugOwners()
        {
            _onSubmitDownCommands?.BindDebugOwner(this, nameof(_onSubmitDownCommands));
            _onSubmitUpCommands?.BindDebugOwner(this, nameof(_onSubmitUpCommands));
            _onHoldDecisionCommands?.BindDebugOwner(this, nameof(_onHoldDecisionCommands));
            _onHoldIntervalCommands?.BindDebugOwner(this, nameof(_onHoldIntervalCommands));
            _onHoldCancelCommands?.BindDebugOwner(this, nameof(_onHoldCancelCommands));
            _onGenericStartCommands?.BindDebugOwner(this, nameof(_onGenericStartCommands));
            _onShortStartCommands?.BindDebugOwner(this, nameof(_onShortStartCommands));
            _onLongStartCommands?.BindDebugOwner(this, nameof(_onLongStartCommands));
            _onGenericDecisionCommands?.BindDebugOwner(this, nameof(_onGenericDecisionCommands));
            _onShortDecisionCommands?.BindDebugOwner(this, nameof(_onShortDecisionCommands));
            _onLongDecisionCommands?.BindDebugOwner(this, nameof(_onLongDecisionCommands));
            _onLongMaxDecisionCommands?.BindDebugOwner(this, nameof(_onLongMaxDecisionCommands));
            _onCancelCommands?.BindDebugOwner(this, nameof(_onCancelCommands));
        }

        /// <summary>
        /// Inspector設定をServiceに反映する（初期化なし）。
        /// OnValidateから呼ばれる。
        /// </summary>
        void ApplyInspectorSettingsWithoutInitialize(IUIButtonService service)
        {
            // 基本設定
            service.Kind = _kind;
            service.CanSubmit = _canSubmit;
            service.HoldTime = _holdTime;
            service.LongMaxTime = _longMaxTime;
            service.AutoDecideOnLongMax = _autoDecideOnLongMax;
            service.HoldInterval = _holdInterval;
            service.TriggerAction = _triggerAction;
            service.InputControlCondition = _inputControlCondition;
            service.GuardSelectionWhileHolding = _guardSelectionWhileHolding;
            service.GuardDuringCommandExecution = _guardDuringCommandExecution;
            service.DisableSelectionDuringCommandExecution = _disableSelectionDuringCommandExecution;

            // コマンド設定
            service.OnSubmitDownCommands.SetCommands(new List<VNext.ICommandSource>(_onSubmitDownCommands.Commands));
            service.OnSubmitUpCommands.SetCommands(new List<VNext.ICommandSource>(_onSubmitUpCommands.Commands));
            service.OnHoldDecisionCommands.SetCommands(new List<VNext.ICommandSource>(_onHoldDecisionCommands.Commands));
            service.OnHoldIntervalCommands.SetCommands(new List<VNext.ICommandSource>(_onHoldIntervalCommands.Commands));
            service.OnHoldCancelCommands.SetCommands(new List<VNext.ICommandSource>(_onHoldCancelCommands.Commands));
            service.OnGenericStartCommands.SetCommands(new List<VNext.ICommandSource>(_onGenericStartCommands.Commands));
            service.OnShortStartCommands.SetCommands(new List<VNext.ICommandSource>(_onShortStartCommands.Commands));
            service.OnLongStartCommands.SetCommands(new List<VNext.ICommandSource>(_onLongStartCommands.Commands));
            service.OnGenericDecisionCommands.SetCommands(new List<VNext.ICommandSource>(_onGenericDecisionCommands.Commands));
            service.OnShortDecisionCommands.SetCommands(new List<VNext.ICommandSource>(_onShortDecisionCommands.Commands));
            service.OnLongDecisionCommands.SetCommands(new List<VNext.ICommandSource>(_onLongDecisionCommands.Commands));
            service.OnLongMaxDecisionCommands.SetCommands(new List<VNext.ICommandSource>(_onLongMaxDecisionCommands.Commands));
            service.OnCancelCommands.SetCommands(new List<VNext.ICommandSource>(_onCancelCommands.Commands));
            service.RefreshTelemetry();

            // (events removed)
        }

        public void SetInspectorTelemetry(UIButtonTelemetrySnapshot snapshot)
        {
            _inspectorTelemetry.UpdateFrom(snapshot);
        }
    }
}
