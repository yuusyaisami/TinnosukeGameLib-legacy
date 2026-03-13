// Game.Collision.CollisionSystemProfileSO.cs
//
// ScriptableObject configuration for CollisionSystem v2.3.
// Defines broadphase settings, collider limits, boundary, and output capacity.

using UnityEngine;

namespace Game.Collision
{
    /// <summary>
    /// Profile for CollisionSystem runtime settings.
    /// Create via: Create > Game/Collision/System Profile
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Collision/System Profile", fileName = "CollisionSystemProfile")]
    public class CollisionSystemProfileSO : ScriptableObject
    {
        [Header("Broadphase")]
        [Tooltip("Spatial grid cell size. Smaller = finer but more cells.")]
        [Min(0.1f)]
        public float CellSize = 1.0f;

        [Header("Dynamic (Circle)")]
        [Tooltip("Maximum allowed radius for dynamic colliders. Registration rejected if exceeded.")]
        [Min(0.001f)]
        public float MaxDynamicRadius = 0.5f;

        [Tooltip("Initial capacity for dynamic collider storage.")]
        [Min(64)]
        public int InitialDynamicCapacity = 1024;

        [Header("Static (AABB)")]
        [Tooltip("Maximum allowed half-extents for static colliders.")]
        public Vector2 MaxStaticHalfExtents = new(2.0f, 0.5f);

        [Tooltip("Initial capacity for static collider storage.")]
        [Min(16)]
        public int InitialStaticCapacity = 256;

        [Header("Boundary")]
        [Tooltip("Play area boundary rectangle (minX, minY, width, height).")]
        public Rect BoundaryRect = new(-10f, -10f, 20f, 20f);

        [Header("Output")]
        [Tooltip("Maximum collision hits per frame. Excess hits are discarded.")]
        [Min(256)]
        public int MaxHitsPerFrame = 4096;

        [Tooltip("Capacity for resolved DynDyn hits buffer.")]
        [Min(128)]
        public int ResolvedDynDynCapacity = 2048;

        [Tooltip("Capacity for resolved DynStatic hits buffer.")]
        [Min(128)]
        public int ResolvedDynStaticCapacity = 2048;

        /// <summary>
        /// Inverse of CellSize for multiplication instead of division.
        /// </summary>
        public float InvCellSize => 1f / CellSize;

        /// <summary>
        /// Boundary as float4 (xMin, yMin, xMax, yMax).
        /// </summary>
        public Vector4 BoundaryXYXY => new(
            BoundaryRect.xMin,
            BoundaryRect.yMin,
            BoundaryRect.xMax,
            BoundaryRect.yMax);

        void OnValidate()
        {
            CellSize = Mathf.Max(0.1f, CellSize);
            MaxDynamicRadius = Mathf.Max(0.001f, MaxDynamicRadius);
            InitialDynamicCapacity = Mathf.Max(64, InitialDynamicCapacity);
            InitialStaticCapacity = Mathf.Max(16, InitialStaticCapacity);
            MaxHitsPerFrame = Mathf.Max(256, MaxHitsPerFrame);
            ResolvedDynDynCapacity = Mathf.Max(128, ResolvedDynDynCapacity);
            ResolvedDynStaticCapacity = Mathf.Max(128, ResolvedDynStaticCapacity);
        }
    }
}
