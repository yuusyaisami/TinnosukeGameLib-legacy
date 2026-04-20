#nullable enable
using System;
using DG.Tweening;
using Game.Commands.VNext;
using Game.Common;
using Game.UI;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Channel
{
    [Serializable]
    public sealed class GridObjectChannelMotionPreset
    {
        [LabelText("Duration Seconds")]
        [MinValue(0f)]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        float _durationSeconds = 0.2f;

        [LabelText("Ease")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        Ease _ease = Ease.OutCubic;

        [LabelText("Use Transform Animation")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        bool _useTransformAnimation;

        [ShowIf(nameof(_useTransformAnimation))]
        [LabelText("Transform Animation Channel Tag")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        string _transformAnimationChannelTag = "default";

        [LabelText("Wait For Completion")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        bool _waitForCompletion = true;

        public float DurationSeconds => Mathf.Max(0f, _durationSeconds);
        public Ease Ease => _ease;
        public bool UseTransformAnimation => _useTransformAnimation;
        public string TransformAnimationChannelTag => string.IsNullOrWhiteSpace(_transformAnimationChannelTag) ? "default" : _transformAnimationChannelTag.Trim();
        public bool WaitForCompletion => _waitForCompletion;

        public GridObjectChannelMotionPreset CreateRuntimeCopy()
        {
            return new GridObjectChannelMotionPreset
            {
                _durationSeconds = _durationSeconds,
                _ease = _ease,
                _useTransformAnimation = _useTransformAnimation,
                _transformAnimationChannelTag = _transformAnimationChannelTag,
                _waitForCompletion = _waitForCompletion,
            };
        }
    }

    [Serializable]
    public sealed class GridObjectChannelLayoutPreset : IDynamicManagedRefValue
    {
        [BoxGroup("Range")]
        [LabelText("Range Source Mode")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        TransformGridLayoutRangeSourceMode _rangeSourceMode = TransformGridLayoutRangeSourceMode.RectTransform;

        [BoxGroup("Range")]
        [ShowIf(nameof(UsesAreaChannel))]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Area Source\", _areaActorSource)")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        ActorSource _areaActorSource = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Range")]
        [ShowIf(nameof(UsesAreaChannel))]
        [LabelText("Area Channel Tag")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        string _areaChannelTag = "default";

        [BoxGroup("Layout")]
        [LabelText("Rows")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        DynamicValue<int> _rows = DynamicValueExtensions.FromLiteral(1);

        [BoxGroup("Layout")]
        [LabelText("Columns")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        DynamicValue<int> _columns = DynamicValueExtensions.FromLiteral(1);

        [BoxGroup("Layout")]
        [LabelText("Order")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        GridObjectChannelOrder _order = GridObjectChannelOrder.RowMajor;

        [BoxGroup("Layout")]
        [LabelText("Row Spacing")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        float _rowSpacing;

        [BoxGroup("Layout")]
        [LabelText("Column Spacing")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        float _columnSpacing;

        [BoxGroup("Layout")]
        [LabelText("Item Horizontal Align")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        GridObjectChannelHorizontalAlignment _itemHorizontalAlignment = GridObjectChannelHorizontalAlignment.Left;

        [BoxGroup("Layout")]
        [LabelText("Item Vertical Align")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        GridObjectChannelVerticalAlignment _itemVerticalAlignment = GridObjectChannelVerticalAlignment.Top;

        [BoxGroup("Layout")]
        [LabelText("Area Horizontal Align")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        GridObjectChannelHorizontalAlignment _areaHorizontalAlignment = GridObjectChannelHorizontalAlignment.Left;

        [BoxGroup("Layout")]
        [LabelText("Area Vertical Align")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        GridObjectChannelVerticalAlignment _areaVerticalAlignment = GridObjectChannelVerticalAlignment.Top;

        [BoxGroup("Layout")]
        [LabelText("Item Offset")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        Vector3 _itemOffset = Vector3.zero;

        [BoxGroup("Spawn")]
        [LabelText("Spawn Anchor Mode")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        GridObjectChannelSpawnAnchorMode _spawnAnchorMode = GridObjectChannelSpawnAnchorMode.LayoutTarget;

        [BoxGroup("Spawn")]
        [ShowIf(nameof(UsesFixedAnchor))]
        [LabelText("Fixed Anchor Transform")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        Transform? _fixedAnchorTransform;

        [BoxGroup("Spawn")]
        [ShowIf(nameof(UsesFixedAnchor))]
        [LabelText("Use Fixed Anchor Actor Source")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        bool _useFixedAnchorActorSource;

        [BoxGroup("Spawn")]
        [ShowIf(nameof(ShowsFixedAnchorActorSource))]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Fixed Anchor Source\", _fixedAnchorActorSource)")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        ActorSource _fixedAnchorActorSource = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Spawn")]
        [LabelText("Spawn Offset")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        Vector3 _spawnOffset = Vector3.zero;

        [BoxGroup("Motion Spawn")]
        [LabelText("Spawn Motion")]
        [InlineProperty]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        GridObjectChannelMotionPreset _spawnMotion = new();

        [BoxGroup("Motion Relayout")]
        [LabelText("Relayout Motion")]
        [InlineProperty]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        GridObjectChannelMotionPreset _relayoutMotion = new();

        bool UsesAreaChannel() => _rangeSourceMode == TransformGridLayoutRangeSourceMode.AreaChannel;
        bool UsesFixedAnchor() => _spawnAnchorMode == GridObjectChannelSpawnAnchorMode.FixedAnchor;
        bool ShowsFixedAnchorActorSource() => UsesFixedAnchor() && _useFixedAnchorActorSource;

        public TransformGridLayoutRangeSourceMode RangeSourceMode => _rangeSourceMode;
        public ActorSource AreaActorSource => _areaActorSource;
        public string AreaChannelTag => string.IsNullOrWhiteSpace(_areaChannelTag) ? "default" : _areaChannelTag.Trim();
        public DynamicValue<int> Rows => _rows;
        public DynamicValue<int> Columns => _columns;
        public GridObjectChannelOrder Order => _order;
        public float RowSpacing => Mathf.Max(0f, _rowSpacing);
        public float ColumnSpacing => Mathf.Max(0f, _columnSpacing);
        public GridObjectChannelHorizontalAlignment ItemHorizontalAlignment => _itemHorizontalAlignment;
        public GridObjectChannelVerticalAlignment ItemVerticalAlignment => _itemVerticalAlignment;
        public GridObjectChannelHorizontalAlignment AreaHorizontalAlignment => _areaHorizontalAlignment;
        public GridObjectChannelVerticalAlignment AreaVerticalAlignment => _areaVerticalAlignment;
        public Vector3 ItemOffset => _itemOffset;
        public GridObjectChannelSpawnAnchorMode SpawnAnchorMode => _spawnAnchorMode;
        public Transform? FixedAnchorTransform => _fixedAnchorTransform;
        public bool UseFixedAnchorActorSource => _useFixedAnchorActorSource;
        public ActorSource FixedAnchorActorSource => _fixedAnchorActorSource;
        public Vector3 SpawnOffset => _spawnOffset;
        public GridObjectChannelMotionPreset SpawnMotion => _spawnMotion;
        public GridObjectChannelMotionPreset RelayoutMotion => _relayoutMotion;

        public GridObjectChannelLayoutPreset CreateRuntimeCopy()
        {
            return new GridObjectChannelLayoutPreset
            {
                _rangeSourceMode = _rangeSourceMode,
                _areaActorSource = _areaActorSource,
                _areaChannelTag = _areaChannelTag,
                _rows = _rows,
                _columns = _columns,
                _order = _order,
                _rowSpacing = _rowSpacing,
                _columnSpacing = _columnSpacing,
                _itemHorizontalAlignment = _itemHorizontalAlignment,
                _itemVerticalAlignment = _itemVerticalAlignment,
                _areaHorizontalAlignment = _areaHorizontalAlignment,
                _areaVerticalAlignment = _areaVerticalAlignment,
                _itemOffset = _itemOffset,
                _spawnAnchorMode = _spawnAnchorMode,
                _fixedAnchorTransform = _fixedAnchorTransform,
                _useFixedAnchorActorSource = _useFixedAnchorActorSource,
                _fixedAnchorActorSource = _fixedAnchorActorSource,
                _spawnOffset = _spawnOffset,
                _spawnMotion = _spawnMotion?.CreateRuntimeCopy() ?? new GridObjectChannelMotionPreset(),
                _relayoutMotion = _relayoutMotion?.CreateRuntimeCopy() ?? new GridObjectChannelMotionPreset(),
            };
        }

        public GridObjectChannelLayoutPreset CreateChoiceRuntimeCopy(int itemCount)
        {
            var runtimeCopy = CreateRuntimeCopy();
            var safeCount = Mathf.Max(1, itemCount);
            if (runtimeCopy.Order == GridObjectChannelOrder.ColumnMajor)
            {
                runtimeCopy._rows = DynamicValueExtensions.FromLiteral(1);
                runtimeCopy._columns = DynamicValueExtensions.FromLiteral(safeCount);
            }
            else
            {
                runtimeCopy._rows = DynamicValueExtensions.FromLiteral(safeCount);
                runtimeCopy._columns = DynamicValueExtensions.FromLiteral(1);
            }
            return runtimeCopy;
        }

        public override string ToString()
        {
            return $"LayoutPreset(Range={RangeSourceMode}, Rows={Rows.GetOrDefaultWithoutContext(1)}, Columns={Columns.GetOrDefaultWithoutContext(1)}, Order={Order}, AreaAlign={AreaHorizontalAlignment}/{AreaVerticalAlignment}, ItemAlign={ItemHorizontalAlignment}/{ItemVerticalAlignment}, RowSpacing={RowSpacing}, ColumnSpacing={ColumnSpacing}, SpawnAnchor={SpawnAnchorMode}, SpawnOffset={SpawnOffset})";
        }
    }

    [CreateAssetMenu(
        menuName = "Game/Channel/GridObjectChannel/Layout Preset",
        fileName = "GridObjectChannelLayoutPreset")]
    public sealed class GridObjectChannelLayoutPresetSO : ScriptableObject, IDynamicValueAsset<GridObjectChannelLayoutPreset>
    {
        [SerializeReference, InlineProperty, HideLabel]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        GridObjectChannelLayoutPreset? _preset = new();

        public GridObjectChannelLayoutPreset? Preset
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
            _preset ??= new GridObjectChannelLayoutPreset();
        }
    }
}
