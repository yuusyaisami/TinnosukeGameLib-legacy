#nullable enable
using UnityEngine;
using VContainer;

namespace Game.UI
{
    // ================================================================
    // UIRectMaskMB - UIRectMaskService 逋ｻ骭ｲ逕ｨ MonoBehaviour
    // ================================================================
    //
    // 笊披武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶風
    // 笊・縲宣㍾隕√鷹・鄂ｮ繝ｫ繝ｼ繝ｫ                                             笊・
    // 笊・                                                               笊・
    // 笊・縺薙・繧ｳ繝ｳ繝昴・繝阪Φ繝医・蠢・★ UIElementLifetimeScope 縺ｨ            笊・
    // 笊・縲仙酔縺・GameObject縲代↓驟咲ｽｮ縺吶ｋ縺薙→・・                           笊・
    // 笊・                                                               笊・
    // 笊・豁｣縺励＞驟咲ｽｮ萓・                                                  笊・
    // 笊・  笏披楳 ScrollView (GameObject)                                   笊・
    // 笊・      笏懌楳 UIElementLifetimeScope  竊・蠢・・                       笊・
    // 笊・      笏懌楳 UIRectMaskMB            竊・縺薙・繧ｳ繝ｳ繝昴・繝阪Φ繝・         笊・
    // 笊・      笏懌楳 Unity Mask              竊・Unity 讓呎ｺ悶・ Mask           笊・
    // 笊・      笏懌楳 Image                   竊・Mask 縺ｮ蠖｢迥ｶ繧呈ｱｺ繧√ｋ         笊・
    // 笊・      笏披楳 Content (蟄・                                          笊・
    // 笊・          笏披楳 蟄・UIElement...     竊・Mask 縺ｮ蠖ｱ髻ｿ繧貞女縺代ｋ         笊・
    // 笊・                                                               笊・
    // 笊・窶ｻ Unity 讓呎ｺ悶・ Mask 縺ｾ縺溘・ RectMask2D 縺ｨ菴ｵ逕ｨ縺吶ｋ              笊・
    // 笊・窶ｻ Mask 縺ｮ蠖｢迥ｶ縺ｯ蜷後§ GameObject 縺ｮ Image 縺ｧ豎ｺ縺ｾ繧・             笊・
    // 笊壺武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶幅
    //
    // ## 蠖ｹ蜑ｲ
    //
    // 1. **UIRectMaskService 縺ｮ DI 逋ｻ骭ｲ**: IFeatureInstaller 縺ｨ縺励※逋ｻ骭ｲ
    // 2. **險ｭ螳壹ヵ繧｣繝ｼ繝ｫ繝峨・謠蝉ｾ・*: 繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ驕ｮ阡ｽ髢ｾ蛟､縺ｪ縺ｩ縺ｮ險ｭ螳・
    //
    // ## 險ｭ險域婿驥・
    //
    // - Mask 蛻､螳壹Ο繧ｸ繝・け縺ｯ UIRectMaskService 縺ｫ髮・ｴ・
    // - 縺薙・繧ｳ繝ｳ繝昴・繝阪Φ繝医・ DI 逋ｻ骭ｲ縺ｨ險ｭ螳壹・縺ｿ繧呈球蠖・
    // - 螳滄圀縺ｮ Mask 繧ｳ繝ｳ繝昴・繝阪Φ繝茨ｼ・nity Mask / RectMask2D・峨・蛻･騾泌ｿ・ｦ・
    //
    // ## 菴ｿ逕ｨ縺ｮ豬√ｌ
    //
    // 1. UIRectMaskMB 繧・UIElementLifetimeScope 縺ｨ蜷後§ GameObject 縺ｫ霑ｽ蜉
    // 2. Unity 縺ｮ Mask 縺ｾ縺溘・ RectMask2D 繧貞酔縺・GameObject 縺ｫ霑ｽ蜉
    // 3. Image 繧定ｿｽ蜉縺励※ Mask 縺ｮ蠖｢迥ｶ繧貞ｮ夂ｾｩ
    // 4. 蟄舌・ UIElement 縺ｯ閾ｪ蜍慕噪縺ｫ Mask 縺ｮ蠖ｱ髻ｿ繧貞女縺代ｋ
    //
    // ================================================================

    /// <summary>
    /// UIRectMaskService 繧・DI 逋ｻ骭ｲ縺吶ｋ FeatureInstaller縲・
    /// 
    /// ## 讎りｦ・
    /// 
    /// 縺薙・繧ｳ繝ｳ繝昴・繝阪Φ繝医・ UIRectMaskService 繧偵さ繝ｳ繝・リ縺ｫ逋ｻ骭ｲ縺励・
    /// SelectCandidateProviderScreen 縺九ｉ Mask 蛻､螳壹ｒ蛻ｩ逕ｨ蜿ｯ閭ｽ縺ｫ縺吶ｋ縲・
    /// 
    /// ## 驟咲ｽｮ繝ｫ繝ｼ繝ｫ
    /// 
    /// 蠢・★ UIElementLifetimeScope 縺ｨ蜷後§ GameObject 縺ｫ驟咲ｽｮ縺吶ｋ縺薙→縲・
    /// Unity 縺ｮ Mask/RectMask2D 繧ｳ繝ｳ繝昴・繝阪Φ繝医→菴ｵ逕ｨ縺吶ｋ縲・
    /// </summary>
    public sealed class UIRectMaskMB : MonoBehaviour, IFeatureInstaller
    {
        // ----------------------------------------------------------------
        // Inspector險ｭ螳・
        // ----------------------------------------------------------------

        [Header("Mask Settings")]
        [Tooltip("Inspector setting.")]
        [Range(0f, 1f)]
        [SerializeField]
        float _navigationOcclusionThreshold = 0.5f;

        [Header("Debug")]
        [Tooltip("繝・ヰ繝・げ繝ｭ繧ｰ繧貞・蜉帙☆繧九°")]
        [SerializeField]
        bool _enableDebugLog = false;

        // ----------------------------------------------------------------
        // 繝励Ο繝代ユ繧｣
        // ----------------------------------------------------------------

        /// <summary>
        /// 繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ譎ゅ・驕ｮ阡ｽ髢ｾ蛟､縲・
        /// </summary>
        public float NavigationOcclusionThreshold => _navigationOcclusionThreshold;

        /// <summary>
        /// 繝・ヰ繝・げ繝ｭ繧ｰ繧貞・蜉帙☆繧九°縲・
        /// </summary>
        public bool EnableDebugLog => _enableDebugLog;

        // ----------------------------------------------------------------
        // IFeatureInstaller 螳溯｣・
        // ----------------------------------------------------------------

        /// <summary>
        /// UIRectMaskService 繧・DI 繧ｳ繝ｳ繝・リ縺ｫ逋ｻ骭ｲ縺吶ｋ縲・
        /// </summary>
        public void InstallFeature(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            // UIRectMaskService 繧・Singleton 縺ｧ逋ｻ骭ｲ
            // 閾ｪ蛻・・霄ｫ縺ｮ GameObject 繧・MaskOwner 縺ｨ縺励※貂｡縺・
            var maskOwner = gameObject;
            var threshold = _navigationOcclusionThreshold;

            builder.Register<UIRectMaskService>(RuntimeLifetime.Singleton)
                .WithParameter(typeof(GameObject), maskOwner)
                .WithParameter(typeof(float), threshold)
                .As<IUIRectMaskService>();

            if (_enableDebugLog)
            {
                builder.RegisterBuildCallback(_ =>
                {
                    Debug.Log($"[UIRectMaskMB] Service registered on '{name}'. Threshold: {threshold}");
                });
            }
        }

        // ----------------------------------------------------------------
        // 繧ｨ繝・ぅ繧ｿ逕ｨ
        // ----------------------------------------------------------------

#if UNITY_EDITOR
        void OnValidate()
        {
            // 驟咲ｽｮ繝ｫ繝ｼ繝ｫ縺ｮ隴ｦ蜻・
            var scope = GetComponent<UIElementLifetimeScope>();
            if (scope == null)
            {
                Debug.LogWarning(
                    $"[UIRectMaskMB] '{name}' requires UIElementLifetimeScope on the same GameObject.",
                    this);
            }

            // Unity Mask 縺ｮ遒ｺ隱・
            var mask = GetComponent<UnityEngine.UI.Mask>();
            var rectMask2D = GetComponent<UnityEngine.UI.RectMask2D>();
            if (mask == null && rectMask2D == null)
            {
                Debug.LogWarning(
                    $"[UIRectMaskMB] '{name}' requires Unity Mask or RectMask2D.",
                    this);
            }
        }
#endif
    }
}
