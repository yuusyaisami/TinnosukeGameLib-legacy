#nullable enable
using UnityEngine;
using VContainer;
using System.Collections.Generic;
using System;
using Game.Common;
using VNext = Game.Commands.VNext;

namespace Game.UI
{
    // ================================================================
    // UIElementStateMB - UIElement縺ｮ迥ｶ諷玖ｨｭ螳夂畑MonoBehaviour
    // ================================================================
    //
    // ## 讎りｦ・
    //
    // UIElementStateMB縺ｯ縲ゞIElement縺ｮ迥ｶ諷九→險ｭ螳壹ｒ邂｡逅・☆繧貴onoBehaviour縲・
    // IScopeNode・・aseLifetimeScope/KernelScopeHost・蛾・荳九〒蜍穂ｽ懊＠縲∽ｻ･荳九・讖溯・繧呈署萓帙☆繧・
    //
    // 1. **Inspector險ｭ螳・*: Active/Visible縲∝ｽ薙◆繧雁愛螳壹√リ繝薙ご繝ｼ繧ｷ繝ｧ繝ｳ險ｭ螳・
    // 2. **DI繧ｳ繝ｳ繝・リ逋ｻ骭ｲ**: UIElementStateService繧偵せ繧ｳ繝ｼ繝励↓逋ｻ骭ｲ
    // 3. **螳溯｡梧凾險ｭ螳壼､画峩**: Inspector縺ｮ螟画峩繧担ervice縺ｫ蜿肴丐
    //
    // ## 蠖薙◆繧雁愛螳啌ectTransform縺ｫ縺､縺・※
    //
    // 繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ繧・・繧､繝ｳ繧ｿ繝ｼ驕ｸ謚樊凾縺ｫ縲√％縺ｮUIElement縺ｮ迚ｩ逅・噪縺ｪ鬆伜沺繧・
    // 螳夂ｾｩ縺吶ｋ縺溘ａ縺ｫ菴ｿ逕ｨ縺輔ｌ繧九・
    //
    // ### 繝・ヵ繧ｩ繝ｫ繝亥虚菴・
    //
    // 縺薙・MB縺後い繧ｿ繝・メ縺輔ｌ縺檬ameObject縺ｫRectTransform縺後≠繧句ｴ蜷医・
    // 閾ｪ蜍慕噪縺ｫ縺昴ｌ縺悟ｽ薙◆繧雁愛螳壹→縺励※險ｭ螳壹＆繧後ｋ縲・
    //
    // ### 隍・焚險ｭ螳・
    //
    // 隍・尅縺ｪ蠖｢迥ｶ縺ｮUIElement・・蟄怜梛縺ｪ縺ｩ・峨〒縺ｯ縲∬､・焚縺ｮRectTransform繧・
    // 險ｭ螳壹＠縺ｦ蠖薙◆繧雁愛螳夐伜沺繧呈ｧ区・縺ｧ縺阪ｋ縲・
    //
    // ## 繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ險ｭ螳壹↓縺､縺・※
    //
    // ### IsNavigationSelectable
    //
    // false縺ｫ險ｭ螳壹☆繧九→縲√く繝ｼ繝懊・繝・繧ｲ繝ｼ繝繝代ャ繝峨↓繧医ｋ繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ縺ｧ
    /// 縺薙・UIElement縺ｯ驕ｸ謚槫呵｣懊°繧蛾勁螟悶＆繧後ｋ縲・
    ///
    /// 逕ｨ騾・ Page縲仝indow縲￣anel遲峨・繧ｳ繝ｳ繝・リ隕∫ｴ
    ///
    /// ### NavigationOverride
    ///
    /// 蜷・婿蜷托ｼ井ｸ贋ｸ句ｷｦ蜿ｳ・峨・遘ｻ蜍募・繧呈・遉ｺ逧・↓謖・ｮ壹〒縺阪ｋ縲・
    /// 險ｭ螳壹＆繧後※縺・↑縺・婿蜷代・閾ｪ蜍戊ｨ育ｮ励＆繧後ｋ縲・
    ///
    // ================================================================

    public interface IUIElementStateOptions
    {
        IReadOnlyList<RectTransform> HitTestRects { get; }
        int SelectionOrder { get; }
        int NavigationSelectionOrder { get; }
        Game.Common.DynamicValue<bool> IsSelectable { get; }
        Game.Common.DynamicValue<bool> IsNavigationSelectable { get; }
        NavigationOverride? NavigationOverride { get; }
        VNext.CommandListData OnSelectedCommands { get; }
        VNext.CommandListData OnDeselectedCommands { get; }
    }

    /// <summary>
    /// UIElement縺ｮ迥ｶ諷玖ｨｭ螳夂畑MonoBehaviour縲・
    /// 
    /// ## RequireComponent
    /// 
    /// UIElementLifetimeScope縺ｫRequireComponent縺ｧ蠑ｷ蛻ｶ縺輔ｌ繧九′縲・
    /// RuntimeLifetimeScope驟堺ｸ九〒繧ょ酔讒倥↓蜍穂ｽ懊☆繧九・
    /// 
    /// ## IScopeInstaller
    /// 
    /// 縺薙・MB縺ｯIFeatureInstaller繧貞ｮ溯｣・＠縲・
    /// UIElementStateService繧奪I繧ｳ繝ｳ繝・リ縺ｫ逋ｻ骭ｲ縺吶ｋ縲・
    /// </summary>
    public sealed class UIElementStateMB : MonoBehaviour, IScopeInstaller, IUIElementStateOptions, IScopeAcquireHandler, IScopeReleaseHandler
    {
        // ================================================================
        // Inspector險ｭ螳・- 蝓ｺ譛ｬ迥ｶ諷・
        // ================================================================

        [Header("Inspector")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        bool _initialActive = true;

        [Tooltip("Inspector setting.")]
        [SerializeField]
        bool _initialVisible = true;

        // ================================================================
        // Inspector險ｭ螳・- 蠖薙◆繧雁愛螳・
        // ================================================================

        [Header("Inspector")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        List<RectTransform> _hitTestRects = new();

        [Tooltip("Inspector setting.")]
        [SerializeField]
        int _selectionOrder = 0;

        [Tooltip("Inspector setting.")]
        [SerializeField]
        int _navigationSelectionOrder = 0;
        // ================================================================
        // Inspector險ｭ螳・- 繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ
        // ================================================================

        [Header("Inspector")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        [DynamicValueDefaultLiteral(true)]
        Game.Common.DynamicValue<bool> _isSelectable = new Game.Common.DynamicValue<bool>();

        [Tooltip("Inspector setting.")]
        [SerializeField]
        [DynamicValueDefaultLiteral(true)]
        Game.Common.DynamicValue<bool> _isNavigationSelectable = new Game.Common.DynamicValue<bool>();

        [Tooltip("Inspector setting.")]
        [SerializeField]
        NavigationOverride? _navigationOverride;


        // ================================================================
        // Inspector險ｭ螳・- 驕ｸ謚槭う繝吶Φ繝医さ繝槭Φ繝・
        // ================================================================

        [Header("Inspector")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        [VNext.CommandListFunctionName("UIElementState.OnSelected")]
        VNext.CommandListData _onSelectedCommands = new();

        [Tooltip("Inspector setting.")]
        [SerializeField]
        [VNext.CommandListFunctionName("UIElementState.OnDeselected")]
        VNext.CommandListData _onDeselectedCommands = new();

        // ================================================================
        // 繧ｭ繝｣繝・す繝･
        // ================================================================

        /// <summary>逋ｻ骭ｲ縺輔ｌ縺欖ervice・医Λ繝ｳ繧ｿ繧､繝蜿ら・逕ｨ・・/summary>
        UIElementStateService? _service;

        /// <summary>謇譛芽・せ繧ｳ繝ｼ繝・/summary>
        IScopeNode? _ownerScope;

        /// <summary>VarStore 繧ｭ繝｣繝・す繝･</summary>
        VarStore? _varStore;

        // ================================================================
        // 繝励Ο繝代ユ繧｣
        // ================================================================

        /// <summary>
        /// 蠖薙◆繧雁愛螳夂畑RectTransform縺ｮ繝ｪ繧ｹ繝医・
        /// </summary>
        public IReadOnlyList<RectTransform> HitTestRects => _hitTestRects;

        /// <summary>
        /// 驕ｸ謚樣・ｺ上・
        /// </summary>
        public int SelectionOrder => _selectionOrder;

        /// <summary>
        /// 繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ蟆ら畑縺ｮ蜆ｪ蜈亥ｺｦ縲・
        /// </summary>
        public int NavigationSelectionOrder => _navigationSelectionOrder;

        /// <summary>
        /// 縺薙・UIElement閾ｪ菴薙′驕ｸ謚槫ｯｾ雎｡縺ｫ縺ｪ繧後ｋ縺九ｒ豎ｺ繧√ｋ譚｡莉ｶ縲・
        /// </summary>
        public Game.Common.DynamicValue<bool> IsSelectable => _isSelectable;

        /// <summary>
        /// 繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ縺ｧ驕ｸ謚槫庄閭ｽ縺九ｒ豎ｺ繧√ｋ譚｡莉ｶ・・ynamicValue<bool>・峨・
        /// </summary>
        public Game.Common.DynamicValue<bool> IsNavigationSelectable => _isNavigationSelectable;

        /// <summary>
        /// 繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ譁ｹ蜷代・繧ｪ繝ｼ繝舌・繝ｩ繧､繝芽ｨｭ螳壹・
        /// </summary>
        public NavigationOverride? NavigationOverride => _navigationOverride;

        /// <summary>
        /// 驕ｸ謚樊凾繧ｳ繝槭Φ繝峨Μ繧ｹ繝医・
        /// </summary>
        public VNext.CommandListData OnSelectedCommands => _onSelectedCommands;

        /// <summary>
        /// 驕ｸ謚櫁ｧ｣髯､譎ゅさ繝槭Φ繝峨Μ繧ｹ繝医・
        /// </summary>
        public VNext.CommandListData OnDeselectedCommands => _onDeselectedCommands;

        // ================================================================
        // IFeatureInstaller螳溯｣・
        // ================================================================

        /// <summary>
        /// UIElementStateService繧奪I繧ｳ繝ｳ繝・リ縺ｫ逋ｻ骭ｲ縺吶ｋ縲・
        /// 
        /// ## 蜃ｦ逅・・螳ｹ
        /// 
        /// 1. UIElementStateService繧担ingleton縺ｧ逋ｻ骭ｲ
        /// 2. IUIElementState縺ｨIUIElementStateController縺ｮ荳｡譁ｹ縺ｧ蜈ｬ髢・
        /// 3. BuildCallback蜀・〒Inspector險ｭ螳壹ｒService縺ｫ蜿肴丐
        /// 
        /// ## 蜻ｼ縺ｳ蜃ｺ縺励ち繧､繝溘Φ繧ｰ
        /// 
        /// UIElementLifetimeScope縺ｮConfigure譎ゅ↓蜻ｼ縺ｳ蜃ｺ縺輔ｌ繧九・
        /// </summary>
        public void InstallScopeServices(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            _ownerScope = scope;

            // 蠖薙◆繧雁愛螳夂畑RectTransform縺ｮ繝・ヵ繧ｩ繝ｫ繝郁ｨｭ螳・
            // 繝ｪ繧ｹ繝医′遨ｺ縺ｮ蝣ｴ蜷医√％縺ｮGameObject縺ｮRectTransform繧定・蜍戊ｿｽ蜉
            EnsureDefaultHitTestRect();

            // UIElementStateService繧堤匳骭ｲ
            builder.Register<UIElementStateService>(RuntimeLifetime.Singleton)
                .WithParameter<IScopeNode>(scope)
                .WithParameter<IUIElementStateOptions>(this)
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .As<IUIElementState>()
                .As<IUIElementStateController>()
                .As<IUIModalRoot>();

            // IUIInputConsumerHub繧堤匳骭ｲ
            // 蜷ЁeatureInstaller・医・繧ｿ繝ｳ縲√せ繧ｯ繝ｭ繝ｼ繝ｫ遲会ｼ峨・縺薙・Hub縺ｫConsumer繧堤匳骭ｲ縺吶ｋ
            builder.Register<UIInputConsumerHub>(RuntimeLifetime.Singleton)
                .As<IUIInputConsumerHub>();

            // Apply initial state and inspector settings to the created UIElementStateService
            builder.RegisterBuildCallback(container =>
            {
                if (container.TryResolve<UIElementStateService>(out var service))
                {
                    _service = service;  // _service 繧偵く繝｣繝・す繝･
                    service.SetHitTestRects(_hitTestRects);
                    service.SetSelectionOrder(_selectionOrder);
                    service.SetNavigationSelectionOrder(_navigationSelectionOrder);
                    service.SetSelectableCondition(_isSelectable);
                    service.SetNavigationSelectableCondition(_isNavigationSelectable);
                    service.SetNavigationOverride(_navigationOverride);
                    service.OnSelectedCommands.SetCommands(_onSelectedCommands);
                    service.OnDeselectedCommands.SetCommands(_onDeselectedCommands);
                    service.SetActive(_initialActive);
                    service.SetVisible(_initialVisible);
                }
            });
        }

        // ================================================================
        // MonoBehaviour繝ｩ繧､繝輔し繧､繧ｯ繝ｫ
        // ================================================================
        void Awake()
        {
            BindDebugOwners();
        }

        /// <summary>
        /// Reset縺ｯ繧ｨ繝・ぅ繧ｿ縺ｧ繧ｳ繝ｳ繝昴・繝阪Φ繝郁ｿｽ蜉譎ゅ↓蜻ｼ縺ｰ繧後ｋ縲・
        /// 繝・ヵ繧ｩ繝ｫ繝医〒閾ｪ霄ｫ縺ｮRectTransform繧貞ｽ薙◆繧雁愛螳壹↓霑ｽ蜉縺吶ｋ縲・
        /// </summary>
        void Reset()
        {
            EnsureDefaultHitTestRect();
        }

        /// <summary>
        /// OnValidate縺ｯInspector蛟､螟画峩譎ゅ↓蜻ｼ縺ｰ繧後ｋ・医お繝・ぅ繧ｿ縺ｮ縺ｿ・峨・
        /// 螳溯｡梧凾縺ｫService縺悟ｭ伜惠縺吶ｌ縺ｰ險ｭ螳壹ｒ蜿肴丐縺吶ｋ縲・
        /// </summary>
        void OnValidate()
        {
            BindDebugOwners();
            // 螳溯｡梧凾縺ｮ縺ｿ
            if (!Application.isPlaying) return;

            // Service縺檎匳骭ｲ貂医∩縺ｪ繧芽ｨｭ螳壹ｒ蜿肴丐
            if (_service != null)
            {
                // OnValidate縺ｧ縺ｯ驕ｸ謚樒屮隕悶・蛻晄悄蛹悶・陦後ｏ縺ｪ縺・ｼ域里縺ｫ蛻晄悄蛹匁ｸ医∩・・
                ApplyInspectorSettingsWithoutInitialize(_service);
            }
        }

        // ================================================================
        // IScopeAcquireHandler / IScopeReleaseHandler螳溯｣・
        // ================================================================

        /// <summary>
        /// 繧ｹ繧ｳ繝ｼ繝礼佐蠕玲凾縺ｮ蛻晄悄蛹門・逅・・
        /// VarStore 縺九ｉ IsNavigationSelectable 縺ｮ螟画峩繧定ｳｼ隱ｭ縺吶ｋ縲・
        /// </summary>
        void IScopeAcquireHandler.OnAcquire(IScopeNode scope, bool isReset)
        {
            // VarStore 繧貞叙蠕暦ｼ医Ο繝ｼ繧ｫ繝ｫ縺九ｉ讀懃ｴ｢髢句ｧ具ｼ・
            if (_varStore == null && scope?.Resolver != null)
            {
                scope.Resolver.TryResolve<VarStore>(out _varStore);
            }
        }

        /// <summary>
        /// 繧ｹ繧ｳ繝ｼ繝苓ｧ｣謾ｾ譎ゅ・繧ｯ繝ｪ繝ｼ繝ｳ繧｢繝・・蜃ｦ逅・・
        /// </summary>
        void IScopeReleaseHandler.OnRelease(IScopeNode scope, bool isReset)
        {
            // VarStore 縺ｸ縺ｮ雉ｼ隱ｭ縺ｯ荳崎ｦ・ｼ・arStore 蛛ｴ縺ｧ蜿ら・縺瑚ｧ｣謾ｾ縺輔ｌ繧具ｼ・
            _varStore = null;
        }

        // ================================================================
        // 蜀・Κ繝｡繧ｽ繝・ラ
        // ================================================================

        /// <summary>
        /// 繝・ヵ繧ｩ繝ｫ繝医・蠖薙◆繧雁愛螳啌ectTransform繧定ｨｭ螳壹☆繧九・
        /// 
        /// ## 蜃ｦ逅・
        /// 
        /// _hitTestRects縺檎ｩｺ縺ｮ蝣ｴ蜷医∬・霄ｫ縺ｮRectTransform繧定ｿｽ蜉縺吶ｋ縲・
        /// </summary>
        void EnsureDefaultHitTestRect()
        {
            if (_hitTestRects.Count > 0) return;

            var rt = GetComponent<RectTransform>();
            if (rt != null)
            {
                _hitTestRects.Add(rt);
            }
        }

        void BindDebugOwners()
        {
            _onSelectedCommands?.BindDebugOwner(this, nameof(_onSelectedCommands));
            _onDeselectedCommands?.BindDebugOwner(this, nameof(_onDeselectedCommands));
        }


        /// <summary>
        /// Inspector險ｭ螳壹ｒService縺ｫ蜿肴丐縺吶ｋ・亥・譛溷喧縺ｪ縺暦ｼ峨・
        /// OnValidate縺九ｉ蜻ｼ縺ｰ繧後ｋ縲・
        /// </summary>
        void ApplyInspectorSettingsWithoutInitialize(UIElementStateService service)
        {
            // 蠖薙◆繧雁愛螳・
            service.SetHitTestRects(_hitTestRects);

            // 繝翫ン繧ｲ繝ｼ繧ｷ繝ｧ繝ｳ險ｭ螳・
            service.SetSelectionOrder(_selectionOrder);
            service.SetNavigationSelectionOrder(_navigationSelectionOrder);
            service.SetSelectableCondition(_isSelectable);
            service.SetNavigationSelectableCondition(_isNavigationSelectable);
            service.SetNavigationOverride(_navigationOverride);

            // 驕ｸ謚槭う繝吶Φ繝医さ繝槭Φ繝・
            service.OnSelectedCommands.SetCommands(_onSelectedCommands);
            service.OnDeselectedCommands.SetCommands(_onDeselectedCommands);
        }
    }
}



