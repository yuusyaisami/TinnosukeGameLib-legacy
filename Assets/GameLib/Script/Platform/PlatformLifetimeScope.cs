using System;
using Game;
using Game.Common;
using UnityEngine;
using VContainer;
// 繝励Λ繝・ヨ繝輔か繝ｼ繝蝗ｺ譛峨・萓晏ｭ倬未菫ゅｒ逋ｻ骭ｲ縺吶ｋ縺溘ａ縺ｮLifetimeScope
namespace Game.Platform
{
    // 騾壼ｸｸ縺ｮ蜻ｼ縺ｳ蜃ｺ縺励ｈ繧翫ｂ譌ｩ縺丞・譛溷喧縺輔ｌ繧九ｈ縺・↓縺ｪ縺｣縺ｦ縺・∪縺吶・(order = -15)
    // 縺ｾ縺蘖rojectlifetimeScope縺ｮPrefab縺ｮ蟄蝉ｾ帙↓縺ｪ縺｣縺ｦ縺・∪縺吶・
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlatformMB))]
    [RequireComponent(typeof(Game.Scalar.BaseScalarMB))]
    [RequireComponent(typeof(Game.Common.EventMB))] // Project Event Service
    [RequireComponent(typeof(PlatformRootInstallerContributionHostMB))]
    [RequireComponent(typeof(PlatformRootScopeServicesMB))]
    public class PlatformLifetimeScope : KernelScopeHost
    {
        protected override bool UseBuildCoordinator => true; // 譎ｮ騾壹・ LifetimeScope 縺ｨ縺励※襍ｷ蜍墓凾縺ｫ Build
        protected override bool IsBuildRoot => false;
        protected override LifetimeScopeKind RequiredParentKind => LifetimeScopeKind.Project;


    }

    [DisallowMultipleComponent]
    public sealed class PlatformRootInstallerContributionHostMB : MonoBehaviour, IVerifiedInstallerContributionHost
    {
        public void InstallVerifiedInstallerContributions(IRuntimeContainerBuilder builder, IScopeNode owner)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            if (owner == null)
                throw new ArgumentNullException(nameof(owner));

            if (owner.Kind != LifetimeScopeKind.Platform)
                return;

            GetComponent<PlatformMB>()?.InstallPlatformRuntime(builder, owner);
            GetComponent<Game.Scalar.BaseScalarMB>()?.InstallScalarRuntime(builder, owner);
            GetComponent<Game.Common.EventMB>()?.InstallEventRuntime(builder, owner);
        }

        public bool AcceptsVerifiedInstallerComponent(Component component)
        {
            return false;
        }
    }

    [DisallowMultipleComponent]
    public sealed class PlatformRootScopeServicesMB : MonoBehaviour
    {
        public void InstallPlatformRootRuntime(IRuntimeContainerBuilder builder, IScopeNode owner)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            if (owner == null)
                throw new ArgumentNullException(nameof(owner));

            if (owner.Kind != LifetimeScopeKind.Platform)
                return;

            builder.Register<PlatformHardwareVarAutoRegisterService>(RuntimeLifetime.Singleton)
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();
        }
    }
}

