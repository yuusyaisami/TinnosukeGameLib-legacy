#nullable enable
using System;
using System.Collections.Generic;
using Game;
using Game.Trait;
using UnityEngine;
using VContainer;

namespace Game.UI.TraitList
{
    public enum UITraitListOrder
    {
        RowMajor = 0,
        ColumnMajor = 1,
    }

    public enum UITraitListHorizontalAlignment
    {
        Left = 10,
        Center = 20,
        Right = 30,
    }

    public enum UITraitListVerticalAlignment
    {
        Top = 10,
        Center = 20,
        Bottom = 30,
    }

    public enum UITraitListSpawnSource
    {
        Prefab = 0,
        RuntimeTemplate = 1,
    }

    public enum UITraitListRefreshMode
    {
        FullRebuild = 0,
        Incremental = 1,
        LayoutOnly = 2,
    }

    [Serializable]
    public struct UITraitListRange
    {
        [Min(0)]
        public int StartIndex;
        [Min(0)]
        public int Count;

        public UITraitListRange(int startIndex, int count)
        {
            StartIndex = startIndex;
            Count = count;
        }

        public UITraitListRange Normalize(int totalCount)
        {
            var start = StartIndex < 0 ? 0 : StartIndex;
            var count = Count > 0 ? Count : Mathf.Max(0, totalCount - start);
            if (count < 0)
                count = 0;
            return new UITraitListRange(start, count);
        }

        public int GetEffectiveCount(int totalCount)
        {
            var normalized = Normalize(totalCount);
            var available = totalCount - normalized.StartIndex;
            if (available <= 0)
                return 0;
            if (normalized.Count <= 0)
                return 0;
            return Mathf.Min(available, normalized.Count);
        }
    }

    [Serializable]
    public struct UITraitListSlot
    {
        public ITraitInstance Trait;
        public int TraitIndex;
        public int ListIndex;
        public int Row;
        public int Column;
        public Vector2 AnchoredPosition;
        public UITraitListHorizontalAlignment HorizontalAlignment;
        public UITraitListVerticalAlignment VerticalAlignment;
        public string HolderKey;
        public int RangeStart;
        public int RangeCount;

        public UITraitListSlot(
            ITraitInstance trait,
            int traitIndex,
            int listIndex,
            int row,
            int column,
            Vector2 anchoredPosition,
            UITraitListHorizontalAlignment horizontalAlignment,
            UITraitListVerticalAlignment verticalAlignment)
        {
            Trait = trait;
            TraitIndex = traitIndex;
            ListIndex = listIndex;
            Row = row;
            Column = column;
            AnchoredPosition = anchoredPosition;
            HorizontalAlignment = horizontalAlignment;
            VerticalAlignment = verticalAlignment;
            HolderKey = string.Empty;
            RangeStart = 0;
            RangeCount = 0;
        }
    }

    public sealed class UITraitListVisualInstance
    {
        public ITraitInstance Trait { get; }
        public int TraitIndex { get; private set; }
        public int ListIndex { get; private set; }
        public int Row { get; private set; }
        public int Column { get; private set; }
        public Vector2 AnchoredPosition { get; private set; }
        public Transform Root { get; }
        public RectTransform? RootRect { get; }
        public IScopeNode Scope { get; }
        public IRuntimeResolver Resolver { get; }

        public UITraitListVisualInstance(
            ITraitInstance trait,
            int traitIndex,
            int listIndex,
            int row,
            int column,
            Vector2 anchoredPosition,
            Transform root,
            IScopeNode scope,
            IRuntimeResolver resolver)
        {
            Trait = trait;
            TraitIndex = traitIndex;
            ListIndex = listIndex;
            Row = row;
            Column = column;
            AnchoredPosition = anchoredPosition;
            Root = root;
            RootRect = root as RectTransform;
            Scope = scope;
            Resolver = resolver;
        }

        public void UpdateSlot(in UITraitListSlot slot)
        {
            TraitIndex = slot.TraitIndex;
            ListIndex = slot.ListIndex;
            Row = slot.Row;
            Column = slot.Column;
            AnchoredPosition = slot.AnchoredPosition;
        }
    }

    public sealed class UITraitListRuntime
    {
        public ITraitHolderService Holder { get; }
        public string HolderKey { get; }
        public UITraitListProfileSO Profile { get; }
        public UITraitListRange Range { get; private set; }
        public Transform Parent { get; }
        public IScopeNode ScopeParent { get; }
        public ITraitPlacementService? PlacementService { get; }
        public bool HideVisiblePlacedTraits { get; }
        public List<UITraitListVisualInstance> Instances { get; }
        public Dictionary<ITraitInstance, UITraitListVisualInstance> Lookup { get; }

        public UITraitListRuntime(
            ITraitHolderService holder,
            string holderKey,
            UITraitListProfileSO profile,
            UITraitListRange range,
            Transform parent,
            IScopeNode scopeParent,
            ITraitPlacementService? placementService,
            bool hideVisiblePlacedTraits,
            List<UITraitListVisualInstance> instances,
            Dictionary<ITraitInstance, UITraitListVisualInstance> lookup)
        {
            Holder = holder;
            HolderKey = holderKey ?? string.Empty;
            Profile = profile;
            Range = range;
            Parent = parent;
            ScopeParent = scopeParent;
            PlacementService = placementService;
            HideVisiblePlacedTraits = hideVisiblePlacedTraits;
            Instances = instances;
            Lookup = lookup;
        }

        public void SetRange(UITraitListRange range)
        {
            Range = range;
        }
    }
}
