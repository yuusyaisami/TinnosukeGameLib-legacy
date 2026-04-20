#nullable enable
using System.Collections.Generic;
using VContainer.Unity;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using Game.Input;
using System;
namespace Game.Input
{
    /// <summary>
    /// 蜈･蜉帙さ繝ｳ繧ｷ繝･繝ｼ繝槭・蜆ｪ蜈亥ｺｦ縲・
    /// 蛟､縺悟､ｧ縺阪＞縺ｻ縺ｩ蜆ｪ蜈医＆繧後ｋ縲・
    /// </summary>
    public enum InputConsumerPriority : int
    {
        System = 3000, // 繝・ヰ繝・げ繝ｻ繝輔ぉ繧､繝ｫ繧ｻ繝ｼ繝慕ｳｻ
        UIOverlay = 2000, // 繝｢繝ｼ繝繝ｫ繧ｦ繧｣繝ｳ繝峨え縺ｪ縺ｩ
        UI = 1500, // 騾壼ｸｸ UI
        Gameplay = 1000, // 騾壼ｸｸ繧ｲ繝ｼ繝繝励Ξ繧､
        Background = 500,  // 繝ｭ繧ｰ陦ｨ遉ｺ縺ｪ縺ｩ縲∵怙荳句ｱ､
    }
    public interface IInputConsumer
    {
        /// <summary>
        /// 蜆ｪ蜈亥ｺｦ・・num・峨ょ､縺悟､ｧ縺阪＞縺ｻ縺ｩ蜈医↓蜻ｼ縺ｰ繧後ｋ縲・
        /// </summary>
        InputConsumerPriority Priority { get; }

        /// <summary>
        /// 縺薙・繝輔Ξ繝ｼ繝縺ｮ蜈･蜉帙ｒ蜃ｦ逅・☆繧九・
        /// 蠢・ｦ√↑繧ゅ・縺縺第ｶ郁ｲｻ縺励※繧医＞縲よｶ郁ｲｻ縺ｯ InputFrame 蜀・・ Consumed 繝輔Λ繧ｰ繧呈峩譁ｰ縺吶ｋ縲・
        /// </summary>
        void UpdateInput(ref InputFrame frame);
    }

    public interface IInputRouter : IScopeTickHandler
    {
        void RegisterConsumer(IInputConsumer consumer);
        void UnregisterConsumer(IInputConsumer consumer);

        /// <summary>繝・ヰ繝・げ逕ｨ・壽怙蠕後↓蜃ｦ逅・＠縺溘ヵ繝ｬ繝ｼ繝縲・/summary>
        InputFrame LastFrame { get; }
    }


    /// <summary>
    /// InputSystem 縺九ｉ逕溷・蜉帙ｒ蜿悶ｊ蜃ｺ縺励！nputFrame 縺ｫ縺ｾ縺ｨ繧√※
    /// 蜆ｪ蜈亥ｺｦ鬆・↓ IInputConsumer 縺ｸ驟堺ｿ｡縺吶ｋ繝ｫ繝ｼ繧ｿ繝ｼ縲・
    /// </summary>
    public sealed class InputRouter : IInputRouter
    {
        readonly IInputActionsSource _actionsSource;
        readonly IControlSchemeService _schemeService;
        readonly IPointerService _pointerService;
        readonly IInputBlocker _blocker;

        readonly List<IInputConsumer> _consumers = new List<IInputConsumer>();

        InputFrame _lastFrame;
        public InputFrame LastFrame => _lastFrame;

        public InputRouter(
            IInputActionsSource actionsSource,
            IControlSchemeService schemeService,
            IPointerService pointerService,
            IInputBlocker blocker)
        {
            _actionsSource = actionsSource;
            _schemeService = schemeService;
            _pointerService = pointerService;
            _blocker = blocker;
        }

        public void RegisterConsumer(IInputConsumer consumer)
        {
            if (consumer == null) return;
            if (_consumers.Contains(consumer)) return;

            _consumers.Add(consumer);
            _consumers.Sort((a, b) => b.Priority.CompareTo(a.Priority)); // 髯埼・
        }

        public void UnregisterConsumer(IInputConsumer consumer)
        {
            if (consumer == null) return;
            _consumers.Remove(consumer);
        }

        public void Tick()
        {
            if (_consumers.Count == 0)
                return;

            var actions = _actionsSource.Actions;
            var locomotion = actions.Locomotion;
            var gameUI = actions.GameUI;     // 笘・縺薙％縺・UI竊竪ameUI 縺ｫ螟峨ｏ繧・
            var game = actions.GameAction;

            // ==== Raw 蜿門ｾ・====
            var frame = new InputFrame
            {
                DeltaTime = Time.unscaledDeltaTime,
                Scheme = _schemeService.CurrentScheme,
                UsageMode = _schemeService.CurrentUsageMode,
                PointerScreen = _pointerService.PointerScreen(),

                // Locomotion
                Move = locomotion.Direction.ReadValue<Vector2>(),
                Scroll = gameUI.Scroll.ReadValue<Vector2>(),

                // GameUI 縺ｮ繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ・・ector2・・
                Navigate = gameUI.Navigate.ReadValue<Vector2>(),
            };

            frame.PointerDelta = Vector2.zero;
            if (Mouse.current != null)
                frame.PointerDelta += Mouse.current.delta.ReadValue();
            if (Touchscreen.current != null)
                frame.PointerDelta += Touchscreen.current.primaryTouch.delta.ReadValue();

            // ---- Locomotion 繝懊ち繝ｳ ----
            frame.Dodge = ReadButton(locomotion.Dodge);
            frame.Slow = ReadButton(locomotion.Slow);

            // ---- Gameplay 繝懊ち繝ｳ ----
            frame.Attack = ReadButton(game.Attack);
            frame.Interact = ReadButton(game.Interact);
            frame.Pause = ReadButton(game.Pause);

            // ---- GameUI 繝懊ち繝ｳ ----
            frame.Submit = ReadButton(gameUI.Submit);
            frame.Cancel = ReadButton(gameUI.Cancel);
            frame.Click = ReadButton(gameUI.Click);
            frame.Retry = ReadButton(gameUI.Retry);
            frame.PointerLeft = MergeButtons(frame.Click, ReadPointerButton(Mouse.current != null ? Mouse.current.leftButton : null));
            frame.PointerRight = ReadPointerButton(Mouse.current != null ? Mouse.current.rightButton : null);
            if (Touchscreen.current != null)
            {
                frame.PointerLeft = MergeButtons(frame.PointerLeft, ReadPointerButton(Touchscreen.current.primaryTouch.press));
            }

            // Pointer activity should promote usage mode to Pointer when mouse/touch moves.
            if (frame.Scheme == ControlScheme.Mouse ||
                frame.Scheme == ControlScheme.Keyboard ||
                frame.Scheme == ControlScheme.Touch)
            {
                _pointerService.RegisterPointerActivity(frame.PointerScreen);
            }


            // 繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ邉ｻ縺ｮ蜈･蜉帙′縺ゅｌ縺ｰ UsageMode 繧偵後リ繝薙ご繝ｼ繧ｷ繝ｧ繝ｳ豢ｻ蜍輔≠繧翫阪→縺励※騾夂衍
            if (frame.Move != Vector2.zero || frame.Navigate != Vector2.zero)
            {
                _schemeService.NoteNavigationActivity(frame.Scheme);
            }

            // ==== 繧ｳ繝ｳ繧ｷ繝･繝ｼ繝槭∈驟堺ｿ｡ ====
            for (int i = 0; i < _consumers.Count; i++)
            {
                var consumer = _consumers[i];
                var scope = ScopeFromPriority(consumer.Priority);

                if (_blocker.IsBlocked(scope))
                    continue; // 縺薙・繝ｬ繧､繝､縺ｯ螳悟・繝悶Ο繝・け

                consumer.UpdateInput(ref frame);
            }

            _lastFrame = frame;
        }

        // =======================
        //  helper
        // =======================
        static ButtonState ReadButton(InputAction action)
        {
            if (action == null)
                return default;

            return new ButtonState
            {
                Down = action.WasPressedThisFrame(),
                Held = action.IsPressed(),
                Up = action.WasReleasedThisFrame(),
                Consumed = false
            };
        }

        static ButtonState ReadPointerButton(ButtonControl? button)
        {
            if (button == null)
                return default;

            return new ButtonState
            {
                Down = button.wasPressedThisFrame,
                Held = button.isPressed,
                Up = button.wasReleasedThisFrame,
                Consumed = false
            };
        }

        static ButtonState MergeButtons(ButtonState primary, ButtonState secondary)
        {
            return new ButtonState
            {
                Down = primary.Down || secondary.Down,
                Held = primary.Held || secondary.Held,
                Up = primary.Up || secondary.Up,
                Consumed = primary.Consumed || secondary.Consumed
            };
        }

        static InputBlockScope ScopeFromPriority(InputConsumerPriority p)
        {
            switch (p)
            {
                case InputConsumerPriority.System:
                    return InputBlockScope.System;

                case InputConsumerPriority.UIOverlay:
                case InputConsumerPriority.UI:
                    return InputBlockScope.UI;

                case InputConsumerPriority.Gameplay:
                case InputConsumerPriority.Background:
                default:
                    return InputBlockScope.Gameplay;
            }
        }
    }
}
