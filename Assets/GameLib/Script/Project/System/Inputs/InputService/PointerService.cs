using UnityEngine;
using UnityEngine.InputSystem;
using VContainer.Unity;

namespace Game.Input
{
    /// <summary>
    /// 現在のコントロールスキームに応じたポインタ座標を提供するサービス。
    /// Gamepad の場合は仮想カーソルを管理する。
    /// </summary>
    public sealed class PointerService : IPointerService, ITickable
    {
        readonly IControlSchemeService _schemeService;
        readonly PointerState _pointer;

        public PointerService(IControlSchemeService schemeService)
        {
            _schemeService = schemeService;
            _pointer = new PointerState(Screen.width, Screen.height);
        }

        public Vector2 PointerScreen()
        {
            return _pointer.GetPointerScreen(_schemeService.CurrentScheme);
        }

        public Vector2 PointerWorld(Camera cam, float worldZ = 0f)
        {
            var s = PointerScreen();

            if (cam.orthographic)
            {
                var w = cam.ScreenToWorldPoint(new Vector3(s.x, s.y, 0f));
                w.z = worldZ;
                return w;
            }
            else
            {
                float z = Mathf.Abs(worldZ - cam.transform.position.z);
                var w = cam.ScreenToWorldPoint(new Vector3(s.x, s.y, z));
                w.z = worldZ;
                return w;
            }
        }

        public void Tick()
        {
            _pointer.Tick(_schemeService.CurrentScheme);
        }

        public void NotePointerAction()
        {
            _pointer.NotePointerAction();
            _schemeService.NotePointerActivity();
        }

        public bool RegisterPointerActivity(Vector2 position, float threshold = 1f)
        {
            var moved = _pointer.RegisterPointerActivity(position, threshold);
            if (moved)
            {
                _schemeService.NotePointerActivity();
            }
            return moved;
        }

        public bool HasRecentPointerActivity(float timeout = 0.2f)
        {
            return _pointer.HasRecentPointerActivity(timeout);
        }

        // ===== 内部状態 =====
        sealed class PointerState
        {
            readonly int _screenW;
            readonly int _screenH;

            readonly float _virtualCursorSpeed = 1800f;
            Vector2 _virtualCursor;
            Vector2 _lastTouchPos;

            readonly float _pointerActivityTimeout = 0.25f;
            readonly float _pointerMovementThreshold = 1f;
            Vector2 _lastPointerSample;
            bool _pointerSampled;
            float _lastPointerMoveTime = float.NegativeInfinity;

            public PointerState(int screenW, int screenH)
            {
                _screenW = Mathf.Max(1, screenW);
                _screenH = Mathf.Max(1, screenH);
                _virtualCursor = new Vector2(_screenW * 0.5f, _screenH * 0.5f);
            }

            public Vector2 GetPointerScreen(ControlScheme scheme)
            {
                if (scheme == ControlScheme.Touch)
                {
                    var ts = Touchscreen.current;
                    if (ts != null)
                    {
                        var t = ts.primaryTouch;
                        if (t.press.isPressed)
                            _lastTouchPos = t.position.ReadValue();

                        return _lastTouchPos;
                    }
                }
                else if (IsGamepadScheme(scheme))
                {
                    return _virtualCursor;
                }
                else
                {
                    var m = Mouse.current;
                    if (m != null)
                        return m.position.ReadValue();

                    var pen = Pen.current;
                    if (pen != null)
                        return pen.position.ReadValue();
                }

                return _virtualCursor;
            }

            public void Tick(ControlScheme scheme)
            {
                if (!IsGamepadScheme(scheme))
                    return;

                var gamepad = Gamepad.current;
                if (gamepad == null)
                    return;

                var delta = gamepad.rightStick.ReadValue();
                if (delta.sqrMagnitude <= 0f)
                    return;

                _virtualCursor += delta * _virtualCursorSpeed * Time.unscaledDeltaTime;
                _virtualCursor.x = Mathf.Clamp(_virtualCursor.x, 0f, _screenW - 1);
                _virtualCursor.y = Mathf.Clamp(_virtualCursor.y, 0f, _screenH - 1);
            }

            public void NotePointerAction()
            {
                _lastPointerMoveTime = Time.unscaledTime;
            }

            public bool RegisterPointerActivity(Vector2 position, float threshold)
            {
                var now = Time.unscaledTime;

                if (!_pointerSampled)
                {
                    _lastPointerSample = position;
                    _pointerSampled = true;
                    return false;
                }

                var movementThreshold = threshold > 0f ? threshold : _pointerMovementThreshold;
                var delta = position - _lastPointerSample;
                var moved = delta.sqrMagnitude >= movementThreshold * movementThreshold;

                _lastPointerSample = position;

                if (moved)
                {
                    _lastPointerMoveTime = now;
                }

                return moved;
            }

            public bool HasRecentPointerActivity(float timeout)
            {
                var limit = timeout > 0f ? timeout : _pointerActivityTimeout;
                return Time.unscaledTime - _lastPointerMoveTime <= limit;
            }

            static bool IsGamepadScheme(ControlScheme scheme)
            {
                return scheme == ControlScheme.Gamepad
                       || scheme == ControlScheme.GamepadXbox
                       || scheme == ControlScheme.GamepadPlayStation
                       || scheme == ControlScheme.GamepadSwitch;
            }
        }
    }
}
