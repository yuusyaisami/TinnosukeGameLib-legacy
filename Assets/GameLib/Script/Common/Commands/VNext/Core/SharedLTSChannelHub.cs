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
