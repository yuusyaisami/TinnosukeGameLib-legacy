#ifndef GAME_MESH_EDGE_ALPHA_INCLUDED
#define GAME_MESH_EDGE_ALPHA_INCLUDED

void MeshEdgeAlpha_Apply(inout MeshMaterialSurfaceContext context)
{
    if (_MeshEdgeAlphaEnabled < 0.5)
        return;

    float range = max(_MeshEdgeAlphaRange, 0.0001);
    float softness = lerp(0.0001, range, saturate(_MeshEdgeAlphaSoftness));
    float edgeMask = 1.0 - smoothstep(softness, range, context.DistanceToContour);
    float baseAlpha = saturate(context.Color.a);
    float fadedAlpha = baseAlpha * saturate(1.0 - max(_MeshEdgeAlphaGain, 0.0));

    if (_MeshEdgeAlphaMode < 15.0)
    {
        context.Color.a = lerp(fadedAlpha, baseAlpha, edgeMask);
        return;
    }

    context.Color.a = lerp(baseAlpha, fadedAlpha, edgeMask);
}

#endif
