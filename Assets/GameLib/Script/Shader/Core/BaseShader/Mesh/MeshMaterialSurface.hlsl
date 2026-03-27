#ifndef GAME_MESH_MATERIAL_SURFACE_INCLUDED
#define GAME_MESH_MATERIAL_SURFACE_INCLUDED

#define GAME_MESH_MAX_CONTOUR_SAMPLES 256

#define GAME_MESH_BLEND_OVERRIDE 10.0
#define GAME_MESH_BLEND_ADD 20.0
#define GAME_MESH_BLEND_MULTIPLY 30.0
#define GAME_MESH_BLEND_OVERLAY 40.0

#include "UnityCG.cginc"

CBUFFER_START(UnityPerMaterial)
    float4 _MeshBaseColor;

    float _MeshContourGradientEnabled;
    float4 _MeshContourGradientColor;
    float _MeshContourGradientBlendMode;
    float _MeshContourGradientStrength;
    float _MeshContourGradientRange;
    float _MeshContourGradientFalloff;

    float _MeshEdgeAlphaEnabled;
    float _MeshEdgeAlphaMode;
    float _MeshEdgeAlphaGain;
    float _MeshEdgeAlphaRange;
    float _MeshEdgeAlphaSoftness;

    float _MeshBandsEnabled;
    float _MeshBandsCount;
    float _MeshBandsContrast;
    float4 _MeshBandsColor;
    float _MeshBandsBlendMode;
    float _MeshBandsIntensity;

    float _MeshEdgeFlowEnabled;
    float4 _MeshEdgeFlowColor;
    float _MeshEdgeFlowBlendMode;
    float _MeshEdgeFlowWidth;
    float _MeshEdgeFlowSpeed;
    float _MeshEdgeFlowIntensity;

    float _MeshInteriorNoiseEnabled;
    float _MeshInteriorNoiseScale;
    float _MeshInteriorNoiseSpeed;
    float _MeshInteriorNoiseStrength;
CBUFFER_END

float _MeshContourSampleCount;
float4 _MeshContourBounds;
float4 _MeshContourSamples[GAME_MESH_MAX_CONTOUR_SAMPLES];

struct MeshMaterialSurfaceContext
{
    float2 LocalPos;
    float2 UV;
    float DistanceToContour;
    float DistanceNormalized;
    float TimeSeconds;
    float4 Color;
};

struct MeshMaterialAppData
{
    float4 vertex : POSITION;
    float2 uv : TEXCOORD0;
    float4 color : COLOR;
};

struct MeshMaterialVaryings
{
    float4 vertex : SV_POSITION;
    float2 uv : TEXCOORD0;
    float4 color : COLOR;
    float2 localPos : TEXCOORD1;
};

float3 MeshMaterialSurface_Overlay(float3 baseColor, float3 effectColor)
{
    float3 lower = 2.0 * baseColor * effectColor;
    float3 upper = 1.0 - (2.0 * (1.0 - baseColor) * (1.0 - effectColor));
    return lerp(lower, upper, step(0.5, baseColor));
}

float3 MeshMaterialSurface_BlendColor(float3 baseColor, float3 effectColor, float amount, float mode)
{
    float safeAmount = saturate(amount);
    if (safeAmount <= 0.0)
        return baseColor;

    float3 blended = baseColor * effectColor;
    if (mode < 15.0)
        blended = effectColor;
    else if (mode < 25.0)
        blended = baseColor + effectColor;
    else if (mode < 35.0)
        blended = baseColor * effectColor;
    else
        blended = MeshMaterialSurface_Overlay(baseColor, effectColor);

    return lerp(baseColor, blended, safeAmount);
}

#include "Features/MeshContourDistance.hlsl"
#include "Features/MeshContourGradient.hlsl"
#include "Features/MeshEdgeAlpha.hlsl"
#include "Features/MeshBands.hlsl"
#include "Features/MeshEdgeFlow.hlsl"
#include "Features/MeshInteriorNoise.hlsl"

MeshMaterialVaryings MeshMaterialVert(MeshMaterialAppData input)
{
    MeshMaterialVaryings output;
    output.vertex = UnityObjectToClipPos(input.vertex);
    output.uv = input.uv;
    output.color = input.color;
    output.localPos = input.vertex.xy;
    return output;
}

float4 MeshMaterialFrag(MeshMaterialVaryings input) : SV_Target
{
    MeshMaterialSurfaceContext context;
    context.LocalPos = input.localPos;
    context.UV = input.uv;
    context.TimeSeconds = _Time.y;
    context.Color = _MeshBaseColor * input.color;
    context.DistanceToContour = MeshContourDistance_MinDistance(
        context.LocalPos,
        (int)_MeshContourSampleCount,
        GAME_MESH_MAX_CONTOUR_SAMPLES);
    context.DistanceNormalized = MeshContourDistance_Normalize(
        context.DistanceToContour,
        _MeshContourBounds);

    MeshContourGradient_Apply(context);
    MeshEdgeAlpha_Apply(context);
    MeshBands_Apply(context);
    MeshEdgeFlow_Apply(context);
    MeshInteriorNoise_Apply(context);

    context.Color.rgb = saturate(context.Color.rgb);
    context.Color.a = saturate(context.Color.a);
    return context.Color;
}

#endif
