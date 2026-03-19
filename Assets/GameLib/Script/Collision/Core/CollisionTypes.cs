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
    /// Runtime keeps this as a blittable byte wrapper so Job/Burst code can use it directly.
    /// </summary>
    [Serializable]
    public struct DynamicColliderSetId : IEquatable<DynamicColliderSetId>
    {
        [UnityEngine.SerializeField] byte value;

        public byte Value => value;

        public DynamicColliderSetId(byte value)
        {
            this.value = value;
        }

        public bool Equals(DynamicColliderSetId other) => value == other.value;
        public override bool Equals(object obj) => obj is DynamicColliderSetId other && Equals(other);
        public override int GetHashCode() => value;
        public override string ToString() => CollisionIdCatalogLocator.GetDynamicDisplayName(value);

        public static bool operator ==(DynamicColliderSetId left, DynamicColliderSetId right) => left.value == right.value;
        public static bool operator !=(DynamicColliderSetId left, DynamicColliderSetId right) => left.value != right.value;

        public static implicit operator byte(DynamicColliderSetId id) => id.value;
        public static explicit operator int(DynamicColliderSetId id) => id.value;
        public static explicit operator DynamicColliderSetId(byte value) => new(value);
        public static explicit operator DynamicColliderSetId(int value) => new((byte)value);

        public static readonly DynamicColliderSetId None = new(0);
        public static readonly DynamicColliderSetId Ball = new(10);
        public static readonly DynamicColliderSetId Nail = new(20);
        public static readonly DynamicColliderSetId ObstacleBox = new(30);
        public static readonly DynamicColliderSetId PlayerHurtbox = new(40);
        public static readonly DynamicColliderSetId EnemyHurtbox = new(50);
        public static readonly DynamicColliderSetId PlayerBullet = new(60);
        public static readonly DynamicColliderSetId EnemyBullet = new(70);
        public static readonly DynamicColliderSetId Obstacle = new(80);

        static readonly DynamicColliderSetId[] BuiltinValuesInternal =
        {
            None,
            Ball,
            Nail,
            ObstacleBox,
            PlayerHurtbox,
            EnemyHurtbox,
            PlayerBullet,
            EnemyBullet,
            Obstacle,
        };

        public static DynamicColliderSetId[] BuiltinValues => BuiltinValuesInternal;

        public static string GetBuiltinName(byte value)
        {
            return value switch
            {
                0 => nameof(None),
                10 => nameof(Ball),
                20 => nameof(Nail),
                30 => nameof(ObstacleBox),
                40 => nameof(PlayerHurtbox),
                50 => nameof(EnemyHurtbox),
                60 => nameof(PlayerBullet),
                70 => nameof(EnemyBullet),
                80 => nameof(Obstacle),
                _ => value.ToString(),
            };
        }
    }

    /// <summary>
    /// Static collider classification. Kept small for branchless dispatch.
    /// </summary>
    [Serializable]
    public struct StaticColliderKind : IEquatable<StaticColliderKind>
    {
        [UnityEngine.SerializeField] byte value;

        public byte Value => value;

        public StaticColliderKind(byte value)
        {
            this.value = value;
        }

        public bool Equals(StaticColliderKind other) => value == other.value;
        public override bool Equals(object obj) => obj is StaticColliderKind other && Equals(other);
        public override int GetHashCode() => value;
        public override string ToString() => CollisionIdCatalogLocator.GetStaticDisplayName(value);

        public static bool operator ==(StaticColliderKind left, StaticColliderKind right) => left.value == right.value;
        public static bool operator !=(StaticColliderKind left, StaticColliderKind right) => left.value != right.value;

        public static implicit operator byte(StaticColliderKind kind) => kind.value;
        public static explicit operator int(StaticColliderKind kind) => kind.value;
        public static explicit operator StaticColliderKind(byte value) => new(value);
        public static explicit operator StaticColliderKind(int value) => new((byte)value);

        public static readonly StaticColliderKind StageGeometry = new(10);
        public static readonly StaticColliderKind Boundary = new(20);
        public static readonly StaticColliderKind LivingWall = new(30);
        public static readonly StaticColliderKind NecroWall = new(40);

        static readonly StaticColliderKind[] BuiltinValuesInternal =
        {
            StageGeometry,
            Boundary,
            LivingWall,
            NecroWall,
        };

        public static StaticColliderKind[] BuiltinValues => BuiltinValuesInternal;

        public static string GetBuiltinName(byte value)
        {
            return value switch
            {
                10 => nameof(StageGeometry),
                20 => nameof(Boundary),
                30 => nameof(LivingWall),
                40 => nameof(NecroWall),
                _ => value.ToString(),
            };
        }
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
