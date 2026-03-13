#nullable enable
using System;
using System.Collections.Generic;

namespace Game.Collision
{
    public readonly struct HitContactState
    {
        public readonly int StaticCount;
        public readonly int DynamicCount;

        public int TotalCount => StaticCount + DynamicCount;
        public bool HasAny => TotalCount > 0;

        public HitContactState(int staticCount, int dynamicCount)
        {
            StaticCount = staticCount;
            DynamicCount = dynamicCount;
        }
    }

    public readonly struct HitContactWatchSpec
    {
        public readonly HitWatchFlags WatchFlags;
        public readonly HitEventFlags EventMask;

        public readonly StaticColliderKind[]? IncludeStaticKinds;
        public readonly StaticColliderKind[]? ExcludeStaticKinds;

        public readonly DynamicColliderSetId[]? IncludeDynamicSets;
        public readonly DynamicColliderSetId[]? ExcludeDynamicSets;

        public readonly bool MatchAnyInclude;

        public readonly HitFilter Filter;

        /// <summary>
        /// If >= 1, removes contacts that have not been observed (via Enter/Stay) for this many frames.
        /// This helps recover from missed Exit events.
        /// </summary>
        public readonly int StaleFrameThreshold;

        public HitContactWatchSpec(
            HitWatchFlags watchFlags,
            HitEventFlags eventMask,
            StaticColliderKind[]? includeStaticKinds = null,
            DynamicColliderSetId[]? includeDynamicSets = null,
            StaticColliderKind[]? excludeStaticKinds = null,
            DynamicColliderSetId[]? excludeDynamicSets = null,
            bool matchAnyInclude = true,
            in HitFilter filter = default,
            int staleFrameThreshold = 2)
        {
            WatchFlags = watchFlags;
            EventMask = eventMask;
            IncludeStaticKinds = includeStaticKinds;
            IncludeDynamicSets = includeDynamicSets;
            ExcludeStaticKinds = excludeStaticKinds;
            ExcludeDynamicSets = excludeDynamicSets;
            MatchAnyInclude = matchAnyInclude;
            Filter = filter;
            StaleFrameThreshold = staleFrameThreshold;
        }

        public static HitContactWatchSpec Create(
            HitWatchFlags watchFlags,
            HitEventFlags eventMask,
            StaticColliderKind[]? includeStaticKinds = null,
            DynamicColliderSetId[]? includeDynamicSets = null,
            StaticColliderKind[]? excludeStaticKinds = null,
            DynamicColliderSetId[]? excludeDynamicSets = null,
            bool matchAnyInclude = true,
            in HitFilter filter = default,
            int staleFrameThreshold = 2)
        {
            return new HitContactWatchSpec(
                watchFlags: watchFlags,
                eventMask: eventMask,
                includeStaticKinds: includeStaticKinds,
                includeDynamicSets: includeDynamicSets,
                excludeStaticKinds: excludeStaticKinds,
                excludeDynamicSets: excludeDynamicSets,
                matchAnyInclude: matchAnyInclude,
                filter: filter,
                staleFrameThreshold: staleFrameThreshold);
        }

        public bool Matches(in RoutedHit rh)
        {
            if ((EventMask & (HitEventFlags)(1 << (int)rh.Event)) == 0)
                return false;

            var hit = rh.Hit;

            if (ExcludeStaticKinds != null && ExcludeStaticKinds.Length > 0)
            {
                if (ArrayContains(ExcludeStaticKinds, hit.OtherStaticKind))
                    return false;
            }

            if (ExcludeDynamicSets != null && ExcludeDynamicSets.Length > 0)
            {
                if (hit.OtherDynamic.IsValid && ArrayContains(ExcludeDynamicSets, hit.OtherSetId))
                    return false;
            }

            bool hasStaticInclude = IncludeStaticKinds != null && IncludeStaticKinds.Length > 0;
            bool hasSetInclude = IncludeDynamicSets != null && IncludeDynamicSets.Length > 0;
            if (hasStaticInclude || hasSetInclude)
            {
                bool staticMatched = false;
                if (hasStaticInclude)
                    staticMatched = ArrayContains(IncludeStaticKinds!, hit.OtherStaticKind);

                bool setMatched = false;
                if (hasSetInclude)
                    setMatched = hit.OtherDynamic.IsValid && ArrayContains(IncludeDynamicSets!, hit.OtherSetId);

                if (MatchAnyInclude)
                {
                    if (!staticMatched && !setMatched)
                        return false;
                }
                else
                {
                    if (hasStaticInclude && !staticMatched)
                        return false;
                    if (hasSetInclude && !setMatched)
                        return false;
                }
            }

            return Filter.Matches(hit);
        }

        static bool ArrayContains<T>(T[] array, T value)
        {
            var comparer = EqualityComparer<T>.Default;
            for (int i = 0; i < array.Length; i++)
            {
                if (comparer.Equals(array[i], value))
                    return true;
            }
            return false;
        }
    }

    public readonly struct HitContact
    {
        public readonly StaticColliderHandle Static;
        public readonly DynamicColliderHandle Dynamic;

        public bool IsStatic => Static.IsValid;
        public bool IsDynamic => Dynamic.IsValid;

        public int Hash => IsStatic ? Static.GetHashCode() : Dynamic.GetHashCode();

        HitContact(StaticColliderHandle s, DynamicColliderHandle d)
        {
            Static = s;
            Dynamic = d;
        }

        public static bool TryCreate(in RoutedHit rh, out HitContact contact)
        {
            var hit = rh.Hit;
            if (hit.OtherStatic.IsValid)
            {
                contact = new HitContact(hit.OtherStatic, default);
                return true;
            }
            if (hit.OtherDynamic.IsValid)
            {
                contact = new HitContact(default, hit.OtherDynamic);
                return true;
            }

            contact = default;
            return false;
        }
    }

    public readonly struct HitContactEvent
    {
        public readonly HitContact Contact;
        public readonly RoutedHit RoutedHit;

        public HitEventType EventType => RoutedHit.Event;

        public HitContactEvent(in HitContact contact, in RoutedHit routedHit)
        {
            Contact = contact;
            RoutedHit = routedHit;
        }
    }

    public delegate void HitContactStateChangedHandler(in HitContactState state);
    public delegate void HitContactEventHandler(in HitContactEvent evt);

    public interface IHitContactSubscription : IDisposable
    {
        HitContactState State { get; }
    }
}
