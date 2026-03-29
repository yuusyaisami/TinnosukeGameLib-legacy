#ifndef GAME_TEXT_FX_2D_INCLUDED
#define GAME_TEXT_FX_2D_INCLUDED

// ============================================================================
// TextFx2D.hlsl - Text outline / shadow effects (BaseShader extension)
// ============================================================================

#include "Assets/GameLib/Script/Shader/Core/BaseShader/Features/ColorSpaceUtils.hlsl"

#define TEXTFX_OUTLINE_DIRECTION_LEFT   1.0
#define TEXTFX_OUTLINE_DIRECTION_RIGHT  2.0
#define TEXTFX_OUTLINE_DIRECTION_UP     4.0
#define TEXTFX_OUTLINE_DIRECTION_DOWN   8.0

struct TextFx2DParams
{
    float  outlineEnabled;
    float4 outlineColor;
    float  outlineThickness;
    float  outlineSoftness;
    float  outlineDirectionMask;
    float  outlineAutoColorEnabled;
    float  outlineAutoColorMode;
    float  outlineAutoHue;
    float  outlineAutoSaturation;
    float  outlineAutoLightness;

    float  shadowEnabled;
    float4 shadowColor;
    float2 shadowOffset;
    float  shadowSoftness;

    float  glowEnabled;
    float4 glowColor;
    float  glowThickness;
    float  glowSoftness;
};

inline TextFx2DParams MakeTextFx2DParams(
    float outlineEnabled,
    float4 outlineColor,
    float outlineThickness,
    float outlineSoftness,
    float outlineDirectionMask,
    float outlineAutoColorEnabled,
    float outlineAutoColorMode,
    float outlineAutoHue,
    float outlineAutoSaturation,
    float outlineAutoLightness,
    float shadowEnabled,
    float4 shadowColor,
    float2 shadowOffset,
    float shadowSoftness,
    float glowEnabled,
    float4 glowColor,
    float glowThickness,
    float glowSoftness)
{
    TextFx2DParams p;
    p.outlineEnabled = outlineEnabled;
    p.outlineColor = outlineColor;
    p.outlineThickness = outlineThickness;
    p.outlineSoftness = outlineSoftness;
    p.outlineDirectionMask = outlineDirectionMask;
    p.outlineAutoColorEnabled = outlineAutoColorEnabled;
    p.outlineAutoColorMode = outlineAutoColorMode;
    p.outlineAutoHue = outlineAutoHue;
    p.outlineAutoSaturation = outlineAutoSaturation;
    p.outlineAutoLightness = outlineAutoLightness;
    p.shadowEnabled = shadowEnabled;
    p.shadowColor = shadowColor;
    p.shadowOffset = shadowOffset;
    p.shadowSoftness = shadowSoftness;
    p.glowEnabled = glowEnabled;
    p.glowColor = glowColor;
    p.glowThickness = glowThickness;
    p.glowSoftness = glowSoftness;
    return p;
}

inline TextFx2DParams MakeDefaultTextFx2DParams()
{
    TextFx2DParams p;
    p.outlineEnabled = 0;
    p.outlineColor = float4(0, 0, 0, 1);
    p.outlineThickness = 0;
    p.outlineSoftness = 0;
    p.outlineDirectionMask = 15;
    p.outlineAutoColorEnabled = 0;
    p.outlineAutoColorMode = 0;
    p.outlineAutoHue = 0;
    p.outlineAutoSaturation = 0;
    p.outlineAutoLightness = 0;
    p.shadowEnabled = 0;
    p.shadowColor = float4(0, 0, 0, 0.5);
    p.shadowOffset = float2(0, 0);
    p.shadowSoftness = 0;
    p.glowEnabled = 0;
    p.glowColor = float4(1, 1, 1, 0.5);
    p.glowThickness = 0;
    p.glowSoftness = 0;
    return p;
}

inline float SampleMainTextureAlphaRaw(float2 uv)
{
    float a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;
#if defined(ETC1_EXTERNAL_ALPHA)
    if (_EnableExternalAlpha > 0.5)
    {
        a = SAMPLE_TEXTURE2D(_AlphaTex, sampler_AlphaTex, uv).r;
    }
#endif
    return a;
}

inline float SampleTextMaskAlpha(float2 uv)
{
    float4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
    float alpha = texel.a;
#if defined(ETC1_EXTERNAL_ALPHA)
    if (_EnableExternalAlpha > 0.5)
    {
        alpha = SAMPLE_TEXTURE2D(_AlphaTex, sampler_AlphaTex, uv).r;
    }
#endif
    if (_TextMode > 1.5)
    {
        alpha = max(alpha, max(texel.r, max(texel.g, texel.b)));
    }
    return alpha;
}

inline float ComputeOutlineAlphaAlphaTex(float2 uv, float baseAlpha, float thickness, float softness)
{
    float2 texel = _MainTex_TexelSize.xy;
    float2 sampleOffset = texel * max(thickness, 0.0);

    float maxAlpha = baseAlpha;
    maxAlpha = max(maxAlpha, SampleTextMaskAlpha(uv + float2(sampleOffset.x, 0)));
    maxAlpha = max(maxAlpha, SampleTextMaskAlpha(uv + float2(-sampleOffset.x, 0)));
    maxAlpha = max(maxAlpha, SampleTextMaskAlpha(uv + float2(0, sampleOffset.y)));
    maxAlpha = max(maxAlpha, SampleTextMaskAlpha(uv + float2(0, -sampleOffset.y)));
    maxAlpha = max(maxAlpha, SampleTextMaskAlpha(uv + float2(sampleOffset.x, sampleOffset.y)));
    maxAlpha = max(maxAlpha, SampleTextMaskAlpha(uv + float2(sampleOffset.x, -sampleOffset.y)));
    maxAlpha = max(maxAlpha, SampleTextMaskAlpha(uv + float2(-sampleOffset.x, sampleOffset.y)));
    maxAlpha = max(maxAlpha, SampleTextMaskAlpha(uv + float2(-sampleOffset.x, -sampleOffset.y)));

    float outline = saturate(maxAlpha - baseAlpha);
    float edge = max(1e-4, softness);
    return smoothstep(0.0, edge, outline);
}

inline bool TextFxDirectionMaskHasBit(float roundedDirectionMask, float bitValue)
{
    return fmod(floor(roundedDirectionMask / bitValue), 2.0) > 0.5;
}

inline float ComputeDirectionalOutlineAlphaAlphaTex(
    float2 uv,
    float baseAlpha,
    float thickness,
    float softness,
    float directionMask)
{
    float roundedDirectionMask = max(0.0, floor(directionMask + 0.5));
    if (roundedDirectionMask < 0.5)
        return 0.0;

    float2 texel = _MainTex_TexelSize.xy;
    float2 sampleOffset = texel * max(thickness, 0.0);

    bool hasLeft = TextFxDirectionMaskHasBit(roundedDirectionMask, TEXTFX_OUTLINE_DIRECTION_LEFT);
    bool hasRight = TextFxDirectionMaskHasBit(roundedDirectionMask, TEXTFX_OUTLINE_DIRECTION_RIGHT);
    bool hasUp = TextFxDirectionMaskHasBit(roundedDirectionMask, TEXTFX_OUTLINE_DIRECTION_UP);
    bool hasDown = TextFxDirectionMaskHasBit(roundedDirectionMask, TEXTFX_OUTLINE_DIRECTION_DOWN);

    float maxAlpha = baseAlpha;
    if (hasRight)
        maxAlpha = max(maxAlpha, SampleTextMaskAlpha(uv + float2(sampleOffset.x, 0.0)));
    if (hasLeft)
        maxAlpha = max(maxAlpha, SampleTextMaskAlpha(uv + float2(-sampleOffset.x, 0.0)));
    if (hasUp)
        maxAlpha = max(maxAlpha, SampleTextMaskAlpha(uv + float2(0.0, sampleOffset.y)));
    if (hasDown)
        maxAlpha = max(maxAlpha, SampleTextMaskAlpha(uv + float2(0.0, -sampleOffset.y)));

    if (hasRight && hasUp)
        maxAlpha = max(maxAlpha, SampleTextMaskAlpha(uv + float2(sampleOffset.x, sampleOffset.y)));
    if (hasRight && hasDown)
        maxAlpha = max(maxAlpha, SampleTextMaskAlpha(uv + float2(sampleOffset.x, -sampleOffset.y)));
    if (hasLeft && hasUp)
        maxAlpha = max(maxAlpha, SampleTextMaskAlpha(uv + float2(-sampleOffset.x, sampleOffset.y)));
    if (hasLeft && hasDown)
        maxAlpha = max(maxAlpha, SampleTextMaskAlpha(uv + float2(-sampleOffset.x, -sampleOffset.y)));

    float outline = saturate(maxAlpha - baseAlpha);
    float edge = max(1e-4, softness);
    return smoothstep(0.0, edge, outline);
}

inline float ComputeShadowAlphaAlphaTex(float2 uv, float baseAlpha, float2 offset, float softness)
{
    float2 texel = _MainTex_TexelSize.xy;
    float2 sampleOffset = offset * texel;
    float a = SampleTextMaskAlpha(uv + sampleOffset);
    float shadow = a * (1.0 - baseAlpha);
    float edge = max(1e-4, softness);
    return smoothstep(0.0, edge, shadow);
}

inline float ComputeTextSdfFaceAlpha(float distanceSample, float edgeWidth)
{
    return smoothstep(0.5 - edgeWidth, 0.5 + edgeWidth, distanceSample);
}

inline float ComputeDirectionalOutlineAlphaSdf(
    float2 uv,
    float baseDistance,
    float thickness,
    float softness,
    float directionMask)
{
    float roundedDirectionMask = max(0.0, floor(directionMask + 0.5));
    if (roundedDirectionMask < 0.5)
        return 0.0;

    float2 texel = _MainTex_TexelSize.xy;
    float2 sampleOffset = texel * max(thickness, 0.0);
    float edgeWidth = max(fwidth(baseDistance), 1e-5);

    bool hasLeft = TextFxDirectionMaskHasBit(roundedDirectionMask, TEXTFX_OUTLINE_DIRECTION_LEFT);
    bool hasRight = TextFxDirectionMaskHasBit(roundedDirectionMask, TEXTFX_OUTLINE_DIRECTION_RIGHT);
    bool hasUp = TextFxDirectionMaskHasBit(roundedDirectionMask, TEXTFX_OUTLINE_DIRECTION_UP);
    bool hasDown = TextFxDirectionMaskHasBit(roundedDirectionMask, TEXTFX_OUTLINE_DIRECTION_DOWN);

    float baseFace = ComputeTextSdfFaceAlpha(baseDistance, edgeWidth);
    float maxFace = baseFace;

    if (hasRight)
        maxFace = max(maxFace, ComputeTextSdfFaceAlpha(SampleMainTextureAlphaRaw(uv + float2(sampleOffset.x, 0.0)), edgeWidth));
    if (hasLeft)
        maxFace = max(maxFace, ComputeTextSdfFaceAlpha(SampleMainTextureAlphaRaw(uv + float2(-sampleOffset.x, 0.0)), edgeWidth));
    if (hasUp)
        maxFace = max(maxFace, ComputeTextSdfFaceAlpha(SampleMainTextureAlphaRaw(uv + float2(0.0, sampleOffset.y)), edgeWidth));
    if (hasDown)
        maxFace = max(maxFace, ComputeTextSdfFaceAlpha(SampleMainTextureAlphaRaw(uv + float2(0.0, -sampleOffset.y)), edgeWidth));

    if (hasRight && hasUp)
        maxFace = max(maxFace, ComputeTextSdfFaceAlpha(SampleMainTextureAlphaRaw(uv + float2(sampleOffset.x, sampleOffset.y)), edgeWidth));
    if (hasRight && hasDown)
        maxFace = max(maxFace, ComputeTextSdfFaceAlpha(SampleMainTextureAlphaRaw(uv + float2(sampleOffset.x, -sampleOffset.y)), edgeWidth));
    if (hasLeft && hasUp)
        maxFace = max(maxFace, ComputeTextSdfFaceAlpha(SampleMainTextureAlphaRaw(uv + float2(-sampleOffset.x, sampleOffset.y)), edgeWidth));
    if (hasLeft && hasDown)
        maxFace = max(maxFace, ComputeTextSdfFaceAlpha(SampleMainTextureAlphaRaw(uv + float2(-sampleOffset.x, -sampleOffset.y)), edgeWidth));

    float outline = saturate(maxFace - baseFace);
    float edge = max(1e-4, softness);
    return smoothstep(0.0, edge, outline);
}

inline float ComputeGlowAlphaSdf(float dist, float thickness, float softness)
{
    float edge = 0.5;
    float t = thickness * 0.02;
    float s = softness * 0.02;
    float outer = smoothstep(edge - t - s, edge - t + s, dist);
    return saturate(outer);
}

inline float ComputeShadowAlphaSdf(float distOffset, float distBase, float softness)
{
    float edge = 0.5;
    float s = softness * 0.02;
    float shadowFace = smoothstep(edge - s, edge + s, distOffset);
    float baseFace = smoothstep(edge - s, edge + s, distBase);
    return saturate(shadowFace * (1.0 - baseFace));
}

inline float4 Over(float4 front, float4 back)
{
    float a = front.a + back.a * (1.0 - front.a);
    float3 c = front.rgb * front.a + back.rgb * back.a * (1.0 - front.a);
    return float4(c, a);
}

inline float ComputeTextOutlineAlpha(Surface2D s, TextFx2DParams p)
{
    if (p.outlineEnabled < 0.5 || p.outlineDirectionMask < 0.5)
        return 0.0;

    float alphaScale = saturate(s.vertexAlpha * s.alphaFactor);
    float outlineAlpha = 0.0;

    if (_TextMode > 0.5 && _TextMode < 1.5)
    {
        outlineAlpha = ComputeDirectionalOutlineAlphaSdf(
            s.uvMain,
            s.baseAlphaRaw,
            p.outlineThickness,
            p.outlineSoftness,
            p.outlineDirectionMask);
    }
    else
    {
        outlineAlpha = ComputeDirectionalOutlineAlphaAlphaTex(
            s.uvMain,
            s.baseAlphaRaw,
            p.outlineThickness,
            p.outlineSoftness,
            p.outlineDirectionMask);
    }

    outlineAlpha *= p.outlineColor.a * alphaScale;
    return outlineAlpha;
}

inline float3 ResolveTextOutlineColor(Surface2D s, TextFx2DParams p)
{
    float3 outlineColor = p.outlineColor.rgb;
    if (p.outlineAutoColorEnabled > 0.5)
    {
        half3 hsl = RGBtoHSL(saturate((half3)s.color));
        hsl.x = frac(hsl.x + (half)p.outlineAutoHue);

        if (p.outlineAutoColorMode > 0.5)
        {
            hsl.y = ApplySignedHeadroomAdjust(hsl.y, (half)p.outlineAutoSaturation);
            hsl.z = ApplySignedHeadroomAdjust(hsl.z, (half)p.outlineAutoLightness);
        }
        else
        {
            hsl.y = saturate(hsl.y + (half)p.outlineAutoSaturation);
            hsl.z = saturate(hsl.z + (half)p.outlineAutoLightness);
        }

        outlineColor = HSLtoRGB(hsl) * outlineColor;
    }

    return outlineColor;
}

inline Surface2D Surface2D_ApplyTextFxPrepass(Surface2D s, TextFx2DParams p)
{
    Surface2D result = s;
    if (p.shadowEnabled >= 0.5 || p.glowEnabled >= 0.5)
    {
        float alphaScale = saturate(result.vertexAlpha * result.alphaFactor);
        float shadowAlpha = 0.0;
        if (p.shadowEnabled > 0.5)
        {
            if (_TextMode > 0.5 && _TextMode < 1.5)
            {
                float distOffset = SampleMainTextureAlphaRaw(result.uvMain + (p.shadowOffset * _MainTex_TexelSize.xy));
                shadowAlpha = ComputeShadowAlphaSdf(distOffset, result.baseAlphaRaw, p.shadowSoftness);
            }
            else
            {
                shadowAlpha = ComputeShadowAlphaAlphaTex(result.uvMain, result.baseAlphaRaw, p.shadowOffset, p.shadowSoftness);
            }
            shadowAlpha *= p.shadowColor.a * alphaScale;
        }

        float glowAlpha = 0.0;
        if (p.glowEnabled > 0.5)
        {
            if (_TextMode > 0.5 && _TextMode < 1.5)
            {
                glowAlpha = ComputeGlowAlphaSdf(result.baseAlphaRaw, p.glowThickness, p.glowSoftness);
            }
            else
            {
                glowAlpha = ComputeOutlineAlphaAlphaTex(result.uvMain, result.baseAlphaRaw, p.glowThickness, p.glowSoftness);
            }
            glowAlpha *= p.glowColor.a * alphaScale;
        }

        float4 baseCol = float4(result.color, result.alpha);
        float4 shadowCol = float4(p.shadowColor.rgb, shadowAlpha);
        float4 glowCol = float4(p.glowColor.rgb, glowAlpha);
        float4 comp = shadowCol;
        comp = Over(glowCol, comp);
        comp = Over(baseCol, comp);

        result.color = comp.rgb;
        result.alpha = comp.a;
    }

    return result;
}

inline Surface2D Surface2D_ApplyTextOutlineFx(Surface2D s, TextFx2DParams p)
{
    Surface2D result = s;
    float outlineAlpha = ComputeTextOutlineAlpha(result, p);
    if (outlineAlpha > 1e-6)
    {
        float3 outlineColor = ResolveTextOutlineColor(result, p);
        float4 baseCol = float4(result.color, result.alpha);
        float4 outlineCol = float4(outlineColor, outlineAlpha);
        float4 comp = outlineCol;
        comp = Over(baseCol, comp);
        result.color = comp.rgb;
        result.alpha = comp.a;
    }

    return result;
}

inline Surface2D Surface2D_ApplyTextFx(Surface2D s, TextFx2DParams p)
{
    s = Surface2D_ApplyTextFxPrepass(s, p);
    return Surface2D_ApplyTextOutlineFx(s, p);
}

#endif // GAME_TEXT_FX_2D_INCLUDED
