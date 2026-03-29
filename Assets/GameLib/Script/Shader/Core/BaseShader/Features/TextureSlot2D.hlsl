#ifndef GAME_TEXTURE_SLOT_2D_INCLUDED
#define GAME_TEXTURE_SLOT_2D_INCLUDED

// External texture sampling helpers shared by BaseShader composite features.

// ---------------------------------------------------------------------------
// Slot Type 定義 (C# の TextureSlotType と同期)
// ---------------------------------------------------------------------------
#define TEXTURE_SLOT_NONE        -1
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
#define CHANNEL_RG     3
#define CHANNEL_RB     5
#define CHANNEL_RA     9
#define CHANNEL_GB     6
#define CHANNEL_GA    10
#define CHANNEL_BA    12
#define CHANNEL_RGB    7
#define CHANNEL_RGBA  15

// ---------------------------------------------------------------------------
// UV Space 定義 (C# の NoiseUVSpace と同期)
// ---------------------------------------------------------------------------
#define NOISE_UV_SPACE_SPRITE_LOCAL 0
#define NOISE_UV_SPACE_SCREEN       1
#define NOISE_UV_SPACE_TEXTURE_RAW  2
#define NOISE_UV_SPACE_WORLD_XY     3

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
// ---------------------------------------------------------------------------
struct TextureSlotRef
{
    int    slotType;
    int    channelMask;
    int    uvSpace;
    float4 tilingOffset;
    float4 remap;
};

inline TextureSlotRef MakeTextureSlotRef(
    float slotType,
    float channelMask,
    float uvSpace,
    float4 tilingOffset,
    float4 remap)
{
    TextureSlotRef ref;
    ref.slotType = (int)round(slotType);
    ref.channelMask = (int)round(channelMask);
    ref.uvSpace = clamp((int)round(uvSpace), 0, 3);
    ref.tilingOffset = tilingOffset;
    ref.remap = remap;
    return ref;
}

inline TextureSlotRef MakeDefaultTextureSlotRef()
{
    TextureSlotRef ref;
    ref.slotType = TEXTURE_SLOT_NONE;
    ref.channelMask = CHANNEL_R;
    ref.uvSpace = NOISE_UV_SPACE_SPRITE_LOCAL;
    ref.tilingOffset = float4(1, 1, 0, 0);
    ref.remap = float4(0.5, 0.5, 1, 0);
    return ref;
}

inline float2 ComputeSlotUV(
    float2 uvLocal,
    float2 uvMain,
    float2 screenUV,
    int uvSpace,
    float4 tilingOffset,
    int slotType)
{
    float2 baseUV;

    [branch]
    switch (uvSpace)
    {
        case NOISE_UV_SPACE_SPRITE_LOCAL:
            baseUV = uvLocal;
            break;
        case NOISE_UV_SPACE_SCREEN:
            baseUV = screenUV;
            break;
        case NOISE_UV_SPACE_TEXTURE_RAW:
            baseUV = uvMain;
            break;
        case NOISE_UV_SPACE_WORLD_XY:
        default:
            baseUV = uvLocal;
            break;
    }

    float2 uvOut = baseUV * tilingOffset.xy + tilingOffset.zw;

    if (slotType == TEXTURE_SLOT_EXTERNAL_A || slotType == TEXTURE_SLOT_EXTERNAL_B || slotType == TEXTURE_SLOT_CUSTOM_RT)
    {
        const float kEps = 1e-5f;
        uvOut = clamp(uvOut, float2(kEps, kEps), float2(1.0f - kEps, 1.0f - kEps));
    }

    return uvOut;
}

inline float2 ComputeSlotUVFromSurface(Surface2D s, TextureSlotRef ref)
{
    return ComputeSlotUV(s.uvLocal, s.uvMain, s.screenUV, ref.uvSpace, ref.tilingOffset, ref.slotType);
}

inline half ApplySlotRemap(half value, float4 remap)
{
    half v = value;

    float bias = remap.x;
    if (abs(bias - 0.5) > 1e-4)
        v = v / ((1.0 / max(bias, 1e-4) - 2.0) * (1.0 - v) + 1.0);

    float gain = remap.y;
    if (abs(gain - 0.5) > 1e-4)
    {
        float invGain = 1.0 / max(gain, 1e-4);
        v = (v < 0.5)
            ? 0.5 * pow(abs(2.0 * v), invGain)
            : 1.0 - 0.5 * pow(abs(2.0 * (1.0 - v)), invGain);
    }

    float gamma = remap.z;
    if (abs(gamma - 1.0) > 1e-4)
        v = pow(max(v, 0), gamma);

    if (remap.w > 0.5)
        v = 1.0 - v;

    return saturate(v);
}

inline half ExtractScalar(half4 sample, int channelMask, float4 remap)
{
    half v;

    if (channelMask == 0)
        channelMask = CHANNEL_R;

    if ((channelMask & CHANNEL_R) != 0)
        v = sample.r;
    else if ((channelMask & CHANNEL_G) != 0)
        v = sample.g;
    else if ((channelMask & CHANNEL_B) != 0)
        v = sample.b;
    else if ((channelMask & CHANNEL_A) != 0)
        v = sample.a;
    else
        v = sample.r;

    return ApplySlotRemap(v, remap);
}

inline half2 ExtractVector2(half4 sample, int channelMask, float4 remap)
{
    half2 v01;

    [branch]
    switch (channelMask)
    {
        case CHANNEL_RG: v01 = sample.rg; break;
        case CHANNEL_RB: v01 = sample.rb; break;
        case CHANNEL_RA: v01 = sample.ra; break;
        case CHANNEL_GB: v01 = sample.gb; break;
        case CHANNEL_GA: v01 = sample.ga; break;
        case CHANNEL_BA: v01 = sample.ba; break;
        default:         v01 = sample.rg; break;
    }

    half2 v = v01 * 2.0h - 1.0h;
    if (remap.w > 0.5)
        v = -v;

    return v;
}

inline half4 SampleSlotRaw(float2 uv, int slotType)
{
    if (slotType == TEXTURE_SLOT_NONE)
        return half4(0.5, 0.5, 0.5, 1.0);

    if (slotType == TEXTURE_SLOT_EXTERNAL_A)
        return SAMPLE_TEXTURE2D(_ExtTexA, sampler_ExtTexA, uv);

    if (slotType == TEXTURE_SLOT_EXTERNAL_B)
        return SAMPLE_TEXTURE2D(_ExtTexB, sampler_ExtTexB, uv);

    if (slotType == TEXTURE_SLOT_CUSTOM_RT)
        return SAMPLE_TEXTURE2D(_CustomRT, sampler_CustomRT, uv);

    return half4(0.5, 0.5, 0.5, 1.0);
}

inline half SampleSlotScalar(Surface2D s, TextureSlotRef ref)
{
    float2 uv = ComputeSlotUVFromSurface(s, ref);
    half4 raw = SampleSlotRaw(uv, ref.slotType);
    return ExtractScalar(raw, ref.channelMask, ref.remap);
}

inline half2 SampleSlotVector2(Surface2D s, TextureSlotRef ref)
{
    float2 uv = ComputeSlotUVFromSurface(s, ref);
    half4 raw = SampleSlotRaw(uv, ref.slotType);
    return ExtractVector2(raw, ref.channelMask, ref.remap);
}

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

inline half4 SampleSlotRGBA(float2 uv, TextureSlotRef ref)
{
    half4 raw = SampleSlotRaw(uv, ref.slotType);

    if (ref.channelMask == CHANNEL_RGBA || ref.channelMask == 0)
        return raw;

    if (ref.channelMask == CHANNEL_R || ref.channelMask == CHANNEL_G || ref.channelMask == CHANNEL_B || ref.channelMask == CHANNEL_A)
    {
        half scalar = ExtractScalar(raw, ref.channelMask, ref.remap);
        return half4(scalar, scalar, scalar, scalar);
    }

    return raw;
}

inline half4 SampleSlotRGBA(Surface2D s, TextureSlotRef ref)
{
    float2 uv = ComputeSlotUVFromSurface(s, ref);
    return SampleSlotRGBA(uv, ref);
}

#endif // GAME_TEXTURE_SLOT_2D_INCLUDED
