using System.Collections.Generic;
using VContainer.Unity;
using UnityEngine;
using UnityEngine.InputSystem;
using Game.Input;
using System;
namespace Game.Input
{
    /// <summary>
    /// 入力コンシューマの優先度。
    /// 値が大きいほど優先される。
    /// </summary>
    public enum InputConsumerPriority : int
    {
        System = 3000, // デバッグ・フェイルセーフ系
        UIOverlay = 2000, // モーダルウィンドウなど
        UI = 1500, // 通常 UI
        Gameplay = 1000, // 通常ゲームプレイ
        Background = 500,  // ログ表示など、最下層
    }
    public interface IInputConsumer
    {
        /// <summary>
        /// 優先度（enum）。値が大きいほど先に呼ばれる。
        /// </summary>
        InputConsumerPriority Priority { get; }

        /// <summary>
        /// このフレームの入力を処理する。
        /// 必要なものだけ消費してよい。消費は InputFrame 内の Consumed フラグを更新する。
        /// </summary>
        void UpdateInput(ref InputFrame frame);
    }

    public interface IInputRouter : ITickable
    {
        void RegisterConsumer(IInputConsumer consumer);
        void UnregisterConsumer(IInputConsumer consumer);

        /// <summary>デバッグ用：最後に処理したフレーム。</summary>
        InputFrame LastFrame { get; }
    }


    /// <summary>
    /// InputSystem から生入力を取り出し、InputFrame にまとめて
    /// 優先度順に IInputConsumer へ配信するルーター。
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
            _consumers.Sort((a, b) => b.Priority.CompareTo(a.Priority)); // 降順
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
            var gameUI = actions.GameUI;     // ★ ここが UI→GameUI に変わる
            var game = actions.GameAction;

            // ==== Raw 取得 ====
            var frame = new InputFrame
            {
                DeltaTime = Time.unscaledDeltaTime,
                Scheme = _schemeService.CurrentScheme,
                UsageMode = _schemeService.CurrentUsageMode,
                PointerScreen = _pointerService.PointerScreen(),

                // Locomotion
                Move = locomotion.Direction.ReadValue<Vector2>(),
                Scroll = gameUI.Scroll.ReadValue<Vector2>(),

                // GameUI のナビゲーション（Vector2）
                Navigate = gameUI.Navigate.ReadValue<Vector2>(),
            };

            // ---- Locomotion ボタン ----
            frame.Dodge = ReadButton(locomotion.Dodge);
            frame.Slow = ReadButton(locomotion.Slow);

            // ---- Gameplay ボタン ----
            frame.Attack = ReadButton(game.Attack);
            frame.Interact = ReadButton(game.Interact);
            frame.Pause = ReadButton(game.Pause);

            // ---- GameUI ボタン ----
            frame.Submit = ReadButton(gameUI.Submit);
            frame.Cancel = ReadButton(gameUI.Cancel);
            frame.Click = ReadButton(gameUI.Click);
            frame.Retry = ReadButton(gameUI.Retry);

            // Pointer activity should promote usage mode to Pointer when mouse/touch moves.
            if (frame.Scheme == ControlScheme.Mouse ||
                frame.Scheme == ControlScheme.Keyboard ||
                frame.Scheme == ControlScheme.Touch)
            {
                _pointerService.RegisterPointerActivity(frame.PointerScreen);
            }


            // ナビゲーション系の入力があれば UsageMode を「ナビゲーション活動あり」として通知
            if (frame.Move != Vector2.zero || frame.Navigate != Vector2.zero)
            {
                _schemeService.NoteNavigationActivity(frame.Scheme);
            }

            // ==== コンシューマへ配信 ====
            for (int i = 0; i < _consumers.Count; i++)
            {
                var consumer = _consumers[i];
                var scope = ScopeFromPriority(consumer.Priority);

                if (_blocker.IsBlocked(scope))
                    continue; // このレイヤは完全ブロック

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
