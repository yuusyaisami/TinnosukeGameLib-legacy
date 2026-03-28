#nullable enable
using System;
using System.Collections.Generic;
using Game.Commands.VNext;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.UI
{
    [Serializable]
    public sealed class SliderChannelOptions : ISliderOptions
    {
        public DynamicValue<SliderVisualizerPreset> VisualizerPresetValue { get; set; } =
            DynamicValue<SliderVisualizerPreset>.FromSource(
                new ManagedRefLiteralSource<SliderVisualizerPreset>(new SliderVisualizerPreset()));

        public DynamicValue<SliderPlayerPreset> PlayerPresetValue { get; set; } =
            DynamicValue<SliderPlayerPreset>.FromSource(
                new ManagedRefLiteralSource<SliderPlayerPreset>(new SliderPlayerPreset()));

        public Transform? SegmentBarsRoot { get; set; }
        public Transform? SegmentMarkersRoot { get; set; }
        public ActorSource AreaActorSource { get; set; } = new() { Kind = ActorSourceKind.Current };
        public string AreaChannelTag { get; set; } = "default";
        public SliderRangeSourceMode RangeSourceMode { get; set; } = SliderRangeSourceMode.AreaChannel;
        public RectTransform? RangeRectTransform { get; set; }
        public Transform OwnerTransform { get; set; } = null!;
    }

    [Serializable]
    public sealed class SliderChannelDefinition
    {
        [BoxGroup("Channel")]
        [LabelText("Channel Tag")]
        [SerializeField]
        string _channelTag = "default";

        [BoxGroup("Preset")]
        [LabelText("Visualizer Preset")]
        [SerializeField]
        DynamicValue<SliderVisualizerPreset> _visualizerPreset =
            DynamicValue<SliderVisualizerPreset>.FromSource(
                new ManagedRefLiteralSource<SliderVisualizerPreset>(new SliderVisualizerPreset()));

        [BoxGroup("Preset")]
        [LabelText("Player Preset")]
        [SerializeField]
        DynamicValue<SliderPlayerPreset> _playerPreset =
            DynamicValue<SliderPlayerPreset>.FromSource(
                new ManagedRefLiteralSource<SliderPlayerPreset>(new SliderPlayerPreset()));

        [BoxGroup("Range")]
        [LabelText("Range Source Mode")]
        [SerializeField]
        SliderRangeSourceMode _rangeSourceMode = SliderRangeSourceMode.AreaChannel;

        [BoxGroup("Range")]
        [ShowIf(nameof(UsesAreaChannel))]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Area Source\", _areaActorSource)")]
        [SerializeField]
        ActorSource _areaActorSource = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Range")]
        [ShowIf(nameof(UsesAreaChannel))]
        [LabelText("Area Channel Tag")]
        [SerializeField]
        string _areaChannelTag = "default";

        [BoxGroup("Range")]
        [ShowIf(nameof(UsesRectTransform))]
        [LabelText("Range RectTransform")]
        [SerializeField]
        RectTransform? _rangeRectTransform;

        [BoxGroup("Scene")]
        [LabelText("Segment Bars Root")]
        [SerializeField]
        Transform? _segmentBarsRoot;

        [BoxGroup("Scene")]
        [LabelText("Segment Markers Root")]
        [SerializeField]
        Transform? _segmentMarkersRoot;

        bool UsesAreaChannel() => _rangeSourceMode == SliderRangeSourceMode.AreaChannel;
        bool UsesRectTransform() => _rangeSourceMode == SliderRangeSourceMode.RectTransform;

        public string ChannelTag => string.IsNullOrWhiteSpace(_channelTag) ? "default" : _channelTag.Trim();

        internal SliderChannelOptions CreateOptions(Transform ownerTransform)
        {
            return new SliderChannelOptions
            {
                VisualizerPresetValue = _visualizerPreset,
                PlayerPresetValue = _playerPreset,
                SegmentBarsRoot = _segmentBarsRoot,
                SegmentMarkersRoot = _segmentMarkersRoot,
                AreaActorSource = _areaActorSource,
                AreaChannelTag = string.IsNullOrWhiteSpace(_areaChannelTag) ? "default" : _areaChannelTag.Trim(),
                RangeSourceMode = _rangeSourceMode,
                RangeRectTransform = _rangeRectTransform,
                OwnerTransform = ownerTransform,
            };
        }
    }

    [DisallowMultipleComponent]
    public sealed class SliderChannelHubMB : MonoBehaviour, IFeatureInstaller
    {
        [BoxGroup("Channels")]
        [LabelText("Channels")]
        [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = true, ShowFoldout = true)]
        [SerializeField]
        List<SliderChannelDefinition> _channels = new() { new SliderChannelDefinition() };

        public IReadOnlyList<SliderChannelDefinition> Channels => _channels;

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            builder.Register<SliderChannelHubService>(Lifetime.Singleton)
                .WithParameter(scope)
                .WithParameter(this)
                .As<ISliderChannelHubService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .As<ITickable>();
        }
    }
}
