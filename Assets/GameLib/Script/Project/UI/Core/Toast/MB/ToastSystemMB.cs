#nullable enable
using UnityEngine;
using Sirenix.OdinInspector;
using VContainer;
using Game.Common;
using Game.DI;

namespace Game.UI
{
    [DisallowMultipleComponent]
    public sealed class ToastSystemMB : MonoBehaviour, IScopeInstaller
    {
        const string RootGroup = "Root";
        const string SpawnGroup = "Spawn";
        const string ModeGroup = "Mode";
        const string DirectionGroup = "Direction";
        const string MotionGroup = "Motion";
        const string BoundsGroup = "Bounds";
        const string AnchorGroup = "Anchor";
        const string ChannelGroup = "Transform Channel";
        const string DebugGroup = "Debug";

        [BoxGroup(RootGroup), LabelText("System Tag")]
        [SerializeField] string systemTag = "default";

        [BoxGroup(RootGroup), LabelText("Toast Root"), Required]
        [SerializeField] RectTransform toastRoot = null!;

        [BoxGroup(RootGroup), LabelText("Clamp Area")]
        [SerializeField] RectTransform? clampArea;

        [BoxGroup(SpawnGroup), LabelText("Default Runtime Template"), Required]
        [SerializeField] DynamicValue<BaseRuntimeTemplatePreset> defaultRuntimeTemplate;

        [BoxGroup(SpawnGroup), LabelText("Spawner Tag")]
        [SerializeField] string spawnerTag = string.Empty;

        [BoxGroup(ModeGroup), LabelText("Display Mode")]
        [SerializeField] ToastDisplayMode displayMode = ToastDisplayMode.Queue;

        [BoxGroup(ModeGroup), ShowIf(nameof(IsStackMode)), MinValue(1)]
        [LabelText("Max Visible Count")]
        [SerializeField] int maxVisibleCount = 3;

        [BoxGroup(ModeGroup), LabelText("Auto Close Seconds"), MinValue(0f)]
        [SerializeField] float autoCloseSeconds = 2f;

        [BoxGroup(ModeGroup), ShowIf(nameof(IsQueueMode)), LabelText("Queue Await Mode")]
        [SerializeField] ToastQueueCloseAwaitMode queueCloseAwaitMode = ToastQueueCloseAwaitMode.WaitCloseCommandsAndChannel;

        [BoxGroup(DirectionGroup), LabelText("Show Direction")]
        [SerializeField] ToastDirection showDirection = ToastDirection.Up;

        [BoxGroup(DirectionGroup), LabelText("Close Direction")]
        [SerializeField] ToastDirection closeDirection = ToastDirection.Down;

        [BoxGroup(DirectionGroup), ShowIf(nameof(IsStackMode)), LabelText("Stack Shift Direction")]
        [SerializeField] ToastDirection stackShiftDirection = ToastDirection.Up;

        [BoxGroup(MotionGroup), LabelText("Show Distance Multiplier"), MinValue(0f)]
        [SerializeField] float showDistanceMultiplier = 1f;

        [BoxGroup(MotionGroup), LabelText("Close Distance Multiplier"), MinValue(0f)]
        [SerializeField] float closeDistanceMultiplier = 1f;

        [BoxGroup(MotionGroup), LabelText("Close Distance Multiplier (Stack)"), MinValue(0f)]
        [SerializeField] float closeDistanceMultiplierWhenStack = 0.85f;

        [BoxGroup(MotionGroup), LabelText("Apply Stack Shift To Close")]
        [SerializeField] bool applyStackShiftToCloseMultiplier;

        [BoxGroup(MotionGroup), LabelText("Show Duration"), MinValue(0f)]
        [SerializeField] float showAnimationDuration = 0.72f;

        [BoxGroup(MotionGroup), LabelText("Close Duration"), MinValue(0f)]
        [SerializeField] float closeAnimationDuration = 0.64f;

        [BoxGroup(MotionGroup), LabelText("Stack Shift Duration"), MinValue(0f)]
        [SerializeField] float stackShiftDuration = 0.32f;

        [BoxGroup(ModeGroup), ShowIf(nameof(IsStackMode)), LabelText("Stack Spacing"), MinValue(0f)]
        [SerializeField] float stackSpacing = 8f;

        [BoxGroup(BoundsGroup), LabelText("Use Visual Bounds")]
        [SerializeField] bool useVisualBounds = true;

        [BoxGroup(BoundsGroup), LabelText("Fallback Size")]
        [SerializeField] Vector2 fallbackSize = new(260f, 64f);

        [BoxGroup(BoundsGroup), LabelText("Clamp Inside Screen")]
        [SerializeField] bool clampInsideScreen = true;

        [BoxGroup(BoundsGroup), ShowIf(nameof(clampInsideScreen)), LabelText("Clamp Padding")]
        [SerializeField] Vector2 clampPadding = new(16f, 16f);

        [BoxGroup(AnchorGroup), LabelText("Anchor Preset")]
        [SerializeField] ToastAnchorPreset anchorPreset = ToastAnchorPreset.None;

        [BoxGroup(AnchorGroup), ShowIf(nameof(HasAnchorPreset)), LabelText("Reapply On Relayout")]
        [SerializeField] bool anchorReapplyOnRelayout;

        [BoxGroup(ChannelGroup), LabelText("Show Channel Tag")]
        [SerializeField] string showTransformChannelTag = "toast";

        [BoxGroup(ChannelGroup), LabelText("Close Channel Tag")]
        [SerializeField] string closeTransformChannelTag = "toast";

        [BoxGroup(ChannelGroup), LabelText("Stack Shift Channel Tag")]
        [SerializeField] string stackShiftTransformChannelTag = "toast";

        [BoxGroup(DebugGroup), LabelText("Enable Debug Log")]
        [SerializeField] bool enableDebugLog;

        [BoxGroup(DebugGroup), LabelText("Enable Movement Debug Log")]
        [SerializeField] bool enableMovementDebugLog;

        [BoxGroup(DebugGroup), LabelText("Debug Log Capacity"), MinValue(16)]
        [SerializeField] int debugLogCapacity = 128;

        public void InstallScopeServices(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            var resolvedRoot = toastRoot != null ? toastRoot : GetComponent<RectTransform>();
            if (resolvedRoot == null)
            {
                Debug.LogError("[ToastSystemMB] ToastRoot is missing.");
                return;
            }

            var config = new ToastSystemConfig
            {
                SystemTag = string.IsNullOrWhiteSpace(systemTag) ? "default" : systemTag.Trim(),
                ToastRoot = resolvedRoot,
                ClampArea = clampArea,
                DefaultRuntimeTemplate = defaultRuntimeTemplate,
                SpawnerTag = spawnerTag ?? string.Empty,
                DisplayMode = displayMode,
                MaxVisibleCount = Mathf.Max(1, maxVisibleCount),
                ShowDirection = showDirection,
                CloseDirection = closeDirection,
                StackShiftDirection = stackShiftDirection,
                QueueCloseAwaitMode = queueCloseAwaitMode,
                AutoCloseSeconds = Mathf.Max(0f, autoCloseSeconds),
                ShowDistanceMultiplier = Mathf.Max(0f, showDistanceMultiplier),
                CloseDistanceMultiplier = Mathf.Max(0f, closeDistanceMultiplier),
                CloseDistanceMultiplierWhenStack = Mathf.Max(0f, closeDistanceMultiplierWhenStack),
                ApplyStackShiftToCloseMultiplier = applyStackShiftToCloseMultiplier,
                UseVisualBounds = useVisualBounds,
                FallbackSize = new Vector2(Mathf.Max(1f, fallbackSize.x), Mathf.Max(1f, fallbackSize.y)),
                ClampInsideScreen = clampInsideScreen,
                ClampPadding = new Vector2(Mathf.Max(0f, clampPadding.x), Mathf.Max(0f, clampPadding.y)),
                AnchorPreset = anchorPreset,
                AnchorReapplyOnRelayout = anchorReapplyOnRelayout,
                StackSpacing = Mathf.Max(0f, stackSpacing),
                ShowTransformChannelTag = showTransformChannelTag ?? string.Empty,
                CloseTransformChannelTag = closeTransformChannelTag ?? string.Empty,
                StackShiftTransformChannelTag = stackShiftTransformChannelTag ?? string.Empty,
                ShowAnimationDuration = Mathf.Max(0f, showAnimationDuration),
                CloseAnimationDuration = Mathf.Max(0f, closeAnimationDuration),
                StackShiftDuration = Mathf.Max(0f, stackShiftDuration),
                EnableDebugLog = enableDebugLog,
                EnableMovementDebugLog = enableMovementDebugLog,
                DebugLogCapacity = Mathf.Max(16, debugLogCapacity),
            };

            builder.RegisterInstance(config);
            builder.RegisterAsScopeMulti<IToastSystemService, ToastSystemService>(RuntimeLifetime.Singleton)
                .As<IToastSystemDebugTelemetry>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .WithParameter(scope);
        }

        bool IsQueueMode => displayMode == ToastDisplayMode.Queue;
        bool IsStackMode => displayMode == ToastDisplayMode.Stack;
        bool HasAnchorPreset => anchorPreset != ToastAnchorPreset.None;

        void Reset()
        {
            if (toastRoot == null)
                toastRoot = GetComponent<RectTransform>();
        }
    }
}

