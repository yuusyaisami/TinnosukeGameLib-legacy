#nullable enable
using System.Collections.Generic;
using Game.Commands.VNext;
using Game.Common;
using Game.DI;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.UI
{
    [System.Serializable]
    public sealed class WorldSliderBackgroundVisualizerSettings
    {
        [LabelText("Enabled")]
        [Tooltip("Slider の背面に、bar の全範囲を覆う runtime background を生成します。Slider が非表示のときは background も一緒に非表示になります。")]
        [SerializeField]
        bool _enabled;

        [ShowIf(nameof(_enabled))]
        [LabelText("Template")]
        [Tooltip("Background の生成に使う runtime template です。")]
        [SerializeField]
        DynamicValue<BaseRuntimeTemplatePreset> _templatePreset;

        [ShowIf(nameof(_enabled))]
        [LabelText("Animation Channel Tag")]
        [Tooltip("Background 生成後に IAnimationSpriteHubService から見た目ターゲットを取得する channel tag です。既定は default です。")]
        [SerializeField]
        string _animationChannelTag = "default";

        [ShowIf(nameof(_enabled))]
        [LabelText("Allow Pooling")]
        [Tooltip("Background の生成/破棄で pool を使うかを決めます。")]
        [SerializeField]
        bool _allowPooling = true;

        [ShowIf(nameof(_enabled))]
        [LabelText("Depth Offset")]
        [Tooltip("+ 方向へ押し出して Bar より背面に配置する深度オフセットです。XY 平面では +Z、XZ 平面では +Y を使います。")]
        [SerializeField]
        [MinValue(0f)]
        float _depthOffset = 0.01f;

        [ShowIf(nameof(_enabled))]
        [LabelText("On Background Spawn")]
        [Tooltip("Background を生成し、初回 geometry を適用した直後に 1 回だけ実行されます。")]
        [SerializeField]
        [CommandListFunctionName("WorldSlider.Background.OnSpawn")]
        CommandListData _onSpawnCommands = new();

        public bool Enabled => _enabled;
        public DynamicValue<BaseRuntimeTemplatePreset> TemplatePreset => _templatePreset;
        public string AnimationChannelTag => string.IsNullOrWhiteSpace(_animationChannelTag) ? "default" : _animationChannelTag.Trim();
        public bool AllowPooling => _allowPooling;
        public float DepthOffset => Mathf.Max(0f, _depthOffset);
        public CommandListData OnSpawnCommands => _onSpawnCommands;

        internal WorldSliderBackgroundVisualizerSettings CreateRuntimeCopy()
        {
            return new WorldSliderBackgroundVisualizerSettings
            {
                _enabled = _enabled,
                _templatePreset = _templatePreset,
                _animationChannelTag = _animationChannelTag,
                _allowPooling = _allowPooling,
                _depthOffset = _depthOffset,
                _onSpawnCommands = CloneCommandList(_onSpawnCommands),
            };
        }

        internal void ApplyMutation(
            WorldSliderVisualizerRuntimeMutation mutation,
            ICommandListRuntimeMutationService? mutationService)
        {
            if (mutation == null)
                return;

            if (mutation.ApplyBackground)
            {
                _enabled = mutation.BackgroundEnabled;
                _templatePreset = mutation.BackgroundTemplatePreset;
                _animationChannelTag = mutation.BackgroundAnimationChannelTag;
                _allowPooling = mutation.BackgroundAllowPooling;
                _depthOffset = mutation.BackgroundDepthOffset;
            }

            if (mutation.ApplyBackgroundSpawnCommands)
            {
                _onSpawnCommands ??= new CommandListData();
                _onSpawnCommands.ApplyRuntimeMutation(mutation.BackgroundSpawnCommands, mutationService);
            }
        }

        internal void BindDebugOwners(Object owner, string prefix)
        {
            _onSpawnCommands?.BindDebugOwner(owner, $"{prefix}.{nameof(_onSpawnCommands)}");
        }

        static CommandListData CloneCommandList(CommandListData? source)
        {
            var clone = new CommandListData();
            if (source != null)
                clone.SetCommands(source);
            return clone;
        }
    }

    [System.Serializable]
    public sealed class WorldSliderSimpleVisualizerSettings
    {
        [LabelText("Fill Axis")]
        [Tooltip("Area 矩形のどちらの辺方向へ bar を伸ばすかを指定します。")]
        [SerializeField]
        WorldSliderAreaFillAxis _fillAxis = WorldSliderAreaFillAxis.SizeX;

        [LabelText("Origin Side")]
        [Tooltip("Fill が始まる側です。Min は Area の負側、Max は正側から伸びます。")]
        [SerializeField]
        WorldSliderAreaOriginSide _originSide = WorldSliderAreaOriginSide.Min;

        [LabelText("Render Backend")]
        [Tooltip("Simple 表示を scene 内の SpriteRenderer で描くか、runtime bar を生成して描くかを切り替えます。")]
        [SerializeField]
        WorldSliderSimpleRenderBackend _renderBackend = WorldSliderSimpleRenderBackend.SceneSpriteRenderer;

        [ShowIf(nameof(UsesRuntimeBar))]
        [LabelText("Runtime Bar Template")]
        [Tooltip("Simple の RuntimeGeneratedBar backend で生成する runtime template です。")]
        [SerializeField]
        DynamicValue<BaseRuntimeTemplatePreset> _runtimeBarTemplatePreset;

        [ShowIf(nameof(UsesRuntimeBar))]
        [LabelText("Animation Channel Tag")]
        [Tooltip("Simple runtime bar 生成後に IAnimationSpriteHubService から見た目ターゲットを取得する channel tag です。既定は default です。")]
        [SerializeField]
        string _runtimeBarAnimationChannelTag = "default";

        [ShowIf(nameof(UsesRuntimeBar))]
        [LabelText("Bar Span Scale")]
        [Tooltip("Simple runtime bar の major-axis coverage です。1 で box 全幅、0.8 で 80% を使います。")]
        [MinValue(0f)]
        [SerializeField]
        DynamicValue<float> _runtimeBarSpanScale = DynamicValueExtensions.FromLiteral(1f);

        [ShowIf(nameof(UsesRuntimeBar))]
        [LabelText("Allow Pooling")]
        [Tooltip("Simple runtime bar の生成/破棄で pool を使うかを決めます。")]
        [SerializeField]
        bool _allowPooling = true;

        [ShowIf(nameof(UsesRuntimeBar))]
        [LabelText("On Bar Spawn")]
        [Tooltip("Simple runtime bar を生成し、初回 geometry を適用した直後に 1 回だけ実行されます。")]
        [SerializeField]
        [CommandListFunctionName("WorldSlider.Simple.OnBarSpawn")]
        CommandListData _onBarSpawnCommands = new();

        public WorldSliderAreaFillAxis FillAxis => _fillAxis;
        public WorldSliderAreaOriginSide OriginSide => _originSide;
        public WorldSliderSimpleRenderBackend RenderBackend => _renderBackend;
        public DynamicValue<BaseRuntimeTemplatePreset> RuntimeBarTemplatePreset => _runtimeBarTemplatePreset;
        public string RuntimeBarAnimationChannelTag => string.IsNullOrWhiteSpace(_runtimeBarAnimationChannelTag) ? "default" : _runtimeBarAnimationChannelTag.Trim();
        public DynamicValue<float> RuntimeBarSpanScale => _runtimeBarSpanScale;
        public bool AllowPooling => _allowPooling;
        public CommandListData OnBarSpawnCommands => _onBarSpawnCommands;

        bool UsesRuntimeBar() => _renderBackend == WorldSliderSimpleRenderBackend.RuntimeGeneratedBar;

        internal WorldSliderSimpleVisualizerSettings CreateRuntimeCopy()
        {
            return new WorldSliderSimpleVisualizerSettings
            {
                _fillAxis = _fillAxis,
                _originSide = _originSide,
                _renderBackend = _renderBackend,
                _runtimeBarTemplatePreset = _runtimeBarTemplatePreset,
                _runtimeBarAnimationChannelTag = _runtimeBarAnimationChannelTag,
                _runtimeBarSpanScale = _runtimeBarSpanScale,
                _allowPooling = _allowPooling,
                _onBarSpawnCommands = CloneCommandList(_onBarSpawnCommands),
            };
        }

        internal void ApplyMutation(
            WorldSliderVisualizerRuntimeMutation mutation,
            ICommandListRuntimeMutationService? mutationService)
        {
            if (mutation == null)
                return;

            if (mutation.ApplySimpleLayout)
            {
                _fillAxis = mutation.SimpleFillAxis;
                _originSide = mutation.SimpleOriginSide;
            }

            if (mutation.ApplySimpleBackend)
            {
                _renderBackend = mutation.SimpleRenderBackend;
                _runtimeBarTemplatePreset = mutation.SimpleRuntimeBarTemplatePreset;
                _runtimeBarAnimationChannelTag = mutation.SimpleRuntimeBarAnimationChannelTag;
                _runtimeBarSpanScale = mutation.SimpleRuntimeBarSpanScale;
                _allowPooling = mutation.SimpleAllowPooling;
            }

            if (mutation.ApplySimpleSpawnCommands)
            {
                _onBarSpawnCommands ??= new CommandListData();
                _onBarSpawnCommands.ApplyRuntimeMutation(mutation.SimpleSpawnCommands, mutationService);
            }
        }

        internal void BindDebugOwners(Object owner, string prefix)
        {
            _onBarSpawnCommands?.BindDebugOwner(owner, $"{prefix}.{nameof(_onBarSpawnCommands)}");
        }

        static CommandListData CloneCommandList(CommandListData? source)
        {
            var clone = new CommandListData();
            if (source != null)
                clone.SetCommands(source);
            return clone;
        }
    }

    [System.Serializable]
    public abstract class WorldSliderSegmentEntryBase
    {
        public abstract float ResolveRawValue(IDynamicContext context, float minValue, float maxValue);
        public abstract void WritePayloadVars(IVarStore vars, int entryIndex, float entryRawValue, float entryNormalizedValue);
        public abstract string GetDebugLabel();
        public abstract CommandListData? GetCommands(WorldSliderSegmentCrossingDirection direction);
        public abstract WorldSliderSegmentEntryBase CreateRuntimeCopy();
        internal abstract void BindDebugOwners(Object owner, string prefix);
    }

    [System.Serializable]
    public sealed class WorldSliderCommandSegmentEntry : WorldSliderSegmentEntryBase
    {
        [LabelText("Raw Value")]
        [Tooltip("この entry が存在する生値位置です。player range 内へ clamp されます。")]
        [SerializeField]
        float _rawValue;

        [LabelText("Display Label")]
        [Tooltip("debug や editor 上で entry を識別するための任意ラベルです。空の場合は Raw Value を使用します。")]
        [SerializeField]
        string _displayLabel = string.Empty;

        [LabelText("On Reach Up")]
        [Tooltip("displayed 値が下からこの entry に到達したときに実行される command list です。")]
        [SerializeField]
        [CommandListFunctionName("WorldSlider.Entry.OnReachUp")]
        CommandListData _onReachUpCommands = new();

        [LabelText("On Reach Down")]
        [Tooltip("displayed 値が上からこの entry に到達したときに実行される command list です。")]
        [SerializeField]
        [CommandListFunctionName("WorldSlider.Entry.OnReachDown")]
        CommandListData _onReachDownCommands = new();

        public float RawValue => _rawValue;
        public string DisplayLabel => _displayLabel ?? string.Empty;
        public CommandListData OnReachUpCommands => _onReachUpCommands;
        public CommandListData OnReachDownCommands => _onReachDownCommands;

        public override float ResolveRawValue(IDynamicContext context, float minValue, float maxValue)
        {
            _ = context;
            _ = minValue;
            _ = maxValue;
            return _rawValue;
        }

        public override void WritePayloadVars(IVarStore vars, int entryIndex, float entryRawValue, float entryNormalizedValue)
        {
            _ = vars;
            _ = entryIndex;
            _ = entryRawValue;
            _ = entryNormalizedValue;
        }

        public override string GetDebugLabel()
        {
            if (!string.IsNullOrWhiteSpace(_displayLabel))
                return _displayLabel;

            return _rawValue.ToString("0.###");
        }

        public override CommandListData? GetCommands(WorldSliderSegmentCrossingDirection direction)
        {
            return direction == WorldSliderSegmentCrossingDirection.Increase
                ? _onReachUpCommands
                : _onReachDownCommands;
        }

        public override WorldSliderSegmentEntryBase CreateRuntimeCopy()
        {
            return new WorldSliderCommandSegmentEntry
            {
                _rawValue = _rawValue,
                _displayLabel = _displayLabel,
                _onReachUpCommands = CloneCommandList(_onReachUpCommands),
                _onReachDownCommands = CloneCommandList(_onReachDownCommands),
            };
        }

        internal override void BindDebugOwners(Object owner, string prefix)
        {
            _onReachUpCommands?.BindDebugOwner(owner, $"{prefix}.{nameof(_onReachUpCommands)}");
            _onReachDownCommands?.BindDebugOwner(owner, $"{prefix}.{nameof(_onReachDownCommands)}");
        }

        static CommandListData CloneCommandList(CommandListData? source)
        {
            var clone = new CommandListData();
            if (source != null)
                clone.SetCommands(source);
            return clone;
        }
    }

    [System.Serializable]
    public sealed class WorldSliderSegmentedVisualizerSettings
    {
        [BoxGroup("Layout")]
        [LabelText("Fill Axis")]
        [Tooltip("Area 矩形のどちらの辺方向へ segment を並べるかを指定します。")]
        [SerializeField]
        WorldSliderAreaFillAxis _fillAxis = WorldSliderAreaFillAxis.SizeX;

        [BoxGroup("Layout")]
        [LabelText("Origin Side")]
        [Tooltip("Segment fill の開始側です。Min は Area の負側、Max は正側から並びます。")]
        [SerializeField]
        WorldSliderAreaOriginSide _originSide = WorldSliderAreaOriginSide.Min;

        [BoxGroup("Layout")]
        [LabelText("Placement Mode")]
        [Tooltip("等間隔で区切るか、Entries を個別定義して区切るかを選びます。")]
        [SerializeField]
        WorldSliderSegmentPlacementMode _placementMode = WorldSliderSegmentPlacementMode.CustomEntries;

        [BoxGroup("Layout")]
        [ShowIf(nameof(IsEqualInterval))]
        [LabelText("Interval Step")]
        [Tooltip("EqualInterval のときに使う区切り間隔の生値です。min/max の内側にだけ boundary を作ります。")]
        [SerializeField]
        DynamicValue<float> _intervalStep = DynamicValueExtensions.FromLiteral(10f);

        [BoxGroup("Layout")]
        [LabelText("Bar Span Scale")]
        [Tooltip("各 segment bar の major-axis coverage です。1 で割当 box 全幅、0.8 で 80% を使います。bar 同士の隙間もこの値で表現します。")]
        [MinValue(0f)]
        [SerializeField]
        DynamicValue<float> _barSpanScale = DynamicValueExtensions.FromLiteral(1f);

        [BoxGroup("Runtime")]
        [LabelText("Spawn Segment Bars")]
        [Tooltip("Segment 本体を runtime 生成するかを切り替えます。")]
        [SerializeField]
        bool _spawnSegmentBars = true;

        [BoxGroup("Runtime")]
        [ShowIf(nameof(_spawnSegmentBars))]
        [LabelText("Segment Bar Template")]
        [Tooltip("各 segment bar の生成に使う runtime template です。AnimationSpriteHub の channel tag、または root/child の SpriteRenderer を見た目ターゲットとして使用します。")]
        [SerializeField]
        DynamicValue<BaseRuntimeTemplatePreset> _segmentBarTemplatePreset;

        [BoxGroup("Runtime")]
        [ShowIf(nameof(_spawnSegmentBars))]
        [LabelText("Animation Channel Tag")]
        [Tooltip("Segment bar 生成後に IAnimationSpriteHubService から見た目ターゲットを取得する channel tag です。既定は default です。")]
        [SerializeField]
        string _segmentBarAnimationChannelTag = "default";

        [BoxGroup("Runtime")]
        [LabelText("Spawn Markers")]
        [Tooltip("entry 境界 marker を runtime 生成するかを切り替えます。")]
        [SerializeField]
        bool _spawnMarkers = true;

        [BoxGroup("Runtime")]
        [ShowIf(nameof(_spawnMarkers))]
        [LabelText("Marker Template")]
        [Tooltip("各 marker の生成に使う runtime template です。")]
        [SerializeField]
        DynamicValue<BaseRuntimeTemplatePreset> _markerTemplatePreset;

        [BoxGroup("Runtime")]
        [LabelText("Allow Pooling")]
        [Tooltip("Segment bar と marker の生成/破棄で pool を使うかを決めます。")]
        [SerializeField]
        bool _allowPooling = true;

        [BoxGroup("Runtime")]
        [LabelText("On Segment Bar Spawn")]
        [Tooltip("各 segment bar を生成し、初回 geometry を適用した直後に 1 回だけ実行されます。")]
        [SerializeField]
        [CommandListFunctionName("WorldSlider.Segmented.OnBarSpawn")]
        CommandListData _onSegmentBarSpawnCommands = new();

        [BoxGroup("Runtime")]
        [LabelText("On Marker Spawn")]
        [Tooltip("各 marker を生成し、初回 geometry を適用した直後に 1 回だけ実行されます。")]
        [SerializeField]
        [CommandListFunctionName("WorldSlider.Segmented.OnMarkerSpawn")]
        CommandListData _onMarkerSpawnCommands = new();

        [BoxGroup("Entries")]
        [ShowIf(nameof(IsCustomEntries))]
        [LabelText("Entries")]
        [Tooltip("CustomEntries のときに使う区切り定義です。raw value で並べ替え、範囲外は clamp、重複は collapse されます。")]
        [SerializeReference]
        List<WorldSliderSegmentEntryBase> _entries = new();

        public WorldSliderAreaFillAxis FillAxis => _fillAxis;
        public WorldSliderAreaOriginSide OriginSide => _originSide;
        public WorldSliderSegmentPlacementMode PlacementMode => _placementMode;
        public DynamicValue<float> IntervalStep => _intervalStep;
        public DynamicValue<float> BarSpanScale => _barSpanScale;
        public bool SpawnSegmentBars => _spawnSegmentBars;
        public DynamicValue<BaseRuntimeTemplatePreset> SegmentBarTemplatePreset => _segmentBarTemplatePreset;
        public string SegmentBarAnimationChannelTag => string.IsNullOrWhiteSpace(_segmentBarAnimationChannelTag) ? "default" : _segmentBarAnimationChannelTag.Trim();
        public bool SpawnMarkers => _spawnMarkers;
        public DynamicValue<BaseRuntimeTemplatePreset> MarkerTemplatePreset => _markerTemplatePreset;
        public bool AllowPooling => _allowPooling;
        public CommandListData OnSegmentBarSpawnCommands => _onSegmentBarSpawnCommands;
        public CommandListData OnMarkerSpawnCommands => _onMarkerSpawnCommands;
        public IReadOnlyList<WorldSliderSegmentEntryBase> Entries => _entries;

        bool IsEqualInterval() => _placementMode == WorldSliderSegmentPlacementMode.EqualInterval;
        bool IsCustomEntries() => _placementMode == WorldSliderSegmentPlacementMode.CustomEntries;

        internal WorldSliderSegmentedVisualizerSettings CreateRuntimeCopy()
        {
            var copy = new WorldSliderSegmentedVisualizerSettings
            {
                _fillAxis = _fillAxis,
                _originSide = _originSide,
                _placementMode = _placementMode,
                _intervalStep = _intervalStep,
                _barSpanScale = _barSpanScale,
                _spawnSegmentBars = _spawnSegmentBars,
                _segmentBarTemplatePreset = _segmentBarTemplatePreset,
                _segmentBarAnimationChannelTag = _segmentBarAnimationChannelTag,
                _spawnMarkers = _spawnMarkers,
                _markerTemplatePreset = _markerTemplatePreset,
                _allowPooling = _allowPooling,
                _onSegmentBarSpawnCommands = CloneCommandList(_onSegmentBarSpawnCommands),
                _onMarkerSpawnCommands = CloneCommandList(_onMarkerSpawnCommands),
            };

            if (_entries != null)
            {
                copy._entries = new List<WorldSliderSegmentEntryBase>(_entries.Count);
                for (int i = 0; i < _entries.Count; i++)
                {
                    if (_entries[i] == null)
                        continue;
                    copy._entries.Add(_entries[i].CreateRuntimeCopy());
                }
            }

            return copy;
        }

        internal void ApplyMutation(
            WorldSliderVisualizerRuntimeMutation mutation,
            ICommandListRuntimeMutationService? mutationService)
        {
            if (mutation == null)
                return;

            if (mutation.ApplySegmentedLayout)
            {
                _fillAxis = mutation.SegmentedFillAxis;
                _originSide = mutation.SegmentedOriginSide;
                _placementMode = mutation.SegmentedPlacementMode;
                _intervalStep = mutation.SegmentedIntervalStep;
                _barSpanScale = mutation.SegmentedBarSpanScale;
            }

            if (mutation.ApplySegmentedRuntime)
            {
                _spawnSegmentBars = mutation.SpawnSegmentBars;
                _segmentBarTemplatePreset = mutation.SegmentBarTemplatePreset;
                _segmentBarAnimationChannelTag = mutation.SegmentBarAnimationChannelTag;
                _spawnMarkers = mutation.SpawnMarkers;
                _markerTemplatePreset = mutation.MarkerTemplatePreset;
                _allowPooling = mutation.SegmentedAllowPooling;
            }

            if (mutation.ApplySegmentBarSpawnCommands)
            {
                _onSegmentBarSpawnCommands ??= new CommandListData();
                _onSegmentBarSpawnCommands.ApplyRuntimeMutation(mutation.SegmentBarSpawnCommands, mutationService);
            }

            if (mutation.ApplyMarkerSpawnCommands)
            {
                _onMarkerSpawnCommands ??= new CommandListData();
                _onMarkerSpawnCommands.ApplyRuntimeMutation(mutation.MarkerSpawnCommands, mutationService);
            }
        }

        internal void BindDebugOwners(Object owner, string prefix)
        {
            _onSegmentBarSpawnCommands?.BindDebugOwner(owner, $"{prefix}.{nameof(_onSegmentBarSpawnCommands)}");
            _onMarkerSpawnCommands?.BindDebugOwner(owner, $"{prefix}.{nameof(_onMarkerSpawnCommands)}");

            if (_entries == null)
                return;

            for (int i = 0; i < _entries.Count; i++)
            {
                _entries[i]?.BindDebugOwners(owner, $"{prefix}.{nameof(_entries)}[{i}]");
            }
        }

        static CommandListData CloneCommandList(CommandListData? source)
        {
            var clone = new CommandListData();
            if (source != null)
                clone.SetCommands(source);
            return clone;
        }
    }

    [System.Serializable]
    public sealed class WorldSliderVisualizerPreset : IDynamicManagedRefValue
    {
        [BoxGroup("Mode")]
        [LabelText("Visualizer Mode")]
        [Tooltip("WorldSlider の見た目を Simple 表示にするか、Segmented 表示にするかを切り替えます。")]
        [SerializeField]
        WorldSliderVisualizerMode _mode = WorldSliderVisualizerMode.Simple;

        [BoxGroup("Simple")]
        [ShowIf(nameof(IsSimpleMode))]
        [InlineProperty]
        [SerializeField]
        WorldSliderSimpleVisualizerSettings _simple = new();

        [BoxGroup("Background")]
        [InlineProperty]
        [SerializeField]
        WorldSliderBackgroundVisualizerSettings _background = new();

        [BoxGroup("Segmented")]
        [ShowIf(nameof(IsSegmentedMode))]
        [InlineProperty]
        [SerializeField]
        WorldSliderSegmentedVisualizerSettings _segmented = new();

        public WorldSliderVisualizerMode Mode => _mode;
        public WorldSliderSimpleVisualizerSettings Simple => _simple;
        public WorldSliderBackgroundVisualizerSettings Background => _background;
        public WorldSliderSegmentedVisualizerSettings Segmented => _segmented;

        bool IsSimpleMode() => _mode == WorldSliderVisualizerMode.Simple;
        bool IsSegmentedMode() => _mode == WorldSliderVisualizerMode.Segmented;

        internal WorldSliderVisualizerPreset CreateRuntimeCopy()
        {
            return new WorldSliderVisualizerPreset
            {
                _mode = _mode,
                _simple = _simple?.CreateRuntimeCopy() ?? new WorldSliderSimpleVisualizerSettings(),
                _background = _background?.CreateRuntimeCopy() ?? new WorldSliderBackgroundVisualizerSettings(),
                _segmented = _segmented?.CreateRuntimeCopy() ?? new WorldSliderSegmentedVisualizerSettings(),
            };
        }

        internal void ApplyMutation(
            WorldSliderVisualizerRuntimeMutation mutation,
            ICommandListRuntimeMutationService? mutationService)
        {
            if (mutation == null)
                return;

            if (mutation.ApplyMode)
                _mode = mutation.Mode;

            _simple ??= new WorldSliderSimpleVisualizerSettings();
            _background ??= new WorldSliderBackgroundVisualizerSettings();
            _segmented ??= new WorldSliderSegmentedVisualizerSettings();
            _simple.ApplyMutation(mutation, mutationService);
            _background.ApplyMutation(mutation, mutationService);
            _segmented.ApplyMutation(mutation, mutationService);
        }

        internal void BindDebugOwners(Object owner, string prefix)
        {
            _simple?.BindDebugOwners(owner, $"{prefix}.{nameof(_simple)}");
            _background?.BindDebugOwners(owner, $"{prefix}.{nameof(_background)}");
            _segmented?.BindDebugOwners(owner, $"{prefix}.{nameof(_segmented)}");
        }
    }

    [CreateAssetMenu(
        menuName = "Game/UI/World Slider/Visualizer Preset",
        fileName = "WorldSliderVisualizerPreset")]
    public sealed class WorldSliderVisualizerPresetSO : ScriptableObject, IDynamicValueAsset<WorldSliderVisualizerPreset>
    {
        [SerializeReference, InlineProperty, HideLabel]
        WorldSliderVisualizerPreset? _preset = new();

        public WorldSliderVisualizerPreset? Preset
        {
            get
            {
                EnsurePreset();
                return _preset;
            }
        }

        void OnEnable()
        {
            EnsurePreset();
            BindDebugOwners();
        }

        void OnValidate()
        {
            EnsurePreset();
            BindDebugOwners();
        }

        void EnsurePreset()
        {
            if (_preset == null)
                _preset = new WorldSliderVisualizerPreset();
        }

        void BindDebugOwners()
        {
            _preset?.BindDebugOwners(this, nameof(_preset));
        }
    }
}
