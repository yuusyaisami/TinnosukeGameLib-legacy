#nullable enable
using System;
using DG.Tweening;
using Game.Commands.VNext;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.UI
{
    [Serializable]
    public sealed class TraitListChannelMotionPreset
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

        public TraitListChannelMotionPreset CreateRuntimeCopy()
        {
            return new TraitListChannelMotionPreset
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
    public sealed class TraitListChannelLayoutPreset : IDynamicManagedRefValue
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
        [LabelText("Layout Mode")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        TraitListChannelLayoutMode _layoutMode = TraitListChannelLayoutMode.FixedGrid;

        [BoxGroup("Layout")]
        [ShowIf(nameof(UsesFixedGrid))]
        [LabelText("Rows")]
        [MinValue(1)]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        int _rows = 1;

        [BoxGroup("Layout")]
        [ShowIf(nameof(UsesFixedGrid))]
        [LabelText("Columns")]
        [MinValue(1)]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        int _columns = 1;

        [BoxGroup("Layout")]
        [LabelText("Order")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        TraitListChannelOrder _order = TraitListChannelOrder.RowMajor;

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
        TraitListChannelHorizontalAlignment _itemHorizontalAlignment = TraitListChannelHorizontalAlignment.Left;

        [BoxGroup("Layout")]
        [LabelText("Item Vertical Align")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        TraitListChannelVerticalAlignment _itemVerticalAlignment = TraitListChannelVerticalAlignment.Top;

        [BoxGroup("Layout")]
        [LabelText("Area Horizontal Align")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        TraitListChannelHorizontalAlignment _areaHorizontalAlignment = TraitListChannelHorizontalAlignment.Left;

        [BoxGroup("Layout")]
        [LabelText("Area Vertical Align")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        TraitListChannelVerticalAlignment _areaVerticalAlignment = TraitListChannelVerticalAlignment.Top;

        [BoxGroup("Layout")]
        [LabelText("Item Offset")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        Vector3 _itemOffset = Vector3.zero;

        [BoxGroup("Spawn")]
        [LabelText("Spawn Anchor Mode")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        TraitListChannelSpawnAnchorMode _spawnAnchorMode = TraitListChannelSpawnAnchorMode.LayoutTarget;

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
        TraitListChannelMotionPreset _spawnMotion = new();

        [BoxGroup("Motion Relayout")]
        [LabelText("Relayout Motion")]
        [InlineProperty]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        TraitListChannelMotionPreset _relayoutMotion = new();

        bool UsesAreaChannel() => _rangeSourceMode == TransformGridLayoutRangeSourceMode.AreaChannel;
        bool UsesFixedGrid() => _layoutMode == TraitListChannelLayoutMode.FixedGrid;
        bool UsesFixedAnchor() => _spawnAnchorMode == TraitListChannelSpawnAnchorMode.FixedAnchor;
        bool ShowsFixedAnchorActorSource() => UsesFixedAnchor() && _useFixedAnchorActorSource;

        public TransformGridLayoutRangeSourceMode RangeSourceMode => _rangeSourceMode;
        public ActorSource AreaActorSource => _areaActorSource;
        public string AreaChannelTag => string.IsNullOrWhiteSpace(_areaChannelTag) ? "default" : _areaChannelTag.Trim();
        public TraitListChannelLayoutMode LayoutMode => _layoutMode;
        public int Rows => Mathf.Max(1, _rows);
        public int Columns => Mathf.Max(1, _columns);
        public TraitListChannelOrder Order => _order;
        public float RowSpacing => Mathf.Max(0f, _rowSpacing);
        public float ColumnSpacing => Mathf.Max(0f, _columnSpacing);
        public TraitListChannelHorizontalAlignment ItemHorizontalAlignment => _itemHorizontalAlignment;
        public TraitListChannelVerticalAlignment ItemVerticalAlignment => _itemVerticalAlignment;
        public TraitListChannelHorizontalAlignment AreaHorizontalAlignment => _areaHorizontalAlignment;
        public TraitListChannelVerticalAlignment AreaVerticalAlignment => _areaVerticalAlignment;
        public Vector3 ItemOffset => _itemOffset;
        public TraitListChannelSpawnAnchorMode SpawnAnchorMode => _spawnAnchorMode;
        public Transform? FixedAnchorTransform => _fixedAnchorTransform;
        public bool UseFixedAnchorActorSource => _useFixedAnchorActorSource;
        public ActorSource FixedAnchorActorSource => _fixedAnchorActorSource;
        public Vector3 SpawnOffset => _spawnOffset;
        public TraitListChannelMotionPreset SpawnMotion => _spawnMotion;
        public TraitListChannelMotionPreset RelayoutMotion => _relayoutMotion;

        public TraitListChannelLayoutPreset CreateRuntimeCopy()
        {
            return new TraitListChannelLayoutPreset
            {
                _rangeSourceMode = _rangeSourceMode,
                _areaActorSource = _areaActorSource,
                _areaChannelTag = _areaChannelTag,
                _layoutMode = _layoutMode,
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
                _spawnMotion = _spawnMotion?.CreateRuntimeCopy() ?? new TraitListChannelMotionPreset(),
                _relayoutMotion = _relayoutMotion?.CreateRuntimeCopy() ?? new TraitListChannelMotionPreset(),
            };
        }
    }

    [CreateAssetMenu(
        menuName = "Game/UI/TraitListChannel/Layout Preset",
        fileName = "TraitListChannelLayoutPreset")]
    public sealed class TraitListChannelLayoutPresetSO : ScriptableObject, IDynamicValueAsset<TraitListChannelLayoutPreset>
    {
        [SerializeReference, InlineProperty, HideLabel]
        [Tooltip("Inspector setting.")]
        TraitListChannelLayoutPreset? _preset = new();

        public TraitListChannelLayoutPreset? Preset
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
            _preset ??= new TraitListChannelLayoutPreset();
        }
    }
}
