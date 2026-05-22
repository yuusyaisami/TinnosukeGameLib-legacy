#nullable enable
using System;
using UnityEngine;
using VContainer;

namespace Game.Search
{
    public sealed class DynamicObjectAutoRegistrar :
        IScopeAcquireHandler,
        IScopeReleaseHandler,
        IDisposable
    {
        readonly ScopeIdentityMB _identityMb;
        readonly IRuntimeResolver _resolver;

        IDynamicObjectRegistryService? _registry;
        IScopeNode? _scope;
        bool _registered;

        public DynamicObjectAutoRegistrar(ScopeIdentityMB identityMb, IRuntimeResolver resolver)
        {
            _identityMb = identityMb;
            _resolver = resolver;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            if (scope == null)
                return;

            if (!_identityMb.RegisterToDynamicRegistry)
                return;

            var kind = scope.Kind;
            if (kind != LifetimeScopeKind.Entity && kind != LifetimeScopeKind.Runtime)
                return;

            if (kind == LifetimeScopeKind.Runtime && HasUiAncestor(scope.Parent))
                return;

            if (_registered)
                return;

            var identity = scope.Identity;
            if (identity == null)
                return;

            if (!_resolver.TryResolve<IDynamicObjectRegistryService>(out var registry) || registry == null)
                return;

            _registry = registry;
            _scope = scope;
            _registry.Register(scope, identity);
            _registered = true;
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            UnregisterIfNeeded();
        }

        public void Dispose()
        {
            UnregisterIfNeeded();
        }

        void UnregisterIfNeeded()
        {
            if (!_registered)
                return;

            try
            {
                if (_scope != null)
                    _registry?.Unregister(_scope);
            }
            catch
            {
            }
            finally
            {
                _registered = false;
                _scope = null;
            }
        }

        static bool HasUiAncestor(IScopeNode? node)
        {
            while (node != null)
            {
                var k = node.Kind;
                if (k == LifetimeScopeKind.UI || k == LifetimeScopeKind.UIElement)
                    return true;
                node = node.Parent;
            }
            return false;
        }
    }
}

