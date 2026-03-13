// Game.Collision.BulkCollisionManager.Job.cs
//
// Implementation of IBulkCollisionManager for CollisionSystem v2.3.
// Manages SoA storage, spatial grid, and job scheduling.

#if !UNITY_WEBGL || UNITY_EDITOR
using System;
using Game.Common;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Collision
{
    /// <summary>
    /// Main implementation of the collision manager.
    /// Lifetime: Project scope (DDOL).
    /// </summary>
    public sealed class BulkCollisionManagerJob : IBulkCollisionManager, IDisposable
    {
        readonly ISyncEventBus _eventBus;
        readonly CollisionSystemProfileSO _profile;

        // Storage
        DynamicColliderSoA _dynamics;
        StaticColliderSoA _statics;
        SpatialGrid _grid;
        CollisionHitRawBuffer _rawBuffer;

        // LargeStatic query path (rebuilt each frame)
        NativeArray<int> _largeStaticDenseIndices;
        int _largeStaticCount;

        // Resolved output buffers (reused each frame)
        NativeArray<CollisionHit> _resolvedDynDyn;
        NativeArray<CollisionHit> _resolvedDynStatic;

        // Frame state
        JobHandle _inFlightHandle;
        int _frameIndex;
        uint _frameStamp;
        float _deltaTime;
        bool _jobsInFlight;
        JobHandle _gridBuildHandle;
        bool _gridBuildScheduled;
        bool _disposed;

        public int LastFrameHitCount { get; private set; }

        public BulkCollisionManagerJob(ISyncEventBus eventBus, CollisionSystemProfileSO profile)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));

            // Initialize storage
            _dynamics.Init(_profile.InitialDynamicCapacity, Allocator.Persistent);
            _statics.Init(_profile.InitialStaticCapacity, Allocator.Persistent);
            _grid.Init(_profile.InitialDynamicCapacity, _profile.InitialStaticCapacity, _profile.CellSize, Allocator.Persistent);
            _rawBuffer.Init(_profile.MaxHitsPerFrame, Allocator.Persistent);

            _largeStaticDenseIndices = new NativeArray<int>(_profile.InitialStaticCapacity, Allocator.Persistent);

            // Resolved buffers
            _resolvedDynDyn = new NativeArray<CollisionHit>(_profile.ResolvedDynDynCapacity, Allocator.Persistent);
            _resolvedDynStatic = new NativeArray<CollisionHit>(_profile.ResolvedDynStaticCapacity, Allocator.Persistent);

            _frameStamp = (uint)UnityEngine.Random.Range(1, int.MaxValue);
        }

        // ========== Registration ==========

        public DynamicColliderHandle RegisterDynamic(in DynamicColliderDesc desc)
        {
            MainThread.AssertMainThread();
            EnsureNotDisposed();
            EnsureNoJobsInFlight();

            // Validate
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
            EnsureNoJobsInFlight();

            return _dynamics.Free(handle.Id, handle.Generation);
        }

        public StaticColliderHandle RegisterStatic(in StaticColliderDesc desc)
        {
            MainThread.AssertMainThread();
            EnsureNotDisposed();
            EnsureNoJobsInFlight();

            if (desc.LayerId < 0 || desc.LayerId >= 32)
            {
                Debug.LogError($"[Collision] LayerId {desc.LayerId} out of range [0, 31]");
                return StaticColliderHandle.Invalid;
            }
            if (desc.HalfExtents.x > _profile.MaxStaticHalfExtents.x ||
                desc.HalfExtents.y > _profile.MaxStaticHalfExtents.y)
            {
                Debug.LogError($"[Collision] Static half-extents exceed max");
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

            // Determine if large static
            float maxHalf = math.max(desc.HalfExtents.x, desc.HalfExtents.y);
            _statics.IsLargeStatic[denseIndex] = maxHalf > (_profile.CellSize * 0.5f + 0.0005f);

            // Precompute cell key
            int cellX = (int)math.floor(desc.Center.x * _profile.InvCellSize);
            int cellY = (int)math.floor(desc.Center.y * _profile.InvCellSize);
            _statics.CellKeys[denseIndex] = SpatialGrid.PackCellKey(cellX, cellY);

            return StaticColliderHandle.FromId(handleId, gen);
        }

        public bool UnregisterStatic(StaticColliderHandle handle)
        {
            MainThread.AssertMainThread();
            EnsureNotDisposed();
            EnsureNoJobsInFlight();

            return _statics.Free(handle.Id, handle.Generation);
        }

        // ========== Runtime Updates ==========

        public void SetPosition(DynamicColliderHandle handle, float2 position)
        {
            MainThread.AssertMainThread();
            EnsureNoJobsInFlight();
            if (!_dynamics.TryGetDense(handle.Id, handle.Generation, out var dense)) return;
            _dynamics.Positions[dense] = position;
        }

        public void SetRadius(DynamicColliderHandle handle, float radius)
        {
            MainThread.AssertMainThread();
            EnsureNoJobsInFlight();
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
            EnsureNoJobsInFlight();
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
            EnsureNoJobsInFlight();
            if (!_dynamics.TryGetDense(handle.Id, handle.Generation, out var dense)) return;
            _dynamics.SetIds[dense] = newSetId;
        }

        public void AddHitLayer(DynamicColliderHandle handle, int targetLayerId)
        {
            MainThread.AssertMainThread();
            EnsureNoJobsInFlight();
            if (!_dynamics.TryGetDense(handle.Id, handle.Generation, out var dense)) return;
            if (targetLayerId < 0 || targetLayerId >= 32) return;
            _dynamics.HitMasks[dense] |= 1u << targetLayerId;
        }

        public void RemoveHitLayer(DynamicColliderHandle handle, int targetLayerId)
        {
            MainThread.AssertMainThread();
            EnsureNoJobsInFlight();
            if (!_dynamics.TryGetDense(handle.Id, handle.Generation, out var dense)) return;
            if (targetLayerId < 0 || targetLayerId >= 32) return;
            _dynamics.HitMasks[dense] &= ~(1u << targetLayerId);
        }

        public void SetHitMask(DynamicColliderHandle handle, uint mask)
        {
            MainThread.AssertMainThread();
            EnsureNoJobsInFlight();
            if (!_dynamics.TryGetDense(handle.Id, handle.Generation, out var dense)) return;
            _dynamics.HitMasks[dense] = mask;
        }

        // ========== Frame Pipeline ==========

        public void TickAsync(float deltaTime, JobHandle dependency = default)
        {
            MainThread.AssertMainThread();
            EnsureNotDisposed();
            EnsureNoJobsInFlight();

            _deltaTime = deltaTime;
            _frameIndex++;
            _frameStamp = unchecked(_frameStamp * 1103515245u + 12345u); // LCG for cheap unique stamp

            // Clear for new frame
            _grid.Clear();
            _rawBuffer.ClearForFrame();

            int dynCount = _dynamics.Count;
            int staticCount = _statics.Count;

            // Build LargeStatic index list (dense indices)
            _largeStaticCount = 0;
            if (_largeStaticDenseIndices.IsCreated && _largeStaticDenseIndices.Length < _statics.Capacity)
            {
                _largeStaticDenseIndices.Dispose();
                _largeStaticDenseIndices = new NativeArray<int>(_statics.Capacity, Allocator.Persistent);
            }

            for (int i = 0; i < staticCount; i++)
            {
                if (_statics.IsLargeStatic[i])
                {
                    _largeStaticDenseIndices[_largeStaticCount++] = i;
                }
            }

            // Build dynamic grid
            var buildDynGrid = new BuildDynamicGridJob
            {
                Positions = _dynamics.Positions,
                InvCellSize = _profile.InvCellSize,
                GridWriter = _grid.DynamicCells.AsParallelWriter(),
            };

            var buildDynHandle = buildDynGrid.Schedule(dynCount, 64, dependency);

            // Build static grid
            var buildStaticGrid = new BuildStaticGridJob
            {
                Centers = _statics.Centers,
                IsLargeStatic = _statics.IsLargeStatic,
                InvCellSize = _profile.InvCellSize,
                GridWriter = _grid.StaticCells.AsParallelWriter(),
            };

            var buildStaticHandle = buildStaticGrid.Schedule(staticCount, 32, dependency);

            var gridBuilt = JobHandle.CombineDependencies(buildDynHandle, buildStaticHandle);
            TrackGridBuild(gridBuilt);

            // Neighbor range based on max radius vs cell size
            int neighborRange = (int)math.ceil(_profile.MaxDynamicRadius / _profile.CellSize) + 1;

            // Query DynDyn
            var queryDynDyn = new QueryDynDynJob
            {
                Positions = _dynamics.Positions,
                Radii = _dynamics.Radii,
                LayerBits = _dynamics.LayerBits,
                HitMasks = _dynamics.HitMasks,
                SetIds = _dynamics.SetIds,
                Grid = _grid.DynamicCells,
                Hits = _rawBuffer.AsParallelWriter(),
                SameSetNoDup = true,
                CrossSetOnly = false,
                InvCellSize = _profile.InvCellSize,
                NeighborRange = neighborRange,
            };

            var dynDynHandle = queryDynDyn.Schedule(dynCount, 32, gridBuilt);

            // Query DynStatic
            var queryDynStatic = new QueryDynStaticJob
            {
                DynPositions = _dynamics.Positions,
                DynRadii = _dynamics.Radii,
                DynHitMasks = _dynamics.HitMasks,
                StaticCenters = _statics.Centers,
                StaticHalfExtents = _statics.HalfExtents,
                StaticLayerBits = _statics.LayerBits,
                StaticGrid = _grid.StaticCells,
                LargeStaticDenseIndices = _largeStaticDenseIndices,
                LargeStaticCount = _largeStaticCount,
                Hits = _rawBuffer.AsParallelWriter(),
                InvCellSize = _profile.InvCellSize,
                NeighborRange = neighborRange,
            };
            var dynStaticDependency = JobHandle.CombineDependencies(gridBuilt, dynDynHandle);
            var dynStaticHandle = queryDynStatic.Schedule(dynCount, 32, dynStaticDependency);

            // Boundary check
            var boundary = _profile.BoundaryXYXY;
            var boundaryCheck = new BoundaryCheckJob
            {
                Positions = _dynamics.Positions,
                Radii = _dynamics.Radii,
                BoundaryXYXY = new float4(boundary.x, boundary.y, boundary.z, boundary.w),
                Hits = _rawBuffer.AsParallelWriter(),
            };

            var boundaryDependency = JobHandle.CombineDependencies(gridBuilt, dynStaticHandle);
            var boundaryHandle = boundaryCheck.Schedule(dynCount, 64, boundaryDependency);

            _inFlightHandle = JobHandle.CombineDependencies(
                JobHandle.CombineDependencies(dynDynHandle, dynStaticHandle),
                boundaryHandle
            );
            _jobsInFlight = true;
        }

        public void CompleteAndDispatch()
        {
            MainThread.AssertMainThread();
            EnsureNotDisposed();

            if (!_jobsInFlight)
                return;

            CompleteInFlight();

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

            // Publish
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

            // Resolve self (always dynamic)
            int selfIdx = raw.SelfDenseIndex;
            if (selfIdx >= 0 && selfIdx < _dynamics.Count)
            {
                int selfHandle = _dynamics.DenseToHandle[selfIdx];
                hit.Self = DynamicColliderHandle.FromId(selfHandle, _dynamics.Generations[selfHandle]);
                hit.SelfLayerBit = _dynamics.LayerBits[selfIdx];
                hit.SelfSetId = _dynamics.SetIds[selfIdx];
            }

            // Resolve other
            if (raw.Kind == CollisionKind.DynamicDynamic)
            {
                int otherIdx = raw.OtherDenseIndex;
                if (otherIdx >= 0 && otherIdx < _dynamics.Count)
                {
                    int otherHandle = _dynamics.DenseToHandle[otherIdx];
                    hit.OtherDynamic = DynamicColliderHandle.FromId(otherHandle, _dynamics.Generations[otherHandle]);
                    hit.OtherLayerBit = _dynamics.LayerBits[otherIdx];
                    hit.OtherSetId = _dynamics.SetIds[otherIdx];
                }
            }
            else // DynamicStatic
            {
                int otherIdx = raw.OtherDenseIndex;
                if (otherIdx >= 0 && otherIdx < _statics.Count)
                {
                    int otherHandle = _statics.DenseToHandle[otherIdx];
                    hit.OtherStatic = StaticColliderHandle.FromId(otherHandle, _statics.Generations[otherHandle]);
                    hit.OtherLayerBit = _statics.LayerBits[otherIdx];
                    hit.OtherStaticKind = _statics.Kinds[otherIdx];
                }
                else
                {
                    // Boundary hit
                    hit.OtherStatic = StaticColliderHandle.Invalid;
                    hit.OtherLayerBit = 0;
                    hit.OtherStaticKind = StaticColliderKind.Boundary;
                }
            }

            return hit;
        }

        public void CompleteInFlight()
        {
            if (!_jobsInFlight)
                return;

            _inFlightHandle.Complete();
            _jobsInFlight = false;
            CompleteGridBuildIfPending();
        }

        // ========== Queries ==========

        public int DynamicCount => _dynamics.Count;
        public int StaticCount => _statics.Count;

        public bool IsValid(DynamicColliderHandle handle)
        {
            return _dynamics.IsValid(handle.Id, handle.Generation);
        }

        public bool IsValid(StaticColliderHandle handle)
        {
            return _statics.IsValid(handle.Id, handle.Generation);
        }

        public JobHandle InFlightHandle => _inFlightHandle;

        // ========== Helpers ==========

        void EnsureNotDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BulkCollisionManagerJob));
        }

        void EnsureNoJobsInFlight()
        {
            if (_jobsInFlight)
            {
                Debug.LogWarning("[Collision] Forcing job completion for synchronous operation");
                CompleteInFlight();
            }
            else
            {
                CompleteGridBuildIfPending();
            }
        }

        void TrackGridBuild(JobHandle handle)
        {
            _gridBuildHandle = handle;
            _gridBuildScheduled = !handle.Equals(default(JobHandle));
        }

        void CompleteGridBuildIfPending()
        {
            if (!_gridBuildScheduled)
                return;

            _gridBuildHandle.Complete();
            _gridBuildScheduled = false;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            CompleteInFlight();
            CompleteGridBuildIfPending();

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
