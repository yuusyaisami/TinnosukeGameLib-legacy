#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using Game.Commands.VNext;
using Game.Common;
using Game.SelectRuntime;
using Game.Vars.Generated;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Trait
{
    public sealed class RuntimeTraitBridgeService :
        IScopeAcquireHandler,
        IScopeReleaseHandler,
        ITickable
    {
        readonly RuntimeTraitMB _owner;
        TraitRuntimeLinkKey _registeredKey;
        bool _hasRegisteredKey;

        public RuntimeTraitBridgeService(RuntimeTraitMB owner)
        {
            _owner = owner;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            RefreshRegistration();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            UnregisterCurrent();
        }

        public void Tick()
        {
            RefreshRegistration();
        }

        void RefreshRegistration()
        {
            var currentLink = _owner.LinkData;
            if (currentLink == null)
            {
                UnregisterCurrent();
                return;
            }

            var currentKey = currentLink.ToLinkKey();
            if (_hasRegisteredKey && _registeredKey.Equals(currentKey))
                return;

            UnregisterCurrent();
            if (TraitPlacementScopeResolver.TryResolvePlacementService(currentLink, out var placementService) &&
                placementService != null)
            {
                placementService.NotifyRuntimeEnabled(currentLink, _owner);
                _registeredKey = currentKey;
                _hasRegisteredKey = true;
            }
        }

        void UnregisterCurrent()
        {
            if (!_hasRegisteredKey)
                return;

            var currentLink = _owner.LinkData;
            if (currentLink != null &&
                TraitPlacementScopeResolver.TryResolvePlacementService(currentLink, out var placementService) &&
                placementService != null)
            {
                placementService.NotifyRuntimeDisabled(currentLink, _owner);
            }

            _hasRegisteredKey = false;
            _registeredKey = default;
        }
    }

    public enum RuntimeTraitPresentationCommandTarget
    {
        Hidden = 10,
        Visible = 20,
        Both = 30,
    }

    public interface IRuntimeTraitPresentationCommandMutationService
    {
        bool MutatePresentationCommands(
            RuntimeTraitPresentationCommandTarget target,
            CommandListMutationStep mutation,
            ICommandListRuntimeMutationService? mutationService);
    }

    public sealed class RuntimeTraitPresentationBridgeService :
        IRuntimeTraitPresentationCommandMutationService,
        IScopeAcquireHandler,
        IScopeReleaseHandler,
        ITickable
    {
        static readonly Vector3 HiddenOffset = new(0f, -100000f, 0f);

        readonly RuntimeTraitMB _owner;
        TraitRuntimeLinkKey _registeredKey;
        bool _hasRegisteredKey;
        SelectRuntimeManagerMB? _manager;
        IWorldPointerRuntimeService? _pointerService;
        ITraitPlacementService? _placementService;
        TraitRuntimeLinkData? _linkData;
        WorldPointerTargetMB? _target;
        Transform? _lastParent;
        Vector3 _visiblePosition;
        Quaternion _visibleRotation = Quaternion.identity;
        bool _hasVisiblePose;
        bool _isHidden;
        bool _hasAppliedPresentationState;
        TraitRuntimePresentationState _appliedPresentationState;
        TraitRuntimeLinkKey _spawnExecutedKey;
        bool _hasSpawnExecutedKey;
        readonly PresentationCommandMutationSlot _hiddenCommandsMutation = new();
        readonly PresentationCommandMutationSlot _visibleCommandsMutation = new();

        public RuntimeTraitPresentationBridgeService(RuntimeTraitMB owner)
        {
            _owner = owner;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            CaptureVisiblePose();
            Rebind();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            Unbind();
        }

        public bool MutatePresentationCommands(
            RuntimeTraitPresentationCommandTarget target,
            CommandListMutationStep mutation,
            ICommandListRuntimeMutationService? mutationService)
        {
            if (mutation == null)
                return false;

            return target switch
            {
                RuntimeTraitPresentationCommandTarget.Hidden => _hiddenCommandsMutation.Apply(mutation, mutationService),
                RuntimeTraitPresentationCommandTarget.Visible => _visibleCommandsMutation.Apply(mutation, mutationService),
                RuntimeTraitPresentationCommandTarget.Both
                    => _hiddenCommandsMutation.Apply(mutation, mutationService)
                       && _visibleCommandsMutation.Apply(mutation, mutationService),
                _ => false,
            };
        }

        public void Tick()
        {
            if (_owner == null)
                return;

            RefreshLinkBinding();
            if (_lastParent != _owner.transform.parent)
                Rebind();
        }

        void RefreshLinkBinding()
        {
            var currentLink = _owner.LinkData;
            if (currentLink == null)
            {
                _linkData = null;
                UnsubscribePlacementService();
                _hasRegisteredKey = false;
                _registeredKey = default;
                _hasSpawnExecutedKey = false;
                _spawnExecutedKey = default;
                return;
            }

            var currentKey = currentLink.ToLinkKey();
            if (_hasRegisteredKey && _registeredKey.Equals(currentKey))
            {
                ExecuteDefinitionSpawnCommandsIfNeeded(currentLink);
                return;
            }

            _linkData = currentLink;
            _registeredKey = currentKey;
            _hasRegisteredKey = true;
            ExecuteDefinitionSpawnCommandsIfNeeded(currentLink);

            UnsubscribePlacementService();
            if (TraitPlacementScopeResolver.TryResolvePlacementService(_linkData, out var placementService) && placementService != null)
            {
                _placementService = placementService;
                _placementService.OnPresentationStateChanged += HandlePresentationStateChanged;

                if (_placementService.TryGetPresentationState(_linkData.HolderKey, _linkData.TraitKey, out var state))
                    ApplyPresentationState(state);
            }
        }

        void ExecuteDefinitionSpawnCommandsIfNeeded(TraitRuntimeLinkData? linkData)
        {
            if (_owner == null || linkData == null)
                return;

            var linkKey = linkData.ToLinkKey();
            if (_hasSpawnExecutedKey && _spawnExecutedKey.Equals(linkKey))
                return;

            if (!TryResolveRuntimeScope(_owner, out var runtimeScope) || runtimeScope == null)
                return;

            var definition = ResolveTraitDefinition(runtimeScope, linkData);
            if (definition == null)
                return;

            _spawnExecutedKey = linkKey;
            _hasSpawnExecutedKey = true;

            if (!definition.RunOnRuntimeSpawnCommands)
                return;

            ExecuteCommandList(runtimeScope, definition.OnRuntimeSpawnCommands, "DefinitionSpawn");
        }

        void Rebind()
        {
            _linkData ??= _owner.LinkData;
            if (_linkData == null)
            {
                _hasVisiblePose = false;
                _isHidden = false;
            }

            var nextManager = SelectRuntimeBridgeResolver.FindNearestManager(_owner.transform);
            if (ReferenceEquals(_manager, nextManager))
            {
                _lastParent = _owner.transform.parent;
            }
            else
            {
                UnbindPointerOnly();
                _manager = nextManager;
                _lastParent = _owner.transform.parent;

                _target = _owner.GetComponent<WorldPointerTargetMB>();
                if (_target == null)
                    _target = _owner.GetComponentInChildren<WorldPointerTargetMB>(true);

                if (_target != null && SelectRuntimeBridgeResolver.TryResolvePointerService(_manager, out _pointerService) && _pointerService != null)
                    _pointerService.OnRightClicked += HandleRightClicked;
            }

            UnsubscribePlacementService();
            if (_linkData != null && TraitPlacementScopeResolver.TryResolvePlacementService(_linkData, out var placementService) && placementService != null)
            {
                _placementService = placementService;
                _placementService.OnPresentationStateChanged += HandlePresentationStateChanged;

                if (_placementService.TryGetPresentationState(_linkData.HolderKey, _linkData.TraitKey, out var state))
                    ApplyPresentationState(state);
            }
        }

        void HandleRightClicked(WorldPointerEventData eventData)
        {
            if (_target == null || !ReferenceEquals(eventData.Target, _target))
                return;

            var linkData = _owner.LinkData;
            if (linkData == null)
                return;

            RuntimeLifetimeScope? runtimeScope = null;
            TryResolveRuntimeScope(_owner, out runtimeScope);

            // Hidden の手動適用は MB の Condition で制御する。
            // 条件が false のときは右クリックしても状態を変えない。
            if (!_owner.CanHideOnRightClick(runtimeScope))
                return;

            if (!TraitPlacementScopeResolver.TryResolvePlacementService(linkData, out var placementService) || placementService == null)
                return;

            placementService.TrySetPresentationState(linkData.HolderKey, linkData.TraitKey, TraitRuntimePresentationState.Hidden);
        }

        void HandlePresentationStateChanged(TraitRuntimePresentationChange change)
        {
            if (_linkData == null)
                return;

            if (!string.Equals(change.HolderKey, _linkData.HolderKey, System.StringComparison.Ordinal) ||
                !string.Equals(change.TraitKey, _linkData.TraitKey, System.StringComparison.Ordinal))
                return;

            ApplyPresentationState(change.CurrentState);
        }

        void ApplyPresentationState(TraitRuntimePresentationState state)
        {
            if (_owner == null)
                return;

            if (!TryResolveRuntimeScope(_owner, out var runtimeScope) || runtimeScope == null)
                return;

            var previousState = _hasAppliedPresentationState
                ? _appliedPresentationState
                : TraitRuntimePresentationState.None;

            if (state == TraitRuntimePresentationState.Hidden)
            {
                if (previousState != TraitRuntimePresentationState.Hidden)
                {
                    CaptureVisiblePose();
                    runtimeScope.transform.position = _visiblePosition + HiddenOffset;
                    runtimeScope.transform.rotation = _visibleRotation;
                    _isHidden = true;
                    ExecutePresentationCommands(runtimeScope, _owner.OnHiddenCommands, _hiddenCommandsMutation, "Hidden");
                    ExecuteDefinitionPresentationCommands(runtimeScope, TraitRuntimePresentationState.Hidden);
                }

                runtimeScope.RuntimeIdentity.IsActive = false;
                runtimeScope.TrySetVisible(false);
                _hasAppliedPresentationState = true;
                _appliedPresentationState = TraitRuntimePresentationState.Hidden;
                return;
            }

            if (state == TraitRuntimePresentationState.Visible)
            {
                if (_isHidden && _hasVisiblePose)
                {
                    runtimeScope.transform.SetPositionAndRotation(_visiblePosition, _visibleRotation);
                }

                runtimeScope.RuntimeIdentity.IsActive = true;
                runtimeScope.TrySetVisible(true);
                _isHidden = false;
                if (previousState != TraitRuntimePresentationState.Visible)
                {
                    ExecutePresentationCommands(runtimeScope, _owner.OnVisibleCommands, _visibleCommandsMutation, "Visible");
                    ExecuteDefinitionPresentationCommands(runtimeScope, TraitRuntimePresentationState.Visible);
                }

                _hasAppliedPresentationState = true;
                _appliedPresentationState = TraitRuntimePresentationState.Visible;
            }
        }

        void ExecuteDefinitionPresentationCommands(RuntimeLifetimeScope scope, TraitRuntimePresentationState state)
        {
            var definition = ResolveTraitDefinition(scope, _linkData);
            if (definition == null)
                return;

            switch (state)
            {
                case TraitRuntimePresentationState.Visible:
                    if (definition.RunOnRuntimeVisibleCommands)
                        ExecuteCommandList(scope, definition.OnRuntimeVisibleCommands, "DefinitionVisible");
                    break;

                case TraitRuntimePresentationState.Hidden:
                    if (definition.RunOnRuntimeHiddenCommands)
                        ExecuteCommandList(scope, definition.OnRuntimeHiddenCommands, "DefinitionHidden");
                    break;
            }
        }

        void ExecutePresentationCommands(
            RuntimeLifetimeScope scope,
            DynamicValue<CommandListData> commandSource,
            PresentationCommandMutationSlot mutationSlot,
            string eventName)
        {
            if (scope == null)
                return;

            var resolver = scope.Resolver;
            if (!TryResolveCommandRunner(scope, out var runner) || runner == null)
                return;

            var vars = CreateVars(scope);
            var dynamicContext = new SimpleDynamicContext(vars, scope);
            if (!mutationSlot.TryBuild(commandSource, dynamicContext, out var commands) || commands == null || commands.Count == 0)
                return;

            ExecuteCommandList(scope, commands, eventName);
        }

        void ExecuteCommandList(
            RuntimeLifetimeScope scope,
            CommandListData commands,
            string eventName)
        {
            if (scope == null || commands == null || commands.Count == 0)
                return;

            if (!TryResolveCommandRunner(scope, out var runner) || runner == null)
                return;

            var vars = CreateVars(scope);
            var context = new CommandContext(scope, vars, runner, actor: scope, CommandRunOptions.Default, scope, scope, scope);
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
                    Debug.LogError($"[RuntimeTraitPresentationBridgeService] Command execution failed ({eventName}): {ex.Message}");
                }
            });
        }

        static TraitDefinitionSO? ResolveTraitDefinition(RuntimeLifetimeScope scope, TraitRuntimeLinkData? linkData)
        {
            var resolver = scope?.Resolver;
            if (resolver != null &&
                resolver.TryResolve<IBlackboardService>(out var blackboard) &&
                blackboard != null &&
                blackboard.LocalVars.TryGetVariant(VarIds.GameLib.Base.Trait.Element.definitionAsset, out var variant) &&
                variant.TryGet<TraitDefinitionSO>(out var blackboardDefinition) &&
                blackboardDefinition != null)
            {
                return blackboardDefinition;
            }

            if (linkData == null)
                return null;

            if (!TraitPlacementScopeResolver.TryResolveSourceScope(linkData, out var sourceScope) || sourceScope?.Resolver == null)
                return null;

            if (!sourceScope.Resolver.TryResolve<ITraitHolderHubService>(out var holderHub) || holderHub == null)
                return null;

            if (!holderHub.TryGetHolder(linkData.HolderKey, out var holder) || holder == null)
                return null;

            var traits = holder.Traits;
            if (traits == null || traits.Count == 0)
                return null;

            var traitKey = linkData.TraitKey;
            if (!string.IsNullOrWhiteSpace(traitKey))
            {
                for (var i = 0; i < traits.Count; i++)
                {
                    var instance = traits[i];
                    if (instance == null)
                        continue;

                    if (!string.Equals(instance.InstanceId, traitKey, StringComparison.Ordinal))
                        continue;

                    if (instance.Definition is TraitDefinitionSO byInstance)
                        return byInstance;
                }
            }

            var definitionId = linkData.TraitDefinitionId;
            if (string.IsNullOrWhiteSpace(definitionId))
                return null;

            for (var i = 0; i < traits.Count; i++)
            {
                var instance = traits[i];
                if (instance == null)
                    continue;

                var definition = instance.Definition;
                if (definition == null)
                    continue;

                if (!string.Equals(definition.DefinitionId, definitionId, StringComparison.Ordinal))
                    continue;

                if (definition is TraitDefinitionSO byDefinitionId)
                    return byDefinitionId;
            }

            return null;
        }

        void CaptureVisiblePose()
        {
            if (_owner == null)
                return;

            _visiblePosition = _owner.transform.position;
            _visibleRotation = _owner.transform.rotation;
            _hasVisiblePose = true;
        }

        void Unbind()
        {
            UnsubscribePointerService();
            UnsubscribePlacementService();

            _target = null;
            _manager = null;
            _lastParent = null;
            _linkData = null;
            _hasRegisteredKey = false;
            _registeredKey = default;
            _hasSpawnExecutedKey = false;
            _spawnExecutedKey = default;
            _isHidden = false;
            _hasVisiblePose = false;
            _hasAppliedPresentationState = false;
            _appliedPresentationState = TraitRuntimePresentationState.None;
            _hiddenCommandsMutation.Clear();
            _visibleCommandsMutation.Clear();
        }

        void UnbindPointerOnly()
        {
            UnsubscribePointerService();
            _target = null;
            _pointerService = null;
            _manager = null;
            _lastParent = null;
        }

        void UnsubscribePointerService()
        {
            if (_pointerService != null)
                _pointerService.OnRightClicked -= HandleRightClicked;
        }

        void UnsubscribePlacementService()
        {
            if (_placementService != null)
                _placementService.OnPresentationStateChanged -= HandlePresentationStateChanged;

            _placementService = null;
        }

        static IVarStore CreateVars(IScopeNode scope)
        {
            var vars = new VarStore();
            var resolver = scope?.Resolver;
            if (resolver != null && resolver.TryResolve<IBlackboardService>(out var blackboard) && blackboard != null)
                blackboard.MergeInto(vars, overwrite: true);

            return vars;
        }

        static bool TryResolveRuntimeScope(RuntimeTraitMB owner, out RuntimeLifetimeScope? runtimeScope)
        {
            runtimeScope = null;
            if (owner == null)
                return false;

            if (!ScopeFeatureInstallerUtility.TryGetNearestScopeNode(owner, includeInactive: true, out var scope) || scope == null)
                return false;

            runtimeScope = scope as RuntimeLifetimeScope;
            return runtimeScope != null;
        }

        static bool TryResolveCommandRunner(IScopeNode scope, out ICommandRunner? runner)
        {
            runner = null;
            var resolver = scope?.Resolver;
            if (resolver != null &&
                resolver.TryResolve<ICommandRunner>(out runner) &&
                runner != null)
            {
                return true;
            }

            return scope.TryResolveInAncestors(out runner) && runner != null;
        }

        sealed class PresentationCommandMutationSlot
        {
            readonly List<CommandListMutationStep> _history = new();
            readonly CommandListData _runtimeView = new();
            readonly CommandListData _emptyBase = new();

            public bool Apply(CommandListMutationStep? mutation, ICommandListRuntimeMutationService? mutationService)
            {
                if (mutation == null)
                    return false;

                if (mutation.Operation == CommandListMutationOperation.ClearAll)
                {
                    _history.Clear();
                    _runtimeView.ClearRuntimeMutations();
                    return true;
                }

                if (mutation.RequiresCommands() && mutation.Commands == null)
                    return false;

                if (HasEquivalentMutation(mutation))
                    return true;

                mutationService?.Register(_runtimeView);
                _history.Add(new CommandListMutationStep
                {
                    Operation = mutation.Operation,
                    Commands = mutation.Commands,
                });
                return true;
            }

            public bool TryBuild(DynamicValue<CommandListData> source, IDynamicContext dynamicContext, out CommandListData? commands)
            {
                commands = null;

                var hasBaseCommands = source.TryGet(dynamicContext, out var resolvedBase) && resolvedBase != null;
                if (_history.Count == 0)
                {
                    if (!hasBaseCommands)
                        return false;

                    commands = resolvedBase;
                    return true;
                }

                _runtimeView.SetCommands(hasBaseCommands ? resolvedBase : _emptyBase);
                _runtimeView.ClearRuntimeMutations();
                for (var i = 0; i < _history.Count; i++)
                {
                    _runtimeView.ApplyRuntimeMutation(_history[i]);
                }

                commands = _runtimeView;
                return true;
            }

            public void Clear()
            {
                _history.Clear();
                _runtimeView.ClearRuntimeMutations();
            }

            bool HasEquivalentMutation(CommandListMutationStep mutation)
            {
                for (int i = 0; i < _history.Count; i++)
                {
                    var existing = _history[i];
                    if (existing.Operation != mutation.Operation)
                        continue;

                    if (!ReferenceEquals(existing.Commands, mutation.Commands))
                        continue;

                    return true;
                }

                return false;
            }
        }
    }
}
