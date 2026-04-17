#nullable enable
using System;
using System.Collections.Generic;
using Game.Common;
using Game.DI;
using Game.UI;
using UnityEngine;

namespace Game.Channel
{
    internal sealed class GridObjectChannelLayoutPlanContext
    {
        public GridObjectChannelLayoutPlanContext(
            GridObjectChannelRuntimeState state,
            IDynamicContext dynamicContext)
        {
            State = state;
            DynamicContext = dynamicContext;
        }

        public GridObjectChannelRuntimeState State { get; }
        public IDynamicContext DynamicContext { get; }
    }

    internal sealed class GridObjectChannelLayoutPlanner
    {
        public bool TryBuildResolvedItems(
            GridObjectChannelLayoutPlanContext context,
            List<GridObjectChannelResolvedItem> items,
            out string? error)
        {
            items.Clear();
            error = null;

            var state = context.State;
            var itemSourceRuntime = state.ItemSourceRuntime;
            if (itemSourceRuntime == null || state.ActiveScope == null)
            {
                error = "Player runtime is null.";
                return false;
            }

            var sourceContext = new GridObjectChannelSourceContext(context.DynamicContext, state.ActiveScope, static _ => { });
            if (!itemSourceRuntime.TryBuildItems(sourceContext, items, out error))
                return false;

            var rows = Mathf.Max(1, state.ResolvedLayoutPreset.Rows.GetOrDefault(context.DynamicContext, 1));
            var columns = Mathf.Max(1, state.ResolvedLayoutPreset.Columns.GetOrDefault(context.DynamicContext, 1));
            var canUseSourceCoordinates = !itemSourceRuntime.PreserveSourceCoordinates &&
                                          items.Count > 0 &&
                                          items[0].SourceRow >= 0 &&
                                          items[0].SourceColumn >= 0;

            int[] candidateRows = Array.Empty<int>();
            int[] candidateColumns = Array.Empty<int>();
            Dictionary<int, int>? rowIndexMap = null;
            Dictionary<int, int>? columnIndexMap = null;

            if (canUseSourceCoordinates)
            {
                candidateRows = new int[items.Count];
                candidateColumns = new int[items.Count];

                var occupiedRows = new List<int>(items.Count);
                var occupiedColumns = new List<int>(items.Count);

                for (var i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    itemSourceRuntime.ResolveSourceLayoutCoordinates(item.SourceRow, item.SourceColumn, out var sourceLayoutRow, out var sourceLayoutColumn);

                    var candidateRow = sourceLayoutRow - itemSourceRuntime.RowOffset;
                    var candidateColumn = sourceLayoutColumn - itemSourceRuntime.ColumnOffset;
                    candidateRows[i] = candidateRow;
                    candidateColumns[i] = candidateColumn;

                    if (candidateRow < 0 || candidateColumn < 0)
                        continue;

                    occupiedRows.Add(candidateRow);
                    occupiedColumns.Add(candidateColumn);
                }

                SortAndDeduplicate(occupiedRows);
                SortAndDeduplicate(occupiedColumns);

                rowIndexMap = BuildDenseIndexMap(occupiedRows);
                columnIndexMap = BuildDenseIndexMap(occupiedColumns);
            }

            var writeIndex = 0;
            var filteredByRangeCount = 0;

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                int layoutRow;
                int layoutColumn;
                if (itemSourceRuntime.PreserveSourceCoordinates)
                {
                    itemSourceRuntime.ResolveSourceLayoutCoordinates(item.SourceRow, item.SourceColumn, out var sourceLayoutRow, out var sourceLayoutColumn);
                    layoutRow = sourceLayoutRow - itemSourceRuntime.RowOffset;
                    layoutColumn = sourceLayoutColumn - itemSourceRuntime.ColumnOffset;
                }
                else if (canUseSourceCoordinates)
                {
                    var candidateRow = candidateRows[i];
                    var candidateColumn = candidateColumns[i];
                    if (candidateRow < 0 || candidateColumn < 0 ||
                        rowIndexMap == null || columnIndexMap == null ||
                        !rowIndexMap.TryGetValue(candidateRow, out layoutRow) ||
                        !columnIndexMap.TryGetValue(candidateColumn, out layoutColumn))
                    {
                        filteredByRangeCount++;
                        continue;
                    }
                }
                else
                {
                    var (row, column) = TransformGridSharedUtility.ResolveRowColumn((int)state.ResolvedLayoutPreset.Order, item.ListIndex, rows, columns);
                    layoutRow = row - itemSourceRuntime.RowOffset;
                    layoutColumn = column - itemSourceRuntime.ColumnOffset;
                }

                if (layoutRow < 0 || layoutColumn < 0 || layoutRow >= rows || layoutColumn >= columns)
                {
                    filteredByRangeCount++;
                    continue;
                }

                item.Row = layoutRow;
                item.Column = layoutColumn;

                items[writeIndex++] = item;
            }

            if (writeIndex < items.Count)
                items.RemoveRange(writeIndex, items.Count - writeIndex);

            if (state.EnableVerboseLayoutLog)
            {
                Debug.Log(
                    $"[GridObjectChannel] LayoutPlanner resolved items. Input={writeIndex + filteredByRangeCount} Output={writeIndex} " +
                    $"Filtered={filteredByRangeCount} Rows={rows} Columns={columns} " +
                    $"RowOffset={itemSourceRuntime.RowOffset} ColumnOffset={itemSourceRuntime.ColumnOffset}",
                    state.ListRoot);
            }

            return true;
        }

        static void SortAndDeduplicate(List<int> values)
        {
            if (values.Count <= 1)
                return;

            values.Sort();

            var writeIndex = 1;
            for (var i = 1; i < values.Count; i++)
            {
                if (values[i] == values[writeIndex - 1])
                    continue;

                values[writeIndex++] = values[i];
            }

            if (writeIndex < values.Count)
                values.RemoveRange(writeIndex, values.Count - writeIndex);
        }

        static Dictionary<int, int> BuildDenseIndexMap(List<int> values)
        {
            var map = new Dictionary<int, int>(values.Count);
            for (var i = 0; i < values.Count; i++)
                map[values[i]] = i;

            return map;
        }

        public void RecalculateItemPositions(GridObjectChannelLayoutPlanContext context, List<GridObjectChannelResolvedItem> items)
        {
            if (items.Count == 0)
                return;

            var state = context.State;
            var itemSize = ResolvePlanningItemSize(state);
            var totalRows = ResolveTotalRows(items);
            var totalColumns = ResolveTotalColumns(items);
            var rect = TransformGridSharedUtility.ResolveLayoutRect(
                state.ListRoot,
                state.LayoutReferenceTransform,
                state.LayoutRectTransform,
                state.Canvas,
                state.ActiveScope,
                state.EnvironmentKind,
                state.ResolvedLayoutPreset.RangeSourceMode,
                state.ResolvedLayoutPreset.AreaActorSource,
                ref state.LayoutAreaSourceCache,
                state.ResolvedLayoutPreset.AreaChannelTag);

            if (state.EnableVerboseLayoutLog)
            {
                Debug.Log(
                    $"[GridObjectChannel] Layout rect resolved. Channel={state.ChannelTag} Env={state.EnvironmentKind} " +
                    $"Rect={rect} ListRoot={DescribeTransform(state.ListRoot)} LayoutRef={DescribeTransform(state.LayoutReferenceTransform)} " +
                    $"LayoutRect={DescribeRectTransform(state.LayoutRectTransform)} ItemSize={itemSize} TotalRows={totalRows} TotalColumns={totalColumns} " +
                    $"RowSpacing={state.ResolvedLayoutPreset.RowSpacing} ColumnSpacing={state.ResolvedLayoutPreset.ColumnSpacing} " +
                    $"AreaAlign={state.ResolvedLayoutPreset.AreaHorizontalAlignment}/{state.ResolvedLayoutPreset.AreaVerticalAlignment} " +
                    $"ItemAlign={state.ResolvedLayoutPreset.ItemHorizontalAlignment}/{state.ResolvedLayoutPreset.ItemVerticalAlignment} " +
                    $"ItemOffset={state.ResolvedLayoutPreset.ItemOffset}",
                    state.ListRoot);
            }

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
                    state.ResolvedLayoutPreset.RowSpacing,
                    state.ResolvedLayoutPreset.ColumnSpacing,
                    (int)state.ResolvedLayoutPreset.AreaHorizontalAlignment,
                    (int)state.ResolvedLayoutPreset.AreaVerticalAlignment,
                    state.ResolvedLayoutPreset.ItemOffset);
                items[i] = item;

                if (state.EnableVerboseLayoutLog)
                {
                    Debug.Log(
                        $"[GridObjectChannel] Item target resolved. Channel={state.ChannelTag} Index={i} Key={item.Key.Kind}:{item.Key.ValueA},{item.Key.ValueB} " +
                        $"SourceRow={item.SourceRow} SourceColumn={item.SourceColumn} Row={item.Row} Column={item.Column} TargetLocal={item.TargetLocalPosition}",
                        state.ListRoot);
                }
            }
        }

        public Vector2 ResolvePlanningItemSize(GridObjectChannelRuntimeState state)
        {
            var current = ResolveLayoutItemSize(state.ResolvedVisualizerPreset, state.Visuals.Items);
            if (current.x > 0f || current.y > 0f)
                return current;

            return ResolveTemplateLayoutItemSize(state.ResolvedVisualizerPreset, state.ResolvedRuntimeTemplate);
        }

        Vector2 ResolveLayoutItemSize(
            GridObjectChannelVisualizerPreset visualizerPreset,
            IReadOnlyList<GridObjectChannelVisualInstance> instances)
        {
            if (visualizerPreset.SizeSource == GridObjectChannelVisualizerSizeSource.Fixed)
                return visualizerPreset.FixedSize;

            for (var i = 0; i < instances.Count; i++)
            {
                var instance = instances[i];
                if (instance == null)
                    continue;

                if (TransformGridSharedUtility.TryResolveLayoutElementSize(
                        instance.Resolver,
                        instance.Root,
                        instance.RootRect,
                        (int)visualizerPreset.SizeSource,
                        visualizerPreset.FixedSize,
                        out var size) &&
                    (size.x > 0f || size.y > 0f))
                {
                    return size;
                }
            }

            return Vector2.zero;
        }

        static Vector2 ResolveTemplateLayoutItemSize(
            GridObjectChannelVisualizerPreset visualizerPreset,
            BaseRuntimeTemplateSO? runtimeTemplate)
        {
            if (visualizerPreset.SizeSource == GridObjectChannelVisualizerSizeSource.Fixed)
                return visualizerPreset.FixedSize;

            var prefab = runtimeTemplate?.Prefab;
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
