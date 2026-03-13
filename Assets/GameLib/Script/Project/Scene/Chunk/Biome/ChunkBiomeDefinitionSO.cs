#nullable enable
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Chunk.Biome
{
    [Serializable]
    public struct ChunkBiomeParamRange
    {
        [SerializeField] string key;
        [SerializeField] float min;
        [SerializeField] float max;

        public string Key => key;
        public float Min => min;
        public float Max => max;

        public bool IsMatch(ChunkVarBox varBox)
        {
            if (varBox == null || string.IsNullOrEmpty(key))
                return false;

            if (!varBox.TryGet(key, out var v))
                return false;

            return v >= min && v <= max;
        }
    }

    [CreateAssetMenu(
        fileName = "NewChunkBiome",
        menuName = "Game/Chunk/Biome Definition",
        order = 211)]
    public sealed class ChunkBiomeDefinitionSO : ScriptableObject
    {
        [BoxGroup("Biome")]
        [SerializeField] bool enabled = true;

        [BoxGroup("Biome")]
        [SerializeField] string biomeId = "default";

        [BoxGroup("Biome")]
        [SerializeField] int priority = 0;

        [BoxGroup("Biome")]
        [SerializeField] float baseScore = 0f;

        [BoxGroup("Rules")]
        [SerializeField] List<ChunkBiomeParamRange> ranges = new();

        public bool Enabled => enabled;
        public string BiomeId => biomeId;
        public int Priority => priority;
        public float BaseScore => baseScore;
        public IReadOnlyList<ChunkBiomeParamRange> Ranges => ranges;

        public bool IsMatch(ChunkVarBox varBox)
        {
            if (!enabled)
                return false;

            if (ranges == null || ranges.Count == 0)
                return true;

            for (int i = 0; i < ranges.Count; i++)
            {
                if (!ranges[i].IsMatch(varBox))
                    return false;
            }

            return true;
        }

        void OnValidate()
        {
            if (string.IsNullOrEmpty(biomeId))
                biomeId = "default";
            if (ranges == null)
                ranges = new List<ChunkBiomeParamRange>();
        }
    }
}
