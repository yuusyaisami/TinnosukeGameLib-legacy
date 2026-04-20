#nullable enable
using System;
using System.Collections.Generic;
using Game.MaterialFx.Generated;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.MaterialFx
{
    [Flags]
    public enum OutlineDirectionMask
    {
        None = 0,
        Left = 1,
        Right = 2,
        Up = 4,
        Down = 8,
        All = Left | Right | Up | Down,
    }

    public enum OutlineAutoColorMode
    {
        Hsl = 0,
        HslPlus = 1,
    }

    [Flags]
    public enum TextOutlineDirectionMask
    {
        None = 0,
        Left = 1,
        Right = 2,
        Up = 4,
        Down = 8,
        All = Left | Right | Up | Down,
    }

    public enum TextOutlineAutoColorMode
    {
        Hsl = 0,
        HslPlus = 1,
    }

    /// <summary>
    /// BaseShader 蟆ら畑縺ｮ MaterialFx 繝励Μ繧ｻ繝・ヨ縲・
    /// Inspector 縺ｧ BaseShader 縺ｮ蜈ｨ繝励Ο繝代ユ繧｣繧堤ｷｨ髮・庄閭ｽ縲・
    /// 繝輔ぅ繝ｼ繝ｫ繝牙､画峩譎ゅ↓ AutoEntries 繧定・蜍墓峩譁ｰ縲・
    /// 
    /// ## 讎りｦ・
    /// 縺薙・SO縺ｯ縲。aseShader 縺ｮ CompositeSystem 繝励Ο繝代ユ繧｣繧・Inspector 荳翫〒
    /// 逶ｴ諢溽噪縺ｫ邱ｨ髮・〒縺阪ｋ繧医≧縺ｫ縺励◆繝励Μ繧ｻ繝・ヨ縺ｧ縺吶・
    /// 蜷・お繝輔ぉ繧ｯ繝医・譛牙柑/辟｡蜉ｹ繧貞・繧頑崛縺医√ヱ繝ｩ繝｡繝ｼ繧ｿ繧定ｪｿ謨ｴ縺吶ｋ縺薙→縺ｧ
    /// 繝槭ユ繝ｪ繧｢繝ｫ繧ｨ繝輔ぉ繧ｯ繝医・邨・∩蜷医ｏ縺帙ｒ菫晏ｭ倥・蜀榊茜逕ｨ縺ｧ縺阪∪縺吶・
    /// 
    /// ## 繝・け繧ｹ繝√Ε繧ｽ繝ｼ繧ｹ險ｭ螳壹↓縺､縺・※
    /// - SlotType: 繝・け繧ｹ繝√Ε繧貞叙蠕励☆繧九せ繝ｭ繝・ヨ (5=ExternalA, 6=ExternalB, 7=CustomRT)
    /// - Channel: 菴ｿ逕ｨ縺吶ｋ繝√Ε繝ｳ繝阪Ν (R/G/B/A)
    /// - UVSpace: UV蠎ｧ讓咏ｳｻ (0=SpriteLocal, 1=Screen, 2=TextureRaw, 3=WorldXY)
    /// </summary>
    [Serializable]
    public sealed class BaseShaderFxPreset : MaterialFxPresetDataBase
    {
        public enum BaseShaderRenderBlendPreset
        {
            Alpha = 0,
            Additive = 1,
            AdditiveAlpha = 2,
            Premultiply = 3,
        }

        // 笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊・
        // Dissolve (繝・ぅ繧ｾ繝ｫ繝門柑譫・
        // 笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊・

        [TitleGroup("Inspector")]
        [ToggleLeft]
        [Tooltip("繝・ぅ繧ｾ繝ｫ繝悶お繝輔ぉ繧ｯ繝医ｒ譛牙柑縺ｫ縺吶ｋ")]
        public bool dissolveEnabled = false;

        [TitleGroup("Dissolve")]
        [ShowIf(nameof(dissolveEnabled))]
        [Range(0f, 1f)]
        [Tooltip("豸亥､ｱ縺ｮ騾ｲ陦悟ｺｦ縲・=螳悟・縺ｫ陦ｨ遉ｺ縲・=螳悟・縺ｫ豸亥､ｱ")]
        public float dissolveThreshold = 0f;

        [TitleGroup("Dissolve")]
        [ShowIf(nameof(dissolveEnabled))]
        [Range(0f, 1f)]
        [Tooltip("Inspector setting.")]
        public float dissolveEdgeWidth = 0.1f;

        [TitleGroup("Dissolve")]
        [ShowIf(nameof(dissolveEnabled))]
        [Tooltip("豸亥､ｱ繧ｨ繝・ず縺ｮ逋ｺ蜈芽牡")]
        public Color dissolveEdgeColor = Color.white;

        [TitleGroup("Inspector")]
        [ShowIf(nameof(dissolveEnabled))]
        [ValueDropdown(nameof(GetSlotTypeOptions))]
        [Tooltip("繝・け繧ｹ繝√Ε繧ｹ繝ｭ繝・ヨ (ExtA/ExtB/CustomRT)")]
        public int dissolveSourceSlotType = 5;

        [TitleGroup("Dissolve/Source")]
        [ShowIf(nameof(dissolveEnabled))]
        [ValueDropdown(nameof(GetChannelOptions))]
        [Tooltip("菴ｿ逕ｨ縺吶ｋ繝√Ε繝ｳ繝阪Ν (R/G/B/A)")]
        public int dissolveSourceChannel = 1;

        [TitleGroup("Dissolve/Source")]
        [ShowIf(nameof(dissolveEnabled))]
        [ValueDropdown(nameof(GetUVSpaceOptions))]
        [Tooltip("UV蠎ｧ讓咏ｳｻ (SpriteLocal/Screen/TextureRaw/WorldXY)")]
        public int dissolveSourceUVSpace = 0;

        // 笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊・
        // Flow Warp (繝輔Ο繝ｼ豁ｪ縺ｿ蜉ｹ譫・
        // 笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊・

        [TitleGroup("Inspector")]
        [ToggleLeft]
        [Tooltip("繝輔Ο繝ｼ繝ｯ繝ｼ繝励お繝輔ぉ繧ｯ繝医ｒ譛牙柑縺ｫ縺吶ｋ")]
        public bool flowWarpEnabled = false;

        [TitleGroup("Flow Warp")]
        [ShowIf(nameof(flowWarpEnabled))]
        [Tooltip("Inspector setting.")]
        public Vector2 flowWarpStrength = new Vector2(0.1f, 0.1f);

        [TitleGroup("Flow Warp")]
        [ShowIf(nameof(flowWarpEnabled))]
        [Tooltip("豁ｪ縺ｿ繧｢繝九Γ繝ｼ繧ｷ繝ｧ繝ｳ縺ｮ騾溷ｺｦ")]
        public float flowWarpSpeed = 1f;

        [TitleGroup("Inspector")]
        [ShowIf(nameof(flowWarpEnabled))]
        [ValueDropdown(nameof(GetSlotTypeOptions))]
        [Tooltip("繝・け繧ｹ繝√Ε繧ｹ繝ｭ繝・ヨ (ExtA/ExtB/CustomRT)")]
        public int flowWarpSourceSlotType = 5;

        [TitleGroup("Flow Warp/Source")]
        [ShowIf(nameof(flowWarpEnabled))]
        [ValueDropdown(nameof(GetChannelOptions))]
        [Tooltip("菴ｿ逕ｨ縺吶ｋ繝√Ε繝ｳ繝阪Ν (R/G/B/A)")]
        public int flowWarpSourceChannel = 1;

        [TitleGroup("Flow Warp/Source")]
        [ShowIf(nameof(flowWarpEnabled))]
        [ValueDropdown(nameof(GetUVSpaceOptions))]
        [Tooltip("UV蠎ｧ讓咏ｳｻ (SpriteLocal/Screen/TextureRaw/WorldXY)")]
        public int flowWarpSourceUVSpace = 0;

        // 笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊・
        // Color Overlay (繧ｫ繝ｩ繝ｼ繧ｪ繝ｼ繝舌・繝ｬ繧､)
        // 笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊・

        [TitleGroup("Inspector")]
        [ToggleLeft]
        [Tooltip("繧ｫ繝ｩ繝ｼ繧ｪ繝ｼ繝舌・繝ｬ繧､繧呈怏蜉ｹ縺ｫ縺吶ｋ")]
        public bool colorOverlayEnabled = false;

        [TitleGroup("Color Overlay")]
        [ShowIf(nameof(colorOverlayEnabled))]
        [Tooltip("繧ｪ繝ｼ繝舌・繝ｬ繧､縺吶ｋ濶ｲ")]
        public Color colorOverlayColor = Color.white;

        [TitleGroup("Color Overlay")]
        [ShowIf(nameof(colorOverlayEnabled))]
        [ValueDropdown(nameof(GetBlendModeOptions))]
        [Tooltip("繝悶Ξ繝ｳ繝峨Δ繝ｼ繝・(Normal/Multiply/Screen/Overlay/Add/SoftLight遲・")]
        public int colorOverlayBlendMode = 0;

        [TitleGroup("Color Overlay")]
        [ShowIf(nameof(colorOverlayEnabled))]
        [Range(0f, 1f)]
        [Tooltip("繧ｨ繝輔ぉ繧ｯ繝医・蠑ｷ蠎ｦ縲・=辟｡蜉ｹ縲・=譛螟ｧ")]
        public float colorOverlayIntensity = 1f;

        [TitleGroup("Inspector")]
        [ShowIf(nameof(colorOverlayEnabled))]
        [ValueDropdown(nameof(GetSlotTypeOptions))]
        [Tooltip("繝・け繧ｹ繝√Ε繧ｹ繝ｭ繝・ヨ (ExtA/ExtB/CustomRT)")]
        public int colorOverlaySourceSlotType = 5;

        [TitleGroup("Color Overlay/Source")]
        [ShowIf(nameof(colorOverlayEnabled))]
        [ValueDropdown(nameof(GetChannelOptions))]
        [Tooltip("菴ｿ逕ｨ縺吶ｋ繝√Ε繝ｳ繝阪Ν (R/G/B/A)")]
        public int colorOverlaySourceChannel = 1;

        [TitleGroup("Color Overlay/Source")]
        [ShowIf(nameof(colorOverlayEnabled))]
        [ValueDropdown(nameof(GetUVSpaceOptions))]
        [Tooltip("UV蠎ｧ讓咏ｳｻ (SpriteLocal/Screen/TextureRaw/WorldXY)")]
        public int colorOverlaySourceUVSpace = 0;

        // 笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊・
        // Color Ramp (繧ｫ繝ｩ繝ｼ繝ｩ繝ｳ繝・
        // 笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊・

        [TitleGroup("Inspector")]
        [ToggleLeft]
        [Tooltip("繧ｫ繝ｩ繝ｼ繝ｩ繝ｳ繝励お繝輔ぉ繧ｯ繝医ｒ譛牙柑縺ｫ縺吶ｋ")]
        public bool colorRampEnabled = false;

        [TitleGroup("Color Ramp")]
        [ShowIf(nameof(colorRampEnabled))]
        [Tooltip("繧ｫ繝ｩ繝ｼ繝ｩ繝ｳ繝励ユ繧ｯ繧ｹ繝√Ε (1D縺ｾ縺溘・讓ｪ譁ｹ蜷代・繧ｰ繝ｩ繝・・繧ｷ繝ｧ繝ｳ)")]
        public Texture? colorRampTexture;

        [TitleGroup("Color Ramp")]
        [ShowIf(nameof(colorRampEnabled))]
        [Range(0f, 1f)]
        [Tooltip("繧ｨ繝輔ぉ繧ｯ繝医・蠑ｷ蠎ｦ")]
        public float colorRampIntensity = 1f;

        [TitleGroup("Color Ramp")]
        [ShowIf(nameof(colorRampEnabled))]
        [ToggleLeft]
        [Tooltip("蜈・・繧｢繝ｫ繝輔ぃ蛟､繧剃ｿ晄戟縺吶ｋ")]
        public bool colorRampPreserveAlpha = true;

        [TitleGroup("Inspector")]
        [ShowIf(nameof(colorRampEnabled))]
        [ValueDropdown(nameof(GetSlotTypeOptions))]
        [Tooltip("繝・け繧ｹ繝√Ε繧ｹ繝ｭ繝・ヨ (ExtA/ExtB/CustomRT)")]
        public int colorRampSourceSlotType = 5;

        [TitleGroup("Color Ramp/Source")]
        [ShowIf(nameof(colorRampEnabled))]
        [ValueDropdown(nameof(GetChannelOptions))]
        [Tooltip("菴ｿ逕ｨ縺吶ｋ繝√Ε繝ｳ繝阪Ν (R/G/B/A)")]
        public int colorRampSourceChannel = 1;

        [TitleGroup("Color Ramp/Source")]
        [ShowIf(nameof(colorRampEnabled))]
        [ValueDropdown(nameof(GetUVSpaceOptions))]
        [Tooltip("UV蠎ｧ讓咏ｳｻ (SpriteLocal/Screen/TextureRaw/WorldXY)")]
        public int colorRampSourceUVSpace = 0;

        // 笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊・
        // Refraction (螻域釜蜉ｹ譫・
        // 笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊・

        [TitleGroup("Refraction", "閭梧勹繧呈ｭｪ縺ｾ縺帙ｋ螻域釜繧ｨ繝輔ぉ繧ｯ繝・(繧ｬ繝ｩ繧ｹ縲∵ｰｴ縲∫・豌玲･ｼ縺ｪ縺ｩ)")]
        [ToggleLeft]
        [Tooltip("螻域釜繧ｨ繝輔ぉ繧ｯ繝医ｒ譛牙柑縺ｫ縺吶ｋ")]
        public bool refractionEnabled = false;

        [TitleGroup("Refraction")]
        [ShowIf(nameof(refractionEnabled))]
        [Tooltip("螻域釜縺ｮ蠑ｷ蠎ｦ (X, Y)")]
        public Vector2 refractionStrength = new Vector2(0.1f, 0.1f);

        [TitleGroup("Refraction")]
        [ShowIf(nameof(refractionEnabled))]
        [Range(0f, 1f)]
        [Tooltip("濶ｲ蜿主ｷｮ縺ｮ蠑ｷ蠎ｦ縲ゅ・繝ｪ繧ｺ繝蜉ｹ譫懊ｒ霑ｽ蜉")]
        public float refractionChromaticAberration = 0f;

        [TitleGroup("Inspector")]
        [ShowIf(nameof(refractionEnabled))]
        [ValueDropdown(nameof(GetSlotTypeOptions))]
        [Tooltip("繝・け繧ｹ繝√Ε繧ｹ繝ｭ繝・ヨ (ExtA/ExtB/CustomRT)")]
        public int refractionSourceSlotType = 5;

        [TitleGroup("Refraction/Source")]
        [ShowIf(nameof(refractionEnabled))]
        [ValueDropdown(nameof(GetChannelOptions))]
        [Tooltip("菴ｿ逕ｨ縺吶ｋ繝√Ε繝ｳ繝阪Ν (R/G/B/A)")]
        public int refractionSourceChannel = 1;

        [TitleGroup("Refraction/Source")]
        [ShowIf(nameof(refractionEnabled))]
        [ValueDropdown(nameof(GetUVSpaceOptions))]
        [Tooltip("UV蠎ｧ讓咏ｳｻ (SpriteLocal/Screen/TextureRaw/WorldXY)")]
        public int refractionSourceUVSpace = 0;

        // 笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊・
        // Caustics (繧ｳ繝ｼ繧ｹ繝・ぅ繧ｯ繧ｹ/豌ｴ髱｢縺ｮ蜈牙ｱ域釜讓｡讒・
        // 笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊・

        [TitleGroup("Inspector")]
        [ToggleLeft]
        [Tooltip("繧ｳ繝ｼ繧ｹ繝・ぅ繧ｯ繧ｹ繧ｨ繝輔ぉ繧ｯ繝医ｒ譛牙柑縺ｫ縺吶ｋ")]
        public bool causticsEnabled = false;

        [TitleGroup("Caustics")]
        [ShowIf(nameof(causticsEnabled))]
        [Tooltip("繧ｳ繝ｼ繧ｹ繝・ぅ繧ｯ繧ｹ縺ｮ逋ｺ蜈芽牡")]
        public Color causticsColor = Color.white;

        [TitleGroup("Caustics")]
        [ShowIf(nameof(causticsEnabled))]
        [Range(0f, 5f)]
        [Tooltip("逋ｺ蜈峨・蠑ｷ蠎ｦ")]
        public float causticsIntensity = 1f;

        [TitleGroup("Caustics")]
        [ShowIf(nameof(causticsEnabled))]
        [Range(0f, 1f)]
        [Tooltip("繝代ち繝ｼ繝ｳ縺瑚｡ｨ遉ｺ縺輔ｌ繧九＠縺阪＞蛟､")]
        public float causticsThreshold = 0.5f;

        [TitleGroup("Caustics")]
        [ShowIf(nameof(causticsEnabled))]
        [Range(0f, 1f)]
        [Tooltip("Inspector setting.")]
        public float causticsSoftness = 0.1f;

        [TitleGroup("Caustics")]
        [ShowIf(nameof(causticsEnabled))]
        [Tooltip("繝代ち繝ｼ繝ｳA縺ｮ繧ｹ繧ｯ繝ｭ繝ｼ繝ｫ騾溷ｺｦ (X, Y)")]
        public Vector2 causticsScrollA = new Vector2(0.1f, 0.1f);

        [TitleGroup("Caustics")]
        [ShowIf(nameof(causticsEnabled))]
        [Tooltip("Inspector setting.")]
        public Vector2 causticsScrollB = new Vector2(-0.1f, 0.05f);

        [TitleGroup("Caustics/Source A", "繧ｳ繝ｼ繧ｹ繝・ぅ繧ｯ繧ｹ繝代ち繝ｼ繝ｳA 縺ｮ繝・け繧ｹ繝√Ε繧ｽ繝ｼ繧ｹ")]
        [ShowIf(nameof(causticsEnabled))]
        [ValueDropdown(nameof(GetSlotTypeOptions))]
        [Tooltip("繝・け繧ｹ繝√Ε繧ｹ繝ｭ繝・ヨ")]
        public int causticsSourceASlotType = 5;

        [TitleGroup("Caustics/Source A")]
        [ShowIf(nameof(causticsEnabled))]
        [ValueDropdown(nameof(GetChannelOptions))]
        [Tooltip("菴ｿ逕ｨ縺吶ｋ繝√Ε繝ｳ繝阪Ν")]
        public int causticsSourceAChannel = 0;

        [TitleGroup("Caustics/Source A")]
        [ShowIf(nameof(causticsEnabled))]
        [ValueDropdown(nameof(GetUVSpaceOptions))]
        [Tooltip("UV蠎ｧ讓咏ｳｻ")]
        public int causticsSourceAUVSpace = 0;

        [TitleGroup("Caustics/Source B", "繧ｳ繝ｼ繧ｹ繝・ぅ繧ｯ繧ｹ繝代ち繝ｼ繝ｳB 縺ｮ繝・け繧ｹ繝√Ε繧ｽ繝ｼ繧ｹ (A縺ｨB縺御ｹ礼ｮ励＆繧後ｋ)")]
        [ShowIf(nameof(causticsEnabled))]
        [ValueDropdown(nameof(GetSlotTypeOptions))]
        [Tooltip("繝・け繧ｹ繝√Ε繧ｹ繝ｭ繝・ヨ")]
        public int causticsSourceBSlotType = 6;

        [TitleGroup("Caustics/Source B")]
        [ShowIf(nameof(causticsEnabled))]
        [ValueDropdown(nameof(GetChannelOptions))]
        [Tooltip("菴ｿ逕ｨ縺吶ｋ繝√Ε繝ｳ繝阪Ν")]
        public int causticsSourceBChannel = 0;

        [TitleGroup("Caustics/Source B")]
        [ShowIf(nameof(causticsEnabled))]
        [ValueDropdown(nameof(GetUVSpaceOptions))]
        [Tooltip("UV蠎ｧ讓咏ｳｻ")]
        public int causticsSourceBUVSpace = 0;

        // 笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊・
        // Ripple (豕｢邏句柑譫・
        // 笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊・

        [TitleGroup("Inspector")]
        [ToggleLeft]
        [Tooltip("豕｢邏九お繝輔ぉ繧ｯ繝医ｒ譛牙柑縺ｫ縺吶ｋ")]
        public bool rippleEnabled = false;

        [TitleGroup("Ripple")]
        [ShowIf(nameof(rippleEnabled))]
        [Tooltip("豕｢邏九・荳ｭ蠢・ｺｧ讓・(UV遨ｺ髢・ 0-1)")]
        public Vector2 rippleCenter = new Vector2(0.5f, 0.5f);

        [TitleGroup("Ripple")]
        [ShowIf(nameof(rippleEnabled))]
        [Tooltip("豕｢縺ｮ繝代Λ繝｡繝ｼ繧ｿ: X=蜻ｨ豕｢謨ｰ, Y=謖ｯ蟷・ Z=貂幄｡ｰ, W=騾溷ｺｦ")]
        public Vector4 rippleWaveParams = new Vector4(10f, 0.05f, 2f, 5f);

        [TitleGroup("Ripple")]
        [ShowIf(nameof(rippleEnabled))]
        [Range(0f, 1f)]
        [Tooltip("豕｢邏九・謖ｯ蟷・(豁ｪ縺ｿ縺ｮ蠑ｷ縺・")]
        public float rippleAmplitude = 0.1f;

        [TitleGroup("Ripple")]
        [ShowIf(nameof(rippleEnabled))]
        [Tooltip("豕｢邏九・繝輔ぉ繝ｼ繧ｺ繧ｪ繝輔そ繝・ヨ (繧｢繝九Γ繝ｼ繧ｷ繝ｧ繝ｳ蛻ｶ蠕｡逕ｨ)")]
        public float ripplePhase = 0f;

        [TitleGroup("Ripple")]
        [ShowIf(nameof(rippleEnabled))]
        [ToggleLeft]
        [Tooltip("UV繧呈ｭｪ縺ｾ縺帙ｋ (辟｡蜉ｹ縺ｫ縺吶ｋ縺ｨ濶ｲ縺ｮ縺ｿ螟牙喧)")]
        public bool rippleDistortUV = true;

        [TitleGroup("Ripple")]
        [ShowIf(nameof(rippleEnabled))]
        [Range(0f, 1f)]
        [Tooltip("豕｢邏玖牡縺ｮ繝悶Ξ繝ｳ繝蛾㍼")]
        public float rippleColorBlend = 0f;

        [TitleGroup("Ripple")]
        [ShowIf(nameof(rippleEnabled))]
        [Tooltip("豕｢邏九・繝上う繝ｩ繧､繝郁牡")]
        public Color rippleColor = Color.white;

        // 笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊・
        // Hue Shift (濶ｲ逶ｸ繧ｷ繝輔ヨ)
        // 笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊・

        [TitleGroup("Inspector")]
        [ToggleLeft]
        [Tooltip("濶ｲ逶ｸ繧ｷ繝輔ヨ繧ｨ繝輔ぉ繧ｯ繝医ｒ譛牙柑縺ｫ縺吶ｋ")]
        public bool hueShiftEnabled = false;

        [TitleGroup("Hue Shift")]
        [ShowIf(nameof(hueShiftEnabled))]
        [Range(-1f, 1f)]
        [Tooltip("濶ｲ逶ｸ縺ｮ繧ｷ繝輔ヨ驥・(-1 ~ 1縺ｧ濶ｲ逶ｸ迺ｰ繧剃ｸ蜻ｨ)")]
        public float hueShiftAmount = 0f;

        [TitleGroup("Hue Shift")]
        [ShowIf(nameof(hueShiftEnabled))]
        [Range(-1f, 1f)]
        [Tooltip("蠖ｩ蠎ｦ縺ｮ隱ｿ謨ｴ驥・(-1=繝｢繝弱け繝ｭ, 0=螟牙喧縺ｪ縺・ 1=譛螟ｧ蠖ｩ蠎ｦ)")]
        public float hueSaturationMod = 0f;

        [TitleGroup("Hue Shift")]
        [ShowIf(nameof(hueShiftEnabled))]
        [Range(-1f, 1f)]
        [Tooltip("譏主ｺｦ縺ｮ隱ｿ謨ｴ驥・(-1=逵溘▲鮟・ 0=螟牙喧縺ｪ縺・ 1=逵溘▲逋ｽ)")]
        public float hueValueMod = 0f;

        [TitleGroup("Hue Shift/Mask Source", "濶ｲ逶ｸ繧ｷ繝輔ヨ縺ｮ驕ｩ逕ｨ遽・峇繧貞宛蠕｡縺吶ｋ繝槭せ繧ｯ繝・け繧ｹ繝√Ε")]
        [ShowIf(nameof(hueShiftEnabled))]
        [ValueDropdown(nameof(GetSlotTypeOptions))]
        [Tooltip("繝・け繧ｹ繝√Ε繧ｹ繝ｭ繝・ヨ")]
        public int hueShiftMaskSlotType = 0;

        [TitleGroup("Hue Shift/Mask Source")]
        [ShowIf(nameof(hueShiftEnabled))]
        [ValueDropdown(nameof(GetChannelOptions))]
        [Tooltip("菴ｿ逕ｨ縺吶ｋ繝√Ε繝ｳ繝阪Ν")]
        public int hueShiftMaskChannel = 0;

        [TitleGroup("Hue Shift/Mask Source")]
        [ShowIf(nameof(hueShiftEnabled))]
        [ValueDropdown(nameof(GetUVSpaceOptions))]
        [Tooltip("UV蠎ｧ讓咏ｳｻ")]
        public int hueShiftMaskUVSpace = 0;

        // 笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊・
        // Normal Map (繝弱・繝槭Ν繝槭ャ繝・
        // 笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊・

        [TitleGroup("Normal Map", "謫ｬ莨ｼ逧・↑遶倶ｽ捺─繧剃ｸ弱∴繧九ヮ繝ｼ繝槭Ν繝槭ャ繝励Λ繧､繝・ぅ繝ｳ繧ｰ")]
        [ToggleLeft]
        [Tooltip("繝弱・繝槭Ν繝槭ャ繝励お繝輔ぉ繧ｯ繝医ｒ譛牙柑縺ｫ縺吶ｋ")]
        public bool normalMapEnabled = false;

        [TitleGroup("Normal Map")]
        [ShowIf(nameof(normalMapEnabled))]
        [Range(0f, 2f)]
        [Tooltip("繝弱・繝槭Ν繝槭ャ繝励・蠑ｷ蠎ｦ")]
        public float normalMapStrength = 1f;

        [TitleGroup("Normal Map")]
        [ShowIf(nameof(normalMapEnabled))]
        [Tooltip("繝ｩ繧､繝医・譁ｹ蜷代・繧ｯ繝医Ν (豁｣隕丞喧謗ｨ螂ｨ)")]
        public Vector3 normalMapLightDir = new Vector3(0f, 0f, 1f);

        [TitleGroup("Normal Map/Source", "繝弱・繝槭Ν繝槭ャ繝励ユ繧ｯ繧ｹ繝√Ε縺ｮ繧ｽ繝ｼ繧ｹ")]
        [ShowIf(nameof(normalMapEnabled))]
        [ValueDropdown(nameof(GetSlotTypeOptions))]
        [Tooltip("繝・け繧ｹ繝√Ε繧ｹ繝ｭ繝・ヨ")]
        public int normalMapSourceSlotType = 5;

        [TitleGroup("Normal Map/Source")]
        [ShowIf(nameof(normalMapEnabled))]
        [ValueDropdown(nameof(GetChannelOptions))]
        [Tooltip("菴ｿ逕ｨ縺吶ｋ繝√Ε繝ｳ繝阪Ν")]
        public int normalMapSourceChannel = 1;

        [TitleGroup("Normal Map/Source")]
        [ShowIf(nameof(normalMapEnabled))]
        [ValueDropdown(nameof(GetUVSpaceOptions))]
        [Tooltip("UV蠎ｧ讓咏ｳｻ")]
        public int normalMapSourceUVSpace = 0;

        // 笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊・
        // Emission (逋ｺ蜈・
        // 笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊・

        [TitleGroup("Emission", "繧ｹ繝励Λ繧､繝医↓逋ｺ蜈牙柑譫懊ｒ霑ｽ蜉")]
        [ToggleLeft]
        [Tooltip("逋ｺ蜈峨お繝輔ぉ繧ｯ繝医ｒ譛牙柑縺ｫ縺吶ｋ")]
        public bool emissionEnabled = false;

        [TitleGroup("Emission")]
        [ShowIf(nameof(emissionEnabled))]
        [Tooltip("逋ｺ蜈芽牡 (HDR繧ｫ繝ｩ繝ｼ謗ｨ螂ｨ)")]
        public Color emissionColor = Color.white;

        [TitleGroup("Emission")]
        [ShowIf(nameof(emissionEnabled))]
        [Range(0f, 10f)]
        [Tooltip("逋ｺ蜈峨・蠑ｷ蠎ｦ")]
        public float emissionIntensity = 1f;

        // 笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊・
        // Mask (繝槭せ繧ｯ)
        // 笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊・

        [TitleGroup("Mask", "繝・け繧ｹ繝√Ε繝吶・繧ｹ縺ｮ繧｢繝ｫ繝輔ぃ繝槭せ繧ｯ")]
        [ToggleLeft]
        [Tooltip("繝槭せ繧ｯ繧ｨ繝輔ぉ繧ｯ繝医ｒ譛牙柑縺ｫ縺吶ｋ")]
        public bool maskEnabled = false;

        [TitleGroup("Mask")]
        [ShowIf(nameof(maskEnabled))]
        [Range(0f, 1f)]
        [Tooltip("Inspector setting.")]
        public float maskThreshold = 0.5f;

        [TitleGroup("Mask")]
        [ShowIf(nameof(maskEnabled))]
        [Range(0f, 1f)]
        [Tooltip("繝槭せ繧ｯ蠅・阜縺ｮ繧ｽ繝輔ヨ繝阪せ縲・=繝上・繝峨お繝・ず縲・=繝輔Ν繧ｽ繝輔ヨ")]
        public float maskSoftness = 0.1f;

        [TitleGroup("Mask/Source", "繝槭せ繧ｯ繝・け繧ｹ繝√Ε縺ｮ繧ｽ繝ｼ繧ｹ")]
        [ShowIf(nameof(maskEnabled))]
        [ValueDropdown(nameof(GetSlotTypeOptions))]
        [Tooltip("繝・け繧ｹ繝√Ε繧ｹ繝ｭ繝・ヨ")]
        public int maskSourceSlotType = 5;

        [TitleGroup("Mask/Source")]
        [ShowIf(nameof(maskEnabled))]
        [ValueDropdown(nameof(GetChannelOptions))]
        [Tooltip("菴ｿ逕ｨ縺吶ｋ繝√Ε繝ｳ繝阪Ν")]
        public int maskSourceChannel = 1;

        [TitleGroup("Mask/Source")]
        [ShowIf(nameof(maskEnabled))]
        [ValueDropdown(nameof(GetUVSpaceOptions))]
        [Tooltip("UV蠎ｧ讓咏ｳｻ")]
        public int maskSourceUVSpace = 0;

        // 笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊・
        // Outline 2D
        // 笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊・

        [TitleGroup("Inspector")]
        [ToggleLeft]
        [Tooltip("騾壼ｸｸ繧｢繧ｦ繝医Λ繧､繝ｳ繧呈怏蜉ｹ縺ｫ縺吶ｋ")]
        public bool outlineEnabled = false;

        [TitleGroup("Outline")]
        [ShowIf(nameof(outlineEnabled))]
        [LabelText("Mode")]
        [ValueDropdown(nameof(GetOutlineModeOptions))]
        [Tooltip("Inspector setting.")]
        public int outlineMode = 10;

        [TitleGroup("Outline")]
        [ShowIf(nameof(outlineEnabled))]
        [Tooltip("Inspector setting.")]
        public Color outlineColor = Color.white;

        [TitleGroup("Outline")]
        [ShowIf(nameof(outlineEnabled))]
        [LabelText("Direction")]
        [EnumToggleButtons]
        [Tooltip("Inspector setting.")]
        public OutlineDirectionMask outlineDirectionMask = OutlineDirectionMask.All;

        [TitleGroup("Outline")]
        [ShowIf(nameof(outlineEnabled))]
        [LabelText("Auto Color")]
        [ToggleLeft]
        [Tooltip("Inspector setting.")]
        public bool outlineAutoColorEnabled = false;

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAutoColorSettings))]
        [LabelText("Mode")]
        [EnumToggleButtons]
        [Tooltip("Inspector setting.")]
        public OutlineAutoColorMode outlineAutoColorMode = OutlineAutoColorMode.Hsl;

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAutoColorSettings))]
        [LabelText("H")]
        [Range(-1f, 1f)]
        [Tooltip("Inspector setting.")]
        public float outlineAutoHue = 0f;

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAutoColorSettings))]
        [LabelText("S")]
        [Range(-1f, 1f)]
        [Tooltip("Inspector setting.")]
        public float outlineAutoSaturation = 0f;

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAutoColorSettings))]
        [LabelText("L")]
        [Range(-1f, 1f)]
        [Tooltip("Inspector setting.")]
        public float outlineAutoLightness = 0f;

        [TitleGroup("Outline")]
        [ShowIf(nameof(outlineEnabled))]
        [LabelText("Animated Gradient")]
        [ToggleLeft]
        [Tooltip("Inspector setting.")]
        public bool outlineAnimatedGradientEnabled = false;

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAnimatedGradientSettings))]
        [LabelText("Pattern Type")]
        [ValueDropdown(nameof(GetAnimatedNoisePatternOptions))]
        public int outlineAnimatedGradientPatternType = 10;

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAnimatedGradientSettings))]
        [LabelText("Master Strength")]
        [Min(0f)]
        public float outlineAnimatedGradientMasterStrength = 1f;

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAnimatedGradientSettings))]
        [LabelText("Noise Scale")]
        [Min(0.0001f)]
        public float outlineAnimatedGradientNoiseScale = 6f;

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAnimatedGradientSettings))]
        [LabelText("Direction")]
        public Vector2 outlineAnimatedGradientNoiseDirection = new Vector2(1f, 0f);

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAnimatedGradientSettings))]
        [LabelText("Speed")]
        public float outlineAnimatedGradientNoiseSpeed = 0.2f;

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAnimatedGradientSettings))]
        [LabelText("Offset")]
        public Vector2 outlineAnimatedGradientNoiseOffset = Vector2.zero;

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAnimatedGradientSettings))]
        [LabelText("Rotation Speed")]
        public float outlineAnimatedGradientRotationSpeed = 0f;

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAnimatedGradientSettings))]
        [LabelText("Pulse Amplitude")]
        [Min(0f)]
        public float outlineAnimatedGradientPulseAmplitude = 0f;

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAnimatedGradientSettings))]
        [LabelText("Pulse Speed")]
        public float outlineAnimatedGradientPulseSpeed = 1f;

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAnimatedGradientSettings))]
        [LabelText("Warp Pattern")]
        [ValueDropdown(nameof(GetAnimatedNoisePatternOptions))]
        [HideInInspector]
        public int outlineAnimatedGradientWarpPatternType = 10;

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAnimatedGradientSettings))]
        [LabelText("Warp Scale")]
        [Min(0.0001f)]
        public float outlineAnimatedGradientWarpScale = 2f;

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAnimatedGradientSettings))]
        [LabelText("Warp Strength")]
        [Min(0f)]
        public float outlineAnimatedGradientWarpStrength = 0.1f;

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAnimatedGradientSettings))]
        [LabelText("Warp Direction")]
        public Vector2 outlineAnimatedGradientWarpDirection = new Vector2(0.71f, 0.43f);

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAnimatedGradientSettings))]
        [LabelText("Warp Speed")]
        public float outlineAnimatedGradientWarpSpeed = 0.35f;

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAnimatedGradientSettings))]
        [LabelText("Loop Seconds")]
        [Min(0f)]
        public float outlineAnimatedGradientLoopSeconds = 0f;

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAnimatedGradientSettings))]
        [LabelText("Octaves")]
        [Min(1f)]
        public float outlineAnimatedGradientOctaves = 4f;

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAnimatedGradientSettings))]
        [LabelText("Lacunarity")]
        [Min(1f)]
        public float outlineAnimatedGradientLacunarity = 2f;

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAnimatedGradientSettings))]
        [LabelText("Gain")]
        [Range(0f, 1f)]
        public float outlineAnimatedGradientGain = 0.5f;

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAnimatedGradientSettings))]
        [LabelText("Cell Sharpness")]
        [Min(0.01f)]
        public float outlineAnimatedGradientCellSharpness = 1.5f;

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAnimatedGradientSettings))]
        [LabelText("Pattern Contrast")]
        [Min(0f)]
        public float outlineAnimatedGradientPatternContrast = 1f;

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAnimatedGradientSettings))]
        [LabelText("Hue Amp")]
        [Min(0f)]
        public float outlineAnimatedGradientHueAmplitude = 0.0025f;

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAnimatedGradientSettings))]
        [LabelText("Sat Amp")]
        [Min(0f)]
        public float outlineAnimatedGradientSaturationAmplitude = 0.008f;

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAnimatedGradientSettings))]
        [LabelText("Light Amp")]
        [Min(0f)]
        public float outlineAnimatedGradientLightnessAmplitude = 0.015f;

        [TitleGroup("Outline")]
        [ShowIf(nameof(outlineEnabled))]
        [Min(0f)]
        [Tooltip("Inspector setting.")]
        public float outlineWidth = 1f;

        [TitleGroup("Outline")]
        [ShowIf(nameof(outlineEnabled))]
        [Range(0f, 1f)]
        [Tooltip("繧｢繧ｦ繝医Λ繧､繝ｳ縺ｮ荳埼乗・蠎ｦ")]
        public float outlineOpacity = 1f;

        [TitleGroup("Outline")]
        [ShowIf(nameof(outlineEnabled))]
        [Range(0f, 1f)]
        [Tooltip("繧｢繧ｦ繝医Λ繧､繝ｳ縺ｮ繧ｽ繝輔ヨ繝阪せ")]
        public float outlineSoftness = 0f;

        [TitleGroup("Outline")]
        [ShowIf(nameof(outlineEnabled))]
        [LabelText("Blend Mode")]
        [ValueDropdown(nameof(GetOutlineBlendModeOptions))]
        [Tooltip("Inspector setting.")]
        public int outlineBlendMode = 10;

        [TitleGroup("Outline")]
        [ShowIf(nameof(outlineEnabled))]
        [ToggleLeft]
        [Tooltip("Inspector setting.")]
        public bool outlinePixelPerfect = true;

        [TitleGroup("Outline")]
        [ShowIf(nameof(outlineEnabled))]
        [LabelText("Width Unit")]
        [ValueDropdown(nameof(GetOutlineWidthUnitOptions))]
        [Tooltip("Inspector setting.")]
        public int outlineWidthUnit = 10;

        [TitleGroup("Outline")]
        [ShowIf(nameof(outlineEnabled))]
        [Min(0.0001f)]
        [Tooltip("Inspector setting.")]
        public float outlinePixelStep = 1f;

        [TitleGroup("Outline")]
        [ShowIf(nameof(outlineEnabled))]
        [LabelText("Sample Pattern")]
        [ValueDropdown(nameof(GetOutlineSamplePatternOptions))]
        [Tooltip("Inspector setting.")]
        public int outlineSamplePattern = 10;

        [TitleGroup("Outline")]
        [ShowIf(nameof(outlineEnabled))]
        [ToggleLeft]
        [Tooltip("Inspector setting.")]
        public bool outlineMaskRespect = true;

        [TitleGroup("Outline")]
        [ShowIf(nameof(outlineEnabled))]
        [ToggleLeft]
        [Tooltip("Inspector setting.")]
        public bool outlineUseVertexColor = false;

        [TitleGroup("Outline")]
        [ShowIf(nameof(outlineEnabled))]
        [ToggleLeft]
        [Tooltip("Inspector setting.")]
        public bool outlineUvClampEnabled = true;

        [TitleGroup("Outline")]
        [ShowIf(nameof(outlineEnabled))]
        [LabelText("ZTest Mode")]
        [ValueDropdown(nameof(GetOutlineZTestModeOptions))]
        [Tooltip("Inspector setting.")]
        public int outlineZTestMode = 10;

        // 笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊・
        // Text FX (Outline / Shadow)
        // 笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊・

        [TitleGroup("Inspector")]
        [TitleGroup("Text Fx/Outline")]
        [ToggleLeft]
        [Tooltip("繝・く繧ｹ繝医い繧ｦ繝医Λ繧､繝ｳ繧呈怏蜉ｹ縺ｫ縺吶ｋ")]
        public bool textOutlineEnabled = false;

        [TitleGroup("Text Fx/Outline")]
        [ShowIf(nameof(textOutlineEnabled))]
        [Tooltip("Inspector setting.")]
        public Color textOutlineColor = Color.black;

        [TitleGroup("Text Fx/Outline")]
        [ShowIf(nameof(textOutlineEnabled))]
        [Min(0f)]
        [Tooltip("繧｢繧ｦ繝医Λ繧､繝ｳ縺ｮ螟ｪ縺・(px)")]
        public float textOutlineThickness = 1f;

        [TitleGroup("Text Fx/Outline")]
        [ShowIf(nameof(textOutlineEnabled))]
        [Range(0f, 1f)]
        [Tooltip("繧｢繧ｦ繝医Λ繧､繝ｳ縺ｮ繧ｽ繝輔ヨ繝阪せ")]
        public float textOutlineSoftness = 0.1f;

        [TitleGroup("Text Fx/Outline")]
        [ShowIf(nameof(textOutlineEnabled))]
        [LabelText("Direction")]
        [EnumToggleButtons]
        [Tooltip("Inspector setting.")]
        public TextOutlineDirectionMask textOutlineDirectionMask = TextOutlineDirectionMask.All;

        [TitleGroup("Text Fx/Outline")]
        [ShowIf(nameof(textOutlineEnabled))]
        [LabelText("Auto Color")]
        [ToggleLeft]
        [Tooltip("Inspector setting.")]
        public bool textOutlineAutoColorEnabled = false;

        [TitleGroup("Text Fx/Outline")]
        [ShowIf(nameof(ShowTextOutlineAutoColorSettings))]
        [LabelText("Mode")]
        [EnumToggleButtons]
        [Tooltip("Inspector setting.")]
        public TextOutlineAutoColorMode textOutlineAutoColorMode = TextOutlineAutoColorMode.Hsl;

        [TitleGroup("Text Fx/Outline")]
        [ShowIf(nameof(ShowTextOutlineAutoColorSettings))]
        [LabelText("H")]
        [Range(-1f, 1f)]
        [Tooltip("Inspector setting.")]
        public float textOutlineAutoHue = 0f;

        [TitleGroup("Text Fx/Outline")]
        [ShowIf(nameof(ShowTextOutlineAutoColorSettings))]
        [LabelText("S")]
        [Range(-1f, 1f)]
        [Tooltip("Inspector setting.")]
        public float textOutlineAutoSaturation = 0f;

        [TitleGroup("Text Fx/Outline")]
        [ShowIf(nameof(ShowTextOutlineAutoColorSettings))]
        [LabelText("L")]
        [Range(-1f, 1f)]
        [Tooltip("Inspector setting.")]
        public float textOutlineAutoLightness = 0f;

        [TitleGroup("Text Fx/Shadow")]
        [ToggleLeft]
        [Tooltip("繝・く繧ｹ繝医す繝｣繝峨え繧呈怏蜉ｹ縺ｫ縺吶ｋ")]
        public bool textShadowEnabled = false;

        [TitleGroup("Text Fx/Shadow")]
        [ShowIf(nameof(textShadowEnabled))]
        [Tooltip("繧ｷ繝｣繝峨え濶ｲ")]
        public Color textShadowColor = new Color(0, 0, 0, 0.5f);

        [TitleGroup("Text Fx/Shadow")]
        [ShowIf(nameof(textShadowEnabled))]
        [Tooltip("繧ｷ繝｣繝峨え縺ｮ繧ｪ繝輔そ繝・ヨ (px)")]
        public Vector2 textShadowOffset = new Vector2(1f, -1f);

        [TitleGroup("Text Fx/Shadow")]
        [ShowIf(nameof(textShadowEnabled))]
        [Range(0f, 1f)]
        [Tooltip("繧ｷ繝｣繝峨え縺ｮ繧ｽ繝輔ヨ繝阪せ")]
        public float textShadowSoftness = 0.1f;

        [TitleGroup("Text Fx/Glow")]
        [ToggleLeft]
        [Tooltip("繝・く繧ｹ繝医げ繝ｭ繝ｼ繧呈怏蜉ｹ縺ｫ縺吶ｋ")]
        public bool textGlowEnabled = false;

        [TitleGroup("Text Fx/Glow")]
        [ShowIf(nameof(textGlowEnabled))]
        [Tooltip("繧ｰ繝ｭ繝ｼ濶ｲ")]
        public Color textGlowColor = new Color(1f, 1f, 1f, 0.5f);

        [TitleGroup("Text Fx/Glow")]
        [ShowIf(nameof(textGlowEnabled))]
        [Min(0f)]
        [Tooltip("Inspector setting.")]
        public float textGlowThickness = 2f;

        [TitleGroup("Text Fx/Glow")]
        [ShowIf(nameof(textGlowEnabled))]
        [Range(0f, 1f)]
        [Tooltip("繧ｰ繝ｭ繝ｼ縺ｮ繧ｽ繝輔ヨ繝阪せ")]
        public float textGlowSoftness = 0.2f;

        // --- BlendColor2D ---
        [TitleGroup("Blend Color", "譁ｹ蜷代げ繝ｩ繝・・繧ｷ繝ｧ繝ｳ莉倥″繝悶Ξ繝ｳ繝峨き繝ｩ繝ｼ")]
        [ToggleLeft]
        [Tooltip("BlendColor2D 繧呈怏蜉ｹ縺ｫ縺吶ｋ")]
        public bool blendColor2DEnabled = false;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(blendColor2DEnabled))]
        [Tooltip("Inspector setting.")]
        public Color blendColor2DColor = Color.white;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(blendColor2DEnabled))]
        [Range(0f, 1f)]
        [LabelText("Intensity")]
        public float blendColor2DBlendIntensity = 0f;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(blendColor2DEnabled))]
        [LabelText("Gradient Direction")]
        [ValueDropdown(nameof(GetBlendColorGradientDirectionOptions))]
        public int blendColor2DBlendGradDirection = 0;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(blendColor2DEnabled))]
        [Range(0f, 1f)]
        [LabelText("Gradient Amount")]
        public float blendColor2DBlendGradationAmount = 0f;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(blendColor2DEnabled))]
        [Range(0f, 1f)]
        [LabelText("Softness")]
        public float blendColor2DBlendSoftness = 1f;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(blendColor2DEnabled))]
        [LabelText("Blend Mode")]
        [ValueDropdown(nameof(GetBlendModeOptions))]
        public int blendColor2DBlendMode = 0;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(blendColor2DEnabled))]
        [LabelText("Animated Gradient")]
        [ToggleLeft]
        [Tooltip("Inspector setting.")]
        public bool blendColor2DAnimatedGradientEnabled = false;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(ShowBlendColorAnimatedGradientSettings))]
        [LabelText("Pattern Type")]
        [ValueDropdown(nameof(GetAnimatedNoisePatternOptions))]
        public int blendColor2DAnimatedGradientPatternType = 10;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(ShowBlendColorAnimatedGradientSettings))]
        [LabelText("Master Strength")]
        [Min(0f)]
        public float blendColor2DAnimatedGradientMasterStrength = 1f;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(ShowBlendColorAnimatedGradientSettings))]
        [LabelText("Noise Scale")]
        [Min(0.0001f)]
        public float blendColor2DAnimatedGradientNoiseScale = 6f;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(ShowBlendColorAnimatedGradientSettings))]
        [LabelText("Direction")]
        public Vector2 blendColor2DAnimatedGradientNoiseDirection = new Vector2(1f, 0f);

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(ShowBlendColorAnimatedGradientSettings))]
        [LabelText("Speed")]
        public float blendColor2DAnimatedGradientNoiseSpeed = 0.2f;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(ShowBlendColorAnimatedGradientSettings))]
        [LabelText("Offset")]
        public Vector2 blendColor2DAnimatedGradientNoiseOffset = Vector2.zero;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(ShowBlendColorAnimatedGradientSettings))]
        [LabelText("Rotation Speed")]
        public float blendColor2DAnimatedGradientRotationSpeed = 0f;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(ShowBlendColorAnimatedGradientSettings))]
        [LabelText("Pulse Amplitude")]
        [Min(0f)]
        public float blendColor2DAnimatedGradientPulseAmplitude = 0f;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(ShowBlendColorAnimatedGradientSettings))]
        [LabelText("Pulse Speed")]
        public float blendColor2DAnimatedGradientPulseSpeed = 1f;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(ShowBlendColorAnimatedGradientSettings))]
        [LabelText("Warp Pattern")]
        [ValueDropdown(nameof(GetAnimatedNoisePatternOptions))]
        [HideInInspector]
        public int blendColor2DAnimatedGradientWarpPatternType = 10;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(ShowBlendColorAnimatedGradientSettings))]
        [LabelText("Warp Scale")]
        [Min(0.0001f)]
        public float blendColor2DAnimatedGradientWarpScale = 2f;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(ShowBlendColorAnimatedGradientSettings))]
        [LabelText("Warp Strength")]
        [Min(0f)]
        public float blendColor2DAnimatedGradientWarpStrength = 0.1f;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(ShowBlendColorAnimatedGradientSettings))]
        [LabelText("Warp Direction")]
        public Vector2 blendColor2DAnimatedGradientWarpDirection = new Vector2(0.71f, 0.43f);

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(ShowBlendColorAnimatedGradientSettings))]
        [LabelText("Warp Speed")]
        public float blendColor2DAnimatedGradientWarpSpeed = 0.35f;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(ShowBlendColorAnimatedGradientSettings))]
        [LabelText("Loop Seconds")]
        [Min(0f)]
        public float blendColor2DAnimatedGradientLoopSeconds = 0f;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(ShowBlendColorAnimatedGradientSettings))]
        [LabelText("Octaves")]
        [Min(1f)]
        public float blendColor2DAnimatedGradientOctaves = 4f;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(ShowBlendColorAnimatedGradientSettings))]
        [LabelText("Lacunarity")]
        [Min(1f)]
        public float blendColor2DAnimatedGradientLacunarity = 2f;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(ShowBlendColorAnimatedGradientSettings))]
        [LabelText("Gain")]
        [Range(0f, 1f)]
        public float blendColor2DAnimatedGradientGain = 0.5f;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(ShowBlendColorAnimatedGradientSettings))]
        [LabelText("Cell Sharpness")]
        [Min(0.01f)]
        public float blendColor2DAnimatedGradientCellSharpness = 1.5f;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(ShowBlendColorAnimatedGradientSettings))]
        [LabelText("Pattern Contrast")]
        [Min(0f)]
        public float blendColor2DAnimatedGradientPatternContrast = 1f;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(ShowBlendColorAnimatedGradientSettings))]
        [LabelText("Hue Amp")]
        [Min(0f)]
        public float blendColor2DAnimatedGradientHueAmplitude = 0.0025f;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(ShowBlendColorAnimatedGradientSettings))]
        [LabelText("Sat Amp")]
        [Min(0f)]
        public float blendColor2DAnimatedGradientSaturationAmplitude = 0.008f;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(ShowBlendColorAnimatedGradientSettings))]
        [LabelText("Light Amp")]
        [Min(0f)]
        public float blendColor2DAnimatedGradientLightnessAmplitude = 0.015f;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(ShowBlendColorAnimatedGradientSettings))]
        [LabelText("Pixel Size")]
        [Min(1f)]
        public float blendColor2DAnimatedGradientPixelSize = 1f;

        // --- AdvancedFade2D ---
        [TitleGroup("AdvancedFade", "繝ｯ繧､繝・/ 蠅・阜繧ｰ繝ｭ繝ｼ / Burn")]
        [ToggleLeft]
        [Tooltip("AdvancedFade2D 縺ｮ繝輔ぉ繝ｼ繝画悽菴薙ｒ譛牙柑縺ｫ縺吶ｋ")]
        public bool advancedFadeEnabled = false;

        [TitleGroup("AdvancedFade")]
        [ShowIf(nameof(advancedFadeEnabled))]
        [LabelText("Fade Direction")]
        [ValueDropdown(nameof(GetAdvancedFadeDirectionOptions))]
        public int advancedFadeFadeDirection = 0;

        [TitleGroup("AdvancedFade")]
        [ShowIf(nameof(advancedFadeEnabled))]
        [Range(0f, 1f)]
        [LabelText("Fade Amount")]
        public float advancedFadeFadeAmount = 0f;

        [TitleGroup("AdvancedFade")]
        [ShowIf(nameof(advancedFadeEnabled))]
        [Range(0f, 1f)]
        [LabelText("Softness")]
        public float advancedFadeSoft = 0.1f;

        [TitleGroup("AdvancedFade")]
        [ShowIf(nameof(advancedFadeEnabled))]
        [Min(0f)]
        [LabelText("Glow Intensity")]
        public float advancedFadeGlowIntensity = 0f;

        [TitleGroup("AdvancedFade")]
        [ShowIf(nameof(advancedFadeEnabled))]
        [Min(0f)]
        [LabelText("Glow Range")]
        public float advancedFadeGlowRange = 0.05f;

        [TitleGroup("AdvancedFade")]
        [ShowIf(nameof(advancedFadeEnabled))]
        [LabelText("Glow Blend")]
        [ValueDropdown(nameof(GetRainbowBlendModeOptions))]
        public int advancedFadeGlowBlendMode = 0;

        [TitleGroup("AdvancedFade")]
        [ShowIf(nameof(advancedFadeEnabled))]
        [LabelText("Wave Params A")]
        public Vector4 advancedFadeWaveParamsA = Vector4.zero;

        [TitleGroup("AdvancedFade")]
        [ShowIf(nameof(advancedFadeEnabled))]
        [LabelText("Wave Params B")]
        public Vector4 advancedFadeWaveParamsB = Vector4.zero;

        [TitleGroup("AdvancedFade")]
        [ShowIf(nameof(ShowAdvancedFadeCircleSettings))]
        [LabelText("Circle Start Angle")]
        public float advancedFadeCircleStartAngleDeg = 90f;

        [TitleGroup("AdvancedFade")]
        [ShowIf(nameof(ShowAdvancedFadeCircleSettings))]
        [LabelText("Circle Clockwise")]
        [ToggleLeft]
        public bool advancedFadeCircleClockwise = true;

        // --- Rainbow2D ---
        [TitleGroup("Rainbow", "陌ｹ濶ｲ貍泌・")]
        [ToggleLeft]
        public bool rainbowEnabled = false;

        [TitleGroup("Rainbow")]
        [ShowIf(nameof(rainbowEnabled))]
        [ValueDropdown(nameof(GetRainbowModeOptions))]
        public int rainbowMode = 0;

        [TitleGroup("Rainbow")]
        [ShowIf(nameof(rainbowEnabled))]
        [ValueDropdown(nameof(GetRainbowPatternOptions))]
        public int rainbowPattern = 0;

        [TitleGroup("Rainbow")]
        [ShowIf(nameof(rainbowEnabled))]
        public Vector2 rainbowDirection = new Vector2(1f, 0f);

        [TitleGroup("Rainbow")]
        [ShowIf(nameof(rainbowEnabled))]
        public float rainbowScale = 1f;

        [TitleGroup("Rainbow")]
        [ShowIf(nameof(rainbowEnabled))]
        public float rainbowOffset = 0f;

        [TitleGroup("Rainbow")]
        [ShowIf(nameof(rainbowEnabled))]
        public float rainbowSpeed = 0.5f;

        [TitleGroup("Rainbow")]
        [ShowIf(nameof(rainbowEnabled))]
        public float rainbowPixelSize = 2f;

        [TitleGroup("Rainbow")]
        [ShowIf(nameof(rainbowEnabled))]
        [Range(0f, 1f)]
        public float rainbowIntensity = 0.5f;

        [TitleGroup("Rainbow")]
        [ShowIf(nameof(rainbowEnabled))]
        [ValueDropdown(nameof(GetRainbowBlendModeOptions))]
        public int rainbowBlendMode = 1;

        // --- AdvancedFade2D Burn ---
        [TitleGroup("AdvancedFade", "辟ｼ縺第ｶ医∴(Noise)")]
        [TitleGroup("AdvancedFade/Burn")]
        [ToggleLeft]
        public bool advancedFadeBurnEnabled = false;

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(advancedFadeBurnEnabled))]
        [Range(0f, 1f)]
        public float advancedFadeBurnProgress = 0f;

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(advancedFadeBurnEnabled))]
        [Range(0f, 0.5f)]
        public float advancedFadeBurnEdgeWidth = 0.1f;

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(advancedFadeBurnEnabled))]
        public float advancedFadeBurnNoiseScale = 4f;

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(advancedFadeBurnEnabled))]
        [Range(0f, 1f)]
        public float advancedFadeBurnNoiseStrength = 0.5f;

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(advancedFadeBurnEnabled))]
        [ValueDropdown(nameof(GetBurnNoiseTypeOptions))]
        public int advancedFadeBurnNoiseType = 10;

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(advancedFadeBurnEnabled))]
        public Vector2 advancedFadeBurnDirection = new Vector2(0f, 1f);

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(advancedFadeBurnEnabled))]
        public Color advancedFadeBurnEdgeColor = new Color(1f, 0.5f, 0.1f, 1f);

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(advancedFadeBurnEnabled))]
        [ValueDropdown(nameof(GetRainbowBlendModeOptions))]
        public int advancedFadeBurnBlendMode = 0;

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(advancedFadeBurnEnabled))]
        public bool advancedFadeBurnInvert = false;

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(advancedFadeBurnEnabled))]
        [LabelText("Animated Noise")]
        [ToggleLeft]
        public bool advancedFadeBurnAnimatedNoiseEnabled = false;

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(ShowAdvancedFadeBurnAnimatedNoiseSettings))]
        [LabelText("Pattern Type")]
        [ValueDropdown(nameof(GetAnimatedNoisePatternOptions))]
        public int advancedFadeBurnAnimatedNoisePatternType = 10;

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(ShowAdvancedFadeBurnAnimatedNoiseSettings))]
        [LabelText("Direction")]
        public Vector2 advancedFadeBurnAnimatedNoiseDirection = new Vector2(1f, 0f);

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(ShowAdvancedFadeBurnAnimatedNoiseSettings))]
        [LabelText("Speed")]
        public float advancedFadeBurnAnimatedNoiseSpeed = 0.2f;

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(ShowAdvancedFadeBurnAnimatedNoiseSettings))]
        [LabelText("Offset")]
        public Vector2 advancedFadeBurnAnimatedNoiseOffset = Vector2.zero;

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(ShowAdvancedFadeBurnAnimatedNoiseSettings))]
        [LabelText("Rotation Speed")]
        public float advancedFadeBurnAnimatedNoiseRotationSpeed = 0f;

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(ShowAdvancedFadeBurnAnimatedNoiseSettings))]
        [LabelText("Pulse Amplitude")]
        [Min(0f)]
        public float advancedFadeBurnAnimatedNoisePulseAmplitude = 0f;

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(ShowAdvancedFadeBurnAnimatedNoiseSettings))]
        [LabelText("Pulse Speed")]
        public float advancedFadeBurnAnimatedNoisePulseSpeed = 1f;

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(ShowAdvancedFadeBurnAnimatedNoiseSettings))]
        [LabelText("Warp Pattern")]
        [ValueDropdown(nameof(GetAnimatedNoisePatternOptions))]
        [HideInInspector]
        public int advancedFadeBurnAnimatedNoiseWarpPatternType = 10;

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(ShowAdvancedFadeBurnAnimatedNoiseSettings))]
        [LabelText("Warp Scale")]
        [Min(0.0001f)]
        public float advancedFadeBurnAnimatedNoiseWarpScale = 2f;

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(ShowAdvancedFadeBurnAnimatedNoiseSettings))]
        [LabelText("Warp Strength")]
        [Min(0f)]
        public float advancedFadeBurnAnimatedNoiseWarpStrength = 0.2f;

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(ShowAdvancedFadeBurnAnimatedNoiseSettings))]
        [LabelText("Warp Direction")]
        public Vector2 advancedFadeBurnAnimatedNoiseWarpDirection = new Vector2(0.71f, 0.43f);

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(ShowAdvancedFadeBurnAnimatedNoiseSettings))]
        [LabelText("Warp Speed")]
        public float advancedFadeBurnAnimatedNoiseWarpSpeed = 0.35f;

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(ShowAdvancedFadeBurnAnimatedNoiseSettings))]
        [LabelText("Loop Seconds")]
        [Min(0f)]
        public float advancedFadeBurnAnimatedNoiseLoopSeconds = 0f;

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(ShowAdvancedFadeBurnAnimatedNoiseSettings))]
        [LabelText("Octaves")]
        [Min(1f)]
        public float advancedFadeBurnAnimatedNoiseOctaves = 4f;

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(ShowAdvancedFadeBurnAnimatedNoiseSettings))]
        [LabelText("Lacunarity")]
        [Min(1f)]
        public float advancedFadeBurnAnimatedNoiseLacunarity = 2f;

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(ShowAdvancedFadeBurnAnimatedNoiseSettings))]
        [LabelText("Gain")]
        [Range(0f, 1f)]
        public float advancedFadeBurnAnimatedNoiseGain = 0.5f;

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(ShowAdvancedFadeBurnAnimatedNoiseSettings))]
        [LabelText("Cell Sharpness")]
        [Min(0.01f)]
        public float advancedFadeBurnAnimatedNoiseCellSharpness = 1.5f;

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(ShowAdvancedFadeBurnAnimatedNoiseSettings))]
        [LabelText("Pattern Contrast")]
        [Min(0f)]
        public float advancedFadeBurnAnimatedNoisePatternContrast = 1f;

        // 笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊・
        // Render State (MeshRenderer/SpriteRenderer 蜈ｱ騾・
        // 笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊・

        [TitleGroup("Render State", "謠冗判繝悶Ξ繝ｳ繝・繧ｫ繝ｪ繝ｳ繧ｰ/ZWrite/Queue繧貞宛蠕｡縲・eshRenderer縺ｮ蜉邂玲ｼ泌・縺ｫ蛻ｩ逕ｨ")]
        [Tooltip("Inspector setting.")]
        [ValueDropdown(nameof(GetRenderStateBlendPresetOptions))]
        public BaseShaderRenderBlendPreset renderStateBlendPreset = BaseShaderRenderBlendPreset.Alpha;

        [TitleGroup("Render State")]
        [ToggleLeft]
        [Tooltip("Inspector setting.")]
        public bool renderStateUseCustomBlendFactors = false;

        [TitleGroup("Render State")]
        [ShowIf(nameof(renderStateUseCustomBlendFactors))]
        [LabelText("Src Blend")]
        [Tooltip("Inspector setting.")]
        [ValueDropdown(nameof(GetRenderStateBlendFactorOptions))]
        public int renderStateSrcBlend = 5; // SrcAlpha

        [TitleGroup("Render State")]
        [ShowIf(nameof(renderStateUseCustomBlendFactors))]
        [LabelText("Dst Blend")]
        [Tooltip("Inspector setting.")]
        [ValueDropdown(nameof(GetRenderStateBlendFactorOptions))]
        public int renderStateDstBlend = 10; // OneMinusSrcAlpha

        [TitleGroup("Render State")]
        [ToggleLeft]
        [Tooltip("Inspector setting.")]
        public bool renderStateZWrite = false;

        [TitleGroup("Render State")]
        [ValueDropdown(nameof(GetRenderStateCullOptions))]
        [Tooltip("Inspector setting.")]
        public int renderStateCull = 0; // Off

        [TitleGroup("Render State")]
        [MinValue(-200)]
        [MaxValue(200)]
        [Tooltip("Inspector setting.")]
        public int renderStateQueueOffset = 0;

        [TitleGroup("Render State")]
        [Range(0f, 1f)]
        [Tooltip("Inspector setting.")]
        public float renderStateBlendIntensity = 1f;

        // 笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊・
        // Auto Entries Generation
        // 笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊・

        protected override void OnRefreshAutoEntries()
        {
            ClearAutoEntries();

            var (presetSrcBlend, presetDstBlend) = ResolveRenderStateBlendFactors(renderStateBlendPreset);
            var srcBlend = renderStateUseCustomBlendFactors ? renderStateSrcBlend : presetSrcBlend;
            var dstBlend = renderStateUseCustomBlendFactors ? renderStateDstBlend : presetDstBlend;

            SetAutoEntry(MaterialFxKeys.BaseShader.RenderState.BlendPreset, MakeInt((int)renderStateBlendPreset));
            SetAutoEntry(MaterialFxKeys.BaseShader.RenderState.SrcBlend, MakeInt(srcBlend));
            SetAutoEntry(MaterialFxKeys.BaseShader.RenderState.DstBlend, MakeInt(dstBlend));
            SetAutoEntry(MaterialFxKeys.BaseShader.RenderState.ZWrite, MakeBool(renderStateZWrite));
            SetAutoEntry(MaterialFxKeys.BaseShader.RenderState.Cull, MakeInt(renderStateCull));
            SetAutoEntry(MaterialFxKeys.BaseShader.RenderState.QueueOffset, MakeInt(renderStateQueueOffset));
            SetAutoEntry(MaterialFxKeys.BaseShader.RenderState.BlendIntensity, MakeFloat(renderStateBlendIntensity));

            // --- Dissolve ---
            SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Dissolve.Enabled, MakeBool(dissolveEnabled));
            if (dissolveEnabled)
            {
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Dissolve.Threshold, MakeFloat(dissolveThreshold));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Dissolve.EdgeWidth, MakeFloat(dissolveEdgeWidth));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Dissolve.EdgeColor, MakeColor(dissolveEdgeColor));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Dissolve.Source.SlotType, MakeInt(dissolveSourceSlotType));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Dissolve.Source.Channel, MakeInt(dissolveSourceChannel));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Dissolve.Source.UVSpace, MakeInt(dissolveSourceUVSpace));
            }

            // --- Flow Warp ---
            SetAutoEntry(MaterialFxKeys.BaseShader.FlowWarp.Enabled, MakeBool(flowWarpEnabled));
            if (flowWarpEnabled)
            {
                SetAutoEntry(MaterialFxKeys.BaseShader.FlowWarp.Strength, MakeFloat2(flowWarpStrength));
                SetAutoEntry(MaterialFxKeys.BaseShader.FlowWarp.Speed, MakeFloat(flowWarpSpeed));
                SetAutoEntry(MaterialFxKeys.BaseShader.FlowWarp.Source.SlotType, MakeInt(flowWarpSourceSlotType));
                SetAutoEntry(MaterialFxKeys.BaseShader.FlowWarp.Source.Channel, MakeInt(flowWarpSourceChannel));
                SetAutoEntry(MaterialFxKeys.BaseShader.FlowWarp.Source.UVSpace, MakeInt(flowWarpSourceUVSpace));
            }

            // --- Color Overlay ---
            SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.ColorOverlay.Enabled, MakeBool(colorOverlayEnabled));
            if (colorOverlayEnabled)
            {
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.ColorOverlay.Color, MakeColor(colorOverlayColor));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.ColorOverlay.BlendMode, MakeInt(colorOverlayBlendMode));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.ColorOverlay.Intensity, MakeFloat(colorOverlayIntensity));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.ColorOverlay.Source.SlotType, MakeInt(colorOverlaySourceSlotType));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.ColorOverlay.Source.Channel, MakeInt(colorOverlaySourceChannel));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.ColorOverlay.Source.UVSpace, MakeInt(colorOverlaySourceUVSpace));
            }

            // --- Color Ramp ---
            SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.ColorRamp.Enabled, MakeBool(colorRampEnabled));
            if (colorRampEnabled)
            {
                // Guard against unassigned texture 窶・avoid sending null textures that may be invalid for this feature.
                if (colorRampTexture == null)
                {
                }
                else
                {
                    SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.ColorRamp.Texture, MakeTexture(colorRampTexture));
                }

                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.ColorRamp.Intensity, MakeFloat(colorRampIntensity));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.ColorRamp.PreserveAlpha, MakeBool(colorRampPreserveAlpha));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.ColorRamp.Source.SlotType, MakeInt(colorRampSourceSlotType));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.ColorRamp.Source.Channel, MakeInt(colorRampSourceChannel));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.ColorRamp.Source.UVSpace, MakeInt(colorRampSourceUVSpace));
            }

            // --- Refraction ---
            SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Refraction.Enabled, MakeBool(refractionEnabled));
            if (refractionEnabled)
            {
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Refraction.Strength, MakeFloat2(refractionStrength));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Refraction.ChromaticAberration, MakeFloat(refractionChromaticAberration));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Refraction.Source.SlotType, MakeInt(refractionSourceSlotType));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Refraction.Source.Channel, MakeInt(refractionSourceChannel));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Refraction.Source.UVSpace, MakeInt(refractionSourceUVSpace));
            }

            // --- Caustics ---
            SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Caustics.Enabled, MakeBool(causticsEnabled));
            if (causticsEnabled)
            {
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Caustics.Color, MakeColor(causticsColor));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Caustics.Intensity, MakeFloat(causticsIntensity));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Caustics.Threshold, MakeFloat(causticsThreshold));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Caustics.Softness, MakeFloat(causticsSoftness));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Caustics.ScrollA, MakeFloat2(causticsScrollA));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Caustics.ScrollB, MakeFloat2(causticsScrollB));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Caustics.Source.A.SlotType, MakeInt(causticsSourceASlotType));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Caustics.Source.A.Channel, MakeInt(causticsSourceAChannel));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Caustics.Source.A.UVSpace, MakeInt(causticsSourceAUVSpace));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Caustics.Source.B.SlotType, MakeInt(causticsSourceBSlotType));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Caustics.Source.B.Channel, MakeInt(causticsSourceBChannel));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Caustics.Source.B.UVSpace, MakeInt(causticsSourceBUVSpace));
            }

            // --- Ripple ---
            SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Ripple.Enabled, MakeBool(rippleEnabled));
            if (rippleEnabled)
            {
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Ripple.Center, MakeFloat2(rippleCenter));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Ripple.WaveParams, MakeFloat4(rippleWaveParams));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Ripple.Amplitude, MakeFloat(rippleAmplitude));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Ripple.Phase, MakeFloat(ripplePhase));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Ripple.DistortUV, MakeBool(rippleDistortUV));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Ripple.ColorBlend, MakeFloat(rippleColorBlend));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Ripple.Color, MakeColor(rippleColor));
            }

            // --- Hue Shift ---
            SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.HueShift.Enabled, MakeBool(hueShiftEnabled));
            if (hueShiftEnabled)
            {
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.HueShift.Amount, MakeFloat(hueShiftAmount));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.HueShift.SaturationMod, MakeFloat(hueSaturationMod));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.HueShift.ValueMod, MakeFloat(hueValueMod));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.HueShift.Mask.Source.SlotType, MakeInt(hueShiftMaskSlotType));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.HueShift.Mask.Source.Channel, MakeInt(hueShiftMaskChannel));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.HueShift.Mask.Source.UVSpace, MakeInt(hueShiftMaskUVSpace));
            }

            // --- Normal Map ---
            SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.NormalMap.Enabled, MakeBool(normalMapEnabled));
            if (normalMapEnabled)
            {
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.NormalMap.Strength, MakeFloat(normalMapStrength));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.NormalMap.LightDir, MakeFloat3(normalMapLightDir));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.NormalMap.Source.SlotType, MakeInt(normalMapSourceSlotType));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.NormalMap.Source.Channel, MakeInt(normalMapSourceChannel));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.NormalMap.Source.UVSpace, MakeInt(normalMapSourceUVSpace));
            }

            // --- Emission ---
            SetAutoEntry(MaterialFxKeys.BaseShader.Emission.Enabled, MakeBool(emissionEnabled));
            if (emissionEnabled)
            {
                // 笘・ｿｮ豁｣: emissionColor縺ｮRGB縺ｨemissionIntensity繧堤ｵ仙粋縺励※Color(r,g,b,intensity)縺ｨ縺励※險ｭ螳・
                // 繧ｷ繧ｧ繝ｼ繝繝ｼ蛛ｴ縺ｮ_EmissionColor縺ｯ rgb=color, a=intensity 縺ｨ縺励※謇ｱ繧上ｌ繧・
                var combinedEmissionColor = new Color(emissionColor.r, emissionColor.g, emissionColor.b, emissionIntensity);
                SetAutoEntry(MaterialFxKeys.BaseShader.Emission.Color, MakeColor(combinedEmissionColor));
            }

            // --- Mask ---
            SetAutoEntry(MaterialFxKeys.BaseShader.Mask.Enabled, MakeBool(maskEnabled));
            if (maskEnabled)
            {
                SetAutoEntry(MaterialFxKeys.BaseShader.Mask.Threshold, MakeFloat(maskThreshold));
                SetAutoEntry(MaterialFxKeys.BaseShader.Mask.Softness, MakeFloat(maskSoftness));
                SetAutoEntry(MaterialFxKeys.BaseShader.Mask.Source.SlotType, MakeInt(maskSourceSlotType));
                SetAutoEntry(MaterialFxKeys.BaseShader.Mask.Source.Channel, MakeInt(maskSourceChannel));
                SetAutoEntry(MaterialFxKeys.BaseShader.Mask.Source.UVSpace, MakeInt(maskSourceUVSpace));
            }

            // --- BlendColor2D ---
            SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.Enabled, MakeBool(blendColor2DEnabled));
            if (blendColor2DEnabled)
            {
                SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.Color, MakeColor(blendColor2DColor));
                SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.BlendIntensity, MakeFloat(blendColor2DBlendIntensity));
                SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.BlendGradDirection, MakeInt(blendColor2DBlendGradDirection));
                SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.BlendGradationAmount, MakeFloat(blendColor2DBlendGradationAmount));
                SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.BlendSoftness, MakeFloat(blendColor2DBlendSoftness));
                SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.BlendMode, MakeInt(blendColor2DBlendMode));
                SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.AnimatedGradient.Enabled, MakeBool(blendColor2DAnimatedGradientEnabled));
                if (blendColor2DAnimatedGradientEnabled)
                {
                    SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.AnimatedGradient.PatternType, MakeInt(blendColor2DAnimatedGradientPatternType));
                    SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.AnimatedGradient.MasterStrength, MakeFloat(blendColor2DAnimatedGradientMasterStrength));
                    SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.AnimatedGradient.NoiseScale, MakeFloat(blendColor2DAnimatedGradientNoiseScale));
                    SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.AnimatedGradient.NoiseDirection, MakeFloat2(blendColor2DAnimatedGradientNoiseDirection));
                    SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.AnimatedGradient.NoiseSpeed, MakeFloat(blendColor2DAnimatedGradientNoiseSpeed));
                    SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.AnimatedGradient.NoiseOffset, MakeFloat2(blendColor2DAnimatedGradientNoiseOffset));
                    SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.AnimatedGradient.RotationSpeed, MakeFloat(blendColor2DAnimatedGradientRotationSpeed));
                    SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.AnimatedGradient.PulseAmplitude, MakeFloat(blendColor2DAnimatedGradientPulseAmplitude));
                    SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.AnimatedGradient.PulseSpeed, MakeFloat(blendColor2DAnimatedGradientPulseSpeed));
                    SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.AnimatedGradient.WarpPatternType, MakeInt(0));
                    SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.AnimatedGradient.WarpScale, MakeFloat(blendColor2DAnimatedGradientWarpScale));
                    SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.AnimatedGradient.WarpStrength, MakeFloat(blendColor2DAnimatedGradientWarpStrength));
                    SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.AnimatedGradient.WarpDirection, MakeFloat2(blendColor2DAnimatedGradientWarpDirection));
                    SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.AnimatedGradient.WarpSpeed, MakeFloat(blendColor2DAnimatedGradientWarpSpeed));
                    SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.AnimatedGradient.LoopSeconds, MakeFloat(blendColor2DAnimatedGradientLoopSeconds));
                    SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.AnimatedGradient.Octaves, MakeFloat(blendColor2DAnimatedGradientOctaves));
                    SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.AnimatedGradient.Lacunarity, MakeFloat(blendColor2DAnimatedGradientLacunarity));
                    SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.AnimatedGradient.Gain, MakeFloat(blendColor2DAnimatedGradientGain));
                    SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.AnimatedGradient.CellSharpness, MakeFloat(blendColor2DAnimatedGradientCellSharpness));
                    SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.AnimatedGradient.PatternContrast, MakeFloat(blendColor2DAnimatedGradientPatternContrast));
                    SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.AnimatedGradient.HueAmplitude, MakeFloat(blendColor2DAnimatedGradientHueAmplitude));
                    SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.AnimatedGradient.SaturationAmplitude, MakeFloat(blendColor2DAnimatedGradientSaturationAmplitude));
                    SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.AnimatedGradient.LightnessAmplitude, MakeFloat(blendColor2DAnimatedGradientLightnessAmplitude));
                    SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.AnimatedGradient.PixelSize, MakeFloat(blendColor2DAnimatedGradientPixelSize));
                }
            }

            // --- Outline ---
            SetAutoEntry(MaterialFxKeys.BaseShader.Outline.Enabled, MakeBool(outlineEnabled));
            if (outlineEnabled)
            {
                SetAutoEntry(MaterialFxKeys.BaseShader.Outline.Mode, MakeInt(outlineMode));
                SetAutoEntry(MaterialFxKeys.BaseShader.Outline.Color, MakeColor(outlineColor));
                SetAutoEntry(MaterialFxKeys.BaseShader.Outline.DirectionMask, MakeInt((int)outlineDirectionMask));
                SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AutoColorEnabled, MakeBool(outlineAutoColorEnabled));
                SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AutoColorMode, MakeInt((int)outlineAutoColorMode));
                SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AutoHue, MakeFloat(outlineAutoHue));
                SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AutoSaturation, MakeFloat(outlineAutoSaturation));
                SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AutoLightness, MakeFloat(outlineAutoLightness));
                SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AnimatedGradient.Enabled, MakeBool(outlineAnimatedGradientEnabled));
                if (outlineAnimatedGradientEnabled)
                {
                    SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AnimatedGradient.PatternType, MakeInt(outlineAnimatedGradientPatternType));
                    SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AnimatedGradient.MasterStrength, MakeFloat(outlineAnimatedGradientMasterStrength));
                    SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AnimatedGradient.NoiseScale, MakeFloat(outlineAnimatedGradientNoiseScale));
                    SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AnimatedGradient.NoiseDirection, MakeFloat2(outlineAnimatedGradientNoiseDirection));
                    SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AnimatedGradient.NoiseSpeed, MakeFloat(outlineAnimatedGradientNoiseSpeed));
                    SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AnimatedGradient.NoiseOffset, MakeFloat2(outlineAnimatedGradientNoiseOffset));
                    SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AnimatedGradient.RotationSpeed, MakeFloat(outlineAnimatedGradientRotationSpeed));
                    SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AnimatedGradient.PulseAmplitude, MakeFloat(outlineAnimatedGradientPulseAmplitude));
                    SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AnimatedGradient.PulseSpeed, MakeFloat(outlineAnimatedGradientPulseSpeed));
                    SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AnimatedGradient.WarpPatternType, MakeInt(0));
                    SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AnimatedGradient.WarpScale, MakeFloat(outlineAnimatedGradientWarpScale));
                    SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AnimatedGradient.WarpStrength, MakeFloat(outlineAnimatedGradientWarpStrength));
                    SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AnimatedGradient.WarpDirection, MakeFloat2(outlineAnimatedGradientWarpDirection));
                    SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AnimatedGradient.WarpSpeed, MakeFloat(outlineAnimatedGradientWarpSpeed));
                    SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AnimatedGradient.LoopSeconds, MakeFloat(outlineAnimatedGradientLoopSeconds));
                    SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AnimatedGradient.Octaves, MakeFloat(outlineAnimatedGradientOctaves));
                    SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AnimatedGradient.Lacunarity, MakeFloat(outlineAnimatedGradientLacunarity));
                    SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AnimatedGradient.Gain, MakeFloat(outlineAnimatedGradientGain));
                    SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AnimatedGradient.CellSharpness, MakeFloat(outlineAnimatedGradientCellSharpness));
                    SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AnimatedGradient.PatternContrast, MakeFloat(outlineAnimatedGradientPatternContrast));
                    SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AnimatedGradient.HueAmplitude, MakeFloat(outlineAnimatedGradientHueAmplitude));
                    SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AnimatedGradient.SaturationAmplitude, MakeFloat(outlineAnimatedGradientSaturationAmplitude));
                    SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AnimatedGradient.LightnessAmplitude, MakeFloat(outlineAnimatedGradientLightnessAmplitude));
                }
                SetAutoEntry(MaterialFxKeys.BaseShader.Outline.Width, MakeFloat(outlineWidth));
                SetAutoEntry(MaterialFxKeys.BaseShader.Outline.Opacity, MakeFloat(outlineOpacity));
                SetAutoEntry(MaterialFxKeys.BaseShader.Outline.Softness, MakeFloat(outlineSoftness));
                SetAutoEntry(MaterialFxKeys.BaseShader.Outline.BlendMode, MakeInt(outlineBlendMode));
                SetAutoEntry(MaterialFxKeys.BaseShader.Outline.PixelPerfect, MakeBool(outlinePixelPerfect));
                SetAutoEntry(MaterialFxKeys.BaseShader.Outline.WidthUnit, MakeInt(outlineWidthUnit));
                SetAutoEntry(MaterialFxKeys.BaseShader.Outline.PixelStep, MakeFloat(outlinePixelStep));
                SetAutoEntry(MaterialFxKeys.BaseShader.Outline.SamplePattern, MakeInt(outlineSamplePattern));
                SetAutoEntry(MaterialFxKeys.BaseShader.Outline.MaskRespect, MakeBool(outlineMaskRespect));
                SetAutoEntry(MaterialFxKeys.BaseShader.Outline.UseVertexColor, MakeBool(outlineUseVertexColor));
                SetAutoEntry(MaterialFxKeys.BaseShader.Outline.UVClampEnabled, MakeBool(outlineUvClampEnabled));
                SetAutoEntry(MaterialFxKeys.BaseShader.Outline.ZTestMode, MakeInt(outlineZTestMode));
            }

            // --- Text Fx ---
            SetAutoEntry(MaterialFxKeys.BaseShader.TextFx.Outline.Enabled, MakeBool(textOutlineEnabled));
            if (textOutlineEnabled)
            {
                SetAutoEntry(MaterialFxKeys.BaseShader.TextFx.Outline.DirectionMask, MakeInt((int)textOutlineDirectionMask));
                SetAutoEntry(MaterialFxKeys.BaseShader.TextFx.Outline.AutoColorEnabled, MakeBool(textOutlineAutoColorEnabled));
                SetAutoEntry(MaterialFxKeys.BaseShader.TextFx.Outline.AutoColorMode, MakeInt((int)textOutlineAutoColorMode));
                SetAutoEntry(MaterialFxKeys.BaseShader.TextFx.Outline.AutoHue, MakeFloat(textOutlineAutoHue));
                SetAutoEntry(MaterialFxKeys.BaseShader.TextFx.Outline.AutoSaturation, MakeFloat(textOutlineAutoSaturation));
                SetAutoEntry(MaterialFxKeys.BaseShader.TextFx.Outline.AutoLightness, MakeFloat(textOutlineAutoLightness));
                SetAutoEntry(MaterialFxKeys.BaseShader.TextFx.Outline.Color, MakeColor(textOutlineColor));
                SetAutoEntry(MaterialFxKeys.BaseShader.TextFx.Outline.Thickness, MakeFloat(textOutlineThickness));
                SetAutoEntry(MaterialFxKeys.BaseShader.TextFx.Outline.Softness, MakeFloat(textOutlineSoftness));
            }

            SetAutoEntry(MaterialFxKeys.BaseShader.TextFx.Shadow.Enabled, MakeBool(textShadowEnabled));
            if (textShadowEnabled)
            {
                SetAutoEntry(MaterialFxKeys.BaseShader.TextFx.Shadow.Color, MakeColor(textShadowColor));
                SetAutoEntry(MaterialFxKeys.BaseShader.TextFx.Shadow.Offset, MakeFloat2(textShadowOffset));
                SetAutoEntry(MaterialFxKeys.BaseShader.TextFx.Shadow.Softness, MakeFloat(textShadowSoftness));
            }

            SetAutoEntry(MaterialFxKeys.BaseShader.TextFx.Glow.Enabled, MakeBool(textGlowEnabled));
            if (textGlowEnabled)
            {
                SetAutoEntry(MaterialFxKeys.BaseShader.TextFx.Glow.Color, MakeColor(textGlowColor));
                SetAutoEntry(MaterialFxKeys.BaseShader.TextFx.Glow.Thickness, MakeFloat(textGlowThickness));
                SetAutoEntry(MaterialFxKeys.BaseShader.TextFx.Glow.Softness, MakeFloat(textGlowSoftness));
            }

            // --- Rainbow2D ---
            SetAutoEntry(MaterialFxKeys.BaseShader.Rainbow2D.Enabled, MakeBool(rainbowEnabled));
            if (rainbowEnabled)
            {
                SetAutoEntry(MaterialFxKeys.BaseShader.Rainbow2D.Mode, MakeInt(rainbowMode));
                SetAutoEntry(MaterialFxKeys.BaseShader.Rainbow2D.Pattern, MakeInt(rainbowPattern));
                SetAutoEntry(MaterialFxKeys.BaseShader.Rainbow2D.Direction, MakeFloat2(rainbowDirection));
                SetAutoEntry(MaterialFxKeys.BaseShader.Rainbow2D.Scale, MakeFloat(rainbowScale));
                SetAutoEntry(MaterialFxKeys.BaseShader.Rainbow2D.Offset, MakeFloat(rainbowOffset));
                SetAutoEntry(MaterialFxKeys.BaseShader.Rainbow2D.Speed, MakeFloat(rainbowSpeed));
                SetAutoEntry(MaterialFxKeys.BaseShader.Rainbow2D.PixelSize, MakeFloat(rainbowPixelSize));
                SetAutoEntry(MaterialFxKeys.BaseShader.Rainbow2D.Intensity, MakeFloat(rainbowIntensity));
                SetAutoEntry(MaterialFxKeys.BaseShader.Rainbow2D.BlendMode, MakeInt(rainbowBlendMode));
            }

            // --- AdvancedFade2D ---
            SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Enabled, MakeBool(advancedFadeEnabled));
            if (advancedFadeEnabled)
            {
                SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.FadeDirection, MakeInt(advancedFadeFadeDirection));
                SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.FadeAmount, MakeFloat(advancedFadeFadeAmount));
                SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Soft, MakeFloat(advancedFadeSoft));
                SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.GlowIntensity, MakeFloat(advancedFadeGlowIntensity));
                SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.GlowRange, MakeFloat(advancedFadeGlowRange));
                SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.GlowBlendMode, MakeInt(advancedFadeGlowBlendMode));
                SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.WaveParamsA, MakeFloat4(advancedFadeWaveParamsA));
                SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.WaveParamsB, MakeFloat4(advancedFadeWaveParamsB));
                SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Circle.StartAngleDeg, MakeFloat(advancedFadeCircleStartAngleDeg));
                SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Circle.Clockwise, MakeBool(advancedFadeCircleClockwise));
            }

            // --- AdvancedFade2D Burn ---
            SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.Enabled, MakeBool(advancedFadeBurnEnabled));
            if (advancedFadeBurnEnabled)
            {
                SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.Progress, MakeFloat(advancedFadeBurnProgress));
                SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.EdgeWidth, MakeFloat(advancedFadeBurnEdgeWidth));
                SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.NoiseScale, MakeFloat(advancedFadeBurnNoiseScale));
                SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.NoiseStrength, MakeFloat(advancedFadeBurnNoiseStrength));
                SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.NoiseType, MakeInt(advancedFadeBurnNoiseType));
                SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.Direction, MakeFloat2(advancedFadeBurnDirection));
                SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.EdgeColor, MakeColor(advancedFadeBurnEdgeColor));
                SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.BlendMode, MakeInt(advancedFadeBurnBlendMode));
                SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.Invert, MakeBool(advancedFadeBurnInvert));
                SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.AnimatedNoise.Enabled, MakeBool(advancedFadeBurnAnimatedNoiseEnabled));
                if (advancedFadeBurnAnimatedNoiseEnabled)
                {
                    SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.AnimatedNoise.PatternType, MakeInt(advancedFadeBurnAnimatedNoisePatternType));
                    SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.AnimatedNoise.Direction, MakeFloat2(advancedFadeBurnAnimatedNoiseDirection));
                    SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.AnimatedNoise.Speed, MakeFloat(advancedFadeBurnAnimatedNoiseSpeed));
                    SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.AnimatedNoise.Offset, MakeFloat2(advancedFadeBurnAnimatedNoiseOffset));
                    SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.AnimatedNoise.RotationSpeed, MakeFloat(advancedFadeBurnAnimatedNoiseRotationSpeed));
                    SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.AnimatedNoise.PulseAmplitude, MakeFloat(advancedFadeBurnAnimatedNoisePulseAmplitude));
                    SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.AnimatedNoise.PulseSpeed, MakeFloat(advancedFadeBurnAnimatedNoisePulseSpeed));
                    SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.AnimatedNoise.WarpPatternType, MakeInt(0));
                    SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.AnimatedNoise.WarpScale, MakeFloat(advancedFadeBurnAnimatedNoiseWarpScale));
                    SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.AnimatedNoise.WarpStrength, MakeFloat(advancedFadeBurnAnimatedNoiseWarpStrength));
                    SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.AnimatedNoise.WarpDirection, MakeFloat2(advancedFadeBurnAnimatedNoiseWarpDirection));
                    SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.AnimatedNoise.WarpSpeed, MakeFloat(advancedFadeBurnAnimatedNoiseWarpSpeed));
                    SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.AnimatedNoise.LoopSeconds, MakeFloat(advancedFadeBurnAnimatedNoiseLoopSeconds));
                    SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.AnimatedNoise.Octaves, MakeFloat(advancedFadeBurnAnimatedNoiseOctaves));
                    SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.AnimatedNoise.Lacunarity, MakeFloat(advancedFadeBurnAnimatedNoiseLacunarity));
                    SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.AnimatedNoise.Gain, MakeFloat(advancedFadeBurnAnimatedNoiseGain));
                    SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.AnimatedNoise.CellSharpness, MakeFloat(advancedFadeBurnAnimatedNoiseCellSharpness));
                    SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.AnimatedNoise.PatternContrast, MakeFloat(advancedFadeBurnAnimatedNoisePatternContrast));
                }
            }
        }

        // 笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊・
        // ValueDropdown Options (Odin Inspector逕ｨ)
        // 笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊・

        /// <summary>
        /// 繝・け繧ｹ繝√Ε繧ｹ繝ｭ繝・ヨ繧ｿ繧､繝励・繝峨Ο繝・・繝繧ｦ繝ｳ繧ｪ繝励す繝ｧ繝ｳ
        /// </summary>
        static ValueDropdownList<int> GetSlotTypeOptions()
        {
            return new ValueDropdownList<int>
            {
                { "External A", 5 },
                { "External B", 6 },
                { "Custom RT", 7 },
            };
        }

        /// <summary>
        /// 繝√Ε繝ｳ繝阪Ν驕ｸ謚槭・繝峨Ο繝・・繝繧ｦ繝ｳ繧ｪ繝励す繝ｧ繝ｳ
        /// 笘・ｿｮ豁｣: 繧ｷ繧ｧ繝ｼ繝繝ｼ蛛ｴ縺ｮ CHANNEL_* 螳夂ｾｩ縺ｯ繝薙ャ繝医・繧ｹ繧ｯ (R=1, G=2, B=4, A=8)
        /// </summary>
        static ValueDropdownList<int> GetChannelOptions()
        {
            return new ValueDropdownList<int>
            {
                { "R (Red)", 1 },
                { "G (Green)", 2 },
                { "B (Blue)", 4 },
                { "A (Alpha)", 8 },
            };
        }

        static ValueDropdownList<int> GetRainbowModeOptions()
        {
            return new ValueDropdownList<int>
            {
                { "Gradient", 0 },
                { "Pixel", 1 },
            };
        }

        static ValueDropdownList<int> GetRainbowPatternOptions()
        {
            return new ValueDropdownList<int>
            {
                { "Horizontal", 0 },
                { "Vertical", 1 },
                { "Checker", 2 },
            };
        }

        static ValueDropdownList<int> GetRainbowBlendModeOptions()
        {
            return new ValueDropdownList<int>
            {
                { "Add", 0 },
                { "Screen", 1 },
                { "Overlay", 2 },
                { "Lerp", 3 },
            };
        }

        static ValueDropdownList<int> GetAnimatedNoisePatternOptions()
        {
            return new ValueDropdownList<int>
            {
                { "Smooth Value", 10 },
                { "Perlin", 20 },
                { "FBM", 30 },
                { "Ridged FBM", 40 },
                { "Cellular", 50 },
                { "Hex Cell", 60 },
                { "Turtle Shell", 70 },
                { "Checker", 80 },
                { "Stripes", 90 },
                { "Diamond", 100 },
                { "Truchet", 110 },
                { "Interference", 120 },
                { "Swirl", 130 },
            };
        }

        static ValueDropdownList<int> GetBurnNoiseTypeOptions() => GetAnimatedNoisePatternOptions();

        static ValueDropdownList<int> GetBlendColorGradientDirectionOptions()
        {
            return new ValueDropdownList<int>
            {
                { "None", 0 },
                { "Horizontal", 1 },
                { "Vertical", 2 },
                { "Radial", 3 },
            };
        }

        static ValueDropdownList<int> GetAdvancedFadeDirectionOptions()
        {
            return new ValueDropdownList<int>
            {
                { "Left To Right", 0 },
                { "Right To Left", 1 },
                { "Bottom To Top", 2 },
                { "Top To Bottom", 3 },
                { "Radial In", 4 },
                { "Radial Out", 5 },
                { "Circle", 6 },
            };
        }

        static (int src, int dst) ResolveRenderStateBlendFactors(BaseShaderRenderBlendPreset preset)
        {
            return preset switch
            {
                BaseShaderRenderBlendPreset.Additive => (1, 1),         // One, One
                BaseShaderRenderBlendPreset.AdditiveAlpha => (5, 1),    // SrcAlpha, One
                BaseShaderRenderBlendPreset.Premultiply => (1, 10),     // One, OneMinusSrcAlpha
                _ => (5, 10),                                           // SrcAlpha, OneMinusSrcAlpha
            };
        }

        static ValueDropdownList<BaseShaderRenderBlendPreset> GetRenderStateBlendPresetOptions()
        {
            return new ValueDropdownList<BaseShaderRenderBlendPreset>
            {
                { "Alpha", BaseShaderRenderBlendPreset.Alpha },
                { "Additive (One One)", BaseShaderRenderBlendPreset.Additive },
                { "Additive Alpha (SrcAlpha One)", BaseShaderRenderBlendPreset.AdditiveAlpha },
                { "Premultiply", BaseShaderRenderBlendPreset.Premultiply },
            };
        }

        static ValueDropdownList<int> GetRenderStateBlendFactorOptions()
        {
            return new ValueDropdownList<int>
            {
                { "Zero", 0 },
                { "One", 1 },
                { "DstColor", 2 },
                { "SrcColor", 3 },
                { "OneMinusDstColor", 4 },
                { "SrcAlpha", 5 },
                { "OneMinusSrcColor", 6 },
                { "DstAlpha", 7 },
                { "OneMinusDstAlpha", 8 },
                { "SrcAlphaSaturate", 9 },
                { "OneMinusSrcAlpha", 10 },
            };
        }

        static ValueDropdownList<int> GetRenderStateCullOptions()
        {
            return new ValueDropdownList<int>
            {
                { "Off", 0 },
                { "Front", 1 },
                { "Back", 2 },
            };
        }

        /// <summary>
        /// UV遨ｺ髢薙・繝峨Ο繝・・繝繧ｦ繝ｳ繧ｪ繝励す繝ｧ繝ｳ
        /// </summary>
        static ValueDropdownList<int> GetUVSpaceOptions()
        {
            return new ValueDropdownList<int>
            {
                { "Sprite Local", 0 },
                { "Screen", 1 },
                { "Texture Raw", 2 },
                { "World XY", 3 },
            };
        }

        /// <summary>
        /// 繝悶Ξ繝ｳ繝峨Δ繝ｼ繝峨・繝峨Ο繝・・繝繧ｦ繝ｳ繧ｪ繝励す繝ｧ繝ｳ
        /// </summary>
        static ValueDropdownList<int> GetBlendModeOptions()
        {
            return new ValueDropdownList<int>
            {
                { "Normal", 0 },
                { "Multiply", 1 },
                { "Screen", 2 },
                { "Overlay", 3 },
                { "Add", 4 },
                { "Soft Light", 5 },
                { "Color Dodge", 6 },
                { "Color Burn", 7 },
                { "Darken", 8 },
                { "Lighten", 9 },
                { "Difference", 10 },
            };
        }

        static ValueDropdownList<int> GetOutlineModeOptions()
        {
            return new ValueDropdownList<int>
            {
                { "Outside", 10 },
                { "Inside", 20 },
            };
        }

        static ValueDropdownList<int> GetOutlineBlendModeOptions()
        {
            return new ValueDropdownList<int>
            {
                { "Alpha", 10 },
                { "Add", 20 },
                { "Screen", 30 },
            };
        }

        static ValueDropdownList<int> GetOutlineWidthUnitOptions()
        {
            return new ValueDropdownList<int>
            {
                { "Texel", 10 },
                { "Screen Pixel", 20 },
            };
        }

        static ValueDropdownList<int> GetOutlineSamplePatternOptions()
        {
            return new ValueDropdownList<int>
            {
                { "Diamond4", 10 },
                { "Box8", 20 },
                { "Circle12", 30 },
            };
        }

        static ValueDropdownList<int> GetOutlineZTestModeOptions()
        {
            return new ValueDropdownList<int>
            {
                { "LessEqual", 10 },
                { "Always", 20 },
            };
        }

        bool ShowOutlineAutoColorSettings => outlineEnabled && outlineAutoColorEnabled;
        bool ShowOutlineAnimatedGradientSettings => outlineEnabled && outlineAnimatedGradientEnabled;
        bool ShowBlendColorAnimatedGradientSettings => blendColor2DEnabled && blendColor2DAnimatedGradientEnabled;
        bool ShowTextOutlineAutoColorSettings => textOutlineEnabled && textOutlineAutoColorEnabled;
        bool ShowAdvancedFadeCircleSettings => advancedFadeEnabled && advancedFadeFadeDirection == 6;
        bool ShowAdvancedFadeBurnAnimatedNoiseSettings => advancedFadeBurnEnabled && advancedFadeBurnAnimatedNoiseEnabled;
    }

    [CreateAssetMenu(fileName = "BaseShaderFxPreset", menuName = "Game/MaterialFx/BaseShaderFxPreset")]
    public sealed class BaseShaderFxPresetSO : ScriptableObject
    {
        [SerializeReference, InlineProperty, HideLabel]
        [Tooltip("SO wrapper for BaseShaderFxPreset data.")]
        BaseShaderFxPreset preset = new();

        public BaseShaderFxPreset Preset => preset;

        public IReadOnlyList<MaterialFxPresetEntry> Entries
            => preset?.Entries ?? Array.Empty<MaterialFxPresetEntry>();

        public void RefreshEntries()
        {
            preset?.RefreshEntries();
        }

        void OnEnable()
        {
            preset?.MarkEntriesDirty();
        }

        void OnValidate()
        {
            preset?.MarkEntriesDirty();
        }
    }
}
