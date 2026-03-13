#ifndef GAME_COLOR_RAMP_2D_INCLUDED
#define GAME_COLOR_RAMP_2D_INCLUDED

// ═══════════════════════════════════════════════════════════════════════════
// ColorRamp2D.hlsl - カラーランプ/グラデーションマップエフェクト (v3.0)
// ═══════════════════════════════════════════════════════════════════════════
//
// 仕様書 BaseShader-CompositeSystem-v3.0 準拠
//
// 機能:
//   - ノイズ値を1Dグラデーションテクスチャの UV として使用
//   - 炎、マグマ、オーラ、虹色などの複雑な色変化を実現
//   - ColorOverlay と同じブレンドモードをサポート
//
// ═══════════════════════════════════════════════════════════════════════════

// TextureSlot2D.hlsl は Surface2D.hlsl 経由でインクルード想定
#include "Assets/GameLib/Script/Shader/Core/BaseShader/Features/BlendModes.hlsl"

// ---------------------------------------------------------------------------
// Alpha Mode 定義 (C# の ColorRampAlphaMode と同期)
// ---------------------------------------------------------------------------
#define ALPHA_MODE_PRESERVE    0  // ベースのアルファを維持
#define ALPHA_MODE_FROM_RAMP   1  // ランプのアルファを使用
#define ALPHA_MODE_FROM_NOISE  2  // ノイズ値をアルファとして使用

// ---------------------------------------------------------------------------
// ColorRamp テクスチャ宣言 (256x1 推奨)
// ★ BaseShader.shader: _ColorRampTex
// ---------------------------------------------------------------------------
TEXTURE2D(_ColorRampTex);
SAMPLER(sampler_ColorRampTex);

// ---------------------------------------------------------------------------
// ColorRamp パラメータ構造体
// ---------------------------------------------------------------------------
struct ColorRamp2DParams
{
    float          enabled;
    TextureSlotRef source;        // ノイズソース
    int            blendMode;     // BLEND_MODE_*
    half           opacity;       // 全体の不透明度
    int            alphaMode;     // ALPHA_MODE_*
};

// ---------------------------------------------------------------------------
// CBUFFER から ColorRamp2DParams を生成
// ---------------------------------------------------------------------------
inline ColorRamp2DParams MakeColorRamp2DParams(
    float enabled,
    float sourceSlotType,
    float sourceChannel,
    float sourceUVSpace,
    float4 sourceTilingOffset,
    float4 sourceRemap,
    float blendMode,
    float opacity,
    float alphaMode)
{
    ColorRamp2DParams p = (ColorRamp2DParams)0;
    p.enabled = enabled;
    p.source = MakeTextureSlotRef(
        sourceSlotType,
        sourceChannel,
        sourceUVSpace,
        sourceTilingOffset,
        sourceRemap
    );
    p.blendMode = (int)round(blendMode);
    p.opacity = saturate(opacity);
    p.alphaMode = (int)round(alphaMode);
    return p;
}

// ---------------------------------------------------------------------------
// ★ Simplified: BaseShader.shader の実際のプロパティに合わせた簡易版
// ---------------------------------------------------------------------------
inline ColorRamp2DParams MakeColorRamp2DParamsSimple(
    float enabled,
    float sourceSlotType,
    float sourceChannel,
    float sourceUVSpace,
    float4 sourceTilingOffset,
    float4 sourceRemap,
    float intensity,         // opacity として使用
    float preserveAlpha)     // ALPHA_MODE_PRESERVE or ALPHA_MODE_FROM_RAMP
{
    ColorRamp2DParams p = (ColorRamp2DParams)0;
    p.enabled = enabled;
    p.source = MakeTextureSlotRef(
        sourceSlotType,
        sourceChannel,
        sourceUVSpace,
        sourceTilingOffset,
        sourceRemap
    );
    p.blendMode = BLEND_MODE_NORMAL;  // デフォルトはNormal
    p.opacity = saturate(intensity);
    p.alphaMode = (preserveAlpha > 0.5h) ? ALPHA_MODE_PRESERVE : ALPHA_MODE_FROM_RAMP;
    return p;
}

// デフォルト値で初期化（無効状態）
inline ColorRamp2DParams MakeDefaultColorRamp2DParams()
{
    ColorRamp2DParams p = (ColorRamp2DParams)0;
    p.enabled = 0;
    p.source = MakeDefaultTextureSlotRef();
    p.blendMode = BLEND_MODE_NORMAL;
    p.opacity = 1.0;
    p.alphaMode = ALPHA_MODE_PRESERVE;
    return p;
}

// ---------------------------------------------------------------------------
// ColorRamp 適用
// ---------------------------------------------------------------------------
inline Surface2D Surface2D_ApplyColorRamp(Surface2D s, ColorRamp2DParams p)
{
    if (p.enabled < 0.5h)
        return s;
    if (p.source.slotType == TEXTURE_SLOT_NONE)
        return s;
    
    // ソースからノイズ値を取得 (0-1)
    half noise = SampleSlotScalar(s, p.source);
    
    // ランプテクスチャをサンプリング (noise を U 座標として使用)
    // ★ テクスチャ名を _ColorRampTex に変更
    float2 rampUV = float2(saturate(noise), 0.5);
    half4 rampColor = SAMPLE_TEXTURE2D(_ColorRampTex, sampler_ColorRampTex, rampUV);
    
    // ブレンド
    half3 blended = s.color;
    half finalOpacity = p.opacity;
    
    [branch]
    switch (p.blendMode)
    {
        case BLEND_MODE_NORMAL:
            blended = BlendNormal(s.color, rampColor.rgb, finalOpacity);
            break;
        case BLEND_MODE_MULTIPLY:
            blended = BlendMultiply(s.color, rampColor.rgb, finalOpacity);
            break;
        case BLEND_MODE_ADD:
            blended = BlendAdd(s.color, rampColor.rgb, finalOpacity);
            break;
        case BLEND_MODE_SCREEN:
            blended = BlendScreen(s.color, rampColor.rgb, finalOpacity);
            break;
        case BLEND_MODE_OVERLAY:
            blended = BlendOverlay(s.color, rampColor.rgb, finalOpacity);
            break;
        case BLEND_MODE_SOFTLIGHT:
            blended = BlendSoftLight(s.color, rampColor.rgb, finalOpacity);
            break;
        case BLEND_MODE_HARDLIGHT:
            blended = BlendHardLight(s.color, rampColor.rgb, finalOpacity);
            break;
        case BLEND_MODE_COLOR_BURN:
            blended = BlendColorBurn(s.color, rampColor.rgb, finalOpacity);
            break;
        case BLEND_MODE_COLOR_DODGE:
            blended = BlendColorDodge(s.color, rampColor.rgb, finalOpacity);
            break;
        case BLEND_MODE_DARKEN:
            blended = BlendDarken(s.color, rampColor.rgb, finalOpacity);
            break;
        case BLEND_MODE_LIGHTEN:
            blended = BlendLighten(s.color, rampColor.rgb, finalOpacity);
            break;
        case BLEND_MODE_DIFFERENCE:
            blended = BlendDifference(s.color, rampColor.rgb, finalOpacity);
            break;
        case BLEND_MODE_EXCLUSION:
            blended = BlendExclusion(s.color, rampColor.rgb, finalOpacity);
            break;
        case BLEND_MODE_HUE:
            blended = BlendHue(s.color, rampColor.rgb, finalOpacity);
            break;
        case BLEND_MODE_SATURATION:
            blended = BlendSaturation(s.color, rampColor.rgb, finalOpacity);
            break;
        case BLEND_MODE_COLOR:
            blended = BlendColor(s.color, rampColor.rgb, finalOpacity);
            break;
        case BLEND_MODE_LUMINOSITY:
            blended = BlendLuminosity(s.color, rampColor.rgb, finalOpacity);
            break;
        default:
            blended = s.color;
            break;
    }
    
    s.color = blended;
    
    // アルファモード処理
    [branch]
    switch (p.alphaMode)
    {
        case ALPHA_MODE_FROM_RAMP:
            s.alpha = lerp(s.alpha, rampColor.a, finalOpacity);
            break;
        case ALPHA_MODE_FROM_NOISE:
            s.alpha = lerp(s.alpha, noise, finalOpacity);
            break;
        // ALPHA_MODE_PRESERVE: 何もしない
    }
    
    return s;
}

#endif // GAME_COLOR_RAMP_2D_INCLUDED
