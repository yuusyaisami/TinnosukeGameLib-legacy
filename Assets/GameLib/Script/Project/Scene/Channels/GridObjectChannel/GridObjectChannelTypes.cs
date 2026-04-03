#nullable enable
using System;
using System.Collections.Generic;
using DG.Tweening;
using Game.Commands.VNext;
using Game.Common;
using Game.DI;
using Game.Vars.Generated;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace Game.Channel
{
    public enum GridObjectChannelOrder
    {
        RowMajor = 10,
        ColumnMajor = 20,
    }

    public enum GridObjectChannelHorizontalAlignment
    {
        Left = 10,
        Center = 20,
        Right = 30,
    }

    public enum GridObjectChannelVerticalAlignment
    {
        Top = 10,
        Center = 20,
        Bottom = 30,
    }

    public enum GridObjectChannelRefreshMode
    {
        FullRebuild = 10,
        Incremental = 20,
        LayoutOnly = 30,
    }

    public enum GridObjectChannelSpawnAnchorMode
    {
        LayoutTarget = 10,
        FixedAnchor = 20,
    }

    public enum GridObjectChannelVisualizerSizeSource
    {
        VisualBounds = 10,
        RectTransform = 20,
        Fixed = 30,
    }

    public enum GridObjectChannelSparseLayoutMode
    {
        PreserveSparseCoordinates = 10,
        CompressOccupiedCells = 20,
    }

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

    [Serializable]
    public sealed class GridObjectChannelLayoutPreset : IDynamicManagedRefValue
    {
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

        bool UsesFixedAnchor() => _spawnAnchorMode == GridObjectChannelSpawnAnchorMode.FixedAnchor;
        bool ShowsFixedAnchorActorSource() => UsesFixedAnchor() && _useFixedAnchorActorSource;

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

    [Serializable]
    public sealed class GridObjectChannelVisualizerPreset : IDynamicManagedRefValue
    {
        [BoxGroup("Visual")]
        [LabelText("Runtime Template")]
        [Tooltip("各 item に生成する RuntimeTemplatePreset です。RuntimeTemplateSO へ解決できる必要があります。")]
        [SerializeField]
        DynamicValue<BaseRuntimeTemplatePreset> _runtimeTemplatePreset;

        [BoxGroup("Visual")]
        [LabelText("Allow Pooling")]
        [Tooltip("true のとき生成済み runtime を pool に返して再利用します。")]
        [SerializeField]
        bool _allowPooling = true;

        [BoxGroup("Visual")]
        [LabelText("Size Source")]
        [Tooltip("layout 計算に使う item size の取得元です。")]
        [SerializeField]
        GridObjectChannelVisualizerSizeSource _sizeSource = GridObjectChannelVisualizerSizeSource.VisualBounds;

        [BoxGroup("Visual")]
        [ShowIf(nameof(UsesFixedSize))]
        [LabelText("Fixed Size")]
        [Tooltip("Size Source が Fixed のときに使う item size です。")]
        [SerializeField]
        Vector2 _fixedSize = new(100f, 100f);

        [BoxGroup("Visual")]
        [LabelText("Delay Between Spawns")]
        [Tooltip("新規 item の spawn 間で待機する秒数です。relayout のみでは使用しません。")]
        [SerializeField]
        DynamicValue<float> _delayBetweenSpawns = DynamicValueExtensions.FromLiteral(0f);

        [BoxGroup("Commands")]
        [LabelText("Spawn Commands")]
        [Tooltip("各 item spawn 時に共通で流す command 群です。")]
        [SerializeField]
        [CommandListFunctionName("GridObjectChannel.Item.OnSpawn")]
        CommandListData _spawnCommands = new();

        [BoxGroup("Commands")]
        [LabelText("Counter Var")]
        [Tooltip("spawn command 実行時に現在 item index を書き込む VarKey です。")]
        [SerializeField]
        VarKeyRef _counterVar = new(VarIds.GameLib.Base.CommandVar.i, "i");

        [BoxGroup("Commands")]
        [LabelText("Write Spawner To Context")]
        [Tooltip("true のとき channel owner scope を Context slot へ積んでから spawn command を実行します。")]
        [SerializeField]
        bool _writeSpawnerToContext;

        [BoxGroup("Commands")]
        [ShowIf(nameof(_writeSpawnerToContext))]
        [LabelText("Spawner Context Slot")]
        [Tooltip("Write Spawner To Context が true のときに使う context slot です。")]
        [SerializeField]
        CommandLtsSlot _spawnerContextSlot = CommandLtsSlot.ContextA;

        bool UsesFixedSize() => _sizeSource == GridObjectChannelVisualizerSizeSource.Fixed;

        public DynamicValue<BaseRuntimeTemplatePreset> RuntimeTemplatePreset => _runtimeTemplatePreset;
        public bool AllowPooling => _allowPooling;
        public GridObjectChannelVisualizerSizeSource SizeSource => _sizeSource;
        public Vector2 FixedSize => new(Mathf.Max(0f, _fixedSize.x), Mathf.Max(0f, _fixedSize.y));
        public DynamicValue<float> DelayBetweenSpawns => _delayBetweenSpawns;
        public CommandListData SpawnCommands => _spawnCommands;
        public VarKeyRef CounterVar => _counterVar;
        public bool WriteSpawnerToContext => _writeSpawnerToContext;
        public CommandLtsSlot SpawnerContextSlot => _spawnerContextSlot;

        public bool TryResolveRuntimeTemplate(IDynamicContext context, out BaseRuntimeTemplateSO? runtimeTemplate)
        {
            runtimeTemplate = null;
            if (!_runtimeTemplatePreset.TryGet(context, out BaseRuntimeTemplatePreset? preset) || preset == null)
                return false;

            runtimeTemplate = RuntimeTemplatePresetResolver.ResolveTemplateSO(preset);
            return runtimeTemplate != null;
        }

        public GridObjectChannelVisualizerPreset CreateRuntimeCopy()
        {
            return new GridObjectChannelVisualizerPreset
            {
                _runtimeTemplatePreset = _runtimeTemplatePreset,
                _allowPooling = _allowPooling,
                _sizeSource = _sizeSource,
                _fixedSize = _fixedSize,
                _delayBetweenSpawns = _delayBetweenSpawns,
                _spawnCommands = CloneCommandList(_spawnCommands),
                _counterVar = _counterVar,
                _writeSpawnerToContext = _writeSpawnerToContext,
                _spawnerContextSlot = _spawnerContextSlot,
            };
        }

        static CommandListData CloneCommandList(CommandListData? source)
        {
            var clone = new CommandListData();
            if (source != null)
                clone.SetCommands(source);
            return clone;
        }
    }

    [CreateAssetMenu(
        menuName = "Game/Channel/GridObjectChannel/Visualizer Preset",
        fileName = "GridObjectChannelVisualizerPreset")]
    public sealed class GridObjectChannelVisualizerPresetSO : ScriptableObject, IDynamicValueAsset<GridObjectChannelVisualizerPreset>
    {
        [SerializeReference, InlineProperty, HideLabel]
        [Tooltip("SO 内に保持する GridObjectChannelVisualizerPreset 本体です。")]
        [SerializeField]
        GridObjectChannelVisualizerPreset? _preset = new();

        public GridObjectChannelVisualizerPreset? Preset
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
            _preset ??= new GridObjectChannelVisualizerPreset();
        }
    }

    [Serializable]
    public sealed class GridObjectChannelBindRequest
    {
        [LabelText("Override Player Preset")]
        [Tooltip("true のとき hub 側 default player preset の代わりにここで指定した player preset を使います。")]
        [SerializeField]
        bool _overridePlayerPreset;

        [SerializeField]
        [ShowIf(nameof(_overridePlayerPreset))]
        [Tooltip("bind 時に差し替える player preset です。")]
        DynamicValue<GridObjectChannelPlayerPresetBase> _playerPresetValue =
            DynamicValue<GridObjectChannelPlayerPresetBase>.FromSource(
                new ManagedRefLiteralSource<GridObjectChannelPlayerPresetBase>(new GridObjectChannelStandalonePlayerPreset()));

        [LabelText("Override Layout Preset")]
        [Tooltip("true のとき hub 側 default layout preset の代わりにここで指定した layout preset を使います。")]
        [SerializeField]
        bool _overrideLayoutPreset;

        [SerializeField]
        [ShowIf(nameof(_overrideLayoutPreset))]
        [Tooltip("bind 時に差し替える layout preset です。")]
        DynamicValue<GridObjectChannelLayoutPreset> _layoutPresetValue =
            DynamicValue<GridObjectChannelLayoutPreset>.FromSource(
                new ManagedRefLiteralSource<GridObjectChannelLayoutPreset>(new GridObjectChannelLayoutPreset()));

        [LabelText("Override Visualizer Preset")]
        [Tooltip("true のとき hub 側 default visualizer preset の代わりにここで指定した visualizer preset を使います。")]
        [SerializeField]
        bool _overrideVisualizerPreset;

        [SerializeField]
        [ShowIf(nameof(_overrideVisualizerPreset))]
        [Tooltip("bind 時に差し替える visualizer preset です。")]
        DynamicValue<GridObjectChannelVisualizerPreset> _visualizerPresetValue =
            DynamicValue<GridObjectChannelVisualizerPreset>.FromSource(
                new ManagedRefLiteralSource<GridObjectChannelVisualizerPreset>(new GridObjectChannelVisualizerPreset()));

        public bool OverridePlayerPreset
        {
            get => _overridePlayerPreset;
            set => _overridePlayerPreset = value;
        }

        public DynamicValue<GridObjectChannelPlayerPresetBase> PlayerPresetValue
        {
            get => _playerPresetValue;
            set => _playerPresetValue = value;
        }

        public bool OverrideLayoutPreset
        {
            get => _overrideLayoutPreset;
            set => _overrideLayoutPreset = value;
        }

        public DynamicValue<GridObjectChannelLayoutPreset> LayoutPresetValue
        {
            get => _layoutPresetValue;
            set => _layoutPresetValue = value;
        }

        public bool OverrideVisualizerPreset
        {
            get => _overrideVisualizerPreset;
            set => _overrideVisualizerPreset = value;
        }

        public DynamicValue<GridObjectChannelVisualizerPreset> VisualizerPresetValue
        {
            get => _visualizerPresetValue;
            set => _visualizerPresetValue = value;
        }

        public GridObjectChannelBindRequest Clone()
        {
            return new GridObjectChannelBindRequest
            {
                _overridePlayerPreset = _overridePlayerPreset,
                _playerPresetValue = _playerPresetValue,
                _overrideLayoutPreset = _overrideLayoutPreset,
                _layoutPresetValue = _layoutPresetValue,
                _overrideVisualizerPreset = _overrideVisualizerPreset,
                _visualizerPresetValue = _visualizerPresetValue,
            };
        }
    }

    internal enum GridObjectChannelItemKeyKind
    {
        Standalone = 10,
        SourceCell = 20,
    }

    internal readonly struct GridObjectChannelItemKey : IEquatable<GridObjectChannelItemKey>
    {
        public GridObjectChannelItemKey(GridObjectChannelItemKeyKind kind, int valueA, int valueB)
        {
            Kind = kind;
            ValueA = valueA;
            ValueB = valueB;
        }

        public GridObjectChannelItemKeyKind Kind { get; }
        public int ValueA { get; }
        public int ValueB { get; }

        public static GridObjectChannelItemKey Standalone(int listIndex) => new(GridObjectChannelItemKeyKind.Standalone, listIndex, 0);
        public static GridObjectChannelItemKey SourceCell(int row, int column) => new(GridObjectChannelItemKeyKind.SourceCell, row, column);

        public bool Equals(GridObjectChannelItemKey other)
        {
            return Kind == other.Kind &&
                   ValueA == other.ValueA &&
                   ValueB == other.ValueB;
        }

        public override bool Equals(object? obj) => obj is GridObjectChannelItemKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine((int)Kind, ValueA, ValueB);
    }

    internal sealed class GridObjectChannelResolvedItem
    {
        public GridObjectChannelItemKey Key;
        public int ListIndex;
        public int Row;
        public int Column;
        public int SourceRow;
        public int SourceColumn;
        public Vector3 TargetLocalPosition;
        public List<GridBlackboardCellSnapshot>? CellValues;

        public void SetCellValues(List<GridBlackboardCellSnapshot> values)
        {
            if (values == null || values.Count == 0)
            {
                CellValues = null;
                return;
            }

            CellValues = new List<GridBlackboardCellSnapshot>(values);
        }
    }

    internal sealed class GridObjectChannelVisualInstance
    {
        public GridObjectChannelVisualInstance(
            GridObjectChannelItemKey key,
            Transform root,
            IScopeNode scope,
            IObjectResolver resolver)
        {
            Key = key;
            Root = root;
            RootRect = root as RectTransform;
            Scope = scope;
            Resolver = resolver;
        }

        public GridObjectChannelItemKey Key { get; private set; }
        public Transform Root { get; }
        public RectTransform? RootRect { get; }
        public IScopeNode Scope { get; }
        public IObjectResolver Resolver { get; }
        public int ListIndex { get; private set; }
        public int Row { get; private set; }
        public int Column { get; private set; }
        public int SourceRow { get; private set; }
        public int SourceColumn { get; private set; }
        public Vector3 TargetLocalPosition { get; private set; }

        public void UpdateFromItem(GridObjectChannelResolvedItem item)
        {
            Key = item.Key;
            ListIndex = item.ListIndex;
            Row = item.Row;
            Column = item.Column;
            SourceRow = item.SourceRow;
            SourceColumn = item.SourceColumn;
            TargetLocalPosition = item.TargetLocalPosition;
        }
    }
}
