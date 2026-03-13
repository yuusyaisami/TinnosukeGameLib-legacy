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
                return $"Tag={tag} Anim={ApplyAnimation} Fx={ApplyMaterialFx} Speed={ApplyPlaybackSpeed} Flip={ApplyFlipX} Sort={ApplySortingOrder}";
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
    }
}
