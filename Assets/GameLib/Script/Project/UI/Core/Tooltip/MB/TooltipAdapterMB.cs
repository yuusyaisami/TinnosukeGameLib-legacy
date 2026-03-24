#nullable enable
using System.Collections.Generic;
using Game;
using Game.Common;
using Game.Commands.VNext;
using Game.DI;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace Game.UI
{
    [DisallowMultipleComponent]
    public sealed class TooltipAdapterMB : MonoBehaviour, IFeatureInstaller, ITooltipAdapterOptions
    {
        const string KindGroup = "Kind";
        const string InputGroup = "Input";
        const string PlacementGroup = "Placement";
        const string HitGroup = "Hit Test";
        const string RuntimeGroup = "Runtime";
        const string CommandsGroup = "Commands";
        const string CameraGroup = "Cameras";

        [BoxGroup(KindGroup)]
        [SerializeField] TooltipAdapterKind adapterKind = TooltipAdapterKind.Auto;

        [BoxGroup(KindGroup)]
        [LabelText("Priority")]
        [SerializeField] int priority = 0;

        [BoxGroup(KindGroup)]
        [LabelText("Anchor Transform")]
        [SerializeField] Transform? anchorTransform;

        [BoxGroup(RuntimeGroup)]
        [LabelText("Runtime Template")]
        [SerializeField, InlineProperty, HideLabel] DynamicValue<BaseRuntimeTemplatePreset> runtimeTemplatePreset;

        [BoxGroup(RuntimeGroup)]
        [LabelText("Spawner Tag")]
        [SerializeField] string spawnerTag = "";

        [BoxGroup(InputGroup)]
        [LabelText("Enable Pointer Hover")]
        [SerializeField] bool enablePointerHover = true;

        [BoxGroup(InputGroup)]
        [LabelText("Enable Selection Hover")]
        [SerializeField] bool enableSelectionHover = true;

        [BoxGroup(InputGroup)]
        [Min(0f)]
        [SerializeField] float hoverDelaySeconds = 0.4f;

        [BoxGroup(InputGroup)]
        [Min(0f)]
        [SerializeField] float selectionDelaySeconds = 0.3f;

        [BoxGroup(InputGroup)]
        [Min(0f)]
        [SerializeField] float pointerMoveThreshold = 2f;

        [BoxGroup(PlacementGroup)]
        [SerializeField] TooltipSpawnMode spawnMode = TooltipSpawnMode.FollowPointer;

        [BoxGroup(PlacementGroup)]
        [ShowIf(nameof(IsFollowPointer))]
        [LabelText("Follow Pointer Offset")]
        [SerializeField] Vector2 followPointerOffset = Vector2.zero;

        [BoxGroup(PlacementGroup)]
        [ShowIf(nameof(IsFollowPointer))]
        [LabelText("Follow Pointer Move Scale")]
        [SerializeField] Vector2 followPointerMoveScale = Vector2.one;

        [BoxGroup(PlacementGroup)]
        [ShowIf("IsFixedOffset")]
        [SerializeField] Vector2 fixedOffset = Vector2.zero;

        [BoxGroup(PlacementGroup)]
        [SerializeField] TooltipAnchorX anchorX = TooltipAnchorX.Right;

        [BoxGroup(PlacementGroup)]
        [SerializeField] TooltipAnchorY anchorY = TooltipAnchorY.Up;

        [BoxGroup(HitGroup)]
        [ShowIf("IsUiKind")]
        [LabelText("Hit Rects")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = false)]
        [SerializeField] List<RectTransform> hitRects = new();

        [BoxGroup(HitGroup)]
        [ShowIf("IsWorldKind")]
        [LabelText("Hit Sprites")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = false)]
        [SerializeField] List<SpriteRenderer> hitSprites = new();

        [BoxGroup(CameraGroup)]
        [LabelText("UI Camera")]
        [SerializeField] Camera? uiCamera;

        [BoxGroup(CameraGroup)]
        [LabelText("World Camera")]
        [SerializeField] Camera? worldCamera;

        [BoxGroup(CommandsGroup)]
        [CommandListFunctionName("Tooltip.Show")]
        [SerializeField] CommandListData showCommands = new();

        [BoxGroup(CommandsGroup)]
        [CommandListFunctionName("Tooltip.Hide")]
        [SerializeField] CommandListData hideCommands = new();

        [BoxGroup(CommandsGroup)]
        [InlineProperty]
        [HideLabel]
        [SerializeField] SelfDespawnCommandData selfDespawn = new();

        public TooltipAdapterKind Kind => ResolveKind();
        public bool EnablePointerHover => enablePointerHover;
        public bool EnableSelectionHover => enableSelectionHover;
        public float HoverDelaySeconds => hoverDelaySeconds;
        public float SelectionDelaySeconds => selectionDelaySeconds;
        public float PointerMoveThreshold => pointerMoveThreshold;
        public TooltipSpawnMode SpawnMode => spawnMode;
        public Vector2 FollowPointerOffset => followPointerOffset;
        public Vector2 FollowPointerMoveScale => followPointerMoveScale;
        public Vector2 FixedOffset => fixedOffset;
        public TooltipAnchorX AnchorX => anchorX;
        public TooltipAnchorY AnchorY => anchorY;
        public CommandListData ShowCommands => showCommands;
        public CommandListData HideCommands => hideCommands;
        public SelfDespawnCommandData SelfDespawn => selfDespawn;
        public Camera? UiCamera => uiCamera;
        public Camera? WorldCamera => worldCamera;
        public IReadOnlyList<RectTransform> HitRects => hitRects;
        public IReadOnlyList<SpriteRenderer> HitSprites => hitSprites;
        public Transform? AnchorTransform => anchorTransform != null ? anchorTransform : transform;
        public int Priority => priority;
        public string SpawnerTag => spawnerTag ?? string.Empty;

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            var kind = ResolveKind();
            if (kind == TooltipAdapterKind.UIScreen)
            {
                builder.Register<TooltipUIScreenAdapterService>(Lifetime.Singleton)
                    .WithParameter(scope)
                    .WithParameter<ITooltipAdapterOptions>(this)
                    .As<ITooltipAdapter>()
                    .As<IScopeAcquireHandler>()
                    .As<IScopeReleaseHandler>();
            }
            else
            {
                builder.Register<TooltipWorldAdapterService>(Lifetime.Singleton)
                    .WithParameter(scope)
                    .WithParameter<ITooltipAdapterOptions>(this)
                    .As<ITooltipAdapter>()
                    .As<IScopeAcquireHandler>()
                    .As<IScopeReleaseHandler>();
            }
        }

        public bool TryResolveRuntimeTemplate(IDynamicContext context, out BaseRuntimeTemplateSO? resolvedRuntimeTemplate)
        {
            resolvedRuntimeTemplate = null;
            if (!runtimeTemplatePreset.TryGet(context, out var preset) || preset == null)
                return false;

            resolvedRuntimeTemplate = RuntimeTemplatePresetResolver.ResolveTemplateSO(preset);
            return resolvedRuntimeTemplate != null;
        }

        TooltipAdapterKind ResolveKind()
        {
            if (adapterKind != TooltipAdapterKind.Auto)
                return adapterKind;

            if (hitRects != null && hitRects.Count > 0)
                return TooltipAdapterKind.UIScreen;

            if (GetComponent<RectTransform>() != null)
                return TooltipAdapterKind.UIScreen;

            return TooltipAdapterKind.World;
        }

        bool IsFixedOffset() => spawnMode == TooltipSpawnMode.FixedOffset;
        bool IsFollowPointer() => spawnMode == TooltipSpawnMode.FollowPointer;
        bool IsUiKind() => adapterKind != TooltipAdapterKind.World;
        bool IsWorldKind() => adapterKind != TooltipAdapterKind.UIScreen;

        void Reset()
        {
            if (anchorTransform == null)
                anchorTransform = transform;

            EnsureDefaultHitTargets();
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (anchorTransform == null)
                anchorTransform = transform;

            if (hoverDelaySeconds < 0f)
                hoverDelaySeconds = 0f;
            if (selectionDelaySeconds < 0f)
                selectionDelaySeconds = 0f;
            if (pointerMoveThreshold < 0f)
                pointerMoveThreshold = 0f;

            EnsureDefaultHitTargets();
        }
#endif

        void EnsureDefaultHitTargets()
        {
            var kind = ResolveKind();
            if (kind == TooltipAdapterKind.World)
            {
                if (hitSprites == null || hitSprites.Count == 0)
                {
                    var sr = GetComponentInChildren<SpriteRenderer>();
                    if (sr != null)
                    {
                        hitSprites = new List<SpriteRenderer> { sr };
                    }
                }
                return;
            }

            if (hitRects == null || hitRects.Count == 0)
            {
                var rt = GetComponent<RectTransform>();
                if (rt != null)
                {
                    hitRects = new List<RectTransform> { rt };
                }
            }
        }
    }
}
