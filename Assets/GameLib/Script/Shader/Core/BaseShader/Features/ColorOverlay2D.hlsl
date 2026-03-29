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
    ColorOverlay2DParams p;
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
    ColorOverlay2DParams p;
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
    ColorOverlay2DParams p;
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
    Surface2D result = s;
    if (p.enabled >= 0.5h && p.source.slotType != TEXTURE_SLOT_NONE)
    {
        half noise = SampleSlotScalar(result, p.source);
        half gradientT = lerp(0.5h, noise, p.colorMix);
        half3 overlayColor = lerp(p.colorA, p.colorB, gradientT);
        half overlayAlpha = lerp(p.alphaA, p.alphaB, gradientT);
        half finalOpacity = p.opacity * overlayAlpha;
        half3 blended = result.color;

        [branch]
        switch (p.blendMode)
        {
            case BLEND_MODE_NORMAL:
                blended = BlendNormal(result.color, overlayColor, finalOpacity);
                break;
            case BLEND_MODE_MULTIPLY:
                blended = BlendMultiply(result.color, overlayColor, finalOpacity);
                break;
            case BLEND_MODE_ADD:
                blended = BlendAdd(result.color, overlayColor, finalOpacity);
                break;
            case BLEND_MODE_SCREEN:
                blended = BlendScreen(result.color, overlayColor, finalOpacity);
                break;
            case BLEND_MODE_OVERLAY:
                blended = BlendOverlay(result.color, overlayColor, finalOpacity);
                break;
            case BLEND_MODE_SOFTLIGHT:
                blended = BlendSoftLight(result.color, overlayColor, finalOpacity);
                break;
            case BLEND_MODE_HARDLIGHT:
                blended = BlendHardLight(result.color, overlayColor, finalOpacity);
                break;
            case BLEND_MODE_COLOR_BURN:
                blended = BlendColorBurn(result.color, overlayColor, finalOpacity);
                break;
            case BLEND_MODE_COLOR_DODGE:
                blended = BlendColorDodge(result.color, overlayColor, finalOpacity);
                break;
            case BLEND_MODE_DARKEN:
                blended = BlendDarken(result.color, overlayColor, finalOpacity);
                break;
            case BLEND_MODE_LIGHTEN:
                blended = BlendLighten(result.color, overlayColor, finalOpacity);
                break;
            case BLEND_MODE_DIFFERENCE:
                blended = BlendDifference(result.color, overlayColor, finalOpacity);
                break;
            case BLEND_MODE_EXCLUSION:
                blended = BlendExclusion(result.color, overlayColor, finalOpacity);
                break;
            case BLEND_MODE_HUE:
                blended = BlendHue(result.color, overlayColor, finalOpacity);
                break;
            case BLEND_MODE_SATURATION:
                blended = BlendSaturation(result.color, overlayColor, finalOpacity);
                break;
            case BLEND_MODE_COLOR:
                blended = BlendColor(result.color, overlayColor, finalOpacity);
                break;
            case BLEND_MODE_LUMINOSITY:
                blended = BlendLuminosity(result.color, overlayColor, finalOpacity);
                break;
            default:
                blended = result.color;
                break;
        }

        result.color = blended;
    }

    return result;
}

#endif // GAME_COLOR_OVERLAY_2D_INCLUDED
