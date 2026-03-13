#ifndef GAME_RIPPLE_2D_INCLUDED
#define GAME_RIPPLE_2D_INCLUDED

// ═══════════════════════════════════════════════════════════════════════════
// Ripple2D.hlsl - 波紋エフェクト (v3.0)
// ═══════════════════════════════════════════════════════════════════════════
//
// 仕様書 BaseShader-CompositeSystem-v3.0 準拠
//
// 機能:
//   - 中心から広がる同心円状の波紋エフェクト
//   - 着弾、衝撃波、水面への落下などに使用
//   - UV歪みと色変調の両方をサポート
//   - 距離減衰と時間減衰
//
// ═══════════════════════════════════════════════════════════════════════════

// TextureSlot2D.hlsl は Surface2D.hlsl 経由でインクルード想定

// ---------------------------------------------------------------------------
// Ripple パラメータ構造体
// ★ BaseShader.shader のプロパティに合わせた構造
// ---------------------------------------------------------------------------
struct Ripple2DParams
{
    float          enabled;
    
    // 波紋中心（ローカル UV 座標）
    half2          center;
    
    // 波紋パラメータ (_RippleWaveParams: x=freq, y=speed, z=decay)
    half           frequency;     // 周波数
    half           speed;         // アニメーション速度
    half           decay;         // 距離減衰
    half           amplitude;     // 振幅
    half           phase;         // 位相オフセット（アニメーション用）
    
    // 歪み・色ブレンド
    int            distortEnabled; // UV歪みを有効化
    half           colorBlend;     // 色ブレンド強度
    half3          rippleColor;    // 波紋の色
    half           rippleAlpha;    // 波紋の不透明度
};

// ---------------------------------------------------------------------------
// CBUFFER から Ripple2DParams を生成（旧式：互換性維持）
// ---------------------------------------------------------------------------
inline Ripple2DParams MakeRipple2DParams(
    float enabled,
    float2 center,
    float radius,
    float wavelength,
    float waveCount,
    float thickness,
    float amplitude,
    float falloff,
    float distortEnabled,
    float colorEnabled,
    float4 rippleColor)
{
    Ripple2DParams p = (Ripple2DParams)0;
    p.enabled = enabled;
    p.center = center;
    p.frequency = 6.28318h / max(wavelength, 0.01h);
    p.speed = 1.0h;
    p.decay = falloff;
    p.amplitude = amplitude;
    p.phase = radius;  // radius を phase として使用
    p.distortEnabled = (int)round(distortEnabled);
    p.colorBlend = (colorEnabled > 0.5h) ? 1.0h : 0.0h;
    p.rippleColor = rippleColor.rgb;
    p.rippleAlpha = rippleColor.a;
    return p;
}

// ---------------------------------------------------------------------------
// ★ Simplified: BaseShader.shader の実際のプロパティに合わせた簡易版
// ---------------------------------------------------------------------------
inline Ripple2DParams MakeRipple2DParamsSimple(
    float enabled,
    float2 center,
    float4 waveParams,       // x=freq, y=speed, z=decay, w=0
    float amplitude,
    float phase,
    float distortUV,
    float colorBlend,
    float4 rippleColor)
{
    Ripple2DParams p = (Ripple2DParams)0;
    p.enabled = enabled;
    p.center = center;
    p.frequency = waveParams.x;
    p.speed = waveParams.y;
    p.decay = waveParams.z;
    p.amplitude = amplitude;
    p.phase = phase;
    p.distortEnabled = (distortUV > 0.5h) ? 1 : 0;
    p.colorBlend = colorBlend;
    p.rippleColor = rippleColor.rgb;
    p.rippleAlpha = rippleColor.a;
    return p;
}

// デフォルト値で初期化（無効状態）
inline Ripple2DParams MakeDefaultRipple2DParams()
{
    Ripple2DParams p = (Ripple2DParams)0;
    p.enabled = 0;
    p.center = half2(0.5, 0.5);
    p.frequency = 10.0h;
    p.speed = 2.0h;
    p.decay = 3.0h;
    p.amplitude = 0.02h;
    p.phase = 0.0h;
    p.distortEnabled = 1;
    p.colorBlend = 0.0h;
    p.rippleColor = half3(1, 1, 1);
    p.rippleAlpha = 0.8h;
    return p;
}

// ---------------------------------------------------------------------------
// 波紋強度計算
// ★ 修正: 新しいパラメータ構造に対応
// ---------------------------------------------------------------------------
inline half ComputeRipple(float2 uv, Ripple2DParams p, float time)
{
    float2 delta = uv - p.center;
    float dist = length(delta);
    
    // 波紋関数: sin(距離 * 周波数 - 時間 * 速度 + 位相)
    float wave = sin(dist * p.frequency - time * p.speed + p.phase);
    
    // 距離減衰
    half attenuation = exp(-dist * p.decay);
    
    // 波形を滑らかに（-1,1 → 0,1）
    half waveNorm = wave * 0.5h + 0.5h;
    
    return waveNorm * attenuation * p.amplitude;
}

// ---------------------------------------------------------------------------
// Ripple UV 歪み適用
// ★ 修正: 時間パラメータ追加
// ---------------------------------------------------------------------------
inline float2 Ripple2D_WarpUV(float2 uvMain, float2 uvLocal, Ripple2DParams p, float time)
{
    if (p.enabled < 0.5h)
        return uvMain;
    if (p.distortEnabled < 1)
        return uvMain;
    
    float2 delta = uvLocal - p.center;
    float dist = length(delta);
    
    // 波紋の勾配（歪み方向）
    float wave = sin(dist * p.frequency - time * p.speed + p.phase);
    half attenuation = exp(-dist * p.decay);
    
    if (dist > 0.001h && abs(wave * attenuation) > 0.001h)
    {
        float2 dir = normalize(delta);
        float offset = wave * attenuation * p.amplitude;
        uvMain += dir * offset;
    }
    
    return uvMain;
}

// 互換性のため引数なし版を残す
inline float2 Ripple2D_WarpUV(float2 uvMain, float2 uvLocal, Ripple2DParams p)
{
    return Ripple2D_WarpUV(uvMain, uvLocal, p, 0.0);
}

// ---------------------------------------------------------------------------
// Ripple 色変調適用
// ★ 修正: 新しいパラメータ構造に対応
// ---------------------------------------------------------------------------
inline Surface2D Surface2D_ApplyRipple(Surface2D s, Ripple2DParams p)
{
    if (p.enabled < 0.5h)
        return s;
    if (p.colorBlend < 0.001h)
        return s;
    
    half ripple = ComputeRipple(s.uvLocal, p, _Time.y);
    
    // 色変調
    half t = ripple * p.colorBlend * p.rippleAlpha;
    s.color = lerp(s.color, p.rippleColor, t);
    
    return s;
}

// ---------------------------------------------------------------------------
// Ripple UV + 色変調の一括適用
// ---------------------------------------------------------------------------
inline Surface2D Surface2D_ApplyRippleFull(Surface2D s, Ripple2DParams p, float time)
{
    if (p.enabled < 0.5h)
        return s;
    
    half ripple = ComputeRipple(s.uvLocal, p, time);
    
    // 色変調
    if (p.colorBlend > 0.001h)
    {
        half t = ripple * p.colorBlend * p.rippleAlpha;
        s.color = lerp(s.color, p.rippleColor, t);
    }
    
    return s;
}

#endif // GAME_RIPPLE_2D_INCLUDED
