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
        [Tooltip("この移動演出にかける秒数です。0 の場合は即時に target 位置へ反映します。")]
        [SerializeField]
        float _durationSeconds = 0.2f;

        [LabelText("Ease")]
        [Tooltip("fallback tween や transform animation に渡す easing 種別です。")]
        [SerializeField]
        Ease _ease = Ease.OutCubic;

        [LabelText("Use Transform Animation")]
        [Tooltip("true のとき TransformAnimationChannel を優先して移動演出を行います。")]
        [SerializeField]
        bool _useTransformAnimation;

        [ShowIf(nameof(_useTransformAnimation))]
        [LabelText("Transform Animation Channel Tag")]
        [Tooltip("Use Transform Animation が true のときに連携する TransformAnimationChannel の tag です。")]
        [SerializeField]
        string _transformAnimationChannelTag = "default";

        [LabelText("Wait For Completion")]
        [Tooltip("true のとき移動演出の完了を待ってから次の処理へ進みます。")]
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
        [Tooltip("配置領域を Scene の RectTransform から取るか、AreaChannel から取るかを選びます。")]
        [SerializeField]
        TransformGridLayoutRangeSourceMode _rangeSourceMode = TransformGridLayoutRangeSourceMode.RectTransform;

        [BoxGroup("Range")]
        [ShowIf(nameof(UsesAreaChannel))]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Area Source\", _areaActorSource)")]
        [Tooltip("AreaChannel を解決する対象 scope です。")]
        [SerializeField]
        ActorSource _areaActorSource = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Range")]
        [ShowIf(nameof(UsesAreaChannel))]
        [LabelText("Area Channel Tag")]
        [Tooltip("Range Source Mode が AreaChannel のときに使う channel tag です。")]
        [SerializeField]
        string _areaChannelTag = "default";

        [BoxGroup("Layout")]
        [LabelText("Rows")]
        [Tooltip("レイアウトの行数です。GridBlackboard count source などを動的に参照できます。")]
        [SerializeField]
        DynamicValue<int> _rows = DynamicValueExtensions.FromLiteral(1);

        [BoxGroup("Layout")]
        [LabelText("Columns")]
        [Tooltip("レイアウトの列数です。GridBlackboard count source などを動的に参照できます。")]
        [SerializeField]
        DynamicValue<int> _columns = DynamicValueExtensions.FromLiteral(1);

        [BoxGroup("Layout")]
        [LabelText("Order")]
        [Tooltip("standalone item を row/column へ割り当てる順序です。")]
        [SerializeField]
        GridObjectChannelOrder _order = GridObjectChannelOrder.RowMajor;

        [BoxGroup("Layout")]
        [LabelText("Row Spacing")]
        [Tooltip("行ごとの間隔です。")]
        [SerializeField]
        float _rowSpacing;

        [BoxGroup("Layout")]
        [LabelText("Column Spacing")]
        [Tooltip("列ごとの間隔です。")]
        [SerializeField]
        float _columnSpacing;

        [BoxGroup("Layout")]
        [LabelText("Item Horizontal Align")]
        [Tooltip("各 item の visual bounds を target 位置のどこに合わせるかを決めます。")]
        [SerializeField]
        GridObjectChannelHorizontalAlignment _itemHorizontalAlignment = GridObjectChannelHorizontalAlignment.Left;

        [BoxGroup("Layout")]
        [LabelText("Item Vertical Align")]
        [Tooltip("各 item の visual bounds を target 位置のどこに合わせるかを決めます。")]
        [SerializeField]
        GridObjectChannelVerticalAlignment _itemVerticalAlignment = GridObjectChannelVerticalAlignment.Top;

        [BoxGroup("Layout")]
        [LabelText("Area Horizontal Align")]
        [Tooltip("使用行列全体を layout 領域の横方向どこに寄せるかを決めます。")]
        [SerializeField]
        GridObjectChannelHorizontalAlignment _areaHorizontalAlignment = GridObjectChannelHorizontalAlignment.Left;

        [BoxGroup("Layout")]
        [LabelText("Area Vertical Align")]
        [Tooltip("使用行列全体を layout 領域の縦方向どこに寄せるかを決めます。")]
        [SerializeField]
        GridObjectChannelVerticalAlignment _areaVerticalAlignment = GridObjectChannelVerticalAlignment.Top;

        [BoxGroup("Layout")]
        [LabelText("Item Offset")]
        [Tooltip("計算された各 target 位置へ加算する共通 offset です。")]
        [SerializeField]
        Vector3 _itemOffset = Vector3.zero;

        [BoxGroup("Spawn")]
        [LabelText("Spawn Anchor Mode")]
        [Tooltip("新規 spawn 時の開始位置を layout target から取るか、固定 anchor から取るかを選びます。")]
        [SerializeField]
        GridObjectChannelSpawnAnchorMode _spawnAnchorMode = GridObjectChannelSpawnAnchorMode.LayoutTarget;

        [BoxGroup("Spawn")]
        [ShowIf(nameof(UsesFixedAnchor))]
        [LabelText("Fixed Anchor Transform")]
        [Tooltip("FixedAnchor 使用時の開始位置基準 Transform。未設定時は ActorSource か list root local zero を使います。")]
        [SerializeField]
        Transform? _fixedAnchorTransform;

        [BoxGroup("Spawn")]
        [ShowIf(nameof(UsesFixedAnchor))]
        [LabelText("Use Fixed Anchor Actor Source")]
        [Tooltip("true のとき Fixed Anchor Transform の代わりに ActorSource から anchor transform を解決します。")]
        [SerializeField]
        bool _useFixedAnchorActorSource;

        [BoxGroup("Spawn")]
        [ShowIf(nameof(ShowsFixedAnchorActorSource))]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Fixed Anchor Source\", _fixedAnchorActorSource)")]
        [Tooltip("Fixed Anchor Transform を使わない場合の anchor 解決元です。")]
        [SerializeField]
        ActorSource _fixedAnchorActorSource = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Spawn")]
        [LabelText("Spawn Offset")]
        [Tooltip("spawn 開始位置に加算する offset です。最終的な layout target には必ず移動します。")]
        [SerializeField]
        Vector3 _spawnOffset = Vector3.zero;

        [BoxGroup("Motion Spawn")]
        [LabelText("Spawn Motion")]
        [InlineProperty]
        [Tooltip("新規生成 item が開始位置から layout target へ移動するときの演出です。")]
        [SerializeField]
        GridObjectChannelMotionPreset _spawnMotion = new();

        [BoxGroup("Motion Relayout")]
        [LabelText("Relayout Motion")]
        [InlineProperty]
        [Tooltip("既存 item が現在位置から新しい layout target へ移動するときの演出です。")]
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
        [Tooltip("SO 内に保持する GridObjectChannelLayoutPreset 本体です。")]
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
