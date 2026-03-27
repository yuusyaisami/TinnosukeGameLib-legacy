Shader "Game/Mesh/MeshChannelSurface"
{
    Properties
    {
        _MeshBaseColor("Base Tint", Color) = (1,1,1,1)

        _MeshContourGradientEnabled("Contour Gradient Enabled", Float) = 1
        _MeshContourGradientColor("Contour Gradient Color", Color) = (1,0.35,0.35,1)
        _MeshContourGradientBlendMode("Contour Gradient Blend Mode", Float) = 30
        _MeshContourGradientStrength("Contour Gradient Strength", Float) = 0.2
        _MeshContourGradientRange("Contour Gradient Range", Float) = 0.5
        _MeshContourGradientFalloff("Contour Gradient Falloff", Float) = 1.5

        _MeshEdgeAlphaEnabled("Edge Alpha Enabled", Float) = 1
        _MeshEdgeAlphaMode("Edge Alpha Mode", Float) = 10
        _MeshEdgeAlphaGain("Edge Alpha Gain", Float) = 0.35
        _MeshEdgeAlphaRange("Edge Alpha Range", Float) = 0.2
        _MeshEdgeAlphaSoftness("Edge Alpha Softness", Float) = 0.65

        _MeshBandsEnabled("Bands Enabled", Float) = 0
        _MeshBandsCount("Bands Count", Float) = 4
        _MeshBandsContrast("Bands Contrast", Float) = 0.65
        _MeshBandsColor("Bands Color", Color) = (0.95,0.95,1,1)
        _MeshBandsBlendMode("Bands Blend Mode", Float) = 30
        _MeshBandsIntensity("Bands Intensity", Float) = 0.25

        _MeshEdgeFlowEnabled("Edge Flow Enabled", Float) = 0
        _MeshEdgeFlowColor("Edge Flow Color", Color) = (1,1,1,1)
        _MeshEdgeFlowBlendMode("Edge Flow Blend Mode", Float) = 30
        _MeshEdgeFlowWidth("Edge Flow Width", Float) = 0.12
        _MeshEdgeFlowSpeed("Edge Flow Speed", Float) = 1.2
        _MeshEdgeFlowIntensity("Edge Flow Intensity", Float) = 0.45

        _MeshInteriorNoiseEnabled("Interior Noise Enabled", Float) = 0
        _MeshInteriorNoiseScale("Interior Noise Scale", Float) = 8
        _MeshInteriorNoiseSpeed("Interior Noise Speed", Float) = 0.5
        _MeshInteriorNoiseStrength("Interior Noise Strength", Float) = 0.08
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex MeshMaterialVert
            #pragma fragment MeshMaterialFrag
            #include "MeshMaterialSurface.hlsl"
            ENDCG
        }
    }
}
