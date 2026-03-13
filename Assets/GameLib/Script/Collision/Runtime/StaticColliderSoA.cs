// Game.Collision.StaticColliderSoA.cs
//
// Dense Structure-of-Arrays storage for static (AABB) colliders.
// Uses swap-remove to keep the active range [0, Count) packed for Job iteration.

using System;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.Collision
{
    /// <summary>
    /// SoA storage for static colliders. Packed dense range [0, Count) is valid.
    /// Handle ID is stable (sparse index) and mapped to dense via SparseToDense/DenseToHandle.
    /// </summary>
    public struct StaticColliderSoA : IDisposable
    {
        // Core data arrays
        public NativeArray<float2> Centers;
        public NativeArray<float2> HalfExtents;
        public NativeArray<uint> LayerBits;      // 1 << LayerId
        public NativeArray<StaticColliderKind> Kinds;
        public NativeArray<int> UserData;

        // For Grid: precomputed cell keys (only for single-cell statics)
        public NativeArray<long> CellKeys;
        public NativeArray<bool> IsLargeStatic;  // true = doesn't fit single cell

        public NativeArray<int> Generations;     // Per-handle generation
        public NativeArray<int> SparseToDense;
        public NativeArray<int> DenseToHandle;

        // Free-list management
        public NativeList<int> FreeList;

        int _count;

        int _capacity;
        Allocator _allocator;

        public bool IsCreated => Centers.IsCreated;
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

            Centers = new NativeArray<float2>(capacity, allocator);
            HalfExtents = new NativeArray<float2>(capacity, allocator);
            LayerBits = new NativeArray<uint>(capacity, allocator);
            Kinds = new NativeArray<StaticColliderKind>(capacity, allocator);
            Generations = new NativeArray<int>(capacity, allocator);
            UserData = new NativeArray<int>(capacity, allocator);
            CellKeys = new NativeArray<long>(capacity, allocator);
            IsLargeStatic = new NativeArray<bool>(capacity, allocator);
            SparseToDense = new NativeArray<int>(capacity, allocator);
            DenseToHandle = new NativeArray<int>(capacity, allocator);
            FreeList = new NativeList<int>(capacity, allocator);

            for (int i = 0; i < capacity; i++)
            {
                SparseToDense[i] = -1;
            }

            // Initialize free list
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

            if (denseIndex != lastDense)
            {
                int movedHandle = DenseToHandle[lastDense];

                Centers[denseIndex] = Centers[lastDense];
                HalfExtents[denseIndex] = HalfExtents[lastDense];
                LayerBits[denseIndex] = LayerBits[lastDense];
                Kinds[denseIndex] = Kinds[lastDense];
                UserData[denseIndex] = UserData[lastDense];
                CellKeys[denseIndex] = CellKeys[lastDense];
                IsLargeStatic[denseIndex] = IsLargeStatic[lastDense];

                DenseToHandle[denseIndex] = movedHandle;
                SparseToDense[movedHandle] = denseIndex;
            }

            SparseToDense[idx] = -1;
            DenseToHandle[lastDense] = -1;
            Generations[idx]++;
            _count--;

            FreeList.Add(idx);
            return true;
        }

        /// <summary>
        /// Check if a slot is valid.
        /// </summary>
        public bool IsValid(int idx, int generation)
        {
            if (idx < 0 || idx >= _capacity)
                return false;
            return Generations[idx] == generation;
        }

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
            if (Centers.IsCreated) Centers.Dispose();
            if (HalfExtents.IsCreated) HalfExtents.Dispose();
            if (LayerBits.IsCreated) LayerBits.Dispose();
            if (Kinds.IsCreated) Kinds.Dispose();
            if (Generations.IsCreated) Generations.Dispose();
            if (UserData.IsCreated) UserData.Dispose();
            if (CellKeys.IsCreated) CellKeys.Dispose();
            if (IsLargeStatic.IsCreated) IsLargeStatic.Dispose();
            if (SparseToDense.IsCreated) SparseToDense.Dispose();
            if (DenseToHandle.IsCreated) DenseToHandle.Dispose();
            if (FreeList.IsCreated) FreeList.Dispose();

            _capacity = 0;
            _count = 0;
        }
    }
}
