#nullable enable
using System;
using System.Collections.Generic;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Channel
{
    [Serializable]
    public sealed class GridObjectChannelDefinition
    {
        [BoxGroup("Channel")]
        [LabelText("Channel Tag")]
        [SerializeField]
        string _channelTag = "default";

        [BoxGroup("Channel")]
        [LabelText("Auto Build")]
        [SerializeField]
        bool _autoBuild;

        [BoxGroup("Preset")]
        [LabelText("Player Preset")]
        [SerializeField]
        DynamicValue<GridObjectChannelPlayerPresetBase> _playerPreset =
            DynamicValue<GridObjectChannelPlayerPresetBase>.FromSource(
                new ManagedRefLiteralSource<GridObjectChannelPlayerPresetBase>(new GridObjectChannelStandalonePlayerPreset()));

        [BoxGroup("Preset")]
        [LabelText("Layout Preset")]
        [SerializeField]
        DynamicValue<GridObjectChannelLayoutPreset> _layoutPreset =
            DynamicValue<GridObjectChannelLayoutPreset>.FromSource(
                new ManagedRefLiteralSource<GridObjectChannelLayoutPreset>(new GridObjectChannelLayoutPreset()));

        [BoxGroup("Preset")]
        [LabelText("Visualizer Preset")]
        [SerializeField]
        DynamicValue<GridObjectChannelVisualizerPreset> _visualizerPreset =
            DynamicValue<GridObjectChannelVisualizerPreset>.FromSource(
                new ManagedRefLiteralSource<GridObjectChannelVisualizerPreset>(new GridObjectChannelVisualizerPreset()));

        [BoxGroup("Scene")]
        [LabelText("List Root")]
        [SerializeField]
        Transform? _listRoot;

        [BoxGroup("Scene")]
        [LabelText("Layout Rect")]
        [SerializeField]
        RectTransform? _layoutRectTransform;

        public string ChannelTag => string.IsNullOrWhiteSpace(_channelTag) ? "default" : _channelTag.Trim();
        public bool AutoBuild => _autoBuild;
        public DynamicValue<GridObjectChannelPlayerPresetBase> PlayerPresetValue => _playerPreset;
        public DynamicValue<GridObjectChannelLayoutPreset> LayoutPresetValue => _layoutPreset;
        public DynamicValue<GridObjectChannelVisualizerPreset> VisualizerPresetValue => _visualizerPreset;
        public Transform? ListRoot => _listRoot;
        public RectTransform? LayoutRectTransform => _layoutRectTransform;
    }

    [DisallowMultipleComponent]
    public sealed class GridObjectChannelHubMB : MonoBehaviour, IFeatureInstaller
    {
        [BoxGroup("Channels")]
        [LabelText("Channels")]
        [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = true, ShowFoldout = true)]
        [SerializeField]
        List<GridObjectChannelDefinition> _channels = new() { new GridObjectChannelDefinition() };

        public IReadOnlyList<GridObjectChannelDefinition> Channels => _channels;

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            builder.Register<GridObjectChannelHubService>(Lifetime.Singleton)
                .WithParameter(scope)
                .WithParameter(this)
                .As<IGridObjectChannelHubService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();
        }
    }
}
