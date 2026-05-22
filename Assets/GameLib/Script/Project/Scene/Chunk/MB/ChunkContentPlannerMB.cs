#nullable enable
using Game;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace Game.Chunk
{
    [DisallowMultipleComponent]
    public sealed class ChunkContentPlannerMB : MonoBehaviour, IScopeInstaller
    {
        const string PlanGroup = "Plan";

        [BoxGroup(PlanGroup)]
        [SerializeField] int seed = 0;

        [BoxGroup(PlanGroup)]
        [SerializeField] ChunkProfileSO? defaultProfile;

        [BoxGroup(PlanGroup)]
        [SerializeField] ChunkRuleSetSO? ruleSet;

        public void InstallScopeServices(IRuntimeContainerBuilder builder, IScopeNode owner)
        {
            var config = new ChunkPlannerConfig
            {
                Seed = seed,
                DefaultProfile = defaultProfile,
                RuleSet = ruleSet,
            };

            builder.RegisterInstance(config);

            builder.Register<ChunkContentPlannerService>(RuntimeLifetime.Singleton)
                .As<IChunkContentPlanner>();
        }
    }
}

