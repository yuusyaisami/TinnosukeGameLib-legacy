#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
        readonly Dictionary<ITraitInstance, TraitListChannelVisualInstance> _lookup =
            new(global::Game.ReferenceEqualityComparer<ITraitInstance>.Instance);
        readonly List<TraitListChannelVisualInstance> _instances = new();

        readonly AsyncLocal<int> _operationContextStamp = new();

        ActorSourceResolveCache _holderHubSourceCache;
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
        RectTransform? _layoutRectTransform;
        Canvas? _canvas;
        TraitListChannelEnvironmentKind _environmentKind;
        bool _hasBinding;
        bool _isBuilt;
        bool _isActive;
        bool _queueWorkerActive;
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
            _ = isReset;
            _activeScope = scope;
            _listRoot = _definition.ListRoot != null ? _definition.ListRoot : _mb.transform;
            _layoutRectTransform = _definition.LayoutRectTransform != null
                ? _definition.LayoutRectTransform
                : _listRoot as RectTransform;
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
            _ = scope;
            _ = isReset;

            _isActive = false;
            _refreshQueued = false;
            _queueWorkerActive = false;

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
            _layoutRectTransform = null;
            _canvas = null;
            _holderHubSourceCache = default;
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

        async UniTask<bool> ResolveCurrentStateAsync(CancellationToken ct)
        {
            if (_activeScope == null)
                return false;

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
                return false;
            }

            var hubScope = ActorSourceFastResolver.ResolveCached(_activeScope, binding.HolderHubSource, ref _holderHubSourceCache);
            TraitListChannelRuntimeHelpers.EnsureScopeBuiltIfNeeded(hubScope);
            if (!TryResolveFromScopeOrAncestors<ITraitHolderHubService>(hubScope, out var hub) || hub == null)
            {
                UnbindServices();
                Debug.LogWarning($"[TraitListChannel] TraitHolderHubService is missing. Tag='{Tag}'");
                return false;
            }

            if (!hub.TryGetHolder(holderKey, out var holder) || holder == null)
            {
                UnbindServices();
                Debug.LogWarning($"[TraitListChannel] Holder was not found. Tag='{Tag}' HolderKey='{holderKey}'");
                return false;
            }

            ITraitPlacementService? placementService = null;
            if (playerPreset.HideVisiblePlacedTraits)
                TryResolveFromScopeOrAncestors(hubScope, out placementService);

            BindServices(holder, placementService);
            ct.ThrowIfCancellationRequested();
            return true;
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

            var newlySpawned = new HashSet<ITraitInstance>(global::Game.ReferenceEqualityComparer<ITraitInstance>.Instance);
            var slotLookup = new Dictionary<ITraitInstance, TraitListChannelSlot>(global::Game.ReferenceEqualityComparer<ITraitInstance>.Instance);
            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot.Trait != null && !slotLookup.ContainsKey(slot.Trait))
                    slotLookup.Add(slot.Trait, slot);
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

                    if (instance.Trait == null || !slotLookup.ContainsKey(instance.Trait))
                    {
                        await TraitListChannelRuntimeHelpers.ReleaseSpawnedInstanceAsync(instance.Root, instance.Scope, instance.Resolver);
                        _instances.RemoveAt(i);
                        if (instance.Trait != null)
                            _lookup.Remove(instance.Trait);
                    }
                }

                for (var i = 0; i < slots.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var slot = slots[i];
                    if (slot.Trait == null || _lookup.ContainsKey(slot.Trait))
                        continue;

                    var spawned = await SpawnRawAsync(slot, ct);
                    if (spawned == null)
                        continue;

                    _instances.Add(spawned);
                    _lookup[slot.Trait] = spawned;
                    newlySpawned.Add(slot.Trait);
                }
            }

            RecalculateSlotPositions(slots);
            for (var i = 0; i < slots.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var slot = slots[i];
                if (slot.Trait == null || !_lookup.TryGetValue(slot.Trait, out var instance) || instance == null)
                    continue;

                if (newlySpawned.Contains(slot.Trait))
                    await InitializeSpawnedInstanceAsync(instance, slot, ct);
                else
                    await RelayoutInstanceAsync(instance, slot, ct);
            }

            SortInstancesByListIndex();
            return true;
        }

        async UniTask BuildFromSlotsAsync(List<TraitListChannelSlot> slots, CancellationToken ct)
        {
            if (slots == null || slots.Count == 0)
                return;

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
                _lookup[slot.Trait] = spawned;
            }

            RecalculateSlotPositions(slots);
            for (var i = 0; i < slots.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var slot = slots[i];
                if (slot.Trait == null || !_lookup.TryGetValue(slot.Trait, out var instance) || instance == null)
                    continue;

                await InitializeSpawnedInstanceAsync(instance, slot, ct);
            }

            SortInstancesByListIndex();
        }

        void RecalculateSlotPositions(List<TraitListChannelSlot> slots)
        {
            var itemSize = ResolveLayoutItemSize(_instances);
            TraitListChannelLayoutUtility.RecalculateTargetPositions(
                slots,
                _resolvedLayoutPreset,
                _layoutRectTransform,
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

            return new TraitListChannelVisualInstance(slot.Trait, root, scopeNode, resolver);
        }

        async UniTask InitializeSpawnedInstanceAsync(
            TraitListChannelVisualInstance instance,
            TraitListChannelSlot slot,
            CancellationToken ct)
        {
            instance.UpdateSlot(slot);
            var payload = BuildPayload(slot);
            var commandVars = ApplyPayloadToBlackboard(instance, payload);
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
            TryInvokeLtsInstantiated(instance);
            await ExecuteSpawnCommandsAsync(slot, instance, commandVars, ct);
            await AnimateInstanceAsync(instance, targetLocal, _resolvedLayoutPreset.SpawnMotion, ct);
        }

        async UniTask RelayoutInstanceAsync(
            TraitListChannelVisualInstance instance,
            TraitListChannelSlot slot,
            CancellationToken ct)
        {
            instance.UpdateSlot(slot);
            var payload = BuildPayload(slot);
            ApplyPayloadToBlackboard(instance, payload);
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
                        await player.PlayStepAsync(targetLocal, step);
                        TraitListChannelRuntimeHelpers.SetLocalPosition(instance, targetLocal, _environmentKind);
                        return;
                    }

                    UniTask.Void(async () =>
                    {
                        try
                        {
                            await player.PlayStepAsync(targetLocal, step);
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

            var ctx = new CommandContext(instance.Scope, commandVars, runner, instance.Scope, CommandRunOptions.Default);
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
                return;

            if (!concreteHolder.TryGetRichTextKeys(trait, out var descriptionKey, out var nameKey, out _))
                return;

            if (!string.IsNullOrEmpty(descriptionKey))
                vars.TrySetVariant(VarIds.GameLib.Base.RichText.descriptionKey, DynamicVariant.FromString(descriptionKey));
            if (!string.IsNullOrEmpty(nameKey))
                vars.TrySetVariant(VarIds.GameLib.Base.RichText.nameKey, DynamicVariant.FromString(nameKey));
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

        Vector3 ResolveLocalPointFromTransform(Transform anchor)
        {
            if (_listRoot == null || anchor == null)
                return Vector3.zero;

            if (_environmentKind == TraitListChannelEnvironmentKind.ScreenUI &&
                _layoutRectTransform != null)
            {
                var camera = _canvas != null && _canvas.renderMode == RenderMode.ScreenSpaceCamera
                    ? _canvas.worldCamera
                    : null;
                var screenPoint = RectTransformUtility.WorldToScreenPoint(camera, anchor.position);
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        _layoutRectTransform,
                        screenPoint,
                        camera,
                        out var localPoint))
                {
                    return new Vector3(localPoint.x, localPoint.y, 0f);
                }
            }

            return _listRoot.InverseTransformPoint(anchor.position);
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

        bool TryResolveLayoutElementSize(TraitListChannelVisualInstance instance, out Vector2 size)
        {
            size = Vector2.zero;
            if (instance == null)
                return false;

            switch (_resolvedVisualizerPreset.SizeSource)
            {
                case TraitListChannelVisualizerSizeSource.RectTransform:
                    if (instance.RootRect != null)
                    {
                        var rectSize = instance.RootRect.rect.size;
                        if (rectSize.x > 0f || rectSize.y > 0f)
                        {
                            size = rectSize;
                            return true;
                        }
                    }

                    if (TraitListChannelRuntimeHelpers.TryResolveVisualBounds(instance, out var rectFallbackBounds) &&
                        (rectFallbackBounds.LocalSize.x > 0f || rectFallbackBounds.LocalSize.y > 0f))
                    {
                        size = rectFallbackBounds.LocalSize;
                        return true;
                    }
                    break;

                case TraitListChannelVisualizerSizeSource.VisualBounds:
                    if (TraitListChannelRuntimeHelpers.TryResolveVisualBounds(instance, out var bounds) &&
                        (bounds.LocalSize.x > 0f || bounds.LocalSize.y > 0f))
                    {
                        size = bounds.LocalSize;
                        return true;
                    }

                    if (instance.RootRect != null)
                    {
                        var fallbackRect = instance.RootRect.rect.size;
                        if (fallbackRect.x > 0f || fallbackRect.y > 0f)
                        {
                            size = fallbackRect;
                            return true;
                        }
                    }
                    break;

                case TraitListChannelVisualizerSizeSource.Fixed:
                    size = _resolvedVisualizerPreset.FixedSize;
                    return size.x > 0f || size.y > 0f;
            }

            return false;
        }

        async UniTask ClearSpawnedInstancesAsync(CancellationToken ct)
        {
            for (var i = _instances.Count - 1; i >= 0; i--)
            {
                ct.ThrowIfCancellationRequested();
                var instance = _instances[i];
                if (instance == null)
                    continue;
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
            _ = traits;
            QueueRefresh(_resolvedPlayerPreset.RefreshMode);
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
