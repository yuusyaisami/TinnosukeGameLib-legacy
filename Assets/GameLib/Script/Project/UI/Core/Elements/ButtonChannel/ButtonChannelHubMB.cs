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
    public sealed class ButtonChannelOptions : IButtonChannelOptions
    {
        public DynamicValue<ButtonChannelPreset> PresetValue { get; set; } =
            DynamicValue<ButtonChannelPreset>.FromSource(
                new ManagedRefLiteralSource<ButtonChannelPreset>(new ButtonChannelPreset()));

        public Transform OwnerTransform { get; set; } = null!;
    }

    [Serializable]
    public sealed class ButtonChannelDefinition
    {
        [BoxGroup("Channel")]
        [LabelText("Channel Tag")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        string _channelTag = "default";

        [BoxGroup("Preset")]
        [LabelText("Preset")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        DynamicValue<ButtonChannelPreset> _presetValue =
            DynamicValue<ButtonChannelPreset>.FromSource(
                new ManagedRefLiteralSource<ButtonChannelPreset>(new ButtonChannelPreset()));

        public string ChannelTag => string.IsNullOrWhiteSpace(_channelTag) ? "default" : _channelTag.Trim();

        internal ButtonChannelOptions CreateOptions(Transform ownerTransform)
        {
            return new ButtonChannelOptions
            {
                PresetValue = _presetValue,
                OwnerTransform = ownerTransform,
            };
        }
    }

    [DisallowMultipleComponent]
    public sealed class ButtonChannelHubMB : MonoBehaviour, IFeatureInstaller
    {
        [BoxGroup("Channels")]
        [LabelText("Channels")]
        [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = true, ShowFoldout = true)]
        [SerializeField]
        List<ButtonChannelDefinition> _channels = new() { new ButtonChannelDefinition() };

        IButtonChannelHubService? _hub;

        public IReadOnlyList<ButtonChannelDefinition> Channels => _channels;
        public IButtonChannelHubService? Hub => _hub;

        public void InstallFeature(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            builder.Register<ButtonChannelHubService>(RuntimeLifetime.Singleton)
                .WithParameter(scope)
                .WithParameter(this)
                .As<IButtonChannelHubService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .As<IScopeTickHandler>();

            builder.RegisterBuildCallback(resolver =>
            {
                resolver.TryResolve(out _hub);
            });
        }
    }
}
