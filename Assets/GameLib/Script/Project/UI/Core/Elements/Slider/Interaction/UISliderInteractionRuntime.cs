#nullable enable
using System;
using Game.Commands.VNext;
using Game.Common;
using UnityEngine;

namespace Game.UI
{
    internal sealed class UISliderInteractionRuntime : ISliderInteractionRuntime
    {
        readonly IScopeNode _owner;
        readonly ISliderOptions _options;
        readonly ISliderPlayerRuntime _player;
        readonly ISliderRuntimePresetProvider _presetProvider;
        readonly Canvas _canvas;

        IScopeNode? _activeScope;
        ISliderInteractionAdapter? _adapter;
        ActorSourceResolveCache _areaActorSourceCache;
        bool _pointerCaptureRequested;
        float _nextNavigateTime;
        float _nextScrollTime;
        int _lastNavigateStep;
        int _lastScrollStep;

        public UISliderInteractionRuntime(
            IScopeNode owner,
            ISliderOptions options,
            ISliderPlayerRuntime player,
            ISliderRuntimePresetProvider presetProvider,
            Canvas canvas)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _presetProvider = presetProvider ?? throw new ArgumentNullException(nameof(presetProvider));
            _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
        }

        public UISelectionBlockMask DesiredSelectionBlockMask => _player.IsInteracting ? UISelectionBlockMask.All : UISelectionBlockMask.None;

        public void BindAdapter(ISliderInteractionAdapter? adapter)
        {
            _adapter = adapter;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _ = isReset;
            _activeScope = scope;
            _areaActorSourceCache = default;
            ResetTransientState();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;
            _activeScope = null;
            _areaActorSourceCache = default;
            ResetTransientState();
        }

        public void Tick()
        {
            if (_player.IsInteracting && _adapter != null && !_adapter.IsSelected)
                _player.RequestEndInteraction(SliderInteractionEndReason.SelectionLost);

            if (!_player.IsInteracting)
                ResetTransientState();
        }

        public bool HandleSignal(SliderInteractionSignal signal)
        {
            if (_adapter == null || !_adapter.IsAvailable || _activeScope == null)
                return false;

            if (!CanProcessInput())
                return false;

            switch (signal.Kind)
            {
                case SliderInteractionSignalKind.Submit:
                    return HandleSubmit(signal);
                case SliderInteractionSignalKind.Cancel:
                    return HandleCancel(signal);
                case SliderInteractionSignalKind.Navigate:
                    return HandleNavigate(signal);
                case SliderInteractionSignalKind.Scroll:
                    return HandleScroll(signal);
                case SliderInteractionSignalKind.PointerMove:
                    return HandlePointerMove(signal);
                default:
                    return false;
            }
        }

        bool CanProcessInput()
        {
            if (_player.UIInputMode == SliderUIInputMode.None || !_player.IsUserInputEnabled)
                return false;

            var elementState = _adapter?.ElementState;
            if (elementState != null)
            {
                if (!elementState.IsEffectivelyActive)
                    return false;
                if (!elementState.IsVisible)
                    return false;
            }

            return true;
        }

        bool HandleSubmit(SliderInteractionSignal signal)
        {
            if (signal.Phase == SliderInteractionSignalPhase.Up)
            {
                if (_pointerCaptureRequested && _player.UIInputMode == SliderUIInputMode.PointerCapture)
                {
                    _pointerCaptureRequested = false;
                    _player.RequestEndInteraction(SliderInteractionEndReason.PointerUp);
                    return true;
                }

                return _player.IsInteracting;
            }

            if (signal.Phase != SliderInteractionSignalPhase.Down)
                return false;

            var pointerOver = TryResolveBoundaryIndexFromPointer(signal.PointerPosition, out var pointerIndex, out _);
            if (pointerOver)
            {
                if (!_adapter!.TryEnsureSelected())
                    return false;

                if (_player.UIInputMode == SliderUIInputMode.PointerCapture)
                {
                    _pointerCaptureRequested = _player.RequestBeginInteraction();
                    if (_pointerCaptureRequested)
                        _player.RequestBoundaryIndex(pointerIndex, SliderChangeSource.UserPointer);
                    return _pointerCaptureRequested;
                }

                if (_player.UIInputMode == SliderUIInputMode.SubmitToggle)
                {
                    if (_player.IsInteracting)
                    {
                        _player.RequestEndInteraction(SliderInteractionEndReason.SubmitToggle);
                        return true;
                    }

                    if (_player.RequestBeginInteraction())
                    {
                        _player.RequestBoundaryIndex(pointerIndex, SliderChangeSource.UserPointer);
                        return true;
                    }

                    return false;
                }
            }

            if (!_adapter!.IsSelected)
                return false;

            if (_player.UIInputMode != SliderUIInputMode.SubmitToggle)
                return false;

            if (_player.IsInteracting)
            {
                _player.RequestEndInteraction(SliderInteractionEndReason.SubmitToggle);
                return true;
            }

            return _player.RequestBeginInteraction();
        }

        bool HandlePointerMove(SliderInteractionSignal signal)
        {
            if (_player.UIInputMode != SliderUIInputMode.PointerCapture ||
                !_player.IsInteracting ||
                !_pointerCaptureRequested)
            {
                return false;
            }

            if (!TryResolveBoundaryIndexFromPointer(signal.PointerPosition, out var index, out _))
                return true;

            _player.RequestBoundaryIndex(index, SliderChangeSource.UserPointer);
            return true;
        }

        bool HandleNavigate(SliderInteractionSignal signal)
        {
            if (!_player.IsInteracting)
                return false;

            var step = ResolveStep(signal.Direction);
            if (step == 0)
            {
                _lastNavigateStep = 0;
                return true;
            }

            if (!TryConsumeRepeat(ref _nextNavigateTime, ref _lastNavigateStep, step, _player.NavigateRepeatDelay, _player.NavigateRepeatInterval))
                return true;

            var nextIndex = Mathf.Clamp(_player.CurrentBoundaryIndex + step, 0, Mathf.Max(0, _player.BoundaryCount - 1));
            _player.RequestBoundaryIndex(nextIndex, SliderChangeSource.UserNavigate);
            return true;
        }

        bool HandleScroll(SliderInteractionSignal signal)
        {
            if (!_player.IsInteracting)
                return false;

            var step = ResolveStep(signal.Direction);
            if (step == 0)
            {
                _lastScrollStep = 0;
                return true;
            }

            if (!TryConsumeRepeat(ref _nextScrollTime, ref _lastScrollStep, step, _player.ScrollRepeatDelay, _player.ScrollRepeatInterval))
                return true;

            var nextIndex = Mathf.Clamp(_player.CurrentBoundaryIndex + step, 0, Mathf.Max(0, _player.BoundaryCount - 1));
            _player.RequestBoundaryIndex(nextIndex, SliderChangeSource.UserNavigate);
            return true;
        }

        bool HandleCancel(SliderInteractionSignal signal)
        {
            if (signal.Phase != SliderInteractionSignalPhase.Down || !_player.IsInteracting)
                return false;

            _player.RequestEndInteraction(SliderInteractionEndReason.Cancel);
            return true;
        }

        int ResolveStep(Vector2 direction)
        {
            var segmented = _presetProvider.CurrentVisualizerPreset.Segmented;
            var axisValue = segmented.FillAxis == SliderAreaFillAxis.SizeX ? direction.x : direction.y;
            if (Mathf.Abs(axisValue) < 0.01f)
                return 0;

            var step = axisValue > 0f ? 1 : -1;
            if (segmented.OriginSide == SliderAreaOriginSide.Max)
                step = -step;

            return step;
        }

        bool TryResolveBoundaryIndexFromPointer(Vector2 screenPosition, out int boundaryIndex, out bool pointerInside)
        {
            boundaryIndex = 0;
            pointerInside = false;
            if (_activeScope == null || _player.BoundaryCount <= 0)
                return false;

            var status = SliderRuntimeHelpers.TryResolveScreenRangeSnapshot(
                _activeScope,
                _options,
                _canvas,
                ref _areaActorSourceCache,
                out var rangeSnapshot);
            if (status != SliderRangeResolveStatus.Success)
                return false;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rangeSnapshot.CanvasRect,
                    screenPosition,
                    rangeSnapshot.UICamera,
                    out var canvasLocal))
            {
                return false;
            }

            pointerInside = rangeSnapshot.LocalRect.Contains(canvasLocal);
            if (!SliderRuntimeHelpers.TryMapCanvasLocalToNormalized(
                    rangeSnapshot.LocalRect,
                    _presetProvider.CurrentVisualizerPreset.Segmented.FillAxis,
                    _presetProvider.CurrentVisualizerPreset.Segmented.OriginSide,
                    _player.PaddingStart,
                    _player.PaddingEnd,
                    canvasLocal,
                    out var normalizedValue))
            {
                return false;
            }

            boundaryIndex = _player.ResolveNearestBoundaryIndex(normalizedValue);
            return true;
        }

        void ResetTransientState()
        {
            _pointerCaptureRequested = false;
            _nextNavigateTime = 0f;
            _nextScrollTime = 0f;
            _lastNavigateStep = 0;
            _lastScrollStep = 0;
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
