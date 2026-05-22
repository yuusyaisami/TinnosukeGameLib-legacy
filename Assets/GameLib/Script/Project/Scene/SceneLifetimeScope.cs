using System;
using Game.Platform;
using UnityEngine;
using Game.Spawn;
using Game.Project.Scene.Runtime;
using Game.Entity.Search;
using Game.Entity;
using Game.Project.Bootstrap;
using Game.TransformSystem;
using Game.Common;
namespace Game.Scene
{
    [RequireComponent(typeof(RuntimeManagerMB))]
    [RequireComponent(typeof(DynamicObjectRegistryMB))]
    [RequireComponent(typeof(EntityLifetimeScopeSpawnerMB))]
    [RequireComponent(typeof(Scalar.BaseScalarMB))]
    [RequireComponent(typeof(BulkTransformManagerMB))]
    [RequireComponent(typeof(Common.EventMB))]
    [RequireComponent(typeof(SceneRootInstallerContributionHostMB))]
    [RequireComponent(typeof(SceneRootScopeServicesMB))]
    public sealed class SceneLifetimeScope : KernelScopeHost
    {
        // 縺薙・Scene縺ｮ蜊碑ｪｿ繝薙Ν繝峨・Root
        protected override bool IsBuildRoot => true;

        // 蜊碑ｪｿ繝薙Ν繝峨↓蜿ょ刈
        protected override bool UseBuildCoordinator => true;
        protected override bool AutoBuildOnAwake => true;
        protected override LifetimeScopeKind RequiredParentKind => LifetimeScopeKind.Global;

        protected override void Awake()
        {
            EnsureVerifiedSceneHandoffParticipation();
            base.Awake();
        }

        static void EnsureVerifiedSceneHandoffParticipation()
        {
            if (!KernelLiveBootRuntime.IsVerifiedLiveBootActive)
                return;

            if (KernelLiveBootRuntime.IsSceneHandoffInProgress || KernelLiveBootRuntime.IsSceneHandoffReady)
                return;

            throw new InvalidOperationException("SceneLifetimeScope cannot establish scene-root authority before verified scene handoff begins.");
        }
    }

    [DisallowMultipleComponent]
    public sealed class SceneRootInstallerContributionHostMB : MonoBehaviour, IVerifiedInstallerContributionHost
    {
        public void InstallVerifiedInstallerContributions(IRuntimeContainerBuilder builder, IScopeNode owner)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            if (owner == null)
                throw new ArgumentNullException(nameof(owner));

            if (owner.Kind != LifetimeScopeKind.Scene)
                return;

            GetComponent<RuntimeManagerMB>()?.InstallRuntimeManagerRuntime(builder, owner);
            GetComponent<DynamicObjectRegistryMB>()?.InstallDynamicObjectRegistryRuntime(builder, owner);
            GetComponent<EntityLifetimeScopeSpawnerMB>()?.InstallEntitySpawnerRuntime(builder, owner);
            GetComponent<Scalar.BaseScalarMB>()?.InstallScalarRuntime(builder, owner);
            RuntimeScopeContributionBridge.InstallExplicitInstallerContribution(builder, owner, GetComponent<BulkTransformManagerMB>());
            GetComponent<Common.EventMB>()?.InstallEventRuntime(builder, owner);
        }

        public bool AcceptsVerifiedInstallerComponent(Component component)
        {
            return component is BulkTransformManagerMB;
        }
    }

    [DisallowMultipleComponent]
    public sealed class SceneRootScopeServicesMB : MonoBehaviour
    {
        public void InstallSceneRootRuntime(IRuntimeContainerBuilder builder, IScopeNode owner)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            if (owner == null)
                throw new ArgumentNullException(nameof(owner));

            if (owner.Kind != LifetimeScopeKind.Scene)
                return;

            builder.Register<SceneSpawnerRegistry>(RuntimeLifetime.Singleton)
                .As<ISceneSpawnerRegistry>();
        }
    }

}

