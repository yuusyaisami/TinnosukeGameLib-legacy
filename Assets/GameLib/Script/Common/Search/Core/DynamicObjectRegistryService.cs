#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Search
{
    public sealed class DynamicObjectRegistryService : IDynamicSearchService, IDynamicObjectDebugSource
    {
        sealed class Entry
        {
            public IScopeNode Scope = null!;
            public IScopeIdentityService Identity = null!;
            public Transform? Transform;
        }

        readonly Dictionary<IScopeNode, Entry> _byScope = new(ReferenceEqualityComparer<IScopeNode>.Instance);
        readonly HashSet<Entry> _entries = new(ComparerInstance);
        readonly Dictionary<LifetimeScopeKind, HashSet<Entry>> _byKind = new();
        readonly Dictionary<string, HashSet<Entry>> _byId = new(StringComparer.Ordinal);
        readonly Dictionary<string, HashSet<Entry>> _byCategory = new(StringComparer.Ordinal);
        readonly HashSet<Entry> _noIdEntries = new(ComparerInstance);
        readonly HashSet<Entry> _noCategoryEntries = new(ComparerInstance);

        sealed class EntryComparer : IEqualityComparer<Entry>
        {
            public bool Equals(Entry? x, Entry? y) => ReferenceEquals(x?.Scope, y?.Scope);
            public int GetHashCode(Entry obj) => obj?.Scope != null ? RuntimeHelpers.GetHashCode(obj.Scope) : 0;
        }

        static readonly EntryComparer ComparerInstance = new();

        public int Count => _entries.Count;

        public void Register(IScopeNode scope, IScopeIdentityService identity)
        {
            if (scope == null || identity == null)
                return;

            if (_byScope.ContainsKey(scope))
                return;

            var entry = new Entry
            {
                Scope = scope,
                Identity = identity,
                Transform = ResolveTransform(scope, identity),
            };

            _byScope.Add(scope, entry);
            _entries.Add(entry);

            AddIndex(_byKind, identity.Kind, entry);
            if (!string.IsNullOrEmpty(identity.Id))
                AddIndex(_byId, identity.Id, entry);
            else
                _noIdEntries.Add(entry);

            if (!string.IsNullOrEmpty(identity.Category))
                AddIndex(_byCategory, identity.Category, entry);
            else
                _noCategoryEntries.Add(entry);
        }

        public void Unregister(IScopeNode scope)
        {
            if (scope == null)
                return;

            if (!_byScope.TryGetValue(scope, out var entry))
                return;

            _byScope.Remove(scope);
            _entries.Remove(entry);

            var id = entry.Identity;

            RemoveIndex(_byKind, id.Kind, entry);
            if (!string.IsNullOrEmpty(id.Id))
                RemoveIndex(_byId, id.Id, entry);
            else
                _noIdEntries.Remove(entry);

            if (!string.IsNullOrEmpty(id.Category))
                RemoveIndex(_byCategory, id.Category, entry);
            else
                _noCategoryEntries.Remove(entry);
        }

        public void Update(IScopeNode scope)
        {
            if (scope == null)
                return;

            if (!_byScope.TryGetValue(scope, out var entry))
                return;

            entry.Transform = ResolveTransform(scope, entry.Identity);
        }

        public void Query(in DynamicSearchQuery query, List<DynamicSearchHit> results)
        {
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            results.Clear();

            float queryRadius = math.max(0f, query.Radius);

            if (query.HasIdFilter)
            {
                if (_byId.TryGetValue(query.FilterId!, out var byId))
                    QuerySet(byId, in query, queryRadius, results);
                QuerySet(_noIdEntries, in query, queryRadius, results);
                return;
            }

            if (query.HasCategoryFilter)
            {
                if (_byCategory.TryGetValue(query.FilterCategory!, out var byCategory))
                    QuerySet(byCategory, in query, queryRadius, results);
                QuerySet(_noCategoryEntries, in query, queryRadius, results);
                return;
            }

            if (TryGetSingleKind(query.KindMask, out var kind) && _byKind.TryGetValue(kind, out var byKind))
            {
                QuerySet(byKind, in query, queryRadius, results);
                return;
            }

            QuerySet(_entries, in query, queryRadius, results);
        }

        public void Query(float2 origin, float radius, List<DynamicSearchHit> results, LifetimeScopeMask kindMask = LifetimeScopeMask.All)
        {
            var q = new DynamicSearchQuery(origin, radius, kindMask);
            Query(in q, results);
        }

        public void Query(float2 origin, float radius, float2 forward, float cosHalfAngle, List<DynamicSearchHit> results, LifetimeScopeMask kindMask = LifetimeScopeMask.All)
        {
            var q = new DynamicSearchQuery(origin, radius, forward, cosHalfAngle, kindMask);
            Query(in q, results);
        }

        public void CopyDebugEntries(List<DynamicObjectDebugEntry> destination)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));

            destination.Clear();
            destination.Capacity = math.max(destination.Capacity, _entries.Count);

            foreach (var e in _entries)
            {
                var id = e.Identity;
                var obj = (UnityEngine.Object?)e.Transform;
                destination.Add(new DynamicObjectDebugEntry(id.Kind, id.Id, id.Category, obj));
            }
        }

        static void QuerySet(
            HashSet<Entry> set,
            in DynamicSearchQuery query,
            float queryRadius,
            List<DynamicSearchHit> results)
        {
            if (set == null || set.Count == 0)
                return;

            foreach (var e in set)
            {
                if (e == null)
                    continue;

                var identity = e.Identity;
                if (!Matches(identity, in query))
                    continue;

                var t = e.Transform;
                if (t == null)
                    continue;

                var p = t.position;
                var pos = new float2(p.x, p.y);
                var d = pos - query.Origin;
                float distSq = math.dot(d, d);

                // 検索半径に「ターゲットの半径」を加味してヒット判定する。
                // 仕様: distance <= searchRadius + targetRadius
                float targetRadius = math.max(0f, identity.Radius);
                float effectiveRadius = queryRadius + targetRadius;
                float effectiveRadiusSq = effectiveRadius * effectiveRadius;
                if (distSq > effectiveRadiusSq)
                    continue;

                if (query.HasConeFilter && !PassCone(in query, d, distSq))
                    continue;

                results.Add(new DynamicSearchHit(e.Scope, identity, distSq, pos));
            }
        }

        static bool Matches(IScopeIdentityService id, in DynamicSearchQuery q)
        {
            if (id == null)
                return false;

            if (q.RequireActive && !id.IsActive)
                return false;

            if (!IsKindAllowed(id.Kind, q.KindMask))
                return false;

            if (q.HasIdFilter && !string.IsNullOrEmpty(id.Id) && !string.Equals(id.Id, q.FilterId, StringComparison.Ordinal))
                return false;

            if (q.HasCategoryFilter && !string.IsNullOrEmpty(id.Category) && !string.Equals(id.Category, q.FilterCategory, StringComparison.Ordinal))
                return false;

            return true;
        }

        static bool PassCone(in DynamicSearchQuery q, float2 delta, float distSq)
        {
            if (distSq <= 0.000001f)
                return true;

            float dot = delta.x * q.Forward.x + delta.y * q.Forward.y;
            if (dot <= 0f)
                return false;

            float cos = q.CosHalfAngle;
            float threshold = distSq * cos * cos;
            return (dot * dot) >= threshold;
        }

        static Transform? ResolveTransform(IScopeNode scope, IScopeIdentityService identity)
        {
            if (identity != null && identity.SelfTransform != null)
                return identity.SelfTransform;

            if (scope is Component c)
                return c.transform;

            return null;
        }

        static bool IsKindAllowed(LifetimeScopeKind kind, LifetimeScopeMask mask)
        {
            return LifetimeScopeMaskUtility.IsKindAllowed(kind, mask);
        }

        static bool TryGetSingleKind(LifetimeScopeMask mask, out LifetimeScopeKind kind)
        {
            return LifetimeScopeMaskUtility.TryGetSingleKind(mask, out kind);
        }

        static void AddIndex<TKey>(Dictionary<TKey, HashSet<Entry>> dict, TKey key, Entry entry)
        {
            if (!dict.TryGetValue(key, out var set))
            {
                set = new HashSet<Entry>(ComparerInstance);
                dict[key] = set;
            }
            set.Add(entry);
        }

        static void RemoveIndex<TKey>(Dictionary<TKey, HashSet<Entry>> dict, TKey key, Entry entry)
        {
            if (!dict.TryGetValue(key, out var set))
                return;

            set.Remove(entry);
            if (set.Count == 0)
            {
                dict.Remove(key);
            }
        }
    }
}

