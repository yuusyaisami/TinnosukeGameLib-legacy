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
        [Tooltip("true のとき、各 element の VarStore から指定 VarKey の bool を見て表示可否を判定します。")]
        [SerializeField]
        bool _enabled;

        [ShowIf(nameof(_enabled))]
        [LabelText("Var Key")]
        [Tooltip("各 element の VarStore から解決する判定キーです。キーがない場合は true 扱いです。")]
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
        [Tooltip("player 側の変化を受けたときに channel をどの粒度で更新するかを決めます。")]
        [SerializeField]
        GridObjectChannelRefreshMode _refreshMode = GridObjectChannelRefreshMode.Incremental;

        [BoxGroup("Player")]
        [LabelText("Debounce Frames")]
        [MinValue(0)]
        [Tooltip("変更通知を受けてから実際に refresh を走らせるまで待つ frame 数です。")]
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
        [Tooltip("standalone mode で生成する item 数です。0 から Count-1 の dense item を作ります。")]
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
        [Tooltip("参照する GridBlackboard を持つ scope の取得元です。")]
        [SerializeField]
        ActorSource _gridBlackboardActorSource = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Grid")]
        [LabelText("Row Offset")]
        [Tooltip("GridBlackboard の row を layout row へ変換するときに加算する offset です。")]
        [SerializeField]
        DynamicValue<int> _rowOffset = DynamicValueExtensions.FromLiteral(0);

        [BoxGroup("Grid")]
        [LabelText("Column Offset")]
        [Tooltip("GridBlackboard の column を layout column へ変換するときに加算する offset です。")]
        [SerializeField]
        DynamicValue<int> _columnOffset = DynamicValueExtensions.FromLiteral(0);

        [BoxGroup("Grid")]
        [LabelText("Use Grid Key Filter")]
        [Tooltip("true のとき指定した Grid Key を持つ cell のみを item 化します。")]
        [SerializeField]
        bool _useGridKeyFilter;

        [BoxGroup("Grid")]
        [ShowIf(nameof(_useGridKeyFilter))]
        [LabelText("Grid Key")]
        [Tooltip("Use Grid Key Filter が true のときに参照する GridBlackboard の var key です。")]
        [SerializeField]
        VarKeyRef _gridKey = new();

        [BoxGroup("Grid")]
        [LabelText("Element Condition")]
        [InlineProperty]
        [Tooltip("各 element の表示可否を、その element 自身の VarStore 内 bool で判定します。")]
        [SerializeField]
        GridObjectChannelElementCondition _elementCondition = new();

        [BoxGroup("Grid")]
        [LabelText("Sparse Layout Mode")]
        [Tooltip("空き cell をそのまま座標に反映するか、occupied cell だけを詰めて並べるかを決めます。")]
        [SerializeField]
        GridObjectChannelSparseLayoutMode _sparseLayoutMode = GridObjectChannelSparseLayoutMode.PreserveSparseCoordinates;

        [BoxGroup("Grid")]
        [LabelText("Swap Row / Column")]
        [Tooltip("true のとき GridBlackboard の row/column 解釈を入れ替えて layout row/column へ反映します。Column に並んだ cell を縦表示したい場合に使います。")]
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
        [Tooltip("SO 内に保持する GridObjectChannelPlayerPreset 本体です。")]
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
