#nullable enable
using System;
using System.Collections.Generic;

namespace Game.Collision
{
    /// <summary>
    /// Flexible matching rules for RoutedHit filtering.
    /// - Excludes are applied first.
    /// - Includes are optional; when any include is provided, MatchAnyInclude controls OR/AND behavior.
    /// </summary>
    public readonly struct HitColliderRouteQuery
    {
        public readonly HitEventFlags EventMask;

        public readonly StaticColliderKind[]? IncludeStaticKinds;
        public readonly StaticColliderKind[]? ExcludeStaticKinds;

        public readonly DynamicColliderSetId[]? IncludeDynamicSets;
        public readonly DynamicColliderSetId[]? ExcludeDynamicSets;

        /// <summary>
        /// true: any include match is enough (OR).
        /// false: all provided include groups must match (AND).
        /// </summary>
        public readonly bool MatchAnyInclude;

        public HitColliderRouteQuery(
            HitEventFlags eventMask,
            StaticColliderKind[]? includeStaticKinds = null,
            DynamicColliderSetId[]? includeDynamicSets = null,
            StaticColliderKind[]? excludeStaticKinds = null,
            DynamicColliderSetId[]? excludeDynamicSets = null,
            bool matchAnyInclude = true)
        {
            EventMask = eventMask;
            IncludeStaticKinds = includeStaticKinds;
            IncludeDynamicSets = includeDynamicSets;
            ExcludeStaticKinds = excludeStaticKinds;
            ExcludeDynamicSets = excludeDynamicSets;
            MatchAnyInclude = matchAnyInclude;
        }

        public bool Matches(in RoutedHit routedHit)
        {
            if ((EventMask & (HitEventFlags)(1 << (int)routedHit.Event)) == 0)
                return false;

            var hit = routedHit.Hit;

            if (ExcludeStaticKinds != null && ExcludeStaticKinds.Length > 0)
            {
                var otherKind = hit.OtherStaticKind;
                if (ArrayContains(ExcludeStaticKinds, otherKind))
                    return false;
            }

            if (ExcludeDynamicSets != null && ExcludeDynamicSets.Length > 0)
            {
                if (hit.OtherDynamic.IsValid && ArrayContains(ExcludeDynamicSets, hit.OtherSetId))
                    return false;
            }

            bool hasStaticInclude = IncludeStaticKinds != null && IncludeStaticKinds.Length > 0;
            bool hasSetInclude = IncludeDynamicSets != null && IncludeDynamicSets.Length > 0;
            if (!hasStaticInclude && !hasSetInclude)
                return true;

            bool staticMatched = false;
            if (hasStaticInclude)
            {
                staticMatched = ArrayContains(IncludeStaticKinds!, hit.OtherStaticKind);
            }

            bool setMatched = false;
            if (hasSetInclude)
            {
                setMatched = hit.OtherDynamic.IsValid && ArrayContains(IncludeDynamicSets!, hit.OtherSetId);
            }

            if (MatchAnyInclude)
                return staticMatched || setMatched;

            if (hasStaticInclude && !staticMatched)
                return false;
            if (hasSetInclude && !setMatched)
                return false;
            return true;
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
}
