#ifndef GAME_EMISSION_2D_INCLUDED
#define GAME_EMISSION_2D_INCLUDED

// ═══════════════════════════════════════════════════════════════════════════
// Emission2D.hlsl - 発光エフェクト (v2.0)
// ═══════════════════════════════════════════════════════════════════════════
//
// 仕様書 BaseShader-CompositeSystem-v2.0 Part 3.5 準拠
//
// 機能:
//   - TextureSlot からマスク値を読み取り（オプション）
//   - EmissionColor と乗算して発光を追加
//   - ソースが None の場合は全面に発光
//
// ═══════════════════════════════════════════════════════════════════════════

// TextureSlot2D.hlsl は Surface2D.hlsl 経由でインクルード想定

// ---------------------------------------------------------------------------
// Emission パラメータ構造体
// ---------------------------------------------------------------------------
struct Emission2DParams
{
    float          enabled;
    TextureSlotRef source;
    half3          color;
    float          intensity;
};

// ---------------------------------------------------------------------------
// CBUFFER から Emission2DParams を生成
// ---------------------------------------------------------------------------
inline Emission2DParams MakeEmission2DParams(
    float enabled,
    float sourceSlotType,
    float sourceChannel,
    float sourceUVSpace,
    float4 sourceTilingOffset,
    float4 sourceRemap,
    float4 emissionColor)
{
    Emission2DParams p = (Emission2DParams)0;
    p.enabled = enabled;
    p.source = MakeTextureSlotRef(
        sourceSlotType,
        sourceChannel,
        sourceUVSpace,
        sourceTilingOffset,
        sourceRemap
    );
    p.color = emissionColor.rgb;
    p.intensity = emissionColor.a;
    return p;
}

// デフォルト値で初期化（無効状態）
inline Emission2DParams MakeDefaultEmission2DParams()
{
    Emission2DParams p = (Emission2DParams)0;
    p.enabled = 0;
    p.source = MakeDefaultTextureSlotRef();
    p.color = half3(1, 1, 1);
    p.intensity = 1;
    return p;
}

// ---------------------------------------------------------------------------
// Emission 適用
// ★v2.0: binding 引数なし - 内部で自動解決
// ---------------------------------------------------------------------------
inline Surface2D Surface2D_ApplyEmission(Surface2D s, Emission2DParams p)
{
    if (p.enabled < 0.5)
        return s;
    
    // ソースが指定されている場合はマスクとして使用
    half emissionMask = 1.0;
    if (p.source.slotType != TEXTURE_SLOT_NONE)
    {
        emissionMask = SampleSlotScalar(s, p.source);
    }
    
    // 発光を追加
    s.color += p.color * emissionMask * p.intensity;
    
    return s;
}

// ---------------------------------------------------------------------------
// Emission 適用（アルファ考慮版）
// アルファが残っている部分のみ発光
// ---------------------------------------------------------------------------
inline Surface2D Surface2D_ApplyEmissionWithAlpha(Surface2D s, Emission2DParams p)
{
    if (p.enabled < 0.5)
        return s;
    if (s.alpha < 0.001)
        return s;
    
    half emissionMask = 1.0;
    if (p.source.slotType != TEXTURE_SLOT_NONE)
    {
        emissionMask = SampleSlotScalar(s, p.source);
    }
    
    // アルファを考慮した発光
    s.color += p.color * emissionMask * p.intensity * s.alpha;
    
    return s;
}

#endif // GAME_EMISSION_2D_INCLUDED
