#nullable enable
using UnityEngine;
using Game.Input;

namespace Game.UI
{
    // ================================================================
    // UIInputMB: UIInputService縺ｮFeatureInstaller
    // ================================================================

    /// <summary>
    /// UIInputService繧奪I繧ｳ繝ｳ繝・リ縺ｫ逋ｻ骭ｲ縺吶ｋFeatureInstaller縲・
    /// 
    /// ## 讎りｦ・
    /// 
    /// UIInputService縺ｯ縲∽ｽ弱Ξ繝吶Ν縺ｮInputRouter・医・繝ｭ繧ｸ繧ｧ繧ｯ繝亥・菴薙・蜈･蜉帑ｺ､騾壽紛逅・ｼ峨°繧・
    /// 蜈･蜉帙ｒ蜿励￠蜿悶ｊ縲ゞI蟆ら畑縺ｮUIInputEvent縺ｫ螟画鋤縺励※NavigationService縺ｸ豬√☆蠖ｹ蜑ｲ繧呈戟縺､縲・
    /// 
    /// ## 繝・・繧ｿ繝輔Ο繝ｼ
    /// 
    /// InputRouter (IInputConsumer)
    ///     竊・InputFrame
    /// UIInputService (螟画鋤蜃ｦ逅・
    ///     竊・UIInputEvent
    /// UINavigationService
    ///     竊・
    /// 迴ｾ蝨ｨ驕ｸ謚樔ｸｭ縺ｮUIElement
    /// 
    /// ## 險ｭ螳夐・岼
    /// 
    /// 縺薙・MB縺ｧ縺ｯ迚ｹ縺ｫ險ｭ螳夐・岼縺ｯ縺ゅｊ縺ｾ縺帙ｓ縺後・
    /// 蟆・擂逧・↓蜈･蜉帛､画鋤縺ｮ繧ｫ繧ｹ繧ｿ繝槭う繧ｺ繧ｪ繝励す繝ｧ繝ｳ繧定ｿｽ蜉蜿ｯ閭ｽ縲・
    /// </summary>
    public sealed class UIInputMB : MonoBehaviour, IFeatureInstaller
    {
        // ----------------------------------------------------------------
        // Inspector險ｭ螳夲ｼ亥ｰ・擂縺ｮ諡｡蠑ｵ逕ｨ・・
        // ----------------------------------------------------------------

        [Header("Debug")]
        [Tooltip("蜈･蜉帙う繝吶Φ繝医・繝ｭ繧ｰ繧貞・蜉帙☆繧九°")]
        [SerializeField]
        bool _enableInputLogging = false;

        [Header("ModalStack Input Guard")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        bool _blockInputAfterModalActiveRootsChanged = true;

        [Tooltip("ActiveRoots 螟画峩蠕後↓蜈･蜉帙ｒ繝悶Ο繝・け縺吶ｋ遘呈焚")]
        [Min(0f)]
        [SerializeField]
        float _blockDurationAfterModalActiveRootsChanged = 0.1f;

        [Header("Pointer Move Optimization")]
        [Tooltip("Inspector setting.")]
        [Min(0f)]
        [SerializeField]
        float _pointerMovePixelThreshold = 1.0f;

        [Tooltip("繝昴う繝ｳ繧ｿ繝ｼ遘ｻ蜍輔う繝吶Φ繝医・譛蟆冗匱轣ｫ髢馴囈・育ｧ抵ｼ峨・縺ｧ豈弱ヵ繝ｬ繝ｼ繝")]
        [Min(0f)]
        [SerializeField]
        float _pointerMoveSampleInterval = 0.02f;

        [Tooltip("繝昴う繝ｳ繧ｿ繝ｼ繝懊ち繝ｳ謚ｼ荳区凾縺ｯ遘ｻ蜍暮㍼/髢馴囈繧堤┌隕悶＠縺ｦ蜷梧悄縺吶ｋ")]
        [SerializeField]
        bool _forcePointerSyncOnPress = true;

        // ----------------------------------------------------------------
        // IFeatureInstaller螳溯｣・
        // ----------------------------------------------------------------

        /// <summary>
        /// UIInputService縺ｨ縺昴・髢｢騾｣繧ｵ繝ｼ繝薙せ繧奪I繧ｳ繝ｳ繝・リ縺ｫ逋ｻ骭ｲ縺吶ｋ縲・
        /// 
        /// 逋ｻ骭ｲ鬆・ｺ上・豕ｨ諢・
        /// - UIInputService縺ｯIUINavigationService縺ｫ萓晏ｭ倥☆繧・
        /// - 縺昴・縺溘ａ縲ゞINavigationMB繧医ｊ蠕後↓逋ｻ骭ｲ縺輔ｌ繧九°縲・
        ///   蜷後§Configure繝輔ぉ繝ｼ繧ｺ縺ｧ荳邱偵↓逋ｻ骭ｲ縺輔ｌ繧句ｿ・ｦ√′縺ゅｋ
        /// </summary>
        public void InstallFeature(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            // UIInputService繧堤匳骭ｲ
            // - IUIInputService: 蜈ｬ髢九う繝ｳ繧ｿ繝ｼ繝輔ぉ繝ｼ繧ｹ
            // - IScopeAcquireHandler: Acquire譎ゅ↓InputRouter縺ｸ縺ｮ逋ｻ骭ｲ繧定｡後≧
            // - IDisposable: Dispose譎ゅ↓繧ｯ繝ｪ繝ｼ繝ｳ繧｢繝・・繧定｡後≧
            builder.Register<UIInputService>(RuntimeLifetime.Singleton)
                .As<IUIInputService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .As<global::System.IDisposable>();

            // 險ｭ螳壼､繧堤匳骭ｲ・亥ｰ・擂縺ｮ諡｡蠑ｵ逕ｨ・・
            builder.RegisterInstance(new UIInputOptions
            {
                EnableInputLogging = _enableInputLogging,
                BlockInputAfterModalActiveRootsChanged = _blockInputAfterModalActiveRootsChanged,
                BlockDurationAfterModalActiveRootsChanged = Mathf.Max(0f, _blockDurationAfterModalActiveRootsChanged),
                PointerMovePixelThreshold = Mathf.Max(0f, _pointerMovePixelThreshold),
                PointerMoveSampleInterval = Mathf.Max(0f, _pointerMoveSampleInterval),
                ForcePointerSyncOnPress = _forcePointerSyncOnPress
            });
        }
    }

    // ================================================================
    // UIInputOptions: UIInputService縺ｮ繧ｪ繝励す繝ｧ繝ｳ險ｭ螳・
    // ================================================================

    /// <summary>
    /// UIInputService縺ｮ繧ｪ繝励す繝ｧ繝ｳ險ｭ螳壹・
    /// MB縺ｮInspector險ｭ螳壹ｒService縺ｫ貂｡縺吶◆繧√↓菴ｿ逕ｨ縲・
    /// </summary>
    public sealed class UIInputOptions
    {
        /// <summary>蜈･蜉帙う繝吶Φ繝医・繝ｭ繧ｰ繧貞・蜉帙☆繧九°</summary>
        public bool EnableInputLogging { get; set; }

        /// <summary>ActiveRoots 螟画峩蠕後↓蜈･蜉帙ヶ繝ｭ繝・け繧呈怏蜉ｹ蛹悶☆繧九°</summary>
        public bool BlockInputAfterModalActiveRootsChanged { get; set; } = true;

        /// <summary>ActiveRoots 螟画峩蠕後↓蜈･蜉帙ｒ繝悶Ο繝・け縺吶ｋ遘呈焚</summary>
        public float BlockDurationAfterModalActiveRootsChanged { get; set; } = 0.1f;

        /// <summary>繝昴う繝ｳ繧ｿ繝ｼ遘ｻ蜍輔う繝吶Φ繝医ｒ逋ｺ轣ｫ縺吶ｋ譛蟆冗ｧｻ蜍暮㍼・医ヴ繧ｯ繧ｻ繝ｫ・・/summary>
        public float PointerMovePixelThreshold { get; set; } = 1.0f;

        /// <summary>繝昴う繝ｳ繧ｿ繝ｼ遘ｻ蜍輔う繝吶Φ繝医・譛蟆冗匱轣ｫ髢馴囈・育ｧ抵ｼ峨・縺ｧ豈弱ヵ繝ｬ繝ｼ繝</summary>
        public float PointerMoveSampleInterval { get; set; } = 0.02f;

        /// <summary>繝昴う繝ｳ繧ｿ繝ｼ繝懊ち繝ｳ謚ｼ荳区凾縺ｯ遘ｻ蜍暮㍼/髢馴囈繧堤┌隕悶＠縺ｦ蜷梧悄縺吶ｋ</summary>
        public bool ForcePointerSyncOnPress { get; set; } = true;
    }
}
