#nullable enable
using System;

namespace Game.Collision
{
    [Flags]
    public enum HitWatchFlags : byte
    {
        None = 0,
        SelfOnly = 1 << 0,
        SelfAndOther = 1 << 1,
    }

    public readonly struct HitFrameMeta
    {
        public readonly int FrameIndex;
        public readonly uint FrameStamp;
        public readonly float DeltaTime;

        public HitFrameMeta(int frameIndex, uint frameStamp, float deltaTime)
        {
            FrameIndex = frameIndex;
            FrameStamp = frameStamp;
            DeltaTime = deltaTime;
        }
    }

    [Flags]
    public enum HitEventFlags : byte
    {
        None = 0,
        Enter = 1 << 0,
        Stay = 1 << 1,
        Exit = 1 << 2,
        All = Enter | Stay | Exit,
    }

    public enum HitEventType : byte
    {
        Enter = 0,
        Stay = 1,
        Exit = 2,
    }

    /// <summary>
    /// Lightweight filter for routed hits.
    /// Nullable semantics implemented via the boolean flags below for serialization friendliness.
    /// </summary>
    public struct HitFilter
    {
        public bool UseMobility;
        public ColliderMobilityType Mobility;

        public bool UseDynamicSet;
        public DynamicColliderSetRef DynamicSetId;

        public bool UseStaticKind;
        public StaticColliderKindRef StaticKind;

        public bool Matches(in CollisionHit hit)
        {
            if (UseMobility)
            {
                bool otherIsDyn = hit.OtherDynamic.IsValid;
                if (Mobility == ColliderMobilityType.Dynamic && !otherIsDyn) return false;
                if (Mobility == ColliderMobilityType.Static && otherIsDyn) return false;
            }

            if (UseDynamicSet)
            {
                if (!hit.OtherDynamic.IsValid) return false;
                if (hit.OtherSetId != DynamicSetId) return false;
            }

            if (UseStaticKind)
            {
                if (!hit.OtherStatic.IsValid) return false;
                if (hit.OtherStaticKind != StaticKind) return false;
            }

            return true;
        }
    }

    public readonly struct RoutedHit
    {
        public readonly CollisionHit Hit;
        public readonly HitFrameMeta Meta;

        /// <summary>
        /// true: DynDynで「Other側通知」として配送された（mirror delivery）。
        /// false: Self側通知。
        /// </summary>
        public readonly bool IsOtherSide;
        public readonly HitEventType Event;

        public RoutedHit(in CollisionHit hit, in HitFrameMeta meta, bool isOtherSide, HitEventType evt = HitEventType.Stay)
        {
            Hit = hit;
            Meta = meta;
            IsOtherSide = isOtherSide;
            Event = evt;
        }
    }

    public delegate void RoutedHitHandler(in RoutedHit routedHit);
}
