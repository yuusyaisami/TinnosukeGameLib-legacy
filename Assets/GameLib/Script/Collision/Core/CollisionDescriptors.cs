// Game.Collision.CollisionDescriptors.cs
//
// Collider registration descriptors for CollisionSystem v2.3.
// Used to pass initial configuration when registering colliders.

using Unity.Mathematics;

namespace Game.Collision
{
    /// <summary>
    /// Descriptor for registering a dynamic (circle) collider.
    /// </summary>
    public struct DynamicColliderDesc
    {
        /// <summary>Initial world position.</summary>
        public float2 Position;

        /// <summary>Circle radius. Must not exceed profile MaxDynamicRadius.</summary>
        public float Radius;

        /// <summary>Layer bit index [0, 31].</summary>
        public int LayerId;

        /// <summary>Mask of layers this collider can hit.</summary>
        public uint HitLayerMask;

        /// <summary>Logical set for partitioning.</summary>
        public DynamicColliderSetId SetId;

        /// <summary>Optional user data for identifying the owner.</summary>
        public int UserData;

        public static DynamicColliderDesc Default => new()
        {
            Position = float2.zero,
            Radius = 0.1f,
            LayerId = 0,
            HitLayerMask = ~0u,
            SetId = DynamicColliderSetId.EnemyBullet,
            UserData = 0,
        };
    }

    /// <summary>
    /// Descriptor for registering a static (AABB) collider.
    /// </summary>
    public struct StaticColliderDesc
    {
        /// <summary>Center position of the AABB.</summary>
        public float2 Center;

        /// <summary>Half-extents of the AABB.</summary>
        public float2 HalfExtents;

        /// <summary>Layer bit index [0, 31].</summary>
        public int LayerId;

        /// <summary>Classification of static collider.</summary>
        public StaticColliderKind Kind;

        /// <summary>Optional user data for identifying the owner.</summary>
        public int UserData;

        public static StaticColliderDesc Default => new()
        {
            Center = float2.zero,
            HalfExtents = new float2(0.5f, 0.5f),
            LayerId = 0,
            Kind = StaticColliderKind.StageGeometry,
            UserData = 0,
        };
    }
}
