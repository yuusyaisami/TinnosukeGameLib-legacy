#ifndef GAME_TEXT_FX_2D_INCLUDED
#define GAME_TEXT_FX_2D_INCLUDED

// ============================================================================
// TextFx2D.hlsl - Text outline / shadow effects (BaseShader extension)
// ============================================================================

struct TextFx2DParams
{
    float  outlineEnabled;
    float4 outlineColor;
    float  outlineThickness;
    float  outlineSoftness;

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
    float shadowEnabled,
    float4 shadowColor,
    float2 shadowOffset,
    float shadowSoftness,
    float glowEnabled,
    float4 glowColor,
    float glowThickness,
    float glowSoftness)
{
    TextFx2DParams p = (TextFx2DParams)0;
    p.outlineEnabled = outlineEnabled;
    p.outlineColor = outlineColor;
    p.outlineThickness = outlineThickness;
    p.outlineSoftness = outlineSoftness;
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
    TextFx2DParams p = (TextFx2DParams)0;
    p.outlineEnabled = 0;
    p.outlineColor = float4(0, 0, 0, 1);
    p.outlineThickness = 0;
    p.outlineSoftness = 0;
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

inline float SampleMainAlpha(float2 uv)
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

inline float ComputeOutlineAlphaAlphaTex(float2 uv, float baseAlpha, float thickness, float softness)
{
    float2 texel = _MainTex_TexelSize.xy;
    float2 sampleOffset = texel * max(thickness, 0.0);

    float maxAlpha = baseAlpha;
    maxAlpha = max(maxAlpha, SampleMainAlpha(uv + float2(sampleOffset.x, 0)));
    maxAlpha = max(maxAlpha, SampleMainAlpha(uv + float2(-sampleOffset.x, 0)));
    maxAlpha = max(maxAlpha, SampleMainAlpha(uv + float2(0, sampleOffset.y)));
    maxAlpha = max(maxAlpha, SampleMainAlpha(uv + float2(0, -sampleOffset.y)));
    maxAlpha = max(maxAlpha, SampleMainAlpha(uv + float2(sampleOffset.x, sampleOffset.y)));
    maxAlpha = max(maxAlpha, SampleMainAlpha(uv + float2(sampleOffset.x, -sampleOffset.y)));
    maxAlpha = max(maxAlpha, SampleMainAlpha(uv + float2(-sampleOffset.x, sampleOffset.y)));
    maxAlpha = max(maxAlpha, SampleMainAlpha(uv + float2(-sampleOffset.x, -sampleOffset.y)));

    float outline = saturate(maxAlpha - baseAlpha);
    float edge = max(1e-4, softness);
    return smoothstep(0.0, edge, outline);
}

inline float ComputeShadowAlphaAlphaTex(float2 uv, float baseAlpha, float2 offset, float softness)
{
    float2 texel = _MainTex_TexelSize.xy;
    float2 sampleOffset = offset * texel;
    float a = SampleMainAlpha(uv + sampleOffset);
    float shadow = a * (1.0 - baseAlpha);
    float edge = max(1e-4, softness);
    return smoothstep(0.0, edge, shadow);
}

inline float ComputeOutlineAlphaSdf(float dist, float thickness, float softness)
{
    float edge = 0.5;
    float t = thickness * 0.02;
    float s = softness * 0.02;
    float outer = smoothstep(edge - t - s, edge - t + s, dist);
    float inner = smoothstep(edge - s, edge + s, dist);
    return saturate(outer - inner);
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

inline Surface2D Surface2D_ApplyTextFx(Surface2D s, TextFx2DParams p)
{
#if defined(SURFACE2D_WEBGL_SAFE)
    if (p.outlineEnabled < 0.5 && p.shadowEnabled < 0.5 && p.glowEnabled < 0.5)
        return s;

    float2 texelSize = _MainTex_TexelSize.xy;
    float alphaScale = saturate(s.vertexAlpha * s.alphaFactor);
    float2 uv = s.uvMain;
    float baseAlpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;
#if defined(ETC1_EXTERNAL_ALPHA)
    if (_EnableExternalAlpha > 0.5)
    {
        baseAlpha = SAMPLE_TEXTURE2D(_AlphaTex, sampler_AlphaTex, uv).r;
    }
#endif

    float outlineAlpha = 0.0;
    if (p.outlineEnabled > 0.5)
    {
        float2 outlineOffset = texelSize * max(p.outlineThickness, 0.0);
        float alphaRight = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( outlineOffset.x, 0.0)).a;
        float alphaLeft  = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-outlineOffset.x, 0.0)).a;
        float alphaUp    = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(0.0,  outlineOffset.y)).a;
        float alphaDown  = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(0.0, -outlineOffset.y)).a;
#if defined(ETC1_EXTERNAL_ALPHA)
        if (_EnableExternalAlpha > 0.5)
        {
            alphaRight = SAMPLE_TEXTURE2D(_AlphaTex, sampler_AlphaTex, uv + float2( outlineOffset.x, 0.0)).r;
            alphaLeft  = SAMPLE_TEXTURE2D(_AlphaTex, sampler_AlphaTex, uv + float2(-outlineOffset.x, 0.0)).r;
            alphaUp    = SAMPLE_TEXTURE2D(_AlphaTex, sampler_AlphaTex, uv + float2(0.0,  outlineOffset.y)).r;
            alphaDown  = SAMPLE_TEXTURE2D(_AlphaTex, sampler_AlphaTex, uv + float2(0.0, -outlineOffset.y)).r;
        }
#endif
        float outlineNeighborMax = max(max(alphaRight, alphaLeft), max(alphaUp, alphaDown));
        outlineAlpha = smoothstep(0.0, max(1e-4, p.outlineSoftness), saturate(outlineNeighborMax - baseAlpha));
        outlineAlpha *= p.outlineColor.a * alphaScale;
    }

    float shadowAlpha = 0.0;
    if (p.shadowEnabled > 0.5)
    {
        float2 shadowUV = uv + p.shadowOffset * texelSize;
        float shadowBase = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, shadowUV).a;
#if defined(ETC1_EXTERNAL_ALPHA)
        if (_EnableExternalAlpha > 0.5)
        {
            shadowBase = SAMPLE_TEXTURE2D(_AlphaTex, sampler_AlphaTex, shadowUV).r;
        }
#endif
        shadowAlpha = smoothstep(0.0, max(1e-4, p.shadowSoftness), saturate(shadowBase - baseAlpha));
        shadowAlpha *= p.shadowColor.a * alphaScale;
    }

    float glowAlpha = 0.0;
    if (p.glowEnabled > 0.5)
    {
        float2 glowOffset = texelSize * max(p.glowThickness, 0.0);
        float glowRight = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( glowOffset.x, 0.0)).a;
        float glowLeft  = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-glowOffset.x, 0.0)).a;
        float glowUp    = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(0.0,  glowOffset.y)).a;
        float glowDown  = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(0.0, -glowOffset.y)).a;
#if defined(ETC1_EXTERNAL_ALPHA)
        if (_EnableExternalAlpha > 0.5)
        {
            glowRight = SAMPLE_TEXTURE2D(_AlphaTex, sampler_AlphaTex, uv + float2( glowOffset.x, 0.0)).r;
            glowLeft  = SAMPLE_TEXTURE2D(_AlphaTex, sampler_AlphaTex, uv + float2(-glowOffset.x, 0.0)).r;
            glowUp    = SAMPLE_TEXTURE2D(_AlphaTex, sampler_AlphaTex, uv + float2(0.0,  glowOffset.y)).r;
            glowDown  = SAMPLE_TEXTURE2D(_AlphaTex, sampler_AlphaTex, uv + float2(0.0, -glowOffset.y)).r;
        }
#endif
        float glowNeighborMax = max(max(glowRight, glowLeft), max(glowUp, glowDown));
        glowAlpha = smoothstep(0.0, max(1e-4, p.glowSoftness), glowNeighborMax);
        glowAlpha *= p.glowColor.a * alphaScale;
    }

    float3 colorOut = s.color;
    float alphaOut = s.alpha;

    if (shadowAlpha > 1e-6)
    {
        colorOut = lerp(p.shadowColor.rgb, colorOut, alphaOut);
        alphaOut = max(alphaOut, shadowAlpha);
    }

    if (glowAlpha > 1e-6)
    {
        colorOut = lerp(p.glowColor.rgb, colorOut, alphaOut);
        alphaOut = max(alphaOut, glowAlpha);
    }

    if (outlineAlpha > 1e-6)
    {
        colorOut = lerp(p.outlineColor.rgb, colorOut, alphaOut);
        alphaOut = max(alphaOut, outlineAlpha);
    }

    s.color = colorOut;
    s.alpha = alphaOut;
    return s;
#else
    if (p.outlineEnabled < 0.5 && p.shadowEnabled < 0.5 && p.glowEnabled < 0.5)
        return s;

    float alphaScale = saturate(s.vertexAlpha * s.alphaFactor);

    float outlineAlpha = 0.0;
    if (p.outlineEnabled > 0.5)
    {
        if (_TextMode > 0.5 && _TextMode < 1.5)
        {
            outlineAlpha = ComputeOutlineAlphaSdf(s.baseAlphaRaw, p.outlineThickness, p.outlineSoftness);
        }
        else
        {
            outlineAlpha = ComputeOutlineAlphaAlphaTex(s.uvMain, s.baseAlphaRaw, p.outlineThickness, p.outlineSoftness);
        }
        outlineAlpha *= p.outlineColor.a * alphaScale;
    }

    float shadowAlpha = 0.0;
    if (p.shadowEnabled > 0.5)
    {
        if (_TextMode > 0.5 && _TextMode < 1.5)
        {
            float distOffset = SampleMainAlpha(s.uvMain + (p.shadowOffset * _MainTex_TexelSize.xy));
            shadowAlpha = ComputeShadowAlphaSdf(distOffset, s.baseAlphaRaw, p.shadowSoftness);
        }
        else
        {
            shadowAlpha = ComputeShadowAlphaAlphaTex(s.uvMain, s.baseAlphaRaw, p.shadowOffset, p.shadowSoftness);
        }
        shadowAlpha *= p.shadowColor.a * alphaScale;
    }

    float glowAlpha = 0.0;
    if (p.glowEnabled > 0.5)
    {
        if (_TextMode > 0.5 && _TextMode < 1.5)
        {
            glowAlpha = ComputeGlowAlphaSdf(s.baseAlphaRaw, p.glowThickness, p.glowSoftness);
        }
        else
        {
            glowAlpha = ComputeOutlineAlphaAlphaTex(s.uvMain, s.baseAlphaRaw, p.glowThickness, p.glowSoftness);
        }
        glowAlpha *= p.glowColor.a * alphaScale;
    }

    float4 baseCol = float4(s.color, s.alpha);
    float4 shadowCol = float4(p.shadowColor.rgb, shadowAlpha);
    float4 outlineCol = float4(p.outlineColor.rgb, outlineAlpha);
    float4 glowCol = float4(p.glowColor.rgb, glowAlpha);

    float4 comp = shadowCol;
    comp = Over(glowCol, comp);
    comp = Over(outlineCol, comp);
    comp = Over(baseCol, comp);

    s.color = comp.rgb;
    s.alpha = comp.a;
    return s;
#endif
}

#endif // GAME_TEXT_FX_2D_INCLUDED
