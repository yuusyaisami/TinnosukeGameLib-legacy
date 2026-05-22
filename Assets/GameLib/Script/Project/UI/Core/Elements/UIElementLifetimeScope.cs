#nullable enable
using UnityEngine;
using System.Collections.Generic;
using Game.Common;

namespace Game.UI
{
    // ================================================================
    // UIElementLifetimeScope: UIElement縺ｮ蝓ｺ逶､縺ｨ縺ｪ繧記ifetimeScope
    // ================================================================
    //
    // ## 讎りｦ・
    //
    // UIElementLifetimeScope縺ｯ縲ゞI縺ｫ縺翫￠繧倶ｸ縺､縺ｮ驕ｸ謚槫腰菴阪ｒ陦ｨ縺吶・
    // 縺吶∋縺ｦ縺ｮUIElement・医・繧ｿ繝ｳ縲√ヱ繝阪Ν縲√ム繧､繧｢繝ｭ繧ｰ遲会ｼ峨・
    // 縺薙・繧ｯ繝ｩ繧ｹ縺ｾ縺溘・豢ｾ逕溘け繝ｩ繧ｹ繧偵い繧ｿ繝・メ縺吶ｋ縲・
    //
    // ## UIElement縺ｮ險ｭ險域晄Φ
    //
    // UIElement縺ｯFeatureInstaller縺ｫ繧医ｊ譟碑ｻ溘↓讖溯・繧定ｿｽ蜉縺ｧ縺阪ｋ:
    // - 繝懊ち繝ｳ讖溯・繧定ｿｽ蜉 竊・ButtonChannelHubMB
    // - 繝壹・繧ｸ讖溯・繧定ｿｽ蜉 竊・UIPageFeatureInstaller
    // - 繝医げ繝ｫ讖溯・繧定ｿｽ蜉 竊・UIToggleFeatureInstaller
    //
    // ## 蠢・医さ繝ｳ繝昴・繝阪Φ繝・
    //
    // - **UIElementStateMB**: Active/Visible迥ｶ諷九∝ｽ薙◆繧雁愛螳壹√リ繝薙ご繝ｼ繧ｷ繝ｧ繝ｳ險ｭ螳・
    //   RequireComponent縺ｧ蠑ｷ蛻ｶ逧・↓繧｢繧ｿ繝・メ縺輔ｌ繧・
    //
    // - **CommandRunnerMB**: 繧ｳ繝槭Φ繝牙ｮ溯｡梧ｩ溯・
    // - **BaseScalarMB**: 繧ｹ繧ｫ繝ｩ繝ｼ蛟､邂｡逅・
    // - **EventMB**: 繧､繝吶Φ繝医す繧ｹ繝・Β
    //
    // ## Active迥ｶ諷九↓縺､縺・※
    //
    // UI繧ｷ繧ｹ繝・Β縺ｫ縺翫＞縺ｦ縲；ameObject縺ｮSetActive(false)縺ｯ菴ｿ逕ｨ縺励↑縺・・
    // GameObject閾ｪ菴薙・蟶ｸ縺ｫactive=true縺ｮ縺ｾ縺ｾ縺ｧ縺ゅｊ縲ゞI繧ｷ繧ｹ繝・Β蜀・Κ縺ｮ
    // 繝ｭ繧ｸ繝・け縺ｨ縺励※Active迥ｶ諷九ｒ邂｡逅・☆繧具ｼ・IElementStateService・峨・
    //
    // Active=false縺ｮ蝣ｴ蜷・
    // - 驕ｸ謚槫ｯｾ雎｡縺九ｉ髯､螟・
    // - 蜈･蜉帙う繝吶Φ繝医ｒ蜿励￠蜿悶ｉ縺ｪ縺・
    // - 隕ｪ縺窟ctive=false縺ｪ繧峨∝ｭ舌ｂ螳溯ｳｪ逧・↓Active=false
    //
    // ## IUIModalRoot縺ｮ螳溯｣・
    //
    // 縺薙・繧ｯ繝ｩ繧ｹ縺ｯIUIModalRoot繧貞ｮ溯｣・＠縺ｦ縺翫ｊ縲・
    // Modal Stack縺ｫ逋ｻ骭ｲ蜿ｯ閭ｽ縺ｪ繝ｫ繝ｼ繝郁ｦ∫ｴ縺ｨ縺励※讖溯・縺吶ｋ縲・
    //
    // Modal Stack縺ｫ逋ｻ骭ｲ縺輔ｌ繧九→:
    // - 縺薙・Element驟堺ｸ九・縺ｿ縺碁∈謚槫庄閭ｽ縺ｨ縺ｪ繧具ｼ磯∈謚槭・繧ｯ繝ｩ繝ｳ繝暦ｼ・
    // - 繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ縺ｮ謐懃ｴ｢遽・峇縺後％縺ｮ驟堺ｸ九↓蛻ｶ髯舌＆繧後ｋ
    //
    // ## IUIInputConsumerHub
    //
    // UIElement縺ｫ縺ｯ隍・焚縺ｮIUIInputConsumer縺檎匳骭ｲ縺輔ｌ繧句庄閭ｽ諤ｧ縺後≠繧・
    // - 繝懊ち繝ｳ謚ｼ荳句・逅・
    // - 繧ｹ繧ｯ繝ｭ繝ｼ繝ｫ蜃ｦ逅・
    // - 繝峨Λ繝・げ蜃ｦ逅・
    //
    // 縺薙ｌ繧峨・IUIInputConsumerHub繧帝壹§縺ｦ髮・ｴ・ｮ｡逅・＆繧後ｋ縲・
    // VContainer縺ｧ縺ｯIEnumerable<T>縺ｧ隍・焚隗｣豎ｺ繧ゅ〒縺阪ｋ縺後・
    // Hub繧剃ｽｿ逕ｨ縺吶ｋ縺薙→縺ｧ蜆ｪ蜈亥ｺｦ繧ｽ繝ｼ繝医ｄ蜍慕噪逋ｻ骭ｲ縺悟庄閭ｽ縺ｫ縺ｪ繧九・
    //
    // ================================================================

    /// <summary>
    /// UIElement縺ｮ蝓ｺ逶､縺ｨ縺ｪ繧記ifetimeScope縲・
    /// 
    /// ## 險ｭ險域婿驥・
    /// 
    /// 縺薙・繧ｯ繝ｩ繧ｹ閾ｪ菴薙↓縺ｯ譛蟆城剞縺ｮ讖溯・縺ｮ縺ｿ繧呈戟縺溘○縲・
    /// 蜈ｷ菴鍋噪縺ｪ蜃ｦ逅・・Service繧ЁeatureInstaller縺ｫ蟋碑ｭｲ縺吶ｋ縲・
    /// </summary>
    [RequireComponent(typeof(Game.Scalar.BaseScalarMB))]
    [RequireComponent(typeof(Game.Common.EventMB))]
    [RequireComponent(typeof(UIElementStateMB))]
    public class UIElementLifetimeScope : KernelScopeHost
    {
        // ----------------------------------------------------------------
        // BaseLifetimeScope險ｭ螳・
        // ----------------------------------------------------------------

        /// <summary>
        /// UI Window 縺ｯ隕ｪ(UI)縺ｮ荳九〒繝薙Ν繝峨＆繧後ｋ縺ｮ縺ｧ繝ｫ繝ｼ繝医〒縺ｯ縺ｪ縺・・
        /// </summary>
        protected override bool IsBuildRoot => false;

        /// <summary>
        /// 蜊碑ｪｿ繝薙Ν繝峨↓縺ｯ蜿ょ刈縺輔○繧九・
        /// </summary>
        protected override bool UseBuildCoordinator => true;

        /// <summary>
        /// 閾ｪ蜍・Build 縺ｯ荳崎ｦ・ｼ郁ｦｪ縺九ｉ縺ｮ蜊碑ｪｿ繝薙Ν繝・or BaseLifetimeScopeSpawner 縺碁擇蛟偵ｒ隕九ｋ・峨・
        /// </summary>
        protected override bool AutoBuildOnAwake => false;
        protected override LifetimeScopeKind RequiredParentKind => LifetimeScopeKind.UI;

        // ----------------------------------------------------------------
        // ConfigureBase
        // ----------------------------------------------------------------

        /// <summary>
        /// Awake譎ゅ・險ｭ螳壹・
        /// 繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ縺ｫ縺翫￠繧句・譛溯ｨｭ螳夂ｭ峨ｒ險倩ｿｰ蜿ｯ閭ｽ縲・
        /// </summary>
        protected override void AwakeConfigure(IRuntimeContainerBuilder builder)
        {
            // 蟆・擂逧・↓UIElement蝗ｺ譛峨・蛻晄悄險ｭ螳壹ｒ霑ｽ蜉蜿ｯ閭ｽ
        }

        /// <summary>
        /// DI繧ｳ繝ｳ繝・リ縺ｮ蝓ｺ譛ｬ讒区・縲・
        /// 
        /// ## 逋ｻ骭ｲ蜀・ｮｹ
        /// 
        /// - **IUIInputConsumerHub**: 隍・焚縺ｮIUIInputConsumer繧帝寔邏・ｮ｡逅・
        /// 
        /// ## 豕ｨ諢・
        /// 
        /// UIElementStateService縺ｯUIElementStateMB縺ｧ逋ｻ骭ｲ縺輔ｌ繧九・
        /// 縺薙・繝｡繧ｽ繝・ラ縺ｧ縺ｯ逋ｻ骭ｲ縺励↑縺・・
        /// </summary>
        protected override void ConfigureBase(IRuntimeContainerBuilder builder)
        {

        }

    }
}

