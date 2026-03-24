#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using Game.Commands.VNext;
using Game.Common;
using Game.SelectRuntime;
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

    public sealed class RuntimeTraitPresentationBridgeService :
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
                return;
            }

            var currentKey = currentLink.ToLinkKey();
            if (_hasRegisteredKey && _registeredKey.Equals(currentKey))
                return;

            _linkData = currentLink;
            _registeredKey = currentKey;
            _hasRegisteredKey = true;

            UnsubscribePlacementService();
            if (TraitPlacementScopeResolver.TryResolvePlacementService(_linkData, out var placementService) && placementService != null)
            {
                _placementService = placementService;
                _placementService.OnPresentationStateChanged += HandlePresentationStateChanged;

                if (_placementService.TryGetPresentationState(_linkData.HolderKey, _linkData.TraitKey, out var state))
                    ApplyPresentationState(state);
            }
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

            if (state == TraitRuntimePresentationState.Hidden)
            {
                if (!_isHidden)
                {
                    CaptureVisiblePose();
                    runtimeScope.transform.position = _visiblePosition + HiddenOffset;
                    runtimeScope.transform.rotation = _visibleRotation;
                    _isHidden = true;
                    ExecutePresentationCommands(runtimeScope, _owner.OnHiddenCommands, "Hidden");
                }

                runtimeScope.TrySetVisible(false);
                return;
            }

            if (state == TraitRuntimePresentationState.Visible)
            {
                var wasHidden = _isHidden;
                if (_isHidden && _hasVisiblePose)
                {
                    runtimeScope.transform.SetPositionAndRotation(_visiblePosition, _visibleRotation);
                }

                runtimeScope.TrySetVisible(true);
                _isHidden = false;
                if (wasHidden)
                    ExecutePresentationCommands(runtimeScope, _owner.OnVisibleCommands, "Visible");
            }
        }

        void ExecutePresentationCommands(RuntimeLifetimeScope scope, DynamicValue<CommandListData> commandSource, string eventName)
        {
            if (scope == null)
                return;

            var resolver = scope.Resolver;
            if (resolver == null)
                return;

            if (!resolver.TryResolve(out ICommandRunner? runner) || runner == null)
                return;

            var vars = CreateVars(scope);
            var dynamicContext = new SimpleDynamicContext(vars, scope);
            if (!commandSource.TryGet(dynamicContext, out var commands) || commands == null || commands.Count == 0)
                return;

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
            _isHidden = false;
            _hasVisiblePose = false;
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
    }
}
