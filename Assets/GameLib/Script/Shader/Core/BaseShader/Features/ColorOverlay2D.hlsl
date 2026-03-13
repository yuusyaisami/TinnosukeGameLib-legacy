#ifndef GAME_COLOR_OVERLAY_2D_INCLUDED
#define GAME_COLOR_OVERLAY_2D_INCLUDED

// ═══════════════════════════════════════════════════════════════════════════
// ColorOverlay2D.hlsl - 色オーバーレイエフェクト (v3.0)
// ═══════════════════════════════════════════════════════════════════════════
//
// 仕様書 BaseShader-CompositeSystem-v3.0 準拠
//
// 機能:
//   - ノイズ値を使って単色または2色グラデーションをベースカラーに合成
//   - 雲、霧、影、ハイライトなどの半透明オーバーレイに使用
//   - 6種類のブレンドモードをサポート
//
// ═══════════════════════════════════════════════════════════════════════════

// TextureSlot2D.hlsl は Surface2D.hlsl 経由でインクルード想定
#include "Assets/GameLib/Script/Shader/Core/BaseShader/Features/BlendModes.hlsl"

// ---------------------------------------------------------------------------
// ColorOverlay パラメータ構造体
// ---------------------------------------------------------------------------
struct ColorOverlay2DParams
{
    float          enabled;
    TextureSlotRef source;
    half3          colorA;       // ノイズ=0 の色
    half           alphaA;       // colorA のアルファ
    half3          colorB;       // ノイズ=1 の色
    half           alphaB;       // colorB のアルファ
    half           colorMix;     // 0=colorAのみ, 1=colorBのみ, 0.5=グラデーション
    int            blendMode;    // BLEND_MODE_*
    half           opacity;      // 全体の不透明度
};

// ---------------------------------------------------------------------------
// CBUFFER から ColorOverlay2DParams を生成
// ---------------------------------------------------------------------------
inline ColorOverlay2DParams MakeColorOverlay2DParams(
    float enabled,
    float sourceSlotType,
    float sourceChannel,
    float sourceUVSpace,
    float4 sourceTilingOffset,
    float4 sourceRemap,
    float4 colorA,
    float4 colorB,
    float colorMix,
    float blendMode,
    float opacity)
{
    ColorOverlay2DParams p = (ColorOverlay2DParams)0;
    p.enabled = enabled;
    p.source = MakeTextureSlotRef(
        sourceSlotType,
        sourceChannel,
        sourceUVSpace,
        sourceTilingOffset,
        sourceRemap
    );
    p.colorA = colorA.rgb;
    p.alphaA = colorA.a;
    p.colorB = colorB.rgb;
    p.alphaB = colorB.a;
    p.colorMix = colorMix;
    p.blendMode = (int)round(blendMode);
    p.opacity = saturate(opacity);
    return p;
}

// ---------------------------------------------------------------------------
// ★ Simplified: 単色オーバーレイ用（BaseShader.shader の実際のプロパティに合わせた簡易版）
// ---------------------------------------------------------------------------
inline ColorOverlay2DParams MakeColorOverlay2DParamsSimple(
    float enabled,
    float sourceSlotType,
    float sourceChannel,
    float sourceUVSpace,
    float4 sourceTilingOffset,
    float4 sourceRemap,
    float4 overlayColor,     // 単色（colorA=colorB として使用）
    float blendMode,
    float intensity)         // opacity として使用
{
    ColorOverlay2DParams p = (ColorOverlay2DParams)0;
    p.enabled = enabled;
    p.source = MakeTextureSlotRef(
        sourceSlotType,
        sourceChannel,
        sourceUVSpace,
        sourceTilingOffset,
        sourceRemap
    );
    p.colorA = overlayColor.rgb;
    p.alphaA = overlayColor.a;
    p.colorB = overlayColor.rgb;  // 単色なので同じ
    p.alphaB = overlayColor.a;
    p.colorMix = 0.5h;            // グラデーションなし（中間）
    p.blendMode = (int)round(blendMode);
    p.opacity = saturate(intensity);
    return p;
}

// デフォルト値で初期化（無効状態）
inline ColorOverlay2DParams MakeDefaultColorOverlay2DParams()
{
    ColorOverlay2DParams p = (ColorOverlay2DParams)0;
    p.enabled = 0;
    p.source = MakeDefaultTextureSlotRef();
    p.colorA = half3(1, 1, 1);
    p.alphaA = 0;
    p.colorB = half3(1, 1, 1);
    p.alphaB = 0.5;
    p.colorMix = 1.0;
    p.blendMode = BLEND_MODE_NORMAL;
    p.opacity = 1.0;
    return p;
}

// ---------------------------------------------------------------------------
// ColorOverlay 適用
// ---------------------------------------------------------------------------
inline Surface2D Surface2D_ApplyColorOverlay(Surface2D s, ColorOverlay2DParams p)
{
    if (p.enabled < 0.5h)
        return s;
    if (p.source.slotType == TEXTURE_SLOT_NONE)
        return s;
    
    // ソースからノイズ値を取得
    half noise = SampleSlotScalar(s, p.source);
    
    // グラデーションカラー計算
    // colorMix=0: colorA のみ
    // colorMix=1: colorB のみ
    // colorMix=0.5: noise に応じた補間
    half gradientT = lerp(0.5h, noise, p.colorMix);
    half3 overlayColor = lerp(p.colorA, p.colorB, gradientT);
    half overlayAlpha = lerp(p.alphaA, p.alphaB, gradientT);
    
    // ブレンド
    half finalOpacity = p.opacity * overlayAlpha;
    half3 blended = s.color;
    
    [branch]
    switch (p.blendMode)
    {
        case BLEND_MODE_NORMAL:
            blended = BlendNormal(s.color, overlayColor, finalOpacity);
            break;
        case BLEND_MODE_MULTIPLY:
            blended = BlendMultiply(s.color, overlayColor, finalOpacity);
            break;
        case BLEND_MODE_ADD:
            blended = BlendAdd(s.color, overlayColor, finalOpacity);
            break;
        case BLEND_MODE_SCREEN:
            blended = BlendScreen(s.color, overlayColor, finalOpacity);
            break;
        case BLEND_MODE_OVERLAY:
            blended = BlendOverlay(s.color, overlayColor, finalOpacity);
            break;
        case BLEND_MODE_SOFTLIGHT:
            blended = BlendSoftLight(s.color, overlayColor, finalOpacity);
            break;
        case BLEND_MODE_HARDLIGHT:
            blended = BlendHardLight(s.color, overlayColor, finalOpacity);
            break;
        case BLEND_MODE_COLOR_BURN:
            blended = BlendColorBurn(s.color, overlayColor, finalOpacity);
            break;
        case BLEND_MODE_COLOR_DODGE:
            blended = BlendColorDodge(s.color, overlayColor, finalOpacity);
            break;
        case BLEND_MODE_DARKEN:
            blended = BlendDarken(s.color, overlayColor, finalOpacity);
            break;
        case BLEND_MODE_LIGHTEN:
            blended = BlendLighten(s.color, overlayColor, finalOpacity);
            break;
        case BLEND_MODE_DIFFERENCE:
            blended = BlendDifference(s.color, overlayColor, finalOpacity);
            break;
        case BLEND_MODE_EXCLUSION:
            blended = BlendExclusion(s.color, overlayColor, finalOpacity);
            break;
        case BLEND_MODE_HUE:
            blended = BlendHue(s.color, overlayColor, finalOpacity);
            break;
        case BLEND_MODE_SATURATION:
            blended = BlendSaturation(s.color, overlayColor, finalOpacity);
            break;
        case BLEND_MODE_COLOR:
            blended = BlendColor(s.color, overlayColor, finalOpacity);
            break;
        case BLEND_MODE_LUMINOSITY:
            blended = BlendLuminosity(s.color, overlayColor, finalOpacity);
            break;
        default:
            blended = s.color;
            break;
    }
    
    s.color = blended;
    return s;
}

#endif // GAME_COLOR_OVERLAY_2D_INCLUDED
