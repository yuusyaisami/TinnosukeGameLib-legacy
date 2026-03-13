#ifndef GAME_TEXTURE_SLOT_2D_INCLUDED
#define GAME_TEXTURE_SLOT_2D_INCLUDED

// ═══════════════════════════════════════════════════════════════════════════
// TextureSlot2D.hlsl - 統一テクスチャソースサンプリング (v2.0)
// ═══════════════════════════════════════════════════════════════════════════
// 
// 仕様書 BaseShader-CompositeSystem-v2.0 準拠
// 
// Slot Pool 構成:
//   Slot 0-4: Atlas Slot (Tier/Slice は _AtlasSlotN で動的バインド)
//   Slot 5:   External Texture A (_ExtTexA)
//   Slot 6:   External Texture B (_ExtTexB)
//   Slot 7:   Custom RenderTexture (_CustomRT)
//
// v2.0 変更点:
//   - binding 引数を削除
//   - SlotType から ResolveAtlasSlotBinding で自動解決
//   - 旧互換ラッパー (SampleSlotRawWithBinding 等) を削除
//
// ═══════════════════════════════════════════════════════════════════════════

#include "Assets/GameLib/Script/Shader/Core/BaseShader/Features/NoiseAtlas2D.hlsl"

// ---------------------------------------------------------------------------
// Slot Type 定義 (C# の TextureSlotType と同期)
// ---------------------------------------------------------------------------
#define TEXTURE_SLOT_NONE        -1
#define TEXTURE_SLOT_ATLAS_0      0
#define TEXTURE_SLOT_ATLAS_1      1
#define TEXTURE_SLOT_ATLAS_2      2
#define TEXTURE_SLOT_ATLAS_3      3
#define TEXTURE_SLOT_ATLAS_4      4
#define TEXTURE_SLOT_EXTERNAL_A   5
#define TEXTURE_SLOT_EXTERNAL_B   6
#define TEXTURE_SLOT_CUSTOM_RT    7

// ---------------------------------------------------------------------------
// Channel Mask 定義 (C# の ChannelMask と同期)
// ---------------------------------------------------------------------------
#define CHANNEL_R      1
#define CHANNEL_G      2
#define CHANNEL_B      4
#define CHANNEL_A      8
#define CHANNEL_RG     3   // R+G = 1+2
#define CHANNEL_RB     5   // R+B = 1+4
#define CHANNEL_RA     9   // R+A = 1+8
#define CHANNEL_GB     6   // G+B = 2+4
#define CHANNEL_GA    10   // G+A = 2+8
#define CHANNEL_BA    12   // B+A = 4+8
#define CHANNEL_RGB    7
#define CHANNEL_RGBA  15

// ---------------------------------------------------------------------------
// UV Space 定義 (NoiseAtlas2D.hlsl と共有)
// ---------------------------------------------------------------------------
// NOISE_UV_SPACE_* は NoiseAtlas2D.hlsl で定義済み
// 0: SpriteLocal, 1: Screen, 2: AtlasRaw, 3: WorldXY

// ---------------------------------------------------------------------------
// External Texture 宣言
// ---------------------------------------------------------------------------
TEXTURE2D(_ExtTexA);
SAMPLER(sampler_ExtTexA);

TEXTURE2D(_ExtTexB);
SAMPLER(sampler_ExtTexB);

TEXTURE2D(_CustomRT);
SAMPLER(sampler_CustomRT);

// ---------------------------------------------------------------------------
// TextureSlotRef 構造体
// 注意: sliceIndex は持たない。Slot の (Tier, Slice) は _AtlasSlotN で決定。
// ---------------------------------------------------------------------------
struct TextureSlotRef
{
    int    slotType;      // TEXTURE_SLOT_*
    int    channelMask;   // CHANNEL_*
    int    uvSpace;       // NOISE_UV_SPACE_*
    float4 tilingOffset;  // xy=tiling, zw=offset
    float4 remap;         // x=bias, y=gain, z=gamma, w=invert
};

// ---------------------------------------------------------------------------
// TextureSlotRef 生成（CBUFFER からの読み取りヘルパー）
// ---------------------------------------------------------------------------
inline TextureSlotRef MakeTextureSlotRef(
    float slotType,
    float channelMask,
    float uvSpace,
    float4 tilingOffset,
    float4 remap)
{
    TextureSlotRef ref = (TextureSlotRef)0;
    ref.slotType     = (int)round(slotType);
    ref.channelMask  = (int)round(channelMask);
    ref.uvSpace      = clamp((int)round(uvSpace), 0, 3);
    ref.tilingOffset = tilingOffset;
    ref.remap        = remap;
    return ref;
}

// デフォルト値で初期化
inline TextureSlotRef MakeDefaultTextureSlotRef()
{
    TextureSlotRef ref = (TextureSlotRef)0;
    ref.slotType     = TEXTURE_SLOT_NONE;
    ref.channelMask  = CHANNEL_R;
    ref.uvSpace      = NOISE_UV_SPACE_SPRITE_LOCAL;
    ref.tilingOffset = float4(1, 1, 0, 0);
    ref.remap        = float4(0.5, 0.5, 1, 0);  // bias=0.5, gain=0.5, gamma=1, invert=0
    return ref;
}

// ---------------------------------------------------------------------------
// Slot から (Tier, Slice) を解決
// ★v2.0 の核心: 各 Feature はこの関数を経由して Tier/Slice を取得
// ★修正: 切り捨てではなく round + clamp を使用し、Animation/補間値に対応
// ---------------------------------------------------------------------------
inline int2 ResolveAtlasSlotBinding(int slotType)
{
    float2 v = float2(-1.0, -1.0);
    if (slotType == TEXTURE_SLOT_ATLAS_0) v = _AtlasSlot0.xy;
    else if (slotType == TEXTURE_SLOT_ATLAS_1) v = _AtlasSlot1.xy;
    else if (slotType == TEXTURE_SLOT_ATLAS_2) v = _AtlasSlot2.xy;
    else if (slotType == TEXTURE_SLOT_ATLAS_3) v = _AtlasSlot3.xy;
    else if (slotType == TEXTURE_SLOT_ATLAS_4) v = _AtlasSlot4.xy;
    
    // -1 は無効のまま通す（デフォルト(-1,-1)設計と整合）
    if (v.x < 0.0 || v.y < 0.0)
        return int2(-1, -1);
    
    // round + clamp で安全に整数化（補間/アニメーション値対応）
    int tier  = clamp((int)round(v.x), 0, 5);
    int slice = max((int)round(v.y), 0);
    return int2(tier, slice);
}

// ---------------------------------------------------------------------------
// UV 計算（UVSpace に応じて）
// ---------------------------------------------------------------------------
// ★v2.1 修正: Atlas Slot (0-4) サンプリング時は常に uvLocal を使用
// Multiple スプライトでもフレームごとに同じノイズパターンを適用するため
// ---------------------------------------------------------------------------
inline float2 ComputeSlotUV(
    float2 uvLocal,
    float2 uvMain,
    float2 screenUV,
    int uvSpace,
    float4 tilingOffset,
    int slotType)
{
    float2 baseUV;
    
    // ★ Atlas Slot (0-4) の場合は常に uvLocal を使用
    // Multiple スプライト対応: フレームごとにノイズがずれないようにする
    bool isAtlasSlot = (slotType >= TEXTURE_SLOT_ATLAS_0 && slotType <= TEXTURE_SLOT_ATLAS_4);
    if (isAtlasSlot)
    {
        // Atlas Slot は常にスプライトローカル UV を使用
        // 一部の SpriteAtlas/フィルタリング条件で uvLocal がわずかに範囲外になることがあり、
        // その場合にサンプルが 0 付近へ落ちて Mask/Dissolve が消えるためクランプする。
        baseUV = saturate(uvLocal);
    }
    else
    {
        // External Texture / CustomRT は従来通り UVSpace に従う
        [branch]
        switch (uvSpace)
        {
            case NOISE_UV_SPACE_SPRITE_LOCAL:
                baseUV = uvLocal;
                break;
            case NOISE_UV_SPACE_SCREEN:
                baseUV = screenUV;
                break;
            case NOISE_UV_SPACE_ATLAS_RAW:
                baseUV = uvMain;
                break;
            case NOISE_UV_SPACE_WORLD_XY:
            default:
                // WorldXY は Varyings 経由で渡す必要があるため、uvLocal にフォールバック
                baseUV = uvLocal;
                break;
        }
    }
    
    // Tiling & Offset 適用
    float2 uvOut = baseUV * tilingOffset.xy + tilingOffset.zw;

    // Atlas Slot 用の安全なクランプ: テクスチャの境界でサンプリングが外側に落ちると
    // マスク系が 0 になりやすいため、厳密な 0/1 を避ける少しのマージンを入れる。
    bool isAtlasSlotOut = (slotType >= TEXTURE_SLOT_ATLAS_0 && slotType <= TEXTURE_SLOT_ATLAS_4);
    if (isAtlasSlotOut)
    {
        const float kEps = 1e-5f;
        uvOut = clamp(uvOut, float2(kEps, kEps), float2(1.0f - kEps, 1.0f - kEps));
    }
    return uvOut;
}

// Surface2D 構造体からの UV 計算
// ★v2.1: slotType を渡して Atlas Slot 判定を行う
inline float2 ComputeSlotUVFromSurface(Surface2D s, TextureSlotRef ref)
{
    return ComputeSlotUV(s.uvLocal, s.uvMain, s.screenUV, ref.uvSpace, ref.tilingOffset, ref.slotType);
}

// ---------------------------------------------------------------------------
// Remap 適用
// ---------------------------------------------------------------------------
inline half ApplySlotRemap(half value, float4 remap)
{
    half v = value;
    
    // Bias (0.5 = no change)
    float bias = remap.x;
    if (abs(bias - 0.5) > 1e-4)
    {
        // S-curve bias
        v = v / ((1.0 / max(bias, 1e-4) - 2.0) * (1.0 - v) + 1.0);
    }
    
    // Gain (0.5 = no change)
    float gain = remap.y;
    if (abs(gain - 0.5) > 1e-4)
    {
        float invGain = 1.0 / max(gain, 1e-4);
        // Use abs() to avoid pow with negative base warning
        v = (v < 0.5)
            ? 0.5 * pow(abs(2.0 * v), invGain)
            : 1.0 - 0.5 * pow(abs(2.0 * (1.0 - v)), invGain);
    }
    
    // Gamma (1.0 = no change)
    float gamma = remap.z;
    if (abs(gamma - 1.0) > 1e-4)
    {
        v = pow(max(v, 0), gamma);
    }
    
    // Invert
    if (remap.w > 0.5)
    {
        v = 1.0 - v;
    }
    
    return saturate(v);
}

// ---------------------------------------------------------------------------
// スカラー取り出し（ChannelMask に応じて）
// ★修正: channelMask = 0 の場合も R チャンネルにフォールバック
// ---------------------------------------------------------------------------
inline half ExtractScalar(half4 sample, int channelMask, float4 remap)
{
    half v;
    
    // channelMask = 0 の場合は R チャンネルにフォールバック
    if (channelMask == 0)
        channelMask = CHANNEL_R;
    
    // 単一チャンネル抽出
    if ((channelMask & CHANNEL_R) != 0)
        v = sample.r;
    else if ((channelMask & CHANNEL_G) != 0)
        v = sample.g;
    else if ((channelMask & CHANNEL_B) != 0)
        v = sample.b;
    else if ((channelMask & CHANNEL_A) != 0)
        v = sample.a;
    else
        v = sample.r;  // fallback
    
    return ApplySlotRemap(v, remap);
}

// ---------------------------------------------------------------------------
// ベクトル取り出し（channelMask に応じた2チャンネル → [-1,1]）
// ★修正: 不正な channelMask の場合は RG にフォールバック
// ---------------------------------------------------------------------------
inline half2 ExtractVector2(half4 sample, int channelMask, float4 remap)
{
    half2 v01;
    
    // channelMask に応じて2チャンネルを選択
    [branch]
    switch (channelMask)
    {
        case CHANNEL_RG: v01 = sample.rg; break;
        case CHANNEL_RB: v01 = sample.rb; break;
        case CHANNEL_RA: v01 = sample.ra; break;
        case CHANNEL_GB: v01 = sample.gb; break;
        case CHANNEL_GA: v01 = sample.ga; break;
        case CHANNEL_BA: v01 = sample.ba; break;
        default:         v01 = sample.rg; break;  // fallback to RG (includes channelMask=0)
    }
    
    // [0,1] → [-1,1] 変換
    half2 v = v01 * 2.0h - 1.0h;
    
    // Remap の w (invert) は方向反転として適用
    if (remap.w > 0.5)
        v = -v;
    
    return v;
}

// ---------------------------------------------------------------------------
// Slot からサンプリング (Raw)
// ★v2.0: binding 引数なし - 内部で ResolveAtlasSlotBinding を呼ぶ
// ---------------------------------------------------------------------------
inline half4 SampleSlotRaw(float2 uv, int slotType)
{
    if (slotType == TEXTURE_SLOT_NONE)
        return half4(0.5, 0.5, 0.5, 1.0);
    
    // Atlas Slot 0-4
    if (slotType >= TEXTURE_SLOT_ATLAS_0 && slotType <= TEXTURE_SLOT_ATLAS_4)
    {
#if defined(SURFACE2D_WEBGL_SAFE)
        return half4(0.5, 0.5, 0.5, 1.0);
#else
        // ★ここで動的に Tier/Slice を解決
        int2 binding = ResolveAtlasSlotBinding(slotType);
        int tier = binding.x;
        int slice = binding.y;
        
        // 無効なバインドの場合はデフォルト値
        if (tier < 0 || slice < 0)
            return half4(0.5, 0.5, 0.5, 1.0);
        
        return SampleNoiseAtlasByTier(uv, tier, slice);
#endif
    }
    
    // External Textures
    if (slotType == TEXTURE_SLOT_EXTERNAL_A)
        return SAMPLE_TEXTURE2D(_ExtTexA, sampler_ExtTexA, uv);
    
    if (slotType == TEXTURE_SLOT_EXTERNAL_B)
        return SAMPLE_TEXTURE2D(_ExtTexB, sampler_ExtTexB, uv);
    
    if (slotType == TEXTURE_SLOT_CUSTOM_RT)
        return SAMPLE_TEXTURE2D(_CustomRT, sampler_CustomRT, uv);
    
    return half4(0.5, 0.5, 0.5, 1.0);
}

// ---------------------------------------------------------------------------
// 高レベル API: Surface2D からスカラー値を取得
// ★v2.0: binding 引数なし - 内部で自動解決
// ---------------------------------------------------------------------------
inline half SampleSlotScalar(Surface2D s, TextureSlotRef ref)
{
    float2 uv = ComputeSlotUVFromSurface(s, ref);
    half4 raw = SampleSlotRaw(uv, ref.slotType);
    return ExtractScalar(raw, ref.channelMask, ref.remap);
}

// ---------------------------------------------------------------------------
// 高レベル API: Surface2D からベクトル値を取得
// ★v2.0: binding 引数なし - 内部で自動解決
// ---------------------------------------------------------------------------
inline half2 SampleSlotVector2(Surface2D s, TextureSlotRef ref)
{
    float2 uv = ComputeSlotUVFromSurface(s, ref);
    half4 raw = SampleSlotRaw(uv, ref.slotType);
    return ExtractVector2(raw, ref.channelMask, ref.remap);
}

// ---------------------------------------------------------------------------
// UV のみでサンプリング（Surface2D を経由しない場合）
// ---------------------------------------------------------------------------
inline half SampleSlotScalarDirect(float2 uv, TextureSlotRef ref)
{
    half4 raw = SampleSlotRaw(uv, ref.slotType);
    return ExtractScalar(raw, ref.channelMask, ref.remap);
}

inline half2 SampleSlotVector2Direct(float2 uv, TextureSlotRef ref)
{
    half4 raw = SampleSlotRaw(uv, ref.slotType);
    return ExtractVector2(raw, ref.channelMask, ref.remap);
}

#endif // GAME_TEXTURE_SLOT_2D_INCLUDED
