#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Channel;
using Game.Commands.VNext;
using Game.Common;
using Game.DI;
using Game.Spawn;
using Game.Trait;
using Game.Vars.Generated;
using UnityEngine;
using VContainer;

namespace Game.UI
{
    internal sealed class TraitListChannelRuntime
    {
        readonly struct ResolvedState
        {
            public ResolvedState(
                TraitListChannelBinding binding,
                TraitListChannelPlayerPreset playerPreset,
                TraitListChannelLayoutPreset layoutPreset,
                TraitListChannelVisualizerPreset visualizerPreset,
                BaseRuntimeTemplateSO? runtimeTemplate)
            {
                Binding = binding;
                PlayerPreset = playerPreset;
                LayoutPreset = layoutPreset;
                VisualizerPreset = visualizerPreset;
                RuntimeTemplate = runtimeTemplate;
            }

            public TraitListChannelBinding Binding { get; }
            public TraitListChannelPlayerPreset PlayerPreset { get; }
            public TraitListChannelLayoutPreset LayoutPreset { get; }
            public TraitListChannelVisualizerPreset VisualizerPreset { get; }
            public BaseRuntimeTemplateSO? RuntimeTemplate { get; }
        }

        readonly struct OperationLockState
        {
            public OperationLockState(bool entered, int previousStamp, int currentStamp)
            {
                Entered = entered;
                PreviousStamp = previousStamp;
                CurrentStamp = currentStamp;
            }

            public bool Entered { get; }
            public int PreviousStamp { get; }
            public int CurrentStamp { get; }
        }

        readonly IScopeNode _owner;
        readonly TraitListChannelHubMB _mb;
        readonly TraitListChannelDefinition _definition;
        readonly SemaphoreSlim _mutex = new(1, 1);
        readonly Dictionary<string, TraitListChannelVisualInstance> _lookup =
            new(StringComparer.Ordinal);
        readonly List<TraitListChannelVisualInstance> _instances = new();

        readonly AsyncLocal<int> _operationContextStamp = new();

        ActorSourceResolveCache _holderHubSourceCache;
        ActorSourceResolveCache _layoutAreaSourceCache;
        ActorSourceResolveCache _fixedAnchorSourceCache;

        CancellationTokenSource? _lifecycleCts;
        TraitListChannelBindRequest _bindRequest = new();
        TraitListChannelBinding _resolvedBinding = new();
        TraitListChannelPlayerPreset _resolvedPlayerPreset = new();
        TraitListChannelLayoutPreset _resolvedLayoutPreset = new();
        TraitListChannelVisualizerPreset _resolvedVisualizerPreset = new();
        BaseRuntimeTemplateSO? _resolvedRuntimeTemplate;
        ITraitHolderService? _boundHolder;
        ITraitPlacementService? _placementService;
        IScopeNode? _activeScope;
        Transform? _listRoot;
        Transform? _layoutReferenceTransform;
        RectTransform? _layoutRectTransform;
        Canvas? _canvas;
        TraitListChannelEnvironmentKind _environmentKind;
        bool _hasBinding;
        bool _isBuilt;
        bool _isActive;
        bool _queueWorkerActive;
        bool _bindQueued;
        bool _bindQueueWorkerActive;
        TraitListChannelBindRequest _queuedBindRequest = new();
        bool _queuedBindRebuild;
        bool _refreshQueued;
        TraitListChannelRefreshMode _queuedRefreshMode;
        int _activeOperationStamp;
        int _operationStampSeed;

        public TraitListChannelRuntime(
            IScopeNode owner,
            TraitListChannelHubMB mb,
            TraitListChannelDefinition definition,
            string tag)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _mb = mb ?? throw new ArgumentNullException(nameof(mb));
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Tag = TraitListChannelRuntimeHelpers.NormalizeTag(tag);
        }

        public string Tag { get; }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            if (!ReferenceEquals(_owner, scope))
                return;

            _ = isReset;
            _activeScope = scope;
            _listRoot = _definition.ListRoot != null ? _definition.ListRoot : _mb.transform;
            _layoutReferenceTransform = _definition.LayoutRectTransform != null
                ? _definition.LayoutRectTransform
                : _listRoot;
            _layoutRectTransform = _layoutReferenceTransform as RectTransform;
            _environmentKind = TraitListChannelRuntimeHelpers.ResolveEnvironment(_listRoot, out _canvas);
            _lifecycleCts = new CancellationTokenSource();
            _isActive = true;
            _isBuilt = false;
            _hasBinding = false;

            if (!_definition.AutoBuild)
                return;

            UniTask.Void(async () =>
            {
                try
                {
                    await BindAsync(new TraitListChannelBindRequest(), rebuild: true, CancellationToken.None);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[TraitListChannel] Auto build failed. Tag='{Tag}' Message={ex.Message}");
                }
            });
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            if (!ReferenceEquals(_owner, scope))
                return;

            _ = scope;
            _ = isReset;

            _isActive = false;
            _refreshQueued = false;
            _queueWorkerActive = false;
            _bindQueued = false;
            _bindQueueWorkerActive = false;
            _queuedBindRequest = new TraitListChannelBindRequest();
            _queuedBindRebuild = false;

            if (_boundHolder != null)
                _boundHolder.OnTraitsChanged -= OnTraitsChanged;
            _boundHolder = null;

            if (_placementService != null)
                _placementService.OnPresentationStateChanged -= OnPlacementPresentationStateChanged;
            _placementService = null;

            if (_lifecycleCts != null)
            {
                _lifecycleCts.Cancel();
                _lifecycleCts.Dispose();
                _lifecycleCts = null;
            }

            UniTask.Void(async () =>
            {
                try
                {
                    await _mutex.WaitAsync();
                    try
                    {
                        await ClearSpawnedInstancesAsync(CancellationToken.None);
                    }
                    finally
                    {
                        _mutex.Release();
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[TraitListChannel] Release clear failed. Tag='{Tag}' Message={ex.Message}");
                }
            });

            _activeScope = null;
            _listRoot = null;
            _layoutReferenceTransform = null;
            _layoutRectTransform = null;
            _canvas = null;
            _holderHubSourceCache = default;
            _layoutAreaSourceCache = default;
            _fixedAnchorSourceCache = default;
            _resolvedRuntimeTemplate = null;
            _resolvedBinding = new TraitListChannelBinding();
            _resolvedPlayerPreset = new TraitListChannelPlayerPreset();
            _resolvedLayoutPreset = new TraitListChannelLayoutPreset();
            _resolvedVisualizerPreset = new TraitListChannelVisualizerPreset();
        }

        public bool TryGetBinding(out TraitListChannelBinding? binding)
        {
            if (_hasBinding)
            {
                binding = _resolvedBinding.Clone();
                return true;
            }

            var defaultBinding = _definition.DefaultBinding;
            if (defaultBinding == null)
            {
                binding = null;
                return false;
            }

            binding = defaultBinding.Clone();
            return true;
        }

        public async UniTask<bool> BindAsync(TraitListChannelBindRequest request, bool rebuild, CancellationToken ct)
        {
            using var linkedCts = CreateLinkedTokenSource(ct);
            var linkedToken = linkedCts?.Token ?? ct;

            if (IsReentrantOperationCall())
            {
                QueueBind(request, rebuild);
                return true;
            }

            var lockState = await TryEnterOperationMutexAsync(linkedToken, "Bind");
            if (!lockState.Entered)
                return false;

            try
            {
                if (!_isActive || _activeScope == null)
                    return false;

                _bindRequest = request?.Clone() ?? new TraitListChannelBindRequest();
                _hasBinding = true;
                if (!await ResolveCurrentStateAsync(linkedToken))
                    return false;

                if (!rebuild)
                    return true;

                return await RefreshResolvedStateAsync(TraitListChannelRefreshMode.FullRebuild, linkedToken);
            }
            finally
            {
                ExitOperationContext(lockState.PreviousStamp, lockState.CurrentStamp);
                _mutex.Release();
            }
        }

        public async UniTask<bool> RefreshAsync(TraitListChannelRefreshMode mode, CancellationToken ct)
        {
            using var linkedCts = CreateLinkedTokenSource(ct);
            var linkedToken = linkedCts?.Token ?? ct;

            if (IsReentrantOperationCall())
            {
                QueueRefresh(mode);
                return true;
            }

            var lockState = await TryEnterOperationMutexAsync(linkedToken, "Refresh");
            if (!lockState.Entered)
                return false;

            try
            {
                if (!_hasBinding || !_isActive)
                    return false;

                if (!await ResolveCurrentStateAsync(linkedToken))
                    return false;

                return await RefreshResolvedStateAsync(mode, linkedToken);
            }
            finally
            {
                ExitOperationContext(lockState.PreviousStamp, lockState.CurrentStamp);
                _mutex.Release();
            }
        }

        public async UniTask<bool> SetRangeAsync(bool useRange, TraitListChannelRange range, bool rebuild, CancellationToken ct)
        {
            using var linkedCts = CreateLinkedTokenSource(ct);
            var linkedToken = linkedCts?.Token ?? ct;

            var lockState = await TryEnterOperationMutexAsync(linkedToken, "SetRange");
            if (!lockState.Entered)
                return false;

            try
            {
                if (!_hasBinding)
                    return false;

                _bindRequest.OverrideRange = true;
                _bindRequest.UseRange = useRange;
                _bindRequest.Range = range;
                if (!await ResolveCurrentStateAsync(linkedToken))
                    return false;

                if (!rebuild)
                    return true;

                return await RefreshResolvedStateAsync(TraitListChannelRefreshMode.Incremental, linkedToken);
            }
            finally
            {
                ExitOperationContext(lockState.PreviousStamp, lockState.CurrentStamp);
                _mutex.Release();
            }
        }

        public async UniTask<bool> ClearAsync(bool keepBinding, CancellationToken ct)
        {
            using var linkedCts = CreateLinkedTokenSource(ct);
            var linkedToken = linkedCts?.Token ?? ct;

            var lockState = await TryEnterOperationMutexAsync(linkedToken, "Clear");
            if (!lockState.Entered)
                return false;

            try
            {
                await ClearSpawnedInstancesAsync(linkedToken);
                _isBuilt = false;

                if (!keepBinding)
                {
                    UnbindServices();
                    _hasBinding = false;
                    _bindRequest = new TraitListChannelBindRequest();
                    _resolvedBinding = new TraitListChannelBinding();
                    _resolvedRuntimeTemplate = null;
                }

                return true;
            }
            finally
            {
                ExitOperationContext(lockState.PreviousStamp, lockState.CurrentStamp);
                _mutex.Release();
            }
        }

        UniTask<bool> ResolveCurrentStateAsync(CancellationToken ct)
        {
            if (_activeScope == null)
                return UniTask.FromResult(false);

            var dynCtx = new SimpleDynamicContext(TraitListChannelRuntimeHelpers.ResolveVars(_activeScope), _activeScope);
            var binding = _definition.DefaultBinding != null ? _definition.DefaultBinding.Clone() : new TraitListChannelBinding();
            if (_bindRequest.OverrideHolderHubSource)
                binding.HolderHubSource = _bindRequest.HolderHubSource;
            if (_bindRequest.OverrideHolderKey)
                binding.HolderKey = _bindRequest.HolderKey;
            if (_bindRequest.OverrideRange)
            {
                binding.UseRange = _bindRequest.UseRange;
                binding.Range = _bindRequest.Range;
            }

            var playerPreset = _definition.PlayerPresetValue.GetOrDefault(dynCtx, new TraitListChannelPlayerPreset()).CreateRuntimeCopy();
            if (_bindRequest.OverridePlayerPreset)
                playerPreset = _bindRequest.PlayerPresetValue.GetOrDefault(dynCtx, new TraitListChannelPlayerPreset()).CreateRuntimeCopy();

            var layoutPreset = _definition.LayoutPresetValue.GetOrDefault(dynCtx, new TraitListChannelLayoutPreset()).CreateRuntimeCopy();
            if (_bindRequest.OverrideLayoutPreset)
                layoutPreset = _bindRequest.LayoutPresetValue.GetOrDefault(dynCtx, new TraitListChannelLayoutPreset()).CreateRuntimeCopy();

            var visualizerPreset = _definition.VisualizerPresetValue.GetOrDefault(dynCtx, new TraitListChannelVisualizerPreset()).CreateRuntimeCopy();
            if (_bindRequest.OverrideVisualizerPreset)
                visualizerPreset = _bindRequest.VisualizerPresetValue.GetOrDefault(dynCtx, new TraitListChannelVisualizerPreset()).CreateRuntimeCopy();

            BaseRuntimeTemplateSO? runtimeTemplate = null;
            if (!visualizerPreset.TryResolveRuntimeTemplate(dynCtx, out runtimeTemplate) || runtimeTemplate == null)
            {
                Debug.LogWarning($"[TraitListChannel] RuntimeTemplate could not be resolved. Tag='{Tag}'");
            }

            _resolvedBinding = binding;
            _resolvedPlayerPreset = playerPreset;
            _resolvedLayoutPreset = layoutPreset;
            _resolvedVisualizerPreset = visualizerPreset;
            _resolvedRuntimeTemplate = runtimeTemplate;

            var holderKey = binding.NormalizedHolderKey;
            if (string.IsNullOrEmpty(holderKey))
            {
                UnbindServices();
                return UniTask.FromResult(false);
            }

            var hubScope = ActorSourceFastResolver.ResolveCached(_activeScope, binding.HolderHubSource, ref _holderHubSourceCache);
            TraitListChannelRuntimeHelpers.EnsureScopeBuiltIfNeeded(hubScope);
            if (!TryResolveFromScopeOrAncestors<ITraitHolderHubService>(hubScope, out var hub) || hub == null)
            {
                UnbindServices();
                Debug.LogWarning($"[TraitListChannel] TraitHolderHubService is missing. Tag='{Tag}'");
                return UniTask.FromResult(false);
            }

            if (!hub.TryGetHolder(holderKey, out var holder) || holder == null)
            {
                UnbindServices();
                Debug.LogWarning($"[TraitListChannel] Holder was not found. Tag='{Tag}' HolderKey='{holderKey}'");
                return UniTask.FromResult(false);
            }

            ITraitPlacementService? placementService = null;
            if (playerPreset.HideVisiblePlacedTraits)
                TryResolveFromScopeOrAncestors(hubScope, out placementService);

            BindServices(holder, placementService);
            ct.ThrowIfCancellationRequested();
            return UniTask.FromResult(true);
        }

        async UniTask<bool> RefreshResolvedStateAsync(TraitListChannelRefreshMode mode, CancellationToken ct)
        {
            if (_boundHolder == null || _listRoot == null)
                return false;

            if (_resolvedRuntimeTemplate == null)
            {
                Debug.LogWarning($"[TraitListChannel] Refresh skipped because RuntimeTemplate is null. Tag='{Tag}'");
                return false;
            }

            var filteredTraits = CollectFilteredTraits(
                _boundHolder,
                _resolvedBinding.NormalizedHolderKey,
                _placementService,
                _resolvedPlayerPreset.HideVisiblePlacedTraits);

            if (!TraitListChannelLayoutUtility.TryBuildSlots(
                    filteredTraits,
                    Tag,
                    _resolvedBinding.NormalizedHolderKey,
                    _resolvedBinding.UseRange,
                    _resolvedBinding.Range,
                    _resolvedPlayerPreset.MergeDuplicateTraitDefinitions,
                    _resolvedLayoutPreset,
                out var slots,
                out var normalizedRange,
                out var error))
            {
                Debug.LogError($"[TraitListChannel] Slot build failed. Tag='{Tag}' Message={error}");
                return false;
            }

            _resolvedBinding.Range = normalizedRange;

            if (mode == TraitListChannelRefreshMode.FullRebuild || !_isBuilt)
            {
                await ClearSpawnedInstancesAsync(ct);
                await BuildFromSlotsAsync(slots, ct);
                _isBuilt = true;
                return true;
            }

            var slotLookup = new Dictionary<string, TraitListChannelSlot>(StringComparer.Ordinal);
            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (!string.IsNullOrEmpty(slot.DisplayKey) && !slotLookup.ContainsKey(slot.DisplayKey))
                    slotLookup.Add(slot.DisplayKey, slot);
            }

            var holderTraits = _boundHolder.Traits;
            var holderTraitCount = holderTraits?.Count ?? 0;
            var filteredTraitCount = filteredTraits.Count;

            if (mode != TraitListChannelRefreshMode.FullRebuild && holderTraitCount == 0)
            {
                if (_instances.Count > 0)
                    await ClearSpawnedInstancesAsync(ct);

                _isBuilt = true;
                return true;
            }

            var missingCount = 0;

            if (mode != TraitListChannelRefreshMode.LayoutOnly)
            {
                for (var i = 0; i < _instances.Count; i++)
                {
                    var instance = _instances[i];
                    if (instance == null || string.IsNullOrEmpty(instance.DisplayKey) || !slotLookup.ContainsKey(instance.DisplayKey))
                        missingCount++;
                }

                if (missingCount > 0)
                {
                    Debug.LogWarning(
                        $"[TraitListChannel][RefreshMismatch] tag='{Tag}' mode='{mode}' holder='{_resolvedBinding.NormalizedHolderKey}' " +
                        $"holderCount={holderTraitCount} filteredCount={filteredTraitCount} slotCount={slots.Count} instanceCount={_instances.Count} missingCount={missingCount} " +
                        $"useRange={_resolvedBinding.UseRange} range='{DescribeRange(_resolvedBinding.Range)}' normalizedRange='{DescribeRange(normalizedRange)}' " +
                        $"hideVisiblePlacedTraits={_resolvedPlayerPreset.HideVisiblePlacedTraits} runtimeTemplate='{_resolvedRuntimeTemplate?.name ?? "<null>"}' " +
                        $"filtered='{DescribeTraitList(filteredTraits)}' slots='{DescribeSlotList(slots)}'");
                }
            }

            if (mode != TraitListChannelRefreshMode.LayoutOnly)
            {
                for (var i = _instances.Count - 1; i >= 0; i--)
                {
                    ct.ThrowIfCancellationRequested();
                    var instance = _instances[i];
                    if (instance == null)
                    {
                        _instances.RemoveAt(i);
                        continue;
                    }

                    if (string.IsNullOrEmpty(instance.DisplayKey) || !slotLookup.ContainsKey(instance.DisplayKey))
                    {
                        var holderContainsTrait = TryFindTraitIndex(holderTraits, instance.Trait, out var holderIndex);
                        var filteredContainsTrait = TryFindTraitIndex(filteredTraits, instance.Trait, out var filteredIndex);
                        var slotContainsTrait = TryFindSlotIndex(slots, instance.Trait, out var slotIndex);
                        var removalReason = DescribeRemovalReason(
                            instance.Trait,
                            holderContainsTrait,
                            filteredContainsTrait,
                            slotContainsTrait,
                            _resolvedPlayerPreset.HideVisiblePlacedTraits,
                            _placementService,
                            _resolvedBinding.NormalizedHolderKey);

                        Debug.LogWarning(
                            $"[TraitListChannel][RemoveMissing] reason='{removalReason}' tag='{Tag}' trait='{DescribeTrait(instance.Trait)}' " +
                            $"root='{DescribeTransform(instance.Root)}' parent='{DescribeTransform(instance.Root.parent)}' " +
                            $"holderCount={holderTraitCount} filteredCount={filteredTraitCount} slotCount={slots.Count} instanceCount={_instances.Count} " +
                            $"useRange={_resolvedBinding.UseRange} range='{DescribeRange(_resolvedBinding.Range)}' normalizedRange='{DescribeRange(normalizedRange)}' " +
                            $"hideVisiblePlacedTraits={_resolvedPlayerPreset.HideVisiblePlacedTraits} runtimeTemplate='{_resolvedRuntimeTemplate?.name ?? "<null>"}' " +
                            $"holderContainsTrait={holderContainsTrait} holderIndex={holderIndex} filteredContainsTrait={filteredContainsTrait} filteredIndex={filteredIndex} " +
                            $"slotContainsTrait={slotContainsTrait} slotIndex={slotIndex} filtered='{DescribeTraitList(filteredTraits)}' slots='{DescribeSlotList(slots)}'");
                        await TraitListChannelRuntimeHelpers.ReleaseSpawnedInstanceAsync(instance.Root, instance.Scope, instance.Resolver);
                        _instances.RemoveAt(i);
                        if (!string.IsNullOrEmpty(instance.DisplayKey))
                            _lookup.Remove(instance.DisplayKey);
                    }
                }
            }

            var totalNewlySpawned = 0;
            if (mode != TraitListChannelRefreshMode.LayoutOnly)
            {
                for (var i = 0; i < slots.Count; i++)
                {
                    var slot = slots[i];
                    if (!string.IsNullOrEmpty(slot.DisplayKey) && !_lookup.ContainsKey(slot.DisplayKey))
                        totalNewlySpawned++;
                }
            }

            var initializedNewSpawned = 0;
            RecalculateSlotPositions(slots);
            for (var i = 0; i < slots.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var slot = slots[i];
                if (slot.Trait == null)
                    continue;

                if (string.IsNullOrEmpty(slot.DisplayKey) || !_lookup.TryGetValue(slot.DisplayKey, out var instance) || instance == null)
                {
                    if (mode == TraitListChannelRefreshMode.LayoutOnly)
                        continue;

                    var spawned = await SpawnRawAsync(slot, ct);
                    if (spawned == null)
                        continue;

                    _instances.Add(spawned);
                    _lookup[slot.DisplayKey] = spawned;
                    await InitializeSpawnedInstanceAsync(spawned, slot, ct);
                    initializedNewSpawned++;
                    await DelayBetweenNewSpawnsIfNeededAsync(initializedNewSpawned, totalNewlySpawned, ct);
                }
                else
                {
                    await RelayoutInstanceAsync(instance, slot, ct);
                }
            }

            SortInstancesByListIndex();
            return true;
        }

        async UniTask BuildFromSlotsAsync(List<TraitListChannelSlot> slots, CancellationToken ct)
        {
            if (slots == null || slots.Count == 0)
                return;

            RecalculateSlotPositions(slots);
            var initializedSpawned = 0;
            var totalSpawnCount = 0;
            for (var i = 0; i < slots.Count; i++)
            {
                if (slots[i].Trait != null)
                    totalSpawnCount++;
            }

            for (var i = 0; i < slots.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var slot = slots[i];
                if (slot.Trait == null)
                    continue;

                var spawned = await SpawnRawAsync(slot, ct);
                if (spawned == null)
                    continue;

                _instances.Add(spawned);
                _lookup[slot.DisplayKey] = spawned;
                await InitializeSpawnedInstanceAsync(spawned, slot, ct);
                initializedSpawned++;
                await DelayBetweenNewSpawnsIfNeededAsync(initializedSpawned, totalSpawnCount, ct);
            }

            SortInstancesByListIndex();
        }

        void RecalculateSlotPositions(List<TraitListChannelSlot> slots)
        {
            var itemSize = ResolvePlanningItemSize();
            var layoutRect = TransformGridSharedUtility.ResolveLayoutRect(
                _listRoot,
                _layoutReferenceTransform,
                _layoutRectTransform,
                _canvas,
                _activeScope,
                _environmentKind == TraitListChannelEnvironmentKind.ScreenUI
                    ? TransformGridEnvironmentKind.ScreenUI
                    : TransformGridEnvironmentKind.World,
                _resolvedLayoutPreset.RangeSourceMode,
                _resolvedLayoutPreset.AreaActorSource,
                ref _layoutAreaSourceCache,
                _resolvedLayoutPreset.AreaChannelTag);
            TraitListChannelLayoutUtility.RecalculateTargetPositions(
                slots,
                _resolvedLayoutPreset,
                layoutRect,
                itemSize);
        }

        async UniTask<TraitListChannelVisualInstance?> SpawnRawAsync(TraitListChannelSlot slot, CancellationToken ct)
        {
            if (_activeScope == null || _listRoot == null || _resolvedRuntimeTemplate == null)
                return null;

            if (!TryResolveFromScopeOrAncestors<ISceneSpawnerRegistry>(_activeScope, out var registry) || registry == null)
            {
                Debug.LogWarning($"[TraitListChannel] ISceneSpawnerRegistry is not available. Tag='{Tag}'");
                return null;
            }

            var spawner = registry.TryGet<IAsyncSpawnerService>(SpawnerKind.RuntimeUIElement, "");
            if (spawner == null)
            {
                Debug.LogWarning($"[TraitListChannel] RuntimeUIElement spawner is not available. Tag='{Tag}'");
                return null;
            }

            await UniTask.SwitchToMainThread();
            ct.ThrowIfCancellationRequested();

            var spawnParams = SpawnParams.ForRuntime(
                _resolvedRuntimeTemplate,
                Vector3.zero,
                Quaternion.identity,
                Vector3.one,
                identity: null,
                transformParent: _listRoot,
                lifetimeScopeParent: _activeScope,
                worldSpace: false,
                allowPooling: _resolvedVisualizerPreset.AllowPooling);

            IObjectResolver? resolver = null;
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
                Debug.LogError($"[TraitListChannel] Spawn failed. Tag='{Tag}' Message={ex.Message}");
                return null;
            }

            TraitListChannelRuntimeHelpers.ExtractSpawnedInfo(resolver, out var root, out var scopeNode);
            if (resolver == null || root == null || scopeNode == null)
            {
                await TraitListChannelRuntimeHelpers.ReleaseSpawnedInstanceAsync(root, scopeNode, resolver);
                Debug.LogError($"[TraitListChannel] Spawned instance is missing root or scope. Tag='{Tag}'");
                return null;
            }

            var instance = new TraitListChannelVisualInstance(slot.DisplayKey, slot.Trait, root, scopeNode, resolver);
            TransformGridSharedUtility.SetUiElementVisible(instance.Resolver, false);
            AttachDebugProbe(instance.Root, slot);
            return instance;
        }

        async UniTask InitializeSpawnedInstanceAsync(
            TraitListChannelVisualInstance instance,
            TraitListChannelSlot slot,
            CancellationToken ct)
        {
            instance.UpdateSlot(slot);
            var payload = BuildPayload(slot);
            var commandVars = ApplyPayloadToBlackboard(instance, payload);
            TransformGridSharedUtility.RefreshLayoutAndBounds(instance.Resolver);
            var startAnchor = ResolveSpawnAnchorLocalPosition(slot);
            var startLocal = TraitListChannelRuntimeHelpers.ResolvePlacementLocalPosition(
                instance,
                startAnchor,
                slot.ItemHorizontalAlignment,
                slot.ItemVerticalAlignment);
            var targetLocal = TraitListChannelRuntimeHelpers.ResolvePlacementLocalPosition(
                instance,
                slot.TargetLocalPosition,
                slot.ItemHorizontalAlignment,
                slot.ItemVerticalAlignment);
            TraitListChannelRuntimeHelpers.SetLocalPosition(instance, startLocal, _environmentKind);
            TransformGridSharedUtility.SetUiElementVisible(instance.Resolver, true);
            RunSpawnCommandsDetached(instance, slot, commandVars, ct);
            await AnimateInstanceAsync(instance, targetLocal, _resolvedLayoutPreset.SpawnMotion, ct);
        }

        void RunSpawnCommandsDetached(
            TraitListChannelVisualInstance instance,
            TraitListChannelSlot slot,
            IVarStore commandVars,
            CancellationToken ct)
        {
            UniTask.Void(async () =>
            {
                try
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    TryInvokeLtsInstantiated(instance);
                    await ExecuteSpawnCommandsAsync(slot, instance, commandVars, ct);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[TraitListChannel] Detached spawn command execution failed. Tag='{Tag}' Message={ex.Message}");
                }
            });
        }

        async UniTask RelayoutInstanceAsync(
            TraitListChannelVisualInstance instance,
            TraitListChannelSlot slot,
            CancellationToken ct)
        {
            instance.UpdateSlot(slot);
            AttachDebugProbe(instance.Root, slot);
            var payload = BuildPayload(slot);
            ApplyPayloadToBlackboard(instance, payload);
            TransformGridSharedUtility.RefreshLayoutAndBounds(instance.Resolver);
            var targetLocal = TraitListChannelRuntimeHelpers.ResolvePlacementLocalPosition(
                instance,
                slot.TargetLocalPosition,
                slot.ItemHorizontalAlignment,
                slot.ItemVerticalAlignment);
            await AnimateInstanceAsync(instance, targetLocal, _resolvedLayoutPreset.RelayoutMotion, ct);
        }

        async UniTask AnimateInstanceAsync(
            TraitListChannelVisualInstance instance,
            Vector3 targetLocal,
            TraitListChannelMotionPreset motion,
            CancellationToken ct)
        {
            if (motion == null || motion.DurationSeconds <= 0f)
            {
                TraitListChannelRuntimeHelpers.SetLocalPosition(instance, targetLocal, _environmentKind);
                return;
            }

            if (motion.UseTransformAnimation &&
                TraitListChannelRuntimeHelpers.TryResolveTransformAnimationPlayer(
                    instance,
                    motion.TransformAnimationChannelTag,
                    out var player) &&
                player != null)
            {
                var playerTarget = player.TargetTransform;
                if (playerTarget != null &&
                    (ReferenceEquals(playerTarget, instance.Root) || ReferenceEquals(playerTarget, instance.RootRect)))
                {
                    var motionTarget = TransformGridSharedUtility.ResolveMotionTargetPosition(
                        instance.RootRect,
                        targetLocal,
                        _environmentKind == TraitListChannelEnvironmentKind.ScreenUI
                            ? TransformGridEnvironmentKind.ScreenUI
                            : TransformGridEnvironmentKind.World);
                    var step = new TransformAnimationPresetStep
                    {
                        operation = _environmentKind == TraitListChannelEnvironmentKind.ScreenUI && instance.RootRect != null
                            ? TransformAnimationOperation.AnchoredPosition
                            : TransformAnimationOperation.LocalPosition,
                        duration = DynamicValueExtensions.FromLiteral(motion.DurationSeconds),
                        ease = motion.Ease,
                        relative = false,
                        fireAndForget = false,
                    };

                    if (motion.WaitForCompletion)
                    {
                        await player.PlayStepAsync(motionTarget, step);
                        TraitListChannelRuntimeHelpers.SetLocalPosition(instance, targetLocal, _environmentKind);
                        return;
                    }

                    UniTask.Void(async () =>
                    {
                        try
                        {
                            await player.PlayStepAsync(motionTarget, step);
                            TraitListChannelRuntimeHelpers.SetLocalPosition(instance, targetLocal, _environmentKind);
                        }
                        catch (OperationCanceledException)
                        {
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[TraitListChannel] TransformAnimation fallback triggered after channel failure. Tag='{Tag}' Message={ex.Message}");
                            await RunFallbackTweenAsync(instance, targetLocal, motion, CancellationToken.None);
                        }
                    });
                    return;
                }
            }

            await RunFallbackTweenAsync(instance, targetLocal, motion, ct);
        }

        async UniTask RunFallbackTweenAsync(
            TraitListChannelVisualInstance instance,
            Vector3 targetLocal,
            TraitListChannelMotionPreset motion,
            CancellationToken ct)
        {
            var start = instance.RootRect != null && _environmentKind == TraitListChannelEnvironmentKind.ScreenUI
                ? instance.RootRect.anchoredPosition3D
                : instance.Root.localPosition;
            var duration = motion.DurationSeconds;
            if (duration <= 0f)
            {
                TraitListChannelRuntimeHelpers.SetLocalPosition(instance, targetLocal, _environmentKind);
                return;
            }

            if (!motion.WaitForCompletion)
            {
                UniTask.Void(async () =>
                {
                    try
                    {
                        await RunFallbackTweenCoreAsync(instance, start, targetLocal, duration, motion.Ease, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[TraitListChannel] Detached fallback tween failed. Tag='{Tag}' Message={ex.Message}");
                    }
                });
                return;
            }

            await RunFallbackTweenCoreAsync(instance, start, targetLocal, duration, motion.Ease, ct);
        }

        async UniTask RunFallbackTweenCoreAsync(
            TraitListChannelVisualInstance instance,
            Vector3 start,
            Vector3 targetLocal,
            float duration,
            Ease ease,
            CancellationToken ct)
        {
            var elapsed = 0f;
            while (elapsed < duration)
            {
                ct.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = DOVirtual.EasedValue(0f, 1f, t, ease);
                var next = Vector3.LerpUnclamped(start, targetLocal, eased);
                TraitListChannelRuntimeHelpers.SetLocalPosition(instance, next, _environmentKind);
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            TraitListChannelRuntimeHelpers.SetLocalPosition(instance, targetLocal, _environmentKind);
        }

        async UniTask ExecuteSpawnCommandsAsync(
            TraitListChannelSlot slot,
            TraitListChannelVisualInstance instance,
            IVarStore commandVars,
            CancellationToken ct)
        {
            if (!TryResolveCommandRunner(instance, out var runner) || runner == null)
                return;

            var counterVarId = ResolveVarId(_resolvedVisualizerPreset.CounterVar, VarIds.GameLib.Base.CommandVar.i);
            if (counterVarId > 0)
                commandVars.TrySetVariant(counterVarId, DynamicVariant.FromInt(slot.ListIndex));

            var ctx = new CommandContext(instance.Scope, commandVars, runner, instance.Scope, CommandRunOptions.Default);
            if (_resolvedVisualizerPreset.WriteSpawnerToContext)
            {
                var targetScope = _activeScope ?? _owner;
                ctx.SetScope(ResolveContextSlotOrDefault(_resolvedVisualizerPreset.SpawnerContextSlot), targetScope);
            }

            try
            {
                if (_resolvedVisualizerPreset.SpawnCommands != null && _resolvedVisualizerPreset.SpawnCommands.Count > 0)
                    await runner.ExecuteListAsync(_resolvedVisualizerPreset.SpawnCommands, ctx, ct, CommandRunOptions.Default);

                if (TryResolveDefinitionCommands(slot.Trait?.Definition, out var byDefinition) &&
                    byDefinition != null &&
                    byDefinition.Count > 0)
                {
                    await runner.ExecuteListAsync(byDefinition, ctx, ct, CommandRunOptions.Default);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TraitListChannel] Spawn commands failed. Tag='{Tag}' Message={ex.Message}");
            }
        }

        VarStore BuildPayload(TraitListChannelSlot slot)
        {
            var payload = new VarStore(initialCapacity: 32);
            TraitHolderDataWriteUtility.WriteHolderDataToVarStore(_boundHolder, slot.HolderKey, payload, overwrite: true);

            var trait = slot.Trait;
            if (trait != null)
            {
                var definition = trait.Definition as TraitDefinitionSO;
                TraitDataWriteUtility.WriteTraitDataToVarStore(definition, trait.Context?.Vars, payload, overwrite: true);
                WriteTraitVisualSettings(payload, trait);
            }

            ApplyItemVars(payload, slot);
            ApplyRichTextKeys(payload, trait);
            return payload;
        }

        IVarStore ApplyPayloadToBlackboard(TraitListChannelVisualInstance instance, VarStore payload)
        {
            var commandVars = new VarStore(initialCapacity: 32);
            if (instance.Resolver.TryResolve<IBlackboardService>(out var blackboard) && blackboard != null)
            {
                payload.MergeInto(blackboard.LocalVars, overwrite: true);
                blackboard.LocalVars.MergeInto(commandVars, overwrite: true);
                return commandVars;
            }

            payload.MergeInto(commandVars, overwrite: true);
            return commandVars;
        }

        void ApplyItemVars(IVarStore vars, TraitListChannelSlot slot)
        {
            WriteVariant(vars, VarIds.GameLib.UI.TraitListChannel.Item.channelTag, DynamicVariant.FromString(slot.ChannelTag ?? string.Empty));
            WriteVariant(vars, VarIds.GameLib.UI.TraitListChannel.Item.holderKey, DynamicVariant.FromString(slot.HolderKey ?? string.Empty));
            WriteVariant(vars, VarIds.GameLib.UI.TraitListChannel.Item.listIndex, DynamicVariant.FromInt(slot.ListIndex));
            WriteVariant(vars, VarIds.GameLib.UI.TraitListChannel.Item.traitIndex, DynamicVariant.FromInt(slot.TraitIndex));
            WriteVariant(vars, VarIds.GameLib.UI.TraitListChannel.Item.row, DynamicVariant.FromInt(slot.Row));
            WriteVariant(vars, VarIds.GameLib.UI.TraitListChannel.Item.column, DynamicVariant.FromInt(slot.Column));
            WriteVariant(vars, VarIds.GameLib.UI.TraitListChannel.Item.rangeStart, DynamicVariant.FromInt(slot.RangeStart));
            WriteVariant(vars, VarIds.GameLib.UI.TraitListChannel.Item.rangeCount, DynamicVariant.FromInt(slot.RangeCount));
            WriteVariant(vars, VarIds.GameLib.UI.TraitListChannel.Item.duplicateCount, DynamicVariant.FromInt(Mathf.Max(1, slot.DuplicateCount)));

            var trait = slot.Trait;
            if (trait == null)
                return;

            var definition = trait.Definition;
            if (definition != null)
            {
                WriteVariant(
                    vars,
                    VarIds.GameLib.UI.TraitListChannel.Item.traitDefinitionId,
                    DynamicVariant.FromString(definition.DefinitionId ?? string.Empty));
                WriteManagedRef(vars, VarIds.GameLib.UI.TraitListChannel.Item.traitDefinitionRef, definition);
            }

            WriteVariant(
                vars,
                VarIds.GameLib.UI.TraitListChannel.Item.traitInstanceId,
                DynamicVariant.FromString(trait.InstanceId ?? string.Empty));
            WriteManagedRef(vars, VarIds.GameLib.UI.TraitListChannel.Item.traitInstanceRef, trait);
        }

        void ApplyRichTextKeys(IVarStore vars, ITraitInstance? trait)
        {
            if (vars == null || trait == null)
                return;

            if (_boundHolder is not TraitHolderService concreteHolder)
            {
                ClearRichTextKeys(vars);
                Debug.LogWarning(
                    $"[TraitListChannel] Rich text keys skipped because bound holder is not TraitHolderService. " +
                    $"Tag='{Tag}' HolderType='{_boundHolder?.GetType().FullName ?? "<null>"}'");
                return;
            }

            if (!concreteHolder.TryGetRichTextKeys(trait, out var descriptionKey, out var nameKey, out var diagnostic))
            {
                ClearRichTextKeys(vars);
                Debug.LogWarning(
                    $"[TraitListChannel] Failed to resolve rich text keys. Tag='{Tag}' Holder='{concreteHolder.HolderId}' " +
                    $"TraitDef='{trait.Definition?.DefinitionId ?? string.Empty}' InstanceId='{trait.InstanceId ?? string.Empty}' " +
                    $"Reason='{diagnostic.FailureReason}' HasRegistration={diagnostic.HasRegistration} " +
                    $"HasRefService={diagnostic.HasRichTextRefService}");
                return;
            }

            if (!string.IsNullOrEmpty(descriptionKey))
            {
                var writeRichText = vars.TrySetVariant(VarIds.GameLib.Base.RichText.descriptionKey, DynamicVariant.FromString(descriptionKey));
                var writeTraitElement = vars.TrySetVariant(VarIds.GameLib.Base.Trait.Element.descriptionKey, DynamicVariant.FromString(descriptionKey));
                if (!writeRichText || !writeTraitElement)
                {
                    Debug.LogWarning(
                        $"[TraitListChannel] Failed to write description rich text key to payload vars. " +
                        $"Tag='{Tag}' Holder='{concreteHolder.HolderId}' Key='{descriptionKey}' " +
                        $"WriteRichText={writeRichText} WriteTraitElement={writeTraitElement}");
                }
            }
            else
            {
                vars.TryUnset(VarIds.GameLib.Base.RichText.descriptionKey);
                vars.TryUnset(VarIds.GameLib.Base.Trait.Element.descriptionKey);
                Debug.LogWarning(
                    $"[TraitListChannel] Description rich text key is empty. Tag='{Tag}' Holder='{concreteHolder.HolderId}' " +
                    $"TraitDef='{trait.Definition?.DefinitionId ?? string.Empty}' InstanceId='{trait.InstanceId ?? string.Empty}' " +
                    $"Reason='{diagnostic.FailureReason}' HasRegistration={diagnostic.HasRegistration} HasRefService={diagnostic.HasRichTextRefService}");
            }

            if (!string.IsNullOrEmpty(nameKey))
            {
                var writeRichText = vars.TrySetVariant(VarIds.GameLib.Base.RichText.nameKey, DynamicVariant.FromString(nameKey));
                var writeTraitElement = vars.TrySetVariant(VarIds.GameLib.Base.Trait.Element.nameKey, DynamicVariant.FromString(nameKey));
                if (!writeRichText || !writeTraitElement)
                {
                    Debug.LogWarning(
                        $"[TraitListChannel] Failed to write name rich text key to payload vars. " +
                        $"Tag='{Tag}' Holder='{concreteHolder.HolderId}' Key='{nameKey}' " +
                        $"WriteRichText={writeRichText} WriteTraitElement={writeTraitElement}");
                }
            }
            else
            {
                vars.TryUnset(VarIds.GameLib.Base.RichText.nameKey);
                vars.TryUnset(VarIds.GameLib.Base.Trait.Element.nameKey);
                Debug.LogWarning(
                    $"[TraitListChannel] Name rich text key is empty. Tag='{Tag}' Holder='{concreteHolder.HolderId}' " +
                    $"TraitDef='{trait.Definition?.DefinitionId ?? string.Empty}' InstanceId='{trait.InstanceId ?? string.Empty}' " +
                    $"Reason='{diagnostic.FailureReason}' HasRegistration={diagnostic.HasRegistration} HasRefService={diagnostic.HasRichTextRefService}");
            }
        }

        static void ClearRichTextKeys(IVarStore vars)
        {
            vars.TryUnset(VarIds.GameLib.Base.RichText.descriptionKey);
            vars.TryUnset(VarIds.GameLib.Base.RichText.nameKey);
            vars.TryUnset(VarIds.GameLib.Base.Trait.Element.descriptionKey);
            vars.TryUnset(VarIds.GameLib.Base.Trait.Element.nameKey);
        }

        void WriteTraitVisualSettings(IVarStore vars, ITraitInstance trait)
        {
            if (trait?.Definition is not TraitDefinitionSO definition)
                return;

            var visualSettings = definition.VisualSettings;
            if (visualSettings == null)
                return;

            WriteManagedRef(vars, VarIds.GameLib.Base.VisualSetting.defaultAnim, visualSettings.DefaultAnim);
            WriteManagedRef(vars, VarIds.GameLib.Base.VisualSetting.focusAnim, visualSettings.FocusAnim);
            WriteManagedRef(vars, VarIds.GameLib.Base.VisualSetting.InteractAnim, visualSettings.InteractAnim);
            WriteManagedRef(vars, VarIds.GameLib.Base.VisualSetting.disableAnim, visualSettings.DisableAnim);
        }

        Vector3 ResolveSpawnAnchorLocalPosition(TraitListChannelSlot slot)
        {
            if (_resolvedLayoutPreset.SpawnAnchorMode == TraitListChannelSpawnAnchorMode.LayoutTarget)
                return slot.TargetLocalPosition + _resolvedLayoutPreset.SpawnOffset;

            var anchorLocal = Vector3.zero;
            if (_resolvedLayoutPreset.FixedAnchorTransform != null)
            {
                anchorLocal = ResolveLocalPointFromTransform(_resolvedLayoutPreset.FixedAnchorTransform);
            }
            else if (_resolvedLayoutPreset.UseFixedAnchorActorSource && _activeScope != null)
            {
                var scope = ActorSourceFastResolver.ResolveCached(
                    _activeScope,
                    _resolvedLayoutPreset.FixedAnchorActorSource,
                    ref _fixedAnchorSourceCache);
                var transform = scope?.Identity?.SelfTransform;
                if (transform != null)
                    anchorLocal = ResolveLocalPointFromTransform(transform);
            }

            return anchorLocal + _resolvedLayoutPreset.SpawnOffset;
        }

        async UniTask DelayBetweenNewSpawnsIfNeededAsync(int initializedCount, int totalSpawnCount, CancellationToken ct)
        {
            if (initializedCount >= totalSpawnCount || !_resolvedVisualizerPreset.DelayBetweenSpawns.HasSource || _activeScope == null)
                return;

            var delay = _resolvedVisualizerPreset.DelayBetweenSpawns.GetOrDefault(
                new SimpleDynamicContext(TraitListChannelRuntimeHelpers.ResolveVars(_activeScope), _activeScope),
                0f);
            if (delay <= 0f)
                return;

            await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: ct);
        }

        Vector3 ResolveLocalPointFromTransform(Transform anchor)
        {
            return TransformGridSharedUtility.ResolveLocalPointFromTransform(
                _listRoot,
                _layoutReferenceTransform,
                _layoutRectTransform,
                _canvas,
                anchor,
                _environmentKind == TraitListChannelEnvironmentKind.ScreenUI
                    ? TransformGridEnvironmentKind.ScreenUI
                    : TransformGridEnvironmentKind.World);
        }

        Vector2 ResolveLayoutItemSize(IReadOnlyList<TraitListChannelVisualInstance> instances)
        {
            if (_resolvedVisualizerPreset.SizeSource == TraitListChannelVisualizerSizeSource.Fixed)
                return _resolvedVisualizerPreset.FixedSize;

            if (instances == null || instances.Count == 0)
                return Vector2.zero;

            for (var i = 0; i < instances.Count; i++)
            {
                var instance = instances[i];
                if (instance == null)
                    continue;

                if (TryResolveLayoutElementSize(instance, out var size) && (size.x > 0f || size.y > 0f))
                    return size;
            }

            return Vector2.zero;
        }

        Vector2 ResolvePlanningItemSize()
        {
            var current = ResolveLayoutItemSize(_instances);
            if (current.x > 0f || current.y > 0f)
                return current;

            return ResolveTemplateLayoutItemSize();
        }

        bool TryResolveLayoutElementSize(TraitListChannelVisualInstance instance, out Vector2 size)
        {
            if (instance == null)
            {
                size = Vector2.zero;
                return false;
            }

            return TransformGridSharedUtility.TryResolveLayoutElementSize(
                instance.Resolver,
                instance.Root,
                instance.RootRect,
                (int)_resolvedVisualizerPreset.SizeSource,
                _resolvedVisualizerPreset.FixedSize,
                out size);
        }

        Vector2 ResolveTemplateLayoutItemSize()
        {
            if (_resolvedVisualizerPreset.SizeSource == TraitListChannelVisualizerSizeSource.Fixed)
                return _resolvedVisualizerPreset.FixedSize;

            var prefab = _resolvedRuntimeTemplate?.Prefab;
            if (prefab == null)
                return Vector2.zero;

            return TryGetTemplateRectSize(prefab.transform, out var rectSize) ? rectSize : Vector2.zero;
        }

        static bool TryGetTemplateRectSize(Transform root, out Vector2 size)
        {
            size = Vector2.zero;
            if (root == null)
                return false;

            var rect = root.GetComponentInChildren<RectTransform>(true);
            if (rect == null)
                return false;

            var rectSize = rect.rect.size;
            if (rectSize.x <= 0f && rectSize.y <= 0f)
                return false;

            size = rectSize;
            return true;
        }

        async UniTask ClearSpawnedInstancesAsync(CancellationToken ct)
        {
            for (var i = _instances.Count - 1; i >= 0; i--)
            {
                ct.ThrowIfCancellationRequested();
                var instance = _instances[i];
                if (instance == null)
                    continue;
                //Debug.Log(
                //    $"[TraitListChannel][Clear] tag='{Tag}' trait='{DescribeTrait(instance.Trait)}' root='{DescribeTransform(instance.Root)}' " +
                //    $"parent='{DescribeTransform(instance.Root.parent)}'");
                await TraitListChannelRuntimeHelpers.ReleaseSpawnedInstanceAsync(instance.Root, instance.Scope, instance.Resolver);
            }

            _instances.Clear();
            _lookup.Clear();
        }

        void BindServices(ITraitHolderService holder, ITraitPlacementService? placementService)
        {
            if (!ReferenceEquals(_boundHolder, holder))
            {
                if (_boundHolder != null)
                    _boundHolder.OnTraitsChanged -= OnTraitsChanged;
                _boundHolder = holder;
                _boundHolder.OnTraitsChanged += OnTraitsChanged;
            }

            if (!ReferenceEquals(_placementService, placementService))
            {
                if (_placementService != null)
                    _placementService.OnPresentationStateChanged -= OnPlacementPresentationStateChanged;
                _placementService = placementService;
                if (_placementService != null)
                    _placementService.OnPresentationStateChanged += OnPlacementPresentationStateChanged;
            }
        }

        void UnbindServices()
        {
            if (_boundHolder != null)
                _boundHolder.OnTraitsChanged -= OnTraitsChanged;
            _boundHolder = null;

            if (_placementService != null)
                _placementService.OnPresentationStateChanged -= OnPlacementPresentationStateChanged;
            _placementService = null;
        }

        void OnTraitsChanged(IReadOnlyList<ITraitInstance> traits)
        {
            var refreshMode = traits == null || traits.Count == 0
                ? TraitListChannelRefreshMode.FullRebuild
                : _resolvedPlayerPreset.RefreshMode;

            if (traits == null || traits.Count > 0)
            {
                QueueRefresh(refreshMode);
                return;
            }

            Debug.Log(
                $"[TraitListChannel][OnTraitsChanged] tag='{Tag}' holder='{_resolvedBinding.NormalizedHolderKey}' traitCount={traits?.Count ?? 0} " +
                $"refreshMode='{refreshMode}'");
            QueueRefresh(refreshMode);
        }

        void OnPlacementPresentationStateChanged(TraitRuntimePresentationChange change)
        {
            if (!_resolvedPlayerPreset.HideVisiblePlacedTraits)
                return;

            if (!string.Equals(_resolvedBinding.NormalizedHolderKey, change.HolderKey, StringComparison.Ordinal))
                return;
            QueueRefresh(TraitListChannelRefreshMode.Incremental);
        }

        void QueueRefresh(TraitListChannelRefreshMode mode)
        {
            if (!_isActive || !_hasBinding)
                return;
            _queuedRefreshMode = _refreshQueued ? CombineRefreshModes(_queuedRefreshMode, mode) : mode;
            _refreshQueued = true;
            if (_queueWorkerActive)
                return;

            _queueWorkerActive = true;
            UniTask.Void(async () =>
            {
                try
                {
                    while (_isActive && _hasBinding)
                    {
                        if (!_refreshQueued)
                            break;

                        if (IsReentrantOperationCall())
                        {
                            await UniTask.Yield(PlayerLoopTiming.Update, _lifecycleCts?.Token ?? CancellationToken.None);
                            continue;
                        }

                        var modeToRun = _queuedRefreshMode;
                        _refreshQueued = false;
                        var debounceFrames = Mathf.Max(0, _resolvedPlayerPreset.DebounceFrames);
                        if (debounceFrames > 0)
                            await UniTask.DelayFrame(debounceFrames, cancellationToken: _lifecycleCts?.Token ?? CancellationToken.None);

                        await RefreshAsync(modeToRun, CancellationToken.None);
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[TraitListChannel] Queued refresh failed. Tag='{Tag}' Message={ex.Message}");
                }
                finally
                {
                    _queueWorkerActive = false;
                }
            });
        }

        void QueueBind(TraitListChannelBindRequest request, bool rebuild)
        {
            if (!_isActive)
                return;

            _queuedBindRequest = request?.Clone() ?? new TraitListChannelBindRequest();
            _queuedBindRebuild |= rebuild;
            _bindQueued = true;

            if (_bindQueueWorkerActive)
                return;

            _bindQueueWorkerActive = true;
            UniTask.Void(async () =>
            {
                try
                {
                    while (_isActive)
                    {
                        if (!_bindQueued)
                            break;

                        if (IsReentrantOperationCall())
                        {
                            await UniTask.Yield(PlayerLoopTiming.Update, _lifecycleCts?.Token ?? CancellationToken.None);
                            continue;
                        }

                        var requestToRun = _queuedBindRequest.Clone();
                        var rebuildToRun = _queuedBindRebuild;
                        _bindQueued = false;
                        _queuedBindRebuild = false;

                        if (!await BindAsync(requestToRun, rebuildToRun, CancellationToken.None))
                            return;
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[TraitListChannel] Queued bind failed. Tag='{Tag}' Message={ex.Message}");
                }
                finally
                {
                    _bindQueueWorkerActive = false;
                }
            });
        }

        bool TryResolveDefinitionCommands(ITraitDefinition? definition, out CommandListData? commands)
        {
            commands = null;
            if (definition == null || _resolvedVisualizerPreset.ByDefinition == null)
                return false;

            var definitionId = definition.DefinitionId;
            var entries = _resolvedVisualizerPreset.ByDefinition;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry?.Definition == null)
                    continue;

                if (ReferenceEquals(entry.Definition, definition) || entry.Definition.DefinitionId == definitionId)
                {
                    commands = entry.Commands;
                    return true;
                }
            }

            return false;
        }

        bool TryResolveCommandRunner(TraitListChannelVisualInstance instance, out ICommandRunner? runner)
        {
            runner = null;
            if (instance.Resolver != null &&
                instance.Resolver.TryResolve<ICommandRunner>(out var localRunner) &&
                localRunner != null)
            {
                runner = localRunner;
                return true;
            }

            return TryResolveFromScopeOrAncestors(_activeScope, out runner) && runner != null;
        }

        static bool TryResolveFromScopeOrAncestors<T>(IScopeNode? scope, out T? value) where T : class
        {
            value = null;
            for (var current = scope; current != null; current = current.Parent)
            {
                var resolver = current.Resolver;
                if (resolver == null)
                    continue;
                if (resolver.TryResolve<T>(out var resolved) && resolved != null)
                {
                    value = resolved;
                    return true;
                }
            }

            return false;
        }

        static IReadOnlyList<ITraitInstance> CollectFilteredTraits(
            ITraitHolderService holder,
            string holderKey,
            ITraitPlacementService? placementService,
            bool hideVisiblePlacedTraits)
        {
            if (!hideVisiblePlacedTraits || placementService == null)
                return holder.Traits;

            var traits = holder.Traits;
            if (traits == null || traits.Count == 0)
                return Array.Empty<ITraitInstance>();

            var results = new List<ITraitInstance>(traits.Count);
            for (var i = 0; i < traits.Count; i++)
            {
                var trait = traits[i];
                if (trait == null)
                    continue;

                if (placementService.TryGetPresentationState(holderKey, trait.InstanceId, out var state) &&
                    state == TraitRuntimePresentationState.Visible)
                {
                    continue;
                }

                results.Add(trait);
            }

            return results;
        }

        void TryInvokeLtsInstantiated(TraitListChannelVisualInstance instance)
        {
            try
            {
                instance.Trait?.OnLtsInstantiated(instance.Scope);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TraitListChannel] Trait OnLtsInstantiated failed. Tag='{Tag}' Message={ex.Message}");
            }
        }

        void SortInstancesByListIndex()
        {
            if (_instances.Count <= 1)
                return;

            _instances.Sort(static (a, b) => a.ListIndex.CompareTo(b.ListIndex));
        }

        static TraitListChannelRefreshMode CombineRefreshModes(
            TraitListChannelRefreshMode a,
            TraitListChannelRefreshMode b)
        {
            return GetRefreshPriority(a) <= GetRefreshPriority(b) ? a : b;
        }

        static int GetRefreshPriority(TraitListChannelRefreshMode mode)
        {
            return mode switch
            {
                TraitListChannelRefreshMode.FullRebuild => 0,
                TraitListChannelRefreshMode.Incremental => 1,
                TraitListChannelRefreshMode.LayoutOnly => 2,
                _ => 3,
            };
        }

        void AttachDebugProbe(Transform root, TraitListChannelSlot slot)
        {
            if (root == null)
                return;

            if (!root.TryGetComponent<TraitListChannelRuntimeDebugProbeMB>(out var probe) || probe == null)
                probe = root.gameObject.AddComponent<TraitListChannelRuntimeDebugProbeMB>();

            probe.Configure(
                Tag,
                DescribeTrait(slot.Trait),
                DescribeTransform(_listRoot),
                DescribeTransform(root.parent));
        }

        static string DescribeTrait(ITraitInstance? trait)
        {
            if (trait == null)
                return "<null>";

            var definitionId = trait.Definition is TraitDefinitionSO definition
                ? definition.DefinitionId
                : trait.Definition?.GetType().Name ?? "<no-definition>";
            return $"{definitionId}/{trait.InstanceId}";
        }

        static string DescribeTraitList(IReadOnlyList<ITraitInstance>? traits, int maxCount = 8)
        {
            if (traits == null)
                return "<null>";

            if (traits.Count == 0)
                return "<empty>";

            var builder = new StringBuilder();
            var count = Mathf.Min(traits.Count, maxCount);
            for (var i = 0; i < count; i++)
            {
                if (i > 0)
                    builder.Append(", ");

                builder.Append(DescribeTrait(traits[i]));
            }

            if (traits.Count > count)
                builder.Append($", ... (+{traits.Count - count})");

            return builder.ToString();
        }

        static string DescribeSlotList(IReadOnlyList<TraitListChannelSlot>? slots, int maxCount = 8)
        {
            if (slots == null)
                return "<null>";

            if (slots.Count == 0)
                return "<empty>";

            var builder = new StringBuilder();
            var count = Mathf.Min(slots.Count, maxCount);
            for (var i = 0; i < count; i++)
            {
                if (i > 0)
                    builder.Append(", ");

                var slot = slots[i];
                builder.Append($"[{slot.ListIndex}:{DescribeTrait(slot.Trait)}@{slot.TraitIndex}");
                if (slot.DuplicateCount > 1)
                    builder.Append($"x{slot.DuplicateCount}");
                builder.Append("]");
            }

            if (slots.Count > count)
                builder.Append($", ... (+{slots.Count - count})");

            return builder.ToString();
        }

        static string DescribeRange(TraitListChannelRange range)
            => $"start={range.StartIndex},count={range.Count}";

        static bool TryFindTraitIndex(IReadOnlyList<ITraitInstance>? traits, ITraitInstance? target, out int index)
        {
            index = -1;
            if (traits == null || target == null)
                return false;

            for (var i = 0; i < traits.Count; i++)
            {
                if (ReferenceEquals(traits[i], target))
                {
                    index = i;
                    return true;
                }
            }

            return false;
        }

        static bool TryFindSlotIndex(IReadOnlyList<TraitListChannelSlot>? slots, ITraitInstance? target, out int index)
        {
            index = -1;
            if (slots == null || target == null)
                return false;

            for (var i = 0; i < slots.Count; i++)
            {
                if (ReferenceEquals(slots[i].Trait, target))
                {
                    index = i;
                    return true;
                }
            }

            return false;
        }

        static string DescribeRemovalReason(
            ITraitInstance? trait,
            bool holderContainsTrait,
            bool filteredContainsTrait,
            bool slotContainsTrait,
            bool hideVisiblePlacedTraits,
            ITraitPlacementService? placementService,
            string holderKey)
        {
            if (trait == null)
                return "traitNull";

            if (!holderContainsTrait)
                return "traitNotInHolder";

            if (filteredContainsTrait && !slotContainsTrait)
                return "filteredOutByRangeOrCapacity";

            if (!filteredContainsTrait && hideVisiblePlacedTraits && placementService != null &&
                placementService.TryGetPresentationState(holderKey, trait.InstanceId, out var state) &&
                state == TraitRuntimePresentationState.Visible)
            {
                return "filteredOutByVisiblePlacement";
            }

            if (!filteredContainsTrait)
                return "filteredOutBeforeSlotBuild";

            if (!slotContainsTrait)
                return "slotLookupMiss";

            return "unknownMismatch";
        }

        static string DescribeTransform(Transform? target)
        {
            if (target == null)
                return "<null>";

            return $"{target.name} path='{BuildPath(target)}'";
        }

        static string BuildPath(Transform target)
        {
            var current = target;
            var path = current.name;
            while (current.parent != null)
            {
                current = current.parent;
                path = $"{current.name}/{path}";
            }

            return path;
        }

        static void WriteVariant(IVarStore vars, int varId, DynamicVariant value)
        {
            if (vars == null || varId == 0)
                return;
            vars.TrySetVariant(varId, value);
        }

        static void WriteManagedRef(IVarStore vars, int varId, object? value)
        {
            if (vars == null || varId == 0 || value == null)
                return;
            vars.TrySetManagedRef(varId, value);
        }

        static int ResolveVarId(VarKeyRef key, int fallback)
        {
            if (!string.IsNullOrEmpty(key.StableKey) && VarIdResolver.TryResolve(key.StableKey, out var resolved) && resolved > 0)
                return resolved;

            return key.VarId > 0 ? key.VarId : fallback;
        }

        static CommandLtsSlot ResolveContextSlotOrDefault(CommandLtsSlot slot)
        {
            if (CommandLtsSlotUtility.IsContextSlot(slot))
                return slot;

            return CommandLtsSlot.ContextA;
        }

        CancellationTokenSource? CreateLinkedTokenSource(CancellationToken ct)
        {
            if (_lifecycleCts == null)
                return null;

            return CancellationTokenSource.CreateLinkedTokenSource(ct, _lifecycleCts.Token);
        }

        async UniTask<OperationLockState> TryEnterOperationMutexAsync(CancellationToken ct, string operationName)
        {
            if (IsReentrantOperationCall())
            {
                Debug.LogError(
                    $"[TraitListChannel] Re-entrant '{operationName}' was blocked to avoid deadlock. Tag='{Tag}' " +
                    "This typically happens when spawn commands invoke Bind/Refresh/SetRange/Clear on the same channel while it is rebuilding.");
                return new OperationLockState(false, 0, 0);
            }

            await _mutex.WaitAsync(ct);

            var previousStamp = _operationContextStamp.Value;
            var currentStamp = Interlocked.Increment(ref _operationStampSeed);
            _operationContextStamp.Value = currentStamp;
            Volatile.Write(ref _activeOperationStamp, currentStamp);
            return new OperationLockState(true, previousStamp, currentStamp);
        }

        void ExitOperationContext(int previousStamp, int currentStamp)
        {
            if (currentStamp != 0 && Volatile.Read(ref _activeOperationStamp) == currentStamp)
                Volatile.Write(ref _activeOperationStamp, 0);

            _operationContextStamp.Value = previousStamp;
        }

        bool IsReentrantOperationCall()
        {
            var activeStamp = Volatile.Read(ref _activeOperationStamp);
            var contextStamp = _operationContextStamp.Value;
            return activeStamp != 0 &&
                   contextStamp != 0 &&
                   activeStamp == contextStamp &&
                   _mutex.CurrentCount == 0;
        }
    }
}
