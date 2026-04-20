#nullable enable
using Game;
using UnityEngine;
using VContainer;

namespace Game.SharedTexture
{
    [DisallowMultipleComponent]
    public sealed class SharedTextureChannelHubMB : MonoBehaviour, IFeatureInstaller
    {
        public void InstallFeature(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            builder.Register<SharedTextureChannelHubService>(RuntimeLifetime.Singleton)
                .As<ISharedTextureChannelHub>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();
        }
    }
}
