#nullable enable
using System;
using Game.Channel;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    public enum ParallaxChannelOperation
    {
        SetEnabled = 0,
        ToggleEnabled = 1,
        SetWriteMode = 2,
        SetFactor = 3,
        SetExtraOffset = 4,
        SetAffectAxes = 5,
        SetSmoothing = 6,
        SetMaxOffsetMagnitude = 7,
        SetUpdateEveryNFrames = 8,
        SetAllowUnsafeRigidbody2DWrite = 9,
        SetDriverMode = 10,
        SetCameraBindMode = 11,
        SetDirectTarget = 12,
        SetAnimationChannelTag = 13,
        ResetCameraOrigin = 14,
        ResetRuntimeOverrides = 15,
    }

    [Serializable]
    public sealed class ParallaxChannelCommandData : ICommandData
    {
        public int CommandId => CommandIds.ParallaxChannel;

        public string DebugData
        {
            get
            {
                var target = ApplyToAllChannels ? "AllChannels" : $"Tag={ChannelTag}";
                return $"Op={Operation} {target}";
            }
        }

        [BoxGroup("Target")]
        [LabelText("Operation")]
        [EnumToggleButtons]
        public ParallaxChannelOperation Operation = ParallaxChannelOperation.SetEnabled;

        [BoxGroup("Target")]
        [LabelText("Apply To All Channels")]
        public bool ApplyToAllChannels;

        [BoxGroup("Target")]
        [LabelText("Channel Tag")]
        [ShowIf("@!ApplyToAllChannels")]
        public string ChannelTag = "default";

        [BoxGroup("Enabled")]
        [LabelText("Enabled")]
        [ShowIf("@Operation == ParallaxChannelOperation.SetEnabled")]
        public DynamicValue<bool> Enabled;

        [BoxGroup("Write")]
        [LabelText("Write Mode")]
        [ShowIf("@Operation == ParallaxChannelOperation.SetWriteMode")]
        public ParallaxWriteMode WriteMode = ParallaxWriteMode.AdditiveLocal;

        [BoxGroup("Parallax")]
        [LabelText("Factor")]
        [ShowIf("@Operation == ParallaxChannelOperation.SetFactor")]
        public DynamicValue<Vector3> Factor;

        [BoxGroup("Parallax")]
        [LabelText("Extra Offset")]
        [ShowIf("@Operation == ParallaxChannelOperation.SetExtraOffset")]
        public DynamicValue<Vector3> ExtraOffset;

        [BoxGroup("Parallax")]
        [LabelText("Affect X")]
        [ShowIf("@Operation == ParallaxChannelOperation.SetAffectAxes")]
        public DynamicValue<bool> AffectX;

        [BoxGroup("Parallax")]
        [LabelText("Affect Y")]
        [ShowIf("@Operation == ParallaxChannelOperation.SetAffectAxes")]
        public DynamicValue<bool> AffectY;

        [BoxGroup("Parallax")]
        [LabelText("Affect Z")]
        [ShowIf("@Operation == ParallaxChannelOperation.SetAffectAxes")]
        public DynamicValue<bool> AffectZ;

        [BoxGroup("Smoothing")]
        [LabelText("Use Smoothing")]
        [ShowIf("@Operation == ParallaxChannelOperation.SetSmoothing")]
        public DynamicValue<bool> UseSmoothing;

        [BoxGroup("Smoothing")]
        [LabelText("Smooth Time")]
        [ShowIf("@Operation == ParallaxChannelOperation.SetSmoothing")]
        public DynamicValue<float> SmoothTime;

        [BoxGroup("Parallax")]
        [LabelText("Max Offset Magnitude")]
        [ShowIf("@Operation == ParallaxChannelOperation.SetMaxOffsetMagnitude")]
        public DynamicValue<float> MaxOffsetMagnitude;

        [BoxGroup("Performance")]
        [LabelText("Update Every N Frames")]
        [ShowIf("@Operation == ParallaxChannelOperation.SetUpdateEveryNFrames")]
        public DynamicValue<int> UpdateEveryNFrames;

        [BoxGroup("Safety")]
        [LabelText("Allow Unsafe Rigidbody2D Write")]
        [ShowIf("@Operation == ParallaxChannelOperation.SetAllowUnsafeRigidbody2DWrite")]
        public DynamicValue<bool> AllowUnsafeRigidbody2DWrite;

        [BoxGroup("Bind")]
        [LabelText("Driver Mode")]
        [ShowIf("@Operation == ParallaxChannelOperation.SetDriverMode")]
        public ParallaxDriverMode DriverMode = ParallaxDriverMode.DirectObject;

        [BoxGroup("Bind")]
        [LabelText("Camera Bind Mode")]
        [ShowIf("@Operation == ParallaxChannelOperation.SetCameraBindMode")]
        public ParallaxCameraBindMode CameraBindMode = ParallaxCameraBindMode.MainCamera;

        [BoxGroup("Bind")]
        [LabelText("Direct Target")]
        [ShowIf("@Operation == ParallaxChannelOperation.SetDirectTarget")]
        public DynamicValue<Transform> DirectTarget;

        [BoxGroup("Bind")]
        [LabelText("Animation Channel Tag")]
        [ShowIf("@Operation == ParallaxChannelOperation.SetAnimationChannelTag")]
        public DynamicValue<string> AnimationChannelTag;
    }
}
