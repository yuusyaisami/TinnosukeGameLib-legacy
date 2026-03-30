#ifndef GAME_ADVANCED_FADE_2D_INCLUDED
#define GAME_ADVANCED_FADE_2D_INCLUDED

// ═══════════════════════════════════════════════════════════════════════════
// AdvancedFade2D.hlsl - ワイプベースフェード + 境界グロー + ウェーブ
// ═══════════════════════════════════════════════════════════════════════════
//
// 機能:
//   - 方向性ワイプフェード（左→右、下→上、放射状など）
//   - 境界線にグローエフェクト（発光する境界）
//   - 境界線のウェーブ（波打ち）変形
//   - 画面遷移、キャラクター出現/消失、UI演出に使用
//
// ═══════════════════════════════════════════════════════════════════════════

#include "Assets/GameLib/Script/Shader/Core/BaseShader/Features/AnimatedNoise2D.hlsl"

// ---------------------------------------------------------------------------
// フェード方向定義
// ---------------------------------------------------------------------------
#define FADE_DIR_ALPHA_ONLY       -1 // シンプルな Alpha フェード
#define FADE_DIR_LEFT_TO_RIGHT     0
#define FADE_DIR_RIGHT_TO_LEFT     1
#define FADE_DIR_BOTTOM_TO_TOP     2
#define FADE_DIR_TOP_TO_BOTTOM     3
#define FADE_DIR_RADIAL_IN         4  // 外周→中心
#define FADE_DIR_RADIAL_OUT        5  // 中心→外周
#define FADE_DIR_CIRCLE            6  // 角度スイープ

// グロー ブレンドモード
#define GLOW_BLEND_ADD      0
#define GLOW_BLEND_SCREEN   1
#define GLOW_BLEND_OVERLAY  2

#define FADE_TWO_PI 6.28318530718

// ---------------------------------------------------------------------------
// AdvancedFade2D パラメータ構造体
// ---------------------------------------------------------------------------
struct AdvancedFade2DParams
{
    float enabled;
    int   fadeDirection;    // FADE_DIR_*
    half  fadeAmount;       // 0=完全表示, 1=完全フェード
    half  soft;             // 境界ぼかし幅 (0=ハード, 1=ソフト)
    
    // グロー設定
    half  glowIntensity;    // グロー強度
    half  glowRange;        // グロー範囲（境界からの距離）
    int   glowBlendMode;    // GLOW_BLEND_*
    
    // ウェーブ設定
    // WaveParamsA: x=周波数A, y=振幅A, z=速度A, w=オフセットA
    // WaveParamsB: x=周波数B, y=振幅B, z=速度B, w=オフセットB
    float4 waveParamsA;
    float4 waveParamsB;
    float circleStartAngleDeg;
    float circleClockwise;

    // Burn (Noise Dissolve)
    float burnEnabled;
    half  burnProgress;
    half  burnEdgeWidth;
    half  burnNoiseScale;
    half  burnNoiseStrength;
    int   burnNoiseType;
    float2 burnDirection;
    float4 burnEdgeColor;
    int   burnBlendMode;
    float burnInvert;
    float burnAnimatedNoiseEnabled;
    int   burnAnimatedNoisePatternType;
    float2 burnAnimatedNoiseDirection;
    float burnAnimatedNoiseSpeed;
    float2 burnAnimatedNoiseOffset;
    float burnAnimatedNoiseRotationSpeed;
    float burnAnimatedNoisePulseAmplitude;
    float burnAnimatedNoisePulseSpeed;
    int   burnAnimatedNoiseWarpPatternType;
    float burnAnimatedNoiseWarpScale;
    float burnAnimatedNoiseWarpStrength;
    float2 burnAnimatedNoiseWarpDirection;
    float burnAnimatedNoiseWarpSpeed;
    float burnAnimatedNoiseLoopSeconds;
    int   burnAnimatedNoiseOctaves;
    float burnAnimatedNoiseLacunarity;
    float burnAnimatedNoiseGain;
    float burnAnimatedNoiseCellSharpness;
    float burnAnimatedNoisePatternContrast;
};

// ---------------------------------------------------------------------------
// CBUFFER からパラメータ生成
// ---------------------------------------------------------------------------
inline AdvancedFade2DParams MakeAdvancedFade2DParams(
    float enabled,
    float fadeDirection,
    float fadeAmount,
    float soft,
    float glowIntensity,
    float glowRange,
    float glowBlendMode,
    float4 waveParamsA,
    float4 waveParamsB,
    float circleStartAngleDeg,
    float circleClockwise,
    float burnEnabled,
    float burnProgress,
    float burnEdgeWidth,
    float burnNoiseScale,
    float burnNoiseStrength,
    float burnNoiseType,
    float2 burnDirection,
    float4 burnEdgeColor,
    float burnBlendMode,
    float burnInvert,
    float burnAnimatedNoiseEnabled,
    float burnAnimatedNoisePatternType,
    float2 burnAnimatedNoiseDirection,
    float burnAnimatedNoiseSpeed,
    float2 burnAnimatedNoiseOffset,
    float burnAnimatedNoiseRotationSpeed,
    float burnAnimatedNoisePulseAmplitude,
    float burnAnimatedNoisePulseSpeed,
    float burnAnimatedNoiseWarpPatternType,
    float burnAnimatedNoiseWarpScale,
    float burnAnimatedNoiseWarpStrength,
    float2 burnAnimatedNoiseWarpDirection,
    float burnAnimatedNoiseWarpSpeed,
    float burnAnimatedNoiseLoopSeconds,
    float burnAnimatedNoiseOctaves,
    float burnAnimatedNoiseLacunarity,
    float burnAnimatedNoiseGain,
    float burnAnimatedNoiseCellSharpness,
    float burnAnimatedNoisePatternContrast)
{
    AdvancedFade2DParams p = (AdvancedFade2DParams)0;
    p.enabled = enabled;
    p.fadeDirection = (int)round(fadeDirection);
    p.fadeAmount = saturate(fadeAmount);
    p.soft = saturate(soft);
    p.glowIntensity = max(0, glowIntensity);
    p.glowRange = max(0, glowRange);
    p.glowBlendMode = (int)round(glowBlendMode);
    p.waveParamsA = waveParamsA;
    p.waveParamsB = waveParamsB;
    p.circleStartAngleDeg = circleStartAngleDeg;
    p.circleClockwise = circleClockwise;
    p.burnEnabled = burnEnabled;
    p.burnProgress = saturate(burnProgress);
    p.burnEdgeWidth = max(0.0001, burnEdgeWidth);
    p.burnNoiseScale = max(0.0001, burnNoiseScale);
    p.burnNoiseStrength = saturate(burnNoiseStrength);
    p.burnNoiseType = (int)round(burnNoiseType);
    p.burnDirection = burnDirection;
    p.burnEdgeColor = burnEdgeColor;
    p.burnBlendMode = (int)round(burnBlendMode);
    p.burnInvert = burnInvert;
    p.burnAnimatedNoiseEnabled = burnAnimatedNoiseEnabled;
    p.burnAnimatedNoisePatternType = (int)round(burnAnimatedNoisePatternType);
    p.burnAnimatedNoiseDirection = burnAnimatedNoiseDirection;
    p.burnAnimatedNoiseSpeed = burnAnimatedNoiseSpeed;
    p.burnAnimatedNoiseOffset = burnAnimatedNoiseOffset;
    p.burnAnimatedNoiseRotationSpeed = burnAnimatedNoiseRotationSpeed;
    p.burnAnimatedNoisePulseAmplitude = max(0.0, burnAnimatedNoisePulseAmplitude);
    p.burnAnimatedNoisePulseSpeed = burnAnimatedNoisePulseSpeed;
    p.burnAnimatedNoiseWarpPatternType = (int)round(burnAnimatedNoiseWarpPatternType);
    p.burnAnimatedNoiseWarpScale = max(0.0001, burnAnimatedNoiseWarpScale);
    p.burnAnimatedNoiseWarpStrength = max(0.0, burnAnimatedNoiseWarpStrength);
    p.burnAnimatedNoiseWarpDirection = burnAnimatedNoiseWarpDirection;
    p.burnAnimatedNoiseWarpSpeed = burnAnimatedNoiseWarpSpeed;
    p.burnAnimatedNoiseLoopSeconds = max(0.0, burnAnimatedNoiseLoopSeconds);
    p.burnAnimatedNoiseOctaves = min(max((int)round(burnAnimatedNoiseOctaves), 1), 6);
    p.burnAnimatedNoiseLacunarity = max(1.0, burnAnimatedNoiseLacunarity);
    p.burnAnimatedNoiseGain = saturate(burnAnimatedNoiseGain);
    p.burnAnimatedNoiseCellSharpness = max(0.01, burnAnimatedNoiseCellSharpness);
    p.burnAnimatedNoisePatternContrast = max(0.0, burnAnimatedNoisePatternContrast);
    return p;
}

// デフォルト（無効状態）
inline AdvancedFade2DParams MakeDefaultAdvancedFade2DParams()
{
    AdvancedFade2DParams p = (AdvancedFade2DParams)0;
    p.enabled = 0;
    p.fadeDirection = FADE_DIR_LEFT_TO_RIGHT;
    p.fadeAmount = 0;
    p.soft = 0.1;
    p.glowIntensity = 0;
    p.glowRange = 0.05;
    p.glowBlendMode = GLOW_BLEND_ADD;
    p.waveParamsA = float4(0, 0, 0, 0);
    p.waveParamsB = float4(0, 0, 0, 0);
    p.circleStartAngleDeg = 90;
    p.circleClockwise = 1;
    p.burnEnabled = 0;
    p.burnProgress = 0;
    p.burnEdgeWidth = 0.1;
    p.burnNoiseScale = 4;
    p.burnNoiseStrength = 0.5;
    p.burnNoiseType = 0;
    p.burnDirection = float2(0, 1);
    p.burnEdgeColor = float4(1, 0.5, 0.1, 1);
    p.burnBlendMode = GLOW_BLEND_ADD;
    p.burnInvert = 0;
    p.burnAnimatedNoiseEnabled = 0;
    p.burnAnimatedNoisePatternType = ANIMATED_NOISE_PATTERN_SMOOTH_VALUE;
    p.burnAnimatedNoiseDirection = float2(1, 0);
    p.burnAnimatedNoiseSpeed = 0.2;
    p.burnAnimatedNoiseOffset = float2(0, 0);
    p.burnAnimatedNoiseRotationSpeed = 0;
    p.burnAnimatedNoisePulseAmplitude = 0;
    p.burnAnimatedNoisePulseSpeed = 1;
    p.burnAnimatedNoiseWarpPatternType = ANIMATED_NOISE_PATTERN_SMOOTH_VALUE;
    p.burnAnimatedNoiseWarpScale = 2;
    p.burnAnimatedNoiseWarpStrength = 0.2;
    p.burnAnimatedNoiseWarpDirection = float2(0.71, 0.43);
    p.burnAnimatedNoiseWarpSpeed = 0.35;
    p.burnAnimatedNoiseLoopSeconds = 0;
    p.burnAnimatedNoiseOctaves = 4;
    p.burnAnimatedNoiseLacunarity = 2;
    p.burnAnimatedNoiseGain = 0.5;
    p.burnAnimatedNoiseCellSharpness = 1.5;
    p.burnAnimatedNoisePatternContrast = 1;
    return p;
}

inline half3 ApplyBurnBlend(half3 base, half3 burnColor, half burnAmount, int blendMode)
{
    [branch]
    switch (blendMode)
    {
        case GLOW_BLEND_ADD:
            return base + burnColor * burnAmount;
        case GLOW_BLEND_SCREEN:
            {
                half3 screened = 1.0h - (1.0h - base) * (1.0h - burnColor);
                return lerp(base, screened, burnAmount);
            }
        case GLOW_BLEND_OVERLAY:
            {
                half3 overlay = half3(0.0h, 0.0h, 0.0h);
                overlay.r = base.r < 0.5h ? 2.0h * base.r * burnColor.r : 1.0h - 2.0h * (1.0h - base.r) * (1.0h - burnColor.r);
                overlay.g = base.g < 0.5h ? 2.0h * base.g * burnColor.g : 1.0h - 2.0h * (1.0h - base.g) * (1.0h - burnColor.g);
                overlay.b = base.b < 0.5h ? 2.0h * base.b * burnColor.b : 1.0h - 2.0h * (1.0h - base.b) * (1.0h - burnColor.b);
                return lerp(base, overlay, burnAmount);
            }
        default:
            return base + burnColor * burnAmount;
    }
}

// ---------------------------------------------------------------------------
// ウェーブ変位計算
// ---------------------------------------------------------------------------
inline half ComputeWaveOffset(float coord, float4 waveParams, float time)
{
    // waveParams: x=frequency, y=amplitude, z=speed, w=phase offset
    if (waveParams.y < 0.001h)
        return 0;
    
    half wave = sin(coord * waveParams.x + time * waveParams.z + waveParams.w);
    return wave * waveParams.y;
}

inline half ComputeTotalWaveOffset(float coord, float4 waveA, float4 waveB, float time)
{
    return ComputeWaveOffset(coord, waveA, time) + ComputeWaveOffset(coord, waveB, time);
}

// ---------------------------------------------------------------------------
// フェード座標計算（方向に応じた 0-1 座標を返す）
// ---------------------------------------------------------------------------
inline half ComputeFadeCoord(float2 uv, int direction, float4 waveA, float4 waveB, float time)
{
    half coord = 0.0h;
    half waveOffset = 0.0h;
    
    [branch]
    switch (direction)
    {
        case FADE_DIR_LEFT_TO_RIGHT:
            waveOffset = ComputeTotalWaveOffset(uv.y, waveA, waveB, time);
            coord = uv.x + waveOffset;
            break;
            
        case FADE_DIR_RIGHT_TO_LEFT:
            waveOffset = ComputeTotalWaveOffset(uv.y, waveA, waveB, time);
            coord = 1.0h - uv.x + waveOffset;
            break;
            
        case FADE_DIR_BOTTOM_TO_TOP:
            waveOffset = ComputeTotalWaveOffset(uv.x, waveA, waveB, time);
            coord = uv.y + waveOffset;
            break;
            
        case FADE_DIR_TOP_TO_BOTTOM:
            waveOffset = ComputeTotalWaveOffset(uv.x, waveA, waveB, time);
            coord = 1.0h - uv.y + waveOffset;
            break;
            
        case FADE_DIR_RADIAL_IN:
            {
                float2 centered = uv - 0.5;
                half angle = atan2(centered.y, centered.x);
                waveOffset = ComputeTotalWaveOffset(angle, waveA, waveB, time);
                coord = 1.0h - saturate(length(centered) * 2.0h) + waveOffset;
            }
            break;
            
        case FADE_DIR_RADIAL_OUT:
            {
                float2 centered = uv - 0.5;
                half angle = atan2(centered.y, centered.x);
                waveOffset = ComputeTotalWaveOffset(angle, waveA, waveB, time);
                coord = saturate(length(centered) * 2.0h) + waveOffset;
            }
            break;

        default:
            coord = uv.x;
            break;
    }
    
    return coord;
}

inline half ComputeCircleFadeCoord(float2 uv, AdvancedFade2DParams p, float time)
{
    float2 centered = (uv - 0.5) * AnimatedNoise2D_GetSpriteAspectScale();
    half radius = saturate(length(centered) * 2.0h);
    half waveOffset = ComputeTotalWaveOffset(radius, p.waveParamsA, p.waveParamsB, time);
    float angle = atan2(centered.y, centered.x);
    float angle01 = frac((angle / FADE_TWO_PI) + 1.0);
    float startAngle01 = frac(p.circleStartAngleDeg / 360.0);
    float sweep = (p.circleClockwise > 0.5)
        ? frac(startAngle01 - angle01)
        : frac(angle01 - startAngle01);
    return frac(sweep + waveOffset);
}

inline half ComputeFadeAlphaFromCoord(half fadeCoord, half fadeAmount, half soft)
{
    half threshold = saturate(fadeAmount);
    if (threshold <= 0.0001h)
        return 1.0h;
    if (threshold >= 0.9999h)
        return 0.0h;

    half halfSoft = saturate(soft) * 0.5h;
    if (halfSoft <= 0.00001h)
        return fadeCoord >= threshold ? 1.0h : 0.0h;

    half front = lerp(-halfSoft, 1.0h + halfSoft, threshold);
    return smoothstep(front - halfSoft, front + halfSoft, fadeCoord);
}

inline half ComputeFadeBoundaryDistance(half fadeCoord, half threshold, int direction)
{
    if (direction == FADE_DIR_CIRCLE)
        return abs(frac((fadeCoord - threshold) + 0.5h) - 0.5h);
    return abs(fadeCoord - threshold);
}

// ---------------------------------------------------------------------------
// グローブレンド
// ---------------------------------------------------------------------------
inline half3 ApplyGlowBlend(half3 base, half3 glowColor, half glowAmount, int blendMode)
{
    [branch]
    switch (blendMode)
    {
        case GLOW_BLEND_ADD:
            return base + glowColor * glowAmount;
            
        case GLOW_BLEND_SCREEN:
            {
                half3 screened = 1.0h - (1.0h - base) * (1.0h - glowColor);
                return lerp(base, screened, glowAmount);
            }
            
        case GLOW_BLEND_OVERLAY:
            {
                half3 overlay = half3(0.0h, 0.0h, 0.0h);
                overlay.r = base.r < 0.5h ? 2.0h * base.r * glowColor.r : 1.0h - 2.0h * (1.0h - base.r) * (1.0h - glowColor.r);
                overlay.g = base.g < 0.5h ? 2.0h * base.g * glowColor.g : 1.0h - 2.0h * (1.0h - base.g) * (1.0h - glowColor.g);
                overlay.b = base.b < 0.5h ? 2.0h * base.b * glowColor.b : 1.0h - 2.0h * (1.0h - base.b) * (1.0h - glowColor.b);
                return lerp(base, overlay, glowAmount);
            }
            
        default:
            return base + glowColor * glowAmount;
    }
}

inline AnimatedNoise2DMotionParams AdvancedFade2D_MakeBurnAnimatedNoiseParams(AdvancedFade2DParams p)
{
    if (p.burnAnimatedNoiseEnabled > 0.5)
    {
        return MakeAnimatedNoise2DMotionParamsFull(
            p.burnAnimatedNoiseEnabled,
            p.burnAnimatedNoisePatternType,
            p.burnNoiseScale,
            p.burnAnimatedNoiseDirection,
            p.burnAnimatedNoiseSpeed,
            p.burnAnimatedNoiseOffset,
            p.burnAnimatedNoiseRotationSpeed,
            p.burnAnimatedNoisePulseAmplitude,
            p.burnAnimatedNoisePulseSpeed,
            p.burnAnimatedNoiseWarpPatternType,
            p.burnAnimatedNoiseWarpScale,
            p.burnAnimatedNoiseWarpStrength,
            p.burnAnimatedNoiseWarpDirection,
            p.burnAnimatedNoiseWarpSpeed,
            p.burnAnimatedNoiseLoopSeconds,
            p.burnAnimatedNoiseOctaves,
            p.burnAnimatedNoiseLacunarity,
            p.burnAnimatedNoiseGain,
            p.burnAnimatedNoiseCellSharpness,
            p.burnAnimatedNoisePatternContrast);
    }

    return MakeAnimatedNoise2DMotionParamsFull(
        1.0,
        p.burnNoiseType,
        p.burnNoiseScale,
        float2(1.0, 0.0),
        0.0,
        float2(0.0, 0.0),
        0.0,
        0.0,
        1.0,
        ANIMATED_NOISE_PATTERN_SMOOTH_VALUE,
        2.0,
        0.0,
        float2(1.0, 0.0),
        0.0,
        0.0,
        4.0,
        2.0,
        0.5,
        1.5,
        1.0);
}

// ---------------------------------------------------------------------------
// AdvancedFade2D 適用
// ---------------------------------------------------------------------------
inline Surface2D Surface2D_ApplyAdvancedFade(Surface2D s, AdvancedFade2DParams p, float time)
{
    Surface2D result = s;
    half useFade = p.enabled >= 0.5h ? 1.0h : 0.0h;
    half useBurn = p.burnEnabled > 0.5h ? 1.0h : 0.0h;
    if (useFade > 0.5h)
    {
        // フェード座標を計算（ウェーブ込み）
        if (p.fadeDirection == FADE_DIR_ALPHA_ONLY)
        {
            half fadeAlpha = 1.0h - saturate(p.fadeAmount);
            result.alpha *= fadeAlpha;
            result.alphaFactor *= fadeAlpha;
        }
        else
        {
            half fadeCoord = ComputeFadeCoord(result.uvLocal, p.fadeDirection, p.waveParamsA, p.waveParamsB, time);
            if (p.fadeDirection == FADE_DIR_CIRCLE)
                fadeCoord = ComputeCircleFadeCoord(result.uvLocal, p, time);
            
            half threshold = saturate(p.fadeAmount);
            half fadeAlpha = ComputeFadeAlphaFromCoord(fadeCoord, threshold, p.soft);
            
            // グロー計算（境界付近で発光）
            if (p.glowIntensity > 0.001h && p.glowRange > 0.0001h)
            {
                // 境界からの距離
                half distFromBoundary = ComputeFadeBoundaryDistance(fadeCoord, threshold, p.fadeDirection);

                // 0 at boundary -> 1 at p.glowRange
                half normalized = saturate(1.0h - (distFromBoundary / p.glowRange));
                half glowCurve = normalized * normalized; // 二乗で落ちる

                // 境界の存在度（fadeAlpha が 0.5 に近いほど 1）
                half boundaryPresence = saturate(1.0h - abs(fadeAlpha - 0.5h) * 2.0h);

                half glowAmount = glowCurve * boundaryPresence;
                half finalGlow = glowAmount * p.glowIntensity;

                half3 glowColor = half3(1, 1, 1);
                result.color = ApplyGlowBlend(result.color, glowColor, finalGlow, p.glowBlendMode);
            }
            
            // アルファにフェードを適用（fadeAlpha=1:完全表示, 0:完全フェード）
            result.alpha *= fadeAlpha;
            result.alphaFactor *= fadeAlpha;
        }
    }

    // Burn dissolve (AdvancedFade2D.Enabled に依存させず単体で動作)
    if (useBurn > 0.5h)
    {
        float burnProgress = saturate(p.burnProgress);
        bool burnFullyVisible = (p.burnInvert <= 0.5 && burnProgress <= 0.0001);
        bool burnFullyHidden = (p.burnInvert <= 0.5 && burnProgress >= 0.9999);
        bool burnInvertFullyVisible = (p.burnInvert > 0.5 && burnProgress >= 0.9999);
        bool burnInvertFullyHidden = (p.burnInvert > 0.5 && burnProgress <= 0.0001);

        if (burnFullyVisible || burnInvertFullyVisible)
            return result;

        if (burnFullyHidden || burnInvertFullyHidden)
        {
            result.alpha = 0.0h;
            result.alphaFactor = 0.0h;
            return result;
        }

        float2 dir = p.burnDirection;
        float len = max(length(dir), 1e-4);
        dir /= len;

        float coord = dot(result.uvLocal, dir);
        AnimatedNoise2DMotionParams burnMotion = AdvancedFade2D_MakeBurnAnimatedNoiseParams(p);
        float n = AnimatedNoise2D_Sample01(result.uvLocal, burnMotion, float2(0.0, 0.0), time);
        float edge = p.burnEdgeWidth;
        // 進行端(0/1)ではノイズを無効化してエッジ残りを防ぐ
        float noiseVis = saturate(burnProgress * (1.0 - burnProgress) * 4.0);
        float v = coord + (n - 0.5) * p.burnNoiseStrength * noiseVis;

        // progress を edge を含む 0..1 区間で正規化（0=完全表示, 1=完全消失）
        float span = 1.0 + edge * 2.0;
        float threshold = -edge + burnProgress * span;

        half burnAlpha = smoothstep(threshold - edge, threshold + edge, v);
        if (p.burnInvert > 0.5h)
            burnAlpha = 1.0h - burnAlpha;

        half edgeMask = saturate(1.0h - abs(v - threshold) / edge);
        // 完全表示/完全非表示の領域ではエッジを消す
        half edgeVis = saturate(burnAlpha * (1.0h - burnAlpha) * 4.0h);
        half edgeAmount = edgeMask * p.burnEdgeColor.a * edgeVis;

        result.color = ApplyBurnBlend(result.color, p.burnEdgeColor.rgb, edgeAmount, p.burnBlendMode);
        result.alpha *= burnAlpha;
        result.alphaFactor *= burnAlpha;
    }
    
    return result;
}

// _Time.y を使用しない簡易版
inline Surface2D Surface2D_ApplyAdvancedFade(Surface2D s, AdvancedFade2DParams p)
{
    return Surface2D_ApplyAdvancedFade(s, p, 0);
}

#endif // GAME_ADVANCED_FADE_2D_INCLUDED
