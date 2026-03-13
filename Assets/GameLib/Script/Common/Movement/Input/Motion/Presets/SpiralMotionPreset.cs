#nullable enable
// Game.Movement
// ================================================================================
// SpiralMotionPreset - 螺旋移動モーション
// ================================================================================
//
// 【概要】
// 進行方向を時間経過で回転させる。
// 螺旋状の移動パターンを表現。
// ================================================================================

using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Movement
{
    /// <summary>
    /// 螺旋移動モーション。
    /// GuidanceDirection を時間経過で回転させる。
    /// </summary>
    [Serializable]
    public sealed class SpiralMotionPreset : MotionPreset
    {
        [Header("Spiral Parameters")]
        [LabelText("Rotation Speed")]
        [Tooltip("回転速度（度/秒）")]
        public float RotationSpeed = 90f;

        [LabelText("Initial Angle")]
        [Tooltip("初期角度（度）")]
        public float InitialAngle = 0f;

        [Header("Speed Modulation")]
        [LabelText("Speed Multiplier")]
        [Tooltip("基本速度への倍率")]
        [Min(0f)]
        public float SpeedMultiplier = 1f;

        public override MotionRuntime CreateRuntime() => new SpiralMotionRuntime(this);
    }

    /// <summary>
    /// SpiralMotion のランタイム。
    /// </summary>
    public sealed class SpiralMotionRuntime : MotionRuntime
    {
        readonly SpiralMotionPreset _source;

        public SpiralMotionRuntime(SpiralMotionPreset source)
        {
            _source = source;
        }

        protected override MotionOutput OnTick(in MovementGuidanceFrame frame)
        {
            // 回転角度を計算
            float angle = _source.InitialAngle + ElapsedTime * _source.RotationSpeed;

            // GuidanceDirection を回転
            var guide = frame.GuidanceDirection;
            if (guide.sqrMagnitude < MovementMath.NormalizeEpsilon)
                guide = Vector2.up;

            var rotatedDirection = MovementMath.RotateDirection(guide, angle);

            return new MotionOutput(
                direction: rotatedDirection,
                speedMul: _source.SpeedMultiplier,
                additiveVelocity: Vector2.zero
            );
        }
    }
}
