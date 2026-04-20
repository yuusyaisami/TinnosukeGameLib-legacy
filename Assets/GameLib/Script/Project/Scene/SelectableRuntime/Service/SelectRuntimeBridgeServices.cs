#nullable enable
using System;
using Game;
using Game.Commands.VNext;
using Game.Common;
using Game.VarStoreKeys;
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
        IScopeTickHandler
    {
        readonly WorldPointerTargetMB _owner;
        readonly WorldPointerTargetCommandBridgeService _commandBridge;
        SelectRuntimeManagerMB? _manager;
        IWorldPointerRuntimeService? _service;
        Transform? _lastParent;

        public WorldPointerTargetBridgeService(WorldPointerTargetMB owner)
        {
            _owner = owner;
            _commandBridge = new WorldPointerTargetCommandBridgeService(owner);
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _commandBridge.OnAcquire(scope, isReset);
            Rebind();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            Unbind();
            _commandBridge.OnRelease(scope, isReset);
        }

        public void RefreshBinding()
        {
            Rebind();
        }

        public void ReleaseBinding()
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

        public void ApplyCommandMutations(
            WorldPointerTargetCommandMutationProgram? program,
            ICommandListRuntimeMutationService? mutationService)
        {
            if (_owner == null || program?.Steps == null || program.Steps.Count == 0)
                return;

            for (var i = 0; i < program.Steps.Count; i++)
            {
                var step = program.Steps[i];
                if (step == null || step.Targets == WorldPointerTargetCommandTargets.None)
                    continue;

                ApplyMutation(step.Targets, step.Mutation, mutationService);
            }
        }

        void ApplyMutation(
            WorldPointerTargetCommandTargets targets,
            CommandListMutationStep mutation,
            ICommandListRuntimeMutationService? mutationService)
        {
            if (HasTarget(targets, WorldPointerTargetCommandTargets.LeftClicked))
                _owner.OnLeftClickedCommands.ApplyRuntimeMutation(mutation, mutationService);

            if (HasTarget(targets, WorldPointerTargetCommandTargets.RightClicked))
                _owner.OnRightClickedCommands.ApplyRuntimeMutation(mutation, mutationService);

            if (HasTarget(targets, WorldPointerTargetCommandTargets.LeftShortPressStarted))
                _owner.OnLeftShortPressStartedCommands.ApplyRuntimeMutation(mutation, mutationService);

            if (HasTarget(targets, WorldPointerTargetCommandTargets.LeftShortPressEnded))
                _owner.OnLeftShortPressEndedCommands.ApplyRuntimeMutation(mutation, mutationService);

            if (HasTarget(targets, WorldPointerTargetCommandTargets.RightShortPressStarted))
                _owner.OnRightShortPressStartedCommands.ApplyRuntimeMutation(mutation, mutationService);

            if (HasTarget(targets, WorldPointerTargetCommandTargets.RightShortPressEnded))
                _owner.OnRightShortPressEndedCommands.ApplyRuntimeMutation(mutation, mutationService);

            if (HasTarget(targets, WorldPointerTargetCommandTargets.LeftLongPressStarted))
                _owner.OnLeftLongPressStartedCommands.ApplyRuntimeMutation(mutation, mutationService);

            if (HasTarget(targets, WorldPointerTargetCommandTargets.LeftLongPressEnded))
                _owner.OnLeftLongPressEndedCommands.ApplyRuntimeMutation(mutation, mutationService);

            if (HasTarget(targets, WorldPointerTargetCommandTargets.RightLongPressStarted))
                _owner.OnRightLongPressStartedCommands.ApplyRuntimeMutation(mutation, mutationService);

            if (HasTarget(targets, WorldPointerTargetCommandTargets.RightLongPressEnded))
                _owner.OnRightLongPressEndedCommands.ApplyRuntimeMutation(mutation, mutationService);

            if (HasTarget(targets, WorldPointerTargetCommandTargets.HoverEntered))
                _owner.OnHoverEnteredCommands.ApplyRuntimeMutation(mutation, mutationService);

            if (HasTarget(targets, WorldPointerTargetCommandTargets.HoverExited))
                _owner.OnHoverExitedCommands.ApplyRuntimeMutation(mutation, mutationService);
        }

        static bool HasTarget(WorldPointerTargetCommandTargets value, WorldPointerTargetCommandTargets target)
            => (value & target) != 0;

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

            InitializePointerRelationDefaults();
            _commandBridge.RefreshBinding();

            if (!SelectRuntimeBridgeResolver.TryResolvePointerService(_manager, out _service) || _service == null)
                return;

            _service.RegisterTarget(_owner);
        }

        void Unbind()
        {
            InitializePointerRelationDefaults();
            _commandBridge.ReleaseBinding();

            _service?.UnregisterTarget(_owner);
            _service = null;
            _manager = null;
            _lastParent = null;
        }

        void InitializePointerRelationDefaults()
        {
            if (!ScopeFeatureInstallerUtility.TryGetNearestScopeNode(_owner, includeInactive: true, out var scope) ||
                scope?.Resolver == null)
            {
                return;
            }

            if (!scope.Resolver.TryResolve<IBlackboardService>(out var blackboard) || blackboard == null)
                return;

            WritePointerRelation(blackboard, _owner.PointerSelfResultKey, false);
            WritePointerRelation(blackboard, _owner.PointerSelfOrDescendantResultKey, false);
            WritePointerRelation(blackboard, _owner.HoverStateKey, false);
        }

        static void WritePointerRelation(IBlackboardService blackboard, VarKeyRef key, bool value)
        {
            var varId = ResolveVarId(key);
            if (varId <= 0)
                return;

            blackboard.TryLocalSetVariant(varId, DynamicVariant.FromBool(value));
        }

        static int ResolveVarId(VarKeyRef key)
        {
            if (!string.IsNullOrEmpty(key.StableKey) && VarIdResolver.TryResolve(key.StableKey, out var resolved) && resolved > 0)
                return resolved;

            return key.VarId;
        }
    }

    [Flags]
    public enum WorldPointerTargetCommandTargets
    {
        None = 0,
        LeftClicked = 1 << 0,
        RightClicked = 1 << 1,
        LeftShortPressStarted = 1 << 2,
        LeftShortPressEnded = 1 << 3,
        RightShortPressStarted = 1 << 4,
        RightShortPressEnded = 1 << 5,
        LeftLongPressStarted = 1 << 6,
        LeftLongPressEnded = 1 << 7,
        RightLongPressStarted = 1 << 8,
        RightLongPressEnded = 1 << 9,
        HoverEntered = 1 << 10,
        HoverExited = 1 << 11,
        All = LeftClicked | RightClicked | LeftShortPressStarted | LeftShortPressEnded |
              RightShortPressStarted | RightShortPressEnded | LeftLongPressStarted | LeftLongPressEnded |
              RightLongPressStarted | RightLongPressEnded | HoverEntered | HoverExited,
    }

    public sealed class SelectableRuntimeBridgeService :
        IScopeAcquireHandler,
        IScopeReleaseHandler,
        IScopeTickHandler
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

        public void RefreshBinding()
        {
            Rebind();
        }

        public void ReleaseBinding()
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
        IScopeTickHandler
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

        public void RefreshBinding()
        {
            Rebind();
        }

        public void ReleaseBinding()
        {
            Unbind();
        }

        public bool TryEnterEditorMode()
        {
            if (_owner == null)
                return false;

            if (_service == null)
                Rebind();

            return _service != null && _service.TryEnterEditorMode(_owner);
        }

        public bool IsEditing()
        {
            if (_owner == null)
                return false;

            if (_service == null)
                Rebind();

            return _service != null && _service.IsEditing(_owner);
        }

        public bool TryExitEditorMode(bool runExitCommands)
        {
            if (_owner == null)
                return false;

            if (_service == null)
                Rebind();

            return _service != null && _service.TryExitEditorMode(_owner, runExitCommands);
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
