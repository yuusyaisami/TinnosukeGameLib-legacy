#nullable enable
using UnityEngine;
using Sirenix.OdinInspector;
using VContainer;
using VContainer.Unity;
using Game;

namespace Game.UI
{
    [DisallowMultipleComponent]
    public sealed class TooltipSystemMB : MonoBehaviour, IFeatureInstaller
    {
        const string RootGroup = "Roots";
        const string InputGroup = "Input";
        const string ClampGroup = "Clamp";
        const string CameraGroup = "Cameras";
        const string OptionsGroup = "Options";

        [BoxGroup(RootGroup)]
        [LabelText("Tooltip Root")]
        [Required]
        [SerializeField] RectTransform tooltipRoot = null!;

        [BoxGroup(RootGroup)]
        [LabelText("World Root (Optional)")]
        [SerializeField] Transform? worldRoot;

        [BoxGroup(ClampGroup)]
        [LabelText("Clamp Area (Optional)")]
        [SerializeField] RectTransform? clampArea;

        [BoxGroup(InputGroup)]
        [SerializeField] TooltipInputMode inputMode = TooltipInputMode.PointerNavigation;

        [BoxGroup(ClampGroup)]
        [LabelText("Enable Clamp")]
        [SerializeField] bool enableClamp = true;

        [BoxGroup(ClampGroup)]
        [ShowIf("enableClamp")]
        [Min(0f)]
        [SerializeField] float flipThresholdX = 0.2f;

        [BoxGroup(ClampGroup)]
        [ShowIf("enableClamp")]
        [Min(0f)]
        [SerializeField] float flipThresholdY = 0.2f;

        [BoxGroup(CameraGroup)]
        [LabelText("UI Camera")]
        [SerializeField] Camera? uiCamera;

        [BoxGroup(CameraGroup)]
        [LabelText("World Camera")]
        [SerializeField] Camera? worldCamera;

        [BoxGroup(OptionsGroup)]
        [SerializeField] bool runInLateUpdate = true;

        [BoxGroup(OptionsGroup)]
        [LabelText("Spawn Warmup Frames")]
        [MinValue(0)]
        [SerializeField] int spawnWarmupFrames = 2;

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
                UiCamera = uiCamera,
                WorldCamera = worldCamera,
                InputMode = inputMode,
                ClampSettings = new TooltipClampSettings(enableClamp, flipThresholdX, flipThresholdY),
                RunInLateUpdate = runInLateUpdate,
                SpawnWarmupFrames = spawnWarmupFrames,
            };

            builder.RegisterInstance(config);

            builder.Register<ScreenClampService>(Lifetime.Singleton)
                .As<IScreenClampService>();

            var registration = builder.Register<TooltipSystemService>(Lifetime.Singleton)
                .As<ITooltipSystemService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();

            if (runInLateUpdate)
                registration.As<ILateTickable>();
            else
                registration.As<ITickable>();
        }

        void Reset()
        {
            if (tooltipRoot == null)
                tooltipRoot = GetComponent<RectTransform>();
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
        }
#endif
    }
}
