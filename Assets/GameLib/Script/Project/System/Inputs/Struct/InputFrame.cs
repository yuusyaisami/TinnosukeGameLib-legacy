using UnityEngine;

namespace Game.Input
{
    /// <summary>
    /// 単一ボタンの状態。
    /// </summary>
    public struct ButtonState
    {
        public bool Down;     // このフレームで押された
        public bool Held;     // 押されている（押しっぱなし）
        public bool Up;       // このフレームで離された
        public bool Consumed; // どこかのコンシューマが消費済み

        public bool IsAny => Down || Held || Up;

        public bool TryConsumeDown()
        {
            if (Consumed || !Down) return false;
            Consumed = true;
            return true;
        }

        public bool TryConsumeHeld()
        {
            if (Consumed || !Held) return false;
            Consumed = true;
            return true;
        }

        public bool TryConsumeUp()
        {
            if (Consumed || !Up) return false;
            Consumed = true;
            return true;
        }

        public void ForceConsume()
        {
            Consumed = true;
        }
    }

    /// <summary>
    /// 1フレーム分の入力スナップショット。
    /// </summary>
    public struct InputFrame
    {
        public float DeltaTime;

        public ControlScheme Scheme;
        public InputUsageMode UsageMode;

        /// <summary>画面座標のポインタ位置（IPointerService から取得）</summary>
        public Vector2 PointerScreen;

        // ===== Locomotion =====
        /// <summary>移動入力（WASD / 左スティック等）</summary>
        public Vector2 Move;
        public bool MoveConsumed;

        /// <summary>ダッシュや回避系。Locomotion.Dodge から取得。</summary>
        public ButtonState Dodge;

        /// <summary>スロー移動（押しっぱなしでゆっくり動く等）。Locomotion.Slow から取得。</summary>
        public ButtonState Slow;

        // ===== Scroll / Wheel =====
        public Vector2 Scroll;
        public bool ScrollConsumed;

        // ===== Gameplay Buttons =====
        public ButtonState Attack;
        public ButtonState Interact;
        public ButtonState Pause;

        // ===== GameUI (UI) Buttons / Navigation =====
        public ButtonState Submit;
        public ButtonState Cancel;
        public ButtonState Click;
        public ButtonState Retry;

        /// <summary>
        /// UI用ナビゲーション入力。
        /// 通常は十字キー / 左スティック / キーボード矢印などからの Vector2。
        /// </summary>
        public Vector2 Navigate;
        public bool NavigateConsumed;

        // ===== helper =====

        public bool TryConsumeMove()
        {
            if (MoveConsumed || Move == Vector2.zero) return false;
            MoveConsumed = true;
            return true;
        }

        public bool TryConsumeScroll()
        {
            if (ScrollConsumed || Scroll == Vector2.zero) return false;
            ScrollConsumed = true;
            return true;
        }

        public bool TryConsumeNavigate()
        {
            if (NavigateConsumed || Navigate == Vector2.zero) return false;
            NavigateConsumed = true;
            return true;
        }

        /// <summary>
        /// 特定方向のナビゲーションだけ消費したい場合用。
        /// dir は (0,1) などの正規化された方向。
        /// threshold は cosθ のしきい値（1 に近いほど厳しい）。
        /// </summary>
        public bool TryConsumeNavigateDirection(Vector2 dir, float thresholdCos = 0.7f)
        {
            if (NavigateConsumed || Navigate == Vector2.zero) return false;

            var v = Navigate.normalized;
            var d = Vector2.Dot(v, dir.normalized);
            if (d < thresholdCos) return false;

            NavigateConsumed = true;
            return true;
        }
    }
}
