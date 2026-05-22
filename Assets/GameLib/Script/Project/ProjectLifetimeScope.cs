using System;
using UnityEngine;
using Game.TransformSystem;
using Game.Input;
using Game.Scalar;
using Game.Commands;
using Game.Spawn;
using Game.Movement;
using Game.Collision;
using Game.Audio;
using Game.Times;
using Game.Project.Bootstrap;
using Game.Project;

namespace Game
{
    [RequireComponent(typeof(Game.Project.ApplicationShutdownMB))] // Application Shutdown Handling
    [RequireComponent(typeof(Game.Project.SceneFlowInstallerMB))] // Scene Flow Management
    [RequireComponent(typeof(Game.Scalar.ProjectScalarMB))] // Essential Scalar Features - Scalar for the Library
    [RequireComponent(typeof(Game.Common.EventMB))] // Project Event Service
    [RequireComponent(typeof(Game.Flow.FlowHostMB))] // Flow syscall host
    [RequireComponent(typeof(MaterialFx.MaterialFxMB))]
    [RequireComponent(typeof(Game.Save.SaveManagerMB))] // Save Manager
    [RequireComponent(typeof(BulkTransformManagerMB))] // 
    [RequireComponent(typeof(CollisionPipelineModeMB))]
    [RequireComponent(typeof(InputMB))] // Movement Channel Hub
    [RequireComponent(typeof(AudioInstallerMB))]
    [RequireComponent(typeof(TimeInstallerMB))]
    [RequireComponent(typeof(ProjectRootInstallerContributionHostMB))]
    [RequireComponent(typeof(ProjectRootScopeServicesMB))]
    public class ProjectLifetimeScope : KernelScopeHost
    {
        [Header("Debug")]
        [Tooltip("Enable LTS runtime logs for debugging (set via Project scope).")]

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
            base.Awake();
        }

        protected override void OnDestroy()
        {
            if (_instance == this)
                _instance = null;

            base.OnDestroy();
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
            if (KernelLiveBootRuntime.ShouldSuppressLegacyAutoBootstrap())
                return;

            KernelLiveBootRuntime.ThrowLegacyAutoBootstrapForbidden(nameof(ProjectLifetimeScope));
        }

    }

    [DisallowMultipleComponent]
    public sealed class ProjectRootInstallerContributionHostMB : MonoBehaviour, IVerifiedInstallerContributionHost
    {
        public void InstallVerifiedInstallerContributions(IRuntimeContainerBuilder builder, IScopeNode owner)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            if (owner == null)
                throw new ArgumentNullException(nameof(owner));

            if (owner.Kind != LifetimeScopeKind.Project)
                return;

            GetComponent<ApplicationShutdownMB>()?.InstallApplicationShutdownRuntime(builder, owner);
            GetComponent<ProjectScalarMB>()?.InstallScalarRuntime(builder, owner);
            GetComponent<global::Game.Common.EventMB>()?.InstallEventRuntime(builder, owner);
            GetComponent<global::Game.Flow.FlowHostMB>()?.InstallFlowHostRuntime(builder, owner);
            RuntimeScopeContributionBridge.InstallExplicitInstallerContribution(builder, owner, GetComponent<global::Game.MaterialFx.MaterialFxMB>());
            GetComponent<global::Game.Save.SaveManagerMB>()?.InstallSaveManagerRuntime(builder, owner);
            RuntimeScopeContributionBridge.InstallExplicitInstallerContribution(builder, owner, GetComponent<BulkTransformManagerMB>());
            RuntimeScopeContributionBridge.InstallExplicitInstallerContribution(builder, owner, GetComponent<CollisionPipelineModeMB>());
            RuntimeScopeContributionBridge.InstallExplicitInstallerContribution(builder, owner, GetComponent<InputMB>());
            RuntimeScopeContributionBridge.InstallExplicitInstallerContribution(builder, owner, GetComponent<AudioInstallerMB>());
            RuntimeScopeContributionBridge.InstallExplicitInstallerContribution(builder, owner, GetComponent<TimeInstallerMB>());
        }

        public bool AcceptsVerifiedInstallerComponent(Component component)
        {
            return component is global::Game.MaterialFx.MaterialFxMB
                || component is BulkTransformManagerMB
                || component is CollisionPipelineModeMB
                || component is InputMB
                || component is AudioInstallerMB
                || component is TimeInstallerMB;
        }
    }

    [DisallowMultipleComponent]
    public sealed class ProjectRootScopeServicesMB : MonoBehaviour
    {
        public void InstallProjectRootRuntime(IRuntimeContainerBuilder builder, IScopeNode owner)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            if (owner == null)
                throw new ArgumentNullException(nameof(owner));

            if (owner.Kind != LifetimeScopeKind.Project)
                return;

            builder.Register<BaseLifetimeScopeRegistry>(RuntimeLifetime.Singleton)
                .As<IBaseLifetimeScopeRegistry>();

            builder.Register<ScalarBindingManager>(RuntimeLifetime.Singleton)
                .As<IScalarBindingManager>()
                .As<IScalarBindingTelemetry>()
                .As<IScopeTickHandler>();

            builder.Register<BaseLifetimeScopeSpawner>(RuntimeLifetime.Singleton)
                .As<IScopeSpawner>();
        }
    }
}

