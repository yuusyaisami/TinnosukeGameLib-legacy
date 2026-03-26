#ifndef GAME_MESH_INTERIOR_NOISE_INCLUDED
#define GAME_MESH_INTERIOR_NOISE_INCLUDED

float MeshInteriorNoise_Hash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

void MeshInteriorNoise_Apply(inout MeshMaterialSurfaceContext context)
{
    if (_MeshInteriorNoiseEnabled < 0.5)
        return;

    float2 animatedUV = context.LocalPos * _MeshInteriorNoiseScale + context.TimeSeconds * _MeshInteriorNoiseSpeed;
    float noise = MeshInteriorNoise_Hash21(animatedUV) * 2.0 - 1.0;
    context.Color.rgb += noise.xxx * max(_MeshInteriorNoiseStrength, 0.0);
}

#endif
