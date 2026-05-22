#nullable enable
using Game;
using Game.Chunk.Biome;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace Game.Chunk
{
    [DisallowMultipleComponent]
    public sealed class ChunkBiomeMB : MonoBehaviour, IScopeInstaller
    {
        const string BiomeGroup = "Biome";

        [BoxGroup(BiomeGroup)]
        [SerializeField] ChunkBiomeSettingsSO? settings;

        public void InstallScopeServices(IRuntimeContainerBuilder builder, IScopeNode owner)
        {
            if (settings != null)
                builder.RegisterInstance(settings);

            builder.Register<ChunkBiomeService>(RuntimeLifetime.Singleton)
                .As<IChunkBiomeService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();
        }
    }
}

