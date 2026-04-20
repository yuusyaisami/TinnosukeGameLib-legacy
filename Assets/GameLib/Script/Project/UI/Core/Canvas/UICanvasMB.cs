#nullable enable
using UnityEngine;
using VContainer;

namespace Game.UI
{
    // ================================================================
    // UICanvasMB - UI繧ｭ繝｣繝ｳ繝舌せ險ｭ螳夂畑MonoBehaviour
    // ================================================================
    //
    // ## 讎りｦ・
    //
    // UICanvasMB縺ｯ縲ゞI縺ｮ繧ｭ繝｣繝ｳ繝舌せ險ｭ螳壹ｒ邂｡逅・☆繧貴onoBehaviour縲・
    // 騾壼ｸｸ縲ゞI繧ｷ繧ｹ繝・Β縺ｮ繝ｫ繝ｼ繝医→縺ｪ繧偽IElement縺ｫ繧｢繧ｿ繝・メ縺輔ｌ繧九・
    //
    // ## 荳ｻ縺ｪ蠖ｹ蜑ｲ
    //
    // 1. **Canvas蜿ら・縺ｮ謠蝉ｾ・*: UI繧ｷ繧ｹ繝・Β縺靴anvas諠・ｱ縺ｫ繧｢繧ｯ繧ｻ繧ｹ縺吶ｋ縺溘ａ縺ｮ遯灘哨
    // 2. **繧ｭ繝｣繝ｳ繝舌せ繧ｿ繧､繝励・蛻､螳・*: Screen/World Canvas縺ｮ閾ｪ蜍募愛螳・
    // 3. **蠎ｧ讓吝､画鋤縺ｮ繧ｵ繝昴・繝・*: Screen蠎ｧ讓吮∑Local蠎ｧ讓吶・螟画鋤
    // 4. **蟆・擂縺ｮ諡｡蠑ｵ繝昴う繝ｳ繝・*: UI蜈ｨ菴薙・險ｭ螳壹ｒ霑ｽ蜉縺吶ｋ蝣ｴ謇
    //
    // ## 險ｭ險域婿驥・
    //
    // 縺薙・繧ｳ繝ｳ繝昴・繝阪Φ繝医・譛蟆城剞縺ｮ讖溯・縺九ｉ蟋九ａ縲・
    // 蠢・ｦ√↓蠢懊§縺ｦ繝輔ぅ繝ｼ繝ｫ繝峨ｒ霑ｽ蜉縺励※縺・￥縲・
    //
    // 迴ｾ蝨ｨ縺ｯ莉･荳九・縺ｿ繧堤ｮ｡逅・
    // - Canvas蜿ら・
    // - 繧ｭ繝｣繝ｳ繝舌せ繧ｿ繧､繝暦ｼ郁・蜍募愛螳夲ｼ・
    //
    // 蟆・擂霑ｽ蜉莠亥ｮ壹・險ｭ螳壻ｾ・
    // - 繝・ヵ繧ｩ繝ｫ繝医・繧｢繝九Γ繝ｼ繧ｷ繝ｧ繝ｳ險ｭ螳・
    // - 繧ｵ繧ｦ繝ｳ繝芽ｨｭ螳・
    // - 繝・・繝・繧ｹ繧ｭ繝ｳ險ｭ螳・
    // - 繝ｬ繧､繧｢繧ｦ繝郁ｨｭ螳・
    //
    // ================================================================

    /// <summary>
    /// UI繧ｭ繝｣繝ｳ繝舌せ險ｭ螳夂畑MonoBehaviour縲・
    /// 
    /// ## 菴ｿ逕ｨ譁ｹ豕・
    /// 
    /// 1. UI繧ｷ繧ｹ繝・Β縺ｮ繝ｫ繝ｼ繝・IElement縺ｫ繧｢繧ｿ繝・メ
    /// 2. Canvas蜿ら・繧定ｨｭ螳夲ｼ医∪縺溘・閾ｪ蜍募叙蠕暦ｼ・
    /// 3. IFeatureInstaller縺ｨ縺励※UICanvasService繧堤匳骭ｲ
    /// 
    /// ## 閾ｪ蜍募叙蠕・
    /// 
    /// Canvas縺瑚ｨｭ螳壹＆繧後※縺・↑縺・ｴ蜷医∬ｦｪ髫主ｱ､縺九ｉCanvas繧定・蜍募叙蠕励☆繧九・
    /// </summary>
    public sealed class UICanvasMB : MonoBehaviour, IFeatureInstaller
    {
        // ================================================================
        // Inspector險ｭ螳・
        // ================================================================

        [Header("Inspector")]
        // [Tooltip("Inspector setting.")]
        [SerializeField]
        Canvas? _canvas;

        [Header("繝・ヰ繝・げ")]
        // [Tooltip("Inspector setting.")]
        [SerializeField]
        bool _logOnStart = false;

        // ================================================================
        // 蟆・擂縺ｮ諡｡蠑ｵ逕ｨ繝輔ぅ繝ｼ繝ｫ繝会ｼ医さ繝｡繝ｳ繝医〒莠育ｴ・ｼ・
        // ================================================================

        // [Header("繧｢繝九Γ繝ｼ繧ｷ繝ｧ繝ｳ險ｭ螳・)]
        // [Tooltip("Inspector setting.")]
        // [SerializeField]
        // UIAnimationSettings? _defaultAnimationSettings;

        // [Header("繧ｵ繧ｦ繝ｳ繝芽ｨｭ螳・)]
        // [Tooltip("Inspector setting.")]
        // [SerializeField]
        // UISoundSettings? _defaultSoundSettings;

        // [Header("繝・・繝櫁ｨｭ螳・)]
        // [Tooltip("Inspector setting.")]
        // [SerializeField]
        // UIThemeSettings? _themeSettings;

        // ================================================================
        // 繧ｭ繝｣繝・す繝･
        // ================================================================

        /// <summary>逋ｻ骭ｲ縺輔ｌ縺欖ervice</summary>
        UICanvasServiceCore? _service;

        /// <summary>隗｣豎ｺ貂医∩縺ｮCanvas</summary>
        Canvas? _resolvedCanvas;

        // ================================================================
        // 繝励Ο繝代ユ繧｣
        // ================================================================

        /// <summary>
        /// 縺薙・MB縺檎ｮ｡逅・☆繧気anvas縲・
        /// Inspector險ｭ螳壹∪縺溘・閾ｪ蜍募叙蠕励＆繧後◆繧ゅ・縲・
        /// </summary>
        public Canvas? Canvas
        {
            get
            {
                if (_resolvedCanvas == null)
                {
                    _resolvedCanvas = ResolveCanvas();
                }
                return _resolvedCanvas;
            }
        }

        /// <summary>
        /// 繧ｭ繝｣繝ｳ繝舌せ縺ｮ遞ｮ鬘橸ｼ郁・蜍募愛螳夲ｼ峨・
        /// </summary>
        public UICanvasType CanvasType
        {
            get
            {
                var canvas = Canvas;
                if (canvas == null) return UICanvasType.ScreenOverlay;

                return canvas.renderMode switch
                {
                    RenderMode.ScreenSpaceOverlay => UICanvasType.ScreenOverlay,
                    RenderMode.ScreenSpaceCamera => UICanvasType.ScreenCamera,
                    RenderMode.WorldSpace => UICanvasType.World,
                    _ => UICanvasType.ScreenOverlay
                };
            }
        }

        // ================================================================
        // IFeatureInstaller螳溯｣・
        // ================================================================

        /// <summary>
        /// UICanvasServiceCore繧奪I繧ｳ繝ｳ繝・リ縺ｫ逋ｻ骭ｲ縺吶ｋ縲・
        /// 
        /// ## 蜃ｦ逅・・螳ｹ
        /// 
        /// 1. Canvas繧定ｧ｣豎ｺ・・nspector險ｭ螳壹∪縺溘・閾ｪ蜍募叙蠕暦ｼ・
        /// 2. UICanvasServiceCore繧担ingleton縺ｧ逋ｻ骭ｲ
        /// 3. IUICanvasService縺ｨ縺励※蜈ｬ髢・
        /// </summary>
        public void InstallFeature(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            // Canvas繧定ｧ｣豎ｺ
            _resolvedCanvas = ResolveCanvas();

            if (_resolvedCanvas == null)
            {
                Debug.LogWarning($"[UICanvasMB] '{name}' could not find Canvas. " +
                               "Please set Canvas in Inspector or ensure parent has Canvas.");
            }

            // UICanvasServiceCore繧堤匳骭ｲ
            builder.Register<UICanvasServiceCore>(RuntimeLifetime.Singleton)
                .WithParameter(typeof(Canvas), _resolvedCanvas)
                .As<IUICanvasService>();

            // CandidateProvider逋ｻ骭ｲ・・orld/Screen荳｡蟇ｾ蠢懶ｼ・
            builder.Register<ISelectCandidateProvider>(c =>
            {
                var cs = c.Resolve<IUICanvasService>();
                return CanvasType == UICanvasType.World
                    ? new SelectCandidateProviderWorld(cs)
                    : new SelectCandidateProviderScreen(cs);
            }, RuntimeLifetime.Singleton)
            .As<ISelectCandidateProvider>();

            // Ensure the service gets the provider injected when built
            builder.RegisterBuildCallback(container =>
            {
                if (container.TryResolve<UICanvasServiceCore>(out var s) && container.TryResolve<ISelectCandidateProvider>(out var p))
                {
                    s.SetCandidateProvider(p);
                }
            });

            // BuildCallback蜀・〒Service繧偵く繝｣繝・す繝･縲繝ｼ縲閾ｪ霄ｫ縺後ン繝ｫ繝峨＆繧後◆縺ｨ縺阪↓閾ｪ霄ｫ縺ｮ遽・峇蜀・〒蜻ｼ縺ｳ蜃ｺ縺・
            builder.RegisterBuildCallback(container =>
            {
                if (container.TryResolve<UICanvasServiceCore>(out var service))
                {
                    _service = service;

                    if (_logOnStart)
                    {
                        LogCanvasInfo();
                    }
                }
            });
        }

        // ================================================================
        // MonoBehaviour繝ｩ繧､繝輔し繧､繧ｯ繝ｫ
        // ================================================================

        void Reset()
        {
            // 繧ｨ繝・ぅ繧ｿ縺ｧ繧｢繧ｿ繝・メ譎ゅ∬ｦｪ縺ｮCanvas繧定・蜍戊ｨｭ螳・
            _canvas = GetComponentInParent<Canvas>();
        }

        void OnValidate()
        {
            // Inspector螟画峩譎ゅ↓繧ｭ繝｣繝・す繝･繧偵け繝ｪ繧｢
            _resolvedCanvas = null;
        }

        // ================================================================
        // 蜀・Κ繝｡繧ｽ繝・ラ
        // ================================================================

        /// <summary>
        /// Canvas繧定ｧ｣豎ｺ縺吶ｋ縲・
        /// 
        /// ## 隗｣豎ｺ鬆・ｺ・
        /// 
        /// 1. Inspector險ｭ螳壹′縺ゅｌ縺ｰ縺昴ｌ繧剃ｽｿ逕ｨ
        /// 2. 縺ｪ縺代ｌ縺ｰ隕ｪ髫主ｱ､縺九ｉ閾ｪ蜍募叙蠕・
        /// </summary>
        Canvas? ResolveCanvas()
        {
            if (_canvas != null)
            {
                return _canvas;
            }

            return GetComponentInParent<Canvas>();
        }

        /// <summary>
        /// Canvas諠・ｱ繧偵Ο繧ｰ蜃ｺ蜉帙☆繧具ｼ医ョ繝舌ャ繧ｰ逕ｨ・峨・
        /// </summary>
        void LogCanvasInfo()
        {
            var canvas = Canvas;
            if (canvas == null)
            {
                Debug.Log($"[UICanvasMB] '{name}' - No Canvas");
                return;
            }

            Debug.Log($"[UICanvasMB] '{name}' - Canvas: {canvas.name}, " +
                     $"Type: {CanvasType}, " +
                     $"Camera: {canvas.worldCamera?.name ?? "null"}");
        }
    }

    // ================================================================
    // UICanvasServiceCore - 繧ｭ繝｣繝ｳ繝舌せ諠・ｱ繧ｵ繝ｼ繝薙せ・医す繝ｳ繝励Ν迚茨ｼ・
    // ================================================================

    /// <summary>
    /// 繧ｭ繝｣繝ｳ繝舌せ諠・ｱ繧堤ｮ｡逅・☆繧九し繝ｼ繝薙せ縺ｮ繧ｳ繧｢螳溯｣・・
    /// 
    /// ## 蠖ｹ蜑ｲ
    /// 
    /// - Canvas蜿ら・縺ｮ菫晄戟
    /// - 繧ｭ繝｣繝ｳ繝舌せ繧ｿ繧､繝励・蛻､螳・
    /// - 蠎ｧ讓吝､画鋤繝ｦ繝ｼ繝・ぅ繝ｪ繝・ぅ
    /// 
    /// ## 險ｭ險・
    /// 
    /// CandidateProvider髢｢騾｣縺ｯ蛻･繧ｯ繝ｩ繧ｹ・・electCandidateProviderScreen遲会ｼ峨↓蛻・屬縲・
    /// 縺薙・繧ｵ繝ｼ繝薙せ縺ｯ邏皮ｲ九↑Canvas諠・ｱ縺ｮ謠蝉ｾ帙↓蟆ょｿｵ縺吶ｋ縲・
    /// </summary>
    public sealed class UICanvasServiceCore : IUICanvasService
    {
        // ----------------------------------------------------------------
        // 繝輔ぅ繝ｼ繝ｫ繝・
        // ----------------------------------------------------------------

        readonly Canvas? _canvas;
        readonly Camera? _uiCamera;
        readonly UICanvasType _canvasType;

        // 豕ｨ諢・ CandidateProvider縺ｯ蛻･騾碑ｨｭ螳壹＆繧後ｋ
        ISelectCandidateProvider? _candidateProvider;

        // ----------------------------------------------------------------
        // 繝励Ο繝代ユ繧｣
        // ----------------------------------------------------------------

        /// <inheritdoc/>
        public Canvas? Canvas => _canvas;

        /// <inheritdoc/>
        public UICanvasType CanvasType => _canvasType;

        /// <inheritdoc/>
        public Camera? UICamera => _uiCamera;

        /// <inheritdoc/>
        public ISelectCandidateProvider CandidateProvider
        {
            get
            {
                if (_candidateProvider == null)
                {
                    Debug.LogWarning("[UICanvasServiceCore] CandidateProvider not set. " +
                                   "Creating default ScreenCandidateProvider.");
                    _candidateProvider = new SelectCandidateProviderScreen(this);
                }
                return _candidateProvider;
            }
        }

        // ----------------------------------------------------------------
        // 繧ｳ繝ｳ繧ｹ繝医Λ繧ｯ繧ｿ
        // ----------------------------------------------------------------

        /// <summary>
        /// 繧ｳ繝ｳ繧ｹ繝医Λ繧ｯ繧ｿ縲・
        /// </summary>
        /// <param name="canvas">邂｡逅・ｯｾ雎｡縺ｮCanvas</param>
        public UICanvasServiceCore(Canvas? canvas)
        {
            _canvas = canvas;

            if (_canvas != null)
            {
                _canvasType = _canvas.renderMode switch
                {
                    RenderMode.ScreenSpaceOverlay => UICanvasType.ScreenOverlay,
                    RenderMode.ScreenSpaceCamera => UICanvasType.ScreenCamera,
                    RenderMode.WorldSpace => UICanvasType.World,
                    _ => UICanvasType.ScreenOverlay
                };

                _uiCamera = _canvas.worldCamera;
            }
            else
            {
                _canvasType = UICanvasType.ScreenOverlay;
                _uiCamera = null;
            }
        }

        // ----------------------------------------------------------------
        // 險ｭ螳壹Γ繧ｽ繝・ラ
        // ----------------------------------------------------------------

        /// <summary>
        /// CandidateProvider繧定ｨｭ螳壹☆繧九・
        /// 
        /// ## 逕ｨ騾・
        /// 
        /// 螟夜Κ縺九ｉCandidateProvider繧呈ｳｨ蜈･縺吶ｋ蝣ｴ蜷医↓菴ｿ逕ｨ縲・
        /// 萓・ WorldCanvas縺ｧ縺ｯ蟆ら畑縺ｮProvider繧定ｨｭ螳壹☆繧九・
        /// </summary>
        public void SetCandidateProvider(ISelectCandidateProvider? provider)
        {
            _candidateProvider = provider;
        }

        // ----------------------------------------------------------------
        // 蠎ｧ讓吝､画鋤
        // ----------------------------------------------------------------

        /// <inheritdoc/>
        public bool ScreenToLocalPoint(Vector2 screenPosition, out Vector2 localPosition)
        {
            localPosition = Vector2.zero;

            if (_canvas == null)
            {
                return false;
            }

            var rectTransform = _canvas.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                return false;
            }

            var camera = _canvasType == UICanvasType.ScreenOverlay ? null : _uiCamera;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform,
                screenPosition,
                camera,
                out localPosition);
        }

        /// <inheritdoc/>
        public Vector2 LocalToScreenPoint(Vector2 localPosition)
        {
            if (_canvas == null)
            {
                return localPosition;
            }

            var rectTransform = _canvas.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                return localPosition;
            }

            // 繝ｭ繝ｼ繧ｫ繝ｫ蠎ｧ讓吶ｒ繝ｯ繝ｼ繝ｫ繝牙ｺｧ讓吶↓螟画鋤
            var worldPos = rectTransform.TransformPoint(localPosition);

            // 繝ｯ繝ｼ繝ｫ繝牙ｺｧ讓吶ｒ繧ｹ繧ｯ繝ｪ繝ｼ繝ｳ蠎ｧ讓吶↓螟画鋤
            var camera = _canvasType == UICanvasType.ScreenOverlay ? null : _uiCamera;
            if (camera != null)
            {
                return camera.WorldToScreenPoint(worldPos);
            }

            return worldPos;
        }

        /// <inheritdoc/>
        public bool RectContainsScreenPoint(RectTransform rect, Vector2 screenPosition)
        {
            if (rect == null)
            {
                return false;
            }

            var camera = _canvasType == UICanvasType.ScreenOverlay ? null : _uiCamera;
            return RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition, camera);
        }
    }
}
