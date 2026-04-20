#nullable enable
// Game.Movement
// ================================================================================
// IHomingMovement - 繝帙・繝溘Φ繧ｰ・医ち繝ｼ繧ｲ繝・ヨ霑ｽ蟆ｾ・峨う繝ｳ繧ｿ繝ｼ繝輔ぉ繝ｼ繧ｹ
// ================================================================================
//
// 縲先ｦりｦ√・
// 繧ｿ繝ｼ繧ｲ繝・ヨ譁ｹ蜷代→蜈･蜉帶婿蜷代ｒ隗貞ｺｦ陬憺俣縺励；uidanceDirection 繧堤函謌舌☆繧九・
// BoolLayer 縺ｧ ON/OFF 繧貞宛蠕｡縺励＾FF 譎ゅ・險育ｮ励ｒ蛛懈ｭ｢縺励※迥ｶ諷九ｒ菫晄戟縺吶ｋ縲・
//
// 縲占ｲｬ蜍吶・
// - TargetChannelHub 縺九ｉ繧ｿ繝ｼ繧ｲ繝・ヨ蜿門ｾ・
// - BaseDirection 縺ｨ TargetDirection 縺ｮ隗貞ｺｦ陬憺俣
// - GuidanceDirection 縺ｮ蜃ｺ蜉・
// - BoolLayer 縺ｫ繧医ｋ譛牙柑/辟｡蜉ｹ蛻ｶ蠕｡
//
// 縲宣㍾隕√↑謖吝虚縲・
// - HomingEnabled == false: 蛛懈ｭ｢・・arget/陬憺俣縺ｪ縺暦ｼ峨；uidanceDirection 縺ｯ逶ｴ蜑堺ｿ晄戟
// - HasTarget == false: GuidanceDirection = BaseDirection
// - BaseDirection == zero && HasTarget: GuidanceDirection = TargetDirection・亥ｮ悟・霑ｽ蠕難ｼ・
// - BaseDirection != zero && HasTarget: 隗貞ｺｦ陬憺俣縺ｧ蜷域・
// ================================================================================

using System;
using UnityEngine;
using Game.Common;

namespace Game.Movement
{
    /// <summary>
    /// 繝帙・繝溘Φ繧ｰ・医ち繝ｼ繧ｲ繝・ヨ霑ｽ蟆ｾ・峨ｒ邂｡逅・☆繧九Δ繧ｸ繝･繝ｼ繝ｫ縲・
    /// </summary>
    public interface IHomingMovement
    {
        /// <summary>Homing 縺ｮ譛牙柑/辟｡蜉ｹ繧貞宛蠕｡縺吶ｋ BoolLayer</summary>
        BoolLayer HomingLayer { get; }

        /// <summary>Homing 縺梧怏蜉ｹ縺具ｼ・oolLayer 縺ｮ蜷域・邨先棡・・/summary>
        bool HomingEnabled { get; }

        /// <summary>迴ｾ蝨ｨ縺ｮ GuidanceDirection・郁ｪｭ縺ｿ蜿悶ｊ蟆ら畑・・/summary>
        Vector2 GuidanceDirection { get; }

        /// <summary>迴ｾ蝨ｨ縺ｮ繧ｿ繝ｼ繧ｲ繝・ヨ諠・ｱ・郁ｪｭ縺ｿ蜿悶ｊ蟆ら畑・・/summary>
        TargetSnapshot CurrentTarget { get; }

        /// <summary>
        /// Homing 繧呈峩譁ｰ縺励；uidanceDirection 繧堤ｮ怜・縲・
        /// </summary>
        /// <param name="baseDirection">蜈･蜉帶婿蜷托ｼ域ｭ｣隕丞喧貂医∩ or zero・・/param>
        /// <param name="ownerPosition">Owner 縺ｮ繝ｯ繝ｼ繝ｫ繝牙ｺｧ讓・/param>
        /// <param name="deltaTime">繝・Ν繧ｿ繧ｿ繧､繝</param>
        /// <returns>蜷域・蠕後・ GuidanceDirection</returns>
        Vector2 Tick(Vector2 baseDirection, Vector2 ownerPosition, float deltaTime);

        /// <summary>
        /// 蜷域・迥ｶ諷九ｒ繝ｪ繧ｻ繝・ヨ・医・繝ｼ繝溘Φ繧ｰ蠖ｱ髻ｿ繧呈ｸ帙ｉ縺呻ｼ峨・
        /// </summary>
        /// <param name="resetAlpha">0=菴輔ｂ縺励↑縺・ 1=螳悟・繝ｪ繧ｻ繝・ヨ</param>
        void ResetBlend(float resetAlpha);

        /// <summary>蜀・Κ迥ｶ諷九ｒ螳悟・縺ｫ繧ｯ繝ｪ繧｢</summary>
        void Clear();
    }

    /// <summary>
    /// Homing 陬憺俣繝代Λ繝｡繝ｼ繧ｿ縲・
    /// </summary>
    [Serializable]
    public sealed class HomingBlendParams
    {
        [Tooltip("Inspector setting.")]
        [Min(0.01f)]
        public float BlendSpeed = 2f;

        [Tooltip("Inspector setting.")]
        public AnimationCurve? BlendCurve;

        [Tooltip("Inspector setting.")]
        [Range(0f, 1f)]
        public float MaxAlpha = 1f;

        /// <summary>繝・ヵ繧ｩ繝ｫ繝医ヱ繝ｩ繝｡繝ｼ繧ｿ繧剃ｽ懈・</summary>
        public static HomingBlendParams Default => new()
        {
            BlendSpeed = 2f,
            BlendCurve = null,
            MaxAlpha = 1f
        };
    }

    /// <summary>
    /// Homing 繝｢繧ｸ繝･繝ｼ繝ｫ縺ｮ繧ｪ繝励す繝ｧ繝ｳ・・I 逋ｻ骭ｲ逕ｨ・峨・
    /// </summary>
    public sealed class HomingMovementOptions
    {
        /// <summary>繧ｿ繝ｼ繧ｲ繝・ヨ繝√Ε繝阪Ν縺ｮ繧ｿ繧ｰ</summary>
        public string TargetChannelTag { get; set; } = "enemy";

        /// <summary>陬憺俣繝代Λ繝｡繝ｼ繧ｿ</summary>
        public HomingBlendParams BlendParams { get; set; } = HomingBlendParams.Default;

        /// <summary>蛻晄悄迥ｶ諷九〒 Homing 繧呈怏蜉ｹ縺ｫ縺吶ｋ縺・/summary>
        public bool EnabledByDefault { get; set; } = true;

        /// <summary>BoolLayer 縺ｮ繝・ヵ繧ｩ繝ｫ繝医く繝ｼ</summary>
        public string DefaultLayerKey { get; set; } = "default";

        /// <summary>繝代Λ繝｡繝ｼ繧ｿ謖・ｮ壹さ繝ｳ繧ｹ繝医Λ繧ｯ繧ｿ縲・/summary>
        public HomingMovementOptions(
            string? targetChannelTag = null,
            HomingBlendParams? blendParams = null,
            bool? enabledByDefault = null,
            string? defaultLayerKey = null)
        {
            if (!string.IsNullOrEmpty(targetChannelTag))
                TargetChannelTag = targetChannelTag;
            if (blendParams != null)
                BlendParams = blendParams;
            if (enabledByDefault.HasValue)
                EnabledByDefault = enabledByDefault.Value;
            if (!string.IsNullOrEmpty(defaultLayerKey))
                DefaultLayerKey = defaultLayerKey;
        }

        public HomingMovementOptions()
        {
        }
    }
}
