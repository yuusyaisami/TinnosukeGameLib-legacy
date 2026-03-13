#nullable enable
using System.Collections.Generic;
using Game;
using Game.Trait;
using Game.UI;
using UnityEngine;

namespace Game.UI.TraitList
{
    public interface IUITraitListLayoutService
    {
        int GetCapacity(UITraitListLayoutProfileSO profile);

        bool TryBuildSlots(
            IReadOnlyList<ITraitInstance> traits,
            UITraitListRange range,
            UITraitListLayoutProfileSO profile,
            out List<UITraitListSlot> slots,
            out UITraitListRange normalizedRange,
            out string? error);
    }

    public sealed class UITraitListLayoutService :
        IUITraitListLayoutService,
        IUITransformListLayoutService<ITraitInstance, UITraitListSlot, UITraitListLayoutProfileSO>,
        IScopeAcquireHandler,
        IScopeReleaseHandler
    {
        public void OnAcquire(IScopeNode scope, bool isReset)
        {
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
        }

        public int GetCapacity(UITraitListLayoutProfileSO profile)
        {
            if (profile == null)
                return 0;

            var rows = Mathf.Max(1, profile.Rows);
            var columns = Mathf.Max(1, profile.Columns);
            return rows * columns;
        }

        public bool TryBuildSlots(
            IReadOnlyList<ITraitInstance> traits,
            UITraitListRange range,
            UITraitListLayoutProfileSO profile,
            out List<UITraitListSlot> slots,
            out UITraitListRange normalizedRange,
            out string? error)
        {
            slots = new List<UITraitListSlot>();
            normalizedRange = range;
            error = null;

            if (profile == null)
            {
                error = "LayoutProfile is null.";
                return false;
            }

            var totalCount = traits?.Count ?? 0;
            normalizedRange = range.Normalize(totalCount);
            var capacity = GetCapacity(profile);
            if (capacity <= 0)
            {
                error = "Layout capacity is 0.";
                return false;
            }

            if (normalizedRange.Count > capacity)
            {
                error = $"RangeCount ({normalizedRange.Count}) exceeds capacity ({capacity}).";
                return false;
            }

            if (traits == null || totalCount == 0)
                return true;

            var startIndex = normalizedRange.StartIndex;
            if (startIndex < 0)
                startIndex = 0;

            var available = totalCount - startIndex;
            if (available <= 0 || normalizedRange.Count <= 0)
                return true;

            var count = Mathf.Min(available, normalizedRange.Count);
            slots = new List<UITraitListSlot>(count);

            var rows = Mathf.Max(1, profile.Rows);
            var columns = Mathf.Max(1, profile.Columns);
            for (int listIndex = 0; listIndex < count; listIndex++)
            {
                var traitIndex = startIndex + listIndex;
                if (traitIndex < 0 || traitIndex >= totalCount)
                    break;

                var trait = traits[traitIndex];
                var (row, column) = ResolveRowColumn(profile.Order, listIndex, rows, columns);
                var pos = ComputeAnchoredPosition(profile, row, column);
                slots.Add(new UITraitListSlot(trait, traitIndex, listIndex, row, column, pos));
            }

            return true;
        }

        static (int row, int column) ResolveRowColumn(UITraitListOrder order, int listIndex, int rows, int columns)
        {
            if (order == UITraitListOrder.ColumnMajor)
            {
                var row = listIndex % rows;
                var column = listIndex / rows;
                return (row, column);
            }

            var r = listIndex / columns;
            var c = listIndex % columns;
            return (r, c);
        }

        static Vector2 ComputeAnchoredPosition(UITraitListLayoutProfileSO profile, int row, int column)
        {
            var x = profile.Offset.x + column * profile.ColumnSpacing;
            var y = profile.Offset.y - row * profile.RowSpacing;
            return new Vector2(x, y);
        }
    }
}
