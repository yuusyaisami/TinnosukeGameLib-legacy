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
        [Tooltip("SpriteRenderer.drawMode に適用する type です。")]
        public AnimationSpriteRendererTypeMode Type = AnimationSpriteRendererTypeMode.Simple;

        [LabelText("Size")]
        [Tooltip("Renderer Type が Sliced または Tiled のときに適用する SpriteRenderer.size です。")]
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
        [Tooltip("UnityEngine.UI.Image.type に適用する type です。")]
        public AnimationSpriteImageTypeMode Type = AnimationSpriteImageTypeMode.Simple;

        [LabelText("Preserve Aspect")]
        [Tooltip("Image.preserveAspect を更新します。")]
        public bool PreserveAspect;

        [LabelText("Size Delta")]
        [Tooltip("Simple / Sliced / Tiled のときに RectTransform.sizeDelta へ適用するサイズです。")]
        [ShowIf(nameof(UsesRectSize))]
        public DynamicValue<Vector2> SizeDeltaSource = DynamicValueExtensions.FromLiteral(Vector2.zero);

        [LabelText("Fill Center")]
        [Tooltip("Sliced / Tiled のときに Image.fillCenter へ適用します。")]
        [ShowIf(nameof(UsesSlicedOrTiled))]
        public bool FillCenter = true;

        [LabelText("Pixels Per Unit")]
        [Tooltip("Sliced / Tiled のときに Image.pixelsPerUnitMultiplier へ適用します。")]
        [ShowIf(nameof(UsesSlicedOrTiled))]
        public DynamicValue<float> PixelsPerUnitMultiplierSource = DynamicValueExtensions.FromLiteral(1f);

        [LabelText("Fill Method")]
        [Tooltip("Filled のときに使う fill method です。")]
        [ShowIf(nameof(UsesFilled))]
        public AnimationSpriteImageFillMethodMode FillMethod = AnimationSpriteImageFillMethodMode.Horizontal;

        [LabelText("Fill Origin")]
        [Tooltip("Filled のときに使う fill origin の整数値です。method に応じて有効範囲が変わります。")]
        [ShowIf(nameof(UsesFilled))]
        public DynamicValue<int> FillOriginSource = DynamicValueExtensions.FromLiteral(0);

        [LabelText("Fill Clockwise")]
        [Tooltip("Radial 系 fill の回転方向です。")]
        [ShowIf(nameof(UsesFilled))]
        public bool FillClockwise = true;

        [LabelText("Fill Amount")]
        [Tooltip("Filled のときに使う 0..1 の fill amount です。")]
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
        public string ChannelTag = "default";

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
        [Tooltip("SpriteRenderer.drawMode / size または Image.type / size / fill 設定を適用します。")]
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
