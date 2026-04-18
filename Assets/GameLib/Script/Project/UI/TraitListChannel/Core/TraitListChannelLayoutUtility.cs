#nullable enable
using System;
using System.Collections.Generic;
using Game.Trait;
using UnityEngine;

namespace Game.UI
{
    internal static class TraitListChannelLayoutUtility
    {
        readonly struct TraitDisplayEntry
        {
            public TraitDisplayEntry(ITraitInstance trait, int traitIndex, int duplicateCount, string displayKey)
            {
                Trait = trait;
                TraitIndex = traitIndex;
                DuplicateCount = duplicateCount;
                DisplayKey = displayKey;
            }

            public ITraitInstance Trait { get; }
            public int TraitIndex { get; }
            public int DuplicateCount { get; }
            public string DisplayKey { get; }
        }

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
            bool mergeDuplicateTraitDefinitions,
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

            var displayTraits = BuildDisplayTraits(traits, mergeDuplicateTraitDefinitions);
            var totalCount = displayTraits.Count;
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

            if (totalCount == 0)
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

                var entry = displayTraits[traitIndex];
                var (row, column) = ResolveRowColumn(preset.Order, listIndex, preset.Rows, preset.Columns);
                slots.Add(new TraitListChannelSlot
                {
                    Trait = entry.Trait,
                    TraitIndex = entry.TraitIndex,
                    DisplayKey = entry.DisplayKey,
                    DuplicateCount = Mathf.Max(1, entry.DuplicateCount),
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

        static List<TraitDisplayEntry> BuildDisplayTraits(IReadOnlyList<ITraitInstance> traits, bool mergeDuplicateTraitDefinitions)
        {
            var results = new List<TraitDisplayEntry>(traits?.Count ?? 0);
            if (traits == null || traits.Count == 0)
                return results;

            if (!mergeDuplicateTraitDefinitions)
            {
                for (var i = 0; i < traits.Count; i++)
                {
                    var trait = traits[i];
                    if (trait == null)
                        continue;

                    results.Add(new TraitDisplayEntry(
                        trait,
                        i,
                        duplicateCount: 1,
                        BuildDisplayKey(trait, mergeDuplicateTraitDefinitions: false)));
                }

                return results;
            }

            var entryIndexesByDefinitionId = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < traits.Count; i++)
            {
                var trait = traits[i];
                if (trait == null)
                    continue;

                var definitionId = trait.Definition?.DefinitionId;
                if (string.IsNullOrWhiteSpace(definitionId))
                {
                    results.Add(new TraitDisplayEntry(
                        trait,
                        i,
                        duplicateCount: 1,
                        BuildDisplayKey(trait, mergeDuplicateTraitDefinitions: false)));
                    continue;
                }

                if (entryIndexesByDefinitionId.TryGetValue(definitionId, out var entryIndex))
                {
                    var entry = results[entryIndex];
                    results[entryIndex] = new TraitDisplayEntry(
                        entry.Trait,
                        entry.TraitIndex,
                        entry.DuplicateCount + 1,
                        entry.DisplayKey);
                    continue;
                }

                entryIndexesByDefinitionId[definitionId] = results.Count;
                results.Add(new TraitDisplayEntry(
                    trait,
                    i,
                    duplicateCount: 1,
                    BuildDisplayKey(trait, mergeDuplicateTraitDefinitions: true)));
            }

            return results;
        }

        static string BuildDisplayKey(ITraitInstance trait, bool mergeDuplicateTraitDefinitions)
        {
            var definitionId = trait.Definition?.DefinitionId ?? string.Empty;
            if (mergeDuplicateTraitDefinitions && !string.IsNullOrWhiteSpace(definitionId))
                return definitionId.Trim();

            var instanceId = trait.InstanceId ?? string.Empty;
            return string.IsNullOrWhiteSpace(definitionId)
                ? instanceId
                : $"{definitionId.Trim()}/{instanceId}";
        }

        public static void RecalculateTargetPositions(
            List<TraitListChannelSlot> slots,
            TraitListChannelLayoutPreset preset,
            Rect layoutRect,
            Vector2 itemSize)
        {
            if (slots == null || slots.Count == 0 || preset == null)
                return;

            var rowsUsed = ResolveUsedRowCount(slots);
            var columnsUsed = ResolveUsedColumnCount(slots);

            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                slot.TargetLocalPosition = TransformGridSharedUtility.ResolveTargetLocalPosition(
                    layoutRect,
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
