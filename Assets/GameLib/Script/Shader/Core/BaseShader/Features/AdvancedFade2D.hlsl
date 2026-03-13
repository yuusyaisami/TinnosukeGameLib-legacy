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

// グロー ブレンドモード
#define GLOW_BLEND_ADD      0
#define GLOW_BLEND_SCREEN   1
#define GLOW_BLEND_OVERLAY  2

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
    float burnEnabled,
    float burnProgress,
    float burnEdgeWidth,
    float burnNoiseScale,
    float burnNoiseStrength,
    float burnNoiseType,
    float2 burnDirection,
    float4 burnEdgeColor,
    float burnBlendMode,
    float burnInvert)
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
    return p;
}

inline float Hash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

inline float NoiseValueSmooth(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = Hash21(i);
    float b = Hash21(i + float2(1, 0));
    float c = Hash21(i + float2(0, 1));
    float d = Hash21(i + float2(1, 1));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

inline float2 GetSpriteAspectScale()
{
    float2 uvSize = max(_SpriteUVRect.zw - _SpriteUVRect.xy, float2(1e-4, 1e-4));
    float2 texWH = max(_MainTex_TexelSize.zw, float2(1.0, 1.0));
    float2 pxSize = uvSize * texWH;
    float aspect = pxSize.x / max(pxSize.y, 1e-4);
    if (aspect >= 1.0)
        return float2(aspect, 1.0);
    return float2(1.0, 1.0 / max(aspect, 1e-4));
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
                half3 overlay;
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
    half coord = 0;
    half waveOffset = 0;
    
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
                half3 overlay;
                overlay.r = base.r < 0.5h ? 2.0h * base.r * glowColor.r : 1.0h - 2.0h * (1.0h - base.r) * (1.0h - glowColor.r);
                overlay.g = base.g < 0.5h ? 2.0h * base.g * glowColor.g : 1.0h - 2.0h * (1.0h - base.g) * (1.0h - glowColor.g);
                overlay.b = base.b < 0.5h ? 2.0h * base.b * glowColor.b : 1.0h - 2.0h * (1.0h - base.b) * (1.0h - glowColor.b);
                return lerp(base, overlay, glowAmount);
            }
            
        default:
            return base + glowColor * glowAmount;
    }
}

// ---------------------------------------------------------------------------
// AdvancedFade2D 適用
// ---------------------------------------------------------------------------
inline Surface2D Surface2D_ApplyAdvancedFade(Surface2D s, AdvancedFade2DParams p, float time)
{
    half useFade = p.enabled >= 0.5h ? 1.0h : 0.0h;
    half useBurn = p.burnEnabled > 0.5h ? 1.0h : 0.0h;
    if (useFade < 0.5h && useBurn < 0.5h)
        return s;
    
    if (useFade > 0.5h)
    {
        // フェード座標を計算（ウェーブ込み）
        if (p.fadeDirection == FADE_DIR_ALPHA_ONLY)
        {
            half fadeAlpha = 1.0h - saturate(p.fadeAmount);
            s.alpha *= fadeAlpha;
            s.alphaFactor *= fadeAlpha;
        }
        else
        {
            half fadeCoord = ComputeFadeCoord(s.uvLocal, p.fadeDirection, p.waveParamsA, p.waveParamsB, time);
            
            // フェード閾値（fadeAmount=0 で完全表示、1 で完全フェード）
            half threshold = saturate(p.fadeAmount);
            
            // ソフトネスに基づいて smoothstep（端点で完全表示/非表示を保証する）
            half halfSoft = saturate(p.soft) * 0.5h;
            half edgeMin = max(0.0h, threshold - halfSoft);
            half edgeMax = min(1.0h, threshold + halfSoft);

            half fadeAlpha = 1.0h;
            if (threshold <= 0.0001h)
            {
                // 完全表示
                fadeAlpha = 1.0h;
            }
            else if (threshold >= 0.9999h)
            {
                // 完全フェード
                fadeAlpha = 0.0h;
            }
            else if (edgeMax - edgeMin <= 0.00001h)
            {
                // soft==0 のときは step
                fadeAlpha = fadeCoord >= threshold ? 1.0h : 0.0h;
            }
            else
            {
                // wave で値が範囲外になるため saturate しておく
                fadeAlpha = smoothstep(edgeMin, edgeMax, saturate(fadeCoord));
            }
            
            // グロー計算（境界付近で発光）
            if (p.glowIntensity > 0.001h && p.glowRange > 0.0001h)
            {
                // 境界からの距離
                half distFromBoundary = abs(fadeCoord - threshold);

                // 0 at boundary -> 1 at p.glowRange
                half normalized = saturate(1.0h - (distFromBoundary / p.glowRange));
                half glowCurve = normalized * normalized; // 二乗で落ちる

                // 境界の存在度（fadeAlpha が 0.5 に近いほど 1）
                half boundaryPresence = saturate(1.0h - abs(fadeAlpha - 0.5h) * 2.0h);

                half glowAmount = glowCurve * boundaryPresence;
                half finalGlow = glowAmount * p.glowIntensity;

                half3 glowColor = half3(1, 1, 1);
                s.color = ApplyGlowBlend(s.color, glowColor, finalGlow, p.glowBlendMode);
            }
            
            // アルファにフェードを適用（fadeAlpha=1:完全表示, 0:完全フェード）
            s.alpha *= fadeAlpha;
            s.alphaFactor *= fadeAlpha;
        }
    }

    // Burn dissolve (AdvancedFade2D.Enabled に依存させず単体で動作)
    if (useBurn > 0.5h)
    {
        float2 dir = p.burnDirection;
        float len = max(length(dir), 1e-4);
        dir /= len;

        float coord = dot(s.uvLocal, dir);
        float2 noiseUV = s.uvLocal * GetSpriteAspectScale() * p.burnNoiseScale;
        float n = (p.burnNoiseType == 0) ? NoiseValueSmooth(noiseUV) : Hash21(noiseUV);
        float edge = p.burnEdgeWidth;
        // 進行端(0/1)ではノイズを無効化してエッジ残りを防ぐ
        float noiseVis = saturate(p.burnProgress * (1.0 - p.burnProgress) * 4.0);
        float v = coord + (n - 0.5) * p.burnNoiseStrength * noiseVis;

        // progress を edge を含む 0..1 区間で正規化（0=完全表示, 1=完全消失）
        float span = 1.0 + edge * 2.0;
        float threshold = -edge + p.burnProgress * span;

        half burnAlpha = smoothstep(threshold - edge, threshold + edge, v);
        if (p.burnInvert > 0.5h)
            burnAlpha = 1.0h - burnAlpha;

        half edgeMask = saturate(1.0h - abs(v - threshold) / edge);
        // 完全表示/完全非表示の領域ではエッジを消す
        half edgeVis = saturate(burnAlpha * (1.0h - burnAlpha) * 4.0h);
        half edgeAmount = edgeMask * p.burnEdgeColor.a * edgeVis;

        s.color = ApplyBurnBlend(s.color, p.burnEdgeColor.rgb, edgeAmount, p.burnBlendMode);
        s.alpha *= burnAlpha;
        s.alphaFactor *= burnAlpha;
    }
    
    return s;
}

// _Time.y を使用しない簡易版
inline Surface2D Surface2D_ApplyAdvancedFade(Surface2D s, AdvancedFade2DParams p)
{
    return Surface2D_ApplyAdvancedFade(s, p, 0);
}

#endif // GAME_ADVANCED_FADE_2D_INCLUDED
