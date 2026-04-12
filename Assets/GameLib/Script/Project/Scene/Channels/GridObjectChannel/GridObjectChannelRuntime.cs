#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using Game.UI;
using UnityEngine;

namespace Game.Channel
{
    internal sealed class GridObjectChannelRuntime
    {
        readonly IScopeNode _owner;
        readonly GridObjectChannelHubMB _mb;
        readonly GridObjectChannelDefinition _definition;
        readonly GridObjectChannelRuntimeState _state;
        readonly GridObjectChannelPresetResolver _presetResolver;
        readonly GridObjectChannelLayoutPlanner _layoutPlanner = new();
        readonly GridObjectChannelPayloadBuilder _payloadBuilder;
        readonly GridObjectChannelVisualSpawner _visualSpawner;
        readonly GridObjectChannelVisualRelayoutService _visualRelayoutService;
        readonly GridObjectChannelVisualInitializer _visualInitializer;
        readonly GridObjectChannelChoiceController _choiceController;
        readonly GridObjectChannelOperationCoordinator _operations;

        public GridObjectChannelRuntime(
            IScopeNode owner,
            GridObjectChannelHubMB mb,
            GridObjectChannelDefinition definition,
            string tag)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _mb = mb ?? throw new ArgumentNullException(nameof(mb));
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Tag = string.IsNullOrWhiteSpace(tag) ? "default" : tag.Trim();

            _state = new GridObjectChannelRuntimeState(_definition);
            _presetResolver = new GridObjectChannelPresetResolver(_definition, QueueRefresh);
            _payloadBuilder = new GridObjectChannelPayloadBuilder(Tag);
            _visualSpawner = new GridObjectChannelVisualSpawner(Tag);
            _visualRelayoutService = new GridObjectChannelVisualRelayoutService(Tag);
            _visualInitializer = new GridObjectChannelVisualInitializer(Tag, _owner, _payloadBuilder, _visualRelayoutService, ResolveChoiceEntry);
            _operations = new GridObjectChannelOperationCoordinator(Tag);
            _choiceController = new GridObjectChannelChoiceController(
                Tag,
                _state,
                BindInternalForChoiceAsync,
                ClearInternalForChoiceAsync,
                ResolveChoiceTimeoutSeconds);
        }

        public string Tag { get; }
        public bool IsChoiceSessionActive => _choiceController.IsChoiceSessionActive;

        public bool TryCancelActiveChoice(string reason = "")
        {
            return _choiceController.TryCancelActiveChoice(reason);
        }

        public bool TryReplaceActiveChoice(string reason = "")
        {
            return _choiceController.TryReplaceActiveChoice(reason);
        }

        public UniTask<GridObjectChoiceSessionResult> ShowChoiceAndWaitAsync(
            GridObjectChoiceRequest request,
            CancellationToken ct)
        {
            return _choiceController.ShowChoiceAndWaitAsync(request, ct);
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            if (!ReferenceEquals(_owner, scope))
                return;

            _ = isReset;
            if (_definition.ListRoot == null)
            {
                Debug.LogError($"[GridObjectChannel] Invalid layout binding. Tag='{Tag}' requires ListRoot, but it was not assigned.");
                return;
            }

            _state.ActiveScope = scope;
            _state.ListRoot = _definition.ListRoot;
            var listRoot = _state.ListRoot;
            _state.LayoutReferenceTransform = _definition.LayoutRectTransform != null
                ? _definition.LayoutRectTransform
                : _state.ListRoot;
            _state.LayoutRectTransform = _state.LayoutReferenceTransform as RectTransform;
            _state.EnvironmentKind = TransformGridSharedUtility.ResolveEnvironment(listRoot, out var canvas);
            _state.Canvas = canvas;
            _state.LifecycleCts = new CancellationTokenSource();
            _state.IsActive = true;
            _state.IsBuilt = false;
            _state.HasBinding = false;
            _state.LayoutAreaSourceCache = default;
            _state.FixedAnchorSourceCache = default;
            _state.ActiveChoiceEntries = null;
            _operations.ResetQueueState();

            if (_state.EnableVerboseLayoutLog)
            {
                Debug.Log(
                    $"[GridObjectChannel] Acquire layout. Tag='{Tag}' Env={_state.EnvironmentKind} " +
                    $"ListRoot={DescribeTransform(_state.ListRoot)} LayoutRef={DescribeTransform(_state.LayoutReferenceTransform)} " +
                    $"LayoutRect={DescribeRectTransform(_state.LayoutRectTransform)} AutoBuild={_definition.AutoBuild}",
                    _state.ListRoot);
            }

            LogDebug($"Acquire. AutoBuild={_definition.AutoBuild}");

            if (!_definition.AutoBuild)
                return;

            UniTask.Void(async () =>
            {
                try
                {
                    await BindAsync(new GridObjectChannelBindRequest(), rebuild: true, CancellationToken.None);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[GridObjectChannel] Auto build failed. Tag='{Tag}' Message={ex.Message}");
                }
            });
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            if (!ReferenceEquals(_owner, scope))
                return;

            _ = isReset;

            _state.IsActive = false;
            _operations.ResetQueueState();
            _choiceController.TryCancelActiveChoice($"[GOC-CHOICE-010] Channel released. tag='{Tag}'");
            _state.ActiveChoiceEntries = null;

            _state.ItemSourceRuntime?.Dispose();
            _state.ItemSourceRuntime = null;

            if (_state.LifecycleCts != null)
            {
                _state.LifecycleCts.Cancel();
                _state.LifecycleCts.Dispose();
                _state.LifecycleCts = null;
            }

            UniTask.Void(async () =>
            {
                try
                {
                    var lockState = await _operations.TryEnterAsync(_state, CancellationToken.None, "ReleaseClear", reentrantIsError: false);
                    if (!lockState.Entered)
                        return;

                    try
                    {
                        await _visualSpawner.ClearSpawnedInstancesAsync(_state.Visuals, CancellationToken.None);
                    }
                    finally
                    {
                        _operations.Exit(_state, lockState);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[GridObjectChannel] Release clear failed. Tag='{Tag}' Message={ex.Message}");
                }
            });

            _state.ActiveScope = null;
            _state.ListRoot = null;
            _state.LayoutReferenceTransform = null;
            _state.LayoutRectTransform = null;
            _state.Canvas = null;
            _state.HasBinding = false;
            _state.LayoutAreaSourceCache = default;
            _state.FixedAnchorSourceCache = default;
            _state.ResetResolvedState();
        }

        public async UniTask<bool> BindAsync(GridObjectChannelBindRequest request, bool rebuild, CancellationToken ct)
        {
            using var linkedCts = GridObjectChannelRuntimeUtility.CreateLinkedTokenSource(_state.LifecycleCts, ct);
            var linkedToken = linkedCts?.Token ?? ct;

            var lockState = await _operations.TryEnterAsync(_state, linkedToken, "Bind");
            if (!lockState.Entered)
                return false;

            try
            {
                if (!_state.IsActive || _state.ActiveScope == null)
                    return false;

                _state.BindRequest = request?.Clone() ?? new GridObjectChannelBindRequest();
                _state.HasBinding = true;
                if (!TryResolveCurrentState())
                    return false;

                if (!rebuild)
                {
                    await _operations.FlushDeferredClearRequestsInsideLockAsync(ExecuteClearCoreAsync, linkedToken);
                    return true;
                }

                var result = await RefreshResolvedStateAsync(GridObjectChannelRefreshMode.FullRebuild, linkedToken);
                await _operations.FlushDeferredClearRequestsInsideLockAsync(ExecuteClearCoreAsync, linkedToken);
                return result;
            }
            finally
            {
                _operations.Exit(_state, lockState);
            }
        }

        public async UniTask<bool> RefreshAsync(GridObjectChannelRefreshMode mode, CancellationToken ct)
        {
            using var linkedCts = GridObjectChannelRuntimeUtility.CreateLinkedTokenSource(_state.LifecycleCts, ct);
            var linkedToken = linkedCts?.Token ?? ct;

            var lockState = await _operations.TryEnterAsync(_state, linkedToken, "Refresh");
            if (!lockState.Entered)
                return false;

            try
            {
                if (!_state.HasBinding || !_state.IsActive || _state.ActiveScope == null)
                    return false;

                if (!TryResolveCurrentState(out var forceFullRebuild))
                    return false;

                if (forceFullRebuild)
                    mode = GridObjectChannelRefreshMode.FullRebuild;

                var result = await RefreshResolvedStateAsync(mode, linkedToken);
                await _operations.FlushDeferredClearRequestsInsideLockAsync(ExecuteClearCoreAsync, linkedToken);
                return result;
            }
            finally
            {
                _operations.Exit(_state, lockState);
            }
        }

        public async UniTask<bool> ClearAsync(bool keepBinding, CancellationToken ct)
        {
            _choiceController.TryCancelActiveChoice($"[GOC-CHOICE-011] Channel clear requested. tag='{Tag}'");

            using var linkedCts = GridObjectChannelRuntimeUtility.CreateLinkedTokenSource(_state.LifecycleCts, ct);
            var linkedToken = linkedCts?.Token ?? ct;

            var lockState = await _operations.TryEnterAsync(_state, linkedToken, "Clear", reentrantIsError: false);
            if (!lockState.Entered)
            {
                _operations.RequestDeferredClear(keepBinding);
                return true;
            }

            try
            {
                await ExecuteClearCoreAsync(keepBinding, linkedToken);
                await _operations.FlushDeferredClearRequestsInsideLockAsync(ExecuteClearCoreAsync, linkedToken);
                return true;
            }
            finally
            {
                _operations.Exit(_state, lockState);
            }
        }

        async UniTask<bool> BindInternalForChoiceAsync(GridObjectChannelBindRequest request, CancellationToken ct)
        {
            return await BindAsync(request, rebuild: true, ct);
        }

        async UniTask<bool> ClearInternalForChoiceAsync(bool keepBinding, CancellationToken ct)
        {
            return await ClearAsync(keepBinding, ct);
        }

        async UniTask ExecuteClearCoreAsync(bool keepBinding, CancellationToken ct)
        {
            await _visualSpawner.ClearSpawnedInstancesAsync(_state.Visuals, ct);
            _state.IsBuilt = false;

            if (keepBinding)
                return;

            _state.ItemSourceRuntime?.Dispose();
            _state.ItemSourceRuntime = null;
            _state.HasBinding = false;
            _state.BindRequest = new GridObjectChannelBindRequest();
            _state.ResetResolvedState();
        }

        bool TryResolveCurrentState()
        {
            return TryResolveCurrentState(out _);
        }

        bool TryResolveCurrentState(out bool forceFullRebuild)
        {
            forceFullRebuild = false;
            if (_state.ActiveScope == null)
                return false;

            var dynamicContext = new SimpleDynamicContext(GridObjectChannelRuntimeUtility.ResolveVars(_state.ActiveScope), _state.ActiveScope);
            if (!_presetResolver.TryResolve(_state, dynamicContext, out var resolved, out var error))
            {
                Debug.LogWarning($"[GridObjectChannel] Player resolve failed. Tag='{Tag}' Message={error}");
                return false;
            }

            _state.ResolvedPlayerPreset = resolved.PlayerPreset;
            _state.ResolvedLayoutPreset = resolved.LayoutPreset;
            _state.ResolvedVisualizerPreset = resolved.VisualizerPreset;
            _state.ResolvedRuntimeTemplate = resolved.RuntimeTemplate;
            forceFullRebuild = resolved.ForceFullRebuild;

            if (_state.ActiveScope.Resolver != null)
                TransformGridSharedUtility.RefreshLayoutAndBounds(_state.ActiveScope.Resolver);

            if (_state.EnableVerboseLayoutLog)
            {
                Debug.Log(
                    $"[GridObjectChannel] Resolved layout context. Tag='{Tag}' Channel={_state.ChannelTag} " +
                    $"Env={_state.EnvironmentKind} RangeSource={_state.ResolvedLayoutPreset.RangeSourceMode} " +
                    $"SpawnAnchor={_state.ResolvedLayoutPreset.SpawnAnchorMode} " +
                    $"ItemAlign={_state.ResolvedLayoutPreset.ItemHorizontalAlignment}/{_state.ResolvedLayoutPreset.ItemVerticalAlignment} " +
                    $"AreaAlign={_state.ResolvedLayoutPreset.AreaHorizontalAlignment}/{_state.ResolvedLayoutPreset.AreaVerticalAlignment} " +
                    $"ItemOffset={_state.ResolvedLayoutPreset.ItemOffset} SpawnOffset={_state.ResolvedLayoutPreset.SpawnOffset} " +
                    $"ListRoot={DescribeTransform(_state.ListRoot)} LayoutRef={DescribeTransform(_state.LayoutReferenceTransform)} " +
                    $"LayoutRect={DescribeRectTransform(_state.LayoutRectTransform)} RuntimeTemplate={_state.ResolvedRuntimeTemplate?.name ?? "null"}",
                    _state.ListRoot);
            }

            if (_state.EnvironmentKind == TransformGridEnvironmentKind.ScreenUI &&
                _state.ResolvedLayoutPreset.RangeSourceMode == TransformGridLayoutRangeSourceMode.RectTransform)
            {
                if (_state.LayoutRectTransform == null)
                {
                    Debug.LogError($"[GridObjectChannel] Invalid layout binding. Tag='{Tag}' requires a RectTransform for RectTransform range source, but LayoutRectTransform was not resolved.");
                    return false;
                }

                var layoutRect = _state.LayoutRectTransform.rect;
                if (layoutRect.width <= 0f && layoutRect.height <= 0f)
                {
                    Debug.LogError($"[GridObjectChannel] Invalid layout binding. Tag='{Tag}' LayoutRectTransform='{_state.LayoutRectTransform.name}' has zero size. Rect={layoutRect}");
                    return false;
                }
            }

            LogDebug(
                $"Resolved state. ForceFullRebuild={forceFullRebuild} Player={_state.ResolvedPlayerPreset.GetType().Name} " +
                $"Rows={_state.ResolvedLayoutPreset.Rows.GetOrDefault(dynamicContext, 1)} " +
                $"Columns={_state.ResolvedLayoutPreset.Columns.GetOrDefault(dynamicContext, 1)}");
            return true;
        }

        async UniTask<bool> RefreshResolvedStateAsync(GridObjectChannelRefreshMode mode, CancellationToken ct)
        {
            if (_state.ListRoot == null || _state.ActiveScope == null)
                return false;

            if (mode != GridObjectChannelRefreshMode.LayoutOnly && _state.ResolvedRuntimeTemplate == null)
            {
                Debug.LogWarning($"[GridObjectChannel] Refresh skipped because RuntimeTemplate is null. Tag='{Tag}'");
                return false;
            }

            if (_state.EnableVerboseLayoutLog)
            {
                Debug.Log(
                    $"[GridObjectChannel] Refresh begin. Tag='{Tag}' Mode={mode} Built={_state.IsBuilt} " +
                    $"Env={_state.EnvironmentKind} ListRoot={DescribeTransform(_state.ListRoot)} " +
                    $"LayoutRef={DescribeTransform(_state.LayoutReferenceTransform)} LayoutRect={DescribeRectTransform(_state.LayoutRectTransform)} " +
                    $"RuntimeTemplate={_state.ResolvedRuntimeTemplate?.name ?? "null"}",
                    _state.ListRoot);
            }

            var items = await BuildItemsForRefreshAsync(mode, ct);
            if (items == null)
                return false;

            if (mode == GridObjectChannelRefreshMode.FullRebuild || !_state.IsBuilt)
            {
                LogDebug($"Refresh build path. Mode={mode} Built={_state.IsBuilt} ItemCount={items.Count}");
                await _visualSpawner.ClearSpawnedInstancesAsync(_state.Visuals, ct);
                await BuildFromItemsAsync(items, ct);
                _state.IsBuilt = true;
                return true;
            }

            var newlySpawnedKeys = new HashSet<GridObjectChannelItemKey>();
            var itemLookup = new Dictionary<GridObjectChannelItemKey, GridObjectChannelResolvedItem>(items.Count);
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (!itemLookup.ContainsKey(item.Key))
                    itemLookup.Add(item.Key, item);
            }

            if (mode != GridObjectChannelRefreshMode.LayoutOnly)
            {
                for (var i = _state.Visuals.Count - 1; i >= 0; i--)
                {
                    ct.ThrowIfCancellationRequested();
                    var instance = _state.Visuals[i];
                    if (instance == null)
                    {
                        _state.Visuals.RemoveAt(i);
                        continue;
                    }

                    if (!itemLookup.ContainsKey(instance.Key))
                    {
                        await GridObjectChannelRuntimeUtility.ReleaseSpawnedInstanceAsync(instance.Root, instance.Scope, instance.Resolver);
                        _state.Visuals.RemoveAt(i);
                    }
                }

                var totalNewCount = 0;
                for (var i = 0; i < items.Count; i++)
                {
                    if (!_state.Visuals.ContainsKey(items[i].Key))
                        totalNewCount++;
                }

                var initializedNewCount = 0;
                for (var i = 0; i < items.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var item = items[i];
                    if (_state.Visuals.ContainsKey(item.Key))
                        continue;

                    var spawned = await _visualSpawner.SpawnRawAsync(_state, item, ct);
                    if (spawned == null)
                        continue;

                    spawned.UpdateFromItem(item);
                    _visualInitializer.ApplyPreviewSpawnPosition(_state, spawned, item);
                    GridObjectChannelVisualSpawner.SetInstancePresentationVisible(spawned, false);
                    _state.Visuals.Add(spawned);
                    newlySpawnedKeys.Add(item.Key);

                    await _visualInitializer.InitializeSpawnedInstanceAsync(_state, item, spawned, ct);
                    initializedNewCount++;
                    await _visualInitializer.DelayBetweenNewSpawnsIfNeededAsync(_state, initializedNewCount, totalNewCount, ct);
                }
            }

            for (var i = 0; i < items.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var item = items[i];
                if (!_state.Visuals.TryGetValue(item.Key, out var instance) || instance == null)
                    continue;

                if (newlySpawnedKeys.Contains(item.Key))
                    continue;

                if (mode != GridObjectChannelRefreshMode.LayoutOnly)
                {
                    var payload = _payloadBuilder.BuildPayload(_state, item);
                    _ = _payloadBuilder.ApplyPayloadToBlackboard(instance, payload);
                }

                await _visualRelayoutService.RelayoutInstanceAsync(_state, instance, item, ct);
            }

            _state.Visuals.SortByListIndex();
            LogDebug($"Refresh completed. Mode={mode} ItemCount={items.Count} VisualCount={_state.Visuals.Count}");
            return true;
        }

        UniTask<List<GridObjectChannelResolvedItem>?> BuildItemsForRefreshAsync(
            GridObjectChannelRefreshMode mode,
            CancellationToken ct)
        {
            var items = new List<GridObjectChannelResolvedItem>(Mathf.Max(32, _state.Visuals.Count));
            if (mode == GridObjectChannelRefreshMode.LayoutOnly && _state.IsBuilt && _state.Visuals.Count > 0)
            {
                for (var i = 0; i < _state.Visuals.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var instance = _state.Visuals[i];
                    items.Add(new GridObjectChannelResolvedItem
                    {
                        Key = instance.Key,
                        ListIndex = instance.ListIndex,
                        Row = instance.Row,
                        Column = instance.Column,
                        SourceRow = instance.SourceRow,
                        SourceColumn = instance.SourceColumn,
                    });
                }

                _layoutPlanner.RecalculateItemPositions(
                    new GridObjectChannelLayoutPlanContext(
                        _state,
                        new SimpleDynamicContext(GridObjectChannelRuntimeUtility.ResolveVars(_state.ActiveScope), _state.ActiveScope!)),
                    items);
                return UniTask.FromResult<List<GridObjectChannelResolvedItem>?>(items);
            }

            var context = new SimpleDynamicContext(GridObjectChannelRuntimeUtility.ResolveVars(_state.ActiveScope), _state.ActiveScope!);
            if (!_layoutPlanner.TryBuildResolvedItems(
                    new GridObjectChannelLayoutPlanContext(_state, context),
                    items,
                    out var error))
            {
                Debug.LogError($"[GridObjectChannel] Item build failed. Tag='{Tag}' Message={error}");
                return UniTask.FromResult<List<GridObjectChannelResolvedItem>?>(null);
            }

            _layoutPlanner.RecalculateItemPositions(
                new GridObjectChannelLayoutPlanContext(_state, context),
                items);
            LogBuiltItems(items);
            return UniTask.FromResult<List<GridObjectChannelResolvedItem>?>(items);
        }

        async UniTask BuildFromItemsAsync(List<GridObjectChannelResolvedItem> items, CancellationToken ct)
        {
            if (items.Count == 0)
                return;

            var totalSpawnCount = items.Count;
            var initializedSpawnCount = 0;
            for (var i = 0; i < items.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var item = items[i];
                var spawned = await _visualSpawner.SpawnRawAsync(_state, item, ct);
                if (spawned == null)
                    continue;

                spawned.UpdateFromItem(item);
                _visualInitializer.ApplyPreviewSpawnPosition(_state, spawned, item);
                GridObjectChannelVisualSpawner.SetInstancePresentationVisible(spawned, false);
                _state.Visuals.Add(spawned);

                await _visualInitializer.InitializeSpawnedInstanceAsync(_state, item, spawned, ct);
                initializedSpawnCount++;
                await _visualInitializer.DelayBetweenNewSpawnsIfNeededAsync(_state, initializedSpawnCount, totalSpawnCount, ct);
            }

            _state.Visuals.SortByListIndex();
            LogDebug($"Build completed. Spawned={_state.Visuals.Count}");
        }

        void QueueRefresh(GridObjectChannelRefreshMode mode)
        {
            _operations.QueueRefresh(
                _state,
                mode,
                async (refreshMode, cancellationToken) => await RefreshAsync(refreshMode, cancellationToken));
        }

        GridObjectChoiceEntry? ResolveChoiceEntry(int listIndex)
        {
            var entries = _state.ActiveChoiceEntries;
            if (entries == null || listIndex < 0 || listIndex >= entries.Count)
                return null;

            return entries[listIndex];
        }

        float ResolveChoiceTimeoutSeconds(GridObjectChoiceWaitOptions waitOptions)
        {
            if (_state.ActiveScope == null)
                return 0f;

            var dynamicContext = new SimpleDynamicContext(GridObjectChannelRuntimeUtility.ResolveVars(_state.ActiveScope), _state.ActiveScope);
            return waitOptions.ResolveTimeoutSeconds(dynamicContext);
        }

        void LogBuiltItems(List<GridObjectChannelResolvedItem> items)
        {
            if (!_state.EnableDebugLog)
                return;

            LogDebug($"Built items. Count={items.Count}");
            if (!_state.EnableVerboseLayoutLog)
                return;

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                Debug.Log(
                    $"[GridObjectChannel] Item[{i}] Key={item.Key} ListIndex={item.ListIndex} " +
                    $"SourceRow={item.SourceRow} SourceColumn={item.SourceColumn} Row={item.Row} Column={item.Column}");
            }
        }

        void LogDebug(string message)
        {
            if (!_state.EnableDebugLog)
                return;

            Debug.Log($"[GridObjectChannel] {message} Tag='{Tag}'");
        }

        static string DescribeTransform(Transform? transform)
        {
            if (transform == null)
                return "null";

            return $"{transform.name} local={transform.localPosition} world={transform.position}";
        }

        static string DescribeRectTransform(RectTransform? rectTransform)
        {
            if (rectTransform == null)
                return "null";

            var rect = rectTransform.rect;
            return $"{rectTransform.name} rect={rect} anchored={rectTransform.anchoredPosition3D} anchorMin={rectTransform.anchorMin} anchorMax={rectTransform.anchorMax} pivot={rectTransform.pivot}";
        }
    }
}
