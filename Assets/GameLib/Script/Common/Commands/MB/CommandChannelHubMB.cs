using System;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace Game.Commands
{
    [DisallowMultipleComponent]
    public sealed class CommandChannelHubMB : MonoBehaviour, IFeatureInstaller, ICommandChannelHubSettings
    {
        [BoxGroup("Channels")]
        [LabelText("Command Channels")]
        [TableList(AlwaysExpanded = true)]
        [SerializeField]
        CommandChannelEntry[] _entries = Array.Empty<CommandChannelEntry>();

        public CommandChannelEntry[] Entries => _entries;

        public void InstallFeature(IContainerBuilder builder, IScopeNode owner)
        {
            _ = owner;

            builder.RegisterInstance<ICommandChannelHubSettings>(this);
            builder.Register<CommandChannelHubService>(Lifetime.Singleton)
                .As<ICommandChannelHubService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();
        }
    }
}
