// Game.Collision.CollisionJobs.cs
//
// Burst-compiled jobs for CollisionSystem v2.3 broadphase and narrowphase.

using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Collision
{
    [BurstCompile]
    public struct BuildDynamicGridJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float2> Positions;
        public float InvCellSize;
        public NativeParallelMultiHashMap<long, int>.ParallelWriter GridWriter;

        public void Execute(int index)
        {
            var pos = Positions[index];
            int cellX = (int)math.floor(pos.x * InvCellSize);
            int cellY = (int)math.floor(pos.y * InvCellSize);
            long key = SpatialGrid.PackCellKey(cellX, cellY);
            GridWriter.Add(key, index);
        }
    }

    [BurstCompile]
    public struct BuildStaticGridJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float2> Centers;
        [ReadOnly] public NativeArray<bool> IsLargeStatic;
        public float InvCellSize;
        public NativeParallelMultiHashMap<long, int>.ParallelWriter GridWriter;

        public void Execute(int index)
        {
            if (IsLargeStatic[index])
                return;

            var center = Centers[index];
            int cellX = (int)math.floor(center.x * InvCellSize);
            int cellY = (int)math.floor(center.y * InvCellSize);
            long key = SpatialGrid.PackCellKey(cellX, cellY);
            GridWriter.Add(key, index);
        }
    }

    [BurstCompile]
    public struct QueryDynDynJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float2> Positions;
        [ReadOnly] public NativeArray<float> Radii;
        [ReadOnly] public NativeArray<uint> LayerBits;
        [ReadOnly] public NativeArray<uint> HitMasks;
        [ReadOnly] public NativeArray<DynamicColliderSetId> SetIds;

        [ReadOnly] public NativeParallelMultiHashMap<long, int> Grid;

        public NativeQueue<CollisionHitRaw>.ParallelWriter Hits;

        public bool SameSetNoDup;
        public bool CrossSetOnly;

        public float InvCellSize;
        public int NeighborRange;

        public void Execute(int queryIdx)
        {
            var queryPos = Positions[queryIdx];
            var queryRadius = Radii[queryIdx];
            var queryHitMask = HitMasks[queryIdx];
            var querySetId = SetIds[queryIdx];

            int baseCellX = (int)math.floor(queryPos.x * InvCellSize);
            int baseCellY = (int)math.floor(queryPos.y * InvCellSize);

            for (int dy = -NeighborRange; dy <= NeighborRange; dy++)
                for (int dx = -NeighborRange; dx <= NeighborRange; dx++)
                {
                    long cellKey = SpatialGrid.PackCellKey(baseCellX + dx, baseCellY + dy);

                    if (!Grid.TryGetFirstValue(cellKey, out int targetIdx, out var it))
                        continue;

                    do
                    {
                        if (targetIdx == queryIdx)
                            continue;

                        bool sameSet = SetIds[targetIdx] == querySetId;
                        if (SameSetNoDup && sameSet && targetIdx <= queryIdx)
                            continue;
                        if (CrossSetOnly && sameSet)
                            continue;

                        // Layer mask (directional)
                        var targetLayer = LayerBits[targetIdx];
                        if ((queryHitMask & targetLayer) == 0)
                            continue;

                        var targetPos = Positions[targetIdx];
                        var targetRadius = Radii[targetIdx];

                        float2 delta = targetPos - queryPos;
                        float distSq = math.lengthsq(delta);
                        float sumRadius = queryRadius + targetRadius;
                        float sumSq = sumRadius * sumRadius;

                        if (distSq >= sumSq)
                            continue;

                        float dist = math.sqrt(distSq);
                        float2 normal = dist > 1e-6f ? delta / dist : new float2(1f, 0f);
                        float penetration = sumRadius - dist;

                        float2 contactPoint = queryPos + normal * (queryRadius - penetration * 0.5f);

                        Hits.Enqueue(new CollisionHitRaw
                        {
                            Kind = CollisionKind.DynamicDynamic,
                            SelfDenseIndex = queryIdx,
                            OtherDenseIndex = targetIdx,
                            Point = contactPoint,
                            Normal = normal,
                            Penetration = penetration,
                            Reflect = ReflectFlags.None
                        });
                    }
                    while (Grid.TryGetNextValue(out targetIdx, ref it));
                }
        }
    }

    [BurstCompile]
    public struct QueryDynStaticJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float2> DynPositions;
        [ReadOnly] public NativeArray<float> DynRadii;
        [ReadOnly] public NativeArray<uint> DynHitMasks;

        [ReadOnly] public NativeArray<float2> StaticCenters;
        [ReadOnly] public NativeArray<float2> StaticHalfExtents;
        [ReadOnly] public NativeArray<uint> StaticLayerBits;

        [ReadOnly] public NativeParallelMultiHashMap<long, int> StaticGrid;

        [ReadOnly] public NativeArray<int> LargeStaticDenseIndices;
        public int LargeStaticCount;

        public NativeQueue<CollisionHitRaw>.ParallelWriter Hits;

        public float InvCellSize;
        public int NeighborRange;

        public void Execute(int dynIdx)
        {
            var pos = DynPositions[dynIdx];
            var radius = DynRadii[dynIdx];
            var hitMask = DynHitMasks[dynIdx];

            int baseCellX = (int)math.floor(pos.x * InvCellSize);
            int baseCellY = (int)math.floor(pos.y * InvCellSize);

            for (int dy = -NeighborRange; dy <= NeighborRange; dy++)
                for (int dx = -NeighborRange; dx <= NeighborRange; dx++)
                {
                    long cellKey = SpatialGrid.PackCellKey(baseCellX + dx, baseCellY + dy);

                    if (!StaticGrid.TryGetFirstValue(cellKey, out int staticIdx, out var it))
                        continue;

                    do
                    {
                        var staticLayer = StaticLayerBits[staticIdx];
                        if ((hitMask & staticLayer) == 0)
                            continue;

                        var aabbCenter = StaticCenters[staticIdx];
                        var aabbHalf = StaticHalfExtents[staticIdx];

                        if (!TryComputeCircleAabbHit(pos, radius, aabbCenter, aabbHalf,
                            out float2 point, out float2 normal, out float penetration, out ReflectFlags reflect))
                            continue;

                        Hits.Enqueue(new CollisionHitRaw
                        {
                            Kind = CollisionKind.DynamicStatic,
                            SelfDenseIndex = dynIdx,
                            OtherDenseIndex = staticIdx,
                            Point = point,
                            Normal = normal,
                            Penetration = penetration,
                            Reflect = reflect
                        });
                    }
                    while (StaticGrid.TryGetNextValue(out staticIdx, ref it));
                }

            // Large statics: grid外の別経路（線形）
            for (int i = 0; i < LargeStaticCount; i++)
            {
                int staticIdx = LargeStaticDenseIndices[i];
                if ((uint)staticIdx >= (uint)StaticCenters.Length)
                    continue;

                var staticLayer = StaticLayerBits[staticIdx];
                if ((hitMask & staticLayer) == 0)
                    continue;

                var aabbCenter = StaticCenters[staticIdx];
                var aabbHalf = StaticHalfExtents[staticIdx];

                if (!TryComputeCircleAabbHit(pos, radius, aabbCenter, aabbHalf,
                    out float2 point, out float2 normal, out float penetration, out ReflectFlags reflect))
                    continue;

                Hits.Enqueue(new CollisionHitRaw
                {
                    Kind = CollisionKind.DynamicStatic,
                    SelfDenseIndex = dynIdx,
                    OtherDenseIndex = staticIdx,
                    Point = point,
                    Normal = normal,
                    Penetration = penetration,
                    Reflect = reflect
                });
            }
        }

        static bool TryComputeCircleAabbHit(
            float2 circleCenter, float radius,
            float2 aabbCenter, float2 aabbHalf,
            out float2 point, out float2 normal, out float penetration, out ReflectFlags reflect)
        {
            point = default;
            normal = default;
            penetration = 0f;
            reflect = ReflectFlags.None;

            float2 closest = math.clamp(circleCenter, aabbCenter - aabbHalf, aabbCenter + aabbHalf);
            float2 delta = circleCenter - closest;
            float distSq = math.lengthsq(delta);

            if (distSq >= radius * radius)
                return false;

            if (distSq > 1e-8f)
            {
                float dist = math.sqrt(distSq);
                normal = delta / dist;
                penetration = radius - dist;
                point = closest; // surface point
            }
            else
            {
                // inside AABB: pick minimum axis to exit, and SNAP point onto face (surface).
                float2 offset = circleCenter - aabbCenter;
                float2 absOffset = math.abs(offset);
                float2 toEdge = aabbHalf - absOffset;

                if (toEdge.x < toEdge.y)
                {
                    float sign = offset.x >= 0f ? 1f : -1f;
                    normal = new float2(sign, 0f);
                    point = new float2(aabbCenter.x + aabbHalf.x * sign, circleCenter.y);
                    penetration = radius + toEdge.x;
                }
                else
                {
                    float sign = offset.y >= 0f ? 1f : -1f;
                    normal = new float2(0f, sign);
                    point = new float2(circleCenter.x, aabbCenter.y + aabbHalf.y * sign);
                    penetration = radius + toEdge.y;
                }
            }

            reflect = CollisionJobMath.ReflectFromNormal(normal);
            return true;
        }
    }

    [BurstCompile]
    public struct BoundaryCheckJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float2> Positions;
        [ReadOnly] public NativeArray<float> Radii;
        public float4 BoundaryXYXY;

        public NativeQueue<CollisionHitRaw>.ParallelWriter Hits;

        public void Execute(int idx)
        {
            var pos = Positions[idx];
            var radius = Radii[idx];

            if (pos.x - radius < BoundaryXYXY.x)
                if (!TryWriteHit(idx, new float2(BoundaryXYXY.x, pos.y), new float2(1, 0))) return;

            if (pos.x + radius > BoundaryXYXY.z)
                if (!TryWriteHit(idx, new float2(BoundaryXYXY.z, pos.y), new float2(-1, 0))) return;

            if (pos.y - radius < BoundaryXYXY.y)
                if (!TryWriteHit(idx, new float2(pos.x, BoundaryXYXY.y), new float2(0, 1))) return;

            if (pos.y + radius > BoundaryXYXY.w)
                if (!TryWriteHit(idx, new float2(pos.x, BoundaryXYXY.w), new float2(0, -1))) return;
        }

        bool TryWriteHit(int selfIdx, float2 point, float2 normal)
        {
            Hits.Enqueue(new CollisionHitRaw
            {
                Kind = CollisionKind.DynamicStatic,
                SelfDenseIndex = selfIdx,
                OtherDenseIndex = -1,
                Point = point,
                Normal = normal,
                Penetration = 0f,
                Reflect = CollisionJobMath.ReflectFromNormal(normal),
            });
            return true;
        }
    }

    internal static class CollisionJobMath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReflectFlags ReflectFromNormal(float2 normal)
        {
            ReflectFlags reflect = ReflectFlags.None;
            if (math.abs(normal.x) > 0.5f) reflect |= ReflectFlags.FlipX;
            if (math.abs(normal.y) > 0.5f) reflect |= ReflectFlags.FlipY;
            return reflect;
        }
    }
}
