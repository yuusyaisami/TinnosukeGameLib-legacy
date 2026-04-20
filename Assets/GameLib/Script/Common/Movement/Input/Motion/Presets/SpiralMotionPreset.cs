#nullable enable
// Game.Movement
// ================================================================================
// SpiralMotionPreset - 陞ｺ譌狗ｧｻ蜍輔Δ繝ｼ繧ｷ繝ｧ繝ｳ
// ================================================================================
//
// 縲先ｦりｦ√・
// 騾ｲ陦梧婿蜷代ｒ譎る俣邨碁℃縺ｧ蝗櫁ｻ｢縺輔○繧九・
// 陞ｺ譌狗憾縺ｮ遘ｻ蜍輔ヱ繧ｿ繝ｼ繝ｳ繧定｡ｨ迴ｾ縲・
// ================================================================================

using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Movement
{
    /// <summary>
    /// 陞ｺ譌狗ｧｻ蜍輔Δ繝ｼ繧ｷ繝ｧ繝ｳ縲・
    /// GuidanceDirection 繧呈凾髢鍋ｵ碁℃縺ｧ蝗櫁ｻ｢縺輔○繧九・
    /// </summary>
    [Serializable]
    public sealed class SpiralMotionPreset : MotionPreset
    {
        [Header("Spiral Parameters")]
        [LabelText("Rotation Speed")]
        [Tooltip("Inspector setting.")]
        public float RotationSpeed = 90f;

        [LabelText("Initial Angle")]
        [Tooltip("Inspector setting.")]
        public float InitialAngle = 0f;

        [Header("Speed Modulation")]
        [LabelText("Speed Multiplier")]
        [Tooltip("蝓ｺ譛ｬ騾溷ｺｦ縺ｸ縺ｮ蛟咲紫")]
        [Min(0f)]
        public float SpeedMultiplier = 1f;

        public override MotionRuntime CreateRuntime() => new SpiralMotionRuntime(this);
    }

    /// <summary>
    /// SpiralMotion 縺ｮ繝ｩ繝ｳ繧ｿ繧､繝縲・
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
            // 蝗櫁ｻ｢隗貞ｺｦ繧定ｨ育ｮ・
            float angle = _source.InitialAngle + ElapsedTime * _source.RotationSpeed;

            // GuidanceDirection 繧貞屓霆｢
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
