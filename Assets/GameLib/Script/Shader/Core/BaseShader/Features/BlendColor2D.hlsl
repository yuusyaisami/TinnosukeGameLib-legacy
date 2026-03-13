#ifndef GAME_BLEND_COLOR_2D_INCLUDED
#define GAME_BLEND_COLOR_2D_INCLUDED

// ═══════════════════════════════════════════════════════════════════════════
// BlendColor2D.hlsl - グラデーションブレンドカラーエフェクト
// ═══════════════════════════════════════════════════════════════════════════
//
// 機能:
//   - ColorOverlay2D.Color を使用して、方向性グラデーションブレンドを適用
//   - 水平/垂直/放射状のグラデーション方向をサポート
//   - 画面効果（ビネット、グラデーション背景など）に使用
//
// NOTE: BlendColor2D keeps its own Color property (_BlendColor2DColor) separate from ColorOverlay
#include "Assets/GameLib/Script/Shader/Core/BaseShader/Features/BlendModes.hlsl"

// ═══════════════════════════════════════════════════════════════════════════

// ---------------------------------------------------------------------------
// グラデーション方向定義
// ---------------------------------------------------------------------------
#define BLEND_GRAD_NONE       0  // グラデーションなし（均一）
#define BLEND_GRAD_HORIZONTAL 1  // 左→右
#define BLEND_GRAD_VERTICAL   2  // 下→上
#define BLEND_GRAD_RADIAL     3  // 中心→外周

// ---------------------------------------------------------------------------
// BlendColor2D パラメータ構造体
// ---------------------------------------------------------------------------
struct BlendColor2DParams
{
    float enabled;
    half4 blendColor;           // ColorOverlay2D.Color から取得
    half  blendIntensity;       // ブレンド強度 (0-1)
    int   blendGradDirection;   // BLEND_GRAD_*
    half  blendGradationAmount; // グラデーション量 (0=均一, 1=フル)
    half  blendGradSoftness;    // グラデーション境界のソフトさ (0=硬い, 1=従来どおり)
    int   blendMode;            // BLEND_MODE_* (追加)
};

// ---------------------------------------------------------------------------
// CBUFFER から パラメータ生成
// ---------------------------------------------------------------------------
inline BlendColor2DParams MakeBlendColor2DParams(
    float enabled,
    float4 blendColor,
    float blendIntensity,
    float blendGradDirection,
    float blendGradationAmount,
    float blendGradSoftness,
    float blendMode)
{
    BlendColor2DParams p = (BlendColor2DParams)0;
    p.enabled = enabled;
    p.blendColor = blendColor;
    p.blendIntensity = saturate(blendIntensity);
    p.blendGradDirection = (int)round(blendGradDirection);
    p.blendGradationAmount = saturate(blendGradationAmount);
    p.blendGradSoftness = saturate(blendGradSoftness);
    p.blendMode = (int)round(blendMode);
    return p;
}

// デフォルト（無効状態）
inline BlendColor2DParams MakeDefaultBlendColor2DParams()
{
    BlendColor2DParams p = (BlendColor2DParams)0;
    p.enabled = 0;
    p.blendColor = half4(1, 1, 1, 0);
    p.blendIntensity = 0;
    p.blendGradDirection = BLEND_GRAD_NONE;
    p.blendGradationAmount = 0;
    p.blendGradSoftness = 1;
    p.blendMode = BLEND_MODE_NORMAL;
    return p;
}

// ---------------------------------------------------------------------------
// グラデーション係数計算
// ---------------------------------------------------------------------------
inline half ComputeGradientFactor(float2 uv, int direction, half gradAmount, half gradSoftness)
{
    half gradCoord = 1.0h;
    
    [branch]
    switch (direction)
    {
        case BLEND_GRAD_NONE:
            gradCoord = 1.0h;
            break;
            
        case BLEND_GRAD_HORIZONTAL:
            // 左(0) → 右(1)
            gradCoord = saturate(uv.x);
            break;
            
        case BLEND_GRAD_VERTICAL:
            // 下(0) → 上(1)
            gradCoord = saturate(uv.y);
            break;
            
        case BLEND_GRAD_RADIAL:
            // 中心(1) → 外周(0) のビネット風
            float2 centered = uv - 0.5;
            half dist = saturate(length(centered) * 2.0h); // 0 at center, 1 at corners
            gradCoord = 1.0h - dist;
            break;
            
        default:
            gradCoord = 1.0h;
            break;
    }

    // 従来どおりの線形グラデーション（Softness=1 側）
    half linearFactor = lerp(1.0h, gradCoord, saturate(gradAmount));
    
    // Softness=1 は線形、Softness=0 は Amount で境界が動くハードマスク。
    half softness = saturate(gradSoftness);
    if (softness >= 0.999h)
        return linearFactor;

    // Amount=0 は全面有効（従来互換）
    half amount = saturate(gradAmount);
    if (amount <= 0.0001h)
        return 1.0h;

    // Amount を境界位置として使う（0→左端/下端/外周から、1→右端/上端/中心へ）
    half hardFactor = step(amount, gradCoord);
    return saturate(lerp(hardFactor, linearFactor, softness));
}

// ---------------------------------------------------------------------------
// BlendColor2D 適用
// ---------------------------------------------------------------------------
inline Surface2D Surface2D_ApplyBlendColor(Surface2D s, BlendColor2DParams p)
{
    if (p.enabled < 0.5h)
        return s;
    
    // グラデーション係数を計算
    half gradFactor = ComputeGradientFactor(s.uvLocal, p.blendGradDirection, p.blendGradationAmount, p.blendGradSoftness);
    
    // 最終的なブレンド強度
    half finalIntensity = p.blendIntensity * gradFactor * p.blendColor.a;
    
    // ブレンド適用
    half3 blended = s.color;
    
    [branch]
    switch (p.blendMode)
    {
        case BLEND_MODE_NORMAL:
            blended = BlendNormal(s.color, p.blendColor.rgb, finalIntensity);
            break;
        case BLEND_MODE_MULTIPLY:
            blended = BlendMultiply(s.color, p.blendColor.rgb, finalIntensity);
            break;
        case BLEND_MODE_ADD:
            blended = BlendAdd(s.color, p.blendColor.rgb, finalIntensity);
            break;
        case BLEND_MODE_SCREEN:
            blended = BlendScreen(s.color, p.blendColor.rgb, finalIntensity);
            break;
        case BLEND_MODE_OVERLAY:
            blended = BlendOverlay(s.color, p.blendColor.rgb, finalIntensity);
            break;
        case BLEND_MODE_SOFTLIGHT:
            blended = BlendSoftLight(s.color, p.blendColor.rgb, finalIntensity);
            break;
        case BLEND_MODE_HARDLIGHT:
            blended = BlendHardLight(s.color, p.blendColor.rgb, finalIntensity);
            break;
        case BLEND_MODE_COLOR_BURN:
            blended = BlendColorBurn(s.color, p.blendColor.rgb, finalIntensity);
            break;
        case BLEND_MODE_COLOR_DODGE:
            blended = BlendColorDodge(s.color, p.blendColor.rgb, finalIntensity);
            break;
        case BLEND_MODE_DARKEN:
            blended = BlendDarken(s.color, p.blendColor.rgb, finalIntensity);
            break;
        case BLEND_MODE_LIGHTEN:
            blended = BlendLighten(s.color, p.blendColor.rgb, finalIntensity);
            break;
        case BLEND_MODE_DIFFERENCE:
            blended = BlendDifference(s.color, p.blendColor.rgb, finalIntensity);
            break;
        case BLEND_MODE_EXCLUSION:
            blended = BlendExclusion(s.color, p.blendColor.rgb, finalIntensity);
            break;
        case BLEND_MODE_HUE:
            blended = BlendHue(s.color, p.blendColor.rgb, finalIntensity);
            break;
        case BLEND_MODE_SATURATION:
            blended = BlendSaturation(s.color, p.blendColor.rgb, finalIntensity);
            break;
        case BLEND_MODE_COLOR:
            blended = BlendColor(s.color, p.blendColor.rgb, finalIntensity);
            break;
        case BLEND_MODE_LUMINOSITY:
            blended = BlendLuminosity(s.color, p.blendColor.rgb, finalIntensity);
            break;
        default:
            blended = s.color;
            break;
    }
    
    s.color = blended;
    return s;
}

#endif // GAME_BLEND_COLOR_2D_INCLUDED
