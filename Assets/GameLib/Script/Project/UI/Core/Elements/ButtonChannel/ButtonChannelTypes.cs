#nullable enable
using System;
using System.Collections.Generic;
using Game.Commands.VNext;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.UI
{
    public enum ButtonChannelWorldTriggerButton
    {
        Left = 10,
        Right = 20,
    }

    public enum ButtonChannelPhase
    {
        Idle = 0,
        Pressed = 10,
        HoldReached = 20,
        Short = 30,
        Long = 40,
        LongMax = 50,
        CompletedWaitingRelease = 60,
    }

    public enum ButtonChannelPlayerControlOperation
    {
        SwapInputPreset = 10,
        SwapPlayerPreset = 20,
        MutateInputSettings = 30,
        MutatePlayerSettings = 40,
        ResetRuntimeOverrides = 50,
    }

    public enum ButtonChannelHubControlOperation
    {
        RegisterOrReplace = 10,
        Unregister = 20,
    }

    public readonly struct ButtonChannelOutputSnapshot
    {
        public readonly string Tag;
        public readonly bool IsEnabled;
        public readonly bool IsSelected;
        public readonly bool IsHovered;
        public readonly bool IsInteracting;
        public readonly bool IsCommandExecuting;
        public readonly ButtonChannelPhase Phase;
        public readonly float HoldProgress;
        public readonly float ShortProgress;
        public readonly float LongProgress;
        public readonly bool IsLong;
        public readonly bool IsLongMax;

        public ButtonChannelOutputSnapshot(
            string tag,
            bool isEnabled,
            bool isSelected,
            bool isHovered,
            bool isInteracting,
            bool isCommandExecuting,
            ButtonChannelPhase phase,
            float holdProgress,
            float shortProgress,
            float longProgress,
            bool isLong,
            bool isLongMax)
        {
            Tag = tag ?? "default";
            IsEnabled = isEnabled;
            IsSelected = isSelected;
            IsHovered = isHovered;
            IsInteracting = isInteracting;
            IsCommandExecuting = isCommandExecuting;
            Phase = phase;
            HoldProgress = holdProgress;
            ShortProgress = shortProgress;
            LongProgress = longProgress;
            IsLong = isLong;
            IsLongMax = isLongMax;
        }
    }

    public interface IButtonChannelOutput
    {
        string Tag { get; }
        bool IsEnabled { get; }
        bool IsSelected { get; }
        bool IsHovered { get; }
        bool IsInteracting { get; }
        bool IsCommandExecuting { get; }
        ButtonChannelPhase Phase { get; }
        float HoldProgress { get; }
        float ShortProgress { get; }
        float LongProgress { get; }
        bool IsLong { get; }
        bool IsLongMax { get; }
        event Action<ButtonChannelOutputSnapshot>? OnUpdated;
    }

    public interface IButtonChannelControlService
    {
        bool SwapInputPreset(ButtonInputPresetBase? preset);
        bool SwapPlayerPreset(ButtonPlayerPreset? preset);
        bool MutateInputSettings(ButtonInputRuntimeMutationBase? mutation, ICommandListRuntimeMutationService? mutationService);
        bool MutatePlayerSettings(ButtonPlayerRuntimeMutation? mutation, ICommandListRuntimeMutationService? mutationService);
        bool AppendDecisionCommands(CommandListData? commands, ICommandListRuntimeMutationService? mutationService);
        bool ResetRuntimeOverrides(bool resetInput, bool resetPlayer);
    }

    public interface IButtonChannelHubService
    {
        int ChannelCount { get; }
        bool Contains(string tag);
        bool TryGetOutput(string tag, out IButtonChannelOutput? output);
        bool TryGetControl(string tag, out IButtonChannelControlService? control);
        bool RegisterOrReplace(string tag, ButtonChannelPreset preset);
        bool Unregister(string tag);
        void GetTags(List<string> output);
    }

    public interface IButtonChannelOptions
    {
        DynamicValue<ButtonChannelPreset> PresetValue { get; }
        Transform OwnerTransform { get; }
    }

    [Serializable]
    public sealed class ButtonChannelPreset : IDynamicManagedRefValue
    {
        [BoxGroup("Preset")]
        [LabelText("Input Preset")]
        [Tooltip("押下シーケンスの解釈方法です。Instant / Hold / ShortLong などの入力系 preset を指定します。")]
        [SerializeField]
        DynamicValue<ButtonInputPresetBase> _inputPreset =
            DynamicValue<ButtonInputPresetBase>.FromSource(
                new ManagedRefLiteralSource<ButtonInputPresetBase>(new InstantButtonInputPreset()));

        [BoxGroup("Preset")]
        [LabelText("Player Preset")]
        [Tooltip("enabled condition、binding、selection block、command execution guard などの player 側設定です。")]
        [SerializeField]
        DynamicValue<ButtonPlayerPreset> _playerPreset =
            DynamicValue<ButtonPlayerPreset>.FromSource(
                new ManagedRefLiteralSource<ButtonPlayerPreset>(new ButtonPlayerPreset()));

        public DynamicValue<ButtonInputPresetBase> InputPresetValue => _inputPreset;
        public DynamicValue<ButtonPlayerPreset> PlayerPresetValue => _playerPreset;

        public ButtonChannelPreset CreateRuntimeCopy()
        {
            return new ButtonChannelPreset
            {
                _inputPreset = _inputPreset,
                _playerPreset = _playerPreset,
            };
        }
    }

    [Serializable]
    public sealed class ButtonPlayerPreset : IDynamicManagedRefValue
    {
        [BoxGroup("State")]
        [LabelText("Enabled Condition")]
        [Tooltip("true のときだけこの channel は入力を受け付けます。interaction 中に false へ落ちた場合は自動 cancel します。")]
        [SerializeField]
        DynamicValue<bool> _enabledCondition = DynamicValueExtensions.FromLiteral(true);

        [BoxGroup("Binding UI")]
        [LabelText("UI Trigger Action")]
        [Tooltip("UI adapter 使用時にこの channel を起動する入力アクションです。")]
        [SerializeField]
        UIInputAction _uiTriggerAction = UIInputAction.Submit;

        [BoxGroup("Binding World")]
        [LabelText("World Trigger Button")]
        [Tooltip("World adapter 使用時にこの channel を起動する pointer button です。")]
        [SerializeField]
        ButtonChannelWorldTriggerButton _worldTriggerButton = ButtonChannelWorldTriggerButton.Left;

        [BoxGroup("Guard Execution")]
        [LabelText("Guard During Command Execution")]
        [Tooltip("決定 command 実行中は新しい入力を受け付けないようにします。")]
        [SerializeField]
        bool _guardDuringCommandExecution = true;

        [BoxGroup("Guard Execution")]
        [ShowIf(nameof(_guardDuringCommandExecution))]
        [LabelText("Disable Selection During Command Execution")]
        [Tooltip("決定 command 実行中に UIElement を一時的に非選択にします。UI adapter のときだけ有効です。")]
        [SerializeField]
        bool _disableSelectionDuringCommandExecution;

        [BoxGroup("Guard Interaction")]
        [LabelText("Allow Navigation Selection Change While Interacting")]
        [Tooltip("interaction 中に navigation による selection 変更を許可するかどうかです。false の場合は block を取得して変更を防ぎます。")]
        [SerializeField]
        bool _allowNavigationSelectionChangeWhileInteracting;

        [BoxGroup("Guard Interaction")]
        [LabelText("Allow Pointer Selection Change While Interacting")]
        [Tooltip("interaction 中に pointer による selection / hover 変更を許可するかどうかです。false の場合は block を取得して変更を防ぎます。")]
        [SerializeField]
        bool _allowPointerSelectionChangeWhileInteracting;

        public DynamicValue<bool> EnabledCondition => _enabledCondition;
        public UIInputAction UITriggerAction => _uiTriggerAction;
        public ButtonChannelWorldTriggerButton WorldTriggerButton => _worldTriggerButton;
        public bool GuardDuringCommandExecution => _guardDuringCommandExecution;
        public bool DisableSelectionDuringCommandExecution => _disableSelectionDuringCommandExecution;
        public bool AllowNavigationSelectionChangeWhileInteracting => _allowNavigationSelectionChangeWhileInteracting;
        public bool AllowPointerSelectionChangeWhileInteracting => _allowPointerSelectionChangeWhileInteracting;

        internal ButtonPlayerPreset CreateRuntimeCopy()
        {
            return new ButtonPlayerPreset
            {
                _enabledCondition = _enabledCondition,
                _uiTriggerAction = _uiTriggerAction,
                _worldTriggerButton = _worldTriggerButton,
                _guardDuringCommandExecution = _guardDuringCommandExecution,
                _disableSelectionDuringCommandExecution = _disableSelectionDuringCommandExecution,
                _allowNavigationSelectionChangeWhileInteracting = _allowNavigationSelectionChangeWhileInteracting,
                _allowPointerSelectionChangeWhileInteracting = _allowPointerSelectionChangeWhileInteracting,
            };
        }

        internal void ApplyMutation(ButtonPlayerRuntimeMutation mutation)
        {
            if (mutation == null)
                return;

            if (mutation.ApplyEnabledCondition)
                _enabledCondition = mutation.EnabledCondition;

            if (mutation.ApplyUITriggerAction)
                _uiTriggerAction = mutation.UITriggerAction;

            if (mutation.ApplyWorldTriggerButton)
                _worldTriggerButton = mutation.WorldTriggerButton;

            if (mutation.ApplyExecutionGuards)
            {
                _guardDuringCommandExecution = mutation.GuardDuringCommandExecution;
                _disableSelectionDuringCommandExecution = mutation.DisableSelectionDuringCommandExecution;
            }

            if (mutation.ApplyInteractionSelectionPolicy)
            {
                _allowNavigationSelectionChangeWhileInteracting = mutation.AllowNavigationSelectionChangeWhileInteracting;
                _allowPointerSelectionChangeWhileInteracting = mutation.AllowPointerSelectionChangeWhileInteracting;
            }
        }
    }

    [Serializable]
    public sealed class ButtonPlayerRuntimeMutation
    {
        [BoxGroup("State")]
        [ToggleLeft]
        [LabelText("Apply Enabled Condition")]
        [Tooltip("enabled condition を runtime override する場合に有効にします。")]
        public bool ApplyEnabledCondition;

        [BoxGroup("State")]
        [ShowIf(nameof(ApplyEnabledCondition))]
        [LabelText("Enabled Condition")]
        [Tooltip("true のときだけ channel は入力を受け付けます。")]
        public DynamicValue<bool> EnabledCondition = DynamicValueExtensions.FromLiteral(true);

        [BoxGroup("Binding UI")]
        [ToggleLeft]
        [LabelText("Apply UI Trigger Action")]
        [Tooltip("UI adapter 使用時の trigger action を runtime override する場合に有効にします。")]
        public bool ApplyUITriggerAction;

        [BoxGroup("Binding UI")]
        [ShowIf(nameof(ApplyUITriggerAction))]
        [LabelText("UI Trigger Action")]
        public UIInputAction UITriggerAction = UIInputAction.Submit;

        [BoxGroup("Binding World")]
        [ToggleLeft]
        [LabelText("Apply World Trigger Button")]
        [Tooltip("World adapter 使用時の pointer button を runtime override する場合に有効にします。")]
        public bool ApplyWorldTriggerButton;

        [BoxGroup("Binding World")]
        [ShowIf(nameof(ApplyWorldTriggerButton))]
        [LabelText("World Trigger Button")]
        public ButtonChannelWorldTriggerButton WorldTriggerButton = ButtonChannelWorldTriggerButton.Left;

        [BoxGroup("Guard Execution")]
        [ToggleLeft]
        [LabelText("Apply Execution Guards")]
        [Tooltip("command execution guard と selection disable を runtime override する場合に有効にします。")]
        public bool ApplyExecutionGuards;

        [BoxGroup("Guard Execution")]
        [ShowIf(nameof(ApplyExecutionGuards))]
        [LabelText("Guard During Command Execution")]
        public bool GuardDuringCommandExecution = true;

        [BoxGroup("Guard Execution")]
        [ShowIf(nameof(ApplyExecutionGuards))]
        [LabelText("Disable Selection During Command Execution")]
        public bool DisableSelectionDuringCommandExecution;

        [BoxGroup("Guard Interaction")]
        [ToggleLeft]
        [LabelText("Apply Interaction Selection Policy")]
        [Tooltip("interaction 中の navigation / pointer selection change 許可設定を runtime override する場合に有効にします。")]
        public bool ApplyInteractionSelectionPolicy;

        [BoxGroup("Guard Interaction")]
        [ShowIf(nameof(ApplyInteractionSelectionPolicy))]
        [LabelText("Allow Navigation Selection Change While Interacting")]
        public bool AllowNavigationSelectionChangeWhileInteracting;

        [BoxGroup("Guard Interaction")]
        [ShowIf(nameof(ApplyInteractionSelectionPolicy))]
        [LabelText("Allow Pointer Selection Change While Interacting")]
        public bool AllowPointerSelectionChangeWhileInteracting;

        public bool HasAnyMutation()
        {
            return ApplyEnabledCondition ||
                   ApplyUITriggerAction ||
                   ApplyWorldTriggerButton ||
                   ApplyExecutionGuards ||
                   ApplyInteractionSelectionPolicy;
        }
    }

    [Serializable]
    public abstract class ButtonInputPresetBase : IDynamicManagedRefValue
    {
        public abstract string DebugKind { get; }
        internal abstract ButtonInputPresetBase CreateRuntimeCopy();
        internal abstract void ApplyMutation(ButtonInputRuntimeMutationBase mutation, ICommandListRuntimeMutationService? mutationService);
    }

    [Serializable]
    public abstract class ButtonInputRuntimeMutationBase : IDynamicManagedRefValue
    {
        public abstract string DebugKind { get; }
        public abstract bool HasAnyMutation();
    }

    [Serializable]
    public sealed class InstantButtonInputPreset : ButtonInputPresetBase
    {
        [BoxGroup("Commands")]
        [LabelText("On Down")]
        [Tooltip("trigger down 時に実行する command list です。")]
        [SerializeField]
        [CommandListFunctionName("ButtonChannel.Instant.OnDown")]
        CommandListData _onDownCommands = new();

        [BoxGroup("Commands")]
        [LabelText("On Up")]
        [Tooltip("trigger up 時に実行する command list です。")]
        [SerializeField]
        [CommandListFunctionName("ButtonChannel.Instant.OnUp")]
        CommandListData _onUpCommands = new();

        [BoxGroup("Commands")]
        [LabelText("On Cancel")]
        [Tooltip("down 後に決定前 cancel になったときに実行する command list です。")]
        [SerializeField]
        [CommandListFunctionName("ButtonChannel.Instant.OnCancel")]
        CommandListData _onCancelCommands = new();

        public override string DebugKind => nameof(InstantButtonInputPreset);
        public CommandListData OnDownCommands => _onDownCommands;
        public CommandListData OnUpCommands => _onUpCommands;
        public CommandListData OnCancelCommands => _onCancelCommands;

        internal override ButtonInputPresetBase CreateRuntimeCopy()
        {
            return new InstantButtonInputPreset
            {
                _onDownCommands = ButtonChannelPresetCloneUtility.CloneCommandList(_onDownCommands),
                _onUpCommands = ButtonChannelPresetCloneUtility.CloneCommandList(_onUpCommands),
                _onCancelCommands = ButtonChannelPresetCloneUtility.CloneCommandList(_onCancelCommands),
            };
        }

        internal override void ApplyMutation(ButtonInputRuntimeMutationBase mutation, ICommandListRuntimeMutationService? mutationService)
        {
            if (mutation is not InstantButtonInputRuntimeMutation typed)
                return;

            if (typed.ApplyDownCommands)
                _onDownCommands.ApplyRuntimeMutation(typed.DownCommands, mutationService);

            if (typed.ApplyUpCommands)
                _onUpCommands.ApplyRuntimeMutation(typed.UpCommands, mutationService);

            if (typed.ApplyCancelCommands)
                _onCancelCommands.ApplyRuntimeMutation(typed.CancelCommands, mutationService);
        }
    }

    [Serializable]
    public sealed class HoldButtonInputPreset : ButtonInputPresetBase
    {
        [BoxGroup("Timing")]
        [LabelText("Hold Time")]
        [Tooltip("hold 到達までに必要な時間です。")]
        [SerializeField]
        [MinValue(0.01f)]
        float _holdTime = 1f;

        [BoxGroup("Timing")]
        [LabelText("Auto Decide On Hold Reached")]
        [Tooltip("hold 到達時に自動で決定するかどうかです。false の場合は到達後の up で決定します。")]
        [SerializeField]
        bool _autoDecideOnHoldReached;

        [BoxGroup("Timing")]
        [LabelText("Hold Interval")]
        [Tooltip("hold 中に interval command を実行する間隔です。0 以下の場合は interval 実行を無効扱いにします。")]
        [SerializeField]
        [MinValue(0f)]
        float _holdInterval = 0.1f;

        [BoxGroup("Commands")]
        [LabelText("On Down")]
        [Tooltip("hold 開始時に実行する command list です。")]
        [SerializeField]
        [CommandListFunctionName("ButtonChannel.Hold.OnDown")]
        CommandListData _onDownCommands = new();

        [BoxGroup("Commands")]
        [LabelText("On Hold Reached")]
        [Tooltip("hold 到達時に実行する command list です。")]
        [SerializeField]
        [CommandListFunctionName("ButtonChannel.Hold.OnReached")]
        CommandListData _onHoldReachedCommands = new();

        [BoxGroup("Commands")]
        [LabelText("On Decision Up")]
        [Tooltip("hold 到達後、up で決定したときに実行する command list です。")]
        [SerializeField]
        [CommandListFunctionName("ButtonChannel.Hold.OnDecisionUp")]
        CommandListData _onDecisionUpCommands = new();

        [BoxGroup("Commands")]
        [LabelText("On Cancel")]
        [Tooltip("hold 到達前に cancel されたときに実行する command list です。")]
        [SerializeField]
        [CommandListFunctionName("ButtonChannel.Hold.OnCancel")]
        CommandListData _onCancelCommands = new();

        [BoxGroup("Commands")]
        [LabelText("On Interval")]
        [Tooltip("hold 中、interval ごとに実行する command list です。")]
        [SerializeField]
        [CommandListFunctionName("ButtonChannel.Hold.OnInterval")]
        CommandListData _onIntervalCommands = new();

        public override string DebugKind => nameof(HoldButtonInputPreset);
        public float HoldTime => Mathf.Max(0.01f, _holdTime);
        public bool AutoDecideOnHoldReached => _autoDecideOnHoldReached;
        public float HoldInterval => _holdInterval <= 0f ? 0f : _holdInterval;
        public CommandListData OnDownCommands => _onDownCommands;
        public CommandListData OnHoldReachedCommands => _onHoldReachedCommands;
        public CommandListData OnDecisionUpCommands => _onDecisionUpCommands;
        public CommandListData OnCancelCommands => _onCancelCommands;
        public CommandListData OnIntervalCommands => _onIntervalCommands;

        internal override ButtonInputPresetBase CreateRuntimeCopy()
        {
            return new HoldButtonInputPreset
            {
                _holdTime = _holdTime,
                _autoDecideOnHoldReached = _autoDecideOnHoldReached,
                _holdInterval = _holdInterval,
                _onDownCommands = ButtonChannelPresetCloneUtility.CloneCommandList(_onDownCommands),
                _onHoldReachedCommands = ButtonChannelPresetCloneUtility.CloneCommandList(_onHoldReachedCommands),
                _onDecisionUpCommands = ButtonChannelPresetCloneUtility.CloneCommandList(_onDecisionUpCommands),
                _onCancelCommands = ButtonChannelPresetCloneUtility.CloneCommandList(_onCancelCommands),
                _onIntervalCommands = ButtonChannelPresetCloneUtility.CloneCommandList(_onIntervalCommands),
            };
        }

        internal override void ApplyMutation(ButtonInputRuntimeMutationBase mutation, ICommandListRuntimeMutationService? mutationService)
        {
            if (mutation is not HoldButtonInputRuntimeMutation typed)
                return;

            if (typed.ApplyTiming)
            {
                _holdTime = typed.HoldTime;
                _autoDecideOnHoldReached = typed.AutoDecideOnHoldReached;
                _holdInterval = typed.HoldInterval;
            }

            if (typed.ApplyDownCommands)
                _onDownCommands.ApplyRuntimeMutation(typed.DownCommands, mutationService);

            if (typed.ApplyHoldReachedCommands)
                _onHoldReachedCommands.ApplyRuntimeMutation(typed.HoldReachedCommands, mutationService);

            if (typed.ApplyDecisionUpCommands)
                _onDecisionUpCommands.ApplyRuntimeMutation(typed.DecisionUpCommands, mutationService);

            if (typed.ApplyCancelCommands)
                _onCancelCommands.ApplyRuntimeMutation(typed.CancelCommands, mutationService);

            if (typed.ApplyIntervalCommands)
                _onIntervalCommands.ApplyRuntimeMutation(typed.IntervalCommands, mutationService);
        }
    }

    [Serializable]
    public sealed class ShortLongButtonInputPreset : ButtonInputPresetBase
    {
        [BoxGroup("Timing")]
        [LabelText("Short Duration")]
        [Tooltip("Short から Long に移るまでの時間です。短押しから長押しへ遷移する境界時間として使います。")]
        [SerializeField]
        [MinValue(0.01f)]
        float _shortDuration = 1f;

        [BoxGroup("Timing")]
        [LabelText("Long Max Duration")]
        [Tooltip("Long 開始後、LongMax に到達するまでの追加時間です。")]
        [SerializeField]
        [MinValue(0.01f)]
        float _longMaxDuration = 1f;

        [BoxGroup("Timing")]
        [LabelText("Auto Decide On Long Max")]
        [Tooltip("LongMax 到達時に自動で決定するかどうかです。")]
        [SerializeField]
        bool _autoDecideOnLongMax;

        [BoxGroup("Commands Start")]
        [LabelText("On Generic Start")]
        [Tooltip("押下開始時に必ず実行する command list です。")]
        [SerializeField]
        [CommandListFunctionName("ButtonChannel.ShortLong.OnGenericStart")]
        CommandListData _onGenericStartCommands = new();

        [BoxGroup("Commands Start")]
        [LabelText("On Short Start")]
        [Tooltip("Short 開始時に実行する command list です。")]
        [SerializeField]
        [CommandListFunctionName("ButtonChannel.ShortLong.OnShortStart")]
        CommandListData _onShortStartCommands = new();

        [BoxGroup("Commands Start")]
        [LabelText("On Long Start")]
        [Tooltip("Short を超えて Long に入った瞬間に実行する command list です。")]
        [SerializeField]
        [CommandListFunctionName("ButtonChannel.ShortLong.OnLongStart")]
        CommandListData _onLongStartCommands = new();

        [BoxGroup("Commands Decision")]
        [LabelText("On Generic Decision")]
        [Tooltip("決定時に必ず実行する command list です。")]
        [SerializeField]
        [CommandListFunctionName("ButtonChannel.ShortLong.OnGenericDecision")]
        CommandListData _onGenericDecisionCommands = new();

        [BoxGroup("Commands Decision")]
        [LabelText("On Short Decision")]
        [Tooltip("Short のまま離したときに実行する command list です。")]
        [SerializeField]
        [CommandListFunctionName("ButtonChannel.ShortLong.OnShortDecision")]
        CommandListData _onShortDecisionCommands = new();

        [BoxGroup("Commands Decision")]
        [LabelText("On Long Decision")]
        [Tooltip("Long 状態で離したときに実行する command list です。")]
        [SerializeField]
        [CommandListFunctionName("ButtonChannel.ShortLong.OnLongDecision")]
        CommandListData _onLongDecisionCommands = new();

        [BoxGroup("Commands Decision")]
        [LabelText("On Long Max Decision")]
        [Tooltip("LongMax 状態で決定したときに実行する command list です。")]
        [SerializeField]
        [CommandListFunctionName("ButtonChannel.ShortLong.OnLongMaxDecision")]
        CommandListData _onLongMaxDecisionCommands = new();

        [BoxGroup("Commands Decision")]
        [LabelText("On Cancel")]
        [Tooltip("入力途中で cancel されたときに実行する command list です。")]
        [SerializeField]
        [CommandListFunctionName("ButtonChannel.ShortLong.OnCancel")]
        CommandListData _onCancelCommands = new();

        public override string DebugKind => nameof(ShortLongButtonInputPreset);
        public float ShortDuration => Mathf.Max(0.01f, _shortDuration);
        public float LongMaxDuration => Mathf.Max(0.01f, _longMaxDuration);
        public bool AutoDecideOnLongMax => _autoDecideOnLongMax;
        public CommandListData OnGenericStartCommands => _onGenericStartCommands;
        public CommandListData OnShortStartCommands => _onShortStartCommands;
        public CommandListData OnLongStartCommands => _onLongStartCommands;
        public CommandListData OnGenericDecisionCommands => _onGenericDecisionCommands;
        public CommandListData OnShortDecisionCommands => _onShortDecisionCommands;
        public CommandListData OnLongDecisionCommands => _onLongDecisionCommands;
        public CommandListData OnLongMaxDecisionCommands => _onLongMaxDecisionCommands;
        public CommandListData OnCancelCommands => _onCancelCommands;

        internal override ButtonInputPresetBase CreateRuntimeCopy()
        {
            return new ShortLongButtonInputPreset
            {
                _shortDuration = _shortDuration,
                _longMaxDuration = _longMaxDuration,
                _autoDecideOnLongMax = _autoDecideOnLongMax,
                _onGenericStartCommands = ButtonChannelPresetCloneUtility.CloneCommandList(_onGenericStartCommands),
                _onShortStartCommands = ButtonChannelPresetCloneUtility.CloneCommandList(_onShortStartCommands),
                _onLongStartCommands = ButtonChannelPresetCloneUtility.CloneCommandList(_onLongStartCommands),
                _onGenericDecisionCommands = ButtonChannelPresetCloneUtility.CloneCommandList(_onGenericDecisionCommands),
                _onShortDecisionCommands = ButtonChannelPresetCloneUtility.CloneCommandList(_onShortDecisionCommands),
                _onLongDecisionCommands = ButtonChannelPresetCloneUtility.CloneCommandList(_onLongDecisionCommands),
                _onLongMaxDecisionCommands = ButtonChannelPresetCloneUtility.CloneCommandList(_onLongMaxDecisionCommands),
                _onCancelCommands = ButtonChannelPresetCloneUtility.CloneCommandList(_onCancelCommands),
            };
        }

        internal override void ApplyMutation(ButtonInputRuntimeMutationBase mutation, ICommandListRuntimeMutationService? mutationService)
        {
            if (mutation is not ShortLongButtonInputRuntimeMutation typed)
                return;

            if (typed.ApplyTiming)
            {
                _shortDuration = typed.ShortDuration;
                _longMaxDuration = typed.LongMaxDuration;
                _autoDecideOnLongMax = typed.AutoDecideOnLongMax;
            }

            if (typed.ApplyGenericStartCommands)
                _onGenericStartCommands.ApplyRuntimeMutation(typed.GenericStartCommands, mutationService);

            if (typed.ApplyShortStartCommands)
                _onShortStartCommands.ApplyRuntimeMutation(typed.ShortStartCommands, mutationService);

            if (typed.ApplyLongStartCommands)
                _onLongStartCommands.ApplyRuntimeMutation(typed.LongStartCommands, mutationService);

            if (typed.ApplyGenericDecisionCommands)
                _onGenericDecisionCommands.ApplyRuntimeMutation(typed.GenericDecisionCommands, mutationService);

            if (typed.ApplyShortDecisionCommands)
                _onShortDecisionCommands.ApplyRuntimeMutation(typed.ShortDecisionCommands, mutationService);

            if (typed.ApplyLongDecisionCommands)
                _onLongDecisionCommands.ApplyRuntimeMutation(typed.LongDecisionCommands, mutationService);

            if (typed.ApplyLongMaxDecisionCommands)
                _onLongMaxDecisionCommands.ApplyRuntimeMutation(typed.LongMaxDecisionCommands, mutationService);

            if (typed.ApplyCancelCommands)
                _onCancelCommands.ApplyRuntimeMutation(typed.CancelCommands, mutationService);
        }
    }

    [Serializable]
    public sealed class InstantButtonInputRuntimeMutation : ButtonInputRuntimeMutationBase
    {
        [BoxGroup("Commands")]
        [ToggleLeft]
        [LabelText("Apply Down Commands")]
        public bool ApplyDownCommands;

        [BoxGroup("Commands")]
        [ShowIf(nameof(ApplyDownCommands))]
        [InlineProperty]
        [HideLabel]
        public CommandListMutationStep DownCommands = new() { Operation = CommandListMutationOperation.Override };

        [BoxGroup("Commands")]
        [ToggleLeft]
        [LabelText("Apply Up Commands")]
        public bool ApplyUpCommands;

        [BoxGroup("Commands")]
        [ShowIf(nameof(ApplyUpCommands))]
        [InlineProperty]
        [HideLabel]
        public CommandListMutationStep UpCommands = new() { Operation = CommandListMutationOperation.Override };

        [BoxGroup("Commands")]
        [ToggleLeft]
        [LabelText("Apply Cancel Commands")]
        public bool ApplyCancelCommands;

        [BoxGroup("Commands")]
        [ShowIf(nameof(ApplyCancelCommands))]
        [InlineProperty]
        [HideLabel]
        public CommandListMutationStep CancelCommands = new() { Operation = CommandListMutationOperation.Override };

        public override string DebugKind => nameof(InstantButtonInputRuntimeMutation);

        public override bool HasAnyMutation()
        {
            return ApplyDownCommands || ApplyUpCommands || ApplyCancelCommands;
        }
    }

    [Serializable]
    public sealed class HoldButtonInputRuntimeMutation : ButtonInputRuntimeMutationBase
    {
        [BoxGroup("Timing")]
        [ToggleLeft]
        [LabelText("Apply Timing")]
        public bool ApplyTiming;

        [BoxGroup("Timing")]
        [ShowIf(nameof(ApplyTiming))]
        [LabelText("Hold Time")]
        [MinValue(0.01f)]
        public float HoldTime = 1f;

        [BoxGroup("Timing")]
        [ShowIf(nameof(ApplyTiming))]
        [LabelText("Auto Decide On Hold Reached")]
        public bool AutoDecideOnHoldReached;

        [BoxGroup("Timing")]
        [ShowIf(nameof(ApplyTiming))]
        [LabelText("Hold Interval")]
        [MinValue(0f)]
        public float HoldInterval = 0.1f;

        [BoxGroup("Commands")]
        [ToggleLeft]
        [LabelText("Apply Down Commands")]
        public bool ApplyDownCommands;

        [BoxGroup("Commands")]
        [ShowIf(nameof(ApplyDownCommands))]
        [InlineProperty]
        [HideLabel]
        public CommandListMutationStep DownCommands = new() { Operation = CommandListMutationOperation.Override };

        [BoxGroup("Commands")]
        [ToggleLeft]
        [LabelText("Apply Hold Reached Commands")]
        public bool ApplyHoldReachedCommands;

        [BoxGroup("Commands")]
        [ShowIf(nameof(ApplyHoldReachedCommands))]
        [InlineProperty]
        [HideLabel]
        public CommandListMutationStep HoldReachedCommands = new() { Operation = CommandListMutationOperation.Override };

        [BoxGroup("Commands")]
        [ToggleLeft]
        [LabelText("Apply Decision Up Commands")]
        public bool ApplyDecisionUpCommands;

        [BoxGroup("Commands")]
        [ShowIf(nameof(ApplyDecisionUpCommands))]
        [InlineProperty]
        [HideLabel]
        public CommandListMutationStep DecisionUpCommands = new() { Operation = CommandListMutationOperation.Override };

        [BoxGroup("Commands")]
        [ToggleLeft]
        [LabelText("Apply Cancel Commands")]
        public bool ApplyCancelCommands;

        [BoxGroup("Commands")]
        [ShowIf(nameof(ApplyCancelCommands))]
        [InlineProperty]
        [HideLabel]
        public CommandListMutationStep CancelCommands = new() { Operation = CommandListMutationOperation.Override };

        [BoxGroup("Commands")]
        [ToggleLeft]
        [LabelText("Apply Interval Commands")]
        public bool ApplyIntervalCommands;

        [BoxGroup("Commands")]
        [ShowIf(nameof(ApplyIntervalCommands))]
        [InlineProperty]
        [HideLabel]
        public CommandListMutationStep IntervalCommands = new() { Operation = CommandListMutationOperation.Override };

        public override string DebugKind => nameof(HoldButtonInputRuntimeMutation);

        public override bool HasAnyMutation()
        {
            return ApplyTiming ||
                   ApplyDownCommands ||
                   ApplyHoldReachedCommands ||
                   ApplyDecisionUpCommands ||
                   ApplyCancelCommands ||
                   ApplyIntervalCommands;
        }
    }

    [Serializable]
    public sealed class ShortLongButtonInputRuntimeMutation : ButtonInputRuntimeMutationBase
    {
        [BoxGroup("Timing")]
        [ToggleLeft]
        [LabelText("Apply Timing")]
        public bool ApplyTiming;

        [BoxGroup("Timing")]
        [ShowIf(nameof(ApplyTiming))]
        [LabelText("Short Duration")]
        [MinValue(0.01f)]
        public float ShortDuration = 1f;

        [BoxGroup("Timing")]
        [ShowIf(nameof(ApplyTiming))]
        [LabelText("Long Max Duration")]
        [MinValue(0.01f)]
        public float LongMaxDuration = 1f;

        [BoxGroup("Timing")]
        [ShowIf(nameof(ApplyTiming))]
        [LabelText("Auto Decide On Long Max")]
        public bool AutoDecideOnLongMax;

        [BoxGroup("Commands Start")]
        [ToggleLeft]
        [LabelText("Apply Generic Start Commands")]
        public bool ApplyGenericStartCommands;

        [BoxGroup("Commands Start")]
        [ShowIf(nameof(ApplyGenericStartCommands))]
        [InlineProperty]
        [HideLabel]
        public CommandListMutationStep GenericStartCommands = new() { Operation = CommandListMutationOperation.Override };

        [BoxGroup("Commands Start")]
        [ToggleLeft]
        [LabelText("Apply Short Start Commands")]
        public bool ApplyShortStartCommands;

        [BoxGroup("Commands Start")]
        [ShowIf(nameof(ApplyShortStartCommands))]
        [InlineProperty]
        [HideLabel]
        public CommandListMutationStep ShortStartCommands = new() { Operation = CommandListMutationOperation.Override };

        [BoxGroup("Commands Start")]
        [ToggleLeft]
        [LabelText("Apply Long Start Commands")]
        public bool ApplyLongStartCommands;

        [BoxGroup("Commands Start")]
        [ShowIf(nameof(ApplyLongStartCommands))]
        [InlineProperty]
        [HideLabel]
        public CommandListMutationStep LongStartCommands = new() { Operation = CommandListMutationOperation.Override };

        [BoxGroup("Commands Decision")]
        [ToggleLeft]
        [LabelText("Apply Generic Decision Commands")]
        public bool ApplyGenericDecisionCommands;

        [BoxGroup("Commands Decision")]
        [ShowIf(nameof(ApplyGenericDecisionCommands))]
        [InlineProperty]
        [HideLabel]
        public CommandListMutationStep GenericDecisionCommands = new() { Operation = CommandListMutationOperation.Override };

        [BoxGroup("Commands Decision")]
        [ToggleLeft]
        [LabelText("Apply Short Decision Commands")]
        public bool ApplyShortDecisionCommands;

        [BoxGroup("Commands Decision")]
        [ShowIf(nameof(ApplyShortDecisionCommands))]
        [InlineProperty]
        [HideLabel]
        public CommandListMutationStep ShortDecisionCommands = new() { Operation = CommandListMutationOperation.Override };

        [BoxGroup("Commands Decision")]
        [ToggleLeft]
        [LabelText("Apply Long Decision Commands")]
        public bool ApplyLongDecisionCommands;

        [BoxGroup("Commands Decision")]
        [ShowIf(nameof(ApplyLongDecisionCommands))]
        [InlineProperty]
        [HideLabel]
        public CommandListMutationStep LongDecisionCommands = new() { Operation = CommandListMutationOperation.Override };

        [BoxGroup("Commands Decision")]
        [ToggleLeft]
        [LabelText("Apply Long Max Decision Commands")]
        public bool ApplyLongMaxDecisionCommands;

        [BoxGroup("Commands Decision")]
        [ShowIf(nameof(ApplyLongMaxDecisionCommands))]
        [InlineProperty]
        [HideLabel]
        public CommandListMutationStep LongMaxDecisionCommands = new() { Operation = CommandListMutationOperation.Override };

        [BoxGroup("Commands Decision")]
        [ToggleLeft]
        [LabelText("Apply Cancel Commands")]
        public bool ApplyCancelCommands;

        [BoxGroup("Commands Decision")]
        [ShowIf(nameof(ApplyCancelCommands))]
        [InlineProperty]
        [HideLabel]
        public CommandListMutationStep CancelCommands = new() { Operation = CommandListMutationOperation.Override };

        public override string DebugKind => nameof(ShortLongButtonInputRuntimeMutation);

        public override bool HasAnyMutation()
        {
            return ApplyTiming ||
                   ApplyGenericStartCommands ||
                   ApplyShortStartCommands ||
                   ApplyLongStartCommands ||
                   ApplyGenericDecisionCommands ||
                   ApplyShortDecisionCommands ||
                   ApplyLongDecisionCommands ||
                   ApplyLongMaxDecisionCommands ||
                   ApplyCancelCommands;
        }
    }

    static class ButtonChannelPresetCloneUtility
    {
        public static CommandListData CloneCommandList(CommandListData? source)
        {
            var clone = new CommandListData();
            if (source != null)
                clone.SetCommands(source);
            return clone;
        }
    }
}
