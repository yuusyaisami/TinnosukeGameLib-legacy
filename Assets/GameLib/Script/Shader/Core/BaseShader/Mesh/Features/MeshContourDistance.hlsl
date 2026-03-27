#ifndef GAME_MESH_CONTOUR_DISTANCE_INCLUDED
#define GAME_MESH_CONTOUR_DISTANCE_INCLUDED

#ifndef GAME_MESH_MAX_CONTOUR_SAMPLES
#define GAME_MESH_MAX_CONTOUR_SAMPLES 256
#endif

float MeshContourDistance_PointToSegment(float2 positionWS, float2 a, float2 b)
{
    float2 segment = b - a;
    float lengthSq = dot(segment, segment);
    if (lengthSq <= 0.000001)
        return distance(positionWS, a);

    float t = saturate(dot(positionWS - a, segment) / lengthSq);
    float2 projection = a + segment * t;
    return distance(positionWS, projection);
}

float MeshContourDistance_MinDistance(float2 localPos, int sampleCount, int sampleCapacity)
{
    float minDistance = 999999.0;
    int safeCount = clamp(sampleCount, 0, sampleCapacity);

    [loop]
    for (int i = 0; i < safeCount; i++)
    {
        float4 segment = _MeshContourSamples[i];
        minDistance = min(minDistance, MeshContourDistance_PointToSegment(localPos, segment.xy, segment.zw));
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
