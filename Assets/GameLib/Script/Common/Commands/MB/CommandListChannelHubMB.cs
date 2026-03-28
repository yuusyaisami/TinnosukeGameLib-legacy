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
                new ManagedRefLiteralSource<CommandListChannelPreset>(new CommandListChannelPreset()));
    }

    [Serializable]
    public sealed class CommandListChannelDefinition
    {
        [BoxGroup("Channel")]
        [LabelText("Channel Tag")]
        [Tooltip("CommandListChannel の識別タグです。空白の場合は default を使用します。")]
        [SerializeField]
        string _channelTag = "default";

        [BoxGroup("Preset")]
        [LabelText("Preset")]
        [Tooltip("この channel の source preset です。runtime override reset 時はここへ戻ります。")]
        [SerializeField]
        DynamicValue<CommandListChannelPreset> _presetValue =
            DynamicValue<CommandListChannelPreset>.FromSource(
                new ManagedRefLiteralSource<CommandListChannelPreset>(new CommandListChannelPreset()));

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

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            builder.Register<CommandListChannelHubService>(Lifetime.Singleton)
                .WithParameter(scope)
                .WithParameter(this)
                .As<ICommandListChannelHubService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .As<ITickable>();
        }
    }
}
