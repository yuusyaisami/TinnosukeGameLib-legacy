Shader "Game/Base/Surface2D_Lit_Fx"
{
    Properties
    {
        // --- Base sprite ---
        _MainTex("Diffuse", 2D) = "white" {}
        _MaskTex("Mask", 2D) = "white" {}

        // --- Render State (for MeshRenderer/SpriteRenderer common control) ---
        // _FxBlendPreset : ブレンドプリセット識別子（デバッグ/可視化用）
        // _FxSrcBlend    : RGBブレンドのソース係数（UnityEngine.Rendering.BlendMode の値）
        // _FxDstBlend    : RGBブレンドのデスティネーション係数（UnityEngine.Rendering.BlendMode の値）
        // _FxZWrite      : 深度書き込みの有効/無効（0=Off, 1=On）
        // _FxCull        : カリングモード（0=Off, 1=Front, 2=Back）
        // _FxQueueOffset : Transparent基準の相対レンダーキューオフセット
        [HideInInspector] _FxBlendPreset("Fx Blend Preset", Float) = 0
        [HideInInspector] _FxSrcBlend("Fx Src Blend", Float) = 5
        [HideInInspector] _FxDstBlend("Fx Dst Blend", Float) = 10
        [HideInInspector] _FxZWrite("Fx ZWrite", Float) = 0
        [HideInInspector] _FxCull("Fx Cull", Float) = 0
        [HideInInspector] _FxQueueOffset("Fx Queue Offset", Float) = 0
        [HideInInspector] _CullMode("Cull Mode", Float) = 0
        _FxBlendIntensity("Fx Blend Intensity", Range(0,1)) = 1

        // Sprite Renderer 用（URP 2D Lit と同等）
        [HideInInspector] _Color("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip("Flip", Vector) = (1,1,1,1)
        [HideInInspector] _SpriteUVRect("Sprite UV Rect", Vector) = (0,0,1,1)
        [HideInInspector] _SpriteTexelSizeLocal("Sprite Texel Size Local", Vector) = (1,1,0,0)
        [HideInInspector] _AlphaTex("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha("Enable External Alpha", Float) = 0

        // --- Unity UI Mask / Stencil ---
        [HideInInspector] _StencilComp("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask("Color Mask", Float) = 15
        [HideInInspector] _UseUIAlphaClip("Use UI Alpha Clip", Float) = 0

        // TextMeshPro / UI Text 用
        // 0: 通常（i.color * tex）
        // 1: TMP SDF（RGB=頂点カラー、Alpha=Smooth(SDF)）
        // 2: TMP Alpha（RGB=頂点カラー、Alpha=tex.a）
        _TextMode("Text Mode (0:Sprite 1:TMP SDF 2:TMP Alpha)", Float) = 0

        // --- Text FX (Outline / Shadow) ---
        _TextOutlineEnabled("Text Outline Enabled", Float) = 0
        _TextOutlineColor("Text Outline Color", Color) = (0,0,0,1)
        _TextOutlineThickness("Text Outline Thickness (px)", Float) = 1
        _TextOutlineSoftness("Text Outline Softness", Range(0,1)) = 0.1

        _TextShadowEnabled("Text Shadow Enabled", Float) = 0
        _TextShadowColor("Text Shadow Color", Color) = (0,0,0,0.5)
        _TextShadowOffset("Text Shadow Offset (px)", Vector) = (1,-1,0,0)
        _TextShadowSoftness("Text Shadow Softness", Range(0,1)) = 0.1

        _TextGlowEnabled("Text Glow Enabled", Float) = 0
        _TextGlowColor("Text Glow Color", Color) = (1,1,1,0.5)
        _TextGlowThickness("Text Glow Thickness", Float) = 2
        _TextGlowSoftness("Text Glow Softness", Range(0,1)) = 0.2

        // --- Outline 2D ---
        _OutlineEnabled("Outline Enabled", Float) = 0
        _OutlineMode("Outline Mode (10:Outside 20:Inside)", Float) = 10
        _OutlineColor("Outline Color", Color) = (1,1,1,1)
        _OutlineWidth("Outline Width", Float) = 1
        _OutlineOpacity("Outline Opacity", Range(0,1)) = 1
        _OutlineSoftness("Outline Softness", Range(0,1)) = 0
        _OutlineBlendMode("Outline Blend Mode (10:Alpha 20:Add 30:Screen)", Float) = 10
        _OutlinePixelPerfect("Outline Pixel Perfect", Float) = 1
        _OutlineWidthUnit("Outline Width Unit (10:Texel 20:ScreenPixel)", Float) = 10
        _OutlinePixelStep("Outline Pixel Step", Float) = 1
        _OutlineSamplePattern("Outline Sample Pattern (10:Diamond4 20:Box8 30:Circle12)", Float) = 10
        _OutlineMaskRespect("Outline Mask Respect", Float) = 1
        _OutlineUseVertexColor("Outline Use VertexColor", Float) = 0
        _OutlineUVClampEnabled("Outline UV Clamp Enabled", Float) = 1
        _OutlineZTestMode("Outline ZTest Mode (10:LessEqual 20:Always)", Float) = 10

        // --- Flash 2D ---
        _FlashEnabled("Flash Enabled", Float) = 0
        _FlashColor("Flash Color", Color) = (1,1,1,1)
        _FlashAmount("Flash Amount", Range(0,1)) = 0
        _FlashMode("Flash Mode (0=Lerp,1=Add)", Float) = 0
        _FlashBlinkEnabled("Flash Blink Enabled", Float) = 0
        _FlashBlinkAmplitude("Flash Blink Amplitude", Float) = 0
        _FlashBlinkSpeed("Flash Blink Speed", Float) = 8
        _FlashBlinkPhaseOffset("Flash Blink Phase Offset", Float) = 0

        // --- Pixelation 2D ---
        _PixelationEnabled("Pixelation Enabled", Float) = 0
        _PixelateMode   ("Pixelate Mode (0:Off 1:Screen 2:Texel 3:ColorOnly)", Float) = 0
        // 画面上のピクセルブロックの大きさ（実ピクセル数）
        _PixelBlockScreenSize("Pixel Block Screen Size", Vector) = (4,4,0,0)
        _PixelColorSteps("Color Quantize Steps", Float) = 0
        _PixelAlphaSteps("Alpha Quantize Steps", Float) = 0

        // --- AdvancedFlip2D (擬似3D回転 + 奥行き縮小 + 歪み) ---
        _AdvFlipEnabled("AdvFlip Enabled", Float) = 0
        _AdvFlipEulerDegX("AdvFlip Euler X (deg)", Float) = 0
        _AdvFlipEulerDegY("AdvFlip Euler Y (deg)", Float) = 0
        _AdvFlipEulerDegZ("AdvFlip Euler Z (deg)", Float) = 0
        _AdvFlipPivotLocal("AdvFlip Pivot Local", Vector) = (0,0,0,0)
        _AdvFlipPerspective("AdvFlip Perspective", Float) = 0
        _AdvFlipDepthScale("AdvFlip Depth Scale", Float) = 1
        _AdvFlipPerspectiveSign("AdvFlip Perspective Sign", Float) = 1
        _AdvFlipScaleClamp("AdvFlip Scale Clamp (min,max)", Vector) = (0.25,2,0,0)
        _AdvWarpShear("AdvWarp Shear", Vector) = (0,0,0,0)
        _AdvWarpBend("AdvWarp Bend", Vector) = (0,0,0,0)
        _AdvFlipFallbackHalfSize("AdvFlip Fallback HalfSize", Vector) = (0.5,0.5,0,0)

        // --- AdvancedFade2D ---
        _AdvancedFade2DEnabled("AdvancedFade2D Enabled", Float) = 0
        _AdvancedFade2DFadeDirection("AdvancedFade2D Fade Direction", Float) = 0
        _AdvancedFade2DFadeAmount("AdvancedFade2D Fade Amount", Float) = 0
        _AdvancedFade2DSoft("AdvancedFade2D Softness", Range(0,1)) = 0.1
        _AdvancedFade2DGlowIntensity("AdvancedFade2D Glow Intensity", Float) = 0
        _AdvancedFade2DGlowRange("AdvancedFade2D Glow Range", Float) = 0.05
        _AdvancedFade2DGlowBlendMode("AdvancedFade2D Glow Blend Mode", Float) = 0
        _AdvancedFade2DWaveParamsA("AdvancedFade2D Wave Params A", Vector) = (0,0,0,0)
        _AdvancedFade2DWaveParamsB("AdvancedFade2D Wave Params B", Vector) = (0,0,0,0)

        _AdvancedFade2DBurnEnabled("AdvancedFade2D Burn Enabled", Float) = 0
        _AdvancedFade2DBurnProgress("AdvancedFade2D Burn Progress", Range(0,1)) = 0
        _AdvancedFade2DBurnEdgeWidth("AdvancedFade2D Burn Edge Width", Range(0,0.5)) = 0.1
        _AdvancedFade2DBurnNoiseScale("AdvancedFade2D Burn Noise Scale", Float) = 4
        _AdvancedFade2DBurnNoiseStrength("AdvancedFade2D Burn Noise Strength", Range(0,1)) = 0.5
        _AdvancedFade2DBurnNoiseType("AdvancedFade2D Burn Noise Type", Float) = 0
        _AdvancedFade2DBurnDirection("AdvancedFade2D Burn Direction (xy)", Vector) = (0,1,0,0)
        _AdvancedFade2DBurnEdgeColor("AdvancedFade2D Burn Edge Color", Color) = (1,0.5,0.1,1)
        _AdvancedFade2DBurnBlendMode("AdvancedFade2D Burn Blend Mode", Float) = 0
        _AdvancedFade2DBurnInvert("AdvancedFade2D Burn Invert", Float) = 0

        _Rainbow2DEnabled("Rainbow2D Enabled", Float) = 0
        _Rainbow2DMode("Rainbow2D Mode (0:Gradient 1:Pixel)", Float) = 0
        _Rainbow2DPattern("Rainbow2D Pattern (0:H 1:V 2:Checker)", Float) = 0
        _Rainbow2DDirection("Rainbow2D Direction (xy)", Vector) = (1,0,0,0)
        _Rainbow2DScale("Rainbow2D Scale", Float) = 1
        _Rainbow2DOffset("Rainbow2D Offset", Float) = 0
        _Rainbow2DSpeed("Rainbow2D Speed", Float) = 0.5
        _Rainbow2DPixelSize("Rainbow2D Pixel Size", Float) = 2
        _Rainbow2DIntensity("Rainbow2D Intensity", Range(0,1)) = 0.5
        _Rainbow2DBlendMode("Rainbow2D Blend Mode (0:Add, 1:Screen, 2:Overlay, 3:Lerp)", Float) = 1

        // ═══════════════════════════════════════════════════════════════════════════
        // Composite System (BaseShader-CompositeSystem-v2.0 準拠)
        // ═══════════════════════════════════════════════════════════════════════════

        // --- TextureSlot Bindings (Slot 0-4 → Atlas Tier/Slice) ---
        [HideInInspector] _AtlasSlot0("Atlas Slot 0 (Tier,Slice)", Vector) = (-1,-1,0,0)
        [HideInInspector] _AtlasSlot1("Atlas Slot 1 (Tier,Slice)", Vector) = (-1,-1,0,0)
        [HideInInspector] _AtlasSlot2("Atlas Slot 2 (Tier,Slice)", Vector) = (-1,-1,0,0)
        [HideInInspector] _AtlasSlot3("Atlas Slot 3 (Tier,Slice)", Vector) = (-1,-1,0,0)
        [HideInInspector] _AtlasSlot4("Atlas Slot 4 (Tier,Slice)", Vector) = (-1,-1,0,0)

        // --- External Textures ---
        [NoScaleOffset] _ExtTexA("External Texture A", 2D) = "white" {}
        [NoScaleOffset] _ExtTexB("External Texture B", 2D) = "white" {}
        [NoScaleOffset] _CustomRT("Custom RenderTexture", 2D) = "white" {}

        // --- Dissolve ---
        _DissolveEnabled("Dissolve Enabled", Float) = 0
        [HideInInspector] _DissolveSource_SlotType("Dissolve Source SlotType", Float) = 0
        [HideInInspector] _DissolveSource_Channel("Dissolve Source Channel", Float) = 1
        [HideInInspector] _DissolveSource_UVSpace("Dissolve Source UVSpace", Float) = 0
        [HideInInspector] _DissolveSource_TilingOffset("Dissolve Source TilingOffset", Vector) = (1,1,0,0)
        [HideInInspector] _DissolveSource_Remap("Dissolve Source Remap", Vector) = (0.5,0.5,1,0)
        _DissolveThreshold("Dissolve Threshold", Range(0,1)) = 0
        _DissolveEdgeWidth("Dissolve Edge Width", Range(0,0.5)) = 0.05
        [HDR] _DissolveEdgeColor("Dissolve Edge Color", Color) = (1,1,1,1)

        // --- FlowWarp ---
        _FlowWarpEnabled("FlowWarp Enabled", Float) = 0
        [HideInInspector] _FlowWarpSource_SlotType("FlowWarp Source SlotType", Float) = 1
        [HideInInspector] _FlowWarpSource_Channel("FlowWarp Source Channel", Float) = 3
        [HideInInspector] _FlowWarpSource_UVSpace("FlowWarp Source UVSpace", Float) = 0
        [HideInInspector] _FlowWarpSource_TilingOffset("FlowWarp Source TilingOffset", Vector) = (1,1,0,0)
        [HideInInspector] _FlowWarpSource_Remap("FlowWarp Source Remap", Vector) = (0.5,0.5,1,0)
        _FlowWarpStrength("FlowWarp Strength", Vector) = (0,0,0,0)
        _FlowWarpSpeed("FlowWarp Speed", Float) = 1

        // --- Mask ---
        _MaskEnabled("Mask Enabled", Float) = 0
        [HideInInspector] _MaskSource_SlotType("Mask Source SlotType", Float) = 2
        [HideInInspector] _MaskSource_Channel("Mask Source Channel", Float) = 1
        [HideInInspector] _MaskSource_UVSpace("Mask Source UVSpace", Float) = 0
        [HideInInspector] _MaskSource_TilingOffset("Mask Source TilingOffset", Vector) = (1,1,0,0)
        [HideInInspector] _MaskSource_Remap("Mask Source Remap", Vector) = (0.5,0.5,1,0)
        _MaskThreshold("Mask Threshold", Range(0,1)) = 0
        _MaskSoftness("Mask Softness", Range(0,1)) = 0.1

        // --- Emission ---
        _EmissionEnabled("Emission Enabled", Float) = 0
        [HDR] _EmissionColor("Emission Color", Color) = (0,0,0,1)
        [HideInInspector] _EmissionSource_SlotType("Emission Source SlotType", Float) = 0
        [HideInInspector] _EmissionSource_Channel("Emission Source Channel", Float) = 1
        [HideInInspector] _EmissionSource_UVSpace("Emission Source UVSpace", Float) = 0
        [HideInInspector] _EmissionSource_TilingOffset("Emission Source TilingOffset", Vector) = (1,1,0,0)
        [HideInInspector] _EmissionSource_Remap("Emission Source Remap", Vector) = (0.5,0.5,1,0)

        // ═══════════════════════════════════════════════════════════════════════════
        // Composite System v3.0 拡張 (BaseShader-CompositeSystem-v3.0 準拠)
        // ═══════════════════════════════════════════════════════════════════════════

        // --- ColorOverlay ---
        _ColorOverlayEnabled("ColorOverlay Enabled", Float) = 0
        [HideInInspector] _ColorOverlaySource_SlotType("ColorOverlay Source SlotType", Float) = 0
        [HideInInspector] _ColorOverlaySource_Channel("ColorOverlay Source Channel", Float) = 1
        [HideInInspector] _ColorOverlaySource_UVSpace("ColorOverlay Source UVSpace", Float) = 0
        [HideInInspector] _ColorOverlaySource_TilingOffset("ColorOverlay Source TilingOffset", Vector) = (1,1,0,0)
        [HideInInspector] _ColorOverlaySource_Remap("ColorOverlay Source Remap", Vector) = (0.5,0.5,1,0)
        _ColorOverlayColor("ColorOverlay Color", Color) = (1,1,1,1)
        _ColorOverlayBlendMode("ColorOverlay Blend Mode", Float) = 0
        _ColorOverlayIntensity("ColorOverlay Intensity", Range(0,1)) = 1

        // --- BlendColor2D ---
        _BlendColor2DEnabled("BlendColor2D Enabled", Float) = 0
        _BlendColor2DColor("BlendColor2D Color", Color) = (1,1,1,1)
        _BlendColor2DBlendIntensity("BlendColor2D Intensity", Range(0,1)) = 0
        _BlendColor2DBlendGradDirection("BlendColor2D Gradient Direction", Float) = 0
        _BlendColor2DBlendGradationAmount("BlendColor2D Gradient Amount", Range(0,1)) = 0
        _BlendColor2DBlendSoftness("BlendColor2D Gradient Softness", Range(0,1)) = 1
        _BlendColor2DBlendMode("BlendColor2D Blend Mode", Float) = 0

        // --- ColorRamp ---
        _ColorRampEnabled("ColorRamp Enabled", Float) = 0
        [HideInInspector] _ColorRampSource_SlotType("ColorRamp Source SlotType", Float) = 0
        [HideInInspector] _ColorRampSource_Channel("ColorRamp Source Channel", Float) = 1
        [HideInInspector] _ColorRampSource_UVSpace("ColorRamp Source UVSpace", Float) = 0
        [HideInInspector] _ColorRampSource_TilingOffset("ColorRamp Source TilingOffset", Vector) = (1,1,0,0)
        [HideInInspector] _ColorRampSource_Remap("ColorRamp Source Remap", Vector) = (0.5,0.5,1,0)
        [NoScaleOffset] _ColorRampTex("ColorRamp Texture", 2D) = "white" {}
        _ColorRampIntensity("ColorRamp Intensity", Range(0,1)) = 1
        _ColorRampPreserveAlpha("ColorRamp Preserve Alpha", Float) = 1

        // --- Refraction ---
        _RefractionEnabled("Refraction Enabled", Float) = 0
        [HideInInspector] _RefractionSource_SlotType("Refraction Source SlotType", Float) = 0
        [HideInInspector] _RefractionSource_Channel("Refraction Source Channel", Float) = 3
        [HideInInspector] _RefractionSource_UVSpace("Refraction Source UVSpace", Float) = 0
        [HideInInspector] _RefractionSource_TilingOffset("Refraction Source TilingOffset", Vector) = (1,1,0,0)
        [HideInInspector] _RefractionSource_Remap("Refraction Source Remap", Vector) = (0.5,0.5,1,0)
        _RefractionStrength("Refraction Strength", Vector) = (0.1,0.1,0,0)
        _RefractionChromaticAberration("Refraction Chromatic Aberration", Float) = 0

        // --- Caustics ---
        _CausticsEnabled("Caustics Enabled", Float) = 0
        [HideInInspector] _CausticsSourceA_SlotType("Caustics Source A SlotType", Float) = 0
        [HideInInspector] _CausticsSourceA_Channel("Caustics Source A Channel", Float) = 1
        [HideInInspector] _CausticsSourceA_UVSpace("Caustics Source A UVSpace", Float) = 0
        [HideInInspector] _CausticsSourceA_TilingOffset("Caustics Source A TilingOffset", Vector) = (1,1,0,0)
        [HideInInspector] _CausticsSourceA_Remap("Caustics Source A Remap", Vector) = (0.5,0.5,1,0)
        [HideInInspector] _CausticsSourceB_SlotType("Caustics Source B SlotType", Float) = 0
        [HideInInspector] _CausticsSourceB_Channel("Caustics Source B Channel", Float) = 1
        [HideInInspector] _CausticsSourceB_UVSpace("Caustics Source B UVSpace", Float) = 0
        [HideInInspector] _CausticsSourceB_TilingOffset("Caustics Source B TilingOffset", Vector) = (1,1,0,0)
        [HideInInspector] _CausticsSourceB_Remap("Caustics Source B Remap", Vector) = (0.5,0.5,1,0)
        _CausticsColor("Caustics Color", Color) = (1,1,1,1)
        _CausticsIntensity("Caustics Intensity", Float) = 1
        _CausticsThreshold("Caustics Threshold", Range(0,1)) = 0.5
        _CausticsSoftness("Caustics Softness", Range(0,1)) = 0.1
        _CausticsScrollA("Caustics Scroll A (xy)", Vector) = (0.1,0.05,0,0)
        _CausticsScrollB("Caustics Scroll B (xy)", Vector) = (-0.08,0.06,0,0)

        // --- Ripple ---
        _RippleEnabled("Ripple Enabled", Float) = 0
        _RippleCenter("Ripple Center (uv)", Vector) = (0.5,0.5,0,0)
        _RippleWaveParams("Ripple Wave (freq,speed,decay,0)", Vector) = (10,2,3,0)
        _RippleAmplitude("Ripple Amplitude", Float) = 0.02
        _RipplePhase("Ripple Phase", Float) = 0
        _RippleDistortUV("Ripple Distort UV", Float) = 1
        _RippleColorBlend("Ripple Color Blend", Float) = 0
        _RippleColor("Ripple Color", Color) = (1,1,1,0.5)

        // --- HueShift ---
        _HueShiftEnabled("HueShift Enabled", Float) = 0
        [HideInInspector] _HueShiftMaskSource_SlotType("HueShift Mask Source SlotType", Float) = 255
        [HideInInspector] _HueShiftMaskSource_Channel("HueShift Mask Source Channel", Float) = 1
        [HideInInspector] _HueShiftMaskSource_UVSpace("HueShift Mask Source UVSpace", Float) = 0
        [HideInInspector] _HueShiftMaskSource_TilingOffset("HueShift Mask Source TilingOffset", Vector) = (1,1,0,0)
        [HideInInspector] _HueShiftMaskSource_Remap("HueShift Mask Source Remap", Vector) = (0.5,0.5,1,0)
        _HueShiftAmount("HueShift Amount", Range(0,1)) = 0
        _HueSaturationMod("Hue Saturation Mod", Float) = 0
        _HueValueMod("Hue Value Mod", Float) = 0

        // --- NormalMap ---
        _NormalMapEnabled("NormalMap Enabled", Float) = 0
        [HideInInspector] _NormalMapSource_SlotType("NormalMap Source SlotType", Float) = 0
        [HideInInspector] _NormalMapSource_Channel("NormalMap Source Channel", Float) = 1
        [HideInInspector] _NormalMapSource_UVSpace("NormalMap Source UVSpace", Float) = 0
        [HideInInspector] _NormalMapSource_TilingOffset("NormalMap Source TilingOffset", Vector) = (1,1,0,0)
        [HideInInspector] _NormalMapSource_Remap("NormalMap Source Remap", Vector) = (0.5,0.5,1,0)
        _NormalMapStrength("NormalMap Strength", Float) = 1
        _NormalMapLightDir("NormalMap Light Direction", Vector) = (0.5,0.5,1,0)
        _NormalMapBlendWithBase("NormalMap Blend With Base", Range(0,1)) = 0

        // ═══════════════════════════════════════════════════════════════════════════
        // Transition System v1.0 (BaseShader-TransitionSystem-v1.0 準拠)
        // Phase 3.5: 外部テクスチャとのブレンド遷移
        // ソーステクスチャは _ExtTexA を使用
        // ═══════════════════════════════════════════════════════════════════════════
        _TransitionEnabled("Transition Enabled", Float) = 0
        _TransitionBlendMode("Transition Blend Mode (0=CrossFade,1=Dissolve,2=Wipe)", Float) = 0
        _TransitionProgress("Transition Progress", Range(0,1)) = 0
        _TransitionParams("Transition Params (edgeWidth,softness,wipeAngle,reserved)", Vector) = (0.1,0.1,0,0)
        [HideInInspector] _TransitionFromSpriteUVRect("Transition From Sprite UV Rect (minU,minV,maxU,maxV)", Vector) = (0,0,1,1)
    }

    SubShader
    {
        Tags
        {
            "Queue"                  = "Transparent"
            "RenderType"             = "Transparent"
            "IgnoreProjector"        = "True"
            "CanUseSpriteAtlas"      = "True"
            "DisableBatching"        = "True"
            "RenderPipeline"         = "UniversalPipeline"
            "UniversalMaterialType"  = "Sprite-Unlit"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Pass
        {
            Name "Universal2D_WebGL"
            Tags { "LightMode" = "Universal2D" }

            Cull [_FxCull]
            ZWrite [_FxZWrite]
            ZTest [unity_GUIZTestMode]
            Blend [_FxSrcBlend] [_FxDstBlend], One OneMinusSrcAlpha
            ColorMask [_ColorMask]

            HLSLPROGRAM
            #pragma only_renderers gles3
            #pragma vertex   VertWebGL
            #pragma fragment FragWebGL
            #pragma target 3.0
            #pragma multi_compile_instancing

            #pragma multi_compile _ DEBUG_DISPLAY

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            #define SURFACE2D_WEBGL_SAFE 1
            #include "Assets/GameLib/Script/Shader/Core/BaseShader/Surface2D.hlsl"

            float4 _ClipRect;
            float  _UseUIAlphaClip;
            float  _FxBlendIntensity;

            #if defined(ETC1_EXTERNAL_ALPHA)
            TEXTURE2D(_AlphaTex); SAMPLER(sampler_AlphaTex);
            #endif

            inline float UnityGet2DClipping(float2 position, float4 clipRect)
            {
                float2 inside = step(clipRect.xy, position) * step(position, clipRect.zw);
                return inside.x * inside.y;
            }

            inline float2 TransformTex(float2 uv, float4 st)
            {
                return uv * st.xy + st.zw;
            }

            struct AttributesWebGL
            {
                float3 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VaryingsWebGL
            {
                float4 positionCS : SV_POSITION;
                half4  color      : COLOR;
                float2 uv         : TEXCOORD0;
                float2 screenUV   : TEXCOORD1;
                float2 uiLocalPos : TEXCOORD2;

                SURFACE2D_VARYINGS_EXTRA_MEMBERS

            #if defined(DEBUG_DISPLAY)
                float3 positionWS : TEXCOORD3;
            #endif

                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            VaryingsWebGL VertWebGL(AttributesWebGL input)
            {
                VaryingsWebGL o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                Surface2DContext ctx = MakeSurface2DContext();

                float3 posOS = input.positionOS;
                posOS.x *= _Flip.x;
                posOS.y *= _Flip.y;
                posOS = Surface2D_Vertex_ApplyPositionOS(posOS, input.uv, ctx);

                o.positionCS = TransformObjectToHClip(posOS);

                float4 screenPos = ComputeScreenPos(o.positionCS);
                o.screenUV = screenPos.xy / screenPos.w;

            #if defined(DEBUG_DISPLAY)
                o.positionWS = TransformObjectToWorld(input.positionOS);
            #endif

                o.uv = TransformTex(input.uv, _MainTex_ST);
                o.color = input.color * _Color;
                o.uiLocalPos = posOS.xy;

                SURFACE2D_VARYINGS_EXTRA_INIT(o);
                Surface2D_VertexInject(input, o);

                return o;
            }

            half4 FragWebGL(VaryingsWebGL i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                float2 uv = i.uv;
                Surface2DContext surfaceCtx = MakeSurface2DContext();
                uv = Surface2D_BeforeSample_Apply(uv, i.screenUV, surfaceCtx);

                half4 texCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);

            #if defined(ETC1_EXTERNAL_ALPHA)
                if (_EnableExternalAlpha > 0.5f)
                {
                    half4 alphaTex = SAMPLE_TEXTURE2D(_AlphaTex, sampler_AlphaTex, uv);
                    texCol.a = alphaTex.r;
                }
            #endif

                half4 baseCol;
                if (_TextMode > 0.5 && _TextMode < 1.5)
                {
                    baseCol.rgb = i.color.rgb;
                    float sd = texCol.a;
                    float w = max(fwidth(sd), 1e-5);
                    float faceAlpha = smoothstep(0.5 - w, 0.5 + w, sd);
                    baseCol.a = i.color.a * faceAlpha;
                }
                else if (_TextMode > 1.5)
                {
                    baseCol.rgb = i.color.rgb;
                    float bitmapMask = max(texCol.a, max(texCol.r, max(texCol.g, texCol.b)));
                    baseCol.a = i.color.a * bitmapMask;
                }
                else
                {
                    baseCol = i.color * texCol;
                }

                #ifdef UNITY_UI_CLIP_RECT
                baseCol.a *= UnityGet2DClipping(i.uiLocalPos, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                if (_UseUIAlphaClip > 0.5)
                    clip(baseCol.a - 0.001);
                #endif

                float baseAlphaRaw = texCol.a;
                if (_TextMode > 1.5)
                {
                    baseAlphaRaw = max(texCol.a, max(texCol.r, max(texCol.g, texCol.b)));
                }

                half4 fxColor = Surface2D_AfterSample_Apply(baseCol, baseAlphaRaw, uv, i.screenUV, surfaceCtx, i.color.a);
                baseCol = fxColor;

                half blendIntensity = (half)saturate(_FxBlendIntensity);
                baseCol.rgb *= blendIntensity;
                baseCol.a *= blendIntensity;

                return baseCol;
            }
            ENDHLSL
        }
    }

    SubShader
    {
        Tags
        {
            "Queue"                  = "Transparent"
            "RenderType"             = "Transparent"
            "IgnoreProjector"        = "True"
            "CanUseSpriteAtlas"      = "True"
            "DisableBatching"        = "True"
            "RenderPipeline"         = "UniversalPipeline"
            "UniversalMaterialType"  = "Sprite-Lit"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Pass
        {
            Name "Universal2D"
            Tags { "LightMode" = "Universal2D" }

            Cull [_FxCull]
            ZWrite [_FxZWrite]
            ZTest [unity_GUIZTestMode]
            Blend [_FxSrcBlend] [_FxDstBlend], One OneMinusSrcAlpha
            ColorMask [_ColorMask]

            HLSLPROGRAM
            #pragma exclude_renderers gles3
            #pragma vertex   Vert
            #pragma fragment Frag
            #pragma target 3.5
            #pragma multi_compile_instancing

            #pragma multi_compile _ DEBUG_DISPLAY

            // UI ClipRect / AlphaClip 用キーワード（旧UnityUI.cgincは使わない）
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            // ===== URP includes only =====
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/CombinedShapeLightShared.hlsl"

            // ===== Textures / Samplers =====
            TEXTURE2D(_MainTex);  SAMPLER(sampler_MainTex);
            TEXTURE2D(_MaskTex);  SAMPLER(sampler_MaskTex);

            // ===== your includes =====
            #include "Assets/GameLib/Script/Shader/Core/BaseShader/Surface2D.hlsl"

            float4 _ClipRect;
            float  _UseUIAlphaClip;
            float  _FxBlendIntensity;

            #if defined(ETC1_EXTERNAL_ALPHA)
            TEXTURE2D(_AlphaTex); SAMPLER(sampler_AlphaTex);
            #endif

            // ===== replacements for UnityUI.cginc =====
            inline float UnityGet2DClipping(float2 position, float4 clipRect)
            {
                // clipRect: (xMin,yMin,xMax,yMax)
                float2 inside = step(clipRect.xy, position) * step(position, clipRect.zw);
                return inside.x * inside.y;
            }

            inline float2 TransformTex(float2 uv, float4 st)
            {
                return uv * st.xy + st.zw;
            }

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4  color      : COLOR;
                float2 uv         : TEXCOORD0;

                half2 lightingUV  : TEXCOORD1;
                float2 screenUV   : TEXCOORD2;

                float2 uiLocalPos : TEXCOORD6;

                SURFACE2D_VARYINGS_EXTRA_MEMBERS

            #if defined(DEBUG_DISPLAY)
            float3 positionWS : TEXCOORD3;
            #endif

            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                Surface2DContext ctx = MakeSurface2DContext();

                float3 posOS = input.positionOS;
                posOS.x *= _Flip.x;
                posOS.y *= _Flip.y;

                posOS = Surface2D_Vertex_ApplyPositionOS(posOS, input.uv, ctx);

                o.positionCS = TransformObjectToHClip(posOS);

                #if defined(DEBUG_DISPLAY)
                o.positionWS = TransformObjectToWorld(input.positionOS);
                #endif

                o.uv = TransformTex(input.uv, _MainTex_ST);

                float4 screenPos = ComputeScreenPos(o.positionCS);
                o.screenUV = screenPos.xy / screenPos.w;

                float2 ndc = o.positionCS.xy / max(o.positionCS.w, 1e-6);
                o.lightingUV = half2(ndc);

                SURFACE2D_VARYINGS_EXTRA_INIT(o);
                Surface2D_VertexInject(input, o);

                o.color = input.color * _Color;

                // UI ClipRect 判定は「ローカル座標系」を使う（UnityUI.cginc互換のやり方）
                o.uiLocalPos = posOS.xy;

                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                float2 uv = i.uv;
                Surface2DContext surfaceCtx = MakeSurface2DContext();
                uv = Surface2D_BeforeSample_Apply(uv, i.screenUV, surfaceCtx);

                half4 texCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);

                #if defined(ETC1_EXTERNAL_ALPHA)
                if (_EnableExternalAlpha > 0.5f)
                {
                    half4 alphaTex = SAMPLE_TEXTURE2D(_AlphaTex, sampler_AlphaTex, uv);
                    texCol.a = alphaTex.r;
                }
                #endif

                half4 baseCol;
                if (_TextMode > 0.5 && _TextMode < 1.5)
                {
                    // TMP SDF: alpha = smoothstep around 0.5 (distance field)
                    // Use distance as baseAlphaRaw for outline/glow, but face alpha should be crisp.
                    baseCol.rgb = i.color.rgb;
                    float sd = texCol.a;
                    float w = max(fwidth(sd), 1e-5);
                    float faceAlpha = smoothstep(0.5 - w, 0.5 + w, sd);
                    baseCol.a = i.color.a * faceAlpha;
                }
                else if (_TextMode > 1.5)
                {
                    // TMP bitmap-like alpha: vertex color * texture alpha
                    baseCol.rgb = i.color.rgb;
                    float bitmapMask = max(texCol.a, max(texCol.r, max(texCol.g, texCol.b)));
                    baseCol.a = i.color.a * bitmapMask;
                }
                else
                {
                    baseCol = i.color * texCol;
                }
                half4 maskCol = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, uv);

                #ifdef UNITY_UI_CLIP_RECT
                baseCol.a *= UnityGet2DClipping(i.uiLocalPos, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                if (_UseUIAlphaClip > 0.5)
                    clip(baseCol.a - 0.001);
                #endif

                float baseAlphaRaw = texCol.a;
                if (_TextMode > 1.5)
                {
                    // For bitmap fonts, alpha is often packed in RGB.
                    baseAlphaRaw = max(texCol.a, max(texCol.r, max(texCol.g, texCol.b)));
                }
                float4 fxColor = Surface2D_AfterSample_Apply(baseCol, baseAlphaRaw, uv, i.screenUV, surfaceCtx, i.color.a);
                baseCol = (half4)fxColor;
                half blendIntensity = (half)saturate(_FxBlendIntensity);
                baseCol.rgb *= blendIntensity;
                baseCol.a *= blendIntensity;

                SurfaceData2D surfaceData;
                InputData2D   inputData;
                InitializeSurfaceData(baseCol.rgb, baseCol.a, maskCol, surfaceData);
                InitializeInputData(uv, i.lightingUV, inputData);

                return CombinedShapeLightShared(surfaceData, inputData);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
