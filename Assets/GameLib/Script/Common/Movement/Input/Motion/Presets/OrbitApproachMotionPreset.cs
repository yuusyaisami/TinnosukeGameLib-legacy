#nullable enable
// Game.Movement
// ================================================================================
// OrbitApproachMotionPreset - ターゲットを周回しつつ近づくモーション
// ================================================================================
//
// 【用途】
// - 敵/弾がターゲットを“美しく回り込みながら”近づく。
// - 数学的に単純で安定（接線方向 + 半径方向のブレンド）。
// ================================================================================

using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Movement
{
    [Serializable]
    public sealed class OrbitApproachMotionPreset : MotionPreset
    {
        [Header("Orbit")]
        [LabelText("Clockwise")]
        [Tooltip("true: 時計回り / false: 反時計回り")]
        public bool Clockwise = false;

        [LabelText("Approach")]
        [Tooltip("接線(周回)→半径(接近)のブレンド。0=周回、1=直線接近")]
        [Range(0f, 1f)]
        public float Approach = 0.35f;

        [Header("Wobble (Optional)")]
        [LabelText("Wobble Angle")]
        [Tooltip("微小な角度ゆらぎ（度）。0 で無効。")]
        [Min(0f)]
        public float WobbleAngle = 0f;

        [LabelText("Wobble Frequency")]
        [Tooltip("ゆらぎ周波数（Hz）。")]
        [Min(0f)]
        public float WobbleFrequency = 1.2f;

        [Header("Speed Modulation")]
        [LabelText("Speed Multiplier")]
        [Tooltip("基本速度への倍率")]
        [Min(0f)]
        public float SpeedMultiplier = 1f;

        public override MotionRuntime CreateRuntime() => new OrbitApproachMotionRuntime(this);
    }

    public sealed class OrbitApproachMotionRuntime : MotionRuntime
    {
        readonly OrbitApproachMotionPreset _source;

        public OrbitApproachMotionRuntime(OrbitApproachMotionPreset source)
        {
            _source = source;
        }

        protected override MotionOutput OnTick(in MovementGuidanceFrame frame)
        {
            if (!frame.Target.HasTarget)
                return MotionOutput.Default(frame.GuidanceDirection);

            var radial = frame.Target.TargetDirection;
            if (radial.sqrMagnitude < MovementMath.NormalizeEpsilon)
                radial = Vector2.up;
            radial = MovementMath.NormalizeDirection(radial);

            var tangent = MovementMath.GetPerpendicular(radial);
            if (_source.Clockwise)
                tangent = -tangent;

            float a = Mathf.Clamp01(_source.Approach);
            var blended = (tangent * (1f - a)) + (radial * a);
            blended = MovementMath.NormalizeDirection(blended);

            // 微小な角度ゆらぎ（任意）
            float wobbleAngle = Mathf.Max(0f, _source.WobbleAngle);
            if (wobbleAngle > 0f && _source.WobbleFrequency > 0f)
            {
                float phase = ElapsedTime * _source.WobbleFrequency * Mathf.PI * 2f;
                float angle = Mathf.Sin(phase) * wobbleAngle;
                blended = MovementMath.RotateDirection(blended, angle);
                blended = MovementMath.NormalizeDirection(blended);
            }

            return new MotionOutput(
                direction: blended,
                speedMul: Mathf.Max(0f, _source.SpeedMultiplier),
                additiveVelocity: Vector2.zero
            );
        }
    }
}
