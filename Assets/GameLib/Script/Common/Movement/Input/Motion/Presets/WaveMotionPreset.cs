#nullable enable
// Game.Movement
// ================================================================================
// WaveMotionPreset - 豕｢迥ｶ遘ｻ蜍輔Δ繝ｼ繧ｷ繝ｧ繝ｳ
// ================================================================================
//
// 縲先ｦりｦ√・
// 騾ｲ陦梧婿蜷代↓蟇ｾ縺励※蝙ら峩縺ｫ豁｣蠑ｦ豕｢縺ｮ謠ｺ繧後ｒ蜉縺医ｋ縲・
// 陋・｡檎ｧｻ蜍輔ｄ豕｢謇薙■遘ｻ蜍輔ｒ陦ｨ迴ｾ縲・
// ================================================================================

using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Movement
{
    /// <summary>
    /// 豕｢迥ｶ遘ｻ蜍輔Δ繝ｼ繧ｷ繝ｧ繝ｳ縲・
    /// GuidanceDirection 縺ｫ蟇ｾ縺励※蝙ら峩縺ｫ豁｣蠑ｦ豕｢縺ｮ騾溷ｺｦ繧貞刈邂励☆繧九・
    /// </summary>
    [Serializable]
    public sealed class WaveMotionPreset : MotionPreset
    {
        [Header("Wave Parameters")]
        [LabelText("Amplitude")]
        [Tooltip("Inspector setting.")]
        [Min(0f)]
        public float Amplitude = 2f;

        [LabelText("Frequency")]
        [Tooltip("Inspector setting.")]
        [Min(0.01f)]
        public float Frequency = 1f;

        [LabelText("Phase Offset")]
        [Tooltip("Inspector setting.")]
        [Range(-Mathf.PI, Mathf.PI)]
        public float PhaseOffset = 0f;

        [Header("Speed Modulation")]
        [LabelText("Speed Multiplier")]
        [Tooltip("蝓ｺ譛ｬ騾溷ｺｦ縺ｸ縺ｮ蛟咲紫")]
        [Min(0f)]
        public float SpeedMultiplier = 1f;

        public override MotionRuntime CreateRuntime() => new WaveMotionRuntime(this);
    }

    /// <summary>
    /// WaveMotion 縺ｮ繝ｩ繝ｳ繧ｿ繧､繝縲・
    /// </summary>
    public sealed class WaveMotionRuntime : MotionRuntime
    {
        readonly WaveMotionPreset _source;

        public WaveMotionRuntime(WaveMotionPreset source)
        {
            _source = source;
        }

        protected override MotionOutput OnTick(in MovementGuidanceFrame frame)
        {
            // 菴咲嶌繧定ｨ育ｮ・
            float phase = ElapsedTime * _source.Frequency * Mathf.PI * 2f + _source.PhaseOffset;

            // 讓ｪ譁ｹ蜷代・騾溷ｺｦ繧定ｨ育ｮ・
            float lateral = Mathf.Sin(phase) * _source.Amplitude;

            // GuidanceDirection 縺ｫ蟇ｾ縺励※蝙ら峩縺ｪ譁ｹ蜷代ｒ蜿門ｾ・
            var guide = frame.GuidanceDirection;
            if (guide.sqrMagnitude < MovementMath.NormalizeEpsilon)
                guide = Vector2.up;

            var perpendicular = MovementMath.GetPerpendicular(guide);

            return new MotionOutput(
                direction: guide,
                speedMul: _source.SpeedMultiplier,
                additiveVelocity: perpendicular * lateral
            );
        }
    }
}
