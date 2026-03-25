#nullable enable
using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Channel
{
    [DisallowMultipleComponent]
    public sealed class MeshChannelHubMB : MonoBehaviour, IFeatureInstaller
    {
        [SerializeField]
        MeshChannelEntry[] _entries = Array.Empty<MeshChannelEntry>();

        [SerializeField]
        Material? _defaultMaterial;

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            builder.Register<MeshChannelHubService>(resolver =>
                new MeshChannelHubService(
                    _entries ?? Array.Empty<MeshChannelEntry>(),
                    scope,
                    transform,
                    _defaultMaterial),
                Lifetime.Singleton)
                .As<IMeshChannelHubService>()
                .As<IMeshChannelControlService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .As<ITickable>()
                .AsSelf();
        }
    }
}
