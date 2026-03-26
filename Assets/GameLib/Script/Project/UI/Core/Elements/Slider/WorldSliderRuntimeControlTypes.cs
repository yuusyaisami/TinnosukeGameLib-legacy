#nullable enable
using System;
using Game.Commands.VNext;
using Game.Common;
using Game.DI;
using Game.Scalar;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.UI
{
    public enum WorldSliderSimpleRenderBackend
    {
        SceneSpriteRenderer = 10,
        RuntimeGeneratedBar = 20,
    }

    public enum WorldSliderControlOperation
    {
        SwapPreset = 10,
        MutateSettings = 20,
        ResetRuntimeOverrides = 30,
    }

    public enum WorldSliderSpawnUnitKind
    {
        SimpleBar = 10,
        SegmentBar = 20,
        Marker = 30,
    }

    public interface IWorldSliderRuntimePresetProvider
    {
        WorldSliderVisualizerPreset CurrentVisualizerPreset { get; }
        WorldSliderPlayerPreset CurrentPlayerPreset { get; }
        event Action? OnVisualizerPresetChanged;
        event Action? OnPlayerPresetChanged;
    }

    public interface IWorldSliderControlService
    {
        bool SwapPreset(
            bool applyVisualizer,
            WorldSliderVisualizerPreset? visualizerPreset,
            bool applyPlayer,
            WorldSliderPlayerPreset? playerPreset);

        bool MutateSettings(
            WorldSliderVisualizerRuntimeMutation? visualizerMutation,
            WorldSliderPlayerRuntimeMutation? playerMutation,
            ICommandListRuntimeMutationService? mutationService);

        bool ResetRuntimeOverrides(bool resetVisualizer, bool resetPlayer);
    }

    [Serializable]
    public sealed class WorldSliderPlayerRuntimeMutation
    {
        [BoxGroup("Binding")]
        [ToggleLeft]
        [LabelText("Apply Binding")]
        [Tooltip("現在の player binding entry 設定をこの command で上書きする場合に有効にします。")]
        public bool ApplyBinding;

        [BoxGroup("Binding")]
        [ShowIf(nameof(ApplyBinding))]
        [LabelText("Binding Entries")]
        [Tooltip("Condition=true の entry を候補にし、Order が最も高いものを採用します。候補が 1 件も無い場合は slider を非表示にします。")]
        [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = true, ShowFoldout = true)]
        public System.Collections.Generic.List<WorldSliderPlayerBindingEntry> BindingEntries = new() { new() };

        [BoxGroup("Range")]
        [ToggleLeft]
        [LabelText("Apply Range")]
        [Tooltip("現在の range 設定をこの command で上書きする場合に有効にします。")]
        public bool ApplyRange;

        [BoxGroup("Range")]
        [ShowIf(nameof(ApplyRange))]
        [LabelText("Min Value")]
        [Tooltip("Slider 生値の最小値です。")]
        public DynamicValue<float> MinValue = DynamicValueExtensions.FromLiteral(0f);

        [BoxGroup("Range")]
        [ShowIf(nameof(ApplyRange))]
        [LabelText("Max Value")]
        [Tooltip("Slider 生値の最大値です。")]
        public DynamicValue<float> MaxValue = DynamicValueExtensions.FromLiteral(1f);

        [BoxGroup("Range")]
        [ShowIf(nameof(ApplyRange))]
        [LabelText("Initial Value")]
        [Tooltip("binding が取れない場合に使う初期生値です。")]
        public DynamicValue<float> InitialValue = DynamicValueExtensions.FromLiteral(0f);

        [BoxGroup("Transition")]
        [ToggleLeft]
        [LabelText("Apply Increase Transition")]
        [Tooltip("増加時 transition をこの command で上書きする場合に有効にします。")]
        public bool ApplyIncreaseTransition;

        [BoxGroup("Transition")]
        [ShowIf(nameof(ApplyIncreaseTransition))]
        [InlineProperty]
        [LabelText("Increase Transition")]
        [Tooltip("値が増加したときの delay / duration / ease 設定です。")]
        public WorldSliderTransitionSettings IncreaseTransition = new();

        [BoxGroup("Transition")]
        [ToggleLeft]
        [LabelText("Apply Decrease Transition")]
        [Tooltip("減少時 transition をこの command で上書きする場合に有効にします。")]
        public bool ApplyDecreaseTransition;

        [BoxGroup("Transition")]
        [ShowIf(nameof(ApplyDecreaseTransition))]
        [InlineProperty]
        [LabelText("Decrease Transition")]
        [Tooltip("値が減少したときの delay / duration / ease 設定です。")]
        public WorldSliderTransitionSettings DecreaseTransition = new();

        [BoxGroup("Display")]
        [ToggleLeft]
        [LabelText("Apply Segment Display Mode")]
        [Tooltip("Segmented 表示時の displayed 値量子化モードを変更する場合に有効にします。")]
        public bool ApplySegmentDisplayMode;

        [BoxGroup("Display")]
        [ShowIf(nameof(ApplySegmentDisplayMode))]
        [LabelText("Segment Display Mode")]
        [Tooltip("Continuous のまま描くか、到達済み段で止めるかを切り替えます。")]
        public WorldSliderSegmentDisplayMode SegmentDisplayMode = WorldSliderSegmentDisplayMode.Continuous;

        [BoxGroup("Commands")]
        [ToggleLeft]
        [LabelText("Apply Target Changed Commands")]
        [Tooltip("player の OnTargetValueChangedCommands を runtime mutation する場合に有効にします。")]
        public bool ApplyTargetChangedCommands;

        [BoxGroup("Commands")]
        [ShowIf(nameof(ApplyTargetChangedCommands))]
        [InlineProperty]
        [HideLabel]
        [Tooltip("target changed command list に対する runtime mutation です。Append / Override / ClearAll を選べます。")]
        public CommandListMutationStep TargetChangedCommands = new()
        {
            Operation = CommandListMutationOperation.Override,
        };

        public bool HasAnyMutation()
        {
            return ApplyBinding ||
                   ApplyRange ||
                   ApplyIncreaseTransition ||
                   ApplyDecreaseTransition ||
                   ApplySegmentDisplayMode ||
                   ApplyTargetChangedCommands;
        }
    }

    [Serializable]
    public sealed class WorldSliderVisualizerRuntimeMutation
    {
        [BoxGroup("Mode")]
        [ToggleLeft]
        [LabelText("Apply Mode")]
        [Tooltip("Visualizer の mode をこの command で切り替える場合に有効にします。")]
        public bool ApplyMode;

        [BoxGroup("Mode")]
        [ShowIf(nameof(ApplyMode))]
        [LabelText("Mode")]
        [Tooltip("Simple か Segmented かを切り替えます。切り替え時は旧 visual runtime を破棄して再構築します。")]
        public WorldSliderVisualizerMode Mode = WorldSliderVisualizerMode.Simple;

        [BoxGroup("Simple/Layout")]
        [ToggleLeft]
        [LabelText("Apply Simple Layout")]
        [Tooltip("Simple の fill axis / origin side を変更する場合に有効にします。")]
        public bool ApplySimpleLayout;

        [BoxGroup("Simple/Layout")]
        [ShowIf(nameof(ApplySimpleLayout))]
        [LabelText("Fill Axis")]
        [Tooltip("Area 矩形のどちらの辺方向へ bar を伸ばすかを指定します。")]
        public WorldSliderAreaFillAxis SimpleFillAxis = WorldSliderAreaFillAxis.SizeX;

        [BoxGroup("Simple/Layout")]
        [ShowIf(nameof(ApplySimpleLayout))]
        [LabelText("Origin Side")]
        [Tooltip("Fill が始まる側です。Min は負側、Max は正側から伸びます。")]
        public WorldSliderAreaOriginSide SimpleOriginSide = WorldSliderAreaOriginSide.Min;

        [BoxGroup("Simple/Runtime")]
        [ToggleLeft]
        [LabelText("Apply Simple Backend")]
        [Tooltip("Simple の render backend と runtime template 設定を変更する場合に有効にします。")]
        public bool ApplySimpleBackend;

        [BoxGroup("Simple/Runtime")]
        [ShowIf(nameof(ApplySimpleBackend))]
        [LabelText("Render Backend")]
        [Tooltip("SceneSpriteRenderer か RuntimeGeneratedBar を選びます。")]
        public WorldSliderSimpleRenderBackend SimpleRenderBackend = WorldSliderSimpleRenderBackend.SceneSpriteRenderer;

        [BoxGroup("Simple/Runtime")]
        [ShowIf(nameof(ApplySimpleBackend))]
        [LabelText("Runtime Bar Template")]
        [Tooltip("Simple runtime bar を生成する template です。")]
        public DynamicValue<BaseRuntimeTemplatePreset> SimpleRuntimeBarTemplatePreset;

        [BoxGroup("Simple/Runtime")]
        [ShowIf(nameof(ApplySimpleBackend))]
        [LabelText("Animation Channel Tag")]
        [Tooltip("Simple runtime bar 生成後に IAnimationSpriteHubService から見た目ターゲットを取得する channel tag です。")]
        public string SimpleRuntimeBarAnimationChannelTag = "default";

        [BoxGroup("Simple/Runtime")]
        [ShowIf(nameof(ApplySimpleBackend))]
        [MinValue(0f)]
        [LabelText("Bar Span Scale")]
        [Tooltip("Simple runtime bar の major-axis coverage です。1 で box 全幅、0.8 で 80% を使います。")]
        public DynamicValue<float> SimpleRuntimeBarSpanScale = DynamicValueExtensions.FromLiteral(1f);

        [BoxGroup("Simple/Runtime")]
        [ShowIf(nameof(ApplySimpleBackend))]
        [LabelText("Allow Pooling")]
        [Tooltip("Simple runtime bar の生成/破棄に pool を使うかを決めます。")]
        public bool SimpleAllowPooling = true;

        [BoxGroup("Simple/Runtime")]
        [ToggleLeft]
        [LabelText("Apply Simple Spawn Commands")]
        [Tooltip("Simple runtime bar の OnBarSpawnCommands を runtime mutation する場合に有効にします。")]
        public bool ApplySimpleSpawnCommands;

        [BoxGroup("Simple/Runtime")]
        [ShowIf(nameof(ApplySimpleSpawnCommands))]
        [InlineProperty]
        [HideLabel]
        [Tooltip("Simple runtime bar の spawn command list に対する runtime mutation です。")]
        public CommandListMutationStep SimpleSpawnCommands = new()
        {
            Operation = CommandListMutationOperation.Override,
        };

        [BoxGroup("Segmented/Layout")]
        [ToggleLeft]
        [LabelText("Apply Segmented Layout")]
        [Tooltip("Segmented の layout 設定を変更する場合に有効にします。")]
        public bool ApplySegmentedLayout;

        [BoxGroup("Segmented/Layout")]
        [ShowIf(nameof(ApplySegmentedLayout))]
        [LabelText("Fill Axis")]
        [Tooltip("Area 矩形のどちらの辺方向へ segment を並べるかを指定します。")]
        public WorldSliderAreaFillAxis SegmentedFillAxis = WorldSliderAreaFillAxis.SizeX;

        [BoxGroup("Segmented/Layout")]
        [ShowIf(nameof(ApplySegmentedLayout))]
        [LabelText("Origin Side")]
        [Tooltip("Segment fill の開始側です。Min は負側、Max は正側から並びます。")]
        public WorldSliderAreaOriginSide SegmentedOriginSide = WorldSliderAreaOriginSide.Min;

        [BoxGroup("Segmented/Layout")]
        [ShowIf(nameof(ApplySegmentedLayout))]
        [LabelText("Placement Mode")]
        [Tooltip("EqualInterval か CustomEntries かを切り替えます。entry 構成自体の変更は preset swap で行います。")]
        public WorldSliderSegmentPlacementMode SegmentedPlacementMode = WorldSliderSegmentPlacementMode.CustomEntries;

        [BoxGroup("Segmented/Layout")]
        [ShowIf(nameof(ApplySegmentedLayout))]
        [LabelText("Interval Step")]
        [Tooltip("EqualInterval の区切り間隔です。")]
        public DynamicValue<float> SegmentedIntervalStep = DynamicValueExtensions.FromLiteral(10f);

        [BoxGroup("Segmented/Layout")]
        [ShowIf(nameof(ApplySegmentedLayout))]
        [MinValue(0f)]
        [LabelText("Bar Span Scale")]
        [Tooltip("各 segment bar の major-axis coverage です。1 で割当 box 全幅、0.8 で 80% を使います。bar 同士の隙間もこの値で表現します。")]
        public DynamicValue<float> SegmentedBarSpanScale = DynamicValueExtensions.FromLiteral(1f);

        [BoxGroup("Segmented/Runtime")]
        [ToggleLeft]
        [LabelText("Apply Segmented Runtime")]
        [Tooltip("Segmented の runtime template / pooling / spawn flags を変更する場合に有効にします。")]
        public bool ApplySegmentedRuntime;

        [BoxGroup("Segmented/Runtime")]
        [ShowIf(nameof(ApplySegmentedRuntime))]
        [LabelText("Spawn Segment Bars")]
        [Tooltip("segment bar を runtime 生成するかを切り替えます。")]
        public bool SpawnSegmentBars = true;

        [BoxGroup("Segmented/Runtime")]
        [ShowIf(nameof(ApplySegmentedRuntime))]
        [LabelText("Segment Bar Template")]
        [Tooltip("segment bar を生成する template です。")]
        public DynamicValue<BaseRuntimeTemplatePreset> SegmentBarTemplatePreset;

        [BoxGroup("Segmented/Runtime")]
        [ShowIf(nameof(ApplySegmentedRuntime))]
        [LabelText("Animation Channel Tag")]
        [Tooltip("segment bar 生成後に IAnimationSpriteHubService から見た目ターゲットを取得する channel tag です。")]
        public string SegmentBarAnimationChannelTag = "default";

        [BoxGroup("Segmented/Runtime")]
        [ShowIf(nameof(ApplySegmentedRuntime))]
        [LabelText("Spawn Markers")]
        [Tooltip("marker を runtime 生成するかを切り替えます。")]
        public bool SpawnMarkers = true;

        [BoxGroup("Segmented/Runtime")]
        [ShowIf(nameof(ApplySegmentedRuntime))]
        [LabelText("Marker Template")]
        [Tooltip("marker を生成する template です。")]
        public DynamicValue<BaseRuntimeTemplatePreset> MarkerTemplatePreset;

        [BoxGroup("Segmented/Runtime")]
        [ShowIf(nameof(ApplySegmentedRuntime))]
        [LabelText("Allow Pooling")]
        [Tooltip("Segmented runtime unit の生成/破棄で pool を使うかを決めます。")]
        public bool SegmentedAllowPooling = true;

        [BoxGroup("Segmented/Runtime")]
        [ToggleLeft]
        [LabelText("Apply Segment Bar Spawn Commands")]
        [Tooltip("segment bar の spawn command list を runtime mutation する場合に有効にします。")]
        public bool ApplySegmentBarSpawnCommands;

        [BoxGroup("Segmented/Runtime")]
        [ShowIf(nameof(ApplySegmentBarSpawnCommands))]
        [InlineProperty]
        [HideLabel]
        [Tooltip("segment bar の spawn command list に対する runtime mutation です。")]
        public CommandListMutationStep SegmentBarSpawnCommands = new()
        {
            Operation = CommandListMutationOperation.Override,
        };

        [BoxGroup("Segmented/Runtime")]
        [ToggleLeft]
        [LabelText("Apply Marker Spawn Commands")]
        [Tooltip("marker の spawn command list を runtime mutation する場合に有効にします。")]
        public bool ApplyMarkerSpawnCommands;

        [BoxGroup("Segmented/Runtime")]
        [ShowIf(nameof(ApplyMarkerSpawnCommands))]
        [InlineProperty]
        [HideLabel]
        [Tooltip("marker の spawn command list に対する runtime mutation です。")]
        public CommandListMutationStep MarkerSpawnCommands = new()
        {
            Operation = CommandListMutationOperation.Override,
        };

        public bool HasAnyMutation()
        {
            return ApplyMode ||
                   ApplySimpleLayout ||
                   ApplySimpleBackend ||
                   ApplySimpleSpawnCommands ||
                   ApplySegmentedLayout ||
                   ApplySegmentedRuntime ||
                   ApplySegmentBarSpawnCommands ||
                   ApplyMarkerSpawnCommands;
        }
    }
}
