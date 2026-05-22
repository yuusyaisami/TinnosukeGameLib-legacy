#nullable enable
using System;
using System.Collections.Generic;
using Game.Channel;
using Game.Common;
using Game.MaterialFx;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    public enum AnimationSpriteFlipControlMode
    {
        Trigger = 0,
        ExplicitAngle = 1,
    }

    public enum AnimationSpriteRendererTypeMode
    {
        Simple = 10,
        Sliced = 20,
        Tiled = 30,
    }

    public enum AnimationSpriteImageTypeMode
    {
        Simple = 10,
        Sliced = 20,
        Tiled = 30,
        Filled = 40,
    }

    public enum AnimationSpriteImageFillMethodMode
    {
        Horizontal = 10,
        Vertical = 20,
        Radial90 = 30,
        Radial180 = 40,
        Radial360 = 50,
    }

    [Serializable]
    public sealed class AnimationSpriteRendererTypePayload
    {
        [LabelText("Renderer Type")]
        [Tooltip("Inspector setting.")]
        public AnimationSpriteRendererTypeMode Type = AnimationSpriteRendererTypeMode.Simple;

        [LabelText("Size")]
        [Tooltip("Inspector setting.")]
        [ShowIf(nameof(UsesRendererSize))]
        public DynamicValue<Vector2> SizeSource = DynamicValueExtensions.FromLiteral(Vector2.one);

        bool UsesRendererSize()
        {
            return Type == AnimationSpriteRendererTypeMode.Sliced ||
                   Type == AnimationSpriteRendererTypeMode.Tiled;
        }
    }

    [Serializable]
    public sealed class AnimationSpriteImageTypePayload
    {
        [LabelText("Image Type")]
        [Tooltip("Inspector setting.")]
        public AnimationSpriteImageTypeMode Type = AnimationSpriteImageTypeMode.Simple;

        [LabelText("Preserve Aspect")]
        [Tooltip("Inspector setting.")]
        public bool PreserveAspect;

        [LabelText("Size Delta")]
        [Tooltip("Inspector setting.")]
        [ShowIf(nameof(UsesRectSize))]
        public DynamicValue<Vector2> SizeDeltaSource = DynamicValueExtensions.FromLiteral(Vector2.zero);

        [LabelText("Fill Center")]
        [Tooltip("Inspector setting.")]
        [ShowIf(nameof(UsesSlicedOrTiled))]
        public bool FillCenter = true;

        [LabelText("Pixels Per Unit")]
        [Tooltip("Inspector setting.")]
        [ShowIf(nameof(UsesSlicedOrTiled))]
        public DynamicValue<float> PixelsPerUnitMultiplierSource = DynamicValueExtensions.FromLiteral(1f);

        [LabelText("Fill Method")]
        [Tooltip("Inspector setting.")]
        [ShowIf(nameof(UsesFilled))]
        public AnimationSpriteImageFillMethodMode FillMethod = AnimationSpriteImageFillMethodMode.Horizontal;

        [LabelText("Fill Origin")]
        [Tooltip("Inspector setting.")]
        [ShowIf(nameof(UsesFilled))]
        public DynamicValue<int> FillOriginSource = DynamicValueExtensions.FromLiteral(0);

        [LabelText("Fill Clockwise")]
        [Tooltip("Inspector setting.")]
        [ShowIf(nameof(UsesFilled))]
        public bool FillClockwise = true;

        [LabelText("Fill Amount")]
        [Tooltip("Inspector setting.")]
        [ShowIf(nameof(UsesFilled))]
        public DynamicValue<float> FillAmountSource = DynamicValueExtensions.FromLiteral(1f);

        bool UsesRectSize()
        {
            return Type == AnimationSpriteImageTypeMode.Simple ||
                   Type == AnimationSpriteImageTypeMode.Sliced ||
                   Type == AnimationSpriteImageTypeMode.Tiled;
        }

        bool UsesSlicedOrTiled()
        {
            return Type == AnimationSpriteImageTypeMode.Sliced ||
                   Type == AnimationSpriteImageTypeMode.Tiled;
        }

        bool UsesFilled() => Type == AnimationSpriteImageTypeMode.Filled;
    }

    [Serializable]
    public sealed class MaterialFxPayload
    {
        [LabelText("Context Tag")]
        public string ContextTag = "default";

        [LabelText("Clear Context First")]
        public bool ClearContextFirst;

        [LabelText("Priority")]
        public int Priority;

        [LabelText("Entries")]
        [ListDrawerSettings(ShowFoldout = true, DraggableItems = true, DefaultExpandedState = false)]
        public List<MaterialFxPresetEntry> Entries = new();
    }

    [Serializable]
    public sealed class AnimationSpriteChannelCommandData : ICommandData
    {
        public int CommandId => CommandIds.AnimationSpriteChannel;
        public string DebugData
        {
            get
            {
                var tag = string.IsNullOrEmpty(ChannelTag) ? "<none>" : ChannelTag;
                return $"Tag={tag} Anim={ApplyAnimation} Fx={ApplyMaterialFx} Speed={ApplyPlaybackSpeed} Flip={ApplyFlipX} Sort={ApplySortingOrder} Type={ApplyVisualType}";
            }
        }

        [BoxGroup("Target")]
        [LabelText("Channel Tag")]
        public string ChannelTag = string.Empty;

        [BoxGroup("Animation")]
        [LabelText("Apply Animation")]
        public bool ApplyAnimation = true;

        [BoxGroup("Animation")]
        [LabelText("Await Mode")]
        [ShowIf(nameof(ApplyAnimation))]
        public FlowRunAwaitMode AwaitMode = FlowRunAwaitMode.WaitForCompletion;

        [BoxGroup("Animation")]
        [LabelText("Wait For Once Completion")]
        [ShowIf("@ApplyAnimation && AwaitMode == FlowRunAwaitMode.WaitForCompletion")]
        public bool WaitForOnceCompletion = true;


        [BoxGroup("Animation")]
        [LabelText("Preset Source")]
        [ShowIf(nameof(ApplyAnimation))]
        public DynamicValue<AnimationSpritePreset> PresetSource;

        [BoxGroup("MaterialFx")]
        [LabelText("Apply MaterialFx")]
        public bool ApplyMaterialFx;


        [BoxGroup("MaterialFx")]
        [LabelText("MaterialFx Source")]
        [ShowIf(nameof(ApplyMaterialFx))]
        public DynamicValue<MaterialFxPayload> MaterialFxSource;

        [BoxGroup("Playback Speed")]
        [LabelText("Apply Playback Speed")]
        public bool ApplyPlaybackSpeed;

        [BoxGroup("Playback Speed")]
        [LabelText("Playback Speed Source")]
        [Tooltip("0 = freeze current frame, 1 = normal, 2 = double speed")]
        [ShowIf(nameof(ApplyPlaybackSpeed))]
        public DynamicValue<float> PlaybackSpeedSource = DynamicValueExtensions.FromLiteral(1f);

        [BoxGroup("FlipX")]
        [LabelText("Apply FlipX")]
        public bool ApplyFlipX;

        [BoxGroup("FlipX")]
        [LabelText("Mode")]
        [ShowIf(nameof(ApplyFlipX))]
        public AnimationSpriteFlipControlMode FlipXMode = AnimationSpriteFlipControlMode.Trigger;

        [BoxGroup("FlipX")]
        [LabelText("Flip Angle (Y)")]
        [Tooltip("Typical values are 0 (normal) or 180 (flipped).")]
        [ShowIf("@ApplyFlipX && FlipXMode == AnimationSpriteFlipControlMode.ExplicitAngle")]
        public DynamicValue<float> FlipXAngleSource = DynamicValueExtensions.FromLiteral(180f);

        [BoxGroup("Sorting Order")]
        [LabelText("Apply Sorting Order")]
        public bool ApplySortingOrder;

        [BoxGroup("Sorting Order")]
        [LabelText("Sorting Order Source")]
        [ShowIf(nameof(ApplySortingOrder))]
        public DynamicValue<int> SortingOrderSource = DynamicValueExtensions.FromLiteral(0);

        [BoxGroup("Visual Type")]
        [LabelText("Apply Visual Type")]
        [Tooltip("Inspector setting.")]
        public bool ApplyVisualType;

        [BoxGroup("Visual Type")]
        [LabelText("SpriteRenderer")]
        [ShowIf(nameof(ApplyVisualType))]
        [InlineProperty]
        public AnimationSpriteRendererTypePayload SpriteRendererType = new();

        [BoxGroup("Visual Type")]
        [LabelText("Image")]
        [ShowIf(nameof(ApplyVisualType))]
        [InlineProperty]
        public AnimationSpriteImageTypePayload ImageType = new();
    }
}
