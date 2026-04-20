using UnityEngine;
using Game.TransformSystem;
using Game.Input;
using Game.Scalar;
using Game.Commands;
using System;
using Game.Spawn;
using Game.Movement;
using Game.Collision;
using Game.Audio;
using Game.Times;

namespace Game
{
    [RequireComponent(typeof(Game.Project.ApplicationShutdownMB))] // Application Shutdown Handling
    [RequireComponent(typeof(Game.Project.SceneFlowInstallerMB))] // Scene Flow Management
    [RequireComponent(typeof(Game.Scalar.ProjectScalarMB))] // Essential Scalar Features - Scalar for the Library
    [RequireComponent(typeof(Game.Commands.CommandRunnerMB))] // Runner
    [RequireComponent(typeof(Game.Common.BlackboardMB))] // Project Blackboard
    [RequireComponent(typeof(Game.Common.EventMB))] // Project Event Service
    [RequireComponent(typeof(Game.Flow.FlowHostMB))] // Flow syscall host
    [RequireComponent(typeof(MaterialFx.MaterialFxMB))]
    [RequireComponent(typeof(Game.Save.SaveManagerMB))] // Save Manager
    [RequireComponent(typeof(BulkTransformManagerMB))] // 
    [RequireComponent(typeof(CollisionPipelineModeMB))]
    [RequireComponent(typeof(InputMB))] // Movement Channel Hub
    [RequireComponent(typeof(AudioInstallerMB))]
    [RequireComponent(typeof(TimeInstallerMB))]
    public class ProjectLifetimeScope : RuntimeLifetimeScopeBase
    {
        [Header("Debug")]
        [Tooltip("Enable LTS runtime logs for debugging (set via Project scope).")]
        [SerializeField] bool enableLTSLog = false;

        static ProjectLifetimeScope _instance;
        public static ProjectLifetimeScope Instance => _instance;

        // 蜊碑ｪｿ繝薙Ν繝峨↓蜿ょ刈縺励↑縺・ｼ・Container縺ｮautoRun縺ｧ繝薙Ν繝会ｼ峨′縲∝ｭ舌∈縺ｮ騾夂衍縺ｯ陦後≧
        protected override bool UseBuildCoordinator => true;
        protected override bool IsBuildRoot => true;
        protected override bool AutoBuildOnAwake => true;
        protected override LifetimeScopeKind RequiredParentKind => LifetimeScopeKind.None;

        protected override void Awake()
        {
            if (_instance != null && _instance != this)
            {
                // 菴輔°縺ｮ逅・罰縺ｧ莠碁㍾逕滓・縺輔ｌ縺溷ｴ蜷医・閾ｪ蛻・ｒ豸医☆
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            // Initialize default runtime log enabled state
            Game.LTSLog.Enabled = enableLTSLog;
            base.Awake();
        }

        protected override void OnDestroy()
        {
            if (_instance == this)
                _instance = null;

            base.OnDestroy();
        }
        protected override void AwakeConfigure(IRuntimeContainerBuilder builder)
        {
            // Project 繧ｹ繧ｳ繝ｼ繝怜崋譛峨・蛻晄悄蛹悶ｒ縺薙％縺ｫ譖ｸ縺・
            builder.Register<BaseLifetimeScopeRegistry>(RuntimeLifetime.Singleton)
                .As<IBaseLifetimeScopeRegistry>();
        }

        protected override void ConfigureBase(IRuntimeContainerBuilder builder)
        {

            builder.Register<ScalarBindingManager>(RuntimeLifetime.Singleton)
                .As<IScalarBindingManager>()
                .As<IScalarBindingTelemetry>()
                .As<IScopeTickHandler>();

            builder.Register<BaseLifetimeScopeSpawner>(RuntimeLifetime.Singleton)
                .As<IScopeSpawner>();

            Game.LTSLog.Log("[ProjectLifetimeScope] Configuring Project scoped services.");
        }

        /// <summary>隕ｪ繧ｹ繧ｳ繝ｼ繝励°繧牙他縺ｳ蜃ｺ縺輔ｌ繧狗畑縲ゅ∪縺蟄伜惠縺励↑縺代ｌ縺ｰ逕滓・縺吶ｋ縲・/summary>
        public static void EnsureExists()
        {
            if (_instance != null)
                return;
            EnsureInScene();
        }

        public static bool TryGetResolver(out IRuntimeResolver resolver)
        {
            var instance = _instance;
            if (instance == null)
            {
                resolver = null;
                return false;
            }

            if (instance.Resolver == null)
                instance.EnsureScopeBuilt();

            resolver = instance.Resolver;
            return resolver != null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void EnsureInScene()
        {
            if (_instance != null)
                return;
            // If any ProjectLifetimeScope already exists in the scene, don't create another one.
            // This avoids double-instantiation during early runtime initialization when Awake hasn't run yet.
            var existing = UnityEngine.Object.FindObjectsByType<ProjectLifetimeScope>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );
            if (existing != null && existing.Length > 0)
            {
                // Instance will become available at Awake time; just return now.
                return;
            }

            // Resources/ProjectLifetimeScope.prefab 縺後≠繧後・縺昴ｌ繧剃ｽｿ縺・
            var prefab = Resources.Load<GameObject>("Prefab/Project/ProjectLifetimeScope");
            if (prefab != null)
            {
                UnityEngine.Object.Instantiate(prefab);
                return;
            }

            var go = new GameObject("ProjectLifetimeScope");
            go.AddComponent<ProjectLifetimeScope>();
        }

    }
}
