#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using Game.DI;
using Game.Project.Scene.Runtime;
using Game.SelectRuntime;
using Game.Spawn;
using Game.Vars.Generated;
using UnityEngine;
using VContainer;

namespace Game.Trait
{
    public interface ITraitPlacementService
    {
        event Action<TraitRuntimePresentationChange>? OnPresentationStateChanged;

        bool TryGetPresentationState(string holderKey, string traitKey, out TraitRuntimePresentationState state);
        bool TrySetPresentationState(string holderKey, string traitKey, TraitRuntimePresentationState state);
        bool TryGetRuntime(string holderKey, string traitKey, out RuntimeLifetimeScope? runtimeScope);
        UniTask<RuntimeLifetimeScope?> PlaceAsync(
            string holderKey,
            TraitElementSelector selector,
            IDynamicContext dynamicContext,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            Transform? transformParent,
            CancellationToken ct);

        bool NotifyRuntimeEnabled(TraitRuntimeLinkData? linkData, RuntimeTraitMB bridge);
        bool NotifyRuntimeDisabled(TraitRuntimeLinkData? linkData, RuntimeTraitMB bridge);
    }

    public sealed class TraitPlacementService :
        ITraitPlacementService,
        IScopeAcquireHandler,
        IScopeReleaseHandler
    {
        readonly IScopeNode _owner;
        readonly ITraitHolderHubService _hub;

        readonly Dictionary<TraitRuntimeLinkKey, RuntimeLifetimeScope> _runtimeByLink = new();
        readonly Dictionary<RuntimeLifetimeScope, TraitRuntimeLinkData> _linkByRuntime = new();
        readonly Dictionary<TraitRuntimeLinkKey, TraitRuntimePresentationState> _presentationByLink = new();
        readonly Dictionary<string, ITraitHolderService> _subscribedHolders = new(StringComparer.Ordinal);
        readonly List<TraitRuntimeLinkKey> _linkKeyBuffer = new();

        bool _isActive;

        public event Action<TraitRuntimePresentationChange>? OnPresentationStateChanged;

        public TraitPlacementService(IScopeNode owner, ITraitHolderHubService hub)
        {
            _owner = owner;
            _hub = hub;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _isActive = true;
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _isActive = false;

            var runtimes = new List<RuntimeLifetimeScope>(_linkByRuntime.Keys);
            for (int i = 0; i < runtimes.Count; i++)
            {
                var runtime = runtimes[i];
                if (runtime == null)
                    continue;

                ReleaseRuntimeInternal(runtime, clearPresentationState: true);
            }

            foreach (var pair in _subscribedHolders)
                pair.Value.OnTraitsChanged -= OnHolderTraitsChanged;

            _subscribedHolders.Clear();
            _runtimeByLink.Clear();
            _linkByRuntime.Clear();
            _presentationByLink.Clear();
            _linkKeyBuffer.Clear();
        }

        public bool TryGetPresentationState(string holderKey, string traitKey, out TraitRuntimePresentationState state)
        {
            var linkKey = CreateLinkKey(holderKey, traitKey);
            if (_presentationByLink.TryGetValue(linkKey, out state))
                return true;

            state = TraitRuntimePresentationState.None;
            return false;
        }

        public bool TrySetPresentationState(string holderKey, string traitKey, TraitRuntimePresentationState state)
        {
            var linkKey = CreateLinkKey(holderKey, traitKey);
            if (!_runtimeByLink.TryGetValue(linkKey, out var runtime) || runtime == null)
                return state == TraitRuntimePresentationState.None;

            if (state == TraitRuntimePresentationState.None)
            {
                ReleaseRuntimeInternal(runtime, clearPresentationState: true);
                return true;
            }

            runtime.TrySetVisible(state == TraitRuntimePresentationState.Visible);
            SetPresentationState(linkKey, state, runtime);
            return true;
        }

        public bool TryGetRuntime(string holderKey, string traitKey, out RuntimeLifetimeScope? runtimeScope)
        {
            runtimeScope = null;
            var linkKey = CreateLinkKey(holderKey, traitKey);
            if (!_runtimeByLink.TryGetValue(linkKey, out var runtime) || runtime == null)
                return false;

            runtimeScope = runtime;
            return true;
        }

        public async UniTask<RuntimeLifetimeScope?> PlaceAsync(
            string holderKey,
            TraitElementSelector selector,
            IDynamicContext dynamicContext,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            Transform? transformParent,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (!_isActive)
                return null;

            var normalizedHolderKey = Normalize(holderKey);
            if (string.IsNullOrEmpty(normalizedHolderKey))
                return null;

            if (!_hub.TryGetHolder(normalizedHolderKey, out var holder) || holder == null)
                return null;

            if (!selector.TryResolve(holder, dynamicContext, out var instance, out _) || instance == null)
                return null;

            var definition = instance.Definition as TraitDefinitionSO;
            if (definition == null)
                return null;

            var settings = definition.PlaceableSettings;
            if (settings == null || !settings.Enabled)
                return null;

            if (!settings.TryResolveRuntimeTemplate(dynamicContext, out var template) || template == null)
                return null;

            var linkData = BuildLinkData(normalizedHolderKey, instance);
            var linkKey = linkData.ToLinkKey();
            if (_runtimeByLink.TryGetValue(linkKey, out var existingRuntime) && existingRuntime != null)
            {
                existingRuntime.TrySetVisible(true);
                SetPresentationState(linkKey, TraitRuntimePresentationState.Visible, existingRuntime);
                WriteRuntimeBlackboard(existingRuntime, definition, linkData, TraitRuntimePresentationState.Visible);
                return existingRuntime;
            }

            var spawner = ResolveRuntimeSpawner(_owner);
            if (spawner == null)
                return null;

            var spawnParams = SpawnParams.ForRuntime(
                template,
                position,
                rotation,
                scale == default ? Vector3.one : scale,
                identity: null,
                transformParent: transformParent,
                lifetimeScopeParent: _owner,
                worldSpace: true,
                allowPooling: true);

            IObjectResolver? resolver;
            try
            {
                resolver = await spawner.SpawnAsync(spawnParams, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TraitPlacementService] Spawn failed: {ex.Message}");
                return null;
            }

            var runtimeScope = ResolveRuntimeScope(resolver);
            if (runtimeScope == null)
                return null;

            var runtimeBridge = ResolveOrCreateRuntimeBridge(runtimeScope, settings.ApplyRuntimeTraitMB);
            if (runtimeBridge == null)
            {
                ReleaseRuntimeInternal(runtimeScope, clearPresentationState: true);
                return null;
            }

            if (!TryApplyInitialPlacement(runtimeScope, runtimeBridge, position, rotation))
            {
                runtimeBridge.ClearLinkData();
                ReleaseRuntimeInternal(runtimeScope, clearPresentationState: true);
                return null;
            }

            runtimeBridge.SetLinkData(linkData);
            _runtimeByLink[linkKey] = runtimeScope;
            _linkByRuntime[runtimeScope] = linkData.Clone();
            EnsureHolderSubscription(normalizedHolderKey, holder);
            runtimeScope.TrySetVisible(true);
            SetPresentationState(linkKey, TraitRuntimePresentationState.Visible, runtimeScope);
            WriteRuntimeBlackboard(runtimeScope, definition, linkData, TraitRuntimePresentationState.Visible);
            return runtimeScope;
        }

        public bool NotifyRuntimeEnabled(TraitRuntimeLinkData? linkData, RuntimeTraitMB bridge)
        {
            if (!_isActive || linkData == null || bridge == null)
                return false;

            var runtimeScope = ResolveRuntimeScopeFromBridge(bridge);
            if (runtimeScope == null)
                return false;

            var normalizedHolderKey = Normalize(linkData.HolderKey);
            var normalizedTraitKey = Normalize(linkData.TraitKey);
            if (string.IsNullOrEmpty(normalizedHolderKey) || string.IsNullOrEmpty(normalizedTraitKey))
                return false;

            var normalizedLink = linkData.Clone();
            normalizedLink.HolderKey = normalizedHolderKey;
            normalizedLink.TraitKey = normalizedTraitKey;

            var linkKey = normalizedLink.ToLinkKey();
            _runtimeByLink[linkKey] = runtimeScope;
            _linkByRuntime[runtimeScope] = normalizedLink;
            runtimeScope.TrySetVisible(runtimeScope.IsVisible);
            SetPresentationState(
                linkKey,
                runtimeScope.IsVisible ? TraitRuntimePresentationState.Visible : TraitRuntimePresentationState.Hidden,
                runtimeScope);

            if (_hub.TryGetHolder(normalizedHolderKey, out var holder) && holder != null)
                EnsureHolderSubscription(normalizedHolderKey, holder);

            return true;
        }

        public bool NotifyRuntimeDisabled(TraitRuntimeLinkData? linkData, RuntimeTraitMB bridge)
        {
            if (linkData == null || bridge == null)
                return false;

            var runtimeScope = ResolveRuntimeScopeFromBridge(bridge);
            if (runtimeScope != null)
                return ReleaseRuntimeInternal(runtimeScope, clearPresentationState: true);

            var linkKey = linkData.ToLinkKey();
            var removed = _runtimeByLink.Remove(linkKey);
            RemovePresentationState(linkKey);
            return removed;
        }

        void EnsureHolderSubscription(string holderKey, ITraitHolderService holder)
        {
            if (_subscribedHolders.ContainsKey(holderKey))
                return;

            holder.OnTraitsChanged += OnHolderTraitsChanged;
            _subscribedHolders.Add(holderKey, holder);
        }

        void OnHolderTraitsChanged(IReadOnlyList<ITraitInstance> traits)
        {
            _linkKeyBuffer.Clear();
            foreach (var pair in _runtimeByLink)
                _linkKeyBuffer.Add(pair.Key);

            for (int i = 0; i < _linkKeyBuffer.Count; i++)
            {
                var linkKey = _linkKeyBuffer[i];
                var exists = false;
                for (int j = 0; j < traits.Count; j++)
                {
                    var trait = traits[j];
                    if (trait == null)
                        continue;

                    if (!string.Equals(trait.InstanceId, linkKey.TraitKey, StringComparison.Ordinal))
                        continue;

                    exists = true;
                    break;
                }

                if (exists)
                    continue;

                if (_runtimeByLink.TryGetValue(linkKey, out var runtime) && runtime != null)
                    ReleaseRuntimeInternal(runtime, clearPresentationState: true);
            }
        }

        bool ReleaseRuntimeInternal(RuntimeLifetimeScope runtime, bool clearPresentationState)
        {
            if (runtime == null)
                return false;

            TraitRuntimeLinkData? linkData = null;
            if (_linkByRuntime.TryGetValue(runtime, out var existingLink))
                linkData = existingLink;

            TraitRuntimeLinkKey? linkKey = linkData?.ToLinkKey();
            if (linkKey.HasValue)
            {
                _runtimeByLink.Remove(linkKey.Value);
                if (clearPresentationState)
                    RemovePresentationState(linkKey.Value);
            }

            _linkByRuntime.Remove(runtime);

            var bridge = runtime.GetComponentInChildren<RuntimeTraitMB>(true);
            if (bridge != null)
                bridge.ClearLinkData();

            if (TryResolveRuntimePool(runtime, out var pool) && pool != null)
            {
                pool.Release(runtime);
                return true;
            }

            runtime.TrySetActive(false);
            return true;
        }

        void SetPresentationState(
            TraitRuntimeLinkKey linkKey,
            TraitRuntimePresentationState newState,
            RuntimeLifetimeScope? runtime)
        {
            var previousState = _presentationByLink.TryGetValue(linkKey, out var current)
                ? current
                : TraitRuntimePresentationState.None;
            if (previousState == newState)
            {
                if (runtime != null && _linkByRuntime.TryGetValue(runtime, out var existingLink))
                    WritePresentationState(runtime, existingLink, newState);
                return;
            }

            _presentationByLink[linkKey] = newState;
            if (runtime != null && _linkByRuntime.TryGetValue(runtime, out var linkData))
                WritePresentationState(runtime, linkData, newState);

            OnPresentationStateChanged?.Invoke(new TraitRuntimePresentationChange(
                linkKey.HolderKey,
                linkKey.TraitKey,
                previousState,
                newState));
        }

        void RemovePresentationState(TraitRuntimeLinkKey linkKey)
        {
            var previousState = _presentationByLink.TryGetValue(linkKey, out var current)
                ? current
                : TraitRuntimePresentationState.None;

            _presentationByLink.Remove(linkKey);
            if (previousState == TraitRuntimePresentationState.None)
                return;

            OnPresentationStateChanged?.Invoke(new TraitRuntimePresentationChange(
                linkKey.HolderKey,
                linkKey.TraitKey,
                previousState,
                TraitRuntimePresentationState.None));
        }

        void WriteRuntimeBlackboard(
            RuntimeLifetimeScope runtime,
            TraitDefinitionSO definition,
            TraitRuntimeLinkData linkData,
            TraitRuntimePresentationState presentationState)
        {
            if (runtime == null || runtime.Resolver == null)
                return;

            if (!runtime.Resolver.TryResolve<IBlackboardService>(out var blackboard) || blackboard == null)
                return;

            var traitContext = new TraitInstanceContext(runtime);
            definition.CreateInstance(traitContext);
            traitContext.Vars.MergeInto(blackboard.LocalVars, overwrite: true);
            blackboard.LocalVars.TrySetVariant(VarIds.GameLib.Base.Trait.Element.instanceId, DynamicVariant.FromString(linkData.TraitKey));
            blackboard.LocalVars.TrySetVariant(VarIds.GameLib.Base.Trait.Element.definitionId, DynamicVariant.FromString(linkData.TraitDefinitionId));
            TraitRuntimeLinkVarKeys.WriteLinkData(blackboard.LocalVars, linkData, presentationState);
        }

        void WritePresentationState(RuntimeLifetimeScope runtime, TraitRuntimeLinkData linkData, TraitRuntimePresentationState state)
        {
            if (runtime == null || runtime.Resolver == null)
                return;

            if (!runtime.Resolver.TryResolve<IBlackboardService>(out var blackboard) || blackboard == null)
                return;

            TraitRuntimeLinkVarKeys.WriteLinkData(blackboard.LocalVars, linkData, state);
        }

        bool TryApplyInitialPlacement(
            RuntimeLifetimeScope runtimeScope,
            RuntimeTraitMB bridge,
            Vector3 requestedPosition,
            Quaternion requestedRotation)
        {
            if (runtimeScope == null || bridge == null)
                return false;

            var editor = runtimeScope.GetComponentInChildren<UserMoveRotateRuntimeMB>(true);
            if (editor == null)
                return true;

            var request = UserMoveRotateValidationRequest.Create(editor, runtimeScope);
            if (!request.IsValid)
                return true;

            if (UserMoveRotateValidationUtility.IsValidPose(request, requestedPosition, requestedRotation))
                return true;

            if (!UserMoveRotateValidationUtility.TryFindNearestValidPose(
                    request,
                    requestedPosition,
                    requestedRotation,
                    out var correctedPosition,
                    out var correctedRotation))
            {
                return false;
            }

            runtimeScope.transform.SetPositionAndRotation(correctedPosition, correctedRotation);
            return true;
        }

        TraitRuntimeLinkData BuildLinkData(string holderKey, ITraitInstance instance)
        {
            return new TraitRuntimeLinkData
            {
                SourceScopeKind = _owner.Kind,
                SourceScopeId = _owner.Identity?.Id ?? string.Empty,
                SourceScopeCategory = _owner.Identity?.Category ?? string.Empty,
                HolderKey = holderKey,
                TraitKey = instance.InstanceId ?? string.Empty,
                TraitDefinitionId = instance.Definition.DefinitionId ?? string.Empty,
            };
        }

        TraitRuntimeLinkKey CreateLinkKey(string holderKey, string traitKey)
        {
            return new TraitRuntimeLinkKey(
                _owner.Kind,
                _owner.Identity?.Id ?? string.Empty,
                Normalize(holderKey),
                Normalize(traitKey));
        }

        static string Normalize(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        static IAsyncSpawnerService? ResolveRuntimeSpawner(IScopeNode? scope)
        {
            var current = scope;
            while (current != null)
            {
                var resolver = current.Resolver;
                if (resolver != null &&
                    resolver.TryResolve<ISceneSpawnerRegistry>(out var registry) &&
                    registry != null)
                {
                    var spawner = registry.TryGet<IAsyncSpawnerService>(SpawnerKind.RuntimeEntity, string.Empty);
                    if (spawner != null)
                        return spawner;
                }

                current = current.Parent;
            }

            return null;
        }

        static RuntimeLifetimeScope? ResolveRuntimeScope(IObjectResolver? resolver)
        {
            if (resolver == null)
                return null;

            if (resolver.TryResolve<RuntimeLifetimeScope>(out var runtimeScope) && runtimeScope != null)
                return runtimeScope;

            if (resolver.TryResolve<IScopeNode>(out var scopeNode) && scopeNode is RuntimeLifetimeScope typedRuntime)
                return typedRuntime;

            return null;
        }

        static RuntimeLifetimeScope? ResolveRuntimeScopeFromBridge(RuntimeTraitMB bridge)
        {
            if (bridge == null)
                return null;

            if (!ScopeFeatureInstallerUtility.TryGetNearestScopeNode(bridge, includeInactive: true, out var scope) || scope == null)
                return null;

            return scope as RuntimeLifetimeScope;
        }

        static RuntimeTraitMB? ResolveOrCreateRuntimeBridge(RuntimeLifetimeScope runtimeScope, bool applyRuntimeTraitMb)
        {
            if (runtimeScope == null)
                return null;

            var bridge = runtimeScope.GetComponentInChildren<RuntimeTraitMB>(true);
            if (bridge != null)
                return bridge;

            if (!applyRuntimeTraitMb)
                return null;

            return runtimeScope.gameObject.AddComponent<RuntimeTraitMB>();
        }

        static bool TryResolveRuntimePool(IScopeNode? scope, out IRuntimeLifetimeScopePool? pool)
        {
            var current = scope;
            while (current != null)
            {
                var resolver = current.Resolver;
                if (resolver != null && resolver.TryResolve<IRuntimeLifetimeScopePool>(out var resolved) && resolved != null)
                {
                    pool = resolved;
                    return true;
                }

                current = current.Parent;
            }

            pool = null;
            return false;
        }
    }
}
