using System;
using Game;
using Game.Commands;
using Game.Common;
using UnityEngine;
// 繝励Λ繝・ヨ繝輔か繝ｼ繝蝗ｺ譛峨・萓晏ｭ倬未菫ゅｒ逋ｻ骭ｲ縺吶ｋ縺溘ａ縺ｮLifetimeScope
namespace Game.Platform
{
    // 騾壼ｸｸ縺ｮ蜻ｼ縺ｳ蜃ｺ縺励ｈ繧翫ｂ譌ｩ縺丞・譛溷喧縺輔ｌ繧九ｈ縺・↓縺ｪ縺｣縺ｦ縺・∪縺吶・(order = -15)
    // 縺ｾ縺蘖rojectlifetimeScope縺ｮPrefab縺ｮ蟄蝉ｾ帙↓縺ｪ縺｣縺ｦ縺・∪縺吶・
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlatformMB))]
    [RequireComponent(typeof(Game.Commands.CommandRunnerMB))]
    [RequireComponent(typeof(Game.Scalar.BaseScalarMB))]
    [RequireComponent(typeof(Game.Common.EventMB))] // Project Event Service
    public class PlatformLifetimeScope : RuntimeLifetimeScopeBase
    {
        protected override bool UseBuildCoordinator => true; // 譎ｮ騾壹・ LifetimeScope 縺ｨ縺励※襍ｷ蜍墓凾縺ｫ Build
        protected override bool IsBuildRoot => false;
        protected override LifetimeScopeKind RequiredParentKind => LifetimeScopeKind.Project;

        protected override void AwakeConfigure(IRuntimeContainerBuilder builder)
        {
            var commandRunner = GetComponent<CommandRunnerMB>();
            if (commandRunner == null)
                throw new InvalidOperationException($"{nameof(PlatformLifetimeScope)} requires {nameof(CommandRunnerMB)}.");

            commandRunner.InstallRuntime(builder, this);
        }

        protected override void ConfigureBase(IRuntimeContainerBuilder builder)
        {
            // Platform 繧ｹ繧ｳ繝ｼ繝怜崋譛峨・逋ｻ骭ｲ繧偵％縺薙↓譖ｸ縺・

            builder.Register<PlatformHardwareVarAutoRegisterService>(RuntimeLifetime.Singleton)
                   .As<IScopeAcquireHandler>()
                   .As<IScopeReleaseHandler>();
        }


    }
}
