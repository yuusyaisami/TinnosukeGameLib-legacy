using System;
using Game.Commands;
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
    public sealed class SceneLifetimeScope : RuntimeLifetimeScopeBase
    {
        // 縺薙・Scene縺ｮ蜊碑ｪｿ繝薙Ν繝峨・Root
        protected override bool IsBuildRoot => true;

        // 蜊碑ｪｿ繝薙Ν繝峨↓蜿ょ刈
        protected override bool UseBuildCoordinator => true;
        protected override bool AutoBuildOnAwake => true;
        protected override LifetimeScopeKind RequiredParentKind => LifetimeScopeKind.Global;

        protected override void AwakeConfigure(IRuntimeContainerBuilder builder)
        {
            var commandRunner = GetComponent<CommandRunnerMB>();
            if (commandRunner == null)
                throw new InvalidOperationException($"{nameof(SceneLifetimeScope)} requires {nameof(CommandRunnerMB)}.");

            commandRunner.InstallRuntime(builder, this);

            var runtimeManager = GetComponent<RuntimeManagerMB>();
            if (runtimeManager == null)
                throw new InvalidOperationException($"{nameof(SceneLifetimeScope)} requires {nameof(RuntimeManagerMB)}.");

            runtimeManager.InstallRuntime(builder, this);
        }

        protected override void ConfigureBase(IRuntimeContainerBuilder builder)
        {
            // Scene 蜊倅ｽ阪・繧ｵ繝ｼ繝薙せ逋ｻ骭ｲ
            builder.Register<SceneSpawnerRegistry>(RuntimeLifetime.Singleton)
                .As<ISceneSpawnerRegistry>();
        }
    }

}
