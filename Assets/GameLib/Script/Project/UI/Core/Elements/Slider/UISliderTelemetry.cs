#nullable enable
using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using VContainer;
using Game.Common;
using System.Threading;

namespace Game.UI
{
    public sealed class UISliderTelemetry : IUISliderTelemetry, IScopeAcquireHandler, IScopeReleaseHandler
    {
        const float LongPressThresholdSeconds = 0.5f;

        readonly IScopeNode _owner;
        readonly IUISliderOutput _output;

        CancellationTokenSource? _ctsLongPress;
        bool _isPointerDown;
        bool _isLongPressed;
        Vector2 _lastPointerPos;
        UISliderInteractionEventKind _lastEvent = UISliderInteractionEventKind.None;

        public event Action<UISliderTelemetrySnapshot>? OnTelemetryUpdated;

        public UISliderTelemetry(IScopeNode owner, IUISliderOutput output)
        {
            _owner = owner;
            _output = output;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            //Debug.Log($"[UISliderTelemetry] OnAcquire for scope={scope?.GetPathFromRoot()?.Count}");
            _output.OnUpdated += HandleOutputUpdated;
            Publish();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            //Debug.Log($"[UISliderTelemetry] OnRelease for scope={scope?.GetPathFromRoot()?.Count}");
            _output.OnUpdated -= HandleOutputUpdated;
            CancelLongPress();
            _isPointerDown = false;
            _isLongPressed = false;
            Publish();
        }

        void HandleOutputUpdated(UISliderOutputSnapshot s)
        {
            // When editing changes or values change, publish snapshot
            Publish();
        }

        void Publish()
        {
            var ts = DateTime.UtcNow;
            var snap = new UISliderTelemetrySnapshot(
                _lastEvent,
                _lastPointerPos,
                _output.NormalizedValue,
                _output.RawValue,
                _output.IsEditing,
                _isPointerDown,
                _isLongPressed,
                ts.ToOADate());

            OnTelemetryUpdated?.Invoke(snap);
        }

        public void NotifyPointerDown(Vector2 screenPosition)
        {
            _lastEvent = UISliderInteractionEventKind.PointerDown;
            _lastPointerPos = screenPosition;
            _isPointerDown = true;
            _isLongPressed = false;
            StartLongPressWatcher();
            Publish();
        }

        public void NotifyPointerMove(Vector2 screenPosition)
        {
            _lastEvent = UISliderInteractionEventKind.PointerMove;
            _lastPointerPos = screenPosition;
            Publish();
        }

        public void NotifyPointerUp()
        {
            _lastEvent = _isLongPressed ? UISliderInteractionEventKind.LongPressEnd : UISliderInteractionEventKind.PointerUp;
            _isPointerDown = false;
            CancelLongPress();
            _isLongPressed = false;
            Publish();
        }

        void StartLongPressWatcher()
        {
            CancelLongPress();
            _ctsLongPress = new CancellationTokenSource();
            var token = _ctsLongPress.Token;
            LongPressRoutineAsync(token).Forget();
        }

        void CancelLongPress()
        {
            if (_ctsLongPress != null)
            {
                _ctsLongPress.Cancel();
                _ctsLongPress.Dispose();
                _ctsLongPress = null;
            }
        }

        async UniTask LongPressRoutineAsync(CancellationToken token)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(LongPressThresholdSeconds), cancellationToken: token);
                if (token.IsCancellationRequested)
                    return;

                if (_isPointerDown)
                {
                    _isLongPressed = true;
                    _lastEvent = UISliderInteractionEventKind.LongPressStart;
                    Publish();
                }
            }
            catch (OperationCanceledException)
            {
                // ignored
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
    }
}
