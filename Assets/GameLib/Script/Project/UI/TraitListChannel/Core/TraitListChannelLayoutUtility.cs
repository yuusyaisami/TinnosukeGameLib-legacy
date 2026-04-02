#nullable enable
using System.Collections.Generic;
using Game.Trait;
using UnityEngine;

namespace Game.UI
{
    internal static class TraitListChannelLayoutUtility
    {
        public static int GetCapacity(TraitListChannelLayoutPreset preset)
        {
            if (preset == null)
                return 0;

            return Mathf.Max(1, preset.Rows) * Mathf.Max(1, preset.Columns);
        }

        public static bool TryBuildSlots(
            IReadOnlyList<ITraitInstance> traits,
            string channelTag,
            string holderKey,
            bool useRange,
            TraitListChannelRange range,
            TraitListChannelLayoutPreset preset,
            out List<TraitListChannelSlot> slots,
            out TraitListChannelRange normalizedRange,
            out string? error)
        {
            slots = new List<TraitListChannelSlot>();
            normalizedRange = range;
            error = null;

            if (preset == null)
            {
                error = "Layout preset is null.";
                return false;
            }

            var totalCount = traits?.Count ?? 0;
            var capacity = GetCapacity(preset);
            if (capacity <= 0)
            {
                error = "Layout capacity is 0.";
                return false;
            }

            normalizedRange = useRange
                ? range.Normalize(totalCount)
                : new TraitListChannelRange(0, Mathf.Min(totalCount, capacity));

            if (useRange && normalizedRange.Count > capacity)
            {
                error = $"RangeCount ({normalizedRange.Count}) exceeds capacity ({capacity}).";
                return false;
            }

            if (traits == null || totalCount == 0)
                return true;

            var startIndex = useRange ? Mathf.Max(0, normalizedRange.StartIndex) : 0;
            var available = totalCount - startIndex;
            if (available <= 0 || normalizedRange.Count <= 0)
                return true;

            var count = useRange
                ? Mathf.Min(available, normalizedRange.Count)
                : Mathf.Min(available, capacity);
            slots = new List<TraitListChannelSlot>(count);
            for (var listIndex = 0; listIndex < count; listIndex++)
            {
                var traitIndex = startIndex + listIndex;
                if (traitIndex < 0 || traitIndex >= totalCount)
                    break;

                var (row, column) = ResolveRowColumn(preset.Order, listIndex, preset.Rows, preset.Columns);
                slots.Add(new TraitListChannelSlot
                {
                    Trait = traits[traitIndex],
                    TraitIndex = traitIndex,
                    ListIndex = listIndex,
                    Row = row,
                    Column = column,
                    TargetLocalPosition = Vector3.zero,
                    ItemHorizontalAlignment = preset.ItemHorizontalAlignment,
                    ItemVerticalAlignment = preset.ItemVerticalAlignment,
                    ChannelTag = channelTag ?? string.Empty,
                    HolderKey = holderKey ?? string.Empty,
                    RangeStart = normalizedRange.StartIndex,
                    RangeCount = normalizedRange.Count,
                });
            }

            return true;
        }

        public static void RecalculateTargetPositions(
            List<TraitListChannelSlot> slots,
            TraitListChannelLayoutPreset preset,
            RectTransform? layoutRect,
            Vector2 itemSize)
        {
            if (slots == null || slots.Count == 0 || preset == null)
                return;

            var rect = layoutRect != null ? layoutRect.rect : new Rect(0f, 0f, 0f, 0f);
            var rowsUsed = ResolveUsedRowCount(slots);
            var columnsUsed = ResolveUsedColumnCount(slots);
            var stepX = Mathf.Max(0f, itemSize.x) + preset.ColumnSpacing;
            var stepY = Mathf.Max(0f, itemSize.y) + preset.RowSpacing;

            var baseX = ResolveHorizontalBase(rect, preset.AreaHorizontalAlignment, columnsUsed, stepX);
            var baseY = ResolveVerticalBase(rect, preset.AreaVerticalAlignment, rowsUsed, stepY);
            var horizontalDirection = preset.AreaHorizontalAlignment == TraitListChannelHorizontalAlignment.Right ? -1f : 1f;
            var verticalDirection = preset.AreaVerticalAlignment == TraitListChannelVerticalAlignment.Bottom ? -1f : 1f;

            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                var x = baseX + preset.ItemOffset.x + (slot.Column * stepX * horizontalDirection);
                var y = baseY + preset.ItemOffset.y - (slot.Row * stepY * verticalDirection);
                slot.TargetLocalPosition = new Vector3(x, y, preset.ItemOffset.z);
                slots[i] = slot;
            }
        }

        static (int row, int column) ResolveRowColumn(
            TraitListChannelOrder order,
            int listIndex,
            int rows,
            int columns)
        {
            if (order == TraitListChannelOrder.ColumnMajor)
            {
                var safeColumns = Mathf.Max(1, columns);
                return (listIndex / safeColumns, listIndex % safeColumns);
            }

            var safeRows = Mathf.Max(1, rows);
            return (listIndex / Mathf.Max(1, columns), listIndex % Mathf.Max(1, columns));
        }

        static int ResolveUsedRowCount(IReadOnlyList<TraitListChannelSlot> slots)
        {
            var maxRow = 0;
            for (var i = 0; i < slots.Count; i++)
            {
                if (slots[i].Row > maxRow)
                    maxRow = slots[i].Row;
            }

            return maxRow + 1;
        }

        static int ResolveUsedColumnCount(IReadOnlyList<TraitListChannelSlot> slots)
        {
            var maxColumn = 0;
            for (var i = 0; i < slots.Count; i++)
            {
                if (slots[i].Column > maxColumn)
                    maxColumn = slots[i].Column;
            }

            return maxColumn + 1;
        }

        static float ResolveHorizontalBase(
            Rect rect,
            TraitListChannelHorizontalAlignment alignment,
            int usedColumns,
            float stepX)
        {
            var span = Mathf.Max(0, usedColumns - 1) * stepX;
            return alignment switch
            {
                TraitListChannelHorizontalAlignment.Left => rect.xMin,
                TraitListChannelHorizontalAlignment.Right => rect.xMax,
                TraitListChannelHorizontalAlignment.Center => rect.center.x - (span * 0.5f),
                _ => rect.xMin,
            };
        }

        static float ResolveVerticalBase(
            Rect rect,
            TraitListChannelVerticalAlignment alignment,
            int usedRows,
            float stepY)
        {
            var span = Mathf.Max(0, usedRows - 1) * stepY;
            return alignment switch
            {
                TraitListChannelVerticalAlignment.Top => rect.yMax,
                TraitListChannelVerticalAlignment.Bottom => rect.yMin,
                TraitListChannelVerticalAlignment.Center => rect.center.y + (span * 0.5f),
                _ => rect.yMax,
            };
        }
    }
}
