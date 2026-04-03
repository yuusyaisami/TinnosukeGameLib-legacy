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

            var rowsUsed = ResolveUsedRowCount(slots);
            var columnsUsed = ResolveUsedColumnCount(slots);
            var rect = layoutRect != null ? layoutRect.rect : new Rect(0f, 0f, 0f, 0f);

            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                slot.TargetLocalPosition = TransformGridSharedUtility.ResolveTargetLocalPosition(
                    rect,
                    slot.Row,
                    slot.Column,
                    rowsUsed,
                    columnsUsed,
                    itemSize,
                    preset.RowSpacing,
                    preset.ColumnSpacing,
                    (int)preset.AreaHorizontalAlignment,
                    (int)preset.AreaVerticalAlignment,
                    preset.ItemOffset);
                slots[i] = slot;
            }
        }

        static (int row, int column) ResolveRowColumn(
            TraitListChannelOrder order,
            int listIndex,
            int rows,
            int columns)
        {
            return TransformGridSharedUtility.ResolveRowColumn((int)order, listIndex, rows, columns);
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
    }
}
