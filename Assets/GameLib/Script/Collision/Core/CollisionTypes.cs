// Game.Collision.CollisionTypes.cs
//
// Core collision data types for CollisionSystem v2.3.
// Designed for Job/Burst compatibility (blittable, no managed refs).

using System;
using Unity.Mathematics;
using Unity.Collections;

namespace Game.Collision
{
    /// <summary>
    /// Collision detection kind.
    /// </summary>
    public enum CollisionKind : byte
    {
        DynamicDynamic = 0,
        DynamicStatic = 1,
    }

    /// <summary>
    /// Reflect behavior flags for wall hits.
    /// </summary>
    [Flags]
    public enum ReflectFlags : byte
    {
        None = 0,
        FlipX = 1 << 0,
        FlipY = 1 << 1,
        FlipXY = FlipX | FlipY,
    }

    /// <summary>
    /// Dynamic collider set ID for SoA partitioning.
    /// Static colliders use <see cref="StaticColliderKind"/> instead of borrowing these IDs.
    /// </summary>
    public enum DynamicColliderSetId : byte
    {
        EnemyBullet = 0,
        PlayerBullet = 1,
        PlayerHurtbox = 2,
        EnemyHurtbox = 3,
        Obstacle = 4,

        // ゲームロジック用
        ShareBox = 10, // 単純な動く障害物。
        Item = 11,     // プレイヤーが取得するアイテム。

        // Add project-specific dynamic sets (max 255)
    }

    /// <summary>
    /// Static collider classification. Kept small for branchless dispatch.
    /// </summary>
    public enum StaticColliderKind : byte
    {
        StageGeometry = 0,
        Boundary = 1,
        // ゲームロジック用
        LivingWall = 10,
        NecroWall = 11,
    }

    /// <summary>
    /// Handle to a dynamic (circle) collider.
    /// </summary>
    public readonly struct DynamicColliderHandle : IEquatable<DynamicColliderHandle>
    {
        public readonly int IdPlusOne;
        public readonly int Generation;

        DynamicColliderHandle(int idPlusOne, int generation)
        {
            IdPlusOne = idPlusOne;
            Generation = generation;
        }

        public int Id => IdPlusOne - 1;
        public bool IsValid => IdPlusOne != 0;
        public static DynamicColliderHandle Invalid => default;

        public static DynamicColliderHandle FromId(int id, int generation)
        {
            if (id < 0) return default;
            return new DynamicColliderHandle(id + 1, generation);
        }

        public bool Equals(DynamicColliderHandle other) => IdPlusOne == other.IdPlusOne && Generation == other.Generation;
        public override bool Equals(object obj) => obj is DynamicColliderHandle h && Equals(h);
        public override int GetHashCode() => unchecked((IdPlusOne * 397) ^ Generation);
        public static bool operator ==(DynamicColliderHandle a, DynamicColliderHandle b) => a.Equals(b);
        public static bool operator !=(DynamicColliderHandle a, DynamicColliderHandle b) => !a.Equals(b);
    }

    /// <summary>
    /// Handle to a static (AABB) collider.
    /// </summary>
    public readonly struct StaticColliderHandle : IEquatable<StaticColliderHandle>
    {
        public readonly int IdPlusOne;
        public readonly int Generation;

        StaticColliderHandle(int idPlusOne, int generation)
        {
            IdPlusOne = idPlusOne;
            Generation = generation;
        }

        public int Id => IdPlusOne - 1;
        public bool IsValid => IdPlusOne != 0;
        public static StaticColliderHandle Invalid => default;

        public static StaticColliderHandle FromId(int id, int generation)
        {
            if (id < 0) return default;
            return new StaticColliderHandle(id + 1, generation);
        }

        public bool Equals(StaticColliderHandle other) => IdPlusOne == other.IdPlusOne && Generation == other.Generation;
        public override bool Equals(object obj) => obj is StaticColliderHandle h && Equals(h);
        public override int GetHashCode() => unchecked((IdPlusOne * 397) ^ Generation);
        public static bool operator ==(StaticColliderHandle a, StaticColliderHandle b) => a.Equals(b);
        public static bool operator !=(StaticColliderHandle a, StaticColliderHandle b) => !a.Equals(b);
    }

    /// <summary>
    /// Raw hit data written by Jobs (DenseIndex-based).
    /// </summary>
    public struct CollisionHitRaw
    {
        public CollisionKind Kind;
        public int SelfDenseIndex;
        public int OtherDenseIndex; // -1 = Boundary
        public float2 Point;
        public float2 Normal;
        public float Penetration;
        public ReflectFlags Reflect;
    }

    /// <summary>
    /// Public hit data with resolved handles.
    /// </summary>
    public struct CollisionHit
    {
        public CollisionKind Kind;
        public DynamicColliderHandle Self;
        public DynamicColliderHandle OtherDynamic;
        public StaticColliderHandle OtherStatic; // Invalid = Boundary
        public DynamicColliderSetId SelfSetId;
        public DynamicColliderSetId OtherSetId;
        public StaticColliderKind OtherStaticKind;
        public float2 Point;
        public float2 Normal;
        public float Penetration;
        public uint SelfLayerBit;
        public uint OtherLayerBit;
        public ReflectFlags Reflect;
    }

    /// <summary>
    /// Per-frame collision event payload.
    /// NativeArray views are valid only during CompleteAndDispatch().
    /// Do NOT cache this struct beyond the event handler scope.
    /// </summary>
    public struct CollisionHitFrame
    {
        public int FrameIndex;
        public uint FrameStamp; // Random per-frame for staleness detection
        public float DeltaTime;

        // DynDyn hits
        public NativeArray<CollisionHit> HitsDynDyn;
        public int DynDynCount;

        // DynStatic hits (including Boundary)
        public NativeArray<CollisionHit> HitsDynStatic;
        public int DynStaticCount;
    }
}
