#nullable enable
using Game;
using UnityEngine;
using VContainer;

namespace Game.Chunk
{
    [DisallowMultipleComponent]
    public sealed class ChunkAdapterMB : MonoBehaviour, IFeatureInstaller
    {
        public void InstallFeature(IRuntimeContainerBuilder builder, IScopeNode owner)
        {
            builder.Register<ChunkAdapterService>(RuntimeLifetime.Singleton)
                .WithParameter(owner)
                .As<IChunkAdapter>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();
        }
    }
}
