#nullable enable
using System;
using Game.Commands.VNext;
using Game.Common;
using Game.Trait;
using UnityEngine;
using VContainer;

namespace Game.UI
{
    public enum TraitListChannelOrder
    {
        RowMajor = 10,
        ColumnMajor = 20,
    }

    public enum TraitListChannelLayoutMode
    {
        FixedGrid = 10,
        AutoFit = 20,
    }

    public enum TraitListChannelHorizontalAlignment
    {
        Left = 10,
        Center = 20,
        Right = 30,
    }

    public enum TraitListChannelVerticalAlignment
    {
        Top = 10,
        Center = 20,
        Bottom = 30,
    }

    public enum TraitListChannelRefreshMode
    {
        FullRebuild = 10,
        Incremental = 20,
        LayoutOnly = 30,
    }

    public enum TraitListChannelSpawnAnchorMode
    {
        LayoutTarget = 10,
        FixedAnchor = 20,
    }

    public enum TraitListChannelVisualizerSizeSource
    {
        VisualBounds = 10,
        RectTransform = 20,
        Fixed = 30,
    }

    [Serializable]
    public struct TraitListChannelRange
    {
        [Tooltip("Inspector setting.")]
        [Min(0)]
        public int StartIndex;

        [Tooltip("Inspector setting.")]
        [Min(0)]
        public int Count;

        public TraitListChannelRange(int startIndex, int count)
        {
            StartIndex = startIndex;
            Count = count;
        }

        public static TraitListChannelRange Full => new(0, 0);

        public TraitListChannelRange Normalize(int totalCount)
        {
            var start = Mathf.Max(0, StartIndex);
            var count = Count > 0 ? Count : Mathf.Max(0, totalCount - start);
            if (count < 0)
                count = 0;

            return new TraitListChannelRange(start, count);
        }

        public int GetEffectiveCount(int totalCount)
        {
            var normalized = Normalize(totalCount);
            var available = totalCount - normalized.StartIndex;
            if (available <= 0 || normalized.Count <= 0)
                return 0;

            return Mathf.Min(available, normalized.Count);
        }
    }

    [Serializable]
    public sealed class TraitListChannelBinding
    {
        [SerializeField]
        [Tooltip("Inspector setting.")]
        ActorSource _holderHubSource = new() { Kind = ActorSourceKind.Current };

        [SerializeField]
        [Tooltip("Inspector setting.")]
        string _holderKey = string.Empty;

        [SerializeField]
        [Tooltip("Inspector setting.")]
        bool _useRange;

        [SerializeField]
        [Sirenix.OdinInspector.ShowIf(nameof(_useRange))]
        [Tooltip("Inspector setting.")]
        TraitListChannelRange _range = new(0, 0);

        public ActorSource HolderHubSource
        {
            get => _holderHubSource;
            set => _holderHubSource = value;
        }

        public string HolderKey
        {
            get => _holderKey;
            set => _holderKey = value ?? string.Empty;
        }

        public bool UseRange
        {
            get => _useRange;
            set => _useRange = value;
        }

        public TraitListChannelRange Range
        {
            get => _range;
            set => _range = value;
        }

        public string NormalizedHolderKey => string.IsNullOrWhiteSpace(_holderKey) ? string.Empty : _holderKey.Trim();

        public TraitListChannelBinding Clone()
        {
            return new TraitListChannelBinding
            {
                _holderHubSource = _holderHubSource,
                _holderKey = _holderKey,
                _useRange = _useRange,
                _range = _range,
            };
        }
    }

    [Serializable]
    public sealed class TraitListChannelBindRequest
    {
        [SerializeField]
        [Tooltip("Inspector setting.")]
        bool _overrideHolderHubSource;

        [SerializeField]
        [Sirenix.OdinInspector.ShowIf(nameof(_overrideHolderHubSource))]
        [Tooltip("Inspector setting.")]
        ActorSource _holderHubSource = new() { Kind = ActorSourceKind.Current };

        [SerializeField]
        [Tooltip("Inspector setting.")]
        bool _overrideHolderKey;

        [SerializeField]
        [Sirenix.OdinInspector.ShowIf(nameof(_overrideHolderKey))]
        [Tooltip("Inspector setting.")]
        string _holderKey = string.Empty;

        [SerializeField]
        [Tooltip("Inspector setting.")]
        bool _overrideRange;

        [SerializeField]
        [Sirenix.OdinInspector.ShowIf(nameof(_overrideRange))]
        [Tooltip("Inspector setting.")]
        bool _useRange;

        [SerializeField]
        [Sirenix.OdinInspector.ShowIf(nameof(ShowsRange))]
        [Tooltip("Inspector setting.")]
        TraitListChannelRange _range = new(0, 0);

        [SerializeField]
        [Tooltip("Inspector setting.")]
        bool _overridePlayerPreset;

        [SerializeField]
        [Sirenix.OdinInspector.ShowIf(nameof(_overridePlayerPreset))]
        [Tooltip("Inspector setting.")]
        DynamicValue<TraitListChannelPlayerPreset> _playerPresetValue =
            DynamicValue<TraitListChannelPlayerPreset>.FromSource(
                new ManagedRefLiteralSource<TraitListChannelPlayerPreset>(new TraitListChannelPlayerPreset()));

        [SerializeField]
        [Tooltip("Inspector setting.")]
        bool _overrideLayoutPreset;

        [SerializeField]
        [Sirenix.OdinInspector.ShowIf(nameof(_overrideLayoutPreset))]
        [Tooltip("Inspector setting.")]
        DynamicValue<TraitListChannelLayoutPreset> _layoutPresetValue =
            DynamicValue<TraitListChannelLayoutPreset>.FromSource(
                new ManagedRefLiteralSource<TraitListChannelLayoutPreset>(new TraitListChannelLayoutPreset()));

        [SerializeField]
        [Tooltip("Inspector setting.")]
        bool _overrideVisualizerPreset;

        [SerializeField]
        [Sirenix.OdinInspector.ShowIf(nameof(_overrideVisualizerPreset))]
        [Tooltip("Inspector setting.")]
        DynamicValue<TraitListChannelVisualizerPreset> _visualizerPresetValue =
            DynamicValue<TraitListChannelVisualizerPreset>.FromSource(
                new ManagedRefLiteralSource<TraitListChannelVisualizerPreset>(new TraitListChannelVisualizerPreset()));

        public bool OverrideHolderHubSource
        {
            get => _overrideHolderHubSource;
            set => _overrideHolderHubSource = value;
        }

        public ActorSource HolderHubSource
        {
            get => _holderHubSource;
            set => _holderHubSource = value;
        }

        public bool OverrideHolderKey
        {
            get => _overrideHolderKey;
            set => _overrideHolderKey = value;
        }

        public string HolderKey
        {
            get => _holderKey;
            set => _holderKey = value ?? string.Empty;
        }

        public bool OverrideRange
        {
            get => _overrideRange;
            set => _overrideRange = value;
        }

        bool UsesRange() => _useRange;
        bool ShowsRange() => _overrideRange && _useRange;

        public bool UseRange
        {
            get => _useRange;
            set => _useRange = value;
        }

        public TraitListChannelRange Range
        {
            get => _range;
            set => _range = value;
        }

        public bool OverridePlayerPreset
        {
            get => _overridePlayerPreset;
            set => _overridePlayerPreset = value;
        }

        public DynamicValue<TraitListChannelPlayerPreset> PlayerPresetValue
        {
            get => _playerPresetValue;
            set => _playerPresetValue = value;
        }

        public bool OverrideLayoutPreset
        {
            get => _overrideLayoutPreset;
            set => _overrideLayoutPreset = value;
        }

        public DynamicValue<TraitListChannelLayoutPreset> LayoutPresetValue
        {
            get => _layoutPresetValue;
            set => _layoutPresetValue = value;
        }

        public bool OverrideVisualizerPreset
        {
            get => _overrideVisualizerPreset;
            set => _overrideVisualizerPreset = value;
        }

        public DynamicValue<TraitListChannelVisualizerPreset> VisualizerPresetValue
        {
            get => _visualizerPresetValue;
            set => _visualizerPresetValue = value;
        }

        public TraitListChannelBindRequest Clone()
        {
            return new TraitListChannelBindRequest
            {
                _overrideHolderHubSource = _overrideHolderHubSource,
                _holderHubSource = _holderHubSource,
                _overrideHolderKey = _overrideHolderKey,
                _holderKey = _holderKey,
                _overrideRange = _overrideRange,
                _useRange = _useRange,
                _range = _range,
                _overridePlayerPreset = _overridePlayerPreset,
                _playerPresetValue = _playerPresetValue,
                _overrideLayoutPreset = _overrideLayoutPreset,
                _layoutPresetValue = _layoutPresetValue,
                _overrideVisualizerPreset = _overrideVisualizerPreset,
                _visualizerPresetValue = _visualizerPresetValue,
            };
        }
    }

    internal enum TraitListChannelEnvironmentKind
    {
        ScreenUI = 10,
        World = 20,
    }

    internal struct TraitListChannelSlot
    {
        public ITraitInstance Trait;
        public int TraitIndex;
        public string DisplayKey;
        public int DuplicateCount;
        public int ListIndex;
        public int Row;
        public int Column;
        public Vector3 TargetLocalPosition;
        public TraitListChannelHorizontalAlignment ItemHorizontalAlignment;
        public TraitListChannelVerticalAlignment ItemVerticalAlignment;
        public string ChannelTag;
        public string HolderKey;
        public int RangeStart;
        public int RangeCount;
    }

    internal sealed class TraitListChannelVisualInstance
    {
        public TraitListChannelVisualInstance(
            string displayKey,
            ITraitInstance trait,
            Transform root,
            IScopeNode scope,
            IRuntimeResolver resolver)
        {
            DisplayKey = displayKey;
            Trait = trait;
            Root = root;
            RootRect = root as RectTransform;
            Scope = scope;
            Resolver = resolver;
        }

        public string DisplayKey { get; private set; }
        public ITraitInstance Trait { get; private set; }
        public Transform Root { get; }
        public RectTransform? RootRect { get; }
        public IScopeNode Scope { get; }
        public IRuntimeResolver Resolver { get; }
        public int DuplicateCount { get; private set; }
        public int TraitIndex { get; private set; }
        public int ListIndex { get; private set; }
        public int Row { get; private set; }
        public int Column { get; private set; }
        public Vector3 TargetLocalPosition { get; private set; }

        public void UpdateSlot(in TraitListChannelSlot slot)
        {
            DisplayKey = slot.DisplayKey;
            Trait = slot.Trait;
            DuplicateCount = slot.DuplicateCount;
            TraitIndex = slot.TraitIndex;
            ListIndex = slot.ListIndex;
            Row = slot.Row;
            Column = slot.Column;
            TargetLocalPosition = slot.TargetLocalPosition;
        }
    }
}
