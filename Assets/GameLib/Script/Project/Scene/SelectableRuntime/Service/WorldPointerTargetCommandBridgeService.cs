#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using Game.Commands;
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
        readonly List<RunningCommandExecution> _runningExecutions = new(4);

        sealed class RunningCommandExecution
        {
            public readonly string EventName;
            public readonly CancellationTokenSource Cts;
            public UniTask Task;
            public bool Completed;
            public bool Disposed;

            public RunningCommandExecution(string eventName, CancellationTokenSource cts)
            {
                EventName = eventName;
                Cts = cts;
                Completed = false;
                Disposed = false;
            }
        }

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
            CleanupCompletedCommandExecutions();

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
            CancelRunningCommandExecutions(disposeEntries: true);

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
            ExecuteHoverCommands(commands, eventName, isHovered);
        }

        void ExecuteHoverCommands(CommandListData? commands, string eventName, bool isHovered)
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
            StartCommandExecution(commands, context, eventName);
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
            StartCommandExecution(commands, context, eventName);
        }

        void StartCommandExecution(CommandListData commands, CommandContext context, string eventName)
        {
            CleanupCompletedCommandExecutions();

            var behavior = _owner.CommandOverlapBehavior;
            if (behavior == ExecutionBehavior.SkipIfRunning && HasRunningCommandExecution())
                return;

            if (behavior == ExecutionBehavior.CancelAndRun)
                CancelRunningCommandExecutions(disposeEntries: false);

            var cts = new CancellationTokenSource();
            var entry = new RunningCommandExecution(eventName, cts);
            _runningExecutions.Add(entry);
            entry.Task = ExecuteCommandsAsync(entry, commands, context, cts.Token);
        }

        async UniTask ExecuteCommandsAsync(RunningCommandExecution entry, CommandListData commands, CommandContext context, CancellationToken ct)
        {
            try
            {
                var result = await context.Runner.ExecuteListAsync(commands, context, ct, context.Options);
                if (result.Status == CommandRunStatus.Error)
                    Debug.LogError($"[WorldPointerTarget] Command execution failed ({entry.EventName}): {result.Message}");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WorldPointerTarget] Command execution failed ({entry.EventName}): {ex.Message}");
            }
            finally
            {
                entry.Completed = true;
            }
        }

        bool HasRunningCommandExecution()
        {
            for (var i = 0; i < _runningExecutions.Count; i++)
            {
                if (!_runningExecutions[i].Completed)
                    return true;
            }

            return false;
        }

        void CancelRunningCommandExecutions(bool disposeEntries)
        {
            for (var i = 0; i < _runningExecutions.Count; i++)
            {
                var entry = _runningExecutions[i];
                if (!entry.Completed)
                {
                    try
                    {
                        entry.Cts.Cancel();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                }
            }

            if (disposeEntries)
            {
                for (var i = _runningExecutions.Count - 1; i >= 0; i--)
                {
                    var entry = _runningExecutions[i];
                    if (entry.Disposed)
                        continue;

                    try
                    {
                        entry.Cts.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }

                    entry.Disposed = true;
                    _runningExecutions.RemoveAt(i);
                }
            }
        }

        void CleanupCompletedCommandExecutions()
        {
            for (var i = _runningExecutions.Count - 1; i >= 0; i--)
            {
                var entry = _runningExecutions[i];
                if (!entry.Completed || entry.Disposed)
                    continue;

                try
                {
                    entry.Cts.Dispose();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }

                entry.Disposed = true;
                _runningExecutions.RemoveAt(i);
            }
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