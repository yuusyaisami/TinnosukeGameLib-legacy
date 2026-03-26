#ifndef GAME_MESH_EDGE_FLOW_INCLUDED
#define GAME_MESH_EDGE_FLOW_INCLUDED

void MeshEdgeFlow_Apply(inout MeshMaterialSurfaceContext context)
{
    if (_MeshEdgeFlowEnabled < 0.5)
        return;

    float width = max(_MeshEdgeFlowWidth, 0.0001);
    float edgeMask = 1.0 - smoothstep(0.0, width, context.DistanceToContour);
    float wave = sin((context.DistanceNormalized * 32.0) - (context.TimeSeconds * _MeshEdgeFlowSpeed * 6.2831853)) * 0.5 + 0.5;
    float flow = edgeMask * wave * saturate(_MeshEdgeFlowIntensity);
    context.Color.rgb += _MeshEdgeFlowColor.rgb * flow;
}

#endif
