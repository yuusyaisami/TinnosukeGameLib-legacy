#nullable enable
using System;
using Game.Commands;
using UnityEngine;
using Game.Scene;
using Game.Common;

namespace Game.UI
{
    // ================================================================
    // UILifetimeScope: UI髫主ｱ､縺ｮ繝ｫ繝ｼ繝医→縺ｪ繧記ifetimeScope
    // ================================================================
    //
    // ## 讎りｦ・
    //
    // UILifetimeScope縺ｯ縲ゞI蜈ｨ菴薙・繝ｫ繝ｼ繝医さ繝ｳ繝・リ縺ｨ縺励※讖溯・縺励・
    // UI髢｢騾｣縺ｮ蜈ｱ騾壹し繝ｼ繝薙せ繧呈署萓帙☆繧九・
    //
    // ## 繧ｵ繝ｼ繝薙せ逋ｻ骭ｲ
    //
    // 莉･荳九・繧ｵ繝ｼ繝薙せ縺後％縺ｮ繧ｹ繧ｳ繝ｼ繝励〒逋ｻ骭ｲ縺輔ｌ繧・
    // - IUIElementLifecycleService: UIElement逕滓・/蜑企勁縺ｮ荳蜈・ｮ｡逅・
    //
    // ## 驟咲ｽｮ
    //
    // 騾壼ｸｸ縲√す繝ｼ繝ｳ縺ｮLifetimeScope縺ｮ逶ｴ荳九↓驟咲ｽｮ縺輔ｌ繧九・
    // UIElementLifetimeScope縺ｯ縺薙・繧ｹ繧ｳ繝ｼ繝励・蟄舌→縺ｪ繧九・
    //
    // ================================================================

    [RequireComponent(typeof(Game.Commands.CommandRunnerMB))]
    [RequireComponent(typeof(Game.Scalar.BaseScalarMB))]
    [RequireComponent(typeof(Game.Common.EventMB))]

    // UI蟆ら畑RequireComponent螻樊ｧ
    [RequireComponent(typeof(UIInputMB))]
    [RequireComponent(typeof(UINavigationMB))]
    [RequireComponent(typeof(ModalStackChannelHubMB))]
    [RequireComponent(typeof(UISelectionMB))]
    [RequireComponent(typeof(UICanvasMB))]
    public class UILifetimeScope : RuntimeLifetimeScopeBase
    {
        // UI 縺ｯ隕ｪ(Scene)縺ｮ荳九〒繝薙Ν繝峨＆繧後ｋ縺ｮ縺ｧ繝ｫ繝ｼ繝医〒縺ｯ縺ｪ縺・
        protected override bool IsBuildRoot => false;
        // 蜊碑ｪｿ繝薙Ν繝峨↓縺ｯ蜿ょ刈縺輔○繧・
        protected override bool UseBuildCoordinator => true;
        // 閾ｪ蜍・Build 縺ｯ荳崎ｦ・ｼ郁ｦｪ縺九ｉ縺ｮ蜊碑ｪｿ繝薙Ν繝・or BaseLifetimeScopeSpawner 縺碁擇蛟偵ｒ隕九ｋ・・
        protected override bool AutoBuildOnAwake => false;
        protected override LifetimeScopeKind RequiredParentKind => LifetimeScopeKind.Scene;

        protected override void AwakeConfigure(IRuntimeContainerBuilder builder)
        {
            var commandRunner = GetComponent<CommandRunnerMB>();
            if (commandRunner == null)
                throw new InvalidOperationException($"{nameof(UILifetimeScope)} requires {nameof(CommandRunnerMB)}.");

            commandRunner.InstallRuntime(builder, this);
        }

        protected override void ConfigureBase(IRuntimeContainerBuilder builder)
        {
            // ----------------------------------------------------------------
            // UIElementLifecycleService
            // ----------------------------------------------------------------
            //
            // UI隕∫ｴ縺ｮ逕滓・繝ｻ蜑企勁繧剃ｸ蜈・噪縺ｫ邂｡逅・☆繧九し繝ｼ繝薙せ縲・
            // 莉･荳九・讖溯・繧呈署萓・
            // - BaseLifetimeScopeSpawner邨檎罰縺ｮUIElement逕滓・
            // - IScopeLifecycleService繧剃ｽｿ縺｣縺溷ｮ牙・縺ｪ蜑企勁
            // - Blackboard/Command縺ｮ繧ｳ繝ｳ繝・く繧ｹ繝郁ｨｭ螳・
            //
            // 縺薙・繧ｵ繝ｼ繝薙せ縺ｯUILifetimeScope縺ｧ繧ｰ繝ｭ繝ｼ繝舌Ν縺ｫ逋ｻ骭ｲ縺輔ｌ縲・
            // 縺吶∋縺ｦ縺ｮ蟄舌せ繧ｳ繝ｼ繝励°繧牙茜逕ｨ蜿ｯ閭ｽ縺ｨ縺ｪ繧九・
            // ----------------------------------------------------------------
            builder.Register<UIElementLifecycleService>(RuntimeLifetime.Singleton)
                .As<IUIElementLifecycleService>()
                .WithParameter<Transform>(transform);
        }
    }
}
