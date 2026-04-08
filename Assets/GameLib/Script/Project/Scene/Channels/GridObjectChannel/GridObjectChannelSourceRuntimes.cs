#nullable enable
using System;
using System.Collections.Generic;
using Game.Commands.VNext;
using Game.Common;
using UnityEngine;
using VContainer;

namespace Game.Channel
{
    internal sealed class GridObjectChannelSourceContext
    {
        public GridObjectChannelSourceContext(
            IDynamicContext dynamicContext,
            IScopeNode activeScope,
            Action<GridObjectChannelRefreshMode> queueRefresh)
        {
            DynamicContext = dynamicContext;
            ActiveScope = activeScope;
            QueueRefresh = queueRefresh;
        }

        public IDynamicContext DynamicContext { get; }
        public IScopeNode ActiveScope { get; }
        public Action<GridObjectChannelRefreshMode> QueueRefresh { get; }
    }

    internal interface IGridObjectChannelItemSourceRuntime : IDisposable
    {
        GridObjectChannelPlayerPresetBase Preset { get; }
        GridObjectChannelRefreshMode RefreshMode { get; }
        int DebounceFrames { get; }
        bool PreserveSourceCoordinates { get; }
        int RowOffset { get; }
        int ColumnOffset { get; }
        void ResolveSourceLayoutCoordinates(int sourceRow, int sourceColumn, out int row, out int column);
        bool TryResolve(GridObjectChannelSourceContext context, out string? error);
        bool TryBuildItems(GridObjectChannelSourceContext context, List<GridObjectChannelResolvedItem> items, out string? error);
    }

    internal static class GridObjectChannelPlayerRuntimeFactory
    {
        public static IGridObjectChannelItemSourceRuntime Create(GridObjectChannelPlayerPresetBase preset)
        {
            return preset switch
            {
                GridObjectChannelGridBlackboardPlayerPreset gridPreset => new GridObjectChannelGridBlackboardSourceRuntime(gridPreset),
                GridObjectChannelStandalonePlayerPreset standalonePreset => new GridObjectChannelStandaloneSourceRuntime(standalonePreset),
                _ => new GridObjectChannelStandaloneSourceRuntime(new GridObjectChannelStandalonePlayerPreset()),
            };
        }
    }

    internal sealed class GridObjectChannelStandaloneSourceRuntime : IGridObjectChannelItemSourceRuntime
    {
        readonly GridObjectChannelStandalonePlayerPreset _preset;

        public GridObjectChannelStandaloneSourceRuntime(GridObjectChannelStandalonePlayerPreset preset)
        {
            _preset = preset ?? throw new ArgumentNullException(nameof(preset));
        }

        public GridObjectChannelPlayerPresetBase Preset => _preset;
        public GridObjectChannelRefreshMode RefreshMode => _preset.RefreshMode;
        public int DebounceFrames => _preset.DebounceFrames;
        public bool PreserveSourceCoordinates => false;
        public int RowOffset => 0;
        public int ColumnOffset => 0;

        public void ResolveSourceLayoutCoordinates(int sourceRow, int sourceColumn, out int row, out int column)
        {
            row = sourceRow;
            column = sourceColumn;
        }

        public bool TryResolve(GridObjectChannelSourceContext context, out string? error)
        {
            _ = context;
            error = null;
            return true;
        }

        public bool TryBuildItems(GridObjectChannelSourceContext context, List<GridObjectChannelResolvedItem> items, out string? error)
        {
            items.Clear();
            var count = Mathf.Max(0, _preset.Count.GetOrDefault(context.DynamicContext, 0));
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

    internal sealed class GridObjectChannelGridBlackboardSourceRuntime : IGridObjectChannelItemSourceRuntime
    {
        readonly GridObjectChannelGridBlackboardPlayerPreset _preset;
        readonly List<GridBlackboardCellSnapshot> _allCells = new(128);
        readonly List<GridBlackboardCellSnapshot> _cellBuffer = new(16);

        ActorSourceResolveCache _gridActorCache;
        IGridBlackboardService? _grid;
        Action<GridObjectChannelRefreshMode>? _queueRefresh;
        int _rowOffset;
        int _columnOffset;

        public GridObjectChannelGridBlackboardSourceRuntime(GridObjectChannelGridBlackboardPlayerPreset preset)
        {
            _preset = preset ?? throw new ArgumentNullException(nameof(preset));
        }

        public GridObjectChannelPlayerPresetBase Preset => _preset;
        public GridObjectChannelRefreshMode RefreshMode => _preset.RefreshMode;
        public int DebounceFrames => _preset.DebounceFrames;
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

        public bool TryResolve(GridObjectChannelSourceContext context, out string? error)
        {
            _queueRefresh = context.QueueRefresh;
            _rowOffset = _preset.RowOffset.GetOrDefault(context.DynamicContext, 0);
            _columnOffset = _preset.ColumnOffset.GetOrDefault(context.DynamicContext, 0);

            var scope = ActorSourceFastResolver.ResolveCached(context.ActiveScope, _preset.GridBlackboardActorSource, ref _gridActorCache);
            GridObjectChannelRuntimeUtility.EnsureScopeBuiltIfNeeded(scope);
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

        public bool TryBuildItems(GridObjectChannelSourceContext context, List<GridObjectChannelResolvedItem> items, out string? error)
        {
            _ = context;
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

            var filterVarId = _preset.UseGridKeyFilter ? GridObjectChannelRuntimeUtility.ResolveVarId(_preset.GridKey, 0) : 0;
            var elementConditionVarId = ResolveElementConditionVarId();
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
                    AddCurrentCellIfNeeded(items, filterVarId, elementConditionVarId, context.DynamicContext, ref listIndex, currentRow, currentColumn);
                    _cellBuffer.Clear();
                    currentRow = cell.Row;
                    currentColumn = cell.Column;
                }

                _cellBuffer.Add(cell);
            }

            AddCurrentCellIfNeeded(items, filterVarId, elementConditionVarId, context.DynamicContext, ref listIndex, currentRow, currentColumn);
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
            int elementConditionVarId,
            IDynamicContext dynamicContext,
            ref int listIndex,
            int sourceRow,
            int sourceColumn)
        {
            if (_cellBuffer.Count == 0)
                return;

            if (filterVarId > 0 && !HasEnabledFilterValue(_cellBuffer, filterVarId))
                return;

            if (elementConditionVarId > 0 &&
                !EvaluateElementCondition(_cellBuffer, elementConditionVarId, dynamicContext, sourceRow, sourceColumn))
            {
                return;
            }

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

        int ResolveElementConditionVarId()
        {
            var condition = _preset.ElementCondition;
            if (!condition.Enabled)
                return 0;

            return GridObjectChannelRuntimeUtility.ResolveVarId(condition.Key, 0);
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

        static bool EvaluateElementCondition(
            List<GridBlackboardCellSnapshot> values,
            int conditionVarId,
            IDynamicContext dynamicContext,
            int sourceRow,
            int sourceColumn)
        {
            for (var i = 0; i < values.Count; i++)
            {
                var cell = values[i];
                if (cell.VarId != conditionVarId)
                    continue;

                var conditionValue = cell.Value;
                if (conditionValue.Kind == ValueKind.ManagedRef)
                {
                    var managed = conditionValue.AsManagedRef;
                    if (managed != null &&
                        DeferredDynamicVarResolver.TryResolve(
                            managed,
                            dynamicContext,
                            $"GridObjectChannel.ElementCondition:{conditionVarId}@{sourceRow},{sourceColumn}",
                            out var resolved))
                    {
                        conditionValue = resolved;
                    }
                }

                return conditionValue.TryGet<bool>(out var enabled) && enabled;
            }

            // Key not found on this element => visible.
            return true;
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
}
