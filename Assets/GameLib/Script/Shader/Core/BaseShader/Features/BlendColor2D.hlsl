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
#include "Assets/GameLib/Script/Shader/Core/BaseShader/Features/ColorSpaceUtils.hlsl"
#include "Assets/GameLib/Script/Shader/Core/BaseShader/Features/AnimatedNoise2D.hlsl"
#include "Assets/GameLib/Script/Shader/Core/BaseShader/Features/Pixelation2D.hlsl"

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
    float animatedGradientEnabled;
    float animatedGradientPatternType;
    float animatedGradientMasterStrength;
    float animatedGradientNoiseScale;
    float2 animatedGradientNoiseDirection;
    float animatedGradientNoiseSpeed;
    float2 animatedGradientNoiseOffset;
    float animatedGradientRotationSpeed;
    float animatedGradientPulseAmplitude;
    float animatedGradientPulseSpeed;
    float animatedGradientWarpPatternType;
    float animatedGradientWarpScale;
    float animatedGradientWarpStrength;
    float2 animatedGradientWarpDirection;
    float animatedGradientWarpSpeed;
    float animatedGradientLoopSeconds;
    float animatedGradientOctaves;
    float animatedGradientLacunarity;
    float animatedGradientGain;
    float animatedGradientCellSharpness;
    float animatedGradientPatternContrast;
    float animatedGradientHueAmplitude;
    float animatedGradientSaturationAmplitude;
    float animatedGradientLightnessAmplitude;
    float animatedGradientPixelSize;
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
    float blendMode,
    float animatedGradientEnabled,
    float animatedGradientPatternType,
    float animatedGradientMasterStrength,
    float animatedGradientNoiseScale,
    float2 animatedGradientNoiseDirection,
    float animatedGradientNoiseSpeed,
    float2 animatedGradientNoiseOffset,
    float animatedGradientRotationSpeed,
    float animatedGradientPulseAmplitude,
    float animatedGradientPulseSpeed,
    float animatedGradientWarpPatternType,
    float animatedGradientWarpScale,
    float animatedGradientWarpStrength,
    float2 animatedGradientWarpDirection,
    float animatedGradientWarpSpeed,
    float animatedGradientLoopSeconds,
    float animatedGradientOctaves,
    float animatedGradientLacunarity,
    float animatedGradientGain,
    float animatedGradientCellSharpness,
    float animatedGradientPatternContrast,
    float animatedGradientHueAmplitude,
    float animatedGradientSaturationAmplitude,
    float animatedGradientLightnessAmplitude,
    float animatedGradientPixelSize)
{
    BlendColor2DParams p = (BlendColor2DParams)0;
    p.enabled = enabled;
    p.blendColor = blendColor;
    p.blendIntensity = saturate(blendIntensity);
    p.blendGradDirection = (int)round(blendGradDirection);
    p.blendGradationAmount = saturate(blendGradationAmount);
    p.blendGradSoftness = saturate(blendGradSoftness);
    p.blendMode = (int)round(blendMode);
    p.animatedGradientEnabled = animatedGradientEnabled;
    p.animatedGradientPatternType = animatedGradientPatternType;
    p.animatedGradientMasterStrength = max(animatedGradientMasterStrength, 0.0);
    p.animatedGradientNoiseScale = animatedGradientNoiseScale;
    p.animatedGradientNoiseDirection = animatedGradientNoiseDirection;
    p.animatedGradientNoiseSpeed = animatedGradientNoiseSpeed;
    p.animatedGradientNoiseOffset = animatedGradientNoiseOffset;
    p.animatedGradientRotationSpeed = animatedGradientRotationSpeed;
    p.animatedGradientPulseAmplitude = max(animatedGradientPulseAmplitude, 0.0);
    p.animatedGradientPulseSpeed = animatedGradientPulseSpeed;
    p.animatedGradientWarpPatternType = animatedGradientWarpPatternType;
    p.animatedGradientWarpScale = animatedGradientWarpScale;
    p.animatedGradientWarpStrength = animatedGradientWarpStrength;
    p.animatedGradientWarpDirection = animatedGradientWarpDirection;
    p.animatedGradientWarpSpeed = animatedGradientWarpSpeed;
    p.animatedGradientLoopSeconds = animatedGradientLoopSeconds;
    p.animatedGradientOctaves = animatedGradientOctaves;
    p.animatedGradientLacunarity = animatedGradientLacunarity;
    p.animatedGradientGain = animatedGradientGain;
    p.animatedGradientCellSharpness = animatedGradientCellSharpness;
    p.animatedGradientPatternContrast = animatedGradientPatternContrast;
    p.animatedGradientHueAmplitude = animatedGradientHueAmplitude;
    p.animatedGradientSaturationAmplitude = animatedGradientSaturationAmplitude;
    p.animatedGradientLightnessAmplitude = animatedGradientLightnessAmplitude;
    p.animatedGradientPixelSize = max(animatedGradientPixelSize, 1.0);
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
    p.animatedGradientEnabled = 0;
    p.animatedGradientPatternType = ANIMATED_NOISE_PATTERN_SMOOTH_VALUE;
    p.animatedGradientMasterStrength = 1;
    p.animatedGradientNoiseScale = 6;
    p.animatedGradientNoiseDirection = float2(1, 0);
    p.animatedGradientNoiseSpeed = 0.2;
    p.animatedGradientNoiseOffset = float2(0, 0);
    p.animatedGradientRotationSpeed = 0;
    p.animatedGradientPulseAmplitude = 0;
    p.animatedGradientPulseSpeed = 1;
    p.animatedGradientWarpPatternType = ANIMATED_NOISE_PATTERN_SMOOTH_VALUE;
    p.animatedGradientWarpScale = 2;
    p.animatedGradientWarpStrength = 0.1;
    p.animatedGradientWarpDirection = float2(0.71, 0.43);
    p.animatedGradientWarpSpeed = 0.35;
    p.animatedGradientLoopSeconds = 0;
    p.animatedGradientOctaves = 4;
    p.animatedGradientLacunarity = 2;
    p.animatedGradientGain = 0.5;
    p.animatedGradientCellSharpness = 1.5;
    p.animatedGradientPatternContrast = 1;
    p.animatedGradientHueAmplitude = 0.0025;
    p.animatedGradientSaturationAmplitude = 0.008;
    p.animatedGradientLightnessAmplitude = 0.015;
    p.animatedGradientPixelSize = 1;
    return p;
}

inline float2 BlendColor2D_ApplyAnimatedGradientPixelation(float2 uvLocal, float pixelSize)
{
    if (pixelSize <= 1.0)
        return uvLocal;

    float2 atlasUV = SpriteLocalUVToAtlasUV(uvLocal);
    atlasUV = Pixelation2D_ApplyUV_Texel_SpriteLocal(atlasUV, float2(pixelSize, pixelSize));
    return AtlasUVToSpriteLocalUV(atlasUV);
}

inline AnimatedNoise2DMotionParams BlendColor2D_MakeAnimatedGradientNoiseParams(BlendColor2DParams p)
{
    return MakeAnimatedNoise2DMotionParamsFull(
        p.animatedGradientEnabled,
        p.animatedGradientPatternType,
        p.animatedGradientNoiseScale,
        p.animatedGradientNoiseDirection,
        p.animatedGradientNoiseSpeed,
        p.animatedGradientNoiseOffset,
        p.animatedGradientRotationSpeed,
        p.animatedGradientPulseAmplitude,
        p.animatedGradientPulseSpeed,
        p.animatedGradientWarpPatternType,
        p.animatedGradientWarpScale,
        p.animatedGradientWarpStrength,
        p.animatedGradientWarpDirection,
        p.animatedGradientWarpSpeed,
        p.animatedGradientLoopSeconds,
        p.animatedGradientOctaves,
        p.animatedGradientLacunarity,
        p.animatedGradientGain,
        p.animatedGradientCellSharpness,
        p.animatedGradientPatternContrast);
}

inline half3 ResolveBlendColor2DAnimatedColor(Surface2D s, BlendColor2DParams p)
{
    half3 color = p.blendColor.rgb;
    if (p.animatedGradientEnabled < 0.5 || p.animatedGradientMasterStrength <= 1e-5)
        return color;

    float2 uvLocal = BlendColor2D_ApplyAnimatedGradientPixelation(s.uvLocal, p.animatedGradientPixelSize);
    AnimatedNoise2DMotionParams motion = BlendColor2D_MakeAnimatedGradientNoiseParams(p);
    float time = _Time.y;
    half3 hsl = RGBtoHSL(saturate(color));
    float hueWobble;
    float satWobble;
    float lightWobble;
    AnimatedNoise2D_SampleSignedTriplet(uvLocal, motion, time, hueWobble, satWobble, lightWobble);
    float master = p.animatedGradientMasterStrength;

    hsl.x = frac(hsl.x + (half)(hueWobble * p.animatedGradientHueAmplitude * master));
    hsl.y = saturate(hsl.y + (half)(satWobble * p.animatedGradientSaturationAmplitude * master));
    hsl.z = saturate(hsl.z + (half)(lightWobble * p.animatedGradientLightnessAmplitude * master));
    return HSLtoRGB(hsl);
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
    Surface2D result = s;
    if (p.enabled >= 0.5h)
    {
        half gradFactor = ComputeGradientFactor(result.uvLocal, p.blendGradDirection, p.blendGradationAmount, p.blendGradSoftness);
        half finalIntensity = p.blendIntensity * gradFactor * p.blendColor.a;
        half3 animatedBlendColor = ResolveBlendColor2DAnimatedColor(result, p);
        half3 blended = result.color;

        [branch]
        switch (p.blendMode)
        {
            case BLEND_MODE_NORMAL:
                blended = BlendNormal(result.color, animatedBlendColor, finalIntensity);
                break;
            case BLEND_MODE_MULTIPLY:
                blended = BlendMultiply(result.color, animatedBlendColor, finalIntensity);
                break;
            case BLEND_MODE_ADD:
                blended = BlendAdd(result.color, animatedBlendColor, finalIntensity);
                break;
            case BLEND_MODE_SCREEN:
                blended = BlendScreen(result.color, animatedBlendColor, finalIntensity);
                break;
            case BLEND_MODE_OVERLAY:
                blended = BlendOverlay(result.color, animatedBlendColor, finalIntensity);
                break;
            case BLEND_MODE_SOFTLIGHT:
                blended = BlendSoftLight(result.color, animatedBlendColor, finalIntensity);
                break;
            case BLEND_MODE_HARDLIGHT:
                blended = BlendHardLight(result.color, animatedBlendColor, finalIntensity);
                break;
            case BLEND_MODE_COLOR_BURN:
                blended = BlendColorBurn(result.color, animatedBlendColor, finalIntensity);
                break;
            case BLEND_MODE_COLOR_DODGE:
                blended = BlendColorDodge(result.color, animatedBlendColor, finalIntensity);
                break;
            case BLEND_MODE_DARKEN:
                blended = BlendDarken(result.color, animatedBlendColor, finalIntensity);
                break;
            case BLEND_MODE_LIGHTEN:
                blended = BlendLighten(result.color, animatedBlendColor, finalIntensity);
                break;
            case BLEND_MODE_DIFFERENCE:
                blended = BlendDifference(result.color, animatedBlendColor, finalIntensity);
                break;
            case BLEND_MODE_EXCLUSION:
                blended = BlendExclusion(result.color, animatedBlendColor, finalIntensity);
                break;
            case BLEND_MODE_HUE:
                blended = BlendHue(result.color, animatedBlendColor, finalIntensity);
                break;
            case BLEND_MODE_SATURATION:
                blended = BlendSaturation(result.color, animatedBlendColor, finalIntensity);
                break;
            case BLEND_MODE_COLOR:
                blended = BlendColor(result.color, animatedBlendColor, finalIntensity);
                break;
            case BLEND_MODE_LUMINOSITY:
                blended = BlendLuminosity(result.color, animatedBlendColor, finalIntensity);
                break;
            default:
                blended = result.color;
                break;
        }

        result.color = blended;
    }

    return result;
}

#endif // GAME_BLEND_COLOR_2D_INCLUDED
