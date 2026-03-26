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

        void RecalculateAnchoredPositions(
            List<UITraitListSlot> slots,
            UITraitListLayoutProfileSO profile,
            RectTransform? layoutRect,
            Vector2 itemSize);
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
                slots.Add(new UITraitListSlot(
                    trait,
                    traitIndex,
                    listIndex,
                    row,
                    column,
                    Vector2.zero,
                    profile.HorizontalAlignment,
                    profile.VerticalAlignment));
            }

            return true;
        }

        public void RecalculateAnchoredPositions(
            List<UITraitListSlot> slots,
            UITraitListLayoutProfileSO profile,
            RectTransform? layoutRect,
            Vector2 itemSize)
        {
            if (slots == null || slots.Count == 0 || profile == null)
                return;

            var rect = layoutRect != null ? layoutRect.rect : new Rect(0f, 0f, 0f, 0f);
            var rowsUsed = ResolveUsedRowCount(slots);
            var columnsUsed = ResolveUsedColumnCount(slots);
            var stepX = Mathf.Max(0f, itemSize.x) + Mathf.Max(0f, profile.ColumnSpacing);
            var stepY = Mathf.Max(0f, itemSize.y) + Mathf.Max(0f, profile.RowSpacing);

            var baseX = ResolveHorizontalBase(rect, profile.AreaHorizontalAlignment, columnsUsed, stepX);
            var baseY = ResolveVerticalBase(rect, profile.AreaVerticalAlignment, rowsUsed, stepY);
            var horizontalDirection = profile.AreaHorizontalAlignment == UITraitListHorizontalAlignment.Right ? -1f : 1f;
            var verticalDirection = profile.AreaVerticalAlignment == UITraitListVerticalAlignment.Bottom ? -1f : 1f;

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                slot.AnchoredPosition = ComputeAnchoredPosition(
                    profile,
                    slot.Row,
                    slot.Column,
                    baseX,
                    baseY,
                    stepX,
                    stepY,
                    horizontalDirection,
                    verticalDirection);
                slots[i] = slot;
            }
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

        static Vector2 ComputeAnchoredPosition(
            UITraitListLayoutProfileSO profile,
            int row,
            int column,
            float baseX,
            float baseY,
            float stepX,
            float stepY,
            float horizontalDirection,
            float verticalDirection)
        {
            var x = baseX + profile.Offset.x + (column * stepX * horizontalDirection);
            var y = baseY + profile.Offset.y - (row * stepY * verticalDirection);
            return new Vector2(x, y);
        }

        static int ResolveUsedRowCount(IReadOnlyList<UITraitListSlot> slots)
        {
            var maxRow = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].Row > maxRow)
                    maxRow = slots[i].Row;
            }

            return maxRow + 1;
        }

        static int ResolveUsedColumnCount(IReadOnlyList<UITraitListSlot> slots)
        {
            var maxColumn = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].Column > maxColumn)
                    maxColumn = slots[i].Column;
            }

            return maxColumn + 1;
        }

        static float ResolveHorizontalBase(Rect rect, UITraitListHorizontalAlignment alignment, int usedColumns, float stepX)
        {
            var span = Mathf.Max(0, usedColumns - 1) * stepX;
            return alignment switch
            {
                UITraitListHorizontalAlignment.Left => rect.xMin,
                UITraitListHorizontalAlignment.Right => rect.xMax,
                UITraitListHorizontalAlignment.Center => rect.center.x - (span * 0.5f),
                _ => rect.xMin
            };
        }

        static float ResolveVerticalBase(Rect rect, UITraitListVerticalAlignment alignment, int usedRows, float stepY)
        {
            var span = Mathf.Max(0, usedRows - 1) * stepY;
            return alignment switch
            {
                UITraitListVerticalAlignment.Top => rect.yMax,
                UITraitListVerticalAlignment.Bottom => rect.yMin,
                UITraitListVerticalAlignment.Center => rect.center.y + (span * 0.5f),
                _ => rect.yMax
            };
        }
    }
}
