// Game.Health.BaseVisualData.cs
//
// 螟夜Κ縺ｫ隕九○繧九◆繧√・陦ｨ遉ｺ逕ｨ繝・・繧ｿ縺ｮ蝓ｺ蠎輔け繝ｩ繧ｹ

using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Game.Animation;
using Game.Trait;

namespace Game.Health
{
    /// <summary>
    /// 螟夜Κ縺ｫ隕九○繧九◆繧√・陦ｨ遉ｺ逕ｨ繝・・繧ｿ縺ｮ蝓ｺ蠎輔け繝ｩ繧ｹ縲・
    /// UI縲√Ο繧ｰ縲√ョ繝舌ャ繧ｰ陦ｨ遉ｺ遲峨〒菴ｿ逕ｨ縺輔ｌ繧句・騾壹ョ繝ｼ繧ｿ繧呈署萓帙☆繧九・
    /// HealthSystem 縺ｨ StatusEffectSystem 縺ｧ蜈ｱ騾壻ｽｿ逕ｨ縲・
    /// </summary>
    [Serializable]
    public abstract class BaseVisualData
    {
        /// <summary>陦ｨ遉ｺ蜷搾ｼ・ichText 繝・Φ繝励Ξ繝ｼ繝茨ｼ・/summary>
        [LabelText("Display Name")]
        [Tooltip("Inspector setting.")]
        [InlineProperty]
        public RichTextTemplateData DisplayName = new();

        /// <summary>繧｢繧､繧ｳ繝ｳ逕ｨ AnimationData・医せ繝励Λ繧､繝医す繝ｼ繝亥ｯｾ蠢懶ｼ・/summary>
        [LabelText("Icon Animation")]
        [Tooltip("Inspector setting.")]
        [AssetSelector, AssetOrInternal]
        public AnimationData IconAnimation;

        /// <summary>隱ｬ譏取枚・・ichText 繝・Φ繝励Ξ繝ｼ繝茨ｼ・/summary>
        [LabelText("Description")]
        [InlineProperty]
        public RichTextTemplateData Description = new();

        public string DisplayNameText => DisplayName?.Template ?? string.Empty;
        public string DescriptionText => Description?.Template ?? string.Empty;

        /// <summary>
        /// IconAnimation 縺ｮ譛蛻昴・繝輔Ξ繝ｼ繝縺ｮ繧ｹ繝励Λ繧､繝医ｒ蜿門ｾ励・
        /// IconAnimation 縺・null 縺ｮ蝣ｴ蜷医・ null 繧定ｿ斐☆縲・
        /// </summary>
        public Sprite Icon => IconAnimation?.frames?.Count > 0
            ? IconAnimation.frames[0].sprite
            : null;
    }

    /// <summary>
    /// Health Modifier 逕ｨ縺ｮ陦ｨ遉ｺ繝・・繧ｿ縲・
    /// </summary>
    [Serializable]
    public sealed class HealthModifierVisualData : BaseVisualData
    {
        /// <summary>Buff/Debuff 縺ｮ遞ｮ蛻･</summary>
        [LabelText("Effect Type")]
        public EffectType EffectType;

        /// <summary>蜆ｪ蜈亥ｺｦ陦ｨ遉ｺ逕ｨ縺ｮ繧ｽ繝ｼ繝磯・/summary>
        [LabelText("Sort Order")]
        public int SortOrder;
    }

    /// <summary>
    /// 蜉ｹ譫懊・遞ｮ鬘橸ｼ・I 蛻・｡樒畑・・
    /// </summary>
    public enum EffectType
    {
        /// <summary>繝舌ヵ・域怏蛻ｩ縺ｪ蜉ｹ譫懶ｼ・/summary>
        Buff = 10,

        /// <summary>繝・ヰ繝包ｼ井ｸ榊茜縺ｪ蜉ｹ譫懶ｼ・/summary>
        Debuff = 20,

        /// <summary>荳ｭ遶具ｼ域怏蛻ｩ縺ｧ繧ゆｸ榊茜縺ｧ繧ゅ↑縺・ｼ・/summary>
        Neutral = 30,
    }
}
