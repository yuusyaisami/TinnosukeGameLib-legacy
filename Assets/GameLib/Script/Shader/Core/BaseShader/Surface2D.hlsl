#ifndef GAME_SURFACE2D_INCLUDED
#define GAME_SURFACE2D_INCLUDED

#ifndef Surface2D_VertexInject
#define Surface2D_VertexInject(input, output)
#endif

#ifndef SURFACE2D_VARYINGS_EXTRA_MEMBERS
#define SURFACE2D_VARYINGS_EXTRA_MEMBERS
#endif

#ifndef SURFACE2D_VARYINGS_EXTRA_INIT
#define SURFACE2D_VARYINGS_EXTRA_INIT(output)
#endif

// すべての 2D スプライト/テキストを共通の Surface2D で扱うための基底構造体
struct Surface2D
{
    float3 color;        // 現在の最終カラー
    float  alpha;        // 現在の最終アルファ

    float  baseAlphaRaw; // 元テクスチャ/SDF の素の α（アウトライン等の基準）
    float  vertexAlpha;  // 頂点α（TextFxの外周にも適用するため保持）
    float  alphaFactor;  // マスク/フェード等の積（外周にも反映するため保持）

    float2 uvMain;       // _MainTex 用 UV（元テクスチャ座標）
    float2 uv;           // alias for uvMain - legacy features expect s.uv
    float2 uvLocal;      // スプライトローカル UV (0..1)、SpriteAtlas 依存を回避
    float2 screenUV;     // 0..1 のスクリーン座標
    float2 fadeUV;       // フェード用 UV（SDF/Rect 共通）
};

CBUFFER_START(UnityPerMaterial)
    float4 _MainTex_ST;
    float4 _Color;
    float  _EnableExternalAlpha;
    float3 _padding_C1;
    float4 _MainTex_TexelSize;
    float  _TextMode;
    float3 _padding_TextMode;

    // --- Flash ---
    float  _FlashEnabled;
    float4 _FlashColor;
    float  _FlashAmount;
    float  _FlashMode;
    float  _FlashBlinkEnabled;
    float  _FlashBlinkAmplitude;
    float  _FlashBlinkSpeed;
    float  _FlashBlinkPhaseOffset;

    // --- Pixelation ---
    float  _PixelationEnabled;
    float4 _PixelBlockScreenSize; // (x, y) = 画面ピクセル何個をまとめてブロックにするか
    float  _PixelateMode;         // 0:Off, 1:Screen, 2:Texel, 3:ColorOnly
    float  _PixelColorSteps;      // 色量子化ステップ
    float  _PixelAlphaSteps;      // α量子化ステップ

    // --- AdvancedFlip2D (擬似3D回転 + 奥行き縮小 + 歪み) ---
    float  _AdvFlipEnabled;              // 0 = disabled, 1 = enabled
    float  _AdvFlipEulerDegX;            // X component (deg)
    float  _AdvFlipEulerDegY;            // Y component (deg)
    float  _AdvFlipEulerDegZ;            // Z component (deg)
    float4 _AdvFlipPivotLocal;           // xy = ローカル座標での回転中心
    float  _AdvFlipPerspective;          // 奥行きによる縮小の強さ (0=無効)
    float  _AdvFlipDepthScale;           // 回転で生成されるzをどれくらい奥行きとして扱うか
    float  _AdvFlipPerspectiveSign;      // +1 / -1 環境による奥行き方向補正
    float4 _AdvFlipScaleClamp;           // xy = (minScale, maxScale) 縮小/拡大の上限
    float4 _AdvWarpShear;                // xy = せん断係数
    float4 _AdvWarpBend;                 // xy = 曲げ係数
    float4 _AdvFlipFallbackHalfSize;     // xy = サイズ推定できない場合のフォールバック

    // --- Text FX (Outline / Shadow) ---
    float  _TextOutlineEnabled;
    float4 _TextOutlineColor;
    float  _TextOutlineThickness;
    float  _TextOutlineSoftness;
    float  _TextOutlineDirectionMask;
    float  _TextOutlineAutoColorEnabled;
    float  _TextOutlineAutoColorMode;
    float  _TextOutlineAutoHue;
    float  _TextOutlineAutoSaturation;
    float  _TextOutlineAutoLightness;
    float  _TextShadowEnabled;
    float4 _TextShadowColor;
    float4 _TextShadowOffset; // xy
    float  _TextShadowSoftness;
    float3 _padding_TextShadow;

    float  _TextGlowEnabled;
    float4 _TextGlowColor;
    float  _TextGlowThickness;
    float  _TextGlowSoftness;
    float2 _padding_TextGlow;

    // --- Outline 2D ---
    float  _OutlineEnabled;
    float  _OutlineMode;
    float4 _OutlineColor;
    float  _OutlineDirectionMask;
    float  _OutlineAutoColorEnabled;
    float  _OutlineAutoColorMode;
    float  _OutlineAutoHue;
    float  _OutlineAutoSaturation;
    float  _OutlineAutoLightness;
    float  _OutlineAnimatedGradientEnabled;
    float  _OutlineAnimatedGradientPatternType;
    float  _OutlineAnimatedGradientMasterStrength;
    float  _OutlineAnimatedGradientNoiseScale;
    float4 _OutlineAnimatedGradientNoiseDirection;
    float  _OutlineAnimatedGradientNoiseSpeed;
    float4 _OutlineAnimatedGradientNoiseOffset;
    float  _OutlineAnimatedGradientRotationSpeed;
    float  _OutlineAnimatedGradientPulseAmplitude;
    float  _OutlineAnimatedGradientPulseSpeed;
    float  _OutlineAnimatedGradientWarpPatternType;
    float  _OutlineAnimatedGradientWarpScale;
    float  _OutlineAnimatedGradientWarpStrength;
    float4 _OutlineAnimatedGradientWarpDirection;
    float  _OutlineAnimatedGradientWarpSpeed;
    float  _OutlineAnimatedGradientLoopSeconds;
    float  _OutlineAnimatedGradientOctaves;
    float  _OutlineAnimatedGradientLacunarity;
    float  _OutlineAnimatedGradientGain;
    float  _OutlineAnimatedGradientCellSharpness;
    float  _OutlineAnimatedGradientPatternContrast;
    float  _OutlineAnimatedGradientHueAmplitude;
    float  _OutlineAnimatedGradientSaturationAmplitude;
    float  _OutlineAnimatedGradientLightnessAmplitude;
    float  _OutlineWidth;
    float  _OutlineOpacity;
    float  _OutlineSoftness;
    float  _OutlineBlendMode;
    float  _OutlinePixelPerfect;
    float  _OutlineWidthUnit;
    float  _OutlinePixelStep;
    float  _OutlineSamplePattern;
    float  _OutlineMaskRespect;
    float  _OutlineUseVertexColor;
    float  _OutlineUVClampEnabled;
    float  _OutlineZTestMode;
    float  _padding_Outline;

    // --- Rainbow2D ---
    float  _Rainbow2DEnabled;
    float  _Rainbow2DMode;
    float  _Rainbow2DPattern;
    float4 _Rainbow2DDirection;
    float  _Rainbow2DScale;
    float  _Rainbow2DOffset;
    float  _Rainbow2DSpeed;
    float  _Rainbow2DPixelSize;
    float  _Rainbow2DIntensity;
    float  _Rainbow2DBlendMode;
    float2 _padding_Rainbow2D;

    // ═══════════════════════════════════════════════════════════════════════════
    // Composite System (BaseShader-CompositeSystem-v1.0 準拠)
    // ═══════════════════════════════════════════════════════════════════════════

    // --- Dissolve ---
    float  _DissolveEnabled;
    float  _DissolveSource_SlotType;
    float  _DissolveSource_Channel;
    float  _DissolveSource_UVSpace;
    float4 _DissolveSource_TilingOffset;  // xy=tiling, zw=offset
    float4 _DissolveSource_Remap;         // x=bias, y=gain, z=gamma, w=invert
    float  _DissolveThreshold;
    float  _DissolveEdgeWidth;
    float4 _DissolveEdgeColor;

    // --- FlowWarp ---
    float  _FlowWarpEnabled;
    float  _FlowWarpSource_SlotType;
    float  _FlowWarpSource_Channel;
    float  _FlowWarpSource_UVSpace;
    float4 _FlowWarpSource_TilingOffset;
    float4 _FlowWarpSource_Remap;
    float4 _FlowWarpStrength;   // xy = strength
    float  _FlowWarpSpeed;

    // --- Mask ---
    float  _MaskEnabled;
    float  _MaskSource_SlotType;
    float  _MaskSource_Channel;
    float  _MaskSource_UVSpace;
    float4 _MaskSource_TilingOffset;
    float4 _MaskSource_Remap;
    float  _MaskThreshold;
    float  _MaskSoftness;

    // --- Emission (Source + Color) ---
    float  _EmissionEnabled;
    float4 _EmissionColor;  // rgb=color, a=intensity
    float  _EmissionSource_SlotType;
    float  _EmissionSource_Channel;
    float  _EmissionSource_UVSpace;
    float4 _EmissionSource_TilingOffset;
    float4 _EmissionSource_Remap;

    // ═══════════════════════════════════════════════════════════════════════════
    // Composite System v3.0 拡張
    // ═══════════════════════════════════════════════════════════════════════════

    // --- ColorOverlay (v3.0) ---
    // ★ BaseShader.shader のプロパティ名と一致させる
    float  _ColorOverlayEnabled;                     // int: 有効フラグ
    float  _ColorOverlaySource_SlotType;             // int: TEXTURE_SLOT_*
    float  _ColorOverlaySource_Channel;              // int: CHANNEL_*
    float  _ColorOverlaySource_UVSpace;              // int: NOISE_UV_SPACE_*
    float4 _ColorOverlaySource_TilingOffset;         // float4: xy=tiling, zw=offset
    float4 _ColorOverlaySource_Remap;                // float4: x=bias, y=gain, z=gamma, w=invert
    float4 _ColorOverlayColor;                       // float4: RGBA オーバーレイ色
    float  _ColorOverlayBlendMode;                   // int: BLEND_MODE_*
    float  _ColorOverlayIntensity;                   // float: 強度 0-1

    // --- BlendColor2D ---
    float  _BlendColor2DEnabled;                     // int: 有効フラグ
    float4 _BlendColor2DColor;                       // float4: RGBA blend color (BlendColor2D専用)
    float  _BlendColor2DBlendIntensity;              // float: 強度 0-1
    float  _BlendColor2DBlendGradDirection;          // int: gradient direction
    float  _BlendColor2DBlendGradationAmount;        // float: grad amount 0-1
    float  _BlendColor2DBlendSoftness;               // float: grad softness (0=hard, 1=soft/current)
    float  _BlendColor2DBlendMode;                   // int: BLEND_MODE_* (追加)
    float  _BlendColor2DAnimatedGradientEnabled;
    float  _BlendColor2DAnimatedGradientPatternType;
    float  _BlendColor2DAnimatedGradientMasterStrength;
    float  _BlendColor2DAnimatedGradientNoiseScale;
    float4 _BlendColor2DAnimatedGradientNoiseDirection;
    float  _BlendColor2DAnimatedGradientNoiseSpeed;
    float4 _BlendColor2DAnimatedGradientNoiseOffset;
    float  _BlendColor2DAnimatedGradientRotationSpeed;
    float  _BlendColor2DAnimatedGradientPulseAmplitude;
    float  _BlendColor2DAnimatedGradientPulseSpeed;
    float  _BlendColor2DAnimatedGradientWarpPatternType;
    float  _BlendColor2DAnimatedGradientWarpScale;
    float  _BlendColor2DAnimatedGradientWarpStrength;
    float4 _BlendColor2DAnimatedGradientWarpDirection;
    float  _BlendColor2DAnimatedGradientWarpSpeed;
    float  _BlendColor2DAnimatedGradientLoopSeconds;
    float  _BlendColor2DAnimatedGradientOctaves;
    float  _BlendColor2DAnimatedGradientLacunarity;
    float  _BlendColor2DAnimatedGradientGain;
    float  _BlendColor2DAnimatedGradientCellSharpness;
    float  _BlendColor2DAnimatedGradientPatternContrast;
    float  _BlendColor2DAnimatedGradientHueAmplitude;
    float  _BlendColor2DAnimatedGradientSaturationAmplitude;
    float  _BlendColor2DAnimatedGradientLightnessAmplitude;

    // --- ColorRamp (v3.0) ---
    // ★ BaseShader.shader のプロパティ名と一致させる
    float  _ColorRampEnabled;                        // int: 有効フラグ
    float  _ColorRampSource_SlotType;                // int: TEXTURE_SLOT_*
    float  _ColorRampSource_Channel;                 // int: CHANNEL_*
    float  _ColorRampSource_UVSpace;                 // int: NOISE_UV_SPACE_*
    float4 _ColorRampSource_TilingOffset;            // float4: xy=tiling, zw=offset
    float4 _ColorRampSource_Remap;                   // float4: x=bias, y=gain, z=gamma, w=invert
    // _ColorRampTex はテクスチャ宣言（ColorRamp2D.hlsl）
    float  _ColorRampIntensity;                      // float: 強度 0-1
    float  _ColorRampPreserveAlpha;                  // int: アルファを維持するか

    // --- ExternalTextureComposite ---
    float  _ExternalTextureCompositeEnabled;                // int: 有効フラグ
    float  _ExternalTextureCompositeSource_SlotType;        // int: TEXTURE_SLOT_*
    float  _ExternalTextureCompositeSource_Channel;         // int: CHANNEL_*
    float  _ExternalTextureCompositeSource_UVSpace;         // int: NOISE_UV_SPACE_*
    float4 _ExternalTextureCompositeSource_TilingOffset;    // float4: xy=tiling, zw=offset
    float4 _ExternalTextureCompositeSource_Remap;           // float4: x=bias, y=gain, z=gamma, w=invert
    float  _ExternalTextureCompositeBlendMode;              // int: blend mode
    float  _ExternalTextureCompositeIntensity;              // float: 強度 0-1
    float  _ExternalTextureCompositeUseTextureAlpha;        // int: source alpha を重みへ使うか
    float4 _ExternalTextureCompositeTint;                   // float4: ティント
    float  _ExternalTextureCompositeDisableWhenTextureMissing; // int: texture missing 時に無効化
    float  _ExternalTextureCompositeAffectSurfaceAlpha;     // int: surface alpha に反映するか

    // --- Refraction (v3.0) ---
    // ★ BaseShader.shader のプロパティ名と一致させる
    float  _RefractionEnabled;                       // int: 有効フラグ
    float  _RefractionSource_SlotType;               // int: TEXTURE_SLOT_*
    float  _RefractionSource_Channel;                // int: CHANNEL_* (CHANNEL_GB推奨)
    float  _RefractionSource_UVSpace;                // int: NOISE_UV_SPACE_*
    float4 _RefractionSource_TilingOffset;           // float4: xy=tiling, zw=offset
    float4 _RefractionSource_Remap;                  // float4: x=bias, y=gain, z=gamma, w=invert
    float4 _RefractionStrength;                      // float4: xy=歪み強度 (Vectorプロパティ)
    float  _RefractionChromaticAberration;           // float: 色収差強度

    // --- Caustics (v3.0) ---
    // ★ BaseShader.shader のプロパティ名と一致させる
    float  _CausticsEnabled;                         // int: 有効フラグ
    float  _CausticsSourceA_SlotType;                // int: レイヤーAスロットタイプ
    float  _CausticsSourceA_Channel;                 // int: レイヤーAチャンネル
    float  _CausticsSourceA_UVSpace;                 // int: レイヤーA UVSpace
    float4 _CausticsSourceA_TilingOffset;            // float4: レイヤーA TilingOffset
    float4 _CausticsSourceA_Remap;                   // float4: レイヤーA Remap
    float  _CausticsSourceB_SlotType;                // int: レイヤーBスロットタイプ
    float  _CausticsSourceB_Channel;                 // int: レイヤーBチャンネル
    float  _CausticsSourceB_UVSpace;                 // int: レイヤーB UVSpace
    float4 _CausticsSourceB_TilingOffset;            // float4: レイヤーB TilingOffset
    float4 _CausticsSourceB_Remap;                   // float4: レイヤーB Remap
    float4 _CausticsColor;                           // float4: RGBA ティント色
    float  _CausticsIntensity;                       // float: 強度
    float  _CausticsThreshold;                       // float: しきい値 0-1
    float  _CausticsSoftness;                        // float: ソフトネス 0-1
    float4 _CausticsScrollA;                         // float4: xy=レイヤーAスクロール速度
    float4 _CausticsScrollB;                         // float4: xy=レイヤーBスクロール速度

    // --- Ripple (v3.0) ---
    // ★ BaseShader.shader のプロパティ名と一致させる
    float  _RippleEnabled;                           // int: 有効フラグ
    float4 _RippleCenter;                            // float4: xy=波紋中心
    float4 _RippleWaveParams;                        // float4: x=freq, y=speed, z=decay, w=0
    float  _RippleAmplitude;                         // float: 振幅
    float  _RipplePhase;                             // float: 位相（アニメーション用）
    float  _RippleDistortUV;                         // float: UV歪み有効 (0 or 1)
    float  _RippleColorBlend;                        // float: 色ブレンド強度
    float4 _RippleColor;                             // float4: RGBA 波紋色

    // --- HueShift (v3.0) ---
    // ★ BaseShader.shader のプロパティ名と一致させる (HueShiftMaskSource)
    float  _HueShiftEnabled;                         // int: 有効フラグ
    float  _HueShiftMaskSource_SlotType;             // int: TEXTURE_SLOT_* (255=無効、全面適用)
    float  _HueShiftMaskSource_Channel;              // int: CHANNEL_*
    float  _HueShiftMaskSource_UVSpace;              // int: NOISE_UV_SPACE_*
    float4 _HueShiftMaskSource_TilingOffset;         // float4: xy=tiling, zw=offset
    float4 _HueShiftMaskSource_Remap;                // float4: x=bias, y=gain, z=gamma, w=invert
    float  _HueShiftAmount;                          // float: シフト量 (0-1で360度)
    float  _HueSaturationMod;                        // float: 彩度調整 (0=変化なし)
    float  _HueValueMod;                             // float: 明度調整 (0=変化なし)

    // --- NormalMap (v3.0) ---
    // ★ BaseShader.shader のプロパティ名と一致させる
    float  _NormalMapEnabled;                        // int: 有効フラグ
    float  _NormalMapSource_SlotType;                // int: TEXTURE_SLOT_*
    float  _NormalMapSource_Channel;                 // int: CHANNEL_* (CHANNEL_GB推奨)
    float  _NormalMapSource_UVSpace;                 // int: NOISE_UV_SPACE_*
    float4 _NormalMapSource_TilingOffset;            // float4: xy=tiling, zw=offset
    float4 _NormalMapSource_Remap;                   // float4: x=bias, y=gain, z=gamma, w=invert
    float  _NormalMapStrength;                       // float: 法線強度
    float4 _NormalMapLightDir;                       // float4: xyz=光源方向
    float  _NormalMapBlendWithBase;                  // float: 0=ノイズのみ, 1=ベース法線のみ

    // ═══════════════════════════════════════════════════════════════════════════
    // Transition System v1.0 (BaseShader-TransitionSystem-v1.0 準拠)
    // AnimationSpriteChannelPlayer からのみ制御される
    // ═══════════════════════════════════════════════════════════════════════════

    // --- Transition ---
    float  _TransitionEnabled;                       // int: 有効フラグ
    float  _TransitionBlendMode;                     // int: TransitionBlendMode enum (0=CrossFade, 1=Dissolve, 2=WipeH, 3=WipeV)
    float  _TransitionProgress;                      // float: 進行度 0-1
    float4 _TransitionParams;                        // x=edgeWidth, y=softness, z=direction, w=reserved
    float4 _TransitionFromSpriteUVRect;              // from側スプライトのUV矩形 (minU, minV, maxU, maxV)

    // --- AdvancedFade2D ---
    float  _AdvancedFade2DEnabled;                    // int: enabled
    float  _AdvancedFade2DFadeDirection;              // int: Fade direction (FADE_*)
    float  _AdvancedFade2DFadeAmount;                 // float: fade amount 0-1
    float  _AdvancedFade2DSoft;                       // float: softness
    float  _AdvancedFade2DGlowIntensity;              // float: glow intensity
    float  _AdvancedFade2DGlowRange;                  // float: glow range
    float  _AdvancedFade2DGlowBlendMode;              // int: GLOW_BLEND_*
    float4 _AdvancedFade2DWaveParamsA;                // float4: wave param A
    float4 _AdvancedFade2DWaveParamsB;                // float4: wave param B
    float  _AdvancedFade2DCircleStartAngleDeg;
    float  _AdvancedFade2DCircleClockwise;
    // --- AdvancedFade2D Burn ---
    float  _AdvancedFade2DBurnEnabled;                // int: enabled
    float  _AdvancedFade2DBurnProgress;               // float: 0-1
    float  _AdvancedFade2DBurnEdgeWidth;              // float: edge width
    float  _AdvancedFade2DBurnNoiseScale;             // float: noise scale
    float  _AdvancedFade2DBurnNoiseStrength;          // float: noise strength
    float  _AdvancedFade2DBurnNoiseType;              // int: noise type
    float4 _AdvancedFade2DBurnDirection;              // float4: xy=dir
    float4 _AdvancedFade2DBurnEdgeColor;              // float4: rgba
    float  _AdvancedFade2DBurnBlendMode;              // int
    float  _AdvancedFade2DBurnInvert;                 // int
    float  _AdvancedFade2DBurnAnimatedNoiseEnabled;
    float  _AdvancedFade2DBurnAnimatedNoisePatternType;
    float4 _AdvancedFade2DBurnAnimatedNoiseDirection;
    float  _AdvancedFade2DBurnAnimatedNoiseSpeed;
    float4 _AdvancedFade2DBurnAnimatedNoiseOffset;
    float  _AdvancedFade2DBurnAnimatedNoiseRotationSpeed;
    float  _AdvancedFade2DBurnAnimatedNoisePulseAmplitude;
    float  _AdvancedFade2DBurnAnimatedNoisePulseSpeed;
    float  _AdvancedFade2DBurnAnimatedNoiseWarpPatternType;
    float  _AdvancedFade2DBurnAnimatedNoiseWarpScale;
    float  _AdvancedFade2DBurnAnimatedNoiseWarpStrength;
    float4 _AdvancedFade2DBurnAnimatedNoiseWarpDirection;
    float  _AdvancedFade2DBurnAnimatedNoiseWarpSpeed;
    float  _AdvancedFade2DBurnAnimatedNoiseLoopSeconds;
    float  _AdvancedFade2DBurnAnimatedNoiseOctaves;
    float  _AdvancedFade2DBurnAnimatedNoiseLacunarity;
    float  _AdvancedFade2DBurnAnimatedNoiseGain;
    float  _AdvancedFade2DBurnAnimatedNoiseCellSharpness;
    float  _AdvancedFade2DBurnAnimatedNoisePatternContrast;
CBUFFER_END

UNITY_INSTANCING_BUFFER_START(UnityPerDrawSpriteGame)
    UNITY_DEFINE_INSTANCED_PROP(float4, _RendererColor)
    UNITY_DEFINE_INSTANCED_PROP(float4, _Flip)
    UNITY_DEFINE_INSTANCED_PROP(float4, _SpriteUVRect)
    UNITY_DEFINE_INSTANCED_PROP(float4, _SpriteTexelSizeLocal)
UNITY_INSTANCING_BUFFER_END(UnityPerDrawSpriteGame)

#define _RendererColor        (UNITY_ACCESS_INSTANCED_PROP(UnityPerDrawSpriteGame, _RendererColor))
#define _Flip                 (UNITY_ACCESS_INSTANCED_PROP(UnityPerDrawSpriteGame, _Flip))
#define _SpriteUVRect         (UNITY_ACCESS_INSTANCED_PROP(UnityPerDrawSpriteGame, _SpriteUVRect))
#define _SpriteTexelSizeLocal (UNITY_ACCESS_INSTANCED_PROP(UnityPerDrawSpriteGame, _SpriteTexelSizeLocal).xy)

// Forward declarations (used by feature includes)
inline float2 AtlasUVToSpriteLocalUV(float2 atlasUV);
inline float2 SpriteLocalUVToAtlasUV(float2 uvLocal);

#include "Assets/GameLib/Script/Shader/Core/BaseShader/Features/Flash2D.hlsl"
#include "Assets/GameLib/Script/Shader/Core/BaseShader/Features/Pixelation2D.hlsl"
#include "Assets/GameLib/Script/Shader/Core/BaseShader/Features/AdvancedFlip2D.hlsl"
#include "Assets/GameLib/Script/Shader/Core/BaseShader/Features/TextureSlot2D.hlsl"
#include "Assets/GameLib/Script/Shader/Core/BaseShader/Features/Dissolve2D.hlsl"
#include "Assets/GameLib/Script/Shader/Core/BaseShader/Features/FlowWarp2D.hlsl"
#include "Assets/GameLib/Script/Shader/Core/BaseShader/Features/Mask2D.hlsl"
#include "Assets/GameLib/Script/Shader/Core/BaseShader/Features/Emission2D.hlsl"

// ═══════════════════════════════════════════════════════════════════════════
// v3.0 Features Include
// ═══════════════════════════════════════════════════════════════════════════
#include "Assets/GameLib/Script/Shader/Core/BaseShader/Features/ColorOverlay2D.hlsl"
#include "Assets/GameLib/Script/Shader/Core/BaseShader/Features/ColorRamp2D.hlsl"
#include "Assets/GameLib/Script/Shader/Core/BaseShader/Features/ExternalTextureComposite2D.hlsl"
#include "Assets/GameLib/Script/Shader/Core/BaseShader/Features/Refraction2D.hlsl"
#include "Assets/GameLib/Script/Shader/Core/BaseShader/Features/Caustics2D.hlsl"
#include "Assets/GameLib/Script/Shader/Core/BaseShader/Features/Ripple2D.hlsl"
#include "Assets/GameLib/Script/Shader/Core/BaseShader/Features/HueShift2D.hlsl"
#include "Assets/GameLib/Script/Shader/Core/BaseShader/Features/NormalMap2D.hlsl"
#include "Assets/GameLib/Script/Shader/Core/BaseShader/Features/BlendColor2D.hlsl"
#include "Assets/GameLib/Script/Shader/Core/BaseShader/Features/AdvancedFade2D.hlsl"
#include "Assets/GameLib/Script/Shader/Core/BaseShader/Features/Rainbow2D.hlsl"
#include "Assets/GameLib/Script/Shader/Core/BaseShader/Features/TextFx2D.hlsl"
#include "Assets/GameLib/Script/Shader/Core/BaseShader/Features/Outline2D.hlsl"

// ═══════════════════════════════════════════════════════════════════════════
// Transition System v1.0 Feature Include
// ═══════════════════════════════════════════════════════════════════════════
#include "Assets/GameLib/Script/Shader/Core/BaseShader/Features/Transition2D.hlsl"

// ============================================================================
// Surface2DContext - 全機能パラメータを集約
// ============================================================================
struct Surface2DContext
{
    Pixelation2DParams     pixel;
    Flash2DParams          flash;
    AdvancedFlip2DParams   advFlip;   // 頂点変形用
    
    // Composite System (BaseShader-CompositeSystem-v2.0)
    Dissolve2DParams       dissolve;
    FlowWarp2DParams       flowWarp;
    Mask2DParams           mask;
    Emission2DParams       emission;
    
    // Composite System (BaseShader-CompositeSystem-v3.0)
    ColorOverlay2DParams   colorOverlay;
    ColorRamp2DParams      colorRamp;
    ExternalTextureComposite2DParams externalTextureComposite;
    Refraction2DParams     refraction;
    Caustics2DParams       caustics;
    Ripple2DParams         ripple;
    HueShift2DParams       hueShift;
    NormalMap2DParams      normalMap;
    // Additional color/alpha features
    BlendColor2DParams     blendColor;
    AdvancedFade2DParams   advancedFade;
    TextFx2DParams         textFx;
    Outline2DParams        outline;
    Rainbow2DParams        rainbow;
    
    // Transition System (v1.0)
    Transition2DParams     transition;
};

// ----------------------------------------------------------------------------
// AdvancedFlip2D パラメータ生成（CBUFFER から取得）
// ----------------------------------------------------------------------------
inline AdvancedFlip2DParams MakeAdvancedFlip2DParams()
{
    AdvancedFlip2DParams p;
    p.enabled         = _AdvFlipEnabled;
    p.eulerDegX       = _AdvFlipEulerDegX;
    p.eulerDegY       = _AdvFlipEulerDegY;
    p.eulerDegZ       = _AdvFlipEulerDegZ;
    p.pivotLocal      = _AdvFlipPivotLocal.xy;
    p.perspective     = _AdvFlipPerspective;
    p.depthScale      = _AdvFlipDepthScale;
    p.perspSign       = _AdvFlipPerspectiveSign;
    p.scaleClampMin   = _AdvFlipScaleClamp.x;
    p.scaleClampMax   = _AdvFlipScaleClamp.y;
    p.shear           = _AdvWarpShear.xy;
    p.bend            = _AdvWarpBend.xy;
    p.fallbackHalfSize = _AdvFlipFallbackHalfSize.xy;
    return p;
}

inline Surface2DContext MakeSurface2DContext()
{
    Surface2DContext ctx;
    ctx.pixel   = MakePixelation2DParams();
    ctx.flash   = MakeFlash2DParams();
    ctx.advFlip = MakeAdvancedFlip2DParams();
    
    // Composite System パラメータの初期化 (v2.0)
    ctx.dissolve = MakeDissolve2DParams(
        _DissolveEnabled,
        _DissolveSource_SlotType,
        _DissolveSource_Channel,
        _DissolveSource_UVSpace,
        _DissolveSource_TilingOffset,
        _DissolveSource_Remap,
        _DissolveThreshold,
        _DissolveEdgeWidth,
        _DissolveEdgeColor
    );
    
    ctx.flowWarp = MakeFlowWarp2DParams(
        _FlowWarpEnabled,
        _FlowWarpSource_SlotType,
        _FlowWarpSource_Channel,
        _FlowWarpSource_UVSpace,
        _FlowWarpSource_TilingOffset,
        _FlowWarpSource_Remap,
        _FlowWarpStrength.xy,
        _FlowWarpSpeed
    );
    
    ctx.mask = MakeMask2DParams(
        _MaskEnabled,
        _MaskSource_SlotType,
        _MaskSource_Channel,
        _MaskSource_UVSpace,
        _MaskSource_TilingOffset,
        _MaskSource_Remap,
        _MaskThreshold,
        _MaskSoftness
    );
    
    ctx.emission = MakeEmission2DParams(
        _EmissionEnabled,
        _EmissionSource_SlotType,
        _EmissionSource_Channel,
        _EmissionSource_UVSpace,
        _EmissionSource_TilingOffset,
        _EmissionSource_Remap,
        _EmissionColor
    );
    
    // ═══════════════════════════════════════════════════════════════════════════
    // Composite System v3.0 パラメータ初期化
    // ═══════════════════════════════════════════════════════════════════════════
    
    // ★ 修正: BaseShader.shader のプロパティ名と一致させた
    ctx.colorOverlay = MakeColorOverlay2DParamsSimple(
        _ColorOverlayEnabled,
        _ColorOverlaySource_SlotType,
        _ColorOverlaySource_Channel,
        _ColorOverlaySource_UVSpace,
        _ColorOverlaySource_TilingOffset,
        _ColorOverlaySource_Remap,
        _ColorOverlayColor,
        _ColorOverlayBlendMode,
        _ColorOverlayIntensity
    );
    
    ctx.colorRamp = MakeColorRamp2DParamsSimple(
        _ColorRampEnabled,
        _ColorRampSource_SlotType,
        _ColorRampSource_Channel,
        _ColorRampSource_UVSpace,
        _ColorRampSource_TilingOffset,
        _ColorRampSource_Remap,
        _ColorRampIntensity,
        _ColorRampPreserveAlpha
    );

    ctx.externalTextureComposite = MakeExternalTextureComposite2DParams(
        _ExternalTextureCompositeEnabled,
        _ExternalTextureCompositeSource_SlotType,
        _ExternalTextureCompositeSource_Channel,
        _ExternalTextureCompositeSource_UVSpace,
        _ExternalTextureCompositeSource_TilingOffset,
        _ExternalTextureCompositeSource_Remap,
        _ExternalTextureCompositeBlendMode,
        _ExternalTextureCompositeIntensity,
        _ExternalTextureCompositeUseTextureAlpha,
        _ExternalTextureCompositeTint,
        _ExternalTextureCompositeDisableWhenTextureMissing,
        _ExternalTextureCompositeAffectSurfaceAlpha
    );
    
    ctx.refraction = MakeRefraction2DParamsSimple(
        _RefractionEnabled,
        _RefractionSource_SlotType,
        _RefractionSource_Channel,
        _RefractionSource_UVSpace,
        _RefractionSource_TilingOffset,
        _RefractionSource_Remap,
        _RefractionStrength.xy,
        _RefractionChromaticAberration
    );
    
    ctx.caustics = MakeCaustics2DParamsSimple(
        _CausticsEnabled,
        _CausticsSourceA_SlotType,
        _CausticsSourceA_Channel,
        _CausticsSourceA_UVSpace,
        _CausticsSourceA_TilingOffset,
        _CausticsSourceA_Remap,
        _CausticsSourceB_SlotType,
        _CausticsSourceB_Channel,
        _CausticsSourceB_UVSpace,
        _CausticsSourceB_TilingOffset,
        _CausticsSourceB_Remap,
        _CausticsColor,
        _CausticsIntensity,
        _CausticsThreshold,
        _CausticsSoftness,
        _CausticsScrollA.xy,
        _CausticsScrollB.xy
    );
    
    ctx.ripple = MakeRipple2DParamsSimple(
        _RippleEnabled,
        _RippleCenter.xy,
        _RippleWaveParams,   // x=freq, y=speed, z=decay
        _RippleAmplitude,
        _RipplePhase,
        _RippleDistortUV,
        _RippleColorBlend,
        _RippleColor
    );
    
    ctx.hueShift = MakeHueShift2DParamsSimple(
        _HueShiftEnabled,
        _HueShiftMaskSource_SlotType,
        _HueShiftMaskSource_Channel,
        _HueShiftMaskSource_UVSpace,
        _HueShiftMaskSource_TilingOffset,
        _HueShiftMaskSource_Remap,
        _HueShiftAmount,
        _HueSaturationMod,
        _HueValueMod
    );
    
    ctx.normalMap = MakeNormalMap2DParamsSimple(
        _NormalMapEnabled,
        _NormalMapSource_SlotType,
        _NormalMapSource_Channel,
        _NormalMapSource_UVSpace,
        _NormalMapSource_TilingOffset,
        _NormalMapSource_Remap,
        _NormalMapStrength,
        _NormalMapLightDir.xyz,
        _NormalMapBlendWithBase
    );

    // BlendColor2D パラメータ
    ctx.blendColor = MakeBlendColor2DParams(
        _BlendColor2DEnabled,
        _BlendColor2DColor, // BlendColor2D owns its own color now (no ColorOverlay dependency)
        _BlendColor2DBlendIntensity,
        _BlendColor2DBlendGradDirection,
        _BlendColor2DBlendGradationAmount,
        _BlendColor2DBlendSoftness,
        _BlendColor2DBlendMode,
        _BlendColor2DAnimatedGradientEnabled,
        _BlendColor2DAnimatedGradientPatternType,
        _BlendColor2DAnimatedGradientMasterStrength,
        _BlendColor2DAnimatedGradientNoiseScale,
        _BlendColor2DAnimatedGradientNoiseDirection.xy,
        _BlendColor2DAnimatedGradientNoiseSpeed,
        _BlendColor2DAnimatedGradientNoiseOffset.xy,
        _BlendColor2DAnimatedGradientRotationSpeed,
        _BlendColor2DAnimatedGradientPulseAmplitude,
        _BlendColor2DAnimatedGradientPulseSpeed,
        _BlendColor2DAnimatedGradientWarpPatternType,
        _BlendColor2DAnimatedGradientWarpScale,
        _BlendColor2DAnimatedGradientWarpStrength,
        _BlendColor2DAnimatedGradientWarpDirection.xy,
        _BlendColor2DAnimatedGradientWarpSpeed,
        _BlendColor2DAnimatedGradientLoopSeconds,
        _BlendColor2DAnimatedGradientOctaves,
        _BlendColor2DAnimatedGradientLacunarity,
        _BlendColor2DAnimatedGradientGain,
        _BlendColor2DAnimatedGradientCellSharpness,
        _BlendColor2DAnimatedGradientPatternContrast,
        _BlendColor2DAnimatedGradientHueAmplitude,
        _BlendColor2DAnimatedGradientSaturationAmplitude,
        _BlendColor2DAnimatedGradientLightnessAmplitude
    );

    // AdvancedFade2D パラメータ
    ctx.advancedFade = MakeAdvancedFade2DParams(
        _AdvancedFade2DEnabled,
        _AdvancedFade2DFadeDirection,
        _AdvancedFade2DFadeAmount,
        _AdvancedFade2DSoft,
        _AdvancedFade2DGlowIntensity,
        _AdvancedFade2DGlowRange,
        _AdvancedFade2DGlowBlendMode,
        _AdvancedFade2DWaveParamsA,
        _AdvancedFade2DWaveParamsB,
        _AdvancedFade2DCircleStartAngleDeg,
        _AdvancedFade2DCircleClockwise,
        _AdvancedFade2DBurnEnabled,
        _AdvancedFade2DBurnProgress,
        _AdvancedFade2DBurnEdgeWidth,
        _AdvancedFade2DBurnNoiseScale,
        _AdvancedFade2DBurnNoiseStrength,
        _AdvancedFade2DBurnNoiseType,
        _AdvancedFade2DBurnDirection.xy,
        _AdvancedFade2DBurnEdgeColor,
        _AdvancedFade2DBurnBlendMode,
        _AdvancedFade2DBurnInvert,
        _AdvancedFade2DBurnAnimatedNoiseEnabled,
        _AdvancedFade2DBurnAnimatedNoisePatternType,
        _AdvancedFade2DBurnAnimatedNoiseDirection.xy,
        _AdvancedFade2DBurnAnimatedNoiseSpeed,
        _AdvancedFade2DBurnAnimatedNoiseOffset.xy,
        _AdvancedFade2DBurnAnimatedNoiseRotationSpeed,
        _AdvancedFade2DBurnAnimatedNoisePulseAmplitude,
        _AdvancedFade2DBurnAnimatedNoisePulseSpeed,
        _AdvancedFade2DBurnAnimatedNoiseWarpPatternType,
        _AdvancedFade2DBurnAnimatedNoiseWarpScale,
        _AdvancedFade2DBurnAnimatedNoiseWarpStrength,
        _AdvancedFade2DBurnAnimatedNoiseWarpDirection.xy,
        _AdvancedFade2DBurnAnimatedNoiseWarpSpeed,
        _AdvancedFade2DBurnAnimatedNoiseLoopSeconds,
        _AdvancedFade2DBurnAnimatedNoiseOctaves,
        _AdvancedFade2DBurnAnimatedNoiseLacunarity,
        _AdvancedFade2DBurnAnimatedNoiseGain,
        _AdvancedFade2DBurnAnimatedNoiseCellSharpness,
        _AdvancedFade2DBurnAnimatedNoisePatternContrast
    );

    ctx.textFx = MakeTextFx2DParams(
        _TextOutlineEnabled,
        _TextOutlineColor,
        _TextOutlineThickness,
        _TextOutlineSoftness,
        _TextOutlineDirectionMask,
        _TextOutlineAutoColorEnabled,
        _TextOutlineAutoColorMode,
        _TextOutlineAutoHue,
        _TextOutlineAutoSaturation,
        _TextOutlineAutoLightness,
        _TextShadowEnabled,
        _TextShadowColor,
        _TextShadowOffset.xy,
        _TextShadowSoftness,
        _TextGlowEnabled,
        _TextGlowColor,
        _TextGlowThickness,
        _TextGlowSoftness
    );

    ctx.outline = MakeOutline2DParams(
        _OutlineEnabled,
        _OutlineMode,
        _OutlineColor,
        _OutlineDirectionMask,
        _OutlineAutoColorEnabled,
        _OutlineAutoColorMode,
        _OutlineAutoHue,
        _OutlineAutoSaturation,
        _OutlineAutoLightness,
        _OutlineAnimatedGradientEnabled,
        _OutlineAnimatedGradientPatternType,
        _OutlineAnimatedGradientMasterStrength,
        _OutlineAnimatedGradientNoiseScale,
        _OutlineAnimatedGradientNoiseDirection.xy,
        _OutlineAnimatedGradientNoiseSpeed,
        _OutlineAnimatedGradientNoiseOffset.xy,
        _OutlineAnimatedGradientRotationSpeed,
        _OutlineAnimatedGradientPulseAmplitude,
        _OutlineAnimatedGradientPulseSpeed,
        _OutlineAnimatedGradientWarpPatternType,
        _OutlineAnimatedGradientWarpScale,
        _OutlineAnimatedGradientWarpStrength,
        _OutlineAnimatedGradientWarpDirection.xy,
        _OutlineAnimatedGradientWarpSpeed,
        _OutlineAnimatedGradientLoopSeconds,
        _OutlineAnimatedGradientOctaves,
        _OutlineAnimatedGradientLacunarity,
        _OutlineAnimatedGradientGain,
        _OutlineAnimatedGradientCellSharpness,
        _OutlineAnimatedGradientPatternContrast,
        _OutlineAnimatedGradientHueAmplitude,
        _OutlineAnimatedGradientSaturationAmplitude,
        _OutlineAnimatedGradientLightnessAmplitude,
        _OutlineWidth,
        _OutlineOpacity,
        _OutlineSoftness,
        _OutlineBlendMode,
        _OutlinePixelPerfect,
        _OutlineWidthUnit,
        _OutlinePixelStep,
        _OutlineSamplePattern,
        _OutlineMaskRespect,
        _OutlineUseVertexColor,
        _OutlineUVClampEnabled,
        _OutlineZTestMode
    );

    ctx.rainbow = MakeRainbow2DParams(
        _Rainbow2DEnabled,
        _Rainbow2DMode,
        _Rainbow2DPattern,
        _Rainbow2DDirection.xy,
        _Rainbow2DScale,
        _Rainbow2DOffset,
        _Rainbow2DSpeed,
        _Rainbow2DPixelSize,
        _Rainbow2DIntensity,
        _Rainbow2DBlendMode
    );
    
    // ═══════════════════════════════════════════════════════════════════════════
    // Transition System v1.0 パラメータ初期化
    // ═══════════════════════════════════════════════════════════════════════════
    ctx.transition = MakeTransition2DParams(
        _TransitionEnabled,
        _TransitionBlendMode,
        _TransitionProgress,
        _TransitionParams,
        _TransitionFromSpriteUVRect
    );

#if defined(SURFACE2D_WEBGL_SAFE)
    // WebGL 段階的復旧:
    // TextureSlot 依存が強い機能のみ停止する。
    // TextFx / Outline は WebGL 専用の簡易実装を通す。
    ctx.flowWarp.enabled = 0;
    ctx.emission.enabled = 0;
    ctx.colorOverlay.enabled = 0;
    ctx.colorRamp.enabled = 0;
    ctx.externalTextureComposite.enabled = 0;
    ctx.refraction.enabled = 0;
    ctx.caustics.enabled = 0;
    ctx.ripple.distortEnabled = 0;
    ctx.hueShift.enabled = 0;
    ctx.normalMap.enabled = 0;
    ctx.pixel.enabled = 0;
#endif
    
    return ctx;
}

inline Surface2D MakeSurface2D(
    float3 color,
    float  alpha,
    float  baseAlphaRaw,
    float2 uvMain,
    float2 uvLocal,
    float2 screenUV,
    float2 fadeUV,
    float  vertexAlpha)
{
    Surface2D s;
    s.color        = color;
    s.alpha        = alpha;
    s.baseAlphaRaw = baseAlphaRaw;
    s.vertexAlpha  = vertexAlpha;
    s.alphaFactor  = 1.0;
    s.uvMain       = uvMain;
    s.uvLocal      = uvLocal;
    s.screenUV     = screenUV;
    s.fadeUV       = fadeUV;
    s.uv           = uvMain; // set legacy alias
    return s;
}

// ---------------------------------------------------------------------------
// UV 変換ユーティリティ
// ---------------------------------------------------------------------------

/// <summary>
/// アトラス UV からスプライトローカル UV (0..1) に変換。
/// _SpriteUVRect = (minU, minV, maxU, maxV) を使用。
/// </summary>
inline float2 AtlasUVToSpriteLocalUV(float2 atlasUV)
{
    // 優先順位:
    //   1) _SpriteUVRect が「サブ矩形」を示しているならそれを採用（SpriteAtlas / SpriteRenderer 互換）
    //   2) そうでなければ _MainTex_ST を「フレーム矩形の scale/offset」とみなして逆変換
    //      （スプライトシートを _MainTex_ST で切り替えるアニメーションの安定化）
    //   3) どちらも無ければそのまま

    float4 rect = _SpriteUVRect;
    float2 rectMin = rect.xy;
    float2 rectMax = rect.zw;
    float2 rectSize = rectMax - rectMin;

    // _SpriteUVRect が (0,0,1,1) に近い場合は「分割情報なし」とみなす
    bool hasSpriteRect =
        (abs(rectMin.x) > 1e-5) || (abs(rectMin.y) > 1e-5) ||
        (abs(rectMax.x - 1.0) > 1e-5) || (abs(rectMax.y - 1.0) > 1e-5);

    // rectSize が極小なら「未設定」とみなして無効化（TMP 等の未設定を回避）
    if (rectSize.x <= 1e-5 || rectSize.y <= 1e-5)
        hasSpriteRect = false;

    if (hasSpriteRect)
    {
        rectSize = max(rectSize, float2(1e-6, 1e-6));
        return (atlasUV - rectMin) / rectSize;
    }

    // _MainTex_ST が identity でないなら「フレーム UV 変換」とみなし、0..1 に正規化する
    float2 stScale  = _MainTex_ST.xy;
    float2 stOffset = _MainTex_ST.zw;
    bool hasST =
        (abs(stScale.x - 1.0) > 1e-5) || (abs(stScale.y - 1.0) > 1e-5) ||
        (abs(stOffset.x) > 1e-5) || (abs(stOffset.y) > 1e-5);

    // scale が極小なら無効化（TMP の未設定 ST 対策）
    if (stScale.x <= 1e-5 || stScale.y <= 1e-5)
        hasST = false;

    if (hasST)
    {
        stScale = max(stScale, float2(1e-6, 1e-6));
        return (atlasUV - stOffset) / stScale;
    }

    return atlasUV;
}

/// <summary>
/// スプライトローカル UV (0..1) からアトラス UV へ変換。
/// Pixelation のように「ローカル空間でスナップ → アトラスでサンプル」に必要。
/// </summary>
inline float2 SpriteLocalUVToAtlasUV(float2 uvLocal)
{
    float4 rect = _SpriteUVRect;
    float2 rectMin = rect.xy;
    float2 rectMax = rect.zw;
    float2 rectSize = rectMax - rectMin;

    bool hasSpriteRect =
        (abs(rectMin.x) > 1e-5) || (abs(rectMin.y) > 1e-5) ||
        (abs(rectMax.x - 1.0) > 1e-5) || (abs(rectMax.y - 1.0) > 1e-5);

    // rectSize が極小なら「未設定」とみなして無効化（TMP 等の未設定を回避）
    if (rectSize.x <= 1e-5 || rectSize.y <= 1e-5)
        hasSpriteRect = false;

    if (hasSpriteRect)
    {
        return rectMin + uvLocal * rectSize;
    }

    float2 stScale  = _MainTex_ST.xy;
    float2 stOffset = _MainTex_ST.zw;
    bool hasST =
        (abs(stScale.x - 1.0) > 1e-5) || (abs(stScale.y - 1.0) > 1e-5) ||
        (abs(stOffset.x) > 1e-5) || (abs(stOffset.y) > 1e-5);

    // scale が極小なら無効化（TMP の未設定 ST 対策）
    if (stScale.x <= 1e-5 || stScale.y <= 1e-5)
        hasST = false;

    if (hasST)
    {
        return uvLocal * stScale + stOffset;
    }

    return uvLocal;
}

// ============================================================================
// 頂点変形入口（Vertex Stage）
// ============================================================================

// SpriteRenderer 用：UVRect と TexelSizeLocal からローカルサイズを推定
inline float2 Surface2D_EstimateHalfSizeLocal()
{
    // uvSize = UV空間でのスプライト範囲
    float2 uvSize = (_SpriteUVRect.zw - _SpriteUVRect.xy);
    // texWH = テクスチャの実ピクセルサイズ (width, height)
    float2 texWH  = _MainTex_TexelSize.zw;
    // pxSize = スプライトのピクセルサイズ
    float2 pxSize = uvSize * texWH;
    // localSize = ローカル座標系でのサイズ
    float2 localSize = pxSize * _SpriteTexelSizeLocal;
    // 半分のサイズを返す（最小値でゼロ除算防止）
    return max(localSize * 0.5, float2(1e-4, 1e-4));
}

// 頂点位置のみを加工して返す（Varyings は各 Shader の責務）
// posOS: オブジェクト空間位置
// uv: テクスチャ座標（将来的にUVベースの変形用に予約）
// ctx: Surface2DContext（advFlip パラメータを含む）
inline float3 Surface2D_Vertex_ApplyPositionOS(float3 posOS, float2 uv, Surface2DContext ctx)
{
    // AdvancedFlip が無効なら素通し
    if (ctx.advFlip.enabled <= 0.5)
        return posOS;
    
    // halfSize を推定
    float2 halfSize = Surface2D_EstimateHalfSizeLocal();
    
    // AdvancedFlip2D 変形を適用
    return AdvancedFlip2D_Apply(posOS, halfSize, ctx.advFlip);
}

// デフォルトパイプライン（マクロで差し替え可能）
// ═══════════════════════════════════════════════════════════════════════════
// BaseShader-CompositeSystem-v3.0 + TransitionSystem-v1.0 準拠
// 処理順序:
//   Phase 1: UV Modification (FlowWarp, Refraction, Ripple歪み)
//   Phase 2: Color Composition (ColorOverlay, ColorRamp, Caustics, HueShift)
//   Phase 3: Alpha/Visibility (Mask, Dissolve)
//   Phase 3.5: Text Shadow/Glow
//   Phase 4: Transition (CrossFade, Dissolve, Wipe - 外部テクスチャとのブレンド)
//   Phase 5: Additive Effects (Emission, Ripple色, Flash)
//   Phase 6: Pixelation
//   Phase 7: Final Text Outline
// ═══════════════════════════════════════════════════════════════════════════
#ifndef SURFACE2D_PIPELINE
#define SURFACE2D_PIPELINE(surface, ctx)                                       \
    {                                                                          \
        /* Phase 2: Color Composition (v3.0) */                                \
        surface = Surface2D_ApplyColorOverlay(surface, (ctx).colorOverlay);    \
        surface = Surface2D_ApplyBlendColor(surface, (ctx).blendColor);        \
        surface = Surface2D_ApplyColorRamp(surface, (ctx).colorRamp);          \
        surface = Surface2D_ApplyExternalTextureComposite(surface, (ctx).externalTextureComposite); \
        surface = Surface2D_ApplyCaustics(surface, (ctx).caustics, _Time.y);   \
        surface = Surface2D_ApplyHueShift(surface, (ctx).hueShift, _Time.y);   \
        surface = Surface2D_ApplyRainbow(surface, (ctx).rainbow, _Time.y);     \
                                                                                \
        /* Phase 3: Alpha/Visibility (v2.0) */                                 \
        surface = Surface2D_ApplyMask(surface, (ctx).mask);                    \
        surface = Surface2D_ApplyDissolve(surface, (ctx).dissolve);            \
        surface = Surface2D_ApplyAdvancedFade(surface, (ctx).advancedFade, _Time.y); \
                                                                                \
        /* Phase 3.2: Text Shadow / Glow */                                    \
        surface = Surface2D_ApplyTextFxPrepass(surface, (ctx).textFx);         \
                                                                                \
        /* Phase 3.3: Transition (v1.0) */                                     \
        surface = Surface2D_ApplyTransition(surface, (ctx).transition);        \
                                                                                \
        /* Phase 4: Additive Effects (v2.0 + v3.0) */                          \
        surface = Surface2D_ApplyEmission(surface, (ctx).emission);            \
        surface = Surface2D_ApplyRipple(surface, (ctx).ripple);                \
        surface = Surface2D_ApplyFlash(surface, (ctx).flash);                  \
                                                                                \
        /* Phase 5: Pixelation (v2.0) */                                       \
        surface = Surface2D_ApplyPixelation(surface, (ctx).pixel);             \
                                                                                \
        /* Phase 6: Final Outlines */                                          \
        surface = Surface2D_ApplyOutline(surface, (ctx).outline);              \
        surface = Surface2D_ApplyTextOutlineFx(surface, (ctx).textFx);         \
    }
#endif

// screenUVは画面のgizmo用に残している
// FlowWarp, Refraction, Ripple も UV サンプリング前に適用
inline float2 Surface2D_BeforeSample_Apply(float2 uv, float2 screenUV, Surface2DContext ctx)
{
    Pixelation2DParams pixelP = ctx.pixel;
    if (pixelP.enabled > 0.5f && (pixelP.mode == 1 || pixelP.mode == 2))
    {
        if (pixelP.mode == 1)
            uv = Pixelation2D_ApplyUV_Screen(uv, screenUV, pixelP.blockScreenSize);
        else
            uv = Pixelation2D_ApplyUV_Texel_SpriteLocal(uv, pixelP.blockScreenSize);
    }

    // スプライトローカル UV を事前計算（複数機能で使用）
    float2 uvLocal = AtlasUVToSpriteLocalUV(uv);
    
    // FlowWarp: UV 歪みを適用 (v2.0)
    // uv = アトラス UV を直接歪ませる（変換往復を削減）
    // UVSpace で使う座標は ComputeSlotUV 内で選択される
    if (ctx.flowWarp.enabled > 0.5f)
    {
        // targetUV=uv(アトラスUV), uvLocal, uvMain=uv, screenUV
        uv = FlowWarp2D_WarpUV(uv, uvLocal, uv, screenUV, ctx.flowWarp);
    }
    
    // Refraction: 勾配による UV 歪み (v3.0)
    // 注意: Surface2D が必要なため、ここでは簡易版を使用
    // 完全版は Refraction2D_SampleWithChromatic を使用
    if (ctx.refraction.enabled > 0.5f)
    {
        uv = Refraction2D_WarpUV(uv, uvLocal, screenUV, ctx.refraction, _Time.y);
    }
    
    // Ripple: 波紋による UV 歪み (v3.0)
    // ★ 修正: 時間パラメータを追加
    if (ctx.ripple.enabled > 0.5f && ctx.ripple.distortEnabled > 0)
    {
        uv = Ripple2D_WarpUV(uv, uvLocal, ctx.ripple, _Time.y);
    }

    return uv;
}

inline float4 Surface2D_AfterSample_Apply(
    float4 baseColor,
    float  baseAlphaRaw,
    float2 uv,
    float2 screenUV,
    Surface2DContext ctx,
    float  vertexAlpha)
{
    // アトラス UV からスプライトローカル UV を計算
    float2 uvLocal = AtlasUVToSpriteLocalUV(uv);
    
    Surface2D s = MakeSurface2D(
        baseColor.rgb,
        baseColor.a,
        baseAlphaRaw,
        uv,         // uvMain (アトラス UV)
        uvLocal,    // uvLocal (スプライトローカル 0..1)
        screenUV,
        uv,
        vertexAlpha);

    SURFACE2D_PIPELINE(s, ctx);

    return float4(s.color, s.alpha);
}

#endif // GAME_SURFACE2D_INCLUDED
