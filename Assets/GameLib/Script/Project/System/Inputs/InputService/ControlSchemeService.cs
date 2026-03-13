using System;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using VContainer.Unity;
using UISPlayerInput = UnityEngine.InputSystem.PlayerInput;

namespace Game.Input
{
    /// <summary>
    /// 現在の ControlScheme / InputUsageMode を管理するサービス。
    /// PlayerInput(MB) があればそれを監視し、無ければパッシブ推定。
    /// </summary>
    public sealed class ControlSchemeService : IControlSchemeService, ITickable, IDisposable
    {
        readonly UISPlayerInput _schemeWatcher;

        public ControlScheme CurrentScheme { get; private set; } = ControlScheme.Unknown;
        public InputUsageMode CurrentUsageMode { get; private set; } = InputUsageMode.Unknown;

        public event Action<ControlScheme> OnSchemeChanged;
        public event Action<InputUsageMode> OnUsageModeChanged;

        ControlScheme _lastGamepadScheme = ControlScheme.Gamepad;

        public ControlSchemeService(IObjectResolver resolver)
        {
            if (resolver != null && resolver.TryResolve(out UISPlayerInput watcher))
            {
                _schemeWatcher = watcher;
                _schemeWatcher.onControlsChanged += OnControlsChanged;
            }

            InitializeInitialScheme();
            InitializeUsageFromScheme();
        }

        public void Dispose()
        {
            if (_schemeWatcher != null)
            {
                _schemeWatcher.onControlsChanged -= OnControlsChanged;
            }
        }

        // ========== IControlSchemeService ==========

        public void NoteNavigationActivity(ControlScheme scheme)
        {
            if (IsGamepadScheme(scheme))
            {
                UpdateUsageMode(InputUsageMode.Gamepad);
            }
            else if (scheme == ControlScheme.Keyboard)
            {
                UpdateUsageMode(InputUsageMode.Keyboard);
            }
        }

        public void NotePointerActivity()
        {
            UpdateUsageMode(InputUsageMode.Pointer);
        }

        // ========== ITickable ==========

        public void Tick()
        {
            if (_schemeWatcher != null)
                return; // PlayerInput がいるならイベントのみで更新

            // いない場合は簡易推定
            if (Touchscreen.current != null &&
                Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            {
                SetScheme(ControlScheme.Touch);
            }
            else if (Gamepad.current != null &&
                     Gamepad.current.rightStick.ReadValue() != Vector2.zero)
            {
                SetScheme(ResolveGamepadScheme());
            }
            else if (Keyboard.current != null &&
                     Keyboard.current.anyKey.wasPressedThisFrame)
            {
                SetScheme(ControlScheme.Keyboard);
            }
            else if (Mouse.current != null &&
                     Mouse.current.delta.ReadValue() != Vector2.zero)
            {
                SetScheme(ControlScheme.Mouse);
            }
        }

        // ========== 内部ロジック ==========

        void OnControlsChanged(UISPlayerInput pi)
        {
            SetScheme(FromName(pi.currentControlScheme));
        }

        void InitializeInitialScheme()
        {
            if (_schemeWatcher != null)
            {
                SetScheme(FromName(_schemeWatcher.currentControlScheme));
            }
            else
            {
                SetScheme(GuessScheme());
            }
        }

        void InitializeUsageFromScheme()
        {
            if (CurrentScheme == ControlScheme.Touch)
            {
                UpdateUsageMode(InputUsageMode.Pointer);
            }
            else if (IsGamepadScheme(CurrentScheme))
            {
                UpdateUsageMode(InputUsageMode.Gamepad);
            }
            else if (CurrentScheme == ControlScheme.Mouse)
            {
                UpdateUsageMode(InputUsageMode.Pointer);
            }
            else if (CurrentScheme == ControlScheme.Keyboard)
            {
                UpdateUsageMode(InputUsageMode.Keyboard);
            }
        }

        static bool IsGamepadScheme(ControlScheme scheme)
        {
            return scheme == ControlScheme.Gamepad
                   || scheme == ControlScheme.GamepadXbox
                   || scheme == ControlScheme.GamepadPlayStation
                   || scheme == ControlScheme.GamepadSwitch;
        }

        static ControlScheme FromName(string scheme)
        {
            if (string.IsNullOrEmpty(scheme))
                return ControlScheme.Unknown;

            if (scheme.IndexOf("Gamepad", StringComparison.OrdinalIgnoreCase) >= 0)
                return ControlScheme.Gamepad;

            if (scheme.IndexOf("Touch", StringComparison.OrdinalIgnoreCase) >= 0)
                return ControlScheme.Touch;

            if (scheme.IndexOf("Mouse", StringComparison.OrdinalIgnoreCase) >= 0 &&
                scheme.IndexOf("Keyboard", StringComparison.OrdinalIgnoreCase) < 0)
                return ControlScheme.Mouse;

            if (scheme.IndexOf("Keyboard", StringComparison.OrdinalIgnoreCase) >= 0)
                return ControlScheme.Keyboard;

            return ControlScheme.Unknown;
        }

        ControlScheme GuessScheme()
        {
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
                return ControlScheme.Touch;

            if (Gamepad.current != null && Gamepad.current.wasUpdatedThisFrame)
                return ResolveGamepadScheme();

            if (Mouse.current != null)
                return ControlScheme.Mouse;

            if (Keyboard.current != null)
                return ControlScheme.Keyboard;

            return ControlScheme.Unknown;
        }

        void SetScheme(ControlScheme s)
        {
            if (IsGamepadScheme(s))
            {
                s = ResolveGamepadScheme();
            }

            if (!IsGamepadScheme(s) &&
                s != ControlScheme.Keyboard &&
                s != ControlScheme.Mouse &&
                s != ControlScheme.Touch &&
                s != ControlScheme.Unknown)
            {
                s = ControlScheme.Unknown;
            }

            if (s == CurrentScheme)
                return;

            CurrentScheme = s;

            if (IsGamepadScheme(s))
            {
                _lastGamepadScheme = s;
                UpdateUsageMode(InputUsageMode.Gamepad);
            }
            else if (s == ControlScheme.Touch)
            {
                UpdateUsageMode(InputUsageMode.Pointer);
            }
            else if (s == ControlScheme.Mouse && CurrentUsageMode == InputUsageMode.Unknown)
            {
                UpdateUsageMode(InputUsageMode.Pointer);
            }
            else if (s == ControlScheme.Keyboard && CurrentUsageMode == InputUsageMode.Unknown)
            {
                UpdateUsageMode(InputUsageMode.Keyboard);
            }

            OnSchemeChanged?.Invoke(s);
        }

        ControlScheme ResolveGamepadScheme()
        {
            var gamepad = Gamepad.current;
            if (gamepad == null)
            {
                return IsGamepadScheme(_lastGamepadScheme)
                    ? _lastGamepadScheme
                    : ControlScheme.Gamepad;
            }

            var layout = gamepad.layout?.ToLowerInvariant();
            var product = gamepad.description.product?.ToLowerInvariant();
            var manufacturer = gamepad.description.manufacturer?.ToLowerInvariant();
            var display = gamepad.displayName?.ToLowerInvariant();

            bool IsMatch(string value, params string[] tokens)
            {
                if (string.IsNullOrEmpty(value)) return false;
                for (int i = 0; i < tokens.Length; i++)
                {
                    if (value.Contains(tokens[i])) return true;
                }
                return false;
            }

            if (IsMatch(layout, "xinput", "xbox") ||
                IsMatch(product, "xbox", "x-input") ||
                IsMatch(manufacturer, "microsoft") ||
                IsMatch(display, "xbox"))
            {
                return _lastGamepadScheme = ControlScheme.GamepadXbox;
            }

            if (IsMatch(layout, "dualshock", "dualsense", "playstation", "ps4", "ps5") ||
                IsMatch(product, "dualshock", "dualsense", "playstation", "ps4", "ps5") ||
                IsMatch(manufacturer, "sony") ||
                IsMatch(display, "dualshock", "playstation", "dualsense"))
            {
                return _lastGamepadScheme = ControlScheme.GamepadPlayStation;
            }

            if (IsMatch(layout, "switch", "nintendo", "npad") ||
                IsMatch(product, "switch", "nintendo", "joy-con", "pro controller") ||
                IsMatch(manufacturer, "nintendo") ||
                IsMatch(display, "switch", "joy-con"))
            {
                return _lastGamepadScheme = ControlScheme.GamepadSwitch;
            }

            return _lastGamepadScheme = ControlScheme.Gamepad;
        }

        void UpdateUsageMode(InputUsageMode mode)
        {
            if (CurrentUsageMode == mode)
                return;

            CurrentUsageMode = mode;
            OnUsageModeChanged?.Invoke(mode);
        }
    }
}
