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
        [BoxGroup("Layout")]
        [LabelText("Rows")]
        [MinValue(1)]
        [Tooltip("レイアウトの行数です。Rows x Columns が同時表示 capacity になります。")]
        [SerializeField]
        int _rows = 1;

        [BoxGroup("Layout")]
        [LabelText("Columns")]
        [MinValue(1)]
        [Tooltip("レイアウトの列数です。Rows x Columns が同時表示 capacity になります。")]
        [SerializeField]
        int _columns = 1;

        [BoxGroup("Layout")]
        [LabelText("Order")]
        [Tooltip("listIndex を row/column に割り当てる順序です。")]
        [SerializeField]
        TraitListChannelOrder _order = TraitListChannelOrder.RowMajor;

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
        TraitListChannelHorizontalAlignment _itemHorizontalAlignment = TraitListChannelHorizontalAlignment.Left;

        [BoxGroup("Layout")]
        [LabelText("Item Vertical Align")]
        [Tooltip("各 item の visual bounds を target 位置のどこに合わせるかを決めます。")]
        [SerializeField]
        TraitListChannelVerticalAlignment _itemVerticalAlignment = TraitListChannelVerticalAlignment.Top;

        [BoxGroup("Layout")]
        [LabelText("Area Horizontal Align")]
        [Tooltip("使用行列全体を layout 領域の横方向どこに寄せるかを決めます。")]
        [SerializeField]
        TraitListChannelHorizontalAlignment _areaHorizontalAlignment = TraitListChannelHorizontalAlignment.Left;

        [BoxGroup("Layout")]
        [LabelText("Area Vertical Align")]
        [Tooltip("使用行列全体を layout 領域の縦方向どこに寄せるかを決めます。")]
        [SerializeField]
        TraitListChannelVerticalAlignment _areaVerticalAlignment = TraitListChannelVerticalAlignment.Top;

        [BoxGroup("Layout")]
        [LabelText("Item Offset")]
        [Tooltip("計算された各 target 位置へ加算する共通 offset です。")]
        [SerializeField]
        Vector3 _itemOffset = Vector3.zero;

        [BoxGroup("Spawn")]
        [LabelText("Spawn Anchor Mode")]
        [Tooltip("新規 spawn 時の開始位置を layout target から取るか、固定 anchor から取るかを選びます。")]
        [SerializeField]
        TraitListChannelSpawnAnchorMode _spawnAnchorMode = TraitListChannelSpawnAnchorMode.LayoutTarget;

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
        TraitListChannelMotionPreset _spawnMotion = new();

        [BoxGroup("Motion Relayout")]
        [LabelText("Relayout Motion")]
        [InlineProperty]
        [Tooltip("既存 item が現在位置から新しい layout target へ移動するときの演出です。")]
        [SerializeField]
        TraitListChannelMotionPreset _relayoutMotion = new();

        bool UsesFixedAnchor() => _spawnAnchorMode == TraitListChannelSpawnAnchorMode.FixedAnchor;
        bool ShowsFixedAnchorActorSource() => UsesFixedAnchor() && _useFixedAnchorActorSource;

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
        [Tooltip("SO 内に保持する TraitListChannelLayoutPreset 本体です。")]
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
