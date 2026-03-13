#nullable enable
using System;
using System.Collections.Generic;
using Game;

namespace Game.Collision
{
    public interface IHitColliderChannelHub
    {
        HitColliderChannelRuntime GetOrCreate(DynamicColliderHandle self, in HitContactWatchSpec spec);
    }

    /// <summary>
    /// Runtime生成/登録/破棄を担当するHub。
    /// 1つの (self, spec) に対して 1つの HitColliderChannelRuntime を共有して返す。
    /// </summary>
    public sealed class HitColliderChannelHub :
        IHitColliderChannelHub,
        IScopeAcquireHandler,
        IScopeReleaseHandler
    {
        readonly IHitColliderChannelRouter _router;

        struct Entry
        {
            public DynamicColliderHandle Self;
            public HitContactWatchSpec Spec;
            public HitColliderChannelRuntime Runtime;
        }

        readonly List<Entry> _entries = new(8);

        public HitColliderChannelHub(IHitColliderChannelRouter router)
        {
            _router = router;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            // nothing
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                try { _entries[i].Runtime.Dispose(); } catch { }
            }
            _entries.Clear();
        }

        public HitColliderChannelRuntime GetOrCreate(DynamicColliderHandle self, in HitContactWatchSpec spec)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (!SameHandle(e.Self, self))
                    continue;
                if (!SpecEquals(e.Spec, spec))
                    continue;
                return e.Runtime;
            }

            var runtime = new HitColliderChannelRuntime(_router, self, spec);
            _entries.Add(new Entry { Self = self, Spec = spec, Runtime = runtime });
            return runtime;
        }

        static bool SameHandle(DynamicColliderHandle a, DynamicColliderHandle b)
        {
            return a.IdPlusOne == b.IdPlusOne && a.Generation == b.Generation;
        }

        static bool SpecEquals(in HitContactWatchSpec a, in HitContactWatchSpec b)
        {
            if (a.WatchFlags != b.WatchFlags) return false;
            if (a.EventMask != b.EventMask) return false;
            if (a.MatchAnyInclude != b.MatchAnyInclude) return false;
            if (a.StaleFrameThreshold != b.StaleFrameThreshold) return false;

            if (!FilterEquals(a.Filter, b.Filter)) return false;

            if (!ArrayEquals(a.IncludeStaticKinds, b.IncludeStaticKinds)) return false;
            if (!ArrayEquals(a.ExcludeStaticKinds, b.ExcludeStaticKinds)) return false;
            if (!ArrayEquals(a.IncludeDynamicSets, b.IncludeDynamicSets)) return false;
            if (!ArrayEquals(a.ExcludeDynamicSets, b.ExcludeDynamicSets)) return false;

            return true;
        }

        static bool FilterEquals(in HitFilter a, in HitFilter b)
        {
            return a.UseMobility == b.UseMobility
                && a.Mobility == b.Mobility
                && a.UseDynamicSet == b.UseDynamicSet
                && a.DynamicSetId == b.DynamicSetId
                && a.UseStaticKind == b.UseStaticKind
                && a.StaticKind == b.StaticKind;
        }

        static bool ArrayEquals<T>(T[]? a, T[]? b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;

            var comparer = EqualityComparer<T>.Default;
            for (int i = 0; i < a.Length; i++)
            {
                if (!comparer.Equals(a[i], b[i]))
                    return false;
            }
            return true;
        }
    }
}