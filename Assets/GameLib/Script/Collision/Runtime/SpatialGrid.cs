// Game.Collision.SpatialGrid.cs
//
// Spatial hash grid for broadphase collision detection.
// Uses NativeParallelMultiHashMap for concurrent cell lookups.

using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.Collision
{
    /// <summary>
    /// Spatial grid for broadphase collision detection.
    /// Stores dense indices of colliders in cells.
    /// </summary>
    public struct SpatialGrid : IDisposable
    {
        public NativeParallelMultiHashMap<long, int> DynamicCells;
        public NativeParallelMultiHashMap<long, int> StaticCells;

        float _invCellSize;
        Allocator _allocator;

        public bool IsCreated => DynamicCells.IsCreated;

        public void Init(int dynamicCapacity, int staticCapacity, float cellSize, Allocator allocator)
        {
            if (cellSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(cellSize));

            Dispose();

            _invCellSize = 1f / cellSize;
            _allocator = allocator;

            // Estimate bucket count based on capacity
            int dynamicBuckets = math.max(64, dynamicCapacity / 4);
            int staticBuckets = math.max(16, staticCapacity / 4);

            DynamicCells = new NativeParallelMultiHashMap<long, int>(dynamicCapacity, allocator);
            StaticCells = new NativeParallelMultiHashMap<long, int>(staticCapacity, allocator);
        }

        /// <summary>
        /// Clear all cells for new frame.
        /// </summary>
        public void Clear()
        {
            if (DynamicCells.IsCreated) DynamicCells.Clear();
            if (StaticCells.IsCreated) StaticCells.Clear();
        }

        /// <summary>
        /// Pack cell coordinates into a single long key.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long PackCellKey(int cellX, int cellY)
        {
            return ((long)(uint)cellX << 32) | (uint)cellY;
        }

        /// <summary>
        /// Unpack cell key into coordinates.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void UnpackCellKey(long key, out int cellX, out int cellY)
        {
            cellX = (int)(key >> 32);
            cellY = (int)(key & 0xFFFFFFFF);
        }

        /// <summary>
        /// Get cell coordinates for a position.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int2 GetCellCoords(float2 position)
        {
            return new int2(
                (int)math.floor(position.x * _invCellSize),
                (int)math.floor(position.y * _invCellSize)
            );
        }

        /// <summary>
        /// Get cell key for a position.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long GetCellKey(float2 position)
        {
            var coords = GetCellCoords(position);
            return PackCellKey(coords.x, coords.y);
        }

        public void Dispose()
        {
            if (DynamicCells.IsCreated) DynamicCells.Dispose();
            if (StaticCells.IsCreated) StaticCells.Dispose();
        }
    }

    /// <summary>
    /// Read-only accessor for spatial grid during Jobs.
    /// </summary>
    public struct SpatialGridReadOnly
    {
        [ReadOnly] public NativeParallelMultiHashMap<long, int> DynamicCells;
        [ReadOnly] public NativeParallelMultiHashMap<long, int> StaticCells;
        public float InvCellSize;
        public int NeighborRange;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int2 GetCellCoords(float2 position)
        {
            return new int2(
                (int)math.floor(position.x * InvCellSize),
                (int)math.floor(position.y * InvCellSize)
            );
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long GetCellKey(int cellX, int cellY)
        {
            return SpatialGrid.PackCellKey(cellX, cellY);
        }
    }
}
