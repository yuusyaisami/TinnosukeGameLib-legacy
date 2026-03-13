// Game.Collision.DynamicColliderSoA.cs
//
// Dense Structure-of-Arrays storage for dynamic (circle) colliders.
// Uses swap-remove to keep the active range [0, Count) packed for Job iteration.

using System;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.Collision
{
    /// <summary>
    /// SoA storage for dynamic colliders. Packed dense range [0, Count) is valid.
    /// Handle ID is stable (sparse index) and mapped to dense via SparseToDense/DenseToHandle.
    /// </summary>
    public struct DynamicColliderSoA : IDisposable
    {
        // Core data arrays
        public NativeArray<float2> Positions;
        public NativeArray<float> Radii;
        public NativeArray<uint> LayerBits;      // 1 << LayerId
        public NativeArray<uint> HitMasks;       // Which layers this can hit
        public NativeArray<DynamicColliderSetId> SetIds;
        public NativeArray<int> UserData;

        // Handle indirection (sparse -> dense, dense -> sparse)
        public NativeArray<int> Generations;     // Per-handle generation
        public NativeArray<int> SparseToDense;   // HandleId -> Dense index (or -1 if free)
        public NativeArray<int> DenseToHandle;   // Dense index -> HandleId

        // Free-list of handle IDs
        public NativeList<int> FreeList;

        int _count;

        int _capacity;
        Allocator _allocator;

        public bool IsCreated => Positions.IsCreated;
        public int Capacity => _capacity;
        public int Count => _count;

        public void Init(int capacity, Allocator allocator)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            Dispose();

            _capacity = capacity;
            _allocator = allocator;
            _count = 0;

            Positions = new NativeArray<float2>(capacity, allocator);
            Radii = new NativeArray<float>(capacity, allocator);
            LayerBits = new NativeArray<uint>(capacity, allocator);
            HitMasks = new NativeArray<uint>(capacity, allocator);
            SetIds = new NativeArray<DynamicColliderSetId>(capacity, allocator);
            Generations = new NativeArray<int>(capacity, allocator);
            UserData = new NativeArray<int>(capacity, allocator);
            SparseToDense = new NativeArray<int>(capacity, allocator);
            DenseToHandle = new NativeArray<int>(capacity, allocator);
            FreeList = new NativeList<int>(capacity, allocator);

            for (int i = 0; i < capacity; i++)
            {
                SparseToDense[i] = -1;
            }

            // Initialize free list (all slots available)
            for (int i = capacity - 1; i >= 0; i--)
            {
                FreeList.Add(i);
            }
        }

        /// <summary>
        /// Allocate a slot. Returns handle + dense index or -1 if full.
        /// </summary>
        public int Allocate(out int denseIndex, out int generation)
        {
            if (FreeList.Length == 0)
            {
                denseIndex = -1;
                generation = 0;
                return -1;
            }

            int idx = FreeList[FreeList.Length - 1];
            FreeList.RemoveAt(FreeList.Length - 1);

            generation = Generations[idx] + 1;
            Generations[idx] = generation;

            denseIndex = _count;
            SparseToDense[idx] = denseIndex;
            DenseToHandle[denseIndex] = idx;
            _count++;

            return idx;
        }

        /// <summary>
        /// Free a slot. Returns true if generation matched and slot was freed.
        /// </summary>
        public bool Free(int idx, int generation)
        {
            if (idx < 0 || idx >= _capacity)
                return false;
            if (Generations[idx] != generation)
                return false;

            int denseIndex = SparseToDense[idx];
            if (denseIndex < 0 || denseIndex >= _count)
                return false;

            int lastDense = _count - 1;

            // Swap-remove dense slot if not last
            if (denseIndex != lastDense)
            {
                int movedHandle = DenseToHandle[lastDense];

                Positions[denseIndex] = Positions[lastDense];
                Radii[denseIndex] = Radii[lastDense];
                LayerBits[denseIndex] = LayerBits[lastDense];
                HitMasks[denseIndex] = HitMasks[lastDense];
                SetIds[denseIndex] = SetIds[lastDense];
                UserData[denseIndex] = UserData[lastDense];

                DenseToHandle[denseIndex] = movedHandle;
                SparseToDense[movedHandle] = denseIndex;
            }

            // Clear mappings for removed handle
            SparseToDense[idx] = -1;
            DenseToHandle[lastDense] = -1;
            Generations[idx]++;
            _count--;

            FreeList.Add(idx);
            return true;
        }

        /// <summary>
        /// Check if a slot is valid (generation matches).
        /// </summary>
        public bool IsValid(int idx, int generation)
        {
            if (idx < 0 || idx >= _capacity)
                return false;
            return Generations[idx] == generation;
        }

        /// <summary>
        /// Try to get dense index for a handle.
        /// </summary>
        public bool TryGetDense(int handleId, int generation, out int denseIndex)
        {
            denseIndex = -1;
            if (!IsValid(handleId, generation))
                return false;
            denseIndex = SparseToDense[handleId];
            return denseIndex >= 0 && denseIndex < _count;
        }

        public void Dispose()
        {
            if (Positions.IsCreated) Positions.Dispose();
            if (Radii.IsCreated) Radii.Dispose();
            if (LayerBits.IsCreated) LayerBits.Dispose();
            if (HitMasks.IsCreated) HitMasks.Dispose();
            if (SetIds.IsCreated) SetIds.Dispose();
            if (Generations.IsCreated) Generations.Dispose();
            if (UserData.IsCreated) UserData.Dispose();
            if (SparseToDense.IsCreated) SparseToDense.Dispose();
            if (DenseToHandle.IsCreated) DenseToHandle.Dispose();
            if (FreeList.IsCreated) FreeList.Dispose();

            _capacity = 0;
            _count = 0;
        }
    }
}
