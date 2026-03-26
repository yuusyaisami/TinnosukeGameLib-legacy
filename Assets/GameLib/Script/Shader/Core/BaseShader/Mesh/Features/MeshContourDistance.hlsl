#ifndef GAME_MESH_CONTOUR_DISTANCE_INCLUDED
#define GAME_MESH_CONTOUR_DISTANCE_INCLUDED

#ifndef GAME_MESH_MAX_CONTOUR_SAMPLES
#define GAME_MESH_MAX_CONTOUR_SAMPLES 64
#endif

float MeshContourDistance_MinDistance(float2 localPos, int sampleCount, int sampleCapacity)
{
    float minDistance = 999999.0;
    int safeCount = clamp(sampleCount, 0, sampleCapacity);
    [loop]
    for (int i = 0; i < safeCount; i++)
    {
        float2 samplePoint = _MeshContourSamples[i].xy;
        minDistance = min(minDistance, distance(localPos, samplePoint));
    }

    return safeCount > 0 ? minDistance : 0.0;
}

float MeshContourDistance_Normalize(float distanceToContour, float4 contourBounds)
{
    float2 size = max(contourBounds.zw - contourBounds.xy, float2(0.0001, 0.0001));
    float normalizer = max(size.x, size.y);
    return saturate(distanceToContour / normalizer);
}

#endif
