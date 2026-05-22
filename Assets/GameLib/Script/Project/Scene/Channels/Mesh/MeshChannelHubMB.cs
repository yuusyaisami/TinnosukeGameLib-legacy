#nullable enable
using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Channel
{
    [DisallowMultipleComponent]
    public sealed class MeshChannelHubMB : MeshChannelHubAuthoring, IScopeInstaller
    {
        public void InstallScopeServices(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            builder.Register<MeshChannelHubService>(resolver =>
                new MeshChannelHubService(
                    Entries,
                    scope,
                    transform),
                RuntimeLifetime.Singleton)
                .As<IMeshChannelHubService>()
                .As<IMeshChannelControlService>()
                .As<IMeshMaterialFxControlService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .As<IScopeTickHandler>()
                .AsSelf();
        }
    }
}

