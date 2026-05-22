// Game.Entity
// ================================================================================
// EntityLifetimeScope - Entity 逕ｨ縺ｮ LifetimeScope
// ================================================================================
//
// 縲先ｦりｦ√・
// Entity・医く繝｣繝ｩ繧ｯ繧ｿ繝ｼ縲∵雰縲√い繧､繝・Β縺ｪ縺ｩ・峨ｒ邂｡逅・☆繧・LifetimeScope縲・
// 蜷・Entity 縺斐→縺ｫ迢ｬ遶九＠縺・DI 繧ｹ繧ｳ繝ｼ繝励ｒ謖√■縲・ntity 蝗ｺ譛峨・繧ｵ繝ｼ繝薙せ繧堤匳骭ｲ縺ｧ縺阪ｋ縲・
//
// 縲占ｦｪ繧ｹ繧ｳ繝ｼ繝励・
// - 騾壼ｸｸ縺ｯ FieldLifetimeScope 縺ｾ縺溘・ SceneLifetimeScope 縺ｮ蟄舌→縺励※蟄伜惠
// - 蜊碑ｪｿ繝薙Ν繝峨↓蜿ょ刈縺励∬ｦｪ縺ｮ繝薙Ν繝牙ｮ御ｺ・ｾ後↓繝薙Ν繝峨＆繧後ｋ
//
// 縲職ntityRegistry 縺ｸ縺ｮ逋ｻ骭ｲ縲・
// - EntityIdentityService 縺ｫ繧医ｊ閾ｪ蜍慕噪縺ｫ逋ｻ骭ｲ縺輔ｌ繧・
// - IScopeIdentityService 縺九ｉ Id/Category 繧貞叙蠕励＠縲√う繝ｳ繝・ャ繧ｯ繧ｹ縺ｫ霑ｽ蜉
// - 繧ｹ繧ｳ繝ｼ繝礼ｴ譽・凾縺ｫ閾ｪ蜍慕噪縺ｫ Unregister
//
// 縲仙ｿ・医さ繝ｳ繝昴・繝阪Φ繝医・
// - CommandRunnerMB: 繧ｳ繝槭Φ繝峨す繧ｹ繝・Β
// - BaseScalarMB: 繧ｹ繧ｫ繝ｩ繝ｼ蛟､繧ｷ繧ｹ繝・Β
// - EventMB: 繧､繝吶Φ繝医す繧ｹ繝・Β
// - ActionBlockMB: 繧｢繧ｯ繧ｷ繝ｧ繝ｳ繝悶Ο繝・け
// - FootTransformMB: Entity 雜ｳ菴咲ｽｮ/ Z 繧ｪ繝輔そ繝・ヨ
// ================================================================================

using System;
using UnityEngine;
using Game.Input;
using Game.Profile;
using Game.Common;

namespace Game.Entity
{
    [RequireComponent(typeof(Scalar.BaseScalarMB))]
    [RequireComponent(typeof(Common.EventMB))]
    [RequireComponent(typeof(ActionBlockMB))]
    [RequireComponent(typeof(FootTransformMB))]
    [RequireComponent(typeof(ScopeBindingRegistryMB))]
    [RequireComponent(typeof(EntityInstallerContributionHostMB))]
    public sealed class EntityLifetimeScope : KernelScopeHost
    {
        // Entity 縺ｯ隕ｪ(Field or Scene)縺ｮ荳九〒繝薙Ν繝峨＆繧後ｋ縺ｮ縺ｧ繝ｫ繝ｼ繝医〒縺ｯ縺ｪ縺・
        protected override bool IsBuildRoot => false;

        // 蜊碑ｪｿ繝薙Ν繝峨↓縺ｯ蜿ょ刈縺輔○繧・
        protected override bool UseBuildCoordinator => true;

        // 閾ｪ蜍・Build 縺ｯ荳崎ｦ・ｼ郁ｦｪ縺九ｉ縺ｮ蜊碑ｪｿ繝薙Ν繝・or Spawner 縺碁擇蛟偵ｒ隕九ｋ・・
        protected override bool AutoBuildOnAwake => false;
        protected override LifetimeScopeKind RequiredParentKind => LifetimeScopeKind.Field;

        protected override void ConfigureBase(IRuntimeContainerBuilder builder)
        {
            Debug.Log("[EntityLifetimeScope] Configuring Entity scoped services.");

            // Entity 蜊倅ｽ阪〒谺ｲ縺励＞繧ｵ繝ｼ繝薙せ逋ｻ骭ｲ
            // builder.Register<EnemyBrain>(RuntimeLifetime.Scoped);
            // builder.RegisterInstance(this); // 縺ｪ縺ｩ
        }
    }

    [DisallowMultipleComponent]
    public sealed class EntityInstallerContributionHostMB : MonoBehaviour, IVerifiedInstallerContributionHost
    {
        public void InstallVerifiedInstallerContributions(IRuntimeContainerBuilder builder, IScopeNode owner)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            if (owner == null)
                throw new ArgumentNullException(nameof(owner));

            if (owner.Kind != LifetimeScopeKind.Entity)
                return;

            GetComponent<Scalar.BaseScalarMB>()?.InstallScalarRuntime(builder, owner);
            GetComponent<Common.EventMB>()?.InstallEventRuntime(builder, owner);
            GetComponent<ActionBlockMB>()?.InstallActionBlockRuntime(builder, owner);
            GetComponent<ScopeBindingRegistryMB>()?.InstallScopeBindingRegistryRuntime(builder, owner);
        }

        public bool AcceptsVerifiedInstallerComponent(Component component)
        {
            return false;
        }
    }
}


