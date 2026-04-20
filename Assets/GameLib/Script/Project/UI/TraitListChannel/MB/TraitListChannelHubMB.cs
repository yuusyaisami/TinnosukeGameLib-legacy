#nullable enable
using System;
using System.Collections.Generic;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.UI
{
    [Serializable]
    public sealed class TraitListChannelDefinition
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

        [BoxGroup("Binding")]
        [LabelText("Default Binding")]
        [InlineProperty]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        TraitListChannelBinding _defaultBinding = new();

        [BoxGroup("Preset")]
        [LabelText("Player Preset")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        DynamicValue<TraitListChannelPlayerPreset> _playerPreset =
            DynamicValue<TraitListChannelPlayerPreset>.FromSource(
                new ManagedRefLiteralSource<TraitListChannelPlayerPreset>(new TraitListChannelPlayerPreset()));

        [BoxGroup("Preset")]
        [LabelText("Layout Preset")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        DynamicValue<TraitListChannelLayoutPreset> _layoutPreset =
            DynamicValue<TraitListChannelLayoutPreset>.FromSource(
                new ManagedRefLiteralSource<TraitListChannelLayoutPreset>(new TraitListChannelLayoutPreset()));

        [BoxGroup("Preset")]
        [LabelText("Visualizer Preset")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        DynamicValue<TraitListChannelVisualizerPreset> _visualizerPreset =
            DynamicValue<TraitListChannelVisualizerPreset>.FromSource(
                new ManagedRefLiteralSource<TraitListChannelVisualizerPreset>(new TraitListChannelVisualizerPreset()));

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

        public string ChannelTag => string.IsNullOrWhiteSpace(_channelTag) ? "default" : _channelTag.Trim();
        public bool AutoBuild => _autoBuild;
        public TraitListChannelBinding DefaultBinding => _defaultBinding;
        public DynamicValue<TraitListChannelPlayerPreset> PlayerPresetValue => _playerPreset;
        public DynamicValue<TraitListChannelLayoutPreset> LayoutPresetValue => _layoutPreset;
        public DynamicValue<TraitListChannelVisualizerPreset> VisualizerPresetValue => _visualizerPreset;
        public Transform? ListRoot => _listRoot;
        public Transform? LayoutRectTransform => _layoutRectTransform;
    }

    [DisallowMultipleComponent]
    public sealed class TraitListChannelHubMB : MonoBehaviour, IFeatureInstaller
    {
        [BoxGroup("Channels")]
        [LabelText("Channels")]
        [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = true, ShowFoldout = true)]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        List<TraitListChannelDefinition> _channels = new() { new TraitListChannelDefinition() };

        public IReadOnlyList<TraitListChannelDefinition> Channels => _channels;

        public void InstallFeature(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            builder.Register<TraitListChannelHubService>(RuntimeLifetime.Singleton)
                .WithParameter(scope)
                .WithParameter(this)
                .As<ITraitListChannelHubService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();
        }
    }
}
