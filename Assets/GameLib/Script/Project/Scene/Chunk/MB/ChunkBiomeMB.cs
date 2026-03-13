#nullable enable
using Game;
using Game.Chunk.Biome;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace Game.Chunk
{
    [DisallowMultipleComponent]
    public sealed class ChunkBiomeMB : MonoBehaviour, IFeatureInstaller
    {
        const string BiomeGroup = "Biome";

        [BoxGroup(BiomeGroup)]
        [SerializeField] ChunkBiomeSettingsSO? settings;

        public void InstallFeature(IContainerBuilder builder, IScopeNode owner)
        {
            if (settings != null)
                builder.RegisterInstance(settings);

            builder.Register<ChunkBiomeService>(Lifetime.Singleton)
                .As<IChunkBiomeService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();
        }
    }
}
