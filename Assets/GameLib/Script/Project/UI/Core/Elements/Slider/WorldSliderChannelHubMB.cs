#nullable enable
using System;
using System.Collections.Generic;
using Game.Common;
using Game.Commands.VNext;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.UI
{
    [Serializable]
    public sealed class WorldSliderChannelOptions : IWorldSliderOptions
    {
        public DynamicValue<WorldSliderVisualizerPreset> VisualizerPresetValue { get; set; } =
            DynamicValue<WorldSliderVisualizerPreset>.FromSource(
                new ManagedRefLiteralSource<WorldSliderVisualizerPreset>(new WorldSliderVisualizerPreset()));

        public DynamicValue<WorldSliderPlayerPreset> PlayerPresetValue { get; set; } =
            DynamicValue<WorldSliderPlayerPreset>.FromSource(
                new ManagedRefLiteralSource<WorldSliderPlayerPreset>(new WorldSliderPlayerPreset()));

        public SpriteRenderer? SimpleBarRenderer { get; set; }
        public Transform? SegmentBarsRoot { get; set; }
        public Transform? SegmentMarkersRoot { get; set; }
        public ActorSource AreaActorSource { get; set; } = new() { Kind = ActorSourceKind.Current };
        public string AreaChannelTag { get; set; } = "default";
        public Transform OwnerTransform { get; set; } = null!;
    }

    [Serializable]
    public sealed class WorldSliderChannelDefinition
    {
        [BoxGroup("Channel")]
        [LabelText("Channel Tag")]
        [Tooltip("WorldSlider channel の識別タグです。空白の場合は default を使用します。")]
        [SerializeField]
        string _channelTag = "default";

        [BoxGroup("Preset")]
        [LabelText("Visualizer Preset")]
        [Tooltip("この channel の見た目側の基準設定です。runtime control の reset でここへ戻ります。")]
        [SerializeField]
        DynamicValue<WorldSliderVisualizerPreset> _visualizerPreset =
            DynamicValue<WorldSliderVisualizerPreset>.FromSource(
                new ManagedRefLiteralSource<WorldSliderVisualizerPreset>(new WorldSliderVisualizerPreset()));

        [BoxGroup("Preset")]
        [LabelText("Player Preset")]
        [Tooltip("この channel の binding/range/transition/command の基準設定です。runtime control の reset でここへ戻ります。")]
        [SerializeField]
        DynamicValue<WorldSliderPlayerPreset> _playerPreset =
            DynamicValue<WorldSliderPlayerPreset>.FromSource(
                new ManagedRefLiteralSource<WorldSliderPlayerPreset>(new WorldSliderPlayerPreset()));

        [BoxGroup("Scene")]
        [LabelText("Simple Bar Renderer")]
        [Tooltip("Simple mode かつ SceneSpriteRenderer backend のときに使う SpriteRenderer です。")]
        [SerializeField]
        SpriteRenderer? _simpleBarRenderer;

        [BoxGroup("Scene")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Area Source\", _areaActorSource)")]
        [Tooltip("Slider の描画範囲を決める AreaChannel を持つ actor / scope です。")]
        [SerializeField]
        ActorSource _areaActorSource = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Scene")]
        [LabelText("Area Channel Tag")]
        [Tooltip("取得対象の AreaChannel tag です。空白の場合は default を使用します。")]
        [SerializeField]
        string _areaChannelTag = "default";

        [BoxGroup("Scene")]
        [LabelText("Segment Bars Root")]
        [Tooltip("Segmented mode の segment bar を生成する親 Transform です。")]
        [SerializeField]
        Transform? _segmentBarsRoot;

        [BoxGroup("Scene")]
        [LabelText("Segment Markers Root")]
        [Tooltip("Segmented mode の marker を生成する親 Transform です。")]
        [SerializeField]
        Transform? _segmentMarkersRoot;

        public string ChannelTag => string.IsNullOrWhiteSpace(_channelTag) ? "default" : _channelTag.Trim();

        internal WorldSliderChannelOptions CreateOptions(Transform ownerTransform)
        {
            return new WorldSliderChannelOptions
            {
                VisualizerPresetValue = _visualizerPreset,
                PlayerPresetValue = _playerPreset,
                SimpleBarRenderer = _simpleBarRenderer,
                SegmentBarsRoot = _segmentBarsRoot,
                SegmentMarkersRoot = _segmentMarkersRoot,
                AreaActorSource = _areaActorSource,
                AreaChannelTag = string.IsNullOrWhiteSpace(_areaChannelTag) ? "default" : _areaChannelTag.Trim(),
                OwnerTransform = ownerTransform,
            };
        }
    }

    [DisallowMultipleComponent]
    public sealed class WorldSliderChannelHubMB : MonoBehaviour, IFeatureInstaller
    {
        [BoxGroup("Channels")]
        [LabelText("Channels")]
        [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = true, ShowFoldout = true)]
        [SerializeField]
        List<WorldSliderChannelDefinition> _channels = new() { new WorldSliderChannelDefinition() };

        public IReadOnlyList<WorldSliderChannelDefinition> Channels => _channels;

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            builder.Register<WorldSliderChannelHubService>(Lifetime.Singleton)
                .WithParameter(scope)
                .WithParameter(this)
                .As<IWorldSliderChannelHubService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .As<ITickable>();
        }
    }
}
