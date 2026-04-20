#nullable enable
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.UI
{
    // ================================================================
    // UINavigationMB: UINavigationService縺ｮFeatureInstaller
    // ================================================================

    /// <summary>
    /// UINavigationService繧奪I繧ｳ繝ｳ繝・リ縺ｫ逋ｻ骭ｲ縺吶ｋFeatureInstaller縲・
    /// 
    /// ## 讎りｦ・
    /// 
    /// UINavigationService縺ｯ縲ゞIInputService縺九ｉ蜿励￠蜿悶▲縺欟IInputEvent繧貞・逅・＠縲・
    /// 迴ｾ蝨ｨ驕ｸ謚樔ｸｭ縺ｮUIElement縺ｫ驟堺ｿ｡縺吶ｋ蠖ｹ蜑ｲ繧呈戟縺､縲・
    /// 縺ｾ縺溘∵婿蜷代く繝ｼ蜈･蜉帙↓繧医ｋ繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ・井ｸ贋ｸ句ｷｦ蜿ｳ縺ｮ驕ｸ謚樒ｧｻ蜍包ｼ峨ｂ諡・ｽ薙☆繧九・
    /// 
    /// ## 荳ｻ縺ｪ讖溯・
    /// 
    /// 1. **蜈･蜉帙う繝吶Φ繝医・驟堺ｿ｡**: 迴ｾ蝨ｨ縺ｮSelect縺ｫUIInputEvent繧帝・菫｡
    /// 2. **譁ｹ蜷代リ繝薙ご繝ｼ繧ｷ繝ｧ繝ｳ**: 荳贋ｸ句ｷｦ蜿ｳ繧ｭ繝ｼ縺ｫ繧医ｋ驕ｸ謚樒ｧｻ蜍・
    /// 3. **繝ｪ繝斐・繝亥・逅・*: 譁ｹ蜷代く繝ｼ髟ｷ謚ｼ縺玲凾縺ｮ騾｣邯夂ｧｻ蜍・
    /// 
    /// ## 險ｭ螳夐・岼
    /// 
    /// - 繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ縺ｮ蜈･蜉幃明蛟､
    /// - 繝ｪ繝斐・繝磯幕蟋九∪縺ｧ縺ｮ驕・ｻｶ譎る俣
    /// - 繝ｪ繝斐・繝磯俣髫・
    /// </summary>
    public sealed class UINavigationMB : MonoBehaviour, IFeatureInstaller
    {
        // ----------------------------------------------------------------
        // Inspector險ｭ螳・
        // ----------------------------------------------------------------

        [Header("Navigation Threshold")]
        [Tooltip("Inspector setting.")]
        [Range(0.1f, 0.9f)]
        [SerializeField]
        float _navigateThreshold = 0.5f;

        [Header("Repeat Settings")]
        [Tooltip("Inspector setting.")]
        [Range(0.1f, 1.0f)]
        [SerializeField]
        float _repeatDelay = 0.4f;

        [Tooltip("Inspector setting.")]
        [Range(0.05f, 0.5f)]
        [SerializeField]
        float _repeatRate = 0.1f;

        [Header("Debug")]
        [Tooltip("繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ繧､繝吶Φ繝医・繝ｭ繧ｰ繧貞・蜉帙☆繧九°")]
        [SerializeField]
        bool _enableNavigationLogging = false;



        [Header("Debug")]
        [SerializeField]
        UINavigationDebugView _debugView = new UINavigationDebugView();

        // ----------------------------------------------------------------
        // IFeatureInstaller螳溯｣・
        // ----------------------------------------------------------------

        /// <summary>
        /// UINavigationService繧奪I繧ｳ繝ｳ繝・リ縺ｫ逋ｻ骭ｲ縺吶ｋ縲・
        /// 
        /// 逋ｻ骭ｲ鬆・ｺ上・豕ｨ諢・
        /// - UINavigationService縺ｯIUISelectionService縺ｫ萓晏ｭ倥☆繧・
        /// - UISelectionMB縺悟・縺ｫ逋ｻ骭ｲ縺輔ｌ縺ｦ縺・ｋ蠢・ｦ√′縺ゅｋ
        /// </summary>
        public void InstallFeature(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            // 繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ險ｭ螳壹ｒ逋ｻ骭ｲ
            builder.RegisterInstance(new UINavigationOptions
            {
                NavigateThreshold = _navigateThreshold,
                RepeatDelay = _repeatDelay,
                RepeatRate = _repeatRate,
                EnableNavigationLogging = _enableNavigationLogging
            });

            // UINavigationService繧堤匳骭ｲ
            // - IUINavigationService: 蜈ｬ髢九う繝ｳ繧ｿ繝ｼ繝輔ぉ繝ｼ繧ｹ
            builder.Register<UINavigationService>(RuntimeLifetime.Singleton)
                .As<IUINavigationService>()
                .As<IUINavigationTelemetry>();

            builder.Register<UIInputNavigateManagerService>(RuntimeLifetime.Singleton)
            .As<IUIInputNavigateService>()
            .As<IScopeAcquireHandler>()
            .As<IScopeReleaseHandler>();

            // Register debug view instance and bind
            builder.RegisterInstance(_debugView);
            builder.RegisterBuildCallback(container =>
            {
                if (container.TryResolve<IUINavigationTelemetry>(out var telemetry))
                {
                    _debugView.Bind(telemetry);
                }
            });
        }
    }

    // ================================================================
    // UINavigationOptions: UINavigationService縺ｮ繧ｪ繝励す繝ｧ繝ｳ險ｭ螳・
    // ================================================================

    /// <summary>
    /// UINavigationService縺ｮ繧ｪ繝励す繝ｧ繝ｳ險ｭ螳壹・
    /// MB縺ｮInspector險ｭ螳壹ｒService縺ｫ貂｡縺吶◆繧√↓菴ｿ逕ｨ縲・
    /// </summary>
    public sealed class UINavigationOptions
    {
        /// <summary>譁ｹ蜷大・蜉帙→縺励※隱崎ｭ倥☆繧区怙蟆上・螟ｧ縺阪＆</summary>
        public float NavigateThreshold { get; set; } = 0.5f;

        /// <summary>繝ｪ繝斐・繝医′蟋九∪繧九∪縺ｧ縺ｮ驕・ｻｶ・育ｧ抵ｼ・/summary>
        public float RepeatDelay { get; set; } = 0.4f;

        /// <summary>繝ｪ繝斐・繝育匱轣ｫ縺ｮ髢馴囈・育ｧ抵ｼ・/summary>
        public float RepeatRate { get; set; } = 0.1f;

        /// <summary>繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ繧､繝吶Φ繝医・繝ｭ繧ｰ繧貞・蜉帙☆繧九°</summary>
        public bool EnableNavigationLogging { get; set; }
    }
}
