#if UNITY_WEBGL && !UNITY_EDITOR
#nullable enable
// Game.Collision.BulkCollisionManagerWebGL.cs
//
// WebGL 向け：Jobs/Burst を使わずメインスレッドで衝突判定を行う実装。

using System;
using Game.Common;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Collision
{
    public sealed class BulkCollisionManagerWebGL : IBulkCollisionManager, IDisposable
    {
        readonly ISyncEventBus _eventBus;
        readonly CollisionSystemProfileSO _profile;

        DynamicColliderSoA _dynamics;
        StaticColliderSoA _statics;
        SpatialGrid _grid;
        CollisionHitRawBuffer _rawBuffer;

        NativeArray<int> _largeStaticDenseIndices;
        int _largeStaticCount;

        NativeArray<CollisionHit> _resolvedDynDyn;
        NativeArray<CollisionHit> _resolvedDynStatic;

        int _frameIndex;
        uint _frameStamp;
        float _deltaTime;
        bool _frameReady;
        bool _disposed;

        public int LastFrameHitCount { get; private set; }

        public BulkCollisionManagerWebGL(ISyncEventBus eventBus, CollisionSystemProfileSO profile)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));

            _dynamics.Init(_profile.InitialDynamicCapacity, Allocator.Persistent);
            _statics.Init(_profile.InitialStaticCapacity, Allocator.Persistent);
            _grid.Init(_profile.InitialDynamicCapacity, _profile.InitialStaticCapacity, _profile.CellSize, Allocator.Persistent);
            _rawBuffer.Init(_profile.MaxHitsPerFrame, Allocator.Persistent);

            _largeStaticDenseIndices = new NativeArray<int>(_profile.InitialStaticCapacity, Allocator.Persistent);

            _resolvedDynDyn = new NativeArray<CollisionHit>(_profile.MaxHitsPerFrame, Allocator.Persistent);
            _resolvedDynStatic = new NativeArray<CollisionHit>(_profile.MaxHitsPerFrame, Allocator.Persistent);

            _frameStamp = 1u;
        }

        // ========== Registration ==========

        public DynamicColliderHandle RegisterDynamic(in DynamicColliderDesc desc)
        {
            MainThread.AssertMainThread();
            EnsureNotDisposed();

            if (desc.LayerId < 0 || desc.LayerId >= 32)
            {
                Debug.LogError($"[Collision] LayerId {desc.LayerId} out of range [0, 31]");
                return DynamicColliderHandle.Invalid;
            }
            if (desc.Radius > _profile.MaxDynamicRadius)
            {
                Debug.LogError($"[Collision] Radius {desc.Radius} exceeds max {_profile.MaxDynamicRadius}");
                return DynamicColliderHandle.Invalid;
            }

            int handleId = _dynamics.Allocate(out int denseIndex, out int gen);
            if (handleId < 0)
            {
                Debug.LogError("[Collision] Dynamic collider capacity exceeded");
                return DynamicColliderHandle.Invalid;
            }

            _dynamics.Positions[denseIndex] = desc.Position;
            _dynamics.Radii[denseIndex] = desc.Radius;
            _dynamics.LayerBits[denseIndex] = 1u << desc.LayerId;
            _dynamics.HitMasks[denseIndex] = desc.HitLayerMask;
            _dynamics.SetIds[denseIndex] = desc.SetId;
            _dynamics.UserData[denseIndex] = desc.UserData;

            return DynamicColliderHandle.FromId(handleId, gen);
        }

        public bool UnregisterDynamic(DynamicColliderHandle handle)
        {
            MainThread.AssertMainThread();
            EnsureNotDisposed();
            return _dynamics.Free(handle.Id, handle.Generation);
        }

        public StaticColliderHandle RegisterStatic(in StaticColliderDesc desc)
        {
            MainThread.AssertMainThread();
            EnsureNotDisposed();

            if (desc.LayerId < 0 || desc.LayerId >= 32)
            {
                Debug.LogError($"[Collision] LayerId {desc.LayerId} out of range [0, 31]");
                return StaticColliderHandle.Invalid;
            }
            if (desc.HalfExtents.x > _profile.MaxStaticHalfExtents.x ||
                desc.HalfExtents.y > _profile.MaxStaticHalfExtents.y)
            {
                Debug.LogError("[Collision] Static half-extents exceed max");
                return StaticColliderHandle.Invalid;
            }

            int handleId = _statics.Allocate(out int denseIndex, out int gen);
            if (handleId < 0)
            {
                Debug.LogError("[Collision] Static collider capacity exceeded");
                return StaticColliderHandle.Invalid;
            }

            _statics.Centers[denseIndex] = desc.Center;
            _statics.HalfExtents[denseIndex] = desc.HalfExtents;
            _statics.LayerBits[denseIndex] = 1u << desc.LayerId;
            _statics.Kinds[denseIndex] = desc.Kind;
            _statics.UserData[denseIndex] = desc.UserData;

            float maxHalf = math.max(desc.HalfExtents.x, desc.HalfExtents.y);
            _statics.IsLargeStatic[denseIndex] = maxHalf > (_profile.CellSize * 0.5f + 0.0005f);

            int cellX = (int)math.floor(desc.Center.x * _profile.InvCellSize);
            int cellY = (int)math.floor(desc.Center.y * _profile.InvCellSize);
            _statics.CellKeys[denseIndex] = SpatialGrid.PackCellKey(cellX, cellY);

            return StaticColliderHandle.FromId(handleId, gen);
        }

        public bool UnregisterStatic(StaticColliderHandle handle)
        {
            MainThread.AssertMainThread();
            EnsureNotDisposed();
            return _statics.Free(handle.Id, handle.Generation);
        }

        // ========== Runtime Updates ==========

        public void SetPosition(DynamicColliderHandle handle, float2 position)
        {
            MainThread.AssertMainThread();
            if (!_dynamics.TryGetDense(handle.Id, handle.Generation, out var dense)) return;
            _dynamics.Positions[dense] = position;
        }

        public void SetRadius(DynamicColliderHandle handle, float radius)
        {
            MainThread.AssertMainThread();
            if (!_dynamics.TryGetDense(handle.Id, handle.Generation, out var dense)) return;
            if (radius > _profile.MaxDynamicRadius)
            {
                Debug.LogWarning($"[Collision] Clamping radius {radius} to max {_profile.MaxDynamicRadius}");
                radius = _profile.MaxDynamicRadius;
            }
            _dynamics.Radii[dense] = radius;
        }

        public void SetLayer(DynamicColliderHandle handle, int newLayerId)
        {
            MainThread.AssertMainThread();
            if (!_dynamics.TryGetDense(handle.Id, handle.Generation, out var dense)) return;
            if (newLayerId < 0 || newLayerId >= 32)
            {
                Debug.LogError($"[Collision] Invalid layer {newLayerId}");
                return;
            }
            _dynamics.LayerBits[dense] = 1u << newLayerId;
        }

        public void SetSetId(DynamicColliderHandle handle, DynamicColliderSetId newSetId)
        {
            MainThread.AssertMainThread();
            if (!_dynamics.TryGetDense(handle.Id, handle.Generation, out var dense)) return;
            _dynamics.SetIds[dense] = newSetId;
        }

        public void AddHitLayer(DynamicColliderHandle handle, int targetLayerId)
        {
            MainThread.AssertMainThread();
            if (!_dynamics.TryGetDense(handle.Id, handle.Generation, out var dense)) return;
            if (targetLayerId < 0 || targetLayerId >= 32) return;
            _dynamics.HitMasks[dense] |= 1u << targetLayerId;
        }

        public void RemoveHitLayer(DynamicColliderHandle handle, int targetLayerId)
        {
            MainThread.AssertMainThread();
            if (!_dynamics.TryGetDense(handle.Id, handle.Generation, out var dense)) return;
            if (targetLayerId < 0 || targetLayerId >= 32) return;
            _dynamics.HitMasks[dense] &= ~(1u << targetLayerId);
        }

        public void SetHitMask(DynamicColliderHandle handle, uint mask)
        {
            MainThread.AssertMainThread();
            if (!_dynamics.TryGetDense(handle.Id, handle.Generation, out var dense)) return;
            _dynamics.HitMasks[dense] = mask;
        }

        // ========== Frame Pipeline ==========

        public void TickAsync(float deltaTime, JobHandle dependency = default)
        {
            MainThread.AssertMainThread();
            EnsureNotDisposed();

            _deltaTime = deltaTime;
            _frameIndex++;
            _frameStamp = unchecked(_frameStamp * 1103515245u + 12345u);

            _grid.Clear();
            _rawBuffer.ClearForFrame();

            int dynCount = _dynamics.Count;
            int staticCount = _statics.Count;

            _largeStaticCount = 0;
            if (_largeStaticDenseIndices.IsCreated && _largeStaticDenseIndices.Length < _statics.Capacity)
            {
                _largeStaticDenseIndices.Dispose();
                _largeStaticDenseIndices = new NativeArray<int>(_statics.Capacity, Allocator.Persistent);
            }

            for (int i = 0; i < staticCount; i++)
            {
                if (_statics.IsLargeStatic[i])
                    _largeStaticDenseIndices[_largeStaticCount++] = i;
            }

            // Build grids
            for (int i = 0; i < dynCount; i++)
            {
                var pos = _dynamics.Positions[i];
                int cellX = (int)math.floor(pos.x * _profile.InvCellSize);
                int cellY = (int)math.floor(pos.y * _profile.InvCellSize);
                long key = SpatialGrid.PackCellKey(cellX, cellY);
                _grid.DynamicCells.Add(key, i);
            }

            for (int i = 0; i < staticCount; i++)
            {
                if (_statics.IsLargeStatic[i])
                    continue;

                var center = _statics.Centers[i];
                int cellX = (int)math.floor(center.x * _profile.InvCellSize);
                int cellY = (int)math.floor(center.y * _profile.InvCellSize);
                long key = SpatialGrid.PackCellKey(cellX, cellY);
                _grid.StaticCells.Add(key, i);
            }

            int neighborRange = (int)math.ceil(_profile.MaxDynamicRadius / _profile.CellSize) + 1;
            var hits = _rawBuffer.AsParallelWriter();

            // DynDyn
            for (int queryIdx = 0; queryIdx < dynCount; queryIdx++)
            {
                var queryPos = _dynamics.Positions[queryIdx];
                var queryRadius = _dynamics.Radii[queryIdx];
                var queryHitMask = _dynamics.HitMasks[queryIdx];
                var querySetId = _dynamics.SetIds[queryIdx];

                int baseCellX = (int)math.floor(queryPos.x * _profile.InvCellSize);
                int baseCellY = (int)math.floor(queryPos.y * _profile.InvCellSize);

                for (int dy = -neighborRange; dy <= neighborRange; dy++)
                    for (int dx = -neighborRange; dx <= neighborRange; dx++)
                    {
                        long cellKey = SpatialGrid.PackCellKey(baseCellX + dx, baseCellY + dy);

                        if (!_grid.DynamicCells.TryGetFirstValue(cellKey, out int targetIdx, out var it))
                            continue;

                        do
                        {
                            if (targetIdx == queryIdx)
                                continue;

                            bool sameSet = _dynamics.SetIds[targetIdx] == querySetId;
                            if (sameSet && targetIdx <= queryIdx)
                                continue;

                            var targetLayer = _dynamics.LayerBits[targetIdx];
                            if ((queryHitMask & targetLayer) == 0)
                                continue;

                            var targetPos = _dynamics.Positions[targetIdx];
                            var targetRadius = _dynamics.Radii[targetIdx];

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

                            hits.Enqueue(new CollisionHitRaw
                            {
                                Kind = CollisionKind.DynamicDynamic,
                                SelfDenseIndex = queryIdx,
                                OtherDenseIndex = targetIdx,
                                Point = contactPoint,
                                Normal = normal,
                                Penetration = penetration,
                                Reflect = ReflectFlags.None,
                            });
                        }
                        while (_grid.DynamicCells.TryGetNextValue(out targetIdx, ref it));
                    }
            }

            // DynStatic
            for (int dynIdx = 0; dynIdx < dynCount; dynIdx++)
            {
                var pos = _dynamics.Positions[dynIdx];
                var radius = _dynamics.Radii[dynIdx];
                var hitMask = _dynamics.HitMasks[dynIdx];

                int baseCellX = (int)math.floor(pos.x * _profile.InvCellSize);
                int baseCellY = (int)math.floor(pos.y * _profile.InvCellSize);

                for (int dy = -neighborRange; dy <= neighborRange; dy++)
                    for (int dx = -neighborRange; dx <= neighborRange; dx++)
                    {
                        long cellKey = SpatialGrid.PackCellKey(baseCellX + dx, baseCellY + dy);

                        if (!_grid.StaticCells.TryGetFirstValue(cellKey, out int staticIdx, out var it))
                            continue;

                        do
                        {
                            var staticLayer = _statics.LayerBits[staticIdx];
                            if ((hitMask & staticLayer) == 0)
                                continue;

                            var aabbCenter = _statics.Centers[staticIdx];
                            var aabbHalf = _statics.HalfExtents[staticIdx];

                            if (!TryComputeCircleAabbHit(pos, radius, aabbCenter, aabbHalf,
                                out float2 point, out float2 normal, out float penetration, out ReflectFlags reflect))
                                continue;

                            hits.Enqueue(new CollisionHitRaw
                            {
                                Kind = CollisionKind.DynamicStatic,
                                SelfDenseIndex = dynIdx,
                                OtherDenseIndex = staticIdx,
                                Point = point,
                                Normal = normal,
                                Penetration = penetration,
                                Reflect = reflect,
                            });
                        }
                        while (_grid.StaticCells.TryGetNextValue(out staticIdx, ref it));
                    }

                for (int i = 0; i < _largeStaticCount; i++)
                {
                    int staticIdx = _largeStaticDenseIndices[i];
                    if ((uint)staticIdx >= (uint)_statics.Centers.Length)
                        continue;

                    var staticLayer = _statics.LayerBits[staticIdx];
                    if ((hitMask & staticLayer) == 0)
                        continue;

                    var aabbCenter = _statics.Centers[staticIdx];
                    var aabbHalf = _statics.HalfExtents[staticIdx];

                    if (!TryComputeCircleAabbHit(pos, radius, aabbCenter, aabbHalf,
                        out float2 point, out float2 normal, out float penetration, out ReflectFlags reflect))
                        continue;

                    hits.Enqueue(new CollisionHitRaw
                    {
                        Kind = CollisionKind.DynamicStatic,
                        SelfDenseIndex = dynIdx,
                        OtherDenseIndex = staticIdx,
                        Point = point,
                        Normal = normal,
                        Penetration = penetration,
                        Reflect = reflect,
                    });
                }
            }

            // Boundary
            var boundary = _profile.BoundaryXYXY;
            var b = new float4(boundary.x, boundary.y, boundary.z, boundary.w);
            for (int idx = 0; idx < dynCount; idx++)
            {
                var pos = _dynamics.Positions[idx];
                var radius = _dynamics.Radii[idx];

                if (pos.x - radius < b.x)
                    TryWriteBoundaryHit(ref hits, idx, new float2(b.x, pos.y), new float2(1, 0));
                if (pos.x + radius > b.z)
                    TryWriteBoundaryHit(ref hits, idx, new float2(b.z, pos.y), new float2(-1, 0));
                if (pos.y - radius < b.y)
                    TryWriteBoundaryHit(ref hits, idx, new float2(pos.x, b.y), new float2(0, 1));
                if (pos.y + radius > b.w)
                    TryWriteBoundaryHit(ref hits, idx, new float2(pos.x, b.w), new float2(0, -1));
            }

            _frameReady = true;
        }

        static void TryWriteBoundaryHit(ref Unity.Collections.NativeQueue<CollisionHitRaw>.ParallelWriter hits, int selfIdx, float2 point, float2 normal)
        {
            hits.Enqueue(new CollisionHitRaw
            {
                Kind = CollisionKind.DynamicStatic,
                SelfDenseIndex = selfIdx,
                OtherDenseIndex = -1,
                Point = point,
                Normal = normal,
                Penetration = 0f,
                Reflect = CollisionJobMath.ReflectFromNormal(normal),
            });
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
                point = closest;
            }
            else
            {
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

        public void CompleteAndDispatch()
        {
            MainThread.AssertMainThread();
            EnsureNotDisposed();

            if (!_frameReady)
                return;

            _frameReady = false;

            // Resolve raw hits to CollisionHit with handles by draining the queue.
            int dynDynCount = 0;
            int dynStaticCount = 0;
            int overflow = 0;

            while (_rawBuffer.TryDequeue(out var raw))
            {
                if (raw.Kind == CollisionKind.DynamicDynamic)
                {
                    if (dynDynCount >= _resolvedDynDyn.Length)
                    {
                        overflow++;
                        continue;
                    }

                    _resolvedDynDyn[dynDynCount++] = ResolveHit(in raw);
                }
                else
                {
                    if (dynStaticCount >= _resolvedDynStatic.Length)
                    {
                        overflow++;
                        continue;
                    }

                    _resolvedDynStatic[dynStaticCount++] = ResolveHit(in raw);
                }
            }

            if (overflow > 0)
                _rawBuffer.AddOverflow(overflow);

            var frame = new CollisionHitFrame
            {
                FrameIndex = _frameIndex,
                FrameStamp = _frameStamp,
                DeltaTime = _deltaTime,
                HitsDynDyn = _resolvedDynDyn,
                DynDynCount = dynDynCount,
                HitsDynStatic = _resolvedDynStatic,
                DynStaticCount = dynStaticCount,
            };

            LastFrameHitCount = dynDynCount + dynStaticCount;
            _eventBus.Publish(CollisionEventIds.Frame, in frame);
        }

        CollisionHit ResolveHit(in CollisionHitRaw raw)
        {
            var hit = new CollisionHit
            {
                Kind = raw.Kind,
                Point = raw.Point,
                Normal = raw.Normal,
                Penetration = raw.Penetration,
                Reflect = raw.Reflect,
            };

            int selfIdx = raw.SelfDenseIndex;
            if (selfIdx >= 0 && selfIdx < _dynamics.Count)
            {
                int selfHandle = _dynamics.DenseToHandle[selfIdx];
                hit.Self = DynamicColliderHandle.FromId(selfHandle, _dynamics.Generations[selfHandle]);
                hit.SelfLayerBit = _dynamics.LayerBits[selfIdx];
                hit.SelfSetId = _dynamics.SetIds[selfIdx];
            }

            if (raw.Kind == CollisionKind.DynamicDynamic)
            {
                int otherIdx = raw.OtherDenseIndex;
                if (otherIdx >= 0 && otherIdx < _dynamics.Count)
                {
                    int otherHandle = _dynamics.DenseToHandle[otherIdx];
                    hit.OtherDynamic = DynamicColliderHandle.FromId(otherHandle, _dynamics.Generations[otherHandle]);
                    hit.OtherLayerBit = _dynamics.LayerBits[otherIdx];
                    hit.OtherSetId = _dynamics.SetIds[otherIdx];
                    hit.OtherStaticKind = default;
                }
            }
            else
            {
                int otherIdx = raw.OtherDenseIndex;
                if (otherIdx >= 0 && otherIdx < _statics.Count)
                {
                    int otherHandle = _statics.DenseToHandle[otherIdx];
                    hit.OtherStatic = StaticColliderHandle.FromId(otherHandle, _statics.Generations[otherHandle]);
                    hit.OtherLayerBit = _statics.LayerBits[otherIdx];
                    hit.OtherSetId = default;
                    hit.OtherStaticKind = _statics.Kinds[otherIdx];
                }
                else
                {
                    hit.OtherStatic = StaticColliderHandle.Invalid;
                    hit.OtherLayerBit = 0;
                    hit.OtherSetId = default;
                    hit.OtherStaticKind = StaticColliderKind.Boundary;
                }
            }

            return hit;
        }

        public void CompleteInFlight()
        {
            // no-op (no Jobs)
        }

        // ========== Queries ==========

        public int DynamicCount => _dynamics.Count;
        public int StaticCount => _statics.Count;

        public bool IsValid(DynamicColliderHandle handle) => _dynamics.IsValid(handle.Id, handle.Generation);
        public bool IsValid(StaticColliderHandle handle) => _statics.IsValid(handle.Id, handle.Generation);

        public JobHandle InFlightHandle => default;

        void EnsureNotDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BulkCollisionManagerWebGL));
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _dynamics.Dispose();
            _statics.Dispose();
            _grid.Dispose();
            _rawBuffer.Dispose();

            if (_largeStaticDenseIndices.IsCreated) _largeStaticDenseIndices.Dispose();
            if (_resolvedDynDyn.IsCreated) _resolvedDynDyn.Dispose();
            if (_resolvedDynStatic.IsCreated) _resolvedDynStatic.Dispose();

            _disposed = true;
        }
    }
}

#endif
