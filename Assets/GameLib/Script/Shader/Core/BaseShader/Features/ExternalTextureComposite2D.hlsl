#ifndef GAME_EXTERNAL_TEXTURE_COMPOSITE_2D_INCLUDED
#define GAME_EXTERNAL_TEXTURE_COMPOSITE_2D_INCLUDED

// TextureSlot2D.hlsl は Surface2D.hlsl 経由でインクルード想定

#define EXTERNAL_TEXTURE_COMPOSITE_BLEND_REPLACE  0
#define EXTERNAL_TEXTURE_COMPOSITE_BLEND_LERP    10
#define EXTERNAL_TEXTURE_COMPOSITE_BLEND_ADD     20
#define EXTERNAL_TEXTURE_COMPOSITE_BLEND_MULTIPLY 30

struct ExternalTextureComposite2DParams
{
    float enabled;
    TextureSlotRef source;
    int blendMode;
    half intensity;
    half useTextureAlpha;
    half4 tint;
    half disableWhenTextureMissing;
    half affectSurfaceAlpha;
};

inline ExternalTextureComposite2DParams MakeExternalTextureComposite2DParams(
    float enabled,
    float sourceSlotType,
    float sourceChannel,
    float sourceUVSpace,
    float4 sourceTilingOffset,
    float4 sourceRemap,
    float blendMode,
    float intensity,
    float useTextureAlpha,
    float4 tint,
    float disableWhenTextureMissing,
    float affectSurfaceAlpha)
{
    ExternalTextureComposite2DParams p = (ExternalTextureComposite2DParams)0;
    p.enabled = enabled;
    p.source = MakeTextureSlotRef(
        sourceSlotType,
        sourceChannel,
        sourceUVSpace,
        sourceTilingOffset,
        sourceRemap
    );
    p.blendMode = (int)round(blendMode);
    p.intensity = saturate(intensity);
    p.useTextureAlpha = saturate(useTextureAlpha);
    p.tint = tint;
    p.disableWhenTextureMissing = saturate(disableWhenTextureMissing);
    p.affectSurfaceAlpha = saturate(affectSurfaceAlpha);
    return p;
}

inline ExternalTextureComposite2DParams MakeDefaultExternalTextureComposite2DParams()
{
    ExternalTextureComposite2DParams p = (ExternalTextureComposite2DParams)0;
    p.enabled = 0.0;
    p.source = MakeDefaultTextureSlotRef();
    p.blendMode = EXTERNAL_TEXTURE_COMPOSITE_BLEND_REPLACE;
    p.intensity = 1.0h;
    p.useTextureAlpha = 0.0h;
    p.tint = half4(1.0h, 1.0h, 1.0h, 1.0h);
    p.disableWhenTextureMissing = 1.0h;
    p.affectSurfaceAlpha = 0.0h;
    return p;
}

inline bool ExternalTextureComposite2D_IsSupportedSlot(int slotType)
{
    return slotType == TEXTURE_SLOT_EXTERNAL_A
        || slotType == TEXTURE_SLOT_EXTERNAL_B
        || slotType == TEXTURE_SLOT_CUSTOM_RT;
}

inline Surface2D Surface2D_ApplyExternalTextureComposite(
    Surface2D s,
    ExternalTextureComposite2DParams p)
{
    if (p.enabled < 0.5h)
        return s;

    if (!ExternalTextureComposite2D_IsSupportedSlot(p.source.slotType))
        return s;

    half4 source = SampleSlotRGBA(s, p.source) * p.tint;
    half blendWeight = saturate(p.intensity * lerp(1.0h, source.a, p.useTextureAlpha));

    if (blendWeight <= 0.0h && p.disableWhenTextureMissing > 0.5h)
        return s;

    half3 currentColor = s.color;
    half currentAlpha = s.alpha;

    [branch]
    switch (p.blendMode)
    {
        case EXTERNAL_TEXTURE_COMPOSITE_BLEND_REPLACE:
            s.color = lerp(currentColor, source.rgb, blendWeight);
            if (p.affectSurfaceAlpha > 0.5h)
                s.alpha = lerp(currentAlpha, source.a, blendWeight);
            break;

        case EXTERNAL_TEXTURE_COMPOSITE_BLEND_ADD:
            s.color = currentColor + source.rgb * blendWeight;
            if (p.affectSurfaceAlpha > 0.5h)
                s.alpha = saturate(currentAlpha + source.a * blendWeight);
            break;

        case EXTERNAL_TEXTURE_COMPOSITE_BLEND_MULTIPLY:
            s.color = lerp(currentColor, currentColor * source.rgb, blendWeight);
            if (p.affectSurfaceAlpha > 0.5h)
                s.alpha = lerp(currentAlpha, currentAlpha * source.a, blendWeight);
            break;

        case EXTERNAL_TEXTURE_COMPOSITE_BLEND_LERP:
        default:
            s.color = lerp(currentColor, source.rgb, blendWeight);
            if (p.affectSurfaceAlpha > 0.5h)
                s.alpha = lerp(currentAlpha, source.a, blendWeight);
            break;
    }

    return s;
}

#endif // GAME_EXTERNAL_TEXTURE_COMPOSITE_2D_INCLUDED
