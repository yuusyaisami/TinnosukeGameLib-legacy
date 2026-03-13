using VContainer;
using VContainer.Unity;
using Game.Platform;
using UnityEngine;
using Game.Spawn;
using Game.Project.Scene.Runtime;
using Game.Entity.Search;
using Game.Entity;
using Game.TransformSystem;
using Game.Common;
namespace Game.Scene
{
    [RequireComponent(typeof(Commands.CommandRunnerMB))]
    [RequireComponent(typeof(RuntimeManagerMB))]
    [RequireComponent(typeof(DynamicObjectRegistryMB))]
    [RequireComponent(typeof(EntityLifetimeScopeSpawnerMB))]
    [RequireComponent(typeof(Scalar.BaseScalarMB))]
    [RequireComponent(typeof(BulkTransformManagerMB))]
    [RequireComponent(typeof(Common.EventMB))]
    [RequireComponent(typeof(BlackboardMB))]
    public sealed class SceneLifetimeScope : BaseLifetimeScope<GlobalLifetimeScope>
    {
        // このSceneの協調ビルドのRoot
        protected override bool IsBuildRoot => true;

        // 協調ビルドに参加
        protected override bool UseBuildCoordinator => true;
        protected override bool AutoBuildOnAwake => true;

        protected override void ConfigureBase(IContainerBuilder builder)
        {
            // Scene 単位のサービス登録
            builder.Register<SceneSpawnerRegistry>(Lifetime.Singleton)
                .As<ISceneSpawnerRegistry>();
        }
    }

}
