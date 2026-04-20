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
        [Tooltip("Inspector setting.")]
        [SerializeField]
        string _channelTag = "default";

        [BoxGroup("Channel")]
        [LabelText("Auto Build")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        bool _autoBuild;

        [BoxGroup("Preset")]
        [LabelText("Player Preset")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        DynamicValue<GridObjectChannelPlayerPresetBase> _playerPreset =
            DynamicValue<GridObjectChannelPlayerPresetBase>.FromSource(
                new ManagedRefLiteralSource<GridObjectChannelPlayerPresetBase>(new GridObjectChannelStandalonePlayerPreset()));

        [BoxGroup("Preset")]
        [LabelText("Layout Preset")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        DynamicValue<GridObjectChannelLayoutPreset> _layoutPreset =
            DynamicValue<GridObjectChannelLayoutPreset>.FromSource(
                new ManagedRefLiteralSource<GridObjectChannelLayoutPreset>(new GridObjectChannelLayoutPreset()));

        [BoxGroup("Preset")]
        [LabelText("Visualizer Preset")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        DynamicValue<GridObjectChannelVisualizerPreset> _visualizerPreset =
            DynamicValue<GridObjectChannelVisualizerPreset>.FromSource(
                new ManagedRefLiteralSource<GridObjectChannelVisualizerPreset>(new GridObjectChannelVisualizerPreset()));

        [BoxGroup("Scene")]
        [LabelText("List Root")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        Transform? _listRoot;

        [BoxGroup("Scene")]
        [LabelText("Layout Rect")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        Transform? _layoutRectTransform;

        [BoxGroup("Debug")]
        [LabelText("Enable Debug Log")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        bool _enableDebugLog;

        [BoxGroup("Debug")]
        [ShowIf(nameof(_enableDebugLog))]
        [LabelText("Verbose Layout")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        bool _enableVerboseLayoutLog;

        [BoxGroup("Debug")]
        [ShowIf(nameof(_enableDebugLog))]
        [LabelText("Verbose Blackboard")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        bool _enableVerboseBlackboardLog;

        public string ChannelTag => string.IsNullOrWhiteSpace(_channelTag) ? "default" : _channelTag.Trim();
        public bool AutoBuild => _autoBuild;
        public DynamicValue<GridObjectChannelPlayerPresetBase> PlayerPresetValue => _playerPreset;
        public DynamicValue<GridObjectChannelLayoutPreset> LayoutPresetValue => _layoutPreset;
        public DynamicValue<GridObjectChannelVisualizerPreset> VisualizerPresetValue => _visualizerPreset;
        public Transform? ListRoot => _listRoot;
        public Transform? LayoutRectTransform => _layoutRectTransform;
        public bool EnableDebugLog => _enableDebugLog;
        public bool EnableVerboseLayoutLog => _enableDebugLog && _enableVerboseLayoutLog;
        public bool EnableVerboseBlackboardLog => _enableDebugLog && _enableVerboseBlackboardLog;
    }

    [DisallowMultipleComponent]
    public sealed class GridObjectChannelHubMB : MonoBehaviour, IFeatureInstaller
    {
        [BoxGroup("Channels")]
        [LabelText("Channels")]
        [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = true, ShowFoldout = true)]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        List<GridObjectChannelDefinition> _channels = new() { new GridObjectChannelDefinition() };

        public IReadOnlyList<GridObjectChannelDefinition> Channels => _channels;

        public void InstallFeature(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            builder.Register<GridObjectChannelHubService>(RuntimeLifetime.Singleton)
                .WithParameter(scope)
                .WithParameter(this)
                .As<IGridObjectChannelHubService>()
                .As<IChoiceChannelHubService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();
        }
    }
}
