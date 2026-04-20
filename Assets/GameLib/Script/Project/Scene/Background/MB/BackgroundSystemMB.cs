#nullable enable
using System.Collections.Generic;
using Game;
using Game.Commands.VNext;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Background
{
    [DisallowMultipleComponent]
    public sealed class BackgroundSystemMB : MonoBehaviour, IFeatureInstaller
    {
        const string RootGroup = "Root";
        const string ViewGroup = "View";
        const string UpdateGroup = "Update";
        const string CommandGroup = "Commands";
        const string LayerGroup = "Layers";

        [BoxGroup(RootGroup)]
        [SerializeField] BackgroundSpace space = BackgroundSpace.World;

        [BoxGroup(RootGroup)]
        [SerializeField] BackgroundMode mode = BackgroundMode.Infinite;

        [BoxGroup(RootGroup)]
        [ShowIf(nameof(IsWorldSpace))]
        [LabelText("World Root")]
        [SerializeField] Transform? worldRoot;

        [BoxGroup(RootGroup)]
        [ShowIf(nameof(IsUiSpace))]
        [LabelText("UI Root")]
        [SerializeField] RectTransform? uiRoot;

        [BoxGroup(ViewGroup)]
        [ShowIf(nameof(IsWorldSpace))]
        [LabelText("World Camera")]
        [SerializeField] Camera? worldCamera;

        [BoxGroup(ViewGroup)]
        [ShowIf(nameof(IsUiSpace))]
        [LabelText("UI Camera")]
        [SerializeField] Camera? uiCamera;

        [BoxGroup(ViewGroup)]
        [LabelText("Target Transform")]
        [SerializeField] Transform? targetTransform;

        [BoxGroup(ViewGroup)]
        [ShowIf(nameof(IsWorldSpace))]
        [LabelText("Use Camera View")]
        [SerializeField] bool useCameraView = true;

        [BoxGroup(ViewGroup)]
        [ShowIf(nameof(ShowManualViewSize))]
        [LabelText("Manual View Size")]
        [SerializeField] Vector2 manualViewSize = new Vector2(32f, 32f);

        [BoxGroup(UpdateGroup)]
        [Min(0f)]
        [SerializeField] float updateIntervalSeconds = 0f;

        [BoxGroup(UpdateGroup)]
        [Min(0)]
        [SerializeField] int maxSpawnPerFrame = 4;

        [BoxGroup(UpdateGroup)]
        [Min(0)]
        [SerializeField] int maxRemovePerFrame = 4;

        [BoxGroup(UpdateGroup)]
        [SerializeField] Vector2Int viewMarginTiles = Vector2Int.zero;

        [BoxGroup(UpdateGroup)]
        [LabelText("Preload Outside View (Tiles)")]
        [Tooltip("Inspector setting.")]
        [SerializeField] Vector2Int preloadOutsideViewTiles = Vector2Int.zero;

        [BoxGroup(UpdateGroup)]
        [SerializeField] bool runInLateUpdate = true;

        [BoxGroup(CommandGroup)]
        [CommandListFunctionName("Background.Update")]
        [SerializeField] CommandListData updateCommands = new();

        [BoxGroup(CommandGroup)]
        [ListDrawerSettings(ShowFoldout = false)]
        [SerializeField] List<BackgroundConditionalCommand> conditionalCommands = new();

        [BoxGroup(CommandGroup)]
        [Min(0f)]
        [LabelText("Command Interval Seconds")]
        [SerializeField] float commandIntervalSeconds = 0f;

        [BoxGroup(LayerGroup)]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
        [SerializeField] List<BackgroundLayerDefinition> layers = new();

        public void InstallFeature(IRuntimeContainerBuilder builder, IScopeNode owner)
        {
            EnsureDefaults();

            var config = new BackgroundSystemConfig
            {
                Space = space,
                Mode = mode,
                WorldRoot = worldRoot != null ? worldRoot : transform,
                UiRoot = uiRoot != null ? uiRoot : transform as RectTransform,
                WorldCamera = worldCamera,
                UiCamera = uiCamera,
                TargetTransform = targetTransform,
                UseCameraView = useCameraView,
                ManualViewSize = manualViewSize,
                UpdateIntervalSeconds = updateIntervalSeconds,
                MaxSpawnPerFrame = maxSpawnPerFrame,
                MaxRemovePerFrame = maxRemovePerFrame,
                ViewMarginTiles = viewMarginTiles,
                PreloadOutsideViewTiles = preloadOutsideViewTiles,
                RunInLateUpdate = runInLateUpdate,
                UpdateCommands = updateCommands,
                ConditionalCommands = conditionalCommands ?? new List<BackgroundConditionalCommand>(),
                CommandIntervalSeconds = commandIntervalSeconds,
                Layers = layers ?? new List<BackgroundLayerDefinition>(),
            };

            builder.RegisterInstance(config);

            var registration = builder.Register<BackgroundSystemService>(RuntimeLifetime.Singleton)
                .WithParameter(owner)
                .As<IBackgroundSystem>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();

            if (runInLateUpdate)
                registration.As<IScopeLateTickHandler>();
            else
                registration.As<IScopeTickHandler>();
        }

        void Reset()
        {
            EnsureDefaults();
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            EnsureDefaults();
        }
#endif

        void EnsureDefaults()
        {
            if (manualViewSize.x <= 0f) manualViewSize.x = 32f;
            if (manualViewSize.y <= 0f) manualViewSize.y = 32f;

            if (updateIntervalSeconds < 0f) updateIntervalSeconds = 0f;
            if (commandIntervalSeconds < 0f) commandIntervalSeconds = 0f;

            if (maxSpawnPerFrame < 0) maxSpawnPerFrame = 0;
            if (maxRemovePerFrame < 0) maxRemovePerFrame = 0;

            if (viewMarginTiles.x < 0) viewMarginTiles.x = 0;
            if (viewMarginTiles.y < 0) viewMarginTiles.y = 0;
            if (preloadOutsideViewTiles.x < 0) preloadOutsideViewTiles.x = 0;
            if (preloadOutsideViewTiles.y < 0) preloadOutsideViewTiles.y = 0;

            if (space == BackgroundSpace.World)
            {
                if (worldRoot == null)
                    worldRoot = transform;
            }
            else
            {
                if (uiRoot == null)
                    uiRoot = transform as RectTransform;
            }

            if (layers == null)
                layers = new List<BackgroundLayerDefinition>();

            for (int i = 0; i < layers.Count; i++)
            {
                var layer = layers[i];
                if (layer != null)
                    layer.EnsureDefaults(space);
            }
        }

        bool IsWorldSpace() => space == BackgroundSpace.World;
        bool IsUiSpace() => space == BackgroundSpace.UI;
        bool ShowManualViewSize() => space == BackgroundSpace.World && !useCameraView;
    }
}
