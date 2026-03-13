#ifndef GAME_NOISE_ATLAS_2D_INCLUDED
#define GAME_NOISE_ATLAS_2D_INCLUDED

// ═══════════════════════════════════════════════════════════════════════════
// NoiseAtlas2D.hlsl - Multi-tier Texture2DArray ベースのノイズアトラス
// ═══════════════════════════════════════════════════════════════════════════
// 
// 仕様書 Section 8.1/8.2 準拠:
// - _NoiseEnabled (0/1): ノイズ有効フラグ
// - _NoiseAtlasTier (0-5): Tier インデックス
// - _NoiseSlice: スライスインデックス
// - _NoiseAtlasT0～T5: Global bind された Texture2DArray (tier 別)
//
// Tier 解像度:
//   0: 16×16   | 1: 32×32   | 2: 64×64
//   3: 128×128 | 4: 256×256 | 5: 512×512
//
// ═══════════════════════════════════════════════════════════════════════════
// 【重要】int 丸め・クランプに関する注意
// ═══════════════════════════════════════════════════════════════════════════
// Material の _NoiseAtlas/_NoiseSlice は float 型で宣言されている。
// これは Unity の MaterialPropertyBlock や Animation との互換性のため。
// 
// ただし、以下の罠に注意:
//   1. Animation/Timeline で補間されると中間値（0.5, 1.7 など）になる
//   2. C# 側で必ず int に丸めてクランプすること
//   3. シェーダー側では (int) キャストで切り捨てが行われる
//
// C# 側での推奨パターン:
//   int tier = Mathf.Clamp(Mathf.RoundToInt(tierFloat), 0, 5);
//   int slice = Mathf.Clamp(Mathf.RoundToInt(sliceFloat), 0, maxSlices - 1);
// ═══════════════════════════════════════════════════════════════════════════

// ---------------------------------------------------------------------------
// Texture2DArray declarations (Global bind)
// WebGL safe mode では atlas sampling 自体を無効化する。
// ---------------------------------------------------------------------------
#if !defined(SURFACE2D_WEBGL_SAFE)
TEXTURE2D_ARRAY(_NoiseAtlasT0);
SAMPLER(sampler_NoiseAtlasT0);

TEXTURE2D_ARRAY(_NoiseAtlasT1);
SAMPLER(sampler_NoiseAtlasT1);

TEXTURE2D_ARRAY(_NoiseAtlasT2);
SAMPLER(sampler_NoiseAtlasT2);

TEXTURE2D_ARRAY(_NoiseAtlasT3);
SAMPLER(sampler_NoiseAtlasT3);

TEXTURE2D_ARRAY(_NoiseAtlasT4);
SAMPLER(sampler_NoiseAtlasT4);

TEXTURE2D_ARRAY(_NoiseAtlasT5);
SAMPLER(sampler_NoiseAtlasT5);
#endif

// ---------------------------------------------------------------------------
// Per-material / Per-renderer uniforms
// ---------------------------------------------------------------------------
// NOTE: These are expected to be set via MaterialPropertyBlock or MaterialFx Layer system
//       _NoiseEnabled: 0 = disabled, 1 = enabled
//       _NoiseAtlasTier: tier index (0-5)
//       _NoiseSlice: slice index within the tier's Texture2DArray

// ---------------------------------------------------------------------------
// NoiseUVSpace - ノイズサンプリングのUV空間選択
// ---------------------------------------------------------------------------
// 【重要】SpriteAtlas 使用時の問題:
//   uvMain はアトラス上の UV になるため、同じスプライトでも
//   アトラスの詰め方によってノイズの見え方が変わる。
//   デフォルトは SpriteLocal (0..1) に正規化するべき。
// ---------------------------------------------------------------------------
#define NOISE_UV_SPACE_SPRITE_LOCAL 0  // スプライトローカルUV (0..1)
#define NOISE_UV_SPACE_SCREEN       1  // スクリーンUV (0..1)
#define NOISE_UV_SPACE_ATLAS_RAW    2  // アトラスUVそのまま（SpriteAtlas依存）
#define NOISE_UV_SPACE_WORLD_XY     3  // ワールド座標XY

// ---------------------------------------------------------------------------
// NoiseOutputMode - Compute側の出力モード（BaseShader側の解釈用）
// ---------------------------------------------------------------------------
// 【重要】Compute側のOUTPUT_MODE_*と同じ値を使用すること
#define NOISE_OUTPUT_SCALAR        0  // R=グレースケール, GBA=unused
#define NOISE_OUTPUT_GRADIENT      1  // R=scalar, G=dN/dx, B=dN/dy, A=1
#define NOISE_OUTPUT_GRADIENT_FULL 2  // RG=勾配, B=cellId, A=edgeMask
#define NOISE_OUTPUT_CURL          3  // RG=curl(x,y), B=scalar, A=1
#define NOISE_OUTPUT_CELL_INFO     4  // R=scalar, G=cellId, B=edgeMask, A=1

// ---------------------------------------------------------------------------
// NoiseAtlas2D Context
// ---------------------------------------------------------------------------
struct NoiseAtlas2DParams
{
    float enabled;      // 0 or 1
    int   tier;         // 0-5 (クランプ済み)
    int   slice;        // slice index (クランプ済み)
    int   uvSpace;      // NOISE_UV_SPACE_*
    int   outputMode;   // NOISE_OUTPUT_*
};

/// <summary>
/// NoiseAtlas2DParams を生成。
/// tier/slice は安全に丸め・クランプされる。
/// </summary>
inline NoiseAtlas2DParams MakeNoiseAtlas2DParams(
    float noiseEnabled,
    float noiseAtlasTier,
    float noiseSlice,
    float noiseUVSpace,
    float noiseOutputMode)
{
    NoiseAtlas2DParams p;
    p.enabled = noiseEnabled;
    // 【安全な丸め・クランプ】
    // round で四捨五入し、クランプで範囲外アクセスを防止
    p.tier    = clamp((int)round(noiseAtlasTier), 0, 5);
    p.slice   = max((int)round(noiseSlice), 0);  // 上限はTier依存なのでここでは下限のみ
    p.uvSpace = clamp((int)round(noiseUVSpace), 0, 3);
    p.outputMode = clamp((int)round(noiseOutputMode), 0, 4);
    return p;
}

/// <summary>
/// 後方互換用: uvSpace/outputMode なし版（デフォルト: SpriteLocal, Scalar）
/// </summary>
inline NoiseAtlas2DParams MakeNoiseAtlas2DParams(float noiseEnabled, float noiseAtlasTier, float noiseSlice)
{
    return MakeNoiseAtlas2DParams(noiseEnabled, noiseAtlasTier, noiseSlice,
                                   (float)NOISE_UV_SPACE_SPRITE_LOCAL,
                                   (float)NOISE_OUTPUT_SCALAR);
}

// ---------------------------------------------------------------------------
// Sample functions (仕様書 Section 8.2)
// ---------------------------------------------------------------------------

/// <summary>
/// Tier を指定して NoiseAtlas からサンプリング。
/// Tier ごとに別々の Texture2DArray を参照するため、switch で分岐。
/// slice は各 Tier の maxSlices でクランプされる。
/// </summary>
inline half4 SampleNoiseAtlasByTier(float2 uv, int tier, int slice)
{
#if defined(SURFACE2D_WEBGL_SAFE)
    return half4(0.5, 0.5, 0.5, 1.0);
#else
    // 【安全な slice クランプ】
    // Tier ごとの maxSlices は C# 側で管理されるが、
    // シェーダー側でも安全のため下限クランプ
    slice = max(slice, 0);
    
    [branch]
    switch (tier)
    {
        case 0:
            return SAMPLE_TEXTURE2D_ARRAY(_NoiseAtlasT0, sampler_NoiseAtlasT0, uv, slice);
        case 1:
            return SAMPLE_TEXTURE2D_ARRAY(_NoiseAtlasT1, sampler_NoiseAtlasT1, uv, slice);
        case 2:
            return SAMPLE_TEXTURE2D_ARRAY(_NoiseAtlasT2, sampler_NoiseAtlasT2, uv, slice);
        case 3:
            return SAMPLE_TEXTURE2D_ARRAY(_NoiseAtlasT3, sampler_NoiseAtlasT3, uv, slice);
        case 4:
            return SAMPLE_TEXTURE2D_ARRAY(_NoiseAtlasT4, sampler_NoiseAtlasT4, uv, slice);
        case 5:
        default:
            return SAMPLE_TEXTURE2D_ARRAY(_NoiseAtlasT5, sampler_NoiseAtlasT5, uv, slice);
    }
#endif
}

/// <summary>
/// NoiseAtlas をサンプリング（Params 版）。
/// enabled が 0 の場合はグレー (0.5, 0.5, 0.5, 1) を返す。
/// </summary>
inline half4 SampleNoiseAtlas(float2 uv, NoiseAtlas2DParams params)
{
    if (params.enabled < 0.5)
    {
        return half4(0.5, 0.5, 0.5, 1.0);
    }
    return SampleNoiseAtlasByTier(uv, params.tier, params.slice);
}

/// <summary>
/// NoiseAtlas をサンプリング（直接パラメータ版）。
/// </summary>
inline half4 SampleNoiseAtlasDirect(float2 uv, float noiseEnabled, float noiseAtlasTier, float noiseSlice)
{
    if (noiseEnabled < 0.5)
    {
        return half4(0.5, 0.5, 0.5, 1.0);
    }
    return SampleNoiseAtlasByTier(uv, (int)noiseAtlasTier, (int)noiseSlice);
}

// ---------------------------------------------------------------------------
// Utility: Get tier resolution
// ---------------------------------------------------------------------------
inline float2 GetNoiseAtlasTierResolution(int tier)
{
    // Tier 0: 16, 1: 32, 2: 64, 3: 128, 4: 256, 5: 512
    float res = (float)(16 << tier);
    return float2(res, res);
}

inline float2 GetNoiseAtlasTexelSize(int tier)
{
    float res = (float)(16 << tier);
    return float2(1.0 / res, 1.0 / res);
}

#endif // GAME_NOISE_ATLAS_2D_INCLUDED
