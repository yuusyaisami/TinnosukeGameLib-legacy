#nullable enable
using System;
using System.Collections.Generic;

namespace Game.Commands.VNext
{
    public interface ISharedLTSChannelHub
    {
        void Register(string tag, IScopeNode scope);
        bool Unregister(string tag, IScopeNode? scope = null);
        bool TryGet(string tag, out IScopeNode? scope);
        bool TryFindTag(IScopeNode scope, out string tag);
        void Clear();
    }

    public sealed class SharedLTSChannelHub : ISharedLTSChannelHub, IScopeReleaseHandler
    {
        readonly Dictionary<string, IScopeNode> _scopes = new(StringComparer.Ordinal);

        public void Register(string tag, IScopeNode scope)
        {
            if (string.IsNullOrWhiteSpace(tag) || scope == null)
                return;

            _scopes[tag] = scope;
        }

        public bool Unregister(string tag, IScopeNode? scope = null)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return false;

            if (!_scopes.TryGetValue(tag, out var registered) || registered == null)
                return false;

            if (scope != null && !ReferenceEquals(scope, registered))
                return false;

            _scopes.Remove(tag);
            return true;
        }

        public bool TryGet(string tag, out IScopeNode? scope)
        {
            scope = null;
            if (string.IsNullOrWhiteSpace(tag))
                return false;

            if (!_scopes.TryGetValue(tag, out var registered) || registered == null)
                return false;

            var identity = registered.Identity;
            if (identity != null && !identity.IsActive)
            {
                _scopes.Remove(tag);
                return false;
            }

            scope = registered;
            return true;
        }

        public bool TryFindTag(IScopeNode scope, out string tag)
        {
            tag = string.Empty;
            if (scope == null)
                return false;

            string? foundTag = null;
            List<string>? staleTags = null;
            foreach (var pair in _scopes)
            {
                var registered = pair.Value;
                if (registered == null)
                {
                    staleTags ??= new List<string>();
                    staleTags.Add(pair.Key);
                    continue;
                }

                var identity = registered.Identity;
                if (identity != null && !identity.IsActive)
                {
                    staleTags ??= new List<string>();
                    staleTags.Add(pair.Key);
                    continue;
                }

                if (!ReferenceEquals(registered, scope))
                    continue;

                if (foundTag == null || string.CompareOrdinal(pair.Key, foundTag) < 0)
                    foundTag = pair.Key;
            }

            if (staleTags != null)
            {
                for (int i = 0; i < staleTags.Count; i++)
                    _scopes.Remove(staleTags[i]);
            }

            if (string.IsNullOrEmpty(foundTag))
                return false;

            tag = foundTag;
            return true;
        }

        public void Clear()
        {
            _scopes.Clear();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;
            _scopes.Clear();
        }
    }
}
