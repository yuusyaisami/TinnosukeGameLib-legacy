#nullable enable
using System;
using Game.Input;
using Game.SelectRuntime;
using UnityEngine;

namespace Game.UI
{
    internal interface IButtonChannelInteractionAdapter : IDisposable
    {
        ButtonChannelAdapterKind AdapterKind { get; }
        bool IsAvailable { get; }
        bool IsSelected { get; }
        bool IsHovered { get; }
        bool AllowsDirectPointerPressWithoutSelection { get; }
        IUIElementState? ElementState { get; }

        void OnAcquire(IScopeNode scope, bool isReset);
        void OnRelease(IScopeNode scope, bool isReset);
        void Tick();
        void SetBlockMask(UISelectionBlockMask mask);
    }

    internal sealed class UIButtonChannelInteractionAdapter : IButtonChannelInteractionAdapter, IUIInputConsumer
    {
        readonly IScopeNode _owner;
        readonly IUIInputConsumerHub _consumerHub;
        readonly IUIElementState _elementState;
        readonly IUISelectionState _selectionState;
        readonly IUISelectionBlockService? _selectionBlockService;
        readonly Func<ButtonChannelInteractionSignal, bool> _dispatch;

        IDisposable? _selectionBlockHandle;
        UISelectionBlockMask _currentBlockMask;

        public UIButtonChannelInteractionAdapter(
            IScopeNode owner,
            IUIInputConsumerHub consumerHub,
            IUIElementState elementState,
            IUISelectionState selectionState,
            IUISelectionBlockService? selectionBlockService,
            Func<ButtonChannelInteractionSignal, bool> dispatch)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _consumerHub = consumerHub ?? throw new ArgumentNullException(nameof(consumerHub));
            _elementState = elementState ?? throw new ArgumentNullException(nameof(elementState));
            _selectionState = selectionState ?? throw new ArgumentNullException(nameof(selectionState));
            _selectionBlockService = selectionBlockService;
            _dispatch = dispatch ?? throw new ArgumentNullException(nameof(dispatch));
        }

        public ButtonChannelAdapterKind AdapterKind => ButtonChannelAdapterKind.UI;
        public bool IsAvailable => _consumerHub != null && _selectionState != null && _elementState != null;
        public bool IsSelected => ReferenceEquals(_selectionState.CurrentElement, _owner);
        public bool IsHovered => ReferenceEquals(_selectionState.HoveredElement, _owner);
        public bool AllowsDirectPointerPressWithoutSelection => false;
        public IUIElementState? ElementState => _elementState;
        public int Priority => 100;

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;
            _consumerHub.Register(this);
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;
            _consumerHub.Unregister(this);
            ReleaseSelectionBlock();
        }

        public void Tick()
        {
        }

        public void SetBlockMask(UISelectionBlockMask mask)
        {
            if (_selectionBlockService == null)
                return;

            if (_currentBlockMask == mask)
                return;

            ReleaseSelectionBlock();
            _currentBlockMask = mask;

            if (_currentBlockMask == UISelectionBlockMask.None)
                return;

            _selectionBlockHandle = _selectionBlockService.AcquireBlock(this, _currentBlockMask);
        }

        public bool Consume(in UIInputEvent e)
        {
            if (!TryTranslate(e, out var signal))
                return false;

            return _dispatch(signal);
        }

        public void Dispose()
        {
            ReleaseSelectionBlock();
        }

        static bool TryTranslate(in UIInputEvent e, out ButtonChannelInteractionSignal signal)
        {
            signal = default;
            if (!TryTranslateAction(e.Type, out var action, out var phase))
                return false;

            signal = new ButtonChannelInteractionSignal(action, phase, e.DeltaTime, e.PointerPosition);
            return true;
        }

        static bool TryTranslateAction(
            UIInputEventType eventType,
            out ButtonChannelInteractionAction action,
            out ButtonChannelInteractionSignalPhase phase)
        {
            action = default;
            phase = default;

            switch (eventType)
            {
                case UIInputEventType.SubmitDown:
                    action = ButtonChannelInteractionAction.UiSubmit;
                    phase = ButtonChannelInteractionSignalPhase.Down;
                    return true;
                case UIInputEventType.SubmitHeld:
                    action = ButtonChannelInteractionAction.UiSubmit;
                    phase = ButtonChannelInteractionSignalPhase.Held;
                    return true;
                case UIInputEventType.SubmitUp:
                    action = ButtonChannelInteractionAction.UiSubmit;
                    phase = ButtonChannelInteractionSignalPhase.Up;
                    return true;
                case UIInputEventType.CancelDown:
                    action = ButtonChannelInteractionAction.UiCancel;
                    phase = ButtonChannelInteractionSignalPhase.Down;
                    return true;
                case UIInputEventType.CancelHeld:
                    action = ButtonChannelInteractionAction.UiCancel;
                    phase = ButtonChannelInteractionSignalPhase.Held;
                    return true;
                case UIInputEventType.CancelUp:
                    action = ButtonChannelInteractionAction.UiCancel;
                    phase = ButtonChannelInteractionSignalPhase.Up;
                    return true;
                case UIInputEventType.AttackDown:
                    action = ButtonChannelInteractionAction.UiAttack;
                    phase = ButtonChannelInteractionSignalPhase.Down;
                    return true;
                case UIInputEventType.AttackHeld:
                    action = ButtonChannelInteractionAction.UiAttack;
                    phase = ButtonChannelInteractionSignalPhase.Held;
                    return true;
                case UIInputEventType.AttackUp:
                    action = ButtonChannelInteractionAction.UiAttack;
                    phase = ButtonChannelInteractionSignalPhase.Up;
                    return true;
                case UIInputEventType.InteractDown:
                    action = ButtonChannelInteractionAction.UiInteract;
                    phase = ButtonChannelInteractionSignalPhase.Down;
                    return true;
                case UIInputEventType.InteractHeld:
                    action = ButtonChannelInteractionAction.UiInteract;
                    phase = ButtonChannelInteractionSignalPhase.Held;
                    return true;
                case UIInputEventType.InteractUp:
                    action = ButtonChannelInteractionAction.UiInteract;
                    phase = ButtonChannelInteractionSignalPhase.Up;
                    return true;
                case UIInputEventType.PauseDown:
                    action = ButtonChannelInteractionAction.UiPause;
                    phase = ButtonChannelInteractionSignalPhase.Down;
                    return true;
                case UIInputEventType.PauseHeld:
                    action = ButtonChannelInteractionAction.UiPause;
                    phase = ButtonChannelInteractionSignalPhase.Held;
                    return true;
                case UIInputEventType.PauseUp:
                    action = ButtonChannelInteractionAction.UiPause;
                    phase = ButtonChannelInteractionSignalPhase.Up;
                    return true;
                case UIInputEventType.RetryDown:
                    action = ButtonChannelInteractionAction.UiRetry;
                    phase = ButtonChannelInteractionSignalPhase.Down;
                    return true;
                case UIInputEventType.RetryHeld:
                    action = ButtonChannelInteractionAction.UiRetry;
                    phase = ButtonChannelInteractionSignalPhase.Held;
                    return true;
                case UIInputEventType.RetryUp:
                    action = ButtonChannelInteractionAction.UiRetry;
                    phase = ButtonChannelInteractionSignalPhase.Up;
                    return true;
                default:
                    return false;
            }
        }

        void ReleaseSelectionBlock()
        {
            _selectionBlockHandle?.Dispose();
            _selectionBlockHandle = null;
            _currentBlockMask = UISelectionBlockMask.None;
        }
    }

    internal sealed class WorldButtonChannelInteractionAdapter : IButtonChannelInteractionAdapter
    {
        readonly Transform _ownerTransform;
        readonly WorldPointerTargetMB _pointerTarget;
        readonly SelectableRuntimeMB? _selectable;
        readonly Func<ButtonChannelInteractionSignal, bool> _dispatch;

        SelectRuntimeManagerMB? _manager;
        IWorldPointerRuntimeService? _pointerService;
        ISelectRuntimeManagerService? _selectService;
        Transform? _lastParent;
        bool _isHovered;
        bool _leftCaptured;
        bool _rightCaptured;

        public WorldButtonChannelInteractionAdapter(
            Transform ownerTransform,
            WorldPointerTargetMB pointerTarget,
            SelectableRuntimeMB? selectable,
            Func<ButtonChannelInteractionSignal, bool> dispatch)
        {
            _ownerTransform = ownerTransform ?? throw new ArgumentNullException(nameof(ownerTransform));
            _pointerTarget = pointerTarget ?? throw new ArgumentNullException(nameof(pointerTarget));
            _selectable = selectable;
            _dispatch = dispatch ?? throw new ArgumentNullException(nameof(dispatch));
        }

        public ButtonChannelAdapterKind AdapterKind => ButtonChannelAdapterKind.World;
        public bool IsAvailable => _pointerService != null && _pointerTarget != null;
        public bool IsSelected => _selectService != null && _selectable != null && ReferenceEquals(_selectService.Current, _selectable);
        public bool IsHovered => _isHovered;
        public bool AllowsDirectPointerPressWithoutSelection => true;
        public IUIElementState? ElementState => null;

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;
            Rebind();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;
            Unbind();
        }

        public void Tick()
        {
            if (_lastParent != _ownerTransform.parent)
                Rebind();
        }

        public void SetBlockMask(UISelectionBlockMask mask)
        {
            _ = mask;
        }

        public void Dispose()
        {
            Unbind();
        }

        void Rebind()
        {
            var nextManager = SelectRuntimeBridgeResolver.FindNearestManager(_ownerTransform);
            if (ReferenceEquals(nextManager, _manager))
            {
                _lastParent = _ownerTransform.parent;
                return;
            }

            Unbind();

            _manager = nextManager;
            _lastParent = _ownerTransform.parent;
            if (_manager == null)
                return;

            SelectRuntimeBridgeResolver.TryResolvePointerService(_manager, out _pointerService);
            SelectRuntimeBridgeResolver.TryResolveManagerService(_manager, out _selectService);

            if (_pointerService != null)
            {
                _pointerService.OnHoveredChanged += HandleHoveredChanged;
                _pointerService.OnFrameUpdated += HandleFrameUpdated;
            }

            if (_selectService != null)
            {
                _selectService.OnSelectionChanged += HandleSelectionChanged;
                _selectService.OnHoveredChanged += HandleManagerHoveredChanged;
            }
        }

        void Unbind()
        {
            if (_pointerService != null)
            {
                _pointerService.OnHoveredChanged -= HandleHoveredChanged;
                _pointerService.OnFrameUpdated -= HandleFrameUpdated;
            }

            if (_selectService != null)
            {
                _selectService.OnSelectionChanged -= HandleSelectionChanged;
                _selectService.OnHoveredChanged -= HandleManagerHoveredChanged;
            }

            _pointerService = null;
            _selectService = null;
            _manager = null;
            _lastParent = null;
            _isHovered = false;
            _leftCaptured = false;
            _rightCaptured = false;
        }

        void HandleHoveredChanged(WorldPointerHoverChangedEventData eventData)
        {
            _isHovered = IsSelfOrDescendant(eventData.CurrentTarget);
        }

        void HandleSelectionChanged(SelectRuntimeSelectionChangedEvent eventData)
        {
            _ = eventData;
        }

        void HandleManagerHoveredChanged(SelectRuntimeHoveredChangedEvent eventData)
        {
            _ = eventData;
        }

        void HandleFrameUpdated(InputFrame frame)
        {
            HandlePointerButton(
                frame.PointerLeft,
                ref _leftCaptured,
                ButtonChannelInteractionAction.PointerLeft,
                frame.PointerScreen,
                frame.DeltaTime);

            HandlePointerButton(
                frame.PointerRight,
                ref _rightCaptured,
                ButtonChannelInteractionAction.PointerRight,
                frame.PointerScreen,
                frame.DeltaTime);
        }

        void HandlePointerButton(
            ButtonState state,
            ref bool captured,
            ButtonChannelInteractionAction action,
            Vector2 pointerScreen,
            float deltaTime)
        {
            if (state.Down && _isHovered)
            {
                captured = true;
                _dispatch(new ButtonChannelInteractionSignal(action, ButtonChannelInteractionSignalPhase.Down, deltaTime, pointerScreen));
            }

            if (state.Held && captured)
            {
                _dispatch(new ButtonChannelInteractionSignal(action, ButtonChannelInteractionSignalPhase.Held, deltaTime, pointerScreen));
            }

            if (!state.Up || !captured)
                return;

            _dispatch(new ButtonChannelInteractionSignal(action, ButtonChannelInteractionSignalPhase.Up, deltaTime, pointerScreen));
            captured = false;
        }

        bool IsSelfOrDescendant(WorldPointerTargetMB? target)
        {
            if (target == null)
                return false;

            var current = target.transform;
            while (current != null)
            {
                if (ReferenceEquals(current, _pointerTarget.transform))
                    return true;

                current = current.parent;
            }

            return false;
        }
    }

    internal sealed class GameRootButtonChannelInteractionAdapter : IButtonChannelInteractionAdapter, IInputConsumer
    {
        readonly IInputRouter _inputRouter;
        readonly Func<ButtonChannelInteractionSignal, bool> _dispatch;

        bool _isRegistered;

        public GameRootButtonChannelInteractionAdapter(
            IInputRouter inputRouter,
            Func<ButtonChannelInteractionSignal, bool> dispatch)
        {
            _inputRouter = inputRouter ?? throw new ArgumentNullException(nameof(inputRouter));
            _dispatch = dispatch ?? throw new ArgumentNullException(nameof(dispatch));
        }

        public ButtonChannelAdapterKind AdapterKind => ButtonChannelAdapterKind.GameRoot;
        public bool IsAvailable => _isRegistered;
        public bool IsSelected => false;
        public bool IsHovered => false;
        public bool AllowsDirectPointerPressWithoutSelection => true;
        public IUIElementState? ElementState => null;
        public InputConsumerPriority Priority => InputConsumerPriority.UIOverlay;

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;
            if (_isRegistered)
                return;

            _inputRouter.RegisterConsumer(this);
            _isRegistered = true;
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;
            if (!_isRegistered)
                return;

            _inputRouter.UnregisterConsumer(this);
            _isRegistered = false;
        }

        public void Tick()
        {
        }

        public void SetBlockMask(UISelectionBlockMask mask)
        {
            _ = mask;
        }

        public void UpdateInput(ref InputFrame frame)
        {
            DispatchButton(frame.Submit, ButtonChannelInteractionAction.UiSubmit, frame.DeltaTime, frame.PointerScreen);
            DispatchButton(frame.Cancel, ButtonChannelInteractionAction.UiCancel, frame.DeltaTime, frame.PointerScreen);
            DispatchButton(frame.Attack, ButtonChannelInteractionAction.UiAttack, frame.DeltaTime, frame.PointerScreen);
            DispatchButton(frame.Interact, ButtonChannelInteractionAction.UiInteract, frame.DeltaTime, frame.PointerScreen);
            DispatchButton(frame.Pause, ButtonChannelInteractionAction.UiPause, frame.DeltaTime, frame.PointerScreen);
            DispatchButton(frame.Retry, ButtonChannelInteractionAction.UiRetry, frame.DeltaTime, frame.PointerScreen);
        }

        void DispatchButton(ButtonState state, ButtonChannelInteractionAction action, float deltaTime, Vector2 pointerPosition)
        {
            if (state.Down)
                _dispatch(new ButtonChannelInteractionSignal(action, ButtonChannelInteractionSignalPhase.Down, deltaTime, pointerPosition));

            if (state.Held)
                _dispatch(new ButtonChannelInteractionSignal(action, ButtonChannelInteractionSignalPhase.Held, deltaTime, pointerPosition));

            if (state.Up)
                _dispatch(new ButtonChannelInteractionSignal(action, ButtonChannelInteractionSignalPhase.Up, deltaTime, pointerPosition));
        }

        public void Dispose()
        {
            if (!_isRegistered)
                return;

            _inputRouter.UnregisterConsumer(this);
            _isRegistered = false;
        }
    }
}
