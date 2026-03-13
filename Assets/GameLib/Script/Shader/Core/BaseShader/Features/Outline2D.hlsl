#ifndef GAME_OUTLINE_2D_INCLUDED
#define GAME_OUTLINE_2D_INCLUDED

// ============================================================================
// Outline2D.hlsl - Generic sprite/UI outline effect
// ============================================================================

struct Outline2DParams
{
    float  enabled;
    float  mode;            // 10: Outside, 20: Inside
    float4 color;
    float  width;
    float  opacity;
    float  softness;
    float  blendMode;       // 10: Alpha, 20: Add, 30: Screen

    float  pixelPerfect;
    float  widthUnit;       // 10: Texel, 20: ScreenPixel
    float  pixelStep;
    float  samplePattern;   // 10: Diamond4, 20: Box8, 30: Circle12

    float  maskRespect;
    float  useVertexColor;
    float  uvClampEnabled;
    float  zTestMode;       // reserved
};

inline Outline2DParams MakeOutline2DParams(
    float enabled,
    float mode,
    float4 color,
    float width,
    float opacity,
    float softness,
    float blendMode,
    float pixelPerfect,
    float widthUnit,
    float pixelStep,
    float samplePattern,
    float maskRespect,
    float useVertexColor,
    float uvClampEnabled,
    float zTestMode)
{
    Outline2DParams p = (Outline2DParams)0;
    p.enabled = enabled;
    p.mode = mode;
    p.color = color;
    p.width = width;
    p.opacity = opacity;
    p.softness = softness;
    p.blendMode = blendMode;
    p.pixelPerfect = pixelPerfect;
    p.widthUnit = widthUnit;
    p.pixelStep = pixelStep;
    p.samplePattern = samplePattern;
    p.maskRespect = maskRespect;
    p.useVertexColor = useVertexColor;
    p.uvClampEnabled = uvClampEnabled;
    p.zTestMode = zTestMode;
    return p;
}

inline float Outline2D_QuantizeWidth(float width, float pixelPerfect, float pixelStep)
{
    float w = max(width, 0.0);
    if (pixelPerfect > 0.5)
    {
        float stepSize = max(pixelStep, 1e-4);
        w = round(w / stepSize) * stepSize;
    }
    return max(w, 0.0);
}

inline float2 Outline2D_ComputeStepUV(Surface2D s, Outline2DParams p)
{
    float widthQ = Outline2D_QuantizeWidth(p.width, p.pixelPerfect, p.pixelStep);
    if (widthQ <= 0.0)
    {
        return float2(0.0, 0.0);
    }

#if defined(SURFACE2D_WEBGL_SAFE)
    return _MainTex_TexelSize.xy * widthQ;
#else
    if (p.widthUnit >= 15.0)
    {
        float2 uvPerScreenX = float2(ddx(s.uvMain.x), ddy(s.uvMain.x));
        float2 uvPerScreenY = float2(ddx(s.uvMain.y), ddy(s.uvMain.y));
        float2 perPixel = float2(length(uvPerScreenX), length(uvPerScreenY));
        perPixel = max(perPixel, float2(1e-6, 1e-6));
        return perPixel * widthQ;
    }

    return _MainTex_TexelSize.xy * widthQ;
#endif
}

inline float2 Outline2D_ClampToSpriteUV(float2 atlasUV)
{
    float2 uvLocal = AtlasUVToSpriteLocalUV(atlasUV);

    float2 rectSize = max(_SpriteUVRect.zw - _SpriteUVRect.xy, float2(1e-6, 1e-6));
    float2 texelLocal = _MainTex_TexelSize.xy / rectSize;
    float2 localMin = texelLocal * 0.5;
    float2 localMax = 1.0 - localMin;

    uvLocal = clamp(uvLocal, localMin, localMax);
    return SpriteLocalUVToAtlasUV(uvLocal);
}

inline float Outline2D_SampleMainAlpha(float2 uv)
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

inline float Outline2D_SampleMainAlphaOffset(Surface2D s, Outline2DParams p, float2 uvOffset)
{
    float2 sampleUV = s.uvMain + uvOffset;
    if (p.uvClampEnabled > 0.5)
    {
        sampleUV = Outline2D_ClampToSpriteUV(sampleUV);
    }

    return Outline2D_SampleMainAlpha(sampleUV);
}

inline void Outline2D_AccumulateMinMax(inout float minA, inout float maxA, float v)
{
    minA = min(minA, v);
    maxA = max(maxA, v);
}

inline float4 Outline2D_Over(float4 front, float4 back)
{
    float a = front.a + back.a * (1.0 - front.a);
    float3 c = front.rgb * front.a + back.rgb * back.a * (1.0 - front.a);
    return float4(c, a);
}

inline float3 Outline2D_Screen(float3 baseColor, float3 blendColor)
{
    return 1.0 - (1.0 - baseColor) * (1.0 - blendColor);
}

inline Surface2D Surface2D_ApplyOutline(Surface2D s, Outline2DParams p)
{
#if defined(SURFACE2D_WEBGL_SAFE)
    if (p.enabled < 0.5)
        return s;

    float2 texelSize = _MainTex_TexelSize.xy * max(p.width, 0.0);
    if (texelSize.x <= 1e-8 && texelSize.y <= 1e-8)
        return s;

    float2 uv = s.uvMain;
    float centerAlpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;
    float alphaRight = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( texelSize.x, 0.0)).a;
    float alphaLeft  = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-texelSize.x, 0.0)).a;
    float alphaUp    = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(0.0,  texelSize.y)).a;
    float alphaDown  = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(0.0, -texelSize.y)).a;
#if defined(ETC1_EXTERNAL_ALPHA)
    if (_EnableExternalAlpha > 0.5)
    {
        centerAlpha = SAMPLE_TEXTURE2D(_AlphaTex, sampler_AlphaTex, uv).r;
        alphaRight = SAMPLE_TEXTURE2D(_AlphaTex, sampler_AlphaTex, uv + float2( texelSize.x, 0.0)).r;
        alphaLeft  = SAMPLE_TEXTURE2D(_AlphaTex, sampler_AlphaTex, uv + float2(-texelSize.x, 0.0)).r;
        alphaUp    = SAMPLE_TEXTURE2D(_AlphaTex, sampler_AlphaTex, uv + float2(0.0,  texelSize.y)).r;
        alphaDown  = SAMPLE_TEXTURE2D(_AlphaTex, sampler_AlphaTex, uv + float2(0.0, -texelSize.y)).r;
    }
#endif

    float neighborMax = max(max(alphaRight, alphaLeft), max(alphaUp, alphaDown));
    float edge = saturate(neighborMax - centerAlpha);
    float outlineAlpha = smoothstep(0.0, max(1e-4, p.softness), edge);
    outlineAlpha *= saturate(p.opacity) * p.color.a;
    if (outlineAlpha <= 1e-6)
        return s;

    float3 outlineColor = p.color.rgb;
    float3 colorOut = lerp(outlineColor, s.color, saturate(s.alpha));
    float alphaOut = max(s.alpha, outlineAlpha * saturate(s.vertexAlpha));
    s.color = colorOut;
    s.alpha = alphaOut;
    return s;
#else
    if (p.enabled < 0.5)
        return s;

    float2 stepUV = Outline2D_ComputeStepUV(s, p);
    if (stepUV.x <= 1e-8 && stepUV.y <= 1e-8)
        return s;

    float centerAlpha = Outline2D_SampleMainAlphaOffset(s, p, float2(0.0, 0.0));
    float minAlpha = centerAlpha;
    float maxAlpha = centerAlpha;

    // Diamond4
    Outline2D_AccumulateMinMax(minAlpha, maxAlpha, Outline2D_SampleMainAlphaOffset(s, p, float2( stepUV.x, 0.0)));
    Outline2D_AccumulateMinMax(minAlpha, maxAlpha, Outline2D_SampleMainAlphaOffset(s, p, float2(-stepUV.x, 0.0)));
    Outline2D_AccumulateMinMax(minAlpha, maxAlpha, Outline2D_SampleMainAlphaOffset(s, p, float2(0.0,  stepUV.y)));
    Outline2D_AccumulateMinMax(minAlpha, maxAlpha, Outline2D_SampleMainAlphaOffset(s, p, float2(0.0, -stepUV.y)));

#if !defined(SURFACE2D_WEBGL_SAFE)
    if (p.samplePattern >= 15.0)
    {
        // Box8 (adds diagonals)
        Outline2D_AccumulateMinMax(minAlpha, maxAlpha, Outline2D_SampleMainAlphaOffset(s, p, float2( stepUV.x,  stepUV.y)));
        Outline2D_AccumulateMinMax(minAlpha, maxAlpha, Outline2D_SampleMainAlphaOffset(s, p, float2( stepUV.x, -stepUV.y)));
        Outline2D_AccumulateMinMax(minAlpha, maxAlpha, Outline2D_SampleMainAlphaOffset(s, p, float2(-stepUV.x,  stepUV.y)));
        Outline2D_AccumulateMinMax(minAlpha, maxAlpha, Outline2D_SampleMainAlphaOffset(s, p, float2(-stepUV.x, -stepUV.y)));
    }

    if (p.samplePattern >= 25.0)
    {
        // Circle12 (adds 4 far cardinal taps)
        float2 farStep = stepUV * 2.0;
        Outline2D_AccumulateMinMax(minAlpha, maxAlpha, Outline2D_SampleMainAlphaOffset(s, p, float2( farStep.x, 0.0)));
        Outline2D_AccumulateMinMax(minAlpha, maxAlpha, Outline2D_SampleMainAlphaOffset(s, p, float2(-farStep.x, 0.0)));
        Outline2D_AccumulateMinMax(minAlpha, maxAlpha, Outline2D_SampleMainAlphaOffset(s, p, float2(0.0,  farStep.y)));
        Outline2D_AccumulateMinMax(minAlpha, maxAlpha, Outline2D_SampleMainAlphaOffset(s, p, float2(0.0, -farStep.y)));
    }
#endif

    bool insideMode = (p.mode >= 15.0);
    float edge = insideMode
        ? saturate(centerAlpha - minAlpha)
        : saturate(maxAlpha - centerAlpha);

    float edgeSoft = max(p.softness, 1e-4);
    float edgeAlpha = (p.softness > 0.0) ? smoothstep(0.0, edgeSoft, edge) : step(1e-4, edge);

    float alphaScale = (p.maskRespect > 0.5)
        ? saturate(s.vertexAlpha * s.alphaFactor)
        : saturate(s.vertexAlpha);
    float outlineAlpha = edgeAlpha * saturate(p.opacity) * p.color.a * alphaScale;
    if (outlineAlpha <= 1e-6)
        return s;

    float3 outlineColor = p.color.rgb;
    if (p.useVertexColor > 0.5)
    {
        outlineColor *= saturate(s.color);
    }

    float4 baseCol = float4(s.color, s.alpha);
    float4 outlineCol = float4(outlineColor, outlineAlpha);
    float4 composed = baseCol;

    if (p.blendMode >= 25.0)
    {
        float3 screened = Outline2D_Screen(baseCol.rgb, outlineCol.rgb);
        composed.rgb = lerp(baseCol.rgb, screened, outlineCol.a);
        composed.a = max(baseCol.a, outlineCol.a);
    }
    else if (p.blendMode >= 15.0)
    {
        composed.rgb = baseCol.rgb + outlineCol.rgb * outlineCol.a;
        composed.a = max(baseCol.a, outlineCol.a);
    }
    else
    {
        composed = insideMode
            ? Outline2D_Over(outlineCol, baseCol)
            : Outline2D_Over(baseCol, outlineCol);
    }

    s.color = composed.rgb;
    s.alpha = composed.a;
    return s;
#endif
}

#endif // GAME_OUTLINE_2D_INCLUDED
