#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using Game.Commands.VNext;
using Game.Common;
using Game.VarStoreKeys;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.SelectRuntime
{
    public sealed class WorldPointerTargetCommandBridgeService :
        IScopeAcquireHandler,
        IScopeReleaseHandler,
        ITickable
    {
        bool EnableShortPressBridgeDebugLog = false;

        readonly WorldPointerTargetMB _owner;
        IScopeNode? _ownerScope;
        IWorldPointerRuntimeService? _pointerService;
        Transform? _lastParent;

        public WorldPointerTargetCommandBridgeService(WorldPointerTargetMB owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
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

            if (_lastParent != GetOwnerParentTransform())
                Rebind();
        }

        void Rebind()
        {
            var nextOwnerScope = ResolveOwnerScope(_owner);
            var nextPointerService = ResolvePointerService(nextOwnerScope);

            if (nextPointerService == null)
            {
                var manager = SelectRuntimeBridgeResolver.FindNearestManager(_owner != null ? _owner.transform : null);
                if (SelectRuntimeBridgeResolver.TryResolvePointerService(manager, out var managerPointerService) && managerPointerService != null)
                    nextPointerService = managerPointerService;
            }

            if (ReferenceEquals(_ownerScope, nextOwnerScope) && ReferenceEquals(_pointerService, nextPointerService))
            {
                _lastParent = GetOwnerParentTransform();
                return;
            }

            Unbind();
            _ownerScope = nextOwnerScope;
            _pointerService = nextPointerService;
            _lastParent = GetOwnerParentTransform();

            InitializePointerRelationDefaults(_ownerScope);

            if (_pointerService == null)
                return;

            _pointerService.OnLeftClicked += HandleLeftClicked;
            _pointerService.OnRightClicked += HandleRightClicked;
            _pointerService.OnLeftShortPressStarted += HandleLeftShortPressStarted;
            _pointerService.OnLeftShortPressEnded += HandleLeftShortPressEnded;
            _pointerService.OnRightShortPressStarted += HandleRightShortPressStarted;
            _pointerService.OnRightShortPressEnded += HandleRightShortPressEnded;
            _pointerService.OnLeftLongPressStarted += HandleLeftLongPressStarted;
            _pointerService.OnLeftLongPressEnded += HandleLeftLongPressEnded;
            _pointerService.OnRightLongPressStarted += HandleRightLongPressStarted;
            _pointerService.OnRightLongPressEnded += HandleRightLongPressEnded;
            _pointerService.OnHoveredChanged += HandleHoveredChanged;
        }

        void Unbind()
        {
            InitializePointerRelationDefaults(_ownerScope);

            if (_pointerService != null)
            {
                _pointerService.OnLeftClicked -= HandleLeftClicked;
                _pointerService.OnRightClicked -= HandleRightClicked;
                _pointerService.OnLeftShortPressStarted -= HandleLeftShortPressStarted;
                _pointerService.OnLeftShortPressEnded -= HandleLeftShortPressEnded;
                _pointerService.OnRightShortPressStarted -= HandleRightShortPressStarted;
                _pointerService.OnRightShortPressEnded -= HandleRightShortPressEnded;
                _pointerService.OnLeftLongPressStarted -= HandleLeftLongPressStarted;
                _pointerService.OnLeftLongPressEnded -= HandleLeftLongPressEnded;
                _pointerService.OnRightLongPressStarted -= HandleRightLongPressStarted;
                _pointerService.OnRightLongPressEnded -= HandleRightLongPressEnded;
                _pointerService.OnHoveredChanged -= HandleHoveredChanged;
            }

            _pointerService = null;
            _ownerScope = null;
            _lastParent = null;
        }

        void InitializePointerRelationDefaults(IScopeNode? scope)
        {
            var blackboard = ResolveBlackboard(scope);
            WritePointerRelation(blackboard, _owner.PointerSelfResultKey, false);
            WritePointerRelation(blackboard, _owner.PointerSelfOrDescendantResultKey, false);
            WritePointerRelation(blackboard, _owner.HoverStateKey, false);
        }

        void HandleLeftClicked(WorldPointerEventData eventData)
        {
            ExecuteCommands(_owner.OnLeftClickedCommands, eventData, "LeftClicked");
        }

        void HandleRightClicked(WorldPointerEventData eventData)
        {
            ExecuteCommands(_owner.OnRightClickedCommands, eventData, "RightClicked");
        }

        void HandleLeftShortPressStarted(WorldPointerEventData eventData)
        {
            if (EnableShortPressBridgeDebugLog)
                Debug.Log($"[WorldPointerTargetCommandBridge] Receive LeftShortPressStarted target={(eventData.Target != null ? eventData.Target.name : "(none)")} commandCount={_owner.OnLeftShortPressStartedCommands.Count}");

            ExecuteCommands(_owner.OnLeftShortPressStartedCommands, eventData, "LeftShortPressStarted");
        }

        void HandleLeftShortPressEnded(WorldPointerEventData eventData)
        {
            if (EnableShortPressBridgeDebugLog)
                Debug.Log($"[WorldPointerTargetCommandBridge] Receive LeftShortPressEnded target={(eventData.Target != null ? eventData.Target.name : "(none)")} commandCount={_owner.OnLeftShortPressEndedCommands.Count}");

            ExecuteCommands(_owner.OnLeftShortPressEndedCommands, eventData, "LeftShortPressEnded");
        }

        void HandleRightShortPressStarted(WorldPointerEventData eventData)
        {
            ExecuteCommands(_owner.OnRightShortPressStartedCommands, eventData, "RightShortPressStarted");
        }

        void HandleRightShortPressEnded(WorldPointerEventData eventData)
        {
            ExecuteCommands(_owner.OnRightShortPressEndedCommands, eventData, "RightShortPressEnded");
        }

        void HandleLeftLongPressStarted(WorldPointerEventData eventData)
        {
            ExecuteCommands(_owner.OnLeftLongPressStartedCommands, eventData, "LeftLongPressStarted");
        }

        void HandleLeftLongPressEnded(WorldPointerEventData eventData)
        {
            ExecuteCommands(_owner.OnLeftLongPressEndedCommands, eventData, "LeftLongPressEnded");
        }

        void HandleRightLongPressStarted(WorldPointerEventData eventData)
        {
            ExecuteCommands(_owner.OnRightLongPressStartedCommands, eventData, "RightLongPressStarted");
        }

        void HandleRightLongPressEnded(WorldPointerEventData eventData)
        {
            ExecuteCommands(_owner.OnRightLongPressEndedCommands, eventData, "RightLongPressEnded");
        }

        void HandleHoveredChanged(WorldPointerHoverChangedEventData eventData)
        {
            if (_owner == null)
                return;

            var entered = ReferenceEquals(eventData.CurrentTarget, _owner);
            var exited = ReferenceEquals(eventData.PreviousTarget, _owner) && !entered;
            if (!entered && !exited)
                return;

            var isHovered = entered;
            var blackboard = ResolveBlackboard(_ownerScope);
            WritePointerRelation(blackboard, _owner.HoverStateKey, isHovered);

            var commands = entered ? _owner.OnHoverEnteredCommands : _owner.OnHoverExitedCommands;
            var eventName = entered ? "HoverEntered" : "HoverExited";
            ExecuteHoverCommands(commands, eventData.EventData, eventName, isHovered);
        }

        void ExecuteHoverCommands(CommandListData? commands, WorldPointerEventData eventData, string eventName, bool isHovered)
        {
            if (commands == null || commands.Count == 0)
                return;

            if (_ownerScope == null || _ownerScope.Resolver == null)
                return;

            if (!TryResolveRunner(_ownerScope, out var runner) || runner == null)
                return;

            var vars = CreateVars(_ownerScope);
            WritePointerRelation(vars, _owner.HoverStateKey, isHovered);

            var context = new CommandContext(_ownerScope, vars, runner, actor: _ownerScope, CommandRunOptions.Default, _ownerScope, _ownerScope, _ownerScope);
            UniTask.Void(async () =>
            {
                try
                {
                    await runner.ExecuteListAsync(commands, context, CancellationToken.None, context.Options);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[WorldPointerTarget] Command execution failed ({eventName}): {ex.Message}");
                }
            });
        }

        void ExecuteCommands(CommandListData? commands, WorldPointerEventData eventData, string eventName)
        {
            if (_owner == null)
                return;

            var isSelf = ReferenceEquals(eventData.Target, _owner);
            var isSelfOrDescendant = IsSelfOrDescendant(eventData.Target, _owner);

            var blackboard = ResolveBlackboard(_ownerScope);
            WritePointerRelation(blackboard, _owner.PointerSelfResultKey, isSelf);
            WritePointerRelation(blackboard, _owner.PointerSelfOrDescendantResultKey, isSelfOrDescendant);

            if (EnableShortPressBridgeDebugLog && (eventName == "LeftShortPressStarted" || eventName == "LeftShortPressEnded"))
                Debug.Log($"[WorldPointerTargetCommandBridge] Gate {eventName} isSelf={isSelf} onlySelf={_owner.OnlyExecuteWhenSelfClicked} scopeReady={_ownerScope != null && _ownerScope.Resolver != null} commands={(commands != null ? commands.Count : 0)}");

            if (_owner.OnlyExecuteWhenSelfClicked && !isSelf)
                return;

            if (commands == null || commands.Count == 0)
                return;

            if (_ownerScope == null || _ownerScope.Resolver == null)
                return;

            if (!TryResolveRunner(_ownerScope, out var runner) || runner == null)
                return;

            var vars = CreateVars(_ownerScope);
            WritePointerRelation(vars, _owner.PointerSelfResultKey, isSelf);
            WritePointerRelation(vars, _owner.PointerSelfOrDescendantResultKey, isSelfOrDescendant);

            var context = new CommandContext(_ownerScope, vars, runner, actor: _ownerScope, CommandRunOptions.Default, _ownerScope, _ownerScope, _ownerScope);
            UniTask.Void(async () =>
            {
                try
                {
                    await runner.ExecuteListAsync(commands, context, CancellationToken.None, context.Options);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[WorldPointerTarget] Command execution failed ({eventName}): {ex.Message}");
                }
            });
        }

        static IVarStore CreateVars(IScopeNode scope)
        {
            var vars = new VarStore();
            if (scope != null && scope.Resolver != null && scope.Resolver.TryResolve<IBlackboardService>(out var blackboard) && blackboard != null)
                blackboard.MergeInto(vars, overwrite: true);

            return vars;
        }

        static void WritePointerRelation(IBlackboardService? blackboard, VarKeyRef key, bool value)
        {
            if (blackboard == null)
                return;

            var varId = ResolveVarId(key);
            if (varId <= 0)
                return;

            blackboard.TryLocalSetVariant(varId, DynamicVariant.FromBool(value));
        }

        static void WritePointerRelation(IVarStore vars, VarKeyRef key, bool value)
        {
            if (vars == null)
                return;

            var varId = ResolveVarId(key);
            if (varId <= 0)
                return;

            vars.TrySetVariant(varId, DynamicVariant.FromBool(value));
        }

        static bool IsSelfOrDescendant(WorldPointerTargetMB? clickedTarget, WorldPointerTargetMB owner)
        {
            if (clickedTarget == null || owner == null)
                return false;

            if (ReferenceEquals(clickedTarget, owner))
                return true;

            var clickedTransform = clickedTarget.transform;
            var ownerTransform = owner.transform;
            return clickedTransform != null && ownerTransform != null && clickedTransform.IsChildOf(ownerTransform);
        }

        static int ResolveVarId(VarKeyRef key)
        {
            if (!string.IsNullOrEmpty(key.StableKey) && VarIdResolver.TryResolve(key.StableKey, out var resolved) && resolved > 0)
                return resolved;

            return key.VarId;
        }

        static bool TryResolveRunner(IScopeNode scope, out ICommandRunner? runner)
        {
            runner = null;
            var resolver = scope?.Resolver;
            if (resolver == null)
                return false;

            return resolver.TryResolve(out runner) && runner != null;
        }

        static IScopeNode? ResolveOwnerScope(WorldPointerTargetMB owner)
        {
            if (owner == null)
                return null;

            return ScopeFeatureInstallerUtility.TryGetNearestScopeNode(owner, includeInactive: true, out var scope) ? scope : null;
        }

        static IWorldPointerRuntimeService? ResolvePointerService(IScopeNode? start)
        {
            var current = start;
            while (current != null)
            {
                var resolver = current.Resolver;
                if (resolver != null && resolver.TryResolve<IWorldPointerRuntimeService>(out var service) && service != null)
                    return service;

                current = current.Parent;
            }

            return null;
        }

        static IBlackboardService? ResolveBlackboard(IScopeNode? scope)
        {
            var resolver = scope?.Resolver;
            if (resolver != null && resolver.TryResolve<IBlackboardService>(out var blackboard) && blackboard != null)
                return blackboard;

            return null;
        }

        Transform? GetOwnerParentTransform()
        {
            var ownerTransform = _owner != null ? _owner.transform : null;
            return ownerTransform != null ? ownerTransform.parent : null;
        }
    }
}