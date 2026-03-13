#nullable enable
// Game.Movement
// ================================================================================
// IMotionMovement - モーション（進行方向変調）インターフェース
// ================================================================================
//
// 【概要】
// GuidanceDirection を入力として、進行方向・速度倍率・加算速度を生成する。
// Motion は波・弧・螺旋などの進行パターンを表現する。
//
// 【責務】
// - MotionPreset/MotionRuntime の管理
// - GuidanceDirection からの MotionOutput 生成
// - Homing の有無を意識しない（Frame から読むだけ）
//
// 【設計意図】
// - Direction の Offset/絶対値を持ち込まず、AdditiveVelocity で横成分を表現
// - これにより設計崩壊を防ぐ
// ================================================================================

using System;
using UnityEngine;

namespace Game.Movement
{
    /// <summary>
    /// モーション（進行方向変調）を管理するモジュール。
    /// </summary>
    public interface IMotionMovement
    {
        /// <summary>Motion が有効か</summary>
        bool IsActive { get; }

        /// <summary>現在の MotionPreset（null で無効）</summary>
        MotionPreset? CurrentMotion { get; }

        /// <summary>経過時間</summary>
        float ElapsedTime { get; }

        /// <summary>
        /// Motion を更新し、MotionOutput を生成。
        /// </summary>
        /// <param name="frame">フレーム情報</param>
        /// <returns>Motion の出力</returns>
        MotionOutput Tick(in MovementGuidanceFrame frame);

        /// <summary>Motion を設定</summary>
        /// <param name="motion">設定する Motion（null で無効化）</param>
        void SetMotion(MotionPreset? motion);

        /// <summary>Motion をクリア</summary>
        void ClearMotion();

        /// <summary>経過時間をリセット（Motion は維持）</summary>
        void ResetTime();
    }

    /// <summary>
    /// Motion モジュールのオプション（DI 登録用）。
    /// </summary>
    public sealed class MotionMovementOptions
    {
        /// <summary>初期 Motion</summary>
        public MotionPreset? InitialMotion { get; set; }

        /// <summary>パラメータ指定コンストラクタ。</summary>
        public MotionMovementOptions(MotionPreset? initialMotion = null)
        {
            InitialMotion = initialMotion;
        }

        public MotionMovementOptions()
        {
        }
    }
}
