#nullable enable
using Game;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.SelectRuntime
{
    static class SelectRuntimeBridgeResolver
    {
        public static SelectRuntimeManagerMB? FindNearestManager(Transform? origin)
        {
            var current = origin;
            while (current != null)
            {
                if (current.TryGetComponent<SelectRuntimeManagerMB>(out var manager) && manager != null)
                    return manager;

                current = current.parent;
            }

            return null;
        }

        public static bool TryResolveManagerScope(SelectRuntimeManagerMB manager, out IScopeNode? scope)
        {
            return ScopeFeatureInstallerUtility.TryGetNearestScopeNode(manager, includeInactive: true, out scope);
        }

        public static bool TryResolvePointerService(SelectRuntimeManagerMB? manager, out IWorldPointerRuntimeService? service)
        {
            service = null;
            if (manager == null)
                return false;

            if (!TryResolveManagerScope(manager, out var scope) || scope?.Resolver == null)
                return false;

            if (scope.Resolver.TryResolve<IWorldPointerRuntimeService>(out var resolved) && resolved != null)
            {
                service = resolved;
                return true;
            }

            return false;
        }

        public static bool TryResolveManagerService(SelectRuntimeManagerMB? manager, out ISelectRuntimeManagerService? service)
        {
            service = null;
            if (manager == null)
                return false;

            if (!TryResolveManagerScope(manager, out var scope) || scope?.Resolver == null)
                return false;

            if (scope.Resolver.TryResolve<ISelectRuntimeManagerService>(out var resolved) && resolved != null)
            {
                service = resolved;
                return true;
            }

            return false;
        }

        public static bool TryResolveMoveRotateService(SelectRuntimeManagerMB? manager, out IUserMoveRotateRuntimeService? service)
        {
            service = null;
            if (manager == null)
                return false;

            if (!TryResolveManagerScope(manager, out var scope) || scope?.Resolver == null)
                return false;

            if (scope.Resolver.TryResolve<IUserMoveRotateRuntimeService>(out var resolved) && resolved != null)
            {
                service = resolved;
                return true;
            }

            return false;
        }
    }

    public sealed class WorldPointerTargetBridgeService :
        IScopeAcquireHandler,
        IScopeReleaseHandler,
        ITickable
    {
        readonly WorldPointerTargetMB _owner;
        SelectRuntimeManagerMB? _manager;
        IWorldPointerRuntimeService? _service;
        Transform? _lastParent;

        public WorldPointerTargetBridgeService(WorldPointerTargetMB owner)
        {
            _owner = owner;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            Rebind();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            Unbind();
        }

        public void Tick()
        {
            if (_owner == null)
                return;

            if (_lastParent != _owner.transform.parent)
                Rebind();
        }

        void Rebind()
        {
            var nextManager = SelectRuntimeBridgeResolver.FindNearestManager(_owner.transform);
            if (ReferenceEquals(_manager, nextManager))
            {
                _lastParent = _owner.transform.parent;
                return;
            }

            Unbind();
            _manager = nextManager;
            _lastParent = _owner.transform.parent;
            if (!SelectRuntimeBridgeResolver.TryResolvePointerService(_manager, out _service) || _service == null)
                return;

            _service.RegisterTarget(_owner);
        }

        void Unbind()
        {
            _service?.UnregisterTarget(_owner);
            _service = null;
            _manager = null;
            _lastParent = null;
        }
    }

    public sealed class SelectableRuntimeBridgeService :
        IScopeAcquireHandler,
        IScopeReleaseHandler,
        ITickable
    {
        readonly SelectableRuntimeMB _owner;
        SelectRuntimeManagerMB? _manager;
        ISelectRuntimeManagerService? _service;
        Transform? _lastParent;

        public SelectableRuntimeBridgeService(SelectableRuntimeMB owner)
        {
            _owner = owner;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            Rebind();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            Unbind();
        }

        public void Tick()
        {
            if (_owner == null)
                return;

            if (_lastParent != _owner.transform.parent)
                Rebind();
        }

        void Rebind()
        {
            var nextManager = SelectRuntimeBridgeResolver.FindNearestManager(_owner.transform);
            if (ReferenceEquals(_manager, nextManager))
            {
                _lastParent = _owner.transform.parent;
                return;
            }

            Unbind();
            _manager = nextManager;
            _lastParent = _owner.transform.parent;
            if (!SelectRuntimeBridgeResolver.TryResolveManagerService(_manager, out _service) || _service == null)
                return;

            _service.RegisterSelectable(_owner);
        }

        void Unbind()
        {
            _service?.UnregisterSelectable(_owner);
            _service = null;
            _manager = null;
            _lastParent = null;
        }
    }

    public sealed class UserMoveRotateRuntimeBridgeService :
        IScopeAcquireHandler,
        IScopeReleaseHandler,
        ITickable
    {
        readonly UserMoveRotateRuntimeMB _owner;
        SelectRuntimeManagerMB? _manager;
        IUserMoveRotateRuntimeService? _service;
        Transform? _lastParent;

        public UserMoveRotateRuntimeBridgeService(UserMoveRotateRuntimeMB owner)
        {
            _owner = owner;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            Rebind();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            Unbind();
        }

        public void Tick()
        {
            if (_owner == null)
                return;

            if (_lastParent != _owner.transform.parent)
                Rebind();
        }

        void Rebind()
        {
            var nextManager = SelectRuntimeBridgeResolver.FindNearestManager(_owner.transform);
            if (ReferenceEquals(_manager, nextManager))
            {
                _lastParent = _owner.transform.parent;
                return;
            }

            Unbind();
            _manager = nextManager;
            _lastParent = _owner.transform.parent;
            if (!SelectRuntimeBridgeResolver.TryResolveMoveRotateService(_manager, out _service) || _service == null)
                return;

            _service.RegisterEditor(_owner);
        }

        void Unbind()
        {
            _service?.UnregisterEditor(_owner);
            _service = null;
            _manager = null;
            _lastParent = null;
        }
    }
}
