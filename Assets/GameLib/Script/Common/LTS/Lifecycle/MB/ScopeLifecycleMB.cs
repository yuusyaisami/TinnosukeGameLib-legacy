// Game.Common
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Sirenix.OdinInspector;
using VNext = Game.Commands.VNext;
using Game;

namespace Game.Common
{
    public sealed class ScopeLifecycleConfig
    {
        // Spawn
        public bool RunSpawnOnStart;
        public VNext.CommandListData SpawnOnStartCommands = new();

        public bool RunSpawnOnEnd;
        public VNext.CommandListData SpawnOnEndCommands = new();

        public float SpawnDelaySeconds; // ★ 追加

        // Despawn
        public bool RunDespawnOnStart;
        public VNext.CommandListData DespawnOnStartCommands = new();

        public bool RunDespawnOnEnd;
        public VNext.CommandListData DespawnOnEndCommands = new();

        public float DespawnDelaySeconds;

        /// <summary>
        /// When true, Despawn start will cancel any in-progress Spawn (default: true).
        /// </summary>
        public bool CancelSpawnOnDespawn = true;

        /// <summary>
        /// When true, this scope auto-despawns itself if AutoDespawnCondition evaluates false.
        /// </summary>
        public bool AutoDespawnWhenConditionFalse;

        /// <summary>
        /// Condition source for lifecycle self-despawn.
        /// </summary>
        public DynamicValue<bool> AutoDespawnCondition = DynamicValueExtensions.FromLiteral(true);
    }

    [DisallowMultipleComponent]
    public sealed class ScopeLifecycleMB : MonoBehaviour, IFeatureInstaller
    {
        [Header("Spawn")]
        [SerializeField] bool runSpawnOnStart;
        [ShowIf(nameof(runSpawnOnStart))] // もし最初からスポーンしている場合は, スポーンコマンドを実行する
        [SerializeField] VNext.CommandListData spawnOnStartCommands = new();
        [SerializeField] bool runSpawnOnEnd;
        [ShowIf(nameof(runSpawnOnEnd))]
        [SerializeField] VNext.CommandListData spawnOnEndCommands = new();
        [ShowIf("@runSpawnOnStart || runSpawnOnEnd")]
        [SerializeField] float spawnDelaySeconds;  // ★ 追加

        [Header("Despawn")]
        [SerializeField] bool runDespawnOnStart;
        [ShowIf(nameof(runDespawnOnStart))]
        [SerializeField] VNext.CommandListData despawnOnStartCommands = new();
        [SerializeField] bool runDespawnOnEnd;
        [ShowIf(nameof(runDespawnOnEnd))]
        [SerializeField] VNext.CommandListData despawnOnEndCommands = new();
        [ShowIf("@runDespawnOnStart || runDespawnOnEnd")]
        [SerializeField] float despawnDelaySeconds;

        [Tooltip("When true, starting Despawn will cancel an in-progress Spawn. Set false to allow Spawn to complete.")]
        [SerializeField] bool cancelSpawnOnDespawn = true;

        [Header("Condition Auto Despawn")]
        [Tooltip("When true, this scope auto-despawns when the condition evaluates false.")]
        [SerializeField] bool autoDespawnWhenConditionFalse = false;

        [ShowIf(nameof(autoDespawnWhenConditionFalse))]
        [SerializeField] DynamicValue<bool> autoDespawnCondition = DynamicValueExtensions.FromLiteral(true);

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            var config = new ScopeLifecycleConfig
            {
                RunSpawnOnStart = runSpawnOnStart,
                SpawnOnStartCommands = spawnOnStartCommands ?? new VNext.CommandListData(),
                RunSpawnOnEnd = runSpawnOnEnd,
                SpawnOnEndCommands = spawnOnEndCommands ?? new VNext.CommandListData(),
                SpawnDelaySeconds = spawnDelaySeconds,

                RunDespawnOnStart = runDespawnOnStart,
                DespawnOnStartCommands = despawnOnStartCommands ?? new VNext.CommandListData(),
                RunDespawnOnEnd = runDespawnOnEnd,
                DespawnOnEndCommands = despawnOnEndCommands ?? new VNext.CommandListData(),
                DespawnDelaySeconds = despawnDelaySeconds,
                CancelSpawnOnDespawn = cancelSpawnOnDespawn,
                AutoDespawnWhenConditionFalse = autoDespawnWhenConditionFalse,
                AutoDespawnCondition = autoDespawnCondition,
            };

            builder.RegisterInstance(config);

            if (scope is RuntimeLifetimeScope runtime)
            {
                builder.Register<RuntimeScopeLifecycleService>(Lifetime.Singleton)
                    .WithParameter(runtime)
                    .As<IScopeLifecycleService>()
                    .As<IScopeLifecycleConditionController>()
                    .As<IScopeReleaseHandler>()
                    .As<ITickable>();
                return;
            }

            // Register as scope-multi so BaseLifetimeScope can resolve lifecycle locally without parent fallback.
            builder.RegisterAsScopeMulti<IScopeLifecycleService, ScopeLifecycleService>(Lifetime.Singleton)
                .WithParameter(scope)
                .As<IScopeLifecycleConditionController>()
                .As<IScopeReleaseHandler>()
                .As<ITickable>();
        }
    }
}
