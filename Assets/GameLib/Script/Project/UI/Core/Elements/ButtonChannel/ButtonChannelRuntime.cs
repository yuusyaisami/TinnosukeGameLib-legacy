#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Commands.VNext;
using Game.Common;
using Game.Vars.Generated;
using UnityEngine;
using VContainer;

namespace Game.UI
{
    internal enum ButtonChannelAdapterKind
    {
        None = 0,
        UI = 10,
        World = 20,
    }

    internal enum ButtonChannelInteractionAction
    {
        UiSubmit = 10,
        UiCancel = 20,
        UiAttack = 30,
        UiInteract = 40,
        UiPause = 50,
        UiRetry = 60,
        PointerLeft = 70,
        PointerRight = 80,
    }

    internal enum ButtonChannelInteractionSignalPhase
    {
        Down = 10,
        Held = 20,
        Up = 30,
    }

    internal readonly struct ButtonChannelInteractionSignal
    {
        public readonly ButtonChannelInteractionAction Action;
        public readonly ButtonChannelInteractionSignalPhase Phase;
        public readonly float DeltaTime;
        public readonly Vector2 PointerPosition;

        public ButtonChannelInteractionSignal(
            ButtonChannelInteractionAction action,
            ButtonChannelInteractionSignalPhase phase,
            float deltaTime,
            Vector2 pointerPosition)
        {
            Action = action;
            Phase = phase;
            DeltaTime = deltaTime;
            PointerPosition = pointerPosition;
        }
    }

    internal static class ButtonChannelVarKeys
    {
        public static int ResolveChannelTagId() => VarIds.GameLib.UI.ButtonChannel.ChannelTag;
        public static int ResolveIsEnabledId() => VarIds.GameLib.UI.ButtonChannel.IsEnabled;
        public static int ResolveIsSelectedId() => VarIds.GameLib.UI.ButtonChannel.IsSelected;
        public static int ResolveIsHoveredId() => VarIds.GameLib.UI.ButtonChannel.IsHovered;
        public static int ResolveIsInteractingId() => VarIds.GameLib.UI.ButtonChannel.IsInteracting;
        public static int ResolvePhaseId() => VarIds.GameLib.UI.ButtonChannel.Phase;
        public static int ResolveHoldTimeId() => VarIds.GameLib.UI.ButtonChannel.HoldTime;
        public static int ResolveHoldProgressId() => VarIds.GameLib.UI.ButtonChannel.HoldProgress;
        public static int ResolveShortLongStateId() => VarIds.GameLib.UI.ButtonChannel.ShortLong.State;
        public static int ResolveShortLongShortProgressId() => VarIds.GameLib.UI.ButtonChannel.ShortLong.ShortProgress;
        public static int ResolveShortLongLongProgressId() => VarIds.GameLib.UI.ButtonChannel.ShortLong.LongProgress;
        public static int ResolveShortLongIsLongId() => VarIds.GameLib.UI.ButtonChannel.ShortLong.IsLong;
        public static int ResolveShortLongIsLongMaxId() => VarIds.GameLib.UI.ButtonChannel.ShortLong.IsLongMax;
        public static int ResolveShortLongLongMaxTimeId() => VarIds.GameLib.UI.ButtonChannel.ShortLong.LongMaxTime;
    }

    internal sealed class ButtonChannelRuntime : IButtonChannelOutput, IButtonChannelControlService
    {
        readonly IScopeNode _owner;
        readonly string _tag;
        readonly ButtonChannelOptions _options;

        IButtonChannelInteractionAdapter? _adapter;
        ButtonInputPresetBase _baseInputPreset = new InstantButtonInputPreset();
        ButtonInputPresetBase _currentInputPreset = new InstantButtonInputPreset();
        ButtonPlayerPreset _basePlayerPreset = new();
        ButtonPlayerPreset _currentPlayerPreset = new();
        ButtonInputProcessorBase _processor = null!;

        bool _isEnabled = true;
        bool _isSelected;
        bool _isHovered;
        bool _isCommandExecuting;
        ButtonChannelOutputSnapshot _lastSnapshot;
        CancellationTokenSource? _commandCts;

        public string Tag => _tag;
        public bool IsEnabled => _isEnabled;
        public bool IsSelected => _isSelected;
        public bool IsHovered => _isHovered;
        public bool IsInteracting => _processor != null && _processor.IsInteracting;
        public bool IsCommandExecuting => _isCommandExecuting;
        public ButtonChannelPhase Phase => _processor != null ? _processor.Phase : ButtonChannelPhase.Idle;
        public float HoldProgress => _processor != null ? _processor.HoldProgress : 0f;
        public float ShortProgress => _processor != null ? _processor.ShortProgress : 0f;
        public float LongProgress => _processor != null ? _processor.LongProgress : 0f;
        public bool IsLong => _processor != null && _processor.IsLong;
        public bool IsLongMax => _processor != null && _processor.IsLongMax;

        public event Action<ButtonChannelOutputSnapshot>? OnUpdated;

        public ButtonChannelRuntime(IScopeNode owner, string tag, ButtonChannelOptions options)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _tag = string.IsNullOrWhiteSpace(tag) ? "default" : tag.Trim();
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _processor = CreateProcessor(_currentInputPreset);
            _lastSnapshot = BuildSnapshot();
        }

        public void OnAcquire(IScopeNode scope, bool isReset, IButtonChannelInteractionAdapter? adapter)
        {
            _ = isReset;
            _adapter = adapter;
            ResolveSourcePresets(scope);
            ResetCommandCts();
            RefreshState(forcePublish: true);
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;

            CancelInteraction();
            _adapter = null;
            _baseInputPreset = new InstantButtonInputPreset();
            _currentInputPreset = new InstantButtonInputPreset();
            _basePlayerPreset = new ButtonPlayerPreset();
            _currentPlayerPreset = new ButtonPlayerPreset();
            _processor = CreateProcessor(_currentInputPreset);
            _isEnabled = false;
            _isSelected = false;
            _isHovered = false;
            _isCommandExecuting = false;

            _commandCts?.Cancel();
            _commandCts?.Dispose();
            _commandCts = null;

            RefreshState(forcePublish: true);
        }

        public void Tick()
        {
            RefreshState(forcePublish: false);
        }

        public bool HandleSignal(in ButtonChannelInteractionSignal signal)
        {
            if (_processor == null || _adapter == null)
                return false;

            RefreshState(forcePublish: false);
            if (!MatchesCurrentBinding(signal.Action))
                return false;

            if (_currentPlayerPreset.GuardDuringCommandExecution && _isCommandExecuting)
                return false;

            if (signal.Phase == ButtonChannelInteractionSignalPhase.Down && !CanStartInteraction())
                return false;

            var handled = _processor.HandleSignal(signal);
            if (handled)
                RefreshState(forcePublish: true);

            return handled;
        }

        public UISelectionBlockMask DesiredSelectionBlockMask
        {
            get
            {
                if (_adapter == null || _adapter.AdapterKind != ButtonChannelAdapterKind.UI || !IsInteracting)
                    return UISelectionBlockMask.None;

                var mask = UISelectionBlockMask.None;
                if (!_currentPlayerPreset.AllowNavigationSelectionChangeWhileInteracting)
                    mask |= UISelectionBlockMask.Navigation;
                if (!_currentPlayerPreset.AllowPointerSelectionChangeWhileInteracting)
                    mask |= UISelectionBlockMask.Pointer;
                return mask;
            }
        }

        public bool SwapInputPreset(ButtonInputPresetBase? preset)
        {
            if (preset == null)
                return false;

            CancelInteraction();
            _baseInputPreset = preset.CreateRuntimeCopy();
            _currentInputPreset = _baseInputPreset.CreateRuntimeCopy();
            _processor = CreateProcessor(_currentInputPreset);
            RefreshState(forcePublish: true);
            return true;
        }

        public bool SwapPlayerPreset(ButtonPlayerPreset? preset)
        {
            if (preset == null)
                return false;

            CancelInteraction();
            _basePlayerPreset = preset.CreateRuntimeCopy();
            _currentPlayerPreset = _basePlayerPreset.CreateRuntimeCopy();
            RefreshState(forcePublish: true);
            return true;
        }

        public bool MutateInputSettings(ButtonInputRuntimeMutationBase? mutation, ICommandListRuntimeMutationService? mutationService)
        {
            if (mutation == null || !mutation.HasAnyMutation())
                return false;

            _currentInputPreset.ApplyMutation(mutation, mutationService);
            _processor = CreateProcessor(_currentInputPreset);
            RefreshState(forcePublish: true);
            return true;
        }

        public bool MutatePlayerSettings(ButtonPlayerRuntimeMutation? mutation, ICommandListRuntimeMutationService? mutationService)
        {
            _ = mutationService;
            if (mutation == null || !mutation.HasAnyMutation())
                return false;

            _currentPlayerPreset.ApplyMutation(mutation);
            RefreshState(forcePublish: true);
            return true;
        }

        public bool AppendDecisionCommands(CommandListData? commands, ICommandListRuntimeMutationService? mutationService)
        {
            if (commands == null || commands.Count == 0)
                return false;

            var step = new CommandListMutationStep
            {
                Operation = CommandListMutationOperation.Append,
                Commands = ButtonChannelPresetCloneUtility.CloneCommandList(commands),
            };

            switch (_currentInputPreset)
            {
                case InstantButtonInputPreset instantPreset:
                    instantPreset.OnUpCommands.ApplyRuntimeMutation(step, mutationService);
                    return true;

                case HoldButtonInputPreset holdPreset:
                    holdPreset.OnDecisionUpCommands.ApplyRuntimeMutation(step, mutationService);
                    return true;

                case ShortLongButtonInputPreset shortLongPreset:
                    shortLongPreset.OnGenericDecisionCommands.ApplyRuntimeMutation(step, mutationService);
                    return true;

                default:
                    return false;
            }
        }

        public bool ResetRuntimeOverrides(bool resetInput, bool resetPlayer)
        {
            if (!resetInput && !resetPlayer)
                return false;

            CancelInteraction();

            if (resetInput)
            {
                _currentInputPreset = _baseInputPreset.CreateRuntimeCopy();
                _processor = CreateProcessor(_currentInputPreset);
            }

            if (resetPlayer)
                _currentPlayerPreset = _basePlayerPreset.CreateRuntimeCopy();

            RefreshState(forcePublish: true);
            return true;
        }

        internal void BindAdapter(IButtonChannelInteractionAdapter? adapter)
        {
            _adapter = adapter;
            RefreshState(forcePublish: true);
        }

        void ResolveSourcePresets(IScopeNode scope)
        {
            var vars = ResolveVarStore(scope);
            var context = new SimpleDynamicContext(vars, scope);
            var sourcePreset = ResolvePreset(_options.PresetValue, context);
            _baseInputPreset = ResolveInputPreset(sourcePreset, context);
            _basePlayerPreset = ResolvePlayerPreset(sourcePreset, context);
            _currentInputPreset = _baseInputPreset.CreateRuntimeCopy();
            _currentPlayerPreset = _basePlayerPreset.CreateRuntimeCopy();
            _processor = CreateProcessor(_currentInputPreset);
        }

        void ResetCommandCts()
        {
            _commandCts?.Cancel();
            _commandCts?.Dispose();
            _commandCts = new CancellationTokenSource();
        }

        static ButtonChannelPreset ResolvePreset(DynamicValue<ButtonChannelPreset> value, IDynamicContext context)
        {
            if (value.TryGet(context, out ButtonChannelPreset? preset) && preset != null)
                return preset.CreateRuntimeCopy();

            return new ButtonChannelPreset();
        }

        static ButtonInputPresetBase ResolveInputPreset(ButtonChannelPreset preset, IDynamicContext context)
        {
            if (preset.InputPresetValue.TryGet(context, out ButtonInputPresetBase? inputPreset) && inputPreset != null)
                return inputPreset.CreateRuntimeCopy();

            return new InstantButtonInputPreset();
        }

        static ButtonPlayerPreset ResolvePlayerPreset(ButtonChannelPreset preset, IDynamicContext context)
        {
            if (preset.PlayerPresetValue.TryGet(context, out ButtonPlayerPreset? playerPreset) && playerPreset != null)
                return playerPreset.CreateRuntimeCopy();

            return new ButtonPlayerPreset();
        }

        ButtonInputProcessorBase CreateProcessor(ButtonInputPresetBase preset)
        {
            return preset switch
            {
                HoldButtonInputPreset holdPreset => new HoldButtonInputProcessor(this, holdPreset),
                ShortLongButtonInputPreset shortLongPreset => new ShortLongButtonInputProcessor(this, shortLongPreset),
                InstantButtonInputPreset instantPreset => new InstantButtonInputProcessor(this, instantPreset),
                _ => new InstantButtonInputProcessor(this, new InstantButtonInputPreset()),
            };
        }

        bool MatchesCurrentBinding(ButtonChannelInteractionAction action)
        {
            if (_adapter == null)
                return false;

            return _adapter.AdapterKind switch
            {
                ButtonChannelAdapterKind.UI => action == ConvertUIAction(_currentPlayerPreset.UITriggerAction),
                ButtonChannelAdapterKind.World => action == ConvertWorldButton(_currentPlayerPreset.WorldTriggerButton),
                _ => false,
            };
        }

        bool CanStartInteraction()
        {
            if (_adapter == null || !_isEnabled)
                return false;

            return _adapter.AdapterKind switch
            {
                ButtonChannelAdapterKind.UI => _isSelected,
                ButtonChannelAdapterKind.World => _isHovered || _adapter.AllowsDirectPointerPressWithoutSelection,
                _ => false,
            };
        }

        void RefreshState(bool forcePublish)
        {
            var previous = _lastSnapshot;

            _isSelected = _adapter != null && _adapter.IsSelected;
            _isHovered = _adapter != null && _adapter.IsHovered;
            _isEnabled = EvaluateIsEnabled();

            if (IsInteracting && ShouldCancelInteraction())
                CancelInteraction();

            var next = BuildSnapshot();
            var changed = forcePublish || !SnapshotsEqual(previous, next);
            if (!changed)
                return;

            _lastSnapshot = next;
            WriteStateVars(next);
            OnUpdated?.Invoke(next);
        }

        bool EvaluateIsEnabled()
        {
            if (_adapter == null || !_adapter.IsAvailable)
                return false;

            if (_adapter.ElementState != null)
            {
                if (!_adapter.ElementState.IsVisible || !_adapter.ElementState.IsEffectivelyActive)
                    return false;
            }

            var context = new SimpleDynamicContext(ResolveVarStore(_owner), _owner);
            return _currentPlayerPreset.EnabledCondition.GetOrDefault(context, true);
        }

        bool ShouldCancelInteraction()
        {
            if (_adapter == null || !_adapter.IsAvailable)
                return true;

            if (_adapter.ElementState != null &&
                (!_adapter.ElementState.IsVisible || !_adapter.ElementState.IsEffectivelyActive))
            {
                return true;
            }

            return _adapter.AdapterKind switch
            {
                ButtonChannelAdapterKind.UI => !_adapter.IsSelected,
                ButtonChannelAdapterKind.World => !_adapter.IsHovered,
                _ => true,
            };
        }

        ButtonChannelOutputSnapshot BuildSnapshot(
            ButtonChannelPhase? forcedPhase = null,
            bool? forcedIsInteracting = null)
        {
            var phase = forcedPhase ?? (_processor != null ? _processor.Phase : ButtonChannelPhase.Idle);
            var isInteracting = forcedIsInteracting ?? (_processor != null && _processor.IsInteracting);
            return new ButtonChannelOutputSnapshot(
                _tag,
                _isEnabled,
                _isSelected,
                _isHovered,
                isInteracting,
                _isCommandExecuting,
                phase,
                _processor != null ? _processor.HoldProgress : 0f,
                _processor != null ? _processor.ShortProgress : 0f,
                _processor != null ? _processor.LongProgress : 0f,
                _processor != null && _processor.IsLong,
                _processor != null && _processor.IsLongMax);
        }

        void WriteStateVars(ButtonChannelOutputSnapshot snapshot)
        {
            var vars = ResolveVarStore(_owner);
            TrySet(vars, ButtonChannelVarKeys.ResolveChannelTagId(), DynamicVariant.FromString(snapshot.Tag));
            TrySet(vars, ButtonChannelVarKeys.ResolveIsEnabledId(), DynamicVariant.FromBool(snapshot.IsEnabled));
            TrySet(vars, ButtonChannelVarKeys.ResolveIsSelectedId(), DynamicVariant.FromBool(snapshot.IsSelected));
            TrySet(vars, ButtonChannelVarKeys.ResolveIsHoveredId(), DynamicVariant.FromBool(snapshot.IsHovered));
            TrySet(vars, ButtonChannelVarKeys.ResolveIsInteractingId(), DynamicVariant.FromBool(snapshot.IsInteracting));
            TrySet(vars, ButtonChannelVarKeys.ResolvePhaseId(), DynamicVariant.FromInt((int)snapshot.Phase));

            if (_currentInputPreset is HoldButtonInputPreset holdPreset)
            {
                TrySet(vars, ButtonChannelVarKeys.ResolveHoldTimeId(), DynamicVariant.FromFloat(holdPreset.HoldTime));
                TrySet(vars, ButtonChannelVarKeys.ResolveHoldProgressId(), DynamicVariant.FromFloat(snapshot.HoldProgress));
            }

            if (_currentInputPreset is ShortLongButtonInputPreset shortLongPreset)
            {
                TrySet(vars, ButtonChannelVarKeys.ResolveHoldTimeId(), DynamicVariant.FromFloat(shortLongPreset.ShortDuration));
                TrySet(vars, ButtonChannelVarKeys.ResolveHoldProgressId(), DynamicVariant.FromFloat(snapshot.ShortProgress));
                TrySet(vars, ButtonChannelVarKeys.ResolveShortLongStateId(), DynamicVariant.FromInt((int)snapshot.Phase));
                TrySet(vars, ButtonChannelVarKeys.ResolveShortLongShortProgressId(), DynamicVariant.FromFloat(snapshot.ShortProgress));
                TrySet(vars, ButtonChannelVarKeys.ResolveShortLongLongProgressId(), DynamicVariant.FromFloat(snapshot.LongProgress));
                TrySet(vars, ButtonChannelVarKeys.ResolveShortLongIsLongId(), DynamicVariant.FromBool(snapshot.IsLong));
                TrySet(vars, ButtonChannelVarKeys.ResolveShortLongIsLongMaxId(), DynamicVariant.FromBool(snapshot.IsLongMax));
                TrySet(vars, ButtonChannelVarKeys.ResolveShortLongLongMaxTimeId(), DynamicVariant.FromFloat(shortLongPreset.LongMaxDuration));
            }
        }

        internal void FireCommands(CommandListData commands, ButtonChannelPhase forcedPhase)
        {
            ExecuteCommandsAsync(commands, forcedPhase, null).Forget();
        }

        internal void FireHoldReachedAndMaybeDecision(HoldButtonInputPreset preset)
        {
            ExecuteHoldReachedAsync(preset).Forget();
        }

        internal void FireHoldDecision(CommandListData commands)
        {
            ExecuteDecisionCommandsAsync(commands, null, ButtonChannelPhase.HoldReached).Forget();
        }

        internal void FireShortLongDecision(CommandListData specificCommands, ButtonChannelPhase phase)
        {
            ExecuteDecisionCommandsAsync(
                (_currentInputPreset as ShortLongButtonInputPreset)?.OnGenericDecisionCommands ?? new CommandListData(),
                specificCommands,
                phase).Forget();
        }

        internal void CancelInteraction()
        {
            _processor?.Cancel();
            RefreshState(forcePublish: true);
        }

        async UniTaskVoid ExecuteHoldReachedAsync(HoldButtonInputPreset preset)
        {
            await ExecuteCommandsAsync(preset.OnHoldReachedCommands, ButtonChannelPhase.HoldReached, null);
            if (preset.AutoDecideOnHoldReached)
                await ExecuteDecisionCommandsInternalAsync(preset.OnDecisionUpCommands, null, ButtonChannelPhase.HoldReached);
        }

        async UniTaskVoid ExecuteDecisionCommandsAsync(CommandListData primary, CommandListData? secondary, ButtonChannelPhase phase)
        {
            await ExecuteDecisionCommandsInternalAsync(primary, secondary, phase);
        }

        async UniTask ExecuteDecisionCommandsInternalAsync(CommandListData primary, CommandListData? secondary, ButtonChannelPhase phase)
        {
            if (_currentPlayerPreset.GuardDuringCommandExecution)
            {
                _isCommandExecuting = true;
                RefreshState(forcePublish: true);
            }

            IUIElementStateController? stateController = null;
            if (_currentPlayerPreset.DisableSelectionDuringCommandExecution &&
                _adapter?.ElementState is IUIElementStateController controller)
            {
                stateController = controller;
                stateController.SetActive(false);
            }

            try
            {
                await ExecuteCommandsAsync(primary, phase, secondary);
            }
            finally
            {
                if (stateController != null)
                    stateController.SetActive(true);

                _isCommandExecuting = false;
                RefreshState(forcePublish: true);
            }
        }

        async UniTask ExecuteCommandsAsync(CommandListData primary, ButtonChannelPhase phase, CommandListData? secondary)
        {
            var runner = ResolveCommandRunner(_owner);
            if (runner == null)
                return;

            var ct = _commandCts?.Token ?? CancellationToken.None;
            await ExecuteSingleCommandListAsync(runner, primary, phase, ct);
            if (secondary != null)
                await ExecuteSingleCommandListAsync(runner, secondary, phase, ct);
        }

        async UniTask ExecuteSingleCommandListAsync(ICommandRunner runner, CommandListData commands, ButtonChannelPhase phase, CancellationToken ct)
        {
            if (commands == null || commands.Count == 0)
                return;

            var vars = BuildCommandVariables(phase);
            var options = CommandRunOptions.Default;
            var context = new CommandContext(_owner, vars, runner, _owner, options);

            try
            {
                var result = await runner.ExecuteListAsync(commands, context, ct, options);
                if (result.Status == CommandRunStatus.Error)
                    Debug.LogError($"[ButtonChannelRuntime] Command execution failed: {result.Message}");
            }
            catch (OperationCanceledException)
            {
            }
        }

        VarStore BuildCommandVariables(ButtonChannelPhase forcedPhase)
        {
            var snapshot = BuildSnapshot(forcedPhase, false);
            var vars = new VarStore();
            TrySet(vars, ButtonChannelVarKeys.ResolveChannelTagId(), DynamicVariant.FromString(snapshot.Tag));
            TrySet(vars, ButtonChannelVarKeys.ResolveIsEnabledId(), DynamicVariant.FromBool(snapshot.IsEnabled));
            TrySet(vars, ButtonChannelVarKeys.ResolveIsSelectedId(), DynamicVariant.FromBool(snapshot.IsSelected));
            TrySet(vars, ButtonChannelVarKeys.ResolveIsHoveredId(), DynamicVariant.FromBool(snapshot.IsHovered));
            TrySet(vars, ButtonChannelVarKeys.ResolveIsInteractingId(), DynamicVariant.FromBool(snapshot.IsInteracting));
            TrySet(vars, ButtonChannelVarKeys.ResolvePhaseId(), DynamicVariant.FromInt((int)snapshot.Phase));

            if (_currentInputPreset is HoldButtonInputPreset holdPreset)
            {
                TrySet(vars, ButtonChannelVarKeys.ResolveHoldTimeId(), DynamicVariant.FromFloat(holdPreset.HoldTime));
                TrySet(vars, ButtonChannelVarKeys.ResolveHoldProgressId(), DynamicVariant.FromFloat(snapshot.HoldProgress));
            }

            if (_currentInputPreset is ShortLongButtonInputPreset shortLongPreset)
            {
                TrySet(vars, ButtonChannelVarKeys.ResolveHoldTimeId(), DynamicVariant.FromFloat(shortLongPreset.ShortDuration));
                TrySet(vars, ButtonChannelVarKeys.ResolveHoldProgressId(), DynamicVariant.FromFloat(snapshot.ShortProgress));
                TrySet(vars, ButtonChannelVarKeys.ResolveShortLongStateId(), DynamicVariant.FromInt((int)snapshot.Phase));
                TrySet(vars, ButtonChannelVarKeys.ResolveShortLongShortProgressId(), DynamicVariant.FromFloat(snapshot.ShortProgress));
                TrySet(vars, ButtonChannelVarKeys.ResolveShortLongLongProgressId(), DynamicVariant.FromFloat(snapshot.LongProgress));
                TrySet(vars, ButtonChannelVarKeys.ResolveShortLongIsLongId(), DynamicVariant.FromBool(snapshot.IsLong));
                TrySet(vars, ButtonChannelVarKeys.ResolveShortLongIsLongMaxId(), DynamicVariant.FromBool(snapshot.IsLongMax));
                TrySet(vars, ButtonChannelVarKeys.ResolveShortLongLongMaxTimeId(), DynamicVariant.FromFloat(shortLongPreset.LongMaxDuration));
            }

            return vars;
        }

        static bool SnapshotsEqual(ButtonChannelOutputSnapshot a, ButtonChannelOutputSnapshot b)
        {
            return a.IsEnabled == b.IsEnabled &&
                   a.IsSelected == b.IsSelected &&
                   a.IsHovered == b.IsHovered &&
                   a.IsInteracting == b.IsInteracting &&
                   a.IsCommandExecuting == b.IsCommandExecuting &&
                   a.Phase == b.Phase &&
                   Mathf.Approximately(a.HoldProgress, b.HoldProgress) &&
                   Mathf.Approximately(a.ShortProgress, b.ShortProgress) &&
                   Mathf.Approximately(a.LongProgress, b.LongProgress) &&
                   a.IsLong == b.IsLong &&
                   a.IsLongMax == b.IsLongMax &&
                   string.Equals(a.Tag, b.Tag, StringComparison.Ordinal);
        }

        static void TrySet(IVarStore vars, int varId, DynamicVariant value)
        {
            if (vars == null || varId <= 0)
                return;

            vars.TrySetVariant(varId, value);
        }

        static IVarStore ResolveVarStore(IScopeNode scope)
        {
            if (scope.TryResolveInAncestors<IVarStore>(out var vars) && vars != null)
                return vars;

            return NullVarStore.Instance;
        }

        static ICommandRunner? ResolveCommandRunner(IScopeNode scope)
        {
            if (scope.TryResolveInAncestors<ICommandRunner>(out var runner) && runner != null)
                return runner;

            return null;
        }

        static ButtonChannelInteractionAction ConvertUIAction(UIInputAction action)
        {
            return action switch
            {
                UIInputAction.Cancel => ButtonChannelInteractionAction.UiCancel,
                UIInputAction.Attack => ButtonChannelInteractionAction.UiAttack,
                UIInputAction.Interact => ButtonChannelInteractionAction.UiInteract,
                UIInputAction.Pause => ButtonChannelInteractionAction.UiPause,
                UIInputAction.Retry => ButtonChannelInteractionAction.UiRetry,
                _ => ButtonChannelInteractionAction.UiSubmit,
            };
        }

        static ButtonChannelInteractionAction ConvertWorldButton(ButtonChannelWorldTriggerButton button)
        {
            return button == ButtonChannelWorldTriggerButton.Right
                ? ButtonChannelInteractionAction.PointerRight
                : ButtonChannelInteractionAction.PointerLeft;
        }

        abstract class ButtonInputProcessorBase
        {
            protected readonly ButtonChannelRuntime Runtime;

            protected ButtonInputProcessorBase(ButtonChannelRuntime runtime)
            {
                Runtime = runtime;
            }

            public abstract ButtonChannelPhase Phase { get; }
            public abstract bool IsInteracting { get; }
            public abstract float HoldProgress { get; }
            public abstract float ShortProgress { get; }
            public abstract float LongProgress { get; }
            public abstract bool IsLong { get; }
            public abstract bool IsLongMax { get; }

            public abstract bool HandleSignal(in ButtonChannelInteractionSignal signal);
            public abstract void Cancel();
        }

        sealed class InstantButtonInputProcessor : ButtonInputProcessorBase
        {
            readonly InstantButtonInputPreset _preset;
            bool _isPressed;

            public InstantButtonInputProcessor(ButtonChannelRuntime runtime, InstantButtonInputPreset preset) : base(runtime)
            {
                _preset = preset;
            }

            public override ButtonChannelPhase Phase => _isPressed ? ButtonChannelPhase.Pressed : ButtonChannelPhase.Idle;
            public override bool IsInteracting => _isPressed;
            public override float HoldProgress => 0f;
            public override float ShortProgress => 0f;
            public override float LongProgress => 0f;
            public override bool IsLong => false;
            public override bool IsLongMax => false;

            public override bool HandleSignal(in ButtonChannelInteractionSignal signal)
            {
                switch (signal.Phase)
                {
                    case ButtonChannelInteractionSignalPhase.Down:
                        _isPressed = true;
                        Runtime.FireCommands(_preset.OnDownCommands, ButtonChannelPhase.Pressed);
                        return true;

                    case ButtonChannelInteractionSignalPhase.Up:
                        if (!_isPressed)
                            return false;

                        Runtime.FireHoldDecision(_preset.OnUpCommands);
                        _isPressed = false;
                        return true;

                    default:
                        return false;
                }
            }

            public override void Cancel()
            {
                if (!_isPressed)
                    return;

                Runtime.FireCommands(_preset.OnCancelCommands, ButtonChannelPhase.Pressed);
                _isPressed = false;
            }
        }

        sealed class HoldButtonInputProcessor : ButtonInputProcessorBase
        {
            readonly HoldButtonInputPreset _preset;
            bool _isHolding;
            bool _holdReached;
            bool _completedWaitingRelease;
            float _elapsed;
            float _intervalElapsed;

            public HoldButtonInputProcessor(ButtonChannelRuntime runtime, HoldButtonInputPreset preset) : base(runtime)
            {
                _preset = preset;
            }

            public override ButtonChannelPhase Phase
            {
                get
                {
                    if (_completedWaitingRelease)
                        return ButtonChannelPhase.CompletedWaitingRelease;
                    if (_holdReached)
                        return ButtonChannelPhase.HoldReached;
                    return _isHolding ? ButtonChannelPhase.Pressed : ButtonChannelPhase.Idle;
                }
            }

            public override bool IsInteracting => _isHolding;
            public override float HoldProgress => _preset.HoldTime > 0f ? Mathf.Clamp01(_elapsed / _preset.HoldTime) : 0f;
            public override float ShortProgress => 0f;
            public override float LongProgress => 0f;
            public override bool IsLong => false;
            public override bool IsLongMax => false;

            public override bool HandleSignal(in ButtonChannelInteractionSignal signal)
            {
                switch (signal.Phase)
                {
                    case ButtonChannelInteractionSignalPhase.Down:
                        _isHolding = true;
                        _holdReached = false;
                        _completedWaitingRelease = false;
                        _elapsed = 0f;
                        _intervalElapsed = 0f;
                        Runtime.FireCommands(_preset.OnDownCommands, ButtonChannelPhase.Pressed);
                        return true;

                    case ButtonChannelInteractionSignalPhase.Held:
                        if (_completedWaitingRelease)
                            return true;
                        if (!_isHolding)
                            return false;

                        _elapsed = Mathf.Min(_preset.HoldTime, _elapsed + Mathf.Max(0f, signal.DeltaTime));
                        _intervalElapsed += Mathf.Max(0f, signal.DeltaTime);

                        if (!_holdReached && _preset.OnIntervalCommands.Count > 0 && _intervalElapsed >= _preset.HoldInterval)
                        {
                            _intervalElapsed = 0f;
                            Runtime.FireCommands(_preset.OnIntervalCommands, ButtonChannelPhase.Pressed);
                        }

                        if (!_holdReached && _elapsed >= _preset.HoldTime)
                        {
                            _holdReached = true;
                            if (_preset.AutoDecideOnHoldReached)
                            {
                                _isHolding = false;
                                _completedWaitingRelease = true;
                            }

                            Runtime.FireHoldReachedAndMaybeDecision(_preset);
                        }

                        return true;

                    case ButtonChannelInteractionSignalPhase.Up:
                        if (_completedWaitingRelease)
                        {
                            _completedWaitingRelease = false;
                            return true;
                        }

                        if (!_isHolding)
                            return false;

                        if (!_holdReached)
                        {
                            Cancel();
                            return true;
                        }

                        _isHolding = false;
                        Runtime.FireHoldDecision(_preset.OnDecisionUpCommands);
                        return true;

                    default:
                        return false;
                }
            }

            public override void Cancel()
            {
                if (!_isHolding && !_completedWaitingRelease)
                    return;

                Runtime.FireCommands(_preset.OnCancelCommands, _holdReached ? ButtonChannelPhase.HoldReached : ButtonChannelPhase.Pressed);
                _isHolding = false;
                _holdReached = false;
                _completedWaitingRelease = false;
                _elapsed = 0f;
                _intervalElapsed = 0f;
            }
        }

        sealed class ShortLongButtonInputProcessor : ButtonInputProcessorBase
        {
            readonly ShortLongButtonInputPreset _preset;
            bool _isHolding;
            ButtonChannelPhase _phase = ButtonChannelPhase.Idle;
            float _shortElapsed;
            float _longElapsed;

            public ShortLongButtonInputProcessor(ButtonChannelRuntime runtime, ShortLongButtonInputPreset preset) : base(runtime)
            {
                _preset = preset;
            }

            public override ButtonChannelPhase Phase => _phase;
            public override bool IsInteracting => _isHolding;
            public override float HoldProgress => ShortProgress;
            public override float ShortProgress => _preset.ShortDuration > 0f ? Mathf.Clamp01(_shortElapsed / _preset.ShortDuration) : 0f;
            public override float LongProgress => _preset.LongMaxDuration > 0f ? Mathf.Clamp01(_longElapsed / _preset.LongMaxDuration) : 0f;
            public override bool IsLong => _phase == ButtonChannelPhase.Long || _phase == ButtonChannelPhase.LongMax || _phase == ButtonChannelPhase.CompletedWaitingRelease;
            public override bool IsLongMax => _phase == ButtonChannelPhase.LongMax || _phase == ButtonChannelPhase.CompletedWaitingRelease;

            public override bool HandleSignal(in ButtonChannelInteractionSignal signal)
            {
                switch (signal.Phase)
                {
                    case ButtonChannelInteractionSignalPhase.Down:
                        _isHolding = true;
                        _phase = ButtonChannelPhase.Short;
                        _shortElapsed = 0f;
                        _longElapsed = 0f;
                        Runtime.FireCommands(_preset.OnGenericStartCommands, ButtonChannelPhase.Short);
                        Runtime.FireCommands(_preset.OnShortStartCommands, ButtonChannelPhase.Short);
                        return true;

                    case ButtonChannelInteractionSignalPhase.Held:
                        if (_phase == ButtonChannelPhase.CompletedWaitingRelease)
                            return true;
                        if (!_isHolding)
                            return false;

                        if (_phase == ButtonChannelPhase.Short)
                        {
                            _shortElapsed += Mathf.Max(0f, signal.DeltaTime);
                            if (_shortElapsed >= _preset.ShortDuration)
                            {
                                var overflow = _shortElapsed - _preset.ShortDuration;
                                _shortElapsed = _preset.ShortDuration;
                                _phase = ButtonChannelPhase.Long;
                                _longElapsed = Mathf.Max(0f, overflow);
                                Runtime.FireCommands(_preset.OnLongStartCommands, ButtonChannelPhase.Long);
                            }
                        }
                        else if (_phase == ButtonChannelPhase.Long || _phase == ButtonChannelPhase.LongMax)
                        {
                            _longElapsed += Mathf.Max(0f, signal.DeltaTime);
                        }

                        if (_phase == ButtonChannelPhase.Long && _longElapsed >= _preset.LongMaxDuration)
                        {
                            _longElapsed = _preset.LongMaxDuration;
                            _phase = ButtonChannelPhase.LongMax;
                            if (_preset.AutoDecideOnLongMax)
                            {
                                _isHolding = false;
                                _phase = ButtonChannelPhase.CompletedWaitingRelease;
                                Runtime.FireShortLongDecision(_preset.OnLongMaxDecisionCommands, ButtonChannelPhase.LongMax);
                            }
                        }
                        else if (_phase == ButtonChannelPhase.LongMax && _longElapsed > _preset.LongMaxDuration)
                        {
                            _longElapsed = _preset.LongMaxDuration;
                        }

                        return true;

                    case ButtonChannelInteractionSignalPhase.Up:
                        if (_phase == ButtonChannelPhase.CompletedWaitingRelease)
                        {
                            _phase = ButtonChannelPhase.Idle;
                            _shortElapsed = 0f;
                            _longElapsed = 0f;
                            return true;
                        }

                        if (!_isHolding)
                            return false;

                        var decisionPhase = _phase == ButtonChannelPhase.LongMax
                            ? ButtonChannelPhase.LongMax
                            : _phase == ButtonChannelPhase.Long
                                ? ButtonChannelPhase.Long
                                : ButtonChannelPhase.Short;

                        var specificCommands = decisionPhase switch
                        {
                            ButtonChannelPhase.Long => _preset.OnLongDecisionCommands,
                            ButtonChannelPhase.LongMax => _preset.OnLongMaxDecisionCommands,
                            _ => _preset.OnShortDecisionCommands,
                        };

                        _isHolding = false;
                        _phase = ButtonChannelPhase.Idle;
                        Runtime.FireShortLongDecision(specificCommands, decisionPhase);
                        return true;

                    default:
                        return false;
                }
            }

            public override void Cancel()
            {
                if (!_isHolding && _phase != ButtonChannelPhase.CompletedWaitingRelease)
                    return;

                var cancelPhase = _phase == ButtonChannelPhase.Idle ? ButtonChannelPhase.Short : _phase;
                Runtime.FireCommands(_preset.OnCancelCommands, cancelPhase);
                _isHolding = false;
                _phase = ButtonChannelPhase.Idle;
                _shortElapsed = 0f;
                _longElapsed = 0f;
            }
        }
    }
}
