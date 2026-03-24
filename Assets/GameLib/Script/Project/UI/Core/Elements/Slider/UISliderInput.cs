#nullable enable
using System;
using UnityEngine;
using Game.Input;
using VContainer;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class UISliderInput
        : IUIInputConsumer,
          IScopeAcquireHandler,
          IScopeReleaseHandler
    {
        const int SliderInputPriority = 90;

        readonly IScopeNode _owner;
        readonly IUISliderController _controller;
        readonly IUISliderOutput _output;
        readonly IUISliderInputOptions _inputOptions;
        readonly IUISliderValueOptions _valueOptions;

        readonly IUIInputConsumerHub? _consumerHub;
        readonly IUIElementState? _elementState;
        readonly IUISelectionState? _selectionState;
        readonly IUISelectionNavigation? _selectionNavigation;
        readonly IUISelectionBlockService? _selectionBlockService;
        readonly IUISliderTelemetry? _telemetry;

        IDisposable? _selectionBlock;

        bool _pointerCaptureRequested;
        bool _selectionSubscribed;
        bool _outputSubscribed;

        Vector2 _handleOffsetLocalInHandle;
        float _nextNavigateTime;
        float _nextScrollTime;
        int _lastNavigateStep;
        int _lastScrollStep;

        public int Priority => SliderInputPriority;

        public UISliderInput(
            IScopeNode owner,
            IUISliderController controller,
            IUISliderOutput output,
            IUISliderInputOptions inputOptions,
            IUISliderValueOptions valueOptions,
            IUIInputConsumerHub? consumerHub = null,
            IUIElementState? elementState = null,
            IUISelectionState? selectionState = null,
            IUISelectionNavigation? selectionNavigation = null,
            IUISelectionBlockService? selectionBlockService = null,
            IUISliderTelemetry? telemetry = null)
        {
            _owner = owner;
            _controller = controller;
            _output = output;
            _inputOptions = inputOptions;
            _valueOptions = valueOptions;
            _consumerHub = consumerHub;
            _elementState = elementState;
            _selectionState = selectionState;
            _selectionNavigation = selectionNavigation;
            _selectionBlockService = selectionBlockService;
            _telemetry = telemetry;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _consumerHub?.Register(this);
            SubscribeSelection();
            SubscribeOutput();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _consumerHub?.Unregister(this);
            UnsubscribeSelection();
            UnsubscribeOutput();
            _pointerCaptureRequested = false;
            ReleaseSelectionBlock();
        }

        public bool Consume(in UIInputEvent e)
        {
            if (!CanProcessInput())
                return false;

            switch (e.Type)
            {
                case UIInputEventType.SubmitDown:
                    return HandleSubmitDown(e);

                case UIInputEventType.SubmitUp:
                    return HandleSubmitUp(e);

                case UIInputEventType.Navigate:
                    return HandleNavigate(e);

                case UIInputEventType.Scroll:
                    return HandleScroll(e);

                case UIInputEventType.PointerMove:
                    return HandlePointerMove(e);

                case UIInputEventType.CancelDown:
                    return HandleCancel();

                default:
                    return false;
            }
        }

        bool CanProcessInput()
        {
            if (_inputOptions.InputMode == UISliderInputMode.None)
                return false;

            if (!_valueOptions.IsEditable)
                return false;

            if (_elementState != null)
            {
                if (!_elementState.IsEffectivelyActive) return false;
                if (!_elementState.IsVisible) return false;
            }

            return true;
        }

        bool HandleSubmitDown(in UIInputEvent e)
        {
            bool pointerOver = IsPointerOverHitRect(e.PointerPosition);
            bool pointerUsage = IsPointerUsage(e.UsageMode, pointerOver);

            if (pointerUsage && pointerOver)
            {
                if (!EnsureSelected())
                    return false;

                if (_inputOptions.InputMode == UISliderInputMode.PointerCapture)
                {
                    _telemetry?.NotifyPointerDown(e.PointerPosition);
                    _controller.RequestBeginEdit(UISliderEditMode.PointerCapture);
                    EnsureSelectionBlock();
                    _pointerCaptureRequested = true;

                    var slider = ResolveUnitySlider();
                    var cam = ResolveUICamera(_inputOptions.HitTestRect ?? _inputOptions.TrackRect);
                    _handleOffsetLocalInHandle = UISliderUnityGeometry.ComputeHandleOffsetLocalInHandle(slider, e.PointerPosition, cam);

                    if (TryGetNormalizedFromPointer(e.PointerPosition, allowOutsideWhenCapturing: true, out var normalized))
                        _controller.RequestSetNormalized(normalized, UISliderChangeSource.UserPointer);

                    return true;
                }

                if (_inputOptions.InputMode == UISliderInputMode.SubmitToggle)
                {
                    _telemetry?.NotifyPointerDown(e.PointerPosition);
                    ToggleEdit(UISliderEditMode.SubmitToggle);
                    EnsureSelectionBlock();

                    if (_output.IsEditing)
                    {
                        var slider = ResolveUnitySlider();
                        var cam = ResolveUICamera(_inputOptions.HitTestRect ?? _inputOptions.TrackRect);
                        _handleOffsetLocalInHandle = UISliderUnityGeometry.ComputeHandleOffsetLocalInHandle(slider, e.PointerPosition, cam);

                        if (TryGetNormalizedFromPointer(e.PointerPosition, allowOutsideWhenCapturing: true, out var normalized))
                            _controller.RequestSetNormalized(normalized, UISliderChangeSource.UserPointer);
                    }

                    return true;
                }
            }

            if (!IsSelected())
                return false;

            ToggleEdit(UISliderEditMode.SubmitToggle);
            EnsureSelectionBlock();
            return true;
        }

        bool HandleSubmitUp(in UIInputEvent e)
        {
            if (!_output.IsEditing)
                return false;

            if (_pointerCaptureRequested && _inputOptions.InputMode == UISliderInputMode.PointerCapture)
            {
                _pointerCaptureRequested = false;
                _telemetry?.NotifyPointerUp();
                _controller.RequestEndEdit(UISliderEndEditReason.PointerUp);
                EnsureSelectionBlock();
                return true;
            }

            if (e.UsageMode == InputUsageMode.Pointer)
                _telemetry?.NotifyPointerUp();

            return true;
        }

        bool HandlePointerMove(in UIInputEvent e)
        {
            if (!_output.IsEditing)
                return false;

            _telemetry?.NotifyPointerMove(e.PointerPosition);

            if (_inputOptions.InputMode != UISliderInputMode.PointerCapture)
                return false;

            if (!_pointerCaptureRequested)
            {
                // Debug.Log($"[UISliderInput] HandlePointerMove: captureRequested={_pointerCaptureRequested} mode={_inputOptions.InputMode} pointer={e.PointerPosition} selected={IsSelected()} OverHitRect={IsPointerOverHitRect(e.PointerPosition)}");
                if (!IsPointerOverHitRect(e.PointerPosition))
                    return true;

                _pointerCaptureRequested = true;
                _telemetry?.NotifyPointerDown(e.PointerPosition);

                var slider = ResolveUnitySlider();
                var cam = ResolveUICamera(_inputOptions.HitTestRect ?? _inputOptions.TrackRect);
                _handleOffsetLocalInHandle = UISliderUnityGeometry.ComputeHandleOffsetLocalInHandle(slider, e.PointerPosition, cam);
            }

            if (TryGetNormalizedFromPointer(e.PointerPosition, allowOutsideWhenCapturing: true, out var normalized))
                _controller.RequestSetNormalized(normalized, UISliderChangeSource.UserPointer);

            return true;
        }

        bool HandleNavigate(in UIInputEvent e)
        {
            if (!_output.IsEditing)
                return false;

            int step = StepFromVector(e.Direction);
            if (step == 0)
            {
                _lastNavigateStep = 0;
                return true;
            }

            if (!TryConsumeRepeat(ref _nextNavigateTime, ref _lastNavigateStep, step, _inputOptions.NavigateRepeatDelay, _inputOptions.NavigateRepeatInterval))
                return true;

            _controller.RequestStep(step, UISliderChangeSource.UserNavigate);
            return true;
        }

        bool HandleScroll(in UIInputEvent e)
        {
            if (!_output.IsEditing)
                return false;

            int step = StepFromVector(e.Direction);
            if (step == 0)
            {
                _lastScrollStep = 0;
                return true;
            }

            if (!TryConsumeRepeat(ref _nextScrollTime, ref _lastScrollStep, step, _inputOptions.ScrollRepeatDelay, _inputOptions.ScrollRepeatInterval))
                return true;

            _controller.RequestStep(step, UISliderChangeSource.UserNavigate);
            return true;
        }

        bool HandleCancel()
        {
            if (!_output.IsEditing)
                return false;

            _controller.RequestEndEdit(UISliderEndEditReason.Cancel);
            EnsureSelectionBlock();
            return true;
        }

        int StepFromVector(Vector2 direction)
        {
            float value = _inputOptions.Axis == UISliderAxis.Horizontal ? direction.x : direction.y;
            if (Mathf.Abs(value) < 0.01f)
                return 0;

            int step = value > 0f ? 1 : -1;
            if (IsReversed(_inputOptions.Axis, _inputOptions.Direction))
                step = -step;

            return step;
        }

        bool EnsureSelected()
        {
            if (IsSelected())
                return true;

            return _selectionNavigation != null && _selectionNavigation.TrySelect(_owner);
        }

        bool IsSelected()
            => _selectionState != null && ReferenceEquals(_selectionState.CurrentElement, _owner);

        bool IsPointerUsage(InputUsageMode usage, bool pointerOver)
            => usage == InputUsageMode.Pointer || pointerOver;

        bool IsPointerOverHitRect(Vector2 screenPos)
        {
            var hit = _inputOptions.HitTestRect ?? _inputOptions.TrackRect;
            var cam = ResolveUICamera(hit);
            return RectTransformUtility.RectangleContainsScreenPoint(hit, screenPos, cam);
        }

        bool TryGetNormalizedFromPointer(Vector2 screenPos, bool allowOutsideWhenCapturing, out float normalized)
        {
            normalized = 0f;

            var hit = _inputOptions.HitTestRect ?? _inputOptions.TrackRect;
            var cam = ResolveUICamera(hit);

            if (!allowOutsideWhenCapturing)
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(hit, screenPos, cam))
                    return false;
            }

            var slider = ResolveUnitySlider();
            Vector2? offset = (_pointerCaptureRequested ? _handleOffsetLocalInHandle : (Vector2?)null);

            return UISliderUnityGeometry.TryScreenToNormalized(slider, screenPos, cam, offset, out normalized);
        }

        Slider ResolveUnitySlider()
        {
            var rt = _inputOptions.TrackRect;
            var s = rt.GetComponentInParent<Slider>();
            if (s == null)
                throw new InvalidOperationException("UISliderInput: UnityEngine.UI.Slider not found in parents.");
            return s;
        }

        Camera? ResolveUICamera(RectTransform anyUI)
        {
            var canvas = anyUI.GetComponentInParent<Canvas>();
            if (canvas == null)
                return _inputOptions.UICamera;

            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return null;

            return canvas.worldCamera != null ? canvas.worldCamera : _inputOptions.UICamera;
        }

        void ToggleEdit(UISliderEditMode mode)
        {
            if (_output.IsEditing)
            {
                _controller.RequestEndEdit(UISliderEndEditReason.SubmitToggle);
                return;
            }

            _controller.RequestBeginEdit(mode);
        }

        void HandleSelectionChanged(IScopeNode? newSelection)
        {
            if (_output.IsEditing && !ReferenceEquals(newSelection, _owner))
            {
                _controller.RequestEndEdit(UISliderEndEditReason.SelectionLost);
                EnsureSelectionBlock();
            }
        }

        void HandleOutputUpdated(UISliderOutputSnapshot snapshot)
        {
            if (!snapshot.IsEditing)
            {
                _pointerCaptureRequested = false;
                _lastNavigateStep = 0;
                _lastScrollStep = 0;
                _nextNavigateTime = 0f;
                _nextScrollTime = 0f;
            }

            EnsureSelectionBlock();
        }

        void EnsureSelectionBlock()
        {
            if (_selectionBlockService == null)
                return;

            if (_output.IsEditing)
                _selectionBlock ??= _selectionBlockService.AcquireBlock(this, UISelectionBlockMask.All);
            else
                ReleaseSelectionBlock();
        }

        void ReleaseSelectionBlock()
        {
            _selectionBlock?.Dispose();
            _selectionBlock = null;
        }

        void SubscribeSelection()
        {
            if (_selectionState == null || _selectionSubscribed)
                return;

            _selectionState.OnSelectionChanged += HandleSelectionChanged;
            _selectionSubscribed = true;
        }

        void UnsubscribeSelection()
        {
            if (_selectionState == null || !_selectionSubscribed)
                return;

            _selectionState.OnSelectionChanged -= HandleSelectionChanged;
            _selectionSubscribed = false;
        }

        void SubscribeOutput()
        {
            if (_outputSubscribed)
                return;

            _output.OnUpdated += HandleOutputUpdated;
            _outputSubscribed = true;
        }

        void UnsubscribeOutput()
        {
            if (!_outputSubscribed)
                return;

            _output.OnUpdated -= HandleOutputUpdated;
            _outputSubscribed = false;
        }

        static bool IsReversed(UISliderAxis axis, UISliderDirection direction)
        {
            if (axis == UISliderAxis.Horizontal)
                return direction == UISliderDirection.RightToLeft;

            return direction == UISliderDirection.TopToBottom;
        }

        static bool TryConsumeRepeat(ref float nextTime, ref int lastStep, int step, float delay, float interval)
        {
            var now = Time.unscaledTime;
            delay = Mathf.Max(0f, delay);
            interval = Mathf.Max(0.001f, interval);

            if (step != lastStep)
            {
                lastStep = step;
                nextTime = now + delay;
                return true;
            }

            if (now < nextTime)
                return false;

            nextTime = now + interval;
            return true;
        }
    }
}
