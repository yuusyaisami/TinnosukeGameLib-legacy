#nullable enable
using System;
using Game.Input;
using Game.SelectRuntime;
using UnityEngine;

namespace Game.UI
{
    internal sealed class WorldPointerSliderInteractionAdapter : ISliderInteractionAdapter
    {
        readonly Transform _ownerTransform;
        readonly WorldPointerTargetMB _pointerTarget;
        readonly SelectableRuntimeMB? _selectable;
        readonly Func<SliderInteractionSignal, bool> _dispatch;

        SelectRuntimeManagerMB? _manager;
        IWorldPointerRuntimeService? _pointerService;
        ISelectRuntimeManagerService? _selectService;
        Transform? _lastParent;
        bool _isHovered;
        bool _leftCaptured;
        bool _rightCaptured;
        WorldPointerEventData _currentHoverData;
        bool _hasCurrentHoverData;

        public WorldPointerSliderInteractionAdapter(
            Transform ownerTransform,
            WorldPointerTargetMB pointerTarget,
            SelectableRuntimeMB? selectable,
            Func<SliderInteractionSignal, bool> dispatch)
        {
            _ownerTransform = ownerTransform ?? throw new ArgumentNullException(nameof(ownerTransform));
            _pointerTarget = pointerTarget ?? throw new ArgumentNullException(nameof(pointerTarget));
            _selectable = selectable;
            _dispatch = dispatch ?? throw new ArgumentNullException(nameof(dispatch));
        }

        public SliderEnvironmentKind EnvironmentKind => SliderEnvironmentKind.World;
        public bool IsAvailable => _pointerService != null;
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

        public bool TryEnsureSelected()
        {
            return IsSelected;
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
                _hasCurrentHoverData = _pointerService.TryGetCurrentHover(out _currentHoverData);
            }

            if (_selectService != null)
            {
                _selectService.OnHoveredChanged += HandleManagerHoveredChanged;
                _selectService.OnSelectionChanged += HandleSelectionChanged;
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
                _selectService.OnHoveredChanged -= HandleManagerHoveredChanged;
                _selectService.OnSelectionChanged -= HandleSelectionChanged;
            }

            _pointerService = null;
            _selectService = null;
            _manager = null;
            _lastParent = null;
            _isHovered = false;
            _leftCaptured = false;
            _rightCaptured = false;
            _hasCurrentHoverData = false;
            _currentHoverData = default;
        }

        void HandleHoveredChanged(WorldPointerHoverChangedEventData eventData)
        {
            _isHovered = IsSelfOrDescendant(eventData.CurrentTarget);
            _currentHoverData = eventData.EventData;
            _hasCurrentHoverData = eventData.CurrentTarget != null;
        }

        void HandleFrameUpdated(InputFrame frame)
        {
            if (_pointerService != null && _pointerService.TryGetCurrentHover(out var hover))
            {
                _currentHoverData = hover;
                _hasCurrentHoverData = true;
            }

            HandlePointerButton(
                frame.PointerLeft,
                ref _leftCaptured,
                SliderInteractionSignalKind.PointerPrimary,
                frame.PointerScreen,
                frame.DeltaTime);
            HandlePointerButton(
                frame.PointerRight,
                ref _rightCaptured,
                SliderInteractionSignalKind.PointerSecondary,
                frame.PointerScreen,
                frame.DeltaTime);
        }

        void HandlePointerButton(
            ButtonState state,
            ref bool captured,
            SliderInteractionSignalKind kind,
            Vector2 pointerScreen,
            float deltaTime)
        {
            if (state.Down && _isHovered)
            {
                captured = true;
                _dispatch(BuildSignal(kind, SliderInteractionSignalPhase.Down, pointerScreen, deltaTime));
            }

            if (state.Held && captured)
                _dispatch(BuildSignal(kind, SliderInteractionSignalPhase.Held, pointerScreen, deltaTime));

            if (!state.Up || !captured)
                return;

            _dispatch(BuildSignal(kind, SliderInteractionSignalPhase.Up, pointerScreen, deltaTime));
            captured = false;
        }

        SliderInteractionSignal BuildSignal(
            SliderInteractionSignalKind kind,
            SliderInteractionSignalPhase phase,
            Vector2 pointerPosition,
            float deltaTime)
        {
            return new SliderInteractionSignal(
                kind,
                phase,
                deltaTime,
                pointerPosition,
                default,
                _currentHoverData.WorldPosition,
                _hasCurrentHoverData);
        }

        void HandleManagerHoveredChanged(SelectRuntimeHoveredChangedEvent eventData)
        {
            _ = eventData;
        }

        void HandleSelectionChanged(SelectRuntimeSelectionChangedEvent eventData)
        {
            _ = eventData;
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
}
