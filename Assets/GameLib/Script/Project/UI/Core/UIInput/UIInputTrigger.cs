#nullable enable
using System;

namespace Game.UI
{
    /// <summary>
    /// UI 上で扱う「どのアクション入力か」を表す。
    /// UIInputService が InputFrame から UIInputEventType へ変換して供給する。
    /// </summary>
    public enum UIInputAction
    {
        Submit,
        Cancel,
        Attack,
        Interact,
        Pause,
        Retry,
    }

    public enum UIInputPhase
    {
        Down,
        Held,
        Up,
    }

    [Serializable]
    public readonly struct UIInputTrigger
    {
        public UIInputAction Action { get; }
        public UIInputPhase Phase { get; }

        public UIInputTrigger(UIInputAction action, UIInputPhase phase)
        {
            Action = action;
            Phase = phase;
        }

        public bool Matches(in UIInputEvent e)
        {
            return UIInputTriggerUtil.TryMatchPhase(in e, Action, out var phase) && phase == Phase;
        }
    }

    public static class UIInputTriggerUtil
    {
        public static bool TryMatchPhase(in UIInputEvent e, UIInputAction action, out UIInputPhase phase)
        {
            phase = default;

            switch (action)
            {
                case UIInputAction.Submit:
                    return TryMatchSubmit(in e, out phase);

                case UIInputAction.Cancel:
                    return TryMatchCancel(in e, out phase);

                case UIInputAction.Attack:
                    return TryMatchAttack(in e, out phase);

                case UIInputAction.Interact:
                    return TryMatchInteract(in e, out phase);

                case UIInputAction.Pause:
                    return TryMatchPause(in e, out phase);

                case UIInputAction.Retry:
                    return TryMatchRetry(in e, out phase);

                default:
                    return false;
            }
        }

        static bool TryMatchRetry(in UIInputEvent e, out UIInputPhase phase)
        {
            phase = default;
            switch (e.Type)
            {
                case UIInputEventType.RetryDown: phase = UIInputPhase.Down; return true;
                case UIInputEventType.RetryHeld: phase = UIInputPhase.Held; return true;
                case UIInputEventType.RetryUp: phase = UIInputPhase.Up; return true;
                default: return false;
            }
        }

        static bool TryMatchSubmit(in UIInputEvent e, out UIInputPhase phase)
        {
            phase = default;
            switch (e.Type)
            {
                case UIInputEventType.SubmitDown: phase = UIInputPhase.Down; return true;
                case UIInputEventType.SubmitHeld: phase = UIInputPhase.Held; return true;
                case UIInputEventType.SubmitUp: phase = UIInputPhase.Up; return true;
                default: return false;
            }
        }

        static bool TryMatchCancel(in UIInputEvent e, out UIInputPhase phase)
        {
            phase = default;
            switch (e.Type)
            {
                case UIInputEventType.CancelDown: phase = UIInputPhase.Down; return true;
                case UIInputEventType.CancelHeld: phase = UIInputPhase.Held; return true;
                case UIInputEventType.CancelUp: phase = UIInputPhase.Up; return true;
                default: return false;
            }
        }

        static bool TryMatchAttack(in UIInputEvent e, out UIInputPhase phase)
        {
            phase = default;
            switch (e.Type)
            {
                case UIInputEventType.AttackDown: phase = UIInputPhase.Down; return true;
                case UIInputEventType.AttackHeld: phase = UIInputPhase.Held; return true;
                case UIInputEventType.AttackUp: phase = UIInputPhase.Up; return true;
                default: return false;
            }
        }

        static bool TryMatchInteract(in UIInputEvent e, out UIInputPhase phase)
        {
            phase = default;
            switch (e.Type)
            {
                case UIInputEventType.InteractDown: phase = UIInputPhase.Down; return true;
                case UIInputEventType.InteractHeld: phase = UIInputPhase.Held; return true;
                case UIInputEventType.InteractUp: phase = UIInputPhase.Up; return true;
                default: return false;
            }
        }

        static bool TryMatchPause(in UIInputEvent e, out UIInputPhase phase)
        {
            phase = default;
            switch (e.Type)
            {
                case UIInputEventType.PauseDown: phase = UIInputPhase.Down; return true;
                case UIInputEventType.PauseHeld: phase = UIInputPhase.Held; return true;
                case UIInputEventType.PauseUp: phase = UIInputPhase.Up; return true;
                default: return false;
            }
        }
    }
}
