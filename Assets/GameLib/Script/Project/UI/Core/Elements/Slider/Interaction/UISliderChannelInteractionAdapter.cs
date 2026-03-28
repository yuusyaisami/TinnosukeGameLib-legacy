#nullable enable
using System;
using Game.Common;

namespace Game.UI
{
    internal sealed class UISliderChannelInteractionAdapter : ISliderInteractionAdapter, IUIInputConsumer
    {
        readonly IScopeNode _owner;
        readonly IUIInputConsumerHub _consumerHub;
        readonly IUIElementState _elementState;
        readonly IUISelectionState _selectionState;
        readonly IUISelectionNavigation _selectionNavigation;
        readonly IUISelectionBlockService? _selectionBlockService;
        readonly Func<SliderInteractionSignal, bool> _dispatch;

        IDisposable? _selectionBlockHandle;
        UISelectionBlockMask _currentBlockMask;

        public UISliderChannelInteractionAdapter(
            IScopeNode owner,
            IUIInputConsumerHub consumerHub,
            IUIElementState elementState,
            IUISelectionState selectionState,
            IUISelectionNavigation selectionNavigation,
            IUISelectionBlockService? selectionBlockService,
            Func<SliderInteractionSignal, bool> dispatch)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _consumerHub = consumerHub ?? throw new ArgumentNullException(nameof(consumerHub));
            _elementState = elementState ?? throw new ArgumentNullException(nameof(elementState));
            _selectionState = selectionState ?? throw new ArgumentNullException(nameof(selectionState));
            _selectionNavigation = selectionNavigation ?? throw new ArgumentNullException(nameof(selectionNavigation));
            _selectionBlockService = selectionBlockService;
            _dispatch = dispatch ?? throw new ArgumentNullException(nameof(dispatch));
        }

        public int Priority => 90;
        public SliderEnvironmentKind EnvironmentKind => SliderEnvironmentKind.ScreenUI;
        public bool IsAvailable => true;
        public bool IsSelected => ReferenceEquals(_selectionState.CurrentElement, _owner);
        public bool IsHovered => ReferenceEquals(_selectionState.HoveredElement, _owner);
        public bool AllowsDirectPointerPressWithoutSelection => false;
        public IUIElementState? ElementState => _elementState;

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

        public bool TryEnsureSelected()
        {
            return IsSelected || _selectionNavigation.TrySelect(_owner);
        }

        public bool Consume(in UIInputEvent e)
        {
            if (!TryTranslate(in e, out var signal))
                return false;

            return _dispatch(signal);
        }

        public void Dispose()
        {
            ReleaseSelectionBlock();
        }

        static bool TryTranslate(in UIInputEvent e, out SliderInteractionSignal signal)
        {
            signal = default;
            switch (e.Type)
            {
                case UIInputEventType.SubmitDown:
                    signal = new SliderInteractionSignal(SliderInteractionSignalKind.Submit, SliderInteractionSignalPhase.Down, e.DeltaTime, e.PointerPosition, default, default, false);
                    return true;
                case UIInputEventType.SubmitHeld:
                    signal = new SliderInteractionSignal(SliderInteractionSignalKind.Submit, SliderInteractionSignalPhase.Held, e.DeltaTime, e.PointerPosition, default, default, false);
                    return true;
                case UIInputEventType.SubmitUp:
                    signal = new SliderInteractionSignal(SliderInteractionSignalKind.Submit, SliderInteractionSignalPhase.Up, e.DeltaTime, e.PointerPosition, default, default, false);
                    return true;
                case UIInputEventType.CancelDown:
                    signal = new SliderInteractionSignal(SliderInteractionSignalKind.Cancel, SliderInteractionSignalPhase.Down, e.DeltaTime, e.PointerPosition, default, default, false);
                    return true;
                case UIInputEventType.Navigate:
                    signal = new SliderInteractionSignal(SliderInteractionSignalKind.Navigate, SliderInteractionSignalPhase.Instant, e.DeltaTime, e.PointerPosition, e.Direction, default, false);
                    return true;
                case UIInputEventType.Scroll:
                    signal = new SliderInteractionSignal(SliderInteractionSignalKind.Scroll, SliderInteractionSignalPhase.Instant, e.DeltaTime, e.PointerPosition, e.Direction, default, false);
                    return true;
                case UIInputEventType.PointerMove:
                    signal = new SliderInteractionSignal(SliderInteractionSignalKind.PointerMove, SliderInteractionSignalPhase.Instant, e.DeltaTime, e.PointerPosition, default, default, false);
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
}
