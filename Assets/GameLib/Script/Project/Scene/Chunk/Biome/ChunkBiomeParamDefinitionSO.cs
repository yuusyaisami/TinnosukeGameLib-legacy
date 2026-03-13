#nullable enable
using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Chunk.Biome
{
    [Serializable]
    public struct ChunkBiomeNoiseSettings
    {
        [SerializeField] float frequency;
        [SerializeField] int octaves;
        [SerializeField] float persistence;
        [SerializeField] float lacunarity;
        [SerializeField] Vector2 offset;
        [SerializeField] int seedOffset;

        public float Frequency => frequency;
        public int Octaves => octaves;
        public float Persistence => persistence;
        public float Lacunarity => lacunarity;
        public Vector2 Offset => offset;
        public int SeedOffset => seedOffset;

        public void EnsureDefaults()
        {
            if (frequency <= 0f) frequency = 0.01f;
            if (octaves <= 0) octaves = 3;
            if (persistence <= 0f) persistence = 0.5f;
            if (lacunarity <= 0f) lacunarity = 2f;
        }

        public float Sample(Vector2 worldPos, int seed)
        {
            EnsureDefaults();

            var baseOffset = offset + new Vector2(seedOffset, seedOffset) + new Vector2(seed * 0.01f, seed * 0.02f);
            var freq = frequency;
            var amp = 1f;
            var sum = 0f;
            var totalAmp = 0f;

            for (int i = 0; i < octaves; i++)
            {
                var x = (worldPos.x + baseOffset.x) * freq;
                var y = (worldPos.y + baseOffset.y) * freq;
                var n = Mathf.PerlinNoise(x, y) * 2f - 1f;
                sum += n * amp;
                totalAmp += amp;
                amp *= persistence;
                freq *= lacunarity;
            }

            if (totalAmp > 0f)
                sum /= totalAmp;

            return sum;
        }
    }

    [CreateAssetMenu(
        fileName = "NewChunkBiomeParam",
        menuName = "Game/Chunk/Biome Param",
        order = 210)]
    public sealed class ChunkBiomeParamDefinitionSO : ScriptableObject
    {
        [BoxGroup("Param")]
        [SerializeField] bool enabled = true;

        [BoxGroup("Param")]
        [SerializeField] string key = string.Empty;

        [BoxGroup("Noise")]
        [SerializeField] ChunkBiomeNoiseSettings noise = new();

        [BoxGroup("Clamp")]
        [SerializeField] bool useClamp = true;

        [BoxGroup("Clamp")]
        [SerializeField] float clampMin = -1f;

        [BoxGroup("Clamp")]
        [SerializeField] float clampMax = 1f;

        [BoxGroup("Remap")]
        [SerializeField] bool normalize01 = false;

        public bool Enabled => enabled;
        public string Key => key;
        public ChunkBiomeNoiseSettings Noise => noise;
        public bool UseClamp => useClamp;
        public float ClampMin => clampMin;
        public float ClampMax => clampMax;
        public bool Normalize01 => normalize01;

        public float Evaluate(Vector2 worldPos, int seed)
        {
            var v = noise.Sample(worldPos, seed);
            if (useClamp)
                v = Mathf.Clamp(v, clampMin, clampMax);
            if (normalize01)
                v = Mathf.InverseLerp(clampMin, clampMax, v);
            return v;
        }

        void OnValidate()
        {
            if (string.IsNullOrEmpty(key))
                key = "param";
            if (clampMax <= clampMin)
            {
                clampMin = -1f;
                clampMax = 1f;
            }
            var n = noise;
            n.EnsureDefaults();
            noise = n;
        }
    }
}
