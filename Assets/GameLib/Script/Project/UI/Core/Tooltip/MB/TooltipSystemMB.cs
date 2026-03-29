#nullable enable
using Game;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.UI
{
    [DisallowMultipleComponent]
    public sealed class TooltipSystemMB : MonoBehaviour, IFeatureInstaller
    {
        const string RootsGroup = "Roots";
        const string InputGroup = "Input";
        const string ClampGroup = "Clamp";
        const string SpawnGroup = "Spawn";
        const string DefaultsGroup = "Shared Defaults";

        [BoxGroup(RootsGroup)]
        [LabelText("Tooltip Root")]
        [Required]
        [Tooltip("UI space tooltip の既定 spawn parent です。TooltipChannelHub が個別 override しない場合はここを使います。")]
        [SerializeField]
        RectTransform tooltipRoot = null!;

        [BoxGroup(RootsGroup)]
        [LabelText("World Root")]
        [Tooltip("World space tooltip の既定 spawn parent です。未指定なら spawner 側の既定 root を使います。")]
        [SerializeField]
        Transform? worldRoot;

        [BoxGroup(RootsGroup)]
        [LabelText("Clamp Area")]
        [Tooltip("UI tooltip の既定 clamp 領域です。未指定時は screen 全体を使います。")]
        [SerializeField]
        RectTransform? clampArea;

        [BoxGroup(InputGroup)]
        [LabelText("Input Mode")]
        [Tooltip("Tooltip 共通の既定 input mode です。hub 側が AutoByInputService の場合の fallback として使います。")]
        [SerializeField]
        TooltipChannelInputMode inputMode = TooltipChannelInputMode.PointerNavigation;

        [BoxGroup(ClampGroup)]
        [LabelText("Enable Clamp")]
        [Tooltip("Tooltip 共通の既定 clamp 設定です。")]
        [SerializeField]
        bool enableClamp = true;

        [BoxGroup(ClampGroup)]
        [ShowIf(nameof(enableClamp))]
        [LabelText("Flip Threshold X")]
        [MinValue(0d)]
        [Tooltip("左右 overflow 比率がこの値を超えたら X anchor を反転する既定値です。")]
        [SerializeField]
        float flipThresholdX = 0.2f;

        [BoxGroup(ClampGroup)]
        [ShowIf(nameof(enableClamp))]
        [LabelText("Flip Threshold Y")]
        [MinValue(0d)]
        [Tooltip("上下 overflow 比率がこの値を超えたら Y anchor を反転する既定値です。")]
        [SerializeField]
        float flipThresholdY = 0.2f;

        [BoxGroup(SpawnGroup)]
        [LabelText("Spawn Warmup Frames")]
        [MinValue(0)]
        [Tooltip("Tooltip 共通の既定 warmup frame 数です。hub 側が 0 のときの fallback に使います。")]
        [SerializeField]
        int spawnWarmupFrames = 2;

        [BoxGroup(DefaultsGroup)]
        [LabelText("Defaults")]
        [InlineProperty]
        [SerializeField]
        TooltipSystemSharedDefaults sharedDefaults = new();

        public void InstallFeature(IContainerBuilder builder, IScopeNode owner)
        {
            var resolvedTooltipRoot = tooltipRoot != null ? tooltipRoot : GetComponent<RectTransform>();
            if (resolvedTooltipRoot == null)
            {
                Debug.LogError("[TooltipSystemMB] Tooltip Root is missing. Please assign a RectTransform.");
                return;
            }

            var config = new TooltipSystemConfig
            {
                TooltipRoot = resolvedTooltipRoot,
                WorldRoot = worldRoot,
                ClampArea = clampArea,
                InputMode = inputMode,
                ClampSettings = new TooltipClampSettings(enableClamp, flipThresholdX, flipThresholdY),
                SpawnWarmupFrames = spawnWarmupFrames,
                SharedDefaults = sharedDefaults != null ? sharedDefaults.CreateRuntimeCopy() : new TooltipSystemSharedDefaults(),
            };

            builder.RegisterInstance(config);
            builder.Register<ScreenClampService>(Lifetime.Singleton)
                .As<IScreenClampService>();
            builder.Register<TooltipSystemService>(Lifetime.Singleton)
                .As<ITooltipSystemService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();
        }

        void Reset()
        {
            if (tooltipRoot == null)
                tooltipRoot = GetComponent<RectTransform>();
            if (sharedDefaults == null)
                sharedDefaults = new TooltipSystemSharedDefaults();
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (tooltipRoot == null)
                tooltipRoot = GetComponent<RectTransform>();
            if (flipThresholdX < 0f)
                flipThresholdX = 0f;
            if (flipThresholdY < 0f)
                flipThresholdY = 0f;
            if (spawnWarmupFrames < 0)
                spawnWarmupFrames = 0;
            if (sharedDefaults == null)
                sharedDefaults = new TooltipSystemSharedDefaults();
        }
#endif
    }
}
