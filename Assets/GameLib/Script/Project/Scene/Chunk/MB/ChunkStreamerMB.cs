#nullable enable
using Game;
using Game.Common;
using Game.DI;
using Game.Spawn;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Chunk
{
    [DisallowMultipleComponent]
    public sealed class ChunkStreamerMB : MonoBehaviour, IFeatureInstaller
    {
        const string ViewGroup = "View";
        const string ChunkGroup = "Chunk";
        const string OriginGroup = "Origin";
        const string RuntimeGroup = "Runtime";

        [BoxGroup(ViewGroup)]
        [SerializeField] Transform? targetTransform;

        [BoxGroup(ViewGroup)]
        [SerializeField] Camera? viewCamera;

        [BoxGroup(ViewGroup)]
        [SerializeField] bool useCameraView = true;

        [BoxGroup(ViewGroup)]
        [SerializeField] Vector2 manualViewSize = new Vector2(32f, 32f);

        [BoxGroup(ChunkGroup)]
        [InlineProperty]
        [SerializeField] ChunkSettings chunkSettings;

        [BoxGroup(OriginGroup)]
        [InlineProperty]
        [SerializeField] ChunkOriginSettings originSettings;

        [BoxGroup(RuntimeGroup)]
        [SerializeField, InlineProperty, HideLabel] DynamicValue<BaseRuntimeTemplatePreset> chunkRuntimeTemplatePreset;

        [BoxGroup(RuntimeGroup)]
        [SerializeField] SpawnerKind spawnerKind = SpawnerKind.RuntimeEntity;

        [BoxGroup(RuntimeGroup)]
        [SerializeField] string spawnerTag = string.Empty;

        [BoxGroup(RuntimeGroup)]
        [EnumToggleButtons]
        [SerializeField] ChunkSpawnPivot spawnPivot = ChunkSpawnPivot.ChunkCenter;

        [BoxGroup(RuntimeGroup)]
        [SerializeField] Transform? chunkParent;

        public void InstallFeature(IContainerBuilder builder, IScopeNode owner)
        {
            chunkSettings.EnsureDefaults();
            originSettings.EnsureDefaults();

            var config = new ChunkStreamerConfig
            {
                TargetTransform = targetTransform != null ? targetTransform : transform,
                ViewCamera = viewCamera,
                UseCameraView = useCameraView,
                ManualViewSize = manualViewSize,
                Settings = chunkSettings,
                OriginSettings = originSettings,
                ChunkRuntimeTemplatePreset = chunkRuntimeTemplatePreset,
                SpawnerKind = spawnerKind,
                SpawnerTag = spawnerTag,
                SpawnPivot = spawnPivot,
                ChunkParent = chunkParent != null ? chunkParent : transform,
            };

            builder.RegisterInstance(config);

            builder.Register<ChunkViewProviderService>(Lifetime.Singleton)
                .As<IChunkViewProvider>();

            builder.Register<ChunkFactoryService>(Lifetime.Singleton)
                .WithParameter(owner)
                .As<IChunkFactory>();

            builder.Register<ChunkStreamerService>(Lifetime.Singleton)
                .As<IChunkStreamer>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .As<ITickable>();
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            chunkSettings.EnsureDefaults();
            originSettings.EnsureDefaults();
            if (manualViewSize.x <= 0f) manualViewSize.x = 32f;
            if (manualViewSize.y <= 0f) manualViewSize.y = 32f;
        }
#endif
    }
}
