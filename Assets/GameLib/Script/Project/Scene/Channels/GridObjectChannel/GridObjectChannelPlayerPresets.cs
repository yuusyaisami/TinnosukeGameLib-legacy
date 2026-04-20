#nullable enable
using System;
using Game.Common;
using Game.Commands.VNext;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Channel
{
    [Serializable]
    public sealed class GridObjectChannelElementCondition
    {
        [LabelText("Enabled")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        bool _enabled;

        [ShowIf(nameof(_enabled))]
        [LabelText("Var Key")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        VarKeyRef _key = new();

        public bool Enabled => _enabled;
        public VarKeyRef Key => _key;

        public GridObjectChannelElementCondition CreateRuntimeCopy()
        {
            return new GridObjectChannelElementCondition
            {
                _enabled = _enabled,
                _key = _key,
            };
        }
    }

    [Serializable]
    public abstract class GridObjectChannelPlayerPresetBase : IDynamicManagedRefValue
    {
        [BoxGroup("Player")]
        [LabelText("Refresh Mode")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        GridObjectChannelRefreshMode _refreshMode = GridObjectChannelRefreshMode.Incremental;

        [BoxGroup("Player")]
        [LabelText("Debounce Frames")]
        [MinValue(0)]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        int _debounceFrames = 1;

        public GridObjectChannelRefreshMode RefreshMode => _refreshMode;
        public int DebounceFrames => Mathf.Max(0, _debounceFrames);

        protected void CopyBaseTo(GridObjectChannelPlayerPresetBase target)
        {
            target._refreshMode = _refreshMode;
            target._debounceFrames = _debounceFrames;
        }

        public abstract GridObjectChannelPlayerPresetBase CreateRuntimeCopy();
    }

    [Serializable]
    public sealed class GridObjectChannelStandalonePlayerPreset : GridObjectChannelPlayerPresetBase
    {
        [BoxGroup("Player")]
        [LabelText("Count")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        DynamicValue<int> _count = DynamicValueExtensions.FromLiteral(0);

        public DynamicValue<int> Count => _count;

        public static GridObjectChannelStandalonePlayerPreset CreateFixedCount(int count)
        {
            return new GridObjectChannelStandalonePlayerPreset
            {
                _count = DynamicValueExtensions.FromLiteral(Mathf.Max(0, count)),
            };
        }

        public override GridObjectChannelPlayerPresetBase CreateRuntimeCopy()
        {
            var copy = new GridObjectChannelStandalonePlayerPreset
            {
                _count = _count,
            };
            CopyBaseTo(copy);
            return copy;
        }
    }

    [Serializable]
    public sealed class GridObjectChannelGridBlackboardPlayerPreset : GridObjectChannelPlayerPresetBase
    {
        [BoxGroup("Grid")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Grid Blackboard Source\", _gridBlackboardActorSource)")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        ActorSource _gridBlackboardActorSource = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Grid")]
        [LabelText("Row Offset")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        DynamicValue<int> _rowOffset = DynamicValueExtensions.FromLiteral(0);

        [BoxGroup("Grid")]
        [LabelText("Column Offset")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        DynamicValue<int> _columnOffset = DynamicValueExtensions.FromLiteral(0);

        [BoxGroup("Grid")]
        [LabelText("Use Grid Key Filter")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        bool _useGridKeyFilter;

        [BoxGroup("Grid")]
        [ShowIf(nameof(_useGridKeyFilter))]
        [LabelText("Grid Key")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        VarKeyRef _gridKey = new();

        [BoxGroup("Grid")]
        [LabelText("Element Condition")]
        [InlineProperty]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        GridObjectChannelElementCondition _elementCondition = new();

        [BoxGroup("Grid")]
        [LabelText("Sparse Layout Mode")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        GridObjectChannelSparseLayoutMode _sparseLayoutMode = GridObjectChannelSparseLayoutMode.PreserveSparseCoordinates;

        [BoxGroup("Grid")]
        [LabelText("Swap Row / Column")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        bool _swapRowAndColumn;

        public ActorSource GridBlackboardActorSource => _gridBlackboardActorSource;
        public DynamicValue<int> RowOffset => _rowOffset;
        public DynamicValue<int> ColumnOffset => _columnOffset;
        public bool UseGridKeyFilter => _useGridKeyFilter;
        public VarKeyRef GridKey => _gridKey;
        public GridObjectChannelElementCondition ElementCondition => _elementCondition;
        public GridObjectChannelSparseLayoutMode SparseLayoutMode => _sparseLayoutMode;
        public bool SwapRowAndColumn => _swapRowAndColumn;

        public override GridObjectChannelPlayerPresetBase CreateRuntimeCopy()
        {
            var copy = new GridObjectChannelGridBlackboardPlayerPreset
            {
                _gridBlackboardActorSource = _gridBlackboardActorSource,
                _rowOffset = _rowOffset,
                _columnOffset = _columnOffset,
                _useGridKeyFilter = _useGridKeyFilter,
                _gridKey = _gridKey,
                _elementCondition = _elementCondition.CreateRuntimeCopy(),
                _sparseLayoutMode = _sparseLayoutMode,
                _swapRowAndColumn = _swapRowAndColumn,
            };
            CopyBaseTo(copy);
            return copy;
        }
    }

    [CreateAssetMenu(
        menuName = "Game/Channel/GridObjectChannel/Player Preset",
        fileName = "GridObjectChannelPlayerPreset")]
    public sealed class GridObjectChannelPlayerPresetSO : ScriptableObject, IDynamicValueAsset<GridObjectChannelPlayerPresetBase>
    {
        [SerializeReference, InlineProperty, HideLabel]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        GridObjectChannelPlayerPresetBase? _preset = new GridObjectChannelStandalonePlayerPreset();

        public GridObjectChannelPlayerPresetBase? Preset
        {
            get
            {
                EnsurePreset();
                return _preset;
            }
        }

        void OnEnable() => EnsurePreset();
        void OnValidate() => EnsurePreset();

        void EnsurePreset()
        {
            _preset ??= new GridObjectChannelStandalonePlayerPreset();
        }
    }
}
