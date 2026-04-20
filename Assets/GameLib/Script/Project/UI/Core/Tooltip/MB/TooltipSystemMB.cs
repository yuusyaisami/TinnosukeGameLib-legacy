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
        [Tooltip("Inspector setting.")]
        [SerializeField]
        RectTransform tooltipRoot = null!;

        [BoxGroup(RootsGroup)]
        [LabelText("World Root")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        Transform? worldRoot;

        [BoxGroup(RootsGroup)]
        [LabelText("Clamp Area")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        RectTransform? clampArea;

        [BoxGroup(InputGroup)]
        [LabelText("Input Mode")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        TooltipChannelInputMode inputMode = TooltipChannelInputMode.PointerNavigation;

        [BoxGroup(ClampGroup)]
        [LabelText("Enable Clamp")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        bool enableClamp = true;

        [BoxGroup(ClampGroup)]
        [ShowIf(nameof(enableClamp))]
        [LabelText("Flip Threshold X")]
        [MinValue(0d)]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        float flipThresholdX = 0.2f;

        [BoxGroup(ClampGroup)]
        [ShowIf(nameof(enableClamp))]
        [LabelText("Flip Threshold Y")]
        [MinValue(0d)]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        float flipThresholdY = 0.2f;

        [BoxGroup(SpawnGroup)]
        [LabelText("Spawn Warmup Frames")]
        [MinValue(0)]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        int spawnWarmupFrames = 2;

        [BoxGroup(DefaultsGroup)]
        [LabelText("Defaults")]
        [InlineProperty]
        [SerializeField]
        TooltipSystemSharedDefaults sharedDefaults = new();

        public void InstallFeature(IRuntimeContainerBuilder builder, IScopeNode owner)
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
                SharedHubPreset = sharedDefaults != null ? sharedDefaults.HubPreset.CreateRuntimeCopy() : new TooltipHubPreset(),
            };

            builder.RegisterInstance(config);
            builder.Register<ScreenClampService>(RuntimeLifetime.Singleton)
                .As<IScreenClampService>();
            builder.Register<TooltipSystemService>(RuntimeLifetime.Singleton)
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
