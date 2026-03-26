#ifndef GAME_MESH_BANDS_INCLUDED
#define GAME_MESH_BANDS_INCLUDED

void MeshBands_Apply(inout MeshMaterialSurfaceContext context)
{
    if (_MeshBandsEnabled < 0.5)
        return;

    float count = max(_MeshBandsCount, 1.0);
    float stepped = frac(context.DistanceNormalized * count);
    float pulse = smoothstep(0.5 - _MeshBandsContrast * 0.5, 0.5 + _MeshBandsContrast * 0.5, stepped);
    context.Color.rgb = lerp(context.Color.rgb, _MeshBandsColor.rgb, pulse * saturate(_MeshBandsIntensity));
}

#endif
