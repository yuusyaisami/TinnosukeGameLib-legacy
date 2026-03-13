#ifndef GAME_BLEND_MODES_INCLUDED
#define GAME_BLEND_MODES_INCLUDED

// ═══════════════════════════════════════════════════════════════════════════
// BlendModes.hlsl - 共通ブレンドモード定義と関数
// ═══════════════════════════════════════════════════════════════════════════

#include "Assets/GameLib/Script/Shader/Core/BaseShader/Features/ColorSpaceUtils.hlsl"

// ---------------------------------------------------------------------------
// Blend Mode 定義
// ---------------------------------------------------------------------------
#ifndef BLEND_MODE_NORMAL
#define BLEND_MODE_NORMAL     0
#define BLEND_MODE_MULTIPLY   1
#define BLEND_MODE_ADD        2
#define BLEND_MODE_SCREEN     3
#define BLEND_MODE_OVERLAY    4
#define BLEND_MODE_SOFTLIGHT  5
#define BLEND_MODE_HARDLIGHT  6
#define BLEND_MODE_COLOR_BURN 7
#define BLEND_MODE_COLOR_DODGE 8
#define BLEND_MODE_DARKEN     9
#define BLEND_MODE_LIGHTEN    10
#define BLEND_MODE_DIFFERENCE 11
#define BLEND_MODE_EXCLUSION  12
#define BLEND_MODE_HUE        13
#define BLEND_MODE_SATURATION 14
#define BLEND_MODE_COLOR      15
#define BLEND_MODE_LUMINOSITY 16
#endif

// ---------------------------------------------------------------------------
// ブレンド関数
// ---------------------------------------------------------------------------
inline half3 BlendNormal(half3 base, half3 overlay, half opacity)
{
    return lerp(base, overlay, opacity);
}

inline half3 BlendMultiply(half3 base, half3 overlay, half opacity)
{
    return lerp(base, base * overlay, opacity);
}

inline half3 BlendAdd(half3 base, half3 overlay, half opacity)
{
    return base + overlay * opacity;
}

inline half3 BlendScreen(half3 base, half3 overlay, half opacity)
{
    half3 result = 1.0h - (1.0h - base) * (1.0h - overlay);
    return lerp(base, result, opacity);
}

inline half3 BlendOverlay(half3 base, half3 overlay, half opacity)
{
    half3 result;
    result.r = base.r < 0.5h ? 2.0h * base.r * overlay.r : 1.0h - 2.0h * (1.0h - base.r) * (1.0h - overlay.r);
    result.g = base.g < 0.5h ? 2.0h * base.g * overlay.g : 1.0h - 2.0h * (1.0h - base.g) * (1.0h - overlay.g);
    result.b = base.b < 0.5h ? 2.0h * base.b * overlay.b : 1.0h - 2.0h * (1.0h - base.b) * (1.0h - overlay.b);
    return lerp(base, result, opacity);
}

inline half3 BlendSoftLight(half3 base, half3 overlay, half opacity)
{
    // Photoshop 風の Soft Light
    half3 result = (overlay < 0.5h)
        ? base - (1.0h - 2.0h * overlay) * base * (1.0h - base)
        : base + (2.0h * overlay - 1.0h) * (sqrt(base) - base);
    return lerp(base, result, opacity);
}

inline half3 BlendHardLight(half3 base, half3 overlay, half opacity)
{
    half3 result;
    result.r = overlay.r < 0.5h ? 2.0h * base.r * overlay.r : 1.0h - 2.0h * (1.0h - base.r) * (1.0h - overlay.r);
    result.g = overlay.g < 0.5h ? 2.0h * base.g * overlay.g : 1.0h - 2.0h * (1.0h - base.g) * (1.0h - overlay.g);
    result.b = overlay.b < 0.5h ? 2.0h * base.b * overlay.b : 1.0h - 2.0h * (1.0h - base.b) * (1.0h - overlay.b);
    return lerp(base, result, opacity);
}

inline half3 BlendColorBurn(half3 base, half3 overlay, half opacity)
{
    half3 result = 1.0h - saturate((1.0h - base) / (overlay + 1e-5h));
    return lerp(base, result, opacity);
}

inline half3 BlendColorDodge(half3 base, half3 overlay, half opacity)
{
    half3 result = saturate(base / (1.0h - overlay + 1e-5h));
    return lerp(base, result, opacity);
}

inline half3 BlendDarken(half3 base, half3 overlay, half opacity)
{
    return lerp(base, min(base, overlay), opacity);
}

inline half3 BlendLighten(half3 base, half3 overlay, half opacity)
{
    return lerp(base, max(base, overlay), opacity);
}

inline half3 BlendDifference(half3 base, half3 overlay, half opacity)
{
    return lerp(base, abs(base - overlay), opacity);
}

inline half3 BlendExclusion(half3 base, half3 overlay, half opacity)
{
    half3 result = base + overlay - 2.0h * base * overlay;
    return lerp(base, result, opacity);
}

// HSVL Blends
inline half3 BlendHue(half3 base, half3 overlay, half opacity)
{
    half3 hsvBase = RGBtoHSV(base);
    half3 hsvOverlay = RGBtoHSV(overlay);
    half3 result = HSVtoRGB(half3(hsvOverlay.x, hsvBase.y, hsvBase.z));
    return lerp(base, result, opacity);
}

inline half3 BlendSaturation(half3 base, half3 overlay, half opacity)
{
    half3 hsvBase = RGBtoHSV(base);
    half3 hsvOverlay = RGBtoHSV(overlay);
    half3 result = HSVtoRGB(half3(hsvBase.x, hsvOverlay.y, hsvBase.z));
    return lerp(base, result, opacity);
}

inline half3 BlendColor(half3 base, half3 overlay, half opacity)
{
    half3 hsvBase = RGBtoHSV(base);
    half3 hsvOverlay = RGBtoHSV(overlay);
    half3 result = HSVtoRGB(half3(hsvOverlay.x, hsvOverlay.y, hsvBase.z));
    return lerp(base, result, opacity);
}

inline half3 BlendLuminosity(half3 base, half3 overlay, half opacity)
{
    half3 hsvBase = RGBtoHSV(base);
    half3 hsvOverlay = RGBtoHSV(overlay);
    half3 result = HSVtoRGB(half3(hsvBase.x, hsvBase.y, hsvOverlay.z));
    return lerp(base, result, opacity);
}

#endif // GAME_BLEND_MODES_INCLUDED

