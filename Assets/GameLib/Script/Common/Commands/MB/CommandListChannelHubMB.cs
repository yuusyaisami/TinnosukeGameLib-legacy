#nullable enable
using System;
using System.Collections.Generic;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Commands
{
    [Serializable]
    public sealed class CommandListChannelOptions : ICommandListChannelOptions
    {
        public DynamicValue<CommandListChannelPreset> PresetValue { get; set; } =
            DynamicValue<CommandListChannelPreset>.FromSource(
                new ManagedRefLiteralSource<CommandListChannelPreset>());
    }

    [Serializable]
    public sealed class CommandListChannelDefinition
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
        DynamicValue<CommandListChannelPreset> _presetValue =
            DynamicValue<CommandListChannelPreset>.FromSource(
                new ManagedRefLiteralSource<CommandListChannelPreset>());

        public string ChannelTag => string.IsNullOrWhiteSpace(_channelTag) ? "default" : _channelTag.Trim();

        internal CommandListChannelOptions CreateOptions()
        {
            return new CommandListChannelOptions
            {
                PresetValue = _presetValue,
            };
        }
    }

    [DisallowMultipleComponent]
    public sealed class CommandListChannelHubMB : MonoBehaviour, IFeatureInstaller
    {
        [BoxGroup("Channels")]
        [LabelText("Channels")]
        [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = true, ShowFoldout = true)]
        [SerializeField]
        List<CommandListChannelDefinition> _channels = new() { new CommandListChannelDefinition() };

        public IReadOnlyList<CommandListChannelDefinition> Channels => _channels;

        public void InstallFeature(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            builder.Register<CommandListChannelHubService>(RuntimeLifetime.Singleton)
                .WithParameter(scope)
                .WithParameter(this)
                .As<ICommandListChannelHubService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .As<IScopeTickHandler>();
        }
    }
}
