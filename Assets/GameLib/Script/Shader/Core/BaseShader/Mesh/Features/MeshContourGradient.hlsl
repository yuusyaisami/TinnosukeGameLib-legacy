#ifndef GAME_MESH_CONTOUR_GRADIENT_INCLUDED
#define GAME_MESH_CONTOUR_GRADIENT_INCLUDED

void MeshContourGradient_Apply(inout MeshMaterialSurfaceContext context)
{
    if (_MeshContourGradientEnabled < 0.5)
        return;

    float normalized = saturate(context.DistanceToContour / max(_MeshContourGradientRange, 0.0001));
    float contourMask = 1.0 - normalized;
    float shaped = pow(saturate(contourMask), max(_MeshContourGradientFalloff, 0.0001));
    context.Color.rgb = MeshMaterialSurface_BlendColor(
        context.Color.rgb,
        _MeshContourGradientColor.rgb,
        shaped * saturate(_MeshContourGradientStrength),
        _MeshContourGradientBlendMode);
}

#endif
