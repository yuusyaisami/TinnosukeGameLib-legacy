#nullable enable
// Game.Movement
// ================================================================================
// ArcMotionPreset - 蠑ｧ迥ｶ遘ｻ蜍輔Δ繝ｼ繧ｷ繝ｧ繝ｳ
// ================================================================================
//
// 縲先ｦりｦ√・
// 騾ｲ陦梧婿蜷代↓蟇ｾ縺励※蠑ｧ繧呈緒縺上ｈ縺・↑讓ｪ譁ｹ蜷第・蛻・ｒ蜉縺医ｋ縲・
// 荳螳壽婿蜷代∈譖ｲ縺後ｊ縺ｪ縺後ｉ騾ｲ繧遘ｻ蜍輔ヱ繧ｿ繝ｼ繝ｳ繧定｡ｨ迴ｾ縲・
// 豕｢縺ｨ驕輔＞縲∽ｸ螳壹・讓ｪ譁ｹ蜷代ヰ繧､繧｢繧ｹ繧呈戟縺､縲・
// ================================================================================

using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Movement
{
    /// <summary>
    /// 蠑ｧ迥ｶ遘ｻ蜍輔Δ繝ｼ繧ｷ繝ｧ繝ｳ縲・
    /// GuidanceDirection 縺ｫ蟇ｾ縺励※讓ｪ譁ｹ蜷代・繝舌う繧｢繧ｹ騾溷ｺｦ繧貞刈邂励☆繧九・
    /// </summary>
    [Serializable]
    public sealed class ArcMotionPreset : MotionPreset
    {
        [Header("Arc Parameters")]
        [LabelText("Lateral Bias")]
        [Tooltip("Inspector setting.")]
        public float LateralBias = 1f;

        [LabelText("Use Curve")]
        [Tooltip("譎る俣邨碁℃縺ｧ繝舌う繧｢繧ｹ繧貞､牙喧縺輔○繧九°")]
        public bool UseCurve = false;

        [LabelText("Bias Curve")]
        [Tooltip("Inspector setting.")]
        [ShowIf(nameof(UseCurve))]
        public AnimationCurve BiasCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

        [LabelText("Curve Duration")]
        [Tooltip("Inspector setting.")]
        [ShowIf(nameof(UseCurve))]
        [Min(0.01f)]
        public float CurveDuration = 1f;

        [Header("Speed Modulation")]
        [LabelText("Speed Multiplier")]
        [Tooltip("蝓ｺ譛ｬ騾溷ｺｦ縺ｸ縺ｮ蛟咲紫")]
        [Min(0f)]
        public float SpeedMultiplier = 1f;

        public override MotionRuntime CreateRuntime() => new ArcMotionRuntime(this);
    }

    /// <summary>
    /// ArcMotion 縺ｮ繝ｩ繝ｳ繧ｿ繧､繝縲・
    /// </summary>
    public sealed class ArcMotionRuntime : MotionRuntime
    {
        readonly ArcMotionPreset _source;

        public ArcMotionRuntime(ArcMotionPreset source)
        {
            _source = source;
        }

        protected override MotionOutput OnTick(in MovementGuidanceFrame frame)
        {
            // 繝舌う繧｢繧ｹ蛟､繧定ｨ育ｮ・
            float bias = _source.LateralBias;

            if (_source.UseCurve && _source.BiasCurve != null)
            {
                // 繧ｫ繝ｼ繝悶°繧牙咲紫繧貞叙蠕暦ｼ医Ν繝ｼ繝励○縺・clamp・・
                float normalizedTime = Mathf.Clamp01(ElapsedTime / _source.CurveDuration);
                float curveValue = _source.BiasCurve.Evaluate(normalizedTime);
                bias *= curveValue;
            }

            // GuidanceDirection 縺ｫ蟇ｾ縺励※蝙ら峩縺ｪ譁ｹ蜷代ｒ蜿門ｾ・
            var guide = frame.GuidanceDirection;
            if (guide.sqrMagnitude < MovementMath.NormalizeEpsilon)
                guide = Vector2.up;

            var perpendicular = MovementMath.GetPerpendicular(guide);

            return new MotionOutput(
                direction: guide,
                speedMul: _source.SpeedMultiplier,
                additiveVelocity: perpendicular * bias
            );
        }
    }
}
