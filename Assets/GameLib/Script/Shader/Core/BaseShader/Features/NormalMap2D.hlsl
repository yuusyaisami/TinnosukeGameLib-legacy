#ifndef GAME_NORMAL_MAP_2D_INCLUDED
#define GAME_NORMAL_MAP_2D_INCLUDED

// ═══════════════════════════════════════════════════════════════════════════
// NormalMap2D.hlsl - 法線マップ生成エフェクト (v3.0)
// ═══════════════════════════════════════════════════════════════════════════
//
// 仕様書 BaseShader-CompositeSystem-v3.0 準拠
//
// 機能:
//   - ノイズの勾配から法線マップを生成
//   - URP 2D Light に影響を与える
//   - 水面、凹凸表面、布のしわなどをライティングで表現
//
// 注意: この機能は URP 2D Lit シェーダーでのみ有効
//
// ═══════════════════════════════════════════════════════════════════════════

// TextureSlot2D.hlsl は Surface2D.hlsl 経由でインクルード想定

// ---------------------------------------------------------------------------
// NormalMap パラメータ構造体
// ★ BaseShader.shader のプロパティに合わせた構造
// ---------------------------------------------------------------------------
struct NormalMap2DParams
{
    float          enabled;
    TextureSlotRef source;        // 勾配ソース
    half           strength;      // 法線強度
    half3          lightDir;      // ライト方向（ワールド空間）
    half           blendWithBase; // 0= ノイズ法線のみ, 1=ベース法線のみ
};

// ---------------------------------------------------------------------------
// CBUFFER から NormalMap2DParams を生成
// ---------------------------------------------------------------------------
inline NormalMap2DParams MakeNormalMap2DParams(
    float enabled,
    float sourceSlotType,
    float sourceChannel,
    float sourceUVSpace,
    float4 sourceTilingOffset,
    float4 sourceRemap,
    float strength,
    float blendWithBase)
{
    NormalMap2DParams p;
    p.enabled = enabled;
    p.source = MakeTextureSlotRef(
        sourceSlotType,
        sourceChannel,
        sourceUVSpace,
        sourceTilingOffset,
        sourceRemap
    );
    p.strength = strength;
    p.blendWithBase = half(blendWithBase);
    p.lightDir = half3(0.5h, 0.5h, 1.0h);
    return p;
}

// ---------------------------------------------------------------------------
// ★ Simplified: BaseShader.shader の実際のプロパティに合わせた簡易版
// ---------------------------------------------------------------------------
inline NormalMap2DParams MakeNormalMap2DParamsSimple(
    float enabled,
    float sourceSlotType,
    float sourceChannel,
    float sourceUVSpace,
    float4 sourceTilingOffset,
    float4 sourceRemap,
    float strength,
    float3 lightDir,
    float blendWithBase /* 0.0 = noise only */)
{
    NormalMap2DParams p;
    p.enabled = enabled;
    p.source = MakeTextureSlotRef(
        sourceSlotType,
        sourceChannel,
        sourceUVSpace,
        sourceTilingOffset,
        sourceRemap
    );
    p.strength = strength;
    p.blendWithBase = half(blendWithBase);
    p.lightDir = normalize(lightDir);
    return p;
}

// デフォルト値で初期化（無効状態）
inline NormalMap2DParams MakeDefaultNormalMap2DParams()
{
    NormalMap2DParams p;
    p.enabled = 0;
    p.source = MakeDefaultTextureSlotRef();
    p.strength = 1.0h;
    p.lightDir = half3(0.5h, 0.5h, 1.0h);
    p.blendWithBase = 0.0h;
    return p;
}

// ---------------------------------------------------------------------------
// 勾配から法線を計算
// ---------------------------------------------------------------------------
inline half3 ComputeNormalFromGradient(half2 gradient, half strength)
{
    // gradient は [-1,1] (SampleSlotVector2 で変換済み)
    half2 grad = gradient * strength;
    
    // 法線計算（tangent space）
    // Z は常に正（表面を向く）
    half3 normal = normalize(half3(-grad.x, -grad.y, 1.0h));
    
    return normal;
}

// ---------------------------------------------------------------------------
// 法線をブレンド
// ---------------------------------------------------------------------------
inline half3 BlendNormals(half3 n1, half3 n2, half t)
{
    // リオリエンテーション（Reoriented Normal Mapping）
    // 2つの法線を物理的に正しくブレンド
    half3 t1 = half3(n1.xy, n1.z + 1.0h);
    half3 t2 = half3(-n2.xy, n2.z);
    half3 result = t1 * dot(t1, t2) - t2 * t1.z;
    return normalize(lerp(n1, result, t));
}

// ---------------------------------------------------------------------------
// NormalMap 生成（Tangent Space 法線を返す）
// ---------------------------------------------------------------------------
inline half3 NormalMap2D_GetNormal(Surface2D s, NormalMap2DParams p, half3 baseNormal)
{
    if (p.enabled < 0.5h)
        return baseNormal;
    if (p.source.slotType == TEXTURE_SLOT_NONE)
        return baseNormal;
    
    // 勾配サンプル（GB チャンネルを想定）
    half2 gradient = SampleSlotVector2(s, p.source);
    
    // 勾配から法線計算
    half3 noiseNormal = ComputeNormalFromGradient(gradient, p.strength);
    
    // ベース法線とブレンド
    if (p.blendWithBase > 0.001h)
    {
        return BlendNormals(noiseNormal, baseNormal, p.blendWithBase);
    }
    
    return noiseNormal;
}

// ---------------------------------------------------------------------------
// Surface2D に法線情報を持たせる拡張版（将来の 2D Lit 対応用）
// ---------------------------------------------------------------------------
#ifdef SURFACE2D_NORMAL_SUPPORT

struct Surface2DWithNormal
{
    Surface2D base;
    half3     normal;  // Tangent space normal
};

inline Surface2DWithNormal Surface2D_ApplyNormalMap(Surface2DWithNormal s, NormalMap2DParams p)
{
    s.normal = NormalMap2D_GetNormal(s.base, p, s.normal);
    return s;
}

#endif // SURFACE2D_NORMAL_SUPPORT

#endif // GAME_NORMAL_MAP_2D_INCLUDED
