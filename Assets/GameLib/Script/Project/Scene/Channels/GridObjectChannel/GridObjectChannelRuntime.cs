#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Commands.VNext;
using Game.Common;
using Game.DI;
using Game.Spawn;
using Game.UI;
using Game.Vars.Generated;
using UnityEngine;
using VContainer;

namespace Game.Channel
{
    internal sealed class GridObjectChannelRuntime
    {
        interface IGridObjectChannelPlayerRuntime : IDisposable
        {
            GridObjectChannelPlayerPresetBase Preset { get; }
            bool PreserveSourceCoordinates { get; }
            int RowOffset { get; }
            int ColumnOffset { get; }
            void ResolveSourceLayoutCoordinates(int sourceRow, int sourceColumn, out int row, out int column);
            bool Resolve(IDynamicContext dynamicContext, IScopeNode activeScope, Action<GridObjectChannelRefreshMode> queueRefresh, out string? error);
            bool TryCollectItems(IDynamicContext dynamicContext, List<GridObjectChannelResolvedItem> items, out string? error);
        }

        sealed class StandalonePlayerRuntime : IGridObjectChannelPlayerRuntime
        {
            readonly GridObjectChannelStandalonePlayerPreset _preset;

            public StandalonePlayerRuntime(GridObjectChannelStandalonePlayerPreset preset)
            {
                _preset = preset ?? throw new ArgumentNullException(nameof(preset));
            }

            public GridObjectChannelPlayerPresetBase Preset => _preset;
            public bool PreserveSourceCoordinates => false;
            public int RowOffset => 0;
            public int ColumnOffset => 0;

            public void ResolveSourceLayoutCoordinates(int sourceRow, int sourceColumn, out int row, out int column)
            {
                row = sourceRow;
                column = sourceColumn;
            }

            public bool Resolve(IDynamicContext dynamicContext, IScopeNode activeScope, Action<GridObjectChannelRefreshMode> queueRefresh, out string? error)
            {
                _ = dynamicContext;
                _ = activeScope;
                _ = queueRefresh;
                error = null;
                return true;
            }

            public bool TryCollectItems(IDynamicContext dynamicContext, List<GridObjectChannelResolvedItem> items, out string? error)
            {
                items.Clear();
                var count = Mathf.Max(0, _preset.Count.GetOrDefault(dynamicContext, 0));
                for (var i = 0; i < count; i++)
                {
                    items.Add(new GridObjectChannelResolvedItem
                    {
                        Key = GridObjectChannelItemKey.Standalone(i),
                        ListIndex = i,
                        SourceRow = -1,
                        SourceColumn = -1,
                    });
                }

                error = null;
                return true;
            }

            public void Dispose()
            {
            }
        }

        sealed class GridBlackboardPlayerRuntime : IGridObjectChannelPlayerRuntime
        {
            readonly GridObjectChannelGridBlackboardPlayerPreset _preset;
            readonly List<GridBlackboardCellSnapshot> _allCells = new(128);
            readonly List<GridBlackboardCellSnapshot> _cellBuffer = new(16);

            ActorSourceResolveCache _gridActorCache;
            IGridBlackboardService? _grid;
            Action<GridObjectChannelRefreshMode>? _queueRefresh;
            int _rowOffset;
            int _columnOffset;

            public GridBlackboardPlayerRuntime(GridObjectChannelGridBlackboardPlayerPreset preset)
            {
                _preset = preset ?? throw new ArgumentNullException(nameof(preset));
            }

            public GridObjectChannelPlayerPresetBase Preset => _preset;
            public bool PreserveSourceCoordinates => _preset.SparseLayoutMode == GridObjectChannelSparseLayoutMode.PreserveSparseCoordinates;
            public int RowOffset => _rowOffset;
            public int ColumnOffset => _columnOffset;

            public void ResolveSourceLayoutCoordinates(int sourceRow, int sourceColumn, out int row, out int column)
            {
                if (_preset.SwapRowAndColumn)
                {
                    row = sourceColumn;
                    column = sourceRow;
                    return;
                }

                row = sourceRow;
                column = sourceColumn;
            }

            public bool Resolve(IDynamicContext dynamicContext, IScopeNode activeScope, Action<GridObjectChannelRefreshMode> queueRefresh, out string? error)
            {
                _queueRefresh = queueRefresh;
                _rowOffset = _preset.RowOffset.GetOrDefault(dynamicContext, 0);
                _columnOffset = _preset.ColumnOffset.GetOrDefault(dynamicContext, 0);

                var scope = ActorSourceFastResolver.ResolveCached(activeScope, _preset.GridBlackboardActorSource, ref _gridActorCache);
                EnsureScopeBuiltIfNeeded(scope);
                if (!TryResolveGridBlackboard(scope, out var grid))
                {
                    SwapGrid(null);
                    error = "IGridBlackboardService is missing.";
                    return false;
                }

                SwapGrid(grid);
                error = null;
                return true;
            }

            public bool TryCollectItems(IDynamicContext dynamicContext, List<GridObjectChannelResolvedItem> items, out string? error)
            {
                _ = dynamicContext;
                items.Clear();

                if (_grid == null)
                {
                    error = "Grid blackboard is not resolved.";
                    return false;
                }

                _allCells.Clear();
                _grid.TryCollectAllCells(_allCells);
                if (_allCells.Count == 0)
                {
                    error = null;
                    return true;
                }

                var filterVarId = _preset.UseGridKeyFilter ? ResolveVarId(_preset.GridKey, 0) : 0;
                var listIndex = 0;
                var currentRow = int.MinValue;
                var currentColumn = int.MinValue;
                _cellBuffer.Clear();

                for (var i = 0; i < _allCells.Count; i++)
                {
                    var cell = _allCells[i];
                    if (currentRow == int.MinValue)
                    {
                        currentRow = cell.Row;
                        currentColumn = cell.Column;
                    }

                    if (cell.Row != currentRow || cell.Column != currentColumn)
                    {
                        AddCurrentCellIfNeeded(items, filterVarId, ref listIndex, currentRow, currentColumn);
                        _cellBuffer.Clear();
                        currentRow = cell.Row;
                        currentColumn = cell.Column;
                    }

                    _cellBuffer.Add(cell);
                }

                AddCurrentCellIfNeeded(items, filterVarId, ref listIndex, currentRow, currentColumn);
                error = null;
                return true;
            }

            public void Dispose()
            {
                SwapGrid(null);
            }

            void AddCurrentCellIfNeeded(
                List<GridObjectChannelResolvedItem> items,
                int filterVarId,
                ref int listIndex,
                int sourceRow,
                int sourceColumn)
            {
                if (_cellBuffer.Count == 0)
                    return;

                if (filterVarId > 0 && !HasEnabledFilterValue(_cellBuffer, filterVarId))
                    return;

                var item = new GridObjectChannelResolvedItem
                {
                    Key = GridObjectChannelItemKey.SourceCell(sourceRow, sourceColumn),
                    ListIndex = listIndex,
                    SourceRow = sourceRow,
                    SourceColumn = sourceColumn,
                };
                if (PreserveSourceCoordinates)
                {
                    ResolveSourceLayoutCoordinates(sourceRow, sourceColumn, out var layoutRow, out var layoutColumn);
                    item.Row = Mathf.Max(0, layoutRow + _rowOffset);
                    item.Column = Mathf.Max(0, layoutColumn + _columnOffset);
                }

                item.SetCellValues(_cellBuffer);
                items.Add(item);
                listIndex++;
            }

            void HandleGridChanged(int version)
            {
                _ = version;
                _queueRefresh?.Invoke(_preset.RefreshMode);
            }

            void SwapGrid(IGridBlackboardService? next)
            {
                if (ReferenceEquals(_grid, next))
                    return;

                if (_grid != null)
                    _grid.OnChanged -= HandleGridChanged;

                _grid = next;

                if (_grid != null)
                    _grid.OnChanged += HandleGridChanged;
            }

            static bool HasEnabledFilterValue(List<GridBlackboardCellSnapshot> values, int filterVarId)
            {
                for (var i = 0; i < values.Count; i++)
                {
                    var cell = values[i];
                    if (cell.VarId != filterVarId || cell.Value.Kind == ValueKind.Null)
                        continue;

                    if (!cell.Value.TryGet<bool>(out var enabled) || enabled)
                        return true;
                }

                return false;
            }

            static bool TryResolveGridBlackboard(IScopeNode? scope, out IGridBlackboardService? grid)
            {
                grid = null;
                for (var current = scope; current != null; current = current.Parent)
                {
                    var resolver = current.Resolver;
                    if (resolver == null)
                        continue;

                    if (resolver.TryResolve<IGridBlackboardService>(out var resolved) && resolved != null)
                    {
                        grid = resolved;
                        return true;
                    }
                }

                return false;
            }
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

        readonly struct ResolveResult
        {
            public ResolveResult(bool success, bool forceFullRebuild)
            {
                Success = success;
                ForceFullRebuild = forceFullRebuild;
            }

            public bool Success { get; }
            public bool ForceFullRebuild { get; }
        }

        readonly IScopeNode _owner;
        readonly GridObjectChannelHubMB _mb;
        readonly GridObjectChannelDefinition _definition;
        readonly SemaphoreSlim _mutex = new(1, 1);
        readonly Dictionary<GridObjectChannelItemKey, GridObjectChannelVisualInstance> _lookup = new();
        readonly List<GridObjectChannelVisualInstance> _instances = new();
        readonly AsyncLocal<int> _operationContextStamp = new();

        CancellationTokenSource? _lifecycleCts;
        GridObjectChannelBindRequest _bindRequest = new();
        GridObjectChannelPlayerPresetBase _resolvedPlayerPreset = new GridObjectChannelStandalonePlayerPreset();
        GridObjectChannelLayoutPreset _resolvedLayoutPreset = new();
        GridObjectChannelVisualizerPreset _resolvedVisualizerPreset = new();
        BaseRuntimeTemplateSO? _resolvedRuntimeTemplate;
        IGridObjectChannelPlayerRuntime? _playerRuntime;
        IScopeNode? _activeScope;
        Transform? _listRoot;
        Transform? _layoutReferenceTransform;
        RectTransform? _layoutRectTransform;
        Canvas? _canvas;
        ActorSourceResolveCache _layoutAreaSourceCache;
        ActorSourceResolveCache _fixedAnchorSourceCache;
        TransformGridEnvironmentKind _environmentKind;
        bool _hasBinding;
        bool _isBuilt;
        bool _isActive;
        bool _queueWorkerActive;
        bool _refreshQueued;
        GridObjectChannelRefreshMode _queuedRefreshMode;
        int _activeOperationStamp;
        int _operationStampSeed;

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
        }

        public string Tag { get; }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _ = isReset;
            _activeScope = scope;
            _listRoot = _definition.ListRoot != null ? _definition.ListRoot : _mb.transform;
            _layoutReferenceTransform = _definition.LayoutRectTransform != null
                ? _definition.LayoutRectTransform
                : _listRoot;
            _layoutRectTransform = _layoutReferenceTransform as RectTransform;
            _environmentKind = TransformGridSharedUtility.ResolveEnvironment(_listRoot, out _canvas);
            _lifecycleCts = new CancellationTokenSource();
            _isActive = true;
            _isBuilt = false;
            _hasBinding = false;
            _layoutAreaSourceCache = default;
            _fixedAnchorSourceCache = default;

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
            _ = scope;
            _ = isReset;

            _isActive = false;
            _refreshQueued = false;
            _queueWorkerActive = false;

            _playerRuntime?.Dispose();
            _playerRuntime = null;

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
                    Debug.LogError($"[GridObjectChannel] Release clear failed. Tag='{Tag}' Message={ex.Message}");
                }
            });

            _activeScope = null;
            _listRoot = null;
            _layoutReferenceTransform = null;
            _layoutRectTransform = null;
            _canvas = null;
            _hasBinding = false;
            _resolvedPlayerPreset = new GridObjectChannelStandalonePlayerPreset();
            _resolvedLayoutPreset = new GridObjectChannelLayoutPreset();
            _resolvedVisualizerPreset = new GridObjectChannelVisualizerPreset();
            _resolvedRuntimeTemplate = null;
            _layoutAreaSourceCache = default;
            _fixedAnchorSourceCache = default;
        }

        public async UniTask<bool> BindAsync(GridObjectChannelBindRequest request, bool rebuild, CancellationToken ct)
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

                _bindRequest = request?.Clone() ?? new GridObjectChannelBindRequest();
                _hasBinding = true;
                var resolveResult = ResolveCurrentState();
                if (!resolveResult.Success)
                    return false;

                if (!rebuild)
                    return true;

                return await RefreshResolvedStateAsync(GridObjectChannelRefreshMode.FullRebuild, linkedToken);
            }
            finally
            {
                ExitOperationContext(lockState.PreviousStamp, lockState.CurrentStamp);
                _mutex.Release();
            }
        }

        public async UniTask<bool> RefreshAsync(GridObjectChannelRefreshMode mode, CancellationToken ct)
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

                var resolveResult = ResolveCurrentState();
                if (!resolveResult.Success)
                    return false;

                if (resolveResult.ForceFullRebuild)
                    mode = GridObjectChannelRefreshMode.FullRebuild;

                return await RefreshResolvedStateAsync(mode, linkedToken);
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
                    _playerRuntime?.Dispose();
                    _playerRuntime = null;
                    _hasBinding = false;
                    _bindRequest = new GridObjectChannelBindRequest();
                    _resolvedPlayerPreset = new GridObjectChannelStandalonePlayerPreset();
                    _resolvedLayoutPreset = new GridObjectChannelLayoutPreset();
                    _resolvedVisualizerPreset = new GridObjectChannelVisualizerPreset();
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

        ResolveResult ResolveCurrentState()
        {
            if (_activeScope == null)
                return new ResolveResult(false, false);

            var dynCtx = new SimpleDynamicContext(ResolveVars(_activeScope), _activeScope);
            var playerPreset = _definition.PlayerPresetValue.GetOrDefault(
                dynCtx,
                new GridObjectChannelStandalonePlayerPreset())?.CreateRuntimeCopy() ?? new GridObjectChannelStandalonePlayerPreset();
            if (_bindRequest.OverridePlayerPreset)
            {
                playerPreset = _bindRequest.PlayerPresetValue.GetOrDefault(
                    dynCtx,
                    new GridObjectChannelStandalonePlayerPreset())?.CreateRuntimeCopy() ?? new GridObjectChannelStandalonePlayerPreset();
            }

            var layoutPreset = _definition.LayoutPresetValue.GetOrDefault(dynCtx, new GridObjectChannelLayoutPreset()).CreateRuntimeCopy();
            if (_bindRequest.OverrideLayoutPreset)
                layoutPreset = _bindRequest.LayoutPresetValue.GetOrDefault(dynCtx, new GridObjectChannelLayoutPreset()).CreateRuntimeCopy();

            var visualizerPreset = _definition.VisualizerPresetValue.GetOrDefault(dynCtx, new GridObjectChannelVisualizerPreset()).CreateRuntimeCopy();
            if (_bindRequest.OverrideVisualizerPreset)
                visualizerPreset = _bindRequest.VisualizerPresetValue.GetOrDefault(dynCtx, new GridObjectChannelVisualizerPreset()).CreateRuntimeCopy();

            BaseRuntimeTemplateSO? runtimeTemplate = null;
            if (!visualizerPreset.TryResolveRuntimeTemplate(dynCtx, out runtimeTemplate) || runtimeTemplate == null)
            {
                Debug.LogWarning($"[GridObjectChannel] RuntimeTemplate could not be resolved. Tag='{Tag}'");
            }

            var previousPlayerType = _resolvedPlayerPreset?.GetType();
            var previousRuntimeTemplate = _resolvedRuntimeTemplate;
            var forceFullRebuild = previousPlayerType != null &&
                                   (previousPlayerType != playerPreset.GetType() ||
                                    !ReferenceEquals(previousRuntimeTemplate, runtimeTemplate));

            if (!ResolvePlayerRuntime(playerPreset, dynCtx, out var error))
            {
                Debug.LogWarning($"[GridObjectChannel] Player resolve failed. Tag='{Tag}' Message={error}");
                return new ResolveResult(false, false);
            }

            _resolvedPlayerPreset = playerPreset;
            _resolvedLayoutPreset = layoutPreset;
            _resolvedVisualizerPreset = visualizerPreset;
            _resolvedRuntimeTemplate = runtimeTemplate;
            return new ResolveResult(true, forceFullRebuild);
        }

        bool ResolvePlayerRuntime(GridObjectChannelPlayerPresetBase playerPreset, IDynamicContext dynamicContext, out string? error)
        {
            var recreate = _playerRuntime == null || _playerRuntime.Preset.GetType() != playerPreset.GetType();
            if (recreate)
            {
                _playerRuntime?.Dispose();
                _playerRuntime = CreatePlayerRuntime(playerPreset);
            }

            if (_playerRuntime == null)
            {
                error = "Player runtime is null.";
                return false;
            }

            return _playerRuntime.Resolve(dynamicContext, _activeScope!, QueueRefresh, out error);
        }

        async UniTask<bool> RefreshResolvedStateAsync(GridObjectChannelRefreshMode mode, CancellationToken ct)
        {
            if (_playerRuntime == null || _listRoot == null)
                return false;

            if (_resolvedRuntimeTemplate == null)
            {
                Debug.LogWarning($"[GridObjectChannel] Refresh skipped because RuntimeTemplate is null. Tag='{Tag}'");
                return false;
            }

            var dynCtx = new SimpleDynamicContext(ResolveVars(_activeScope), _activeScope);
            if (!TryBuildResolvedItems(dynCtx, out var items, out var error))
            {
                Debug.LogError($"[GridObjectChannel] Item build failed. Tag='{Tag}' Message={error}");
                return false;
            }

            if (mode == GridObjectChannelRefreshMode.FullRebuild || !_isBuilt)
            {
                await ClearSpawnedInstancesAsync(ct);
                await BuildFromItemsAsync(items, ct);
                _isBuilt = true;
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
                for (var i = _instances.Count - 1; i >= 0; i--)
                {
                    ct.ThrowIfCancellationRequested();
                    var instance = _instances[i];
                    if (instance == null)
                    {
                        _instances.RemoveAt(i);
                        continue;
                    }

                    if (!itemLookup.ContainsKey(instance.Key))
                    {
                        await ReleaseSpawnedInstanceAsync(instance.Root, instance.Scope, instance.Resolver);
                        _instances.RemoveAt(i);
                        _lookup.Remove(instance.Key);
                    }
                }

                RecalculateItemPositions(items);

                for (var i = 0; i < items.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var item = items[i];
                    if (_lookup.ContainsKey(item.Key))
                        continue;

                    var spawned = await SpawnRawAsync(item, ct);
                    if (spawned == null)
                        continue;

                    spawned.UpdateFromItem(item);
                    _instances.Add(spawned);
                    _lookup[item.Key] = spawned;
                    newlySpawnedKeys.Add(item.Key);
                }
            }

            if (mode == GridObjectChannelRefreshMode.LayoutOnly)
                RecalculateItemPositions(items);

            var initializedNewCount = 0;
            var totalNewCount = newlySpawnedKeys.Count;
            for (var i = 0; i < items.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var item = items[i];
                if (!_lookup.TryGetValue(item.Key, out var instance) || instance == null)
                    continue;

                if (newlySpawnedKeys.Contains(item.Key))
                {
                    await InitializeSpawnedInstanceAsync(instance, item, ct);
                    initializedNewCount++;
                    await DelayBetweenNewSpawnsIfNeededAsync(initializedNewCount, totalNewCount, ct);
                }
                else
                {
                    await RelayoutInstanceAsync(instance, item, ct);
                }
            }

            SortInstancesByListIndex();
            return true;
        }

        async UniTask BuildFromItemsAsync(List<GridObjectChannelResolvedItem> items, CancellationToken ct)
        {
            if (items.Count == 0)
                return;

            RecalculateItemPositions(items);
            for (var i = 0; i < items.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var item = items[i];
                var spawned = await SpawnRawAsync(item, ct);
                if (spawned == null)
                    continue;

                spawned.UpdateFromItem(item);
                _instances.Add(spawned);
                _lookup[item.Key] = spawned;
            }
            var initializedSpawnCount = 0;
            for (var i = 0; i < items.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var item = items[i];
                if (!_lookup.TryGetValue(item.Key, out var instance) || instance == null)
                    continue;

                await InitializeSpawnedInstanceAsync(instance, item, ct);
                initializedSpawnCount++;
                await DelayBetweenNewSpawnsIfNeededAsync(initializedSpawnCount, _instances.Count, ct);
            }

            SortInstancesByListIndex();
        }

        bool TryBuildResolvedItems(IDynamicContext dynamicContext, out List<GridObjectChannelResolvedItem> items, out string? error)
        {
            items = new List<GridObjectChannelResolvedItem>(32);
            error = null;

            if (_playerRuntime == null)
            {
                error = "Player runtime is null.";
                return false;
            }

            if (!_playerRuntime.TryCollectItems(dynamicContext, items, out error))
                return false;

            var rows = Mathf.Max(1, _resolvedLayoutPreset.Rows.GetOrDefault(dynamicContext, 1));
            var columns = Mathf.Max(1, _resolvedLayoutPreset.Columns.GetOrDefault(dynamicContext, 1));

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (_playerRuntime.PreserveSourceCoordinates)
                {
                    _playerRuntime.ResolveSourceLayoutCoordinates(item.SourceRow, item.SourceColumn, out var sourceLayoutRow, out var sourceLayoutColumn);
                    item.Row = Mathf.Max(0, sourceLayoutRow + _playerRuntime.RowOffset);
                    item.Column = Mathf.Max(0, sourceLayoutColumn + _playerRuntime.ColumnOffset);
                }
                else
                {
                    var (row, column) = TransformGridSharedUtility.ResolveRowColumn((int)_resolvedLayoutPreset.Order, item.ListIndex, rows, columns);
                    item.Row = Mathf.Max(0, row + _playerRuntime.RowOffset);
                    item.Column = Mathf.Max(0, column + _playerRuntime.ColumnOffset);
                }

                items[i] = item;
            }

            return true;
        }

        void RecalculateItemPositions(List<GridObjectChannelResolvedItem> items)
        {
            if (items.Count == 0)
                return;

            var itemSize = ResolvePlanningItemSize();
            var totalRows = ResolveTotalRows(items);
            var totalColumns = ResolveTotalColumns(items);
            var rect = TransformGridSharedUtility.ResolveLayoutRect(
                _listRoot,
                _layoutReferenceTransform,
                _layoutRectTransform,
                _canvas,
                _activeScope,
                _environmentKind,
                _resolvedLayoutPreset.RangeSourceMode,
                _resolvedLayoutPreset.AreaActorSource,
                ref _layoutAreaSourceCache,
                _resolvedLayoutPreset.AreaChannelTag);

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                item.TargetLocalPosition = TransformGridSharedUtility.ResolveTargetLocalPosition(
                    rect,
                    item.Row,
                    item.Column,
                    totalRows,
                    totalColumns,
                    itemSize,
                    _resolvedLayoutPreset.RowSpacing,
                    _resolvedLayoutPreset.ColumnSpacing,
                    (int)_resolvedLayoutPreset.AreaHorizontalAlignment,
                    (int)_resolvedLayoutPreset.AreaVerticalAlignment,
                    _resolvedLayoutPreset.ItemOffset);
                items[i] = item;
            }
        }

        async UniTask<GridObjectChannelVisualInstance?> SpawnRawAsync(GridObjectChannelResolvedItem item, CancellationToken ct)
        {
            if (_activeScope == null || _listRoot == null || _resolvedRuntimeTemplate == null)
                return null;

            if (!TryResolveFromScopeOrAncestors<ISceneSpawnerRegistry>(_activeScope, out var registry) || registry == null)
            {
                Debug.LogWarning($"[GridObjectChannel] ISceneSpawnerRegistry is not available. Tag='{Tag}'");
                return null;
            }

            var spawner = ResolveSpawner(registry);
            if (spawner == null)
            {
                Debug.LogWarning($"[GridObjectChannel] Runtime spawner is not available. Tag='{Tag}'");
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
                Debug.LogError($"[GridObjectChannel] Spawn failed. Tag='{Tag}' Message={ex.Message}");
                return null;
            }

            ExtractSpawnedInfo(resolver, out var root, out var scopeNode);
            if (resolver == null || root == null || scopeNode == null)
            {
                await ReleaseSpawnedInstanceAsync(root, scopeNode, resolver);
                Debug.LogError($"[GridObjectChannel] Spawned instance is missing root or scope. Tag='{Tag}'");
                return null;
            }

            var instance = new GridObjectChannelVisualInstance(GridObjectChannelItemKey.Standalone(-1), root, scopeNode, resolver);
            ApplyPreviewSpawnPosition(instance, item);
            TransformGridSharedUtility.SetUiElementVisible(instance.Resolver, false);
            return instance;
        }

        async UniTask InitializeSpawnedInstanceAsync(
            GridObjectChannelVisualInstance instance,
            GridObjectChannelResolvedItem item,
            CancellationToken ct)
        {
            instance.UpdateFromItem(item);
            var payload = BuildPayload(item);
            var commandVars = ApplyPayloadToBlackboard(instance, payload);

            await ExecuteSpawnCommandsAsync(item, instance, commandVars, ct);
            TransformGridSharedUtility.RefreshLayoutAndBounds(instance.Resolver);

            var startAnchor = ResolveSpawnAnchorLocalPosition(item);
            var startLocal = TransformGridSharedUtility.ResolvePlacementLocalPosition(
                instance.Resolver,
                instance.RootRect,
                startAnchor,
                (int)_resolvedLayoutPreset.ItemHorizontalAlignment,
                (int)_resolvedLayoutPreset.ItemVerticalAlignment);
            var targetLocal = TransformGridSharedUtility.ResolvePlacementLocalPosition(
                instance.Resolver,
                instance.RootRect,
                item.TargetLocalPosition,
                (int)_resolvedLayoutPreset.ItemHorizontalAlignment,
                (int)_resolvedLayoutPreset.ItemVerticalAlignment);

            TransformGridSharedUtility.SetLocalPosition(instance.Root, instance.RootRect, startLocal, _environmentKind);
            TransformGridSharedUtility.SetUiElementVisible(instance.Resolver, true);
            await AnimateInstanceAsync(instance, targetLocal, _resolvedLayoutPreset.SpawnMotion, ct);
        }

        async UniTask RelayoutInstanceAsync(
            GridObjectChannelVisualInstance instance,
            GridObjectChannelResolvedItem item,
            CancellationToken ct)
        {
            instance.UpdateFromItem(item);
            ApplyPayloadToBlackboard(instance, BuildPayload(item));
            TransformGridSharedUtility.RefreshLayoutAndBounds(instance.Resolver);
            var targetLocal = TransformGridSharedUtility.ResolvePlacementLocalPosition(
                instance.Resolver,
                instance.RootRect,
                item.TargetLocalPosition,
                (int)_resolvedLayoutPreset.ItemHorizontalAlignment,
                (int)_resolvedLayoutPreset.ItemVerticalAlignment);
            await AnimateInstanceAsync(instance, targetLocal, _resolvedLayoutPreset.RelayoutMotion, ct);
        }

        async UniTask AnimateInstanceAsync(
            GridObjectChannelVisualInstance instance,
            Vector3 targetLocal,
            GridObjectChannelMotionPreset motion,
            CancellationToken ct)
        {
            if (motion == null || motion.DurationSeconds <= 0f)
            {
                TransformGridSharedUtility.SetLocalPosition(instance.Root, instance.RootRect, targetLocal, _environmentKind);
                return;
            }

            if (motion.UseTransformAnimation &&
                TransformGridSharedUtility.TryResolveTransformAnimationPlayer(instance.Resolver, motion.TransformAnimationChannelTag, out var player) &&
                player != null)
            {
                var playerTarget = player.TargetTransform;
                if (playerTarget != null &&
                    (ReferenceEquals(playerTarget, instance.Root) || ReferenceEquals(playerTarget, instance.RootRect)))
                {
                    var motionTarget = TransformGridSharedUtility.ResolveMotionTargetPosition(
                        instance.RootRect,
                        targetLocal,
                        _environmentKind);
                    var step = new TransformAnimationPresetStep
                    {
                        operation = _environmentKind == TransformGridEnvironmentKind.ScreenUI && instance.RootRect != null
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
                        TransformGridSharedUtility.SetLocalPosition(instance.Root, instance.RootRect, targetLocal, _environmentKind);
                        return;
                    }

                    UniTask.Void(async () =>
                    {
                        try
                        {
                            await player.PlayStepAsync(motionTarget, step);
                            TransformGridSharedUtility.SetLocalPosition(instance.Root, instance.RootRect, targetLocal, _environmentKind);
                        }
                        catch (OperationCanceledException)
                        {
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[GridObjectChannel] TransformAnimation fallback triggered after channel failure. Tag='{Tag}' Message={ex.Message}");
                            await RunFallbackTweenAsync(instance, targetLocal, motion, CancellationToken.None);
                        }
                    });
                    return;
                }
            }

            await RunFallbackTweenAsync(instance, targetLocal, motion, ct);
        }

        async UniTask RunFallbackTweenAsync(
            GridObjectChannelVisualInstance instance,
            Vector3 targetLocal,
            GridObjectChannelMotionPreset motion,
            CancellationToken ct)
        {
            var start = instance.RootRect != null && _environmentKind == TransformGridEnvironmentKind.ScreenUI
                ? instance.RootRect.anchoredPosition3D
                : instance.Root.localPosition;
            var duration = motion.DurationSeconds;
            if (duration <= 0f)
            {
                TransformGridSharedUtility.SetLocalPosition(instance.Root, instance.RootRect, targetLocal, _environmentKind);
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
                        Debug.LogError($"[GridObjectChannel] Detached fallback tween failed. Tag='{Tag}' Message={ex.Message}");
                    }
                });
                return;
            }

            await RunFallbackTweenCoreAsync(instance, start, targetLocal, duration, motion.Ease, ct);
        }

        async UniTask RunFallbackTweenCoreAsync(
            GridObjectChannelVisualInstance instance,
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
                TransformGridSharedUtility.SetLocalPosition(instance.Root, instance.RootRect, next, _environmentKind);
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            TransformGridSharedUtility.SetLocalPosition(instance.Root, instance.RootRect, targetLocal, _environmentKind);
        }

        async UniTask ExecuteSpawnCommandsAsync(
            GridObjectChannelResolvedItem item,
            GridObjectChannelVisualInstance instance,
            IVarStore commandVars,
            CancellationToken ct)
        {
            if (!TryResolveCommandRunner(instance, out var runner) || runner == null)
                return;

            var counterVarId = ResolveVarId(_resolvedVisualizerPreset.CounterVar, VarIds.GameLib.Base.CommandVar.i);
            if (counterVarId > 0)
                commandVars.TrySetVariant(counterVarId, DynamicVariant.FromInt(item.ListIndex));

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
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GridObjectChannel] Spawn commands failed. Tag='{Tag}' Message={ex.Message}");
            }
        }

        VarStore BuildPayload(GridObjectChannelResolvedItem item)
        {
            var payload = new VarStore(initialCapacity: 32);
            ApplyItemVars(payload, item);
            ApplyCellValues(payload, item.CellValues);
            return payload;
        }

        IVarStore ApplyPayloadToBlackboard(GridObjectChannelVisualInstance instance, VarStore payload)
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

        void ApplyItemVars(IVarStore vars, GridObjectChannelResolvedItem item)
        {
            WriteVariant(vars, VarIds.GameLib.Channel.GridObjectChannel.Item.channelTag, DynamicVariant.FromString(Tag));
            WriteVariant(vars, VarIds.GameLib.Channel.GridObjectChannel.Item.listIndex, DynamicVariant.FromInt(item.ListIndex));
            WriteVariant(vars, VarIds.GameLib.Channel.GridObjectChannel.Item.row, DynamicVariant.FromInt(item.Row));
            WriteVariant(vars, VarIds.GameLib.Channel.GridObjectChannel.Item.column, DynamicVariant.FromInt(item.Column));
            WriteVariant(vars, VarIds.GameLib.Channel.GridObjectChannel.Item.sourceRow, DynamicVariant.FromInt(item.SourceRow));
            WriteVariant(vars, VarIds.GameLib.Channel.GridObjectChannel.Item.sourceColumn, DynamicVariant.FromInt(item.SourceColumn));
        }

        void ApplyCellValues(IVarStore vars, List<GridBlackboardCellSnapshot>? values)
        {
            if (vars == null || values == null)
                return;

            for (var i = 0; i < values.Count; i++)
            {
                var cell = values[i];
                if (cell.VarId == 0)
                    continue;

                if (cell.Value.Kind == ValueKind.ManagedRef)
                {
                    var managed = cell.Value.AsManagedRef;
                    if (managed != null)
                        vars.TrySetManagedRef(cell.VarId, managed);
                    continue;
                }

                vars.TrySetVariant(cell.VarId, cell.Value);
            }
        }

        Vector3 ResolveSpawnAnchorLocalPosition(GridObjectChannelResolvedItem item)
        {
            if (_resolvedLayoutPreset.SpawnAnchorMode == GridObjectChannelSpawnAnchorMode.LayoutTarget)
                return item.TargetLocalPosition + _resolvedLayoutPreset.SpawnOffset;

            var anchorLocal = Vector3.zero;
            if (_resolvedLayoutPreset.FixedAnchorTransform != null)
            {
                anchorLocal = TransformGridSharedUtility.ResolveLocalPointFromTransform(
                    _listRoot,
                    _layoutReferenceTransform,
                    _layoutRectTransform,
                    _canvas,
                    _resolvedLayoutPreset.FixedAnchorTransform,
                    _environmentKind);
            }
            else if (_resolvedLayoutPreset.UseFixedAnchorActorSource && _activeScope != null)
            {
                var scope = ActorSourceFastResolver.ResolveCached(
                    _activeScope,
                    _resolvedLayoutPreset.FixedAnchorActorSource,
                    ref _fixedAnchorSourceCache,
                    _activeScope);
                var transform = scope?.Identity?.SelfTransform;
                if (transform != null)
                {
                    anchorLocal = TransformGridSharedUtility.ResolveLocalPointFromTransform(
                        _listRoot,
                        _layoutReferenceTransform,
                        _layoutRectTransform,
                        _canvas,
                        transform,
                        _environmentKind);
                }
            }

            return anchorLocal + _resolvedLayoutPreset.SpawnOffset;
        }

        async UniTask DelayBetweenNewSpawnsIfNeededAsync(int initializedCount, int totalSpawnCount, CancellationToken ct)
        {
            if (initializedCount >= totalSpawnCount || !_resolvedVisualizerPreset.DelayBetweenSpawns.HasSource || _activeScope == null)
                return;

            var delay = _resolvedVisualizerPreset.DelayBetweenSpawns.GetOrDefault(
                new SimpleDynamicContext(ResolveVars(_activeScope), _activeScope),
                0f);
            if (delay <= 0f)
                return;

            await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: ct);
        }

        Vector2 ResolveLayoutItemSize(IReadOnlyList<GridObjectChannelVisualInstance> instances)
        {
            if (_resolvedVisualizerPreset.SizeSource == GridObjectChannelVisualizerSizeSource.Fixed)
                return _resolvedVisualizerPreset.FixedSize;

            for (var i = 0; i < instances.Count; i++)
            {
                var instance = instances[i];
                if (instance == null)
                    continue;

                if (TransformGridSharedUtility.TryResolveLayoutElementSize(
                        instance.Resolver,
                        instance.RootRect,
                        (int)_resolvedVisualizerPreset.SizeSource,
                        _resolvedVisualizerPreset.FixedSize,
                        out var size) &&
                    (size.x > 0f || size.y > 0f))
                {
                    return size;
                }
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

        Vector2 ResolveTemplateLayoutItemSize()
        {
            if (_resolvedVisualizerPreset.SizeSource == GridObjectChannelVisualizerSizeSource.Fixed)
                return _resolvedVisualizerPreset.FixedSize;

            var prefab = _resolvedRuntimeTemplate?.Prefab;
            if (prefab == null)
                return Vector2.zero;

            return TryGetTemplateRectSize(prefab.transform, out var rectSize) ? rectSize : Vector2.zero;
        }

        void ApplyPreviewSpawnPosition(GridObjectChannelVisualInstance instance, GridObjectChannelResolvedItem item)
        {
            TransformGridSharedUtility.RefreshLayoutAndBounds(instance.Resolver);
            var startAnchor = ResolveSpawnAnchorLocalPosition(item);
            var previewLocal = TransformGridSharedUtility.ResolvePlacementLocalPosition(
                instance.Resolver,
                instance.RootRect,
                startAnchor,
                (int)_resolvedLayoutPreset.ItemHorizontalAlignment,
                (int)_resolvedLayoutPreset.ItemVerticalAlignment);
            TransformGridSharedUtility.SetLocalPosition(instance.Root, instance.RootRect, previewLocal, _environmentKind);
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
                await ReleaseSpawnedInstanceAsync(instance.Root, instance.Scope, instance.Resolver);
            }

            _instances.Clear();
            _lookup.Clear();
        }

        void QueueRefresh(GridObjectChannelRefreshMode mode)
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
                    Debug.LogError($"[GridObjectChannel] Queued refresh failed. Tag='{Tag}' Message={ex.Message}");
                }
                finally
                {
                    _queueWorkerActive = false;
                }
            });
        }

        void SortInstancesByListIndex()
        {
            if (_instances.Count <= 1)
                return;

            _instances.Sort(static (a, b) => a.ListIndex.CompareTo(b.ListIndex));
        }

        static int ResolveTotalRows(IReadOnlyList<GridObjectChannelResolvedItem> items)
        {
            var maxRow = 0;
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i].Row > maxRow)
                    maxRow = items[i].Row;
            }

            return maxRow + 1;
        }

        static int ResolveTotalColumns(IReadOnlyList<GridObjectChannelResolvedItem> items)
        {
            var maxColumn = 0;
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i].Column > maxColumn)
                    maxColumn = items[i].Column;
            }

            return maxColumn + 1;
        }

        static GridObjectChannelRefreshMode CombineRefreshModes(
            GridObjectChannelRefreshMode a,
            GridObjectChannelRefreshMode b)
        {
            return GetRefreshPriority(a) <= GetRefreshPriority(b) ? a : b;
        }

        static int GetRefreshPriority(GridObjectChannelRefreshMode mode)
        {
            return mode switch
            {
                GridObjectChannelRefreshMode.FullRebuild => 0,
                GridObjectChannelRefreshMode.Incremental => 1,
                GridObjectChannelRefreshMode.LayoutOnly => 2,
                _ => 3,
            };
        }

        static IGridObjectChannelPlayerRuntime CreatePlayerRuntime(GridObjectChannelPlayerPresetBase preset)
        {
            return preset switch
            {
                GridObjectChannelGridBlackboardPlayerPreset gridPreset => new GridBlackboardPlayerRuntime(gridPreset),
                GridObjectChannelStandalonePlayerPreset standalonePreset => new StandalonePlayerRuntime(standalonePreset),
                _ => new StandalonePlayerRuntime(new GridObjectChannelStandalonePlayerPreset()),
            };
        }

        IAsyncSpawnerService? ResolveSpawner(ISceneSpawnerRegistry registry)
        {
            var primary = _environmentKind == TransformGridEnvironmentKind.ScreenUI
                ? SpawnerKind.RuntimeUIElement
                : SpawnerKind.RuntimeEntity;
            var fallback = primary == SpawnerKind.RuntimeUIElement
                ? SpawnerKind.RuntimeEntity
                : SpawnerKind.RuntimeUIElement;

            return registry.TryGet<IAsyncSpawnerService>(primary, "") ??
                   registry.TryGet<IAsyncSpawnerService>(fallback, "");
        }

        bool TryResolveCommandRunner(GridObjectChannelVisualInstance instance, out ICommandRunner? runner)
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

        static IVarStore ResolveVars(IScopeNode? scope)
        {
            if (scope?.Resolver != null &&
                scope.Resolver.TryResolve<IVarStore>(out var vars) &&
                vars != null)
            {
                return vars;
            }

            return NullVarStore.Instance;
        }

        static void EnsureScopeBuiltIfNeeded(IScopeNode? scope)
        {
            if (scope is BaseLifetimeScope baseScope)
            {
                baseScope.EnsureScopeBuilt();
                return;
            }

            if (scope is RuntimeLifetimeScope runtimeScope)
                runtimeScope.EnsureScopeBuilt();
        }

        static async UniTask ReleaseSpawnedInstanceAsync(
            Transform? root,
            IScopeNode? scope,
            IObjectResolver? resolver)
        {
            if (resolver == null)
                return;

            await UniTask.SwitchToMainThread();

            try
            {
                if (resolver.TryResolve<RuntimeLifetimeScope>(out var runtimeScope) && runtimeScope != null)
                {
                    if (runtimeScope.Resolver != null &&
                        runtimeScope.Resolver.TryResolve<IRuntimeLifetimeScopePool>(out var pool) &&
                        pool != null)
                    {
                        pool.Release(runtimeScope);
                        return;
                    }

                    if (root != null)
                        UnityEngine.Object.Destroy(root.gameObject);
                    else
                        UnityEngine.Object.Destroy(runtimeScope.gameObject);
                    return;
                }

                if (scope is BaseLifetimeScope baseScope)
                {
                    await baseScope.DespawnAsync(CancellationToken.None);
                    return;
                }

                if (root != null)
                    UnityEngine.Object.Destroy(root.gameObject);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GridObjectChannel] Release failed: {ex.Message}");
            }
        }

        static void ExtractSpawnedInfo(IObjectResolver? resolver, out Transform? root, out IScopeNode? scopeNode)
        {
            root = null;
            scopeNode = null;
            if (resolver == null)
                return;

            if (resolver.TryResolve<RuntimeLifetimeScope>(out var runtimeScope) && runtimeScope != null)
            {
                root = runtimeScope.transform;
                scopeNode = runtimeScope;
                return;
            }

            if (resolver.TryResolve<BaseLifetimeScope>(out var baseScope) && baseScope != null)
            {
                root = baseScope.transform;
                scopeNode = baseScope;
            }
        }

        static void WriteVariant(IVarStore vars, int varId, DynamicVariant value)
        {
            if (vars == null || varId == 0)
                return;
            vars.TrySetVariant(varId, value);
        }

        static int ResolveVarId(VarKeyRef key, int fallback)
        {
            if (!string.IsNullOrEmpty(key.StableKey) && VarIdResolver.TryResolve(key.StableKey, out var resolved) && resolved > 0)
                return resolved;

            return key.VarId > 0 ? key.VarId : fallback;
        }

        static CommandLtsSlot ResolveContextSlotOrDefault(CommandLtsSlot slot)
        {
            return CommandLtsSlotUtility.IsContextSlot(slot)
                ? slot
                : CommandLtsSlot.ContextA;
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
                    $"[GridObjectChannel] Re-entrant '{operationName}' was blocked to avoid deadlock. Tag='{Tag}'");
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
