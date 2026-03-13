using UnityEngine;
using VContainer.Unity;
using VContainer;
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
    public class ProjectLifetimeScope : BaseLifetimeScope
    {
        [Header("Debug")]
        [Tooltip("Enable LTS runtime logs for debugging (set via Project scope).")]
        [SerializeField] bool enableLTSLog = false;

        static ProjectLifetimeScope _instance;

        // 協調ビルドに参加しない（VContainerのautoRunでビルド）が、子への通知は行う
        protected override bool UseBuildCoordinator => false;
        protected override bool IsBuildRoot => false;

        protected override void Awake()
        {
            if (_instance != null && _instance != this)
            {
                // 何かの理由で二重生成された場合は自分を消す
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            // Initialize default runtime log enabled state
            Game.LTSLog.Enabled = enableLTSLog;
            base.Awake();
        }
        protected override void AwakeConfigure(IContainerBuilder builder)
        {
            // Project スコープ固有の初期化をここに書く
            builder.Register<BaseLifetimeScopeRegistry>(Lifetime.Singleton)
                .As<IBaseLifetimeScopeRegistry>();
        }

        protected override void ConfigureBase(IContainerBuilder builder)
        {

            builder.Register<ScalarBindingManager>(Lifetime.Singleton)
                .As<IScalarBindingManager>()
                .As<IScalarBindingTelemetry>()
                .As<ITickable>();

            builder.Register<BaseLifetimeScopeSpawner>(Lifetime.Singleton)
                .As<IScopeSpawner>();

            Game.LTSLog.Log("[ProjectLifetimeScope] Configuring Project scoped services.");
        }

        /// <summary>親スコープから呼び出される用。まだ存在しなければ生成する。</summary>
        public static void EnsureExists()
        {
            if (_instance != null)
                return;
            EnsureInScene();
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

            // Resources/ProjectLifetimeScope.prefab があればそれを使う
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
