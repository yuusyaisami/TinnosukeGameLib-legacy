// Assets/Game/Script/Core/Identity/BaseLifetimeScopeRegistry.cs
using System;
using System.Collections.Generic;
using Game.Commands;

namespace Game
{
    public interface IBaseLifetimeScopeRegistry
    {
        void RegisterScope(IScopeNode scope, IScopeIdentityService identity);
        void UnregisterScope(IScopeNode scope);

        IScopeNode Resolve(CommandTargetIdentityFilter filter, IScopeNode origin = null);
        IReadOnlyList<IScopeNode> ResolveAll(CommandTargetIdentityFilter filter, IScopeNode origin = null);
    }

    /// <summary>
    /// LifetimeScope の索引。大量スコープでも線形走査を避けるため kind/id/category でインデックス化。
    /// Unregister で確実に掃除し、null を残さないようにする。
    /// </summary>
    public sealed class BaseLifetimeScopeRegistry : IBaseLifetimeScopeRegistry
    {
        sealed class Entry
        {
            public IScopeNode Scope;
            public IScopeIdentityService Identity;
        }

        sealed class EntryComparer : IEqualityComparer<Entry>
        {
            public bool Equals(Entry x, Entry y) => ReferenceEquals(x?.Scope, y?.Scope);
            public int GetHashCode(Entry obj)
                => obj?.Scope != null ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.Scope) : 0;
        }

        readonly EntryComparer _entryComparer = new EntryComparer();

        readonly HashSet<Entry> _entries;
        readonly Dictionary<LifetimeScopeKind, HashSet<Entry>> _byKind;
        readonly Dictionary<string, HashSet<Entry>> _byId;
        readonly Dictionary<string, HashSet<Entry>> _byCategory;
        readonly HashSet<Entry> _noIdEntries;
        readonly HashSet<Entry> _noCategoryEntries;

        public BaseLifetimeScopeRegistry()
        {
            _entries = new HashSet<Entry>(_entryComparer);
            _byKind = new Dictionary<LifetimeScopeKind, HashSet<Entry>>();
            _byId = new Dictionary<string, HashSet<Entry>>(StringComparer.Ordinal);
            _byCategory = new Dictionary<string, HashSet<Entry>>(StringComparer.Ordinal);
            _noIdEntries = new HashSet<Entry>(_entryComparer);
            _noCategoryEntries = new HashSet<Entry>(_entryComparer);
        }

        public void RegisterScope(IScopeNode scope, IScopeIdentityService identity)
        {
            if (scope == null || identity == null)
                return;

            var entry = new Entry { Scope = scope, Identity = identity };

            // avoid duplicate registration for the same scope
            if (!_entries.Add(entry))
                return;

            // Duplicates are allowed by design: ResolveAll is expected to return multiple hits.

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

        public void UnregisterScope(IScopeNode scope)
        {
            if (scope == null)
                return;

            var dummy = new Entry { Scope = scope };
            if (!_entries.TryGetValue(dummy, out var entry))
                return;

            _entries.Remove(entry);
            RemoveIndex(_byKind, entry.Identity.Kind, entry);
            if (!string.IsNullOrEmpty(entry.Identity.Id))
                RemoveIndex(_byId, entry.Identity.Id, entry);
            else
                _noIdEntries.Remove(entry);

            if (!string.IsNullOrEmpty(entry.Identity.Category))
                RemoveIndex(_byCategory, entry.Identity.Category, entry);
            else
                _noCategoryEntries.Remove(entry);
        }

        public IScopeNode Resolve(CommandTargetIdentityFilter filter, IScopeNode origin = null)
        {
            // Deterministic ancestor resolution: nearest match in the parent chain.
            // This avoids HashSet iteration order affecting which scope is picked.
            if (origin != null && filter.searchScope == CommandTargetSearchScope.AncestorsOnly)
            {
                var current = origin.Parent;
                while (current != null)
                {
                    if (TryGetIdentity(current, out var id) && Matches(id, filter))
                        return current;

                    current = current.Parent;
                }

                return null;
            }

            foreach (var e in GetCandidates(filter))
            {
                if (!Matches(e.Identity, filter))
                    continue;

                if (!IsInSearchScope(origin, e.Scope, filter.searchScope))
                    continue;

                return e.Scope; // 最初に一致
            }
            return null;
        }

        bool TryGetIdentity(IScopeNode scope, out IScopeIdentityService identity)
        {
            identity = null;
            if (scope == null)
                return false;

            var dummy = new Entry { Scope = scope };
            if (!_entries.TryGetValue(dummy, out var entry) || entry?.Identity == null)
                return false;

            identity = entry.Identity;
            return true;
        }

        public IReadOnlyList<IScopeNode> ResolveAll(CommandTargetIdentityFilter filter, IScopeNode origin = null)
        {
            var list = new List<IScopeNode>();
            foreach (var e in GetCandidates(filter))
            {
                if (!Matches(e.Identity, filter))
                    continue;

                if (!IsInSearchScope(origin, e.Scope, filter.searchScope))
                    continue;

                list.Add(e.Scope);
            }
            return list;
        }

        IEnumerable<Entry> GetCandidates(CommandTargetIdentityFilter f)
        {
            // Narrow down starting set by the most selective filter
            if (!string.IsNullOrEmpty(f.id))
            {
                if (_byId.TryGetValue(f.id, out var byId) && byId != null)
                    return byId;
                return Array.Empty<Entry>();
            }

            if (!string.IsNullOrEmpty(f.category))
            {
                if (_byCategory.TryGetValue(f.category, out var byCat) && byCat != null)
                    return byCat;
                return Array.Empty<Entry>();
            }

            if (f.kind != LifetimeScopeKind.None && _byKind.TryGetValue(f.kind, out var byKind))
                return byKind;

            return _entries;
        }

        static bool Matches(IScopeIdentityService id, CommandTargetIdentityFilter f)
        {
            if (id == null)
                return false;

            if (f.requireActive && !id.IsActive) return false;
            if (f.kind != LifetimeScopeKind.None && f.kind != id.Kind) return false;
            if (!string.IsNullOrEmpty(f.id))
            {
                if (string.IsNullOrEmpty(id.Id)) return false;
                if (!string.Equals(f.id, id.Id, StringComparison.Ordinal)) return false;
            }
            if (!string.IsNullOrEmpty(f.category))
            {
                if (string.IsNullOrEmpty(id.Category)) return false;
                if (!string.Equals(f.category, id.Category, StringComparison.Ordinal)) return false;
            }
            return true;
        }

        static bool IsInSearchScope(IScopeNode origin, IScopeNode candidate, CommandTargetSearchScope scope)
        {
            if (origin == null || scope == CommandTargetSearchScope.All)
                return true;

            if (candidate == null)
                return false;

            if (ReferenceEquals(origin, candidate))
                return false; // 親/子のみ指定時は自分自身は除外

            switch (scope)
            {
                case CommandTargetSearchScope.AncestorsOnly:
                    {
                        return IsAncestorOf(candidate, origin);
                    }

                case CommandTargetSearchScope.DescendantsOnly:
                    {
                        return IsAncestorOf(origin, candidate);
                    }

                default:
                    return true;
            }
        }

        static bool IsAncestorOf(IScopeNode ancestor, IScopeNode node)
        {
            if (ancestor == null || node == null)
                return false;

            var current = node.Parent;
            while (current != null)
            {
                if (ReferenceEquals(current, ancestor))
                    return true;
                current = current.Parent;
            }
            return false;
        }

        void AddIndex<TKey>(Dictionary<TKey, HashSet<Entry>> dict, TKey key, Entry entry)
        {
            if (!dict.TryGetValue(key, out var set))
            {
                set = new HashSet<Entry>(_entryComparer);
                dict[key] = set;
            }
            set.Add(entry);
        }

        void RemoveIndex<TKey>(Dictionary<TKey, HashSet<Entry>> dict, TKey key, Entry entry)
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