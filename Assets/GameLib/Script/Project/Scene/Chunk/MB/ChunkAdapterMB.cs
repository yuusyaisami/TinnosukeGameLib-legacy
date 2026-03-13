#nullable enable
using Game;
using UnityEngine;
using VContainer;

namespace Game.Chunk
{
    [DisallowMultipleComponent]
    public sealed class ChunkAdapterMB : MonoBehaviour, IFeatureInstaller
    {
        public void InstallFeature(IContainerBuilder builder, IScopeNode owner)
        {
            builder.Register<ChunkAdapterService>(Lifetime.Singleton)
                .WithParameter(owner)
                .As<IChunkAdapter>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();
        }
    }
}
