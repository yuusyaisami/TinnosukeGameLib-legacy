#nullable enable
// Game.Movement
// ================================================================================
// OrbitApproachMotionPreset - 繧ｿ繝ｼ繧ｲ繝・ヨ繧貞捉蝗槭＠縺､縺､霑代▼縺上Δ繝ｼ繧ｷ繝ｧ繝ｳ
// ================================================================================
//
// 縲千畑騾斐・
// - 謨ｵ/蠑ｾ縺後ち繝ｼ繧ｲ繝・ヨ繧停懃ｾ弱＠縺丞屓繧願ｾｼ縺ｿ縺ｪ縺後ｉ窶晁ｿ代▼縺上・
// - 謨ｰ蟄ｦ逧・↓蜊倡ｴ斐〒螳牙ｮ夲ｼ域磁邱壽婿蜷・+ 蜊雁ｾ・婿蜷代・繝悶Ξ繝ｳ繝会ｼ峨・
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
        [Tooltip("Inspector setting.")]
        public bool Clockwise = false;

        [LabelText("Approach")]
        [Tooltip("Inspector setting.")]
        [Range(0f, 1f)]
        public float Approach = 0.35f;

        [Header("Wobble (Optional)")]
        [LabelText("Wobble Angle")]
        [Tooltip("Inspector setting.")]
        [Min(0f)]
        public float WobbleAngle = 0f;

        [LabelText("Wobble Frequency")]
        [Tooltip("Inspector setting.")]
        [Min(0f)]
        public float WobbleFrequency = 1.2f;

        [Header("Speed Modulation")]
        [LabelText("Speed Multiplier")]
        [Tooltip("蝓ｺ譛ｬ騾溷ｺｦ縺ｸ縺ｮ蛟咲紫")]
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

            // 蠕ｮ蟆上↑隗貞ｺｦ繧・ｉ縺趣ｼ井ｻｻ諢擾ｼ・
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
