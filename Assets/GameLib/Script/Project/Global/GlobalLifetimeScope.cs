using System;
using Game.Platform;
using Game.Project.Bootstrap;

using UnityEngine;
namespace Game
{
    // 騾壼ｸｸ縺ｮ蜻ｼ縺ｳ蜃ｺ縺励ｈ繧翫ｂ譌ｩ縺丞・譛溷喧縺輔ｌ繧九ｈ縺・↓縺ｪ縺｣縺ｦ縺・∪縺吶・(order = -10)
    [RequireComponent(typeof(Game.Scalar.BaseScalarMB))]
    [RequireComponent(typeof(Game.Common.EventMB))] // Global Event Service
    [RequireComponent(typeof(GlobalRootInstallerContributionHostMB))]
    public class GlobalLifetimeScope : KernelScopeHost
    {
        static GlobalLifetimeScope _instance;

        // 蜊碑ｪｿ繝薙Ν繝峨↓蜿ょ刈縺励※隕ｪ・・latformLifetimeScope・峨・螳御ｺ・ｒ蠕・▽
        protected override bool UseBuildCoordinator => true;
        protected override bool IsBuildRoot => true;       // EnsureInScene縺九ｉ閾ｪ蜍輔ン繝ｫ繝・
        protected override bool AutoBuildOnAwake => true; // 
        protected override LifetimeScopeKind RequiredParentKind => LifetimeScopeKind.Platform;

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
            base.Awake();
        }

        protected override void ConfigureBase(IRuntimeContainerBuilder builder)
        {
            // Global 繧ｹ繧ｳ繝ｼ繝怜崋譛峨・逋ｻ骭ｲ繧偵％縺薙↓譖ｸ縺・
            Debug.Log("[GlobalLifetimeScope] Configuring Global scoped services.");
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void EnsureInScene()
        {
            if (KernelLiveBootRuntime.ShouldSuppressLegacyAutoBootstrap())
                return;

            KernelLiveBootRuntime.ThrowLegacyAutoBootstrapForbidden(nameof(GlobalLifetimeScope));
        }

    }

    [DisallowMultipleComponent]
    public sealed class GlobalRootInstallerContributionHostMB : MonoBehaviour, IVerifiedInstallerContributionHost
    {
        public void InstallVerifiedInstallerContributions(IRuntimeContainerBuilder builder, IScopeNode owner)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            if (owner == null)
                throw new ArgumentNullException(nameof(owner));

            if (owner.Kind != LifetimeScopeKind.Global)
                return;

            GetComponent<Game.Scalar.BaseScalarMB>()?.InstallScalarRuntime(builder, owner);
            GetComponent<Game.Common.EventMB>()?.InstallEventRuntime(builder, owner);
        }

        public bool AcceptsVerifiedInstallerComponent(Component component)
        {
            return false;
        }
    }
}

