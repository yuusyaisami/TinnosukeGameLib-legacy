#nullable enable
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Chunk.Biome
{
    [CreateAssetMenu(
        fileName = "NewChunkBiomeSettings",
        menuName = "Game/Chunk/Biome Settings",
        order = 212)]
    public sealed class ChunkBiomeSettingsSO : ScriptableObject
    {
        [BoxGroup("Default")]
        [SerializeField] string defaultBiomeId = "default";

        [BoxGroup("Default")]
        [SerializeField] ChunkAxisSettings axisSettings = new();

        [BoxGroup("Default")]
        [SerializeField] float neighborBiasWeight = 0.5f;

        [BoxGroup("Params")]
        [AssetOrInternal]
        [SerializeField] ChunkBiomeParamDefinitionSO[] paramDefinitions = new ChunkBiomeParamDefinitionSO[0];

        [BoxGroup("Biomes")]
        [AssetOrInternal]
        [SerializeField] ChunkBiomeDefinitionSO[] biomeDefinitions = new ChunkBiomeDefinitionSO[0];

        public string DefaultBiomeId => defaultBiomeId;
        public ChunkAxisSettings AxisSettings => axisSettings;
        public float NeighborBiasWeight => neighborBiasWeight;
        public ChunkBiomeParamDefinitionSO[] ParamDefinitions => paramDefinitions;
        public ChunkBiomeDefinitionSO[] BiomeDefinitions => biomeDefinitions;

        void OnValidate()
        {
            if (string.IsNullOrEmpty(defaultBiomeId))
                defaultBiomeId = "default";
            if (paramDefinitions == null)
                paramDefinitions = new ChunkBiomeParamDefinitionSO[0];
            if (biomeDefinitions == null)
                biomeDefinitions = new ChunkBiomeDefinitionSO[0];
            var axis = axisSettings;
            axis.EnsureDefaults();
            axisSettings = axis;
        }
    }
}
