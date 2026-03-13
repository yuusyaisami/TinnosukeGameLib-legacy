using System;
using UnityEngine;


namespace Game.Input
{
    public enum ControlScheme
    {
        Unknown,
        Keyboard,
        Mouse,
        Gamepad,
        Touch,
        GamepadXbox,
        GamepadPlayStation,
        GamepadSwitch
    }

    public enum InputUsageMode
    {
        Unknown,
        Keyboard,
        Gamepad,
        Pointer,
    }
    public interface IInputActionsSource
    {
        PlayerInputAction Actions { get; }
    }

    public interface IControlSchemeService
    {
        ControlScheme CurrentScheme { get; }
        InputUsageMode CurrentUsageMode { get; }

        event Action<ControlScheme> OnSchemeChanged;
        event Action<InputUsageMode> OnUsageModeChanged;

        void NoteNavigationActivity(ControlScheme scheme);
        void NotePointerActivity();
    }

    public interface IPointerService
    {
        Vector2 PointerScreen();
        Vector2 PointerWorld(Camera cam, float worldZ = 0f);

        void NotePointerAction();
        bool RegisterPointerActivity(Vector2 position, float threshold = 1f);
        bool HasRecentPointerActivity(float timeout = 0.2f);
    }
}
