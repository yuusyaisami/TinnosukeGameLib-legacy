#ifndef GAME_OUTLINE_2D_INCLUDED
#define GAME_OUTLINE_2D_INCLUDED

// ============================================================================
// Outline2D.hlsl - Generic sprite/UI outline effect
// ============================================================================

#include "Assets/GameLib/Script/Shader/Core/BaseShader/Features/ColorSpaceUtils.hlsl"
#include "Assets/GameLib/Script/Shader/Core/BaseShader/Features/AnimatedNoise2D.hlsl"

#define OUTLINE2D_DIRECTION_LEFT   1.0
#define OUTLINE2D_DIRECTION_RIGHT  2.0
#define OUTLINE2D_DIRECTION_UP     4.0
#define OUTLINE2D_DIRECTION_DOWN   8.0

struct Outline2DParams
{
    float  enabled;
    float  mode;            // 10: Outside, 20: Inside
    float4 color;
    float  directionMask;
    float  autoColorEnabled;
    float  autoColorMode;
    float  autoHue;
    float  autoSaturation;
    float  autoLightness;
    float  animatedGradientEnabled;
    float  animatedGradientPatternType;
    float  animatedGradientMasterStrength;
    float  animatedGradientNoiseScale;
    float2 animatedGradientNoiseDirection;
    float  animatedGradientNoiseSpeed;
    float2 animatedGradientNoiseOffset;
    float  animatedGradientRotationSpeed;
    float  animatedGradientPulseAmplitude;
    float  animatedGradientPulseSpeed;
    float  animatedGradientWarpPatternType;
    float  animatedGradientWarpScale;
    float  animatedGradientWarpStrength;
    float2 animatedGradientWarpDirection;
    float  animatedGradientWarpSpeed;
    float  animatedGradientLoopSeconds;
    float  animatedGradientOctaves;
    float  animatedGradientLacunarity;
    float  animatedGradientGain;
    float  animatedGradientCellSharpness;
    float  animatedGradientPatternContrast;
    float  animatedGradientHueAmplitude;
    float  animatedGradientSaturationAmplitude;
    float  animatedGradientLightnessAmplitude;
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
    float directionMask,
    float autoColorEnabled,
    float autoColorMode,
    float autoHue,
    float autoSaturation,
    float autoLightness,
    float animatedGradientEnabled,
    float animatedGradientPatternType,
    float animatedGradientMasterStrength,
    float animatedGradientNoiseScale,
    float2 animatedGradientNoiseDirection,
    float animatedGradientNoiseSpeed,
    float2 animatedGradientNoiseOffset,
    float animatedGradientRotationSpeed,
    float animatedGradientPulseAmplitude,
    float animatedGradientPulseSpeed,
    float animatedGradientWarpPatternType,
    float animatedGradientWarpScale,
    float animatedGradientWarpStrength,
    float2 animatedGradientWarpDirection,
    float animatedGradientWarpSpeed,
    float animatedGradientLoopSeconds,
    float animatedGradientOctaves,
    float animatedGradientLacunarity,
    float animatedGradientGain,
    float animatedGradientCellSharpness,
    float animatedGradientPatternContrast,
    float animatedGradientHueAmplitude,
    float animatedGradientSaturationAmplitude,
    float animatedGradientLightnessAmplitude,
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
    Outline2DParams p;
    p.enabled = enabled;
    p.mode = mode;
    p.color = color;
    p.directionMask = directionMask;
    p.autoColorEnabled = autoColorEnabled;
    p.autoColorMode = autoColorMode;
    p.autoHue = autoHue;
    p.autoSaturation = autoSaturation;
    p.autoLightness = autoLightness;
    p.animatedGradientEnabled = animatedGradientEnabled;
    p.animatedGradientPatternType = animatedGradientPatternType;
    p.animatedGradientMasterStrength = max(animatedGradientMasterStrength, 0.0);
    p.animatedGradientNoiseScale = animatedGradientNoiseScale;
    p.animatedGradientNoiseDirection = animatedGradientNoiseDirection;
    p.animatedGradientNoiseSpeed = animatedGradientNoiseSpeed;
    p.animatedGradientNoiseOffset = animatedGradientNoiseOffset;
    p.animatedGradientRotationSpeed = animatedGradientRotationSpeed;
    p.animatedGradientPulseAmplitude = max(animatedGradientPulseAmplitude, 0.0);
    p.animatedGradientPulseSpeed = animatedGradientPulseSpeed;
    p.animatedGradientWarpPatternType = animatedGradientWarpPatternType;
    p.animatedGradientWarpScale = animatedGradientWarpScale;
    p.animatedGradientWarpStrength = animatedGradientWarpStrength;
    p.animatedGradientWarpDirection = animatedGradientWarpDirection;
    p.animatedGradientWarpSpeed = animatedGradientWarpSpeed;
    p.animatedGradientLoopSeconds = animatedGradientLoopSeconds;
    p.animatedGradientOctaves = animatedGradientOctaves;
    p.animatedGradientLacunarity = animatedGradientLacunarity;
    p.animatedGradientGain = animatedGradientGain;
    p.animatedGradientCellSharpness = animatedGradientCellSharpness;
    p.animatedGradientPatternContrast = animatedGradientPatternContrast;
    p.animatedGradientHueAmplitude = animatedGradientHueAmplitude;
    p.animatedGradientSaturationAmplitude = animatedGradientSaturationAmplitude;
    p.animatedGradientLightnessAmplitude = animatedGradientLightnessAmplitude;
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

inline bool Outline2DDirectionMaskHasBit(float roundedDirectionMask, float bitValue)
{
    int mask = (int)roundedDirectionMask;
    int bit = (int)bitValue;
    return (mask & bit) != 0;
}

inline void Outline2D_AccumulateMinMax(inout float minA, inout float maxA, float v)
{
    minA = min(minA, v);
    maxA = max(maxA, v);
}

inline void Outline2D_AccumulateDirectionalSamples(
    Surface2D s,
    Outline2DParams p,
    float roundedDirectionMask,
    float2 stepUV,
    bool includeDiagonals,
    bool includeFarCardinals,
    inout float minAlpha,
    inout float maxAlpha)
{
    bool hasLeft = Outline2DDirectionMaskHasBit(roundedDirectionMask, OUTLINE2D_DIRECTION_LEFT);
    bool hasRight = Outline2DDirectionMaskHasBit(roundedDirectionMask, OUTLINE2D_DIRECTION_RIGHT);
    bool hasUp = Outline2DDirectionMaskHasBit(roundedDirectionMask, OUTLINE2D_DIRECTION_UP);
    bool hasDown = Outline2DDirectionMaskHasBit(roundedDirectionMask, OUTLINE2D_DIRECTION_DOWN);

    if (hasRight)
        Outline2D_AccumulateMinMax(minAlpha, maxAlpha, Outline2D_SampleMainAlphaOffset(s, p, float2(stepUV.x, 0.0)));
    if (hasLeft)
        Outline2D_AccumulateMinMax(minAlpha, maxAlpha, Outline2D_SampleMainAlphaOffset(s, p, float2(-stepUV.x, 0.0)));
    if (hasUp)
        Outline2D_AccumulateMinMax(minAlpha, maxAlpha, Outline2D_SampleMainAlphaOffset(s, p, float2(0.0, stepUV.y)));
    if (hasDown)
        Outline2D_AccumulateMinMax(minAlpha, maxAlpha, Outline2D_SampleMainAlphaOffset(s, p, float2(0.0, -stepUV.y)));

    if (includeDiagonals)
    {
        if (hasRight && hasUp)
            Outline2D_AccumulateMinMax(minAlpha, maxAlpha, Outline2D_SampleMainAlphaOffset(s, p, float2(stepUV.x, stepUV.y)));
        if (hasRight && hasDown)
            Outline2D_AccumulateMinMax(minAlpha, maxAlpha, Outline2D_SampleMainAlphaOffset(s, p, float2(stepUV.x, -stepUV.y)));
        if (hasLeft && hasUp)
            Outline2D_AccumulateMinMax(minAlpha, maxAlpha, Outline2D_SampleMainAlphaOffset(s, p, float2(-stepUV.x, stepUV.y)));
        if (hasLeft && hasDown)
            Outline2D_AccumulateMinMax(minAlpha, maxAlpha, Outline2D_SampleMainAlphaOffset(s, p, float2(-stepUV.x, -stepUV.y)));
    }

    if (includeFarCardinals)
    {
        float2 farStep = stepUV * 2.0;
        if (hasRight)
            Outline2D_AccumulateMinMax(minAlpha, maxAlpha, Outline2D_SampleMainAlphaOffset(s, p, float2(farStep.x, 0.0)));
        if (hasLeft)
            Outline2D_AccumulateMinMax(minAlpha, maxAlpha, Outline2D_SampleMainAlphaOffset(s, p, float2(-farStep.x, 0.0)));
        if (hasUp)
            Outline2D_AccumulateMinMax(minAlpha, maxAlpha, Outline2D_SampleMainAlphaOffset(s, p, float2(0.0, farStep.y)));
        if (hasDown)
            Outline2D_AccumulateMinMax(minAlpha, maxAlpha, Outline2D_SampleMainAlphaOffset(s, p, float2(0.0, -farStep.y)));
    }
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

inline AnimatedNoise2DMotionParams Outline2D_MakeAnimatedGradientNoiseParams(Outline2DParams p)
{
    return MakeAnimatedNoise2DMotionParamsFull(
        p.animatedGradientEnabled,
        p.animatedGradientPatternType,
        p.animatedGradientNoiseScale,
        p.animatedGradientNoiseDirection,
        p.animatedGradientNoiseSpeed,
        p.animatedGradientNoiseOffset,
        p.animatedGradientRotationSpeed,
        p.animatedGradientPulseAmplitude,
        p.animatedGradientPulseSpeed,
        p.animatedGradientWarpPatternType,
        p.animatedGradientWarpScale,
        p.animatedGradientWarpStrength,
        p.animatedGradientWarpDirection,
        p.animatedGradientWarpSpeed,
        p.animatedGradientLoopSeconds,
        p.animatedGradientOctaves,
        p.animatedGradientLacunarity,
        p.animatedGradientGain,
        p.animatedGradientCellSharpness,
        p.animatedGradientPatternContrast);
}

inline float3 ResolveOutline2DColor(Surface2D s, Outline2DParams p)
{
    float3 outlineColor = p.color.rgb;
    if (p.autoColorEnabled > 0.5)
    {
        half3 hsl = RGBtoHSL(saturate((half3)s.color));
        hsl.x = frac(hsl.x + (half)p.autoHue);

        if (p.autoColorMode > 0.5)
        {
            hsl.y = ApplySignedHeadroomAdjust(hsl.y, (half)p.autoSaturation);
            hsl.z = ApplySignedHeadroomAdjust(hsl.z, (half)p.autoLightness);
        }
        else
        {
            hsl.y = saturate(hsl.y + (half)p.autoSaturation);
            hsl.z = saturate(hsl.z + (half)p.autoLightness);
        }

        outlineColor = HSLtoRGB(hsl) * outlineColor;
    }
    else if (p.useVertexColor > 0.5)
    {
        outlineColor *= saturate(s.color);
    }

    if (p.animatedGradientEnabled > 0.5 && p.animatedGradientMasterStrength > 1e-5)
    {
        AnimatedNoise2DMotionParams motion = Outline2D_MakeAnimatedGradientNoiseParams(p);
        float time = _Time.y;
        half3 hsl = RGBtoHSL(saturate((half3)outlineColor));
        float hueWobble;
        float satWobble;
        float lightWobble;
        AnimatedNoise2D_SampleSignedTriplet(s.uvLocal, motion, time, hueWobble, satWobble, lightWobble);
        float master = p.animatedGradientMasterStrength;

        hsl.x = frac(hsl.x + (half)(hueWobble * p.animatedGradientHueAmplitude * master));
        hsl.y = saturate(hsl.y + (half)(satWobble * p.animatedGradientSaturationAmplitude * master));
        hsl.z = saturate(hsl.z + (half)(lightWobble * p.animatedGradientLightnessAmplitude * master));
        outlineColor = HSLtoRGB(hsl);
    }

    return outlineColor;
}

inline Surface2D Surface2D_ApplyOutline(Surface2D s, Outline2DParams p)
{
    Surface2D result = s;
#if defined(SURFACE2D_WEBGL_SAFE)
    if (p.enabled >= 0.5 && p.directionMask >= 0.5)
    {
        float2 texelSize = _MainTex_TexelSize.xy * max(p.width, 0.0);
        if (texelSize.x > 1e-8 || texelSize.y > 1e-8)
        {
            float2 uv = result.uvMain;
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

            float roundedDirectionMask = max(0.0, floor(p.directionMask + 0.5));
            bool hasLeft = Outline2DDirectionMaskHasBit(roundedDirectionMask, OUTLINE2D_DIRECTION_LEFT);
            bool hasRight = Outline2DDirectionMaskHasBit(roundedDirectionMask, OUTLINE2D_DIRECTION_RIGHT);
            bool hasUp = Outline2DDirectionMaskHasBit(roundedDirectionMask, OUTLINE2D_DIRECTION_UP);
            bool hasDown = Outline2DDirectionMaskHasBit(roundedDirectionMask, OUTLINE2D_DIRECTION_DOWN);

            float neighborMax = centerAlpha;
            if (hasRight) neighborMax = max(neighborMax, alphaRight);
            if (hasLeft) neighborMax = max(neighborMax, alphaLeft);
            if (hasUp) neighborMax = max(neighborMax, alphaUp);
            if (hasDown) neighborMax = max(neighborMax, alphaDown);

            float edge = saturate(neighborMax - centerAlpha);
            float outlineAlpha = smoothstep(0.0, max(1e-4, p.softness), edge);
            outlineAlpha *= saturate(p.opacity) * p.color.a;
            if (outlineAlpha > 1e-6)
            {
                float3 outlineColor = ResolveOutline2DColor(result, p);
                float3 colorOut = lerp(outlineColor, result.color, saturate(result.alpha));
                float alphaOut = max(result.alpha, outlineAlpha * saturate(result.vertexAlpha));
                result.color = colorOut;
                result.alpha = alphaOut;
            }
        }
    }
#else
    if (p.enabled >= 0.5 && p.directionMask >= 0.5)
    {
        float2 stepUV = Outline2D_ComputeStepUV(result, p);
        if (stepUV.x > 1e-8 || stepUV.y > 1e-8)
        {
            float centerAlpha = Outline2D_SampleMainAlphaOffset(result, p, float2(0.0, 0.0));
            float minAlpha = centerAlpha;
            float maxAlpha = centerAlpha;
            float roundedDirectionMask = max(0.0, floor(p.directionMask + 0.5));
            Outline2D_AccumulateDirectionalSamples(
                result,
                p,
                roundedDirectionMask,
                stepUV,
                p.samplePattern >= 15.0,
                p.samplePattern >= 25.0,
                minAlpha,
                maxAlpha);

            bool insideMode = (p.mode >= 15.0);
            float edge = insideMode
                ? saturate(centerAlpha - minAlpha)
                : saturate(maxAlpha - centerAlpha);

            float edgeSoft = max(p.softness, 1e-4);
            float edgeAlpha = (p.softness > 0.0) ? smoothstep(0.0, edgeSoft, edge) : step(1e-4, edge);

            float alphaScale = (p.maskRespect > 0.5)
                ? saturate(result.vertexAlpha * result.alphaFactor)
                : saturate(result.vertexAlpha);
            float outlineAlpha = edgeAlpha * saturate(p.opacity) * p.color.a * alphaScale;
            if (outlineAlpha > 1e-6)
            {
                float3 outlineColor = ResolveOutline2DColor(result, p);
                float4 baseCol = float4(result.color, result.alpha);
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

                result.color = composed.rgb;
                result.alpha = composed.a;
            }
        }
    }
#endif

    return result;
}

#endif // GAME_OUTLINE_2D_INCLUDED
