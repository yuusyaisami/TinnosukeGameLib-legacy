#nullable enable
using System;
using Game.Chunk.Biome;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Chunk
{
    public readonly struct ChunkRuleContext
    {
        public readonly ChunkContext Context;
        public readonly int Seed;
        public readonly string BiomeId;
        public readonly ChunkVarBox VarBox;
        public readonly System.Random Random;

        public ChunkRuleContext(ChunkContext context, int seed, string biomeId, ChunkVarBox varBox, System.Random random)
        {
            Context = context;
            Seed = seed;
            BiomeId = biomeId ?? string.Empty;
            VarBox = varBox ?? new ChunkVarBox();
            Random = random;
        }
    }

    public abstract class ChunkRuleSOBase : ScriptableObject
    {
        [SerializeField] bool enabled = true;
        [SerializeField] int priority = 0;

        public bool Enabled => enabled;
        public int Priority => priority;

        public abstract void Apply(ChunkPlan plan, ChunkRuleContext context);
    }

    [CreateAssetMenu(
        fileName = "NewChunkRuleSet",
        menuName = "Game/Chunk/RuleSet",
        order = 201)]
    public sealed class ChunkRuleSetSO : ScriptableObject
    {
        [BoxGroup("Rules")]
        [AssetOrInternal]
        [SerializeField] ChunkRuleSOBase[] rules = Array.Empty<ChunkRuleSOBase>();
        public ChunkRuleSOBase[] Rules => rules;

        public void Apply(ChunkPlan plan, ChunkRuleContext context)
        {
            if (rules == null)
                return;

            for (int i = 0; i < rules.Length; i++)
            {
                var rule = rules[i];
                if (rule == null || !rule.Enabled)
                    continue;
                rule.Apply(plan, context);
            }
        }

        void OnValidate()
        {
            if (rules == null)
                rules = Array.Empty<ChunkRuleSOBase>();
        }
    }
}
