#ifndef GAME_MESH_MATERIAL_SURFACE_INCLUDED
#define GAME_MESH_MATERIAL_SURFACE_INCLUDED

#define GAME_MESH_MAX_CONTOUR_SAMPLES 64

#include "UnityCG.cginc"

CBUFFER_START(UnityPerMaterial)
    float4 _MeshBaseColor;

    float _MeshContourGradientEnabled;
    float4 _MeshContourGradientColor;
    float _MeshContourGradientStrength;
    float _MeshContourGradientRange;
    float _MeshContourGradientFalloff;

    float _MeshEdgeAlphaEnabled;
    float _MeshEdgeAlphaGain;
    float _MeshEdgeAlphaRange;
    float _MeshEdgeAlphaSoftness;

    float _MeshBandsEnabled;
    float _MeshBandsCount;
    float _MeshBandsContrast;
    float4 _MeshBandsColor;
    float _MeshBandsIntensity;

    float _MeshEdgeFlowEnabled;
    float4 _MeshEdgeFlowColor;
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
