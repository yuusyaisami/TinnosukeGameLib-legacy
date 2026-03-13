#nullable enable
using System;
using Game.Chunk.Biome;

namespace Game.Chunk
{
    public sealed class ChunkContentPlannerService : IChunkContentPlanner
    {
        readonly ChunkPlannerConfig _config;
        readonly IChunkBiomeService? _biomeService;

        public ChunkContentPlannerService(ChunkPlannerConfig config, IChunkBiomeService? biomeService = null)
        {
            _config = config;
            _biomeService = biomeService;
        }

        public ChunkPlan BuildPlan(ChunkContext context)
        {
            var plan = new ChunkPlan();
            plan.Seed = ChunkCoordUtility.ComputeChunkSeed(_config.Seed, context.Coord);

            var biomeResult = _biomeService != null
                ? _biomeService.Evaluate(context, plan.Seed)
                : new ChunkBiomeResult("default", new ChunkVarBox());

            plan.BiomeId = biomeResult.BiomeId;
            plan.SetVarBox(biomeResult.VarBox);

            if (_config.RuleSet != null)
            {
                var random = new Random(plan.Seed);
                var ruleContext = new ChunkRuleContext(context, plan.Seed, plan.BiomeId, plan.VarBox, random);
                _config.RuleSet.Apply(plan, ruleContext);
            }

            if (_config.RuleSet == null || (plan.CommonCommands.Count == 0 && plan.ConditionalCommands.Count == 0))
            {
                plan.ApplyProfile(_config.DefaultProfile);
            }

            return plan;
        }
    }
}
