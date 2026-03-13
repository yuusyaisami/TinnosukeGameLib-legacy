#nullable enable
using System;
using Game.Commands.VNext;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Channel
{
    public enum ParallaxDriverMode
    {
        DirectObject = 0,
        TransformAnimationChannel = 1,
        TransformController = 2,
    }

    public enum ParallaxCameraBindMode
    {
        MainCamera = 0,
        SpecificTransform = 1,
        ActorSource = 2,
    }

    public enum ParallaxWriteMode
    {
        AdditiveLocal = 0,
        AdditiveWorld = 1,
        OverrideLocal = 2,
        OverrideWorld = 3,
    }

    [Serializable]
    public struct ParallaxParams
    {
        [LabelText("Parallax Factor")]
        public Vector3 Factor;

        [LabelText("Extra Offset")]
        public Vector3 ExtraOffset;

        [LabelText("Affect X")]
        public bool AffectX;

        [LabelText("Affect Y")]
        public bool AffectY;

        [LabelText("Affect Z")]
        public bool AffectZ;

        [LabelText("Use Local Space")]
        public bool UseLocalSpace;

        [LabelText("Use Smoothing")]
        public bool UseSmoothing;

        [LabelText("Smooth Time")]
        [ShowIf(nameof(UseSmoothing))]
        [MinValue(0f)]
        public float SmoothTime;

        [LabelText("Max Offset Magnitude")]
        [MinValue(0f)]
        public float MaxOffsetMagnitude;

        public static ParallaxParams Default => new ParallaxParams
        {
            Factor = Vector3.one,
            ExtraOffset = Vector3.zero,
            AffectX = true,
            AffectY = true,
            AffectZ = false,
            UseLocalSpace = false,
            UseSmoothing = false,
            SmoothTime = 0.08f,
            MaxOffsetMagnitude = 0f,
        };
    }

    [Serializable]
    public sealed class ParallaxChannelDef : ChannelDefBase
    {
        [BoxGroup("State")]
        [LabelText("Enabled On Acquire")]
        [SerializeField] bool enabledOnAcquire = true;

        [BoxGroup("Bind")]
        [LabelText("Driver Mode")]
        [SerializeField] ParallaxDriverMode driverMode = ParallaxDriverMode.DirectObject;

        [BoxGroup("Bind")]
        [ShowIf(nameof(ShowDirectTarget))]
        [LabelText("Direct Target")]
        [SerializeField] Transform? directTarget;

        [BoxGroup("Bind")]
        [ShowIf(nameof(ShowTransformAnimationSettings))]
        [LabelText("Animation Hub Actor")]
        [SerializeField] ActorSource animationHubActorSource;

        [BoxGroup("Bind")]
        [ShowIf(nameof(ShowTransformAnimationSettings))]
        [LabelText("Animation Channel Tag")]
        [SerializeField] string transformAnimationChannelTag = "default";

        [BoxGroup("Bind")]
        [ShowIf(nameof(ShowTransformControllerSettings))]
        [LabelText("Controller Actor")]
        [SerializeField] ActorSource controllerActorSource;

        [BoxGroup("Bind")]
        [LabelText("Camera Bind")]
        [SerializeField] ParallaxCameraBindMode cameraBindMode = ParallaxCameraBindMode.MainCamera;

        [BoxGroup("Bind")]
        [ShowIf(nameof(ShowSpecificCameraTransform))]
        [LabelText("Camera Transform")]
        [SerializeField] Transform? specificCameraTransform;

        [BoxGroup("Bind")]
        [ShowIf(nameof(ShowActorSourceCamera))]
        [LabelText("Camera Actor")]
        [SerializeField] ActorSource cameraActorSource;

        [BoxGroup("Write")]
        [LabelText("Write Mode")]
        [SerializeField] ParallaxWriteMode writeMode = ParallaxWriteMode.AdditiveLocal;

        [BoxGroup("Write")]
        [LabelText("Allow Rigidbody2D Unsafe Write")]
        [SerializeField] bool allowUnsafeRigidbody2DWrite;

        [BoxGroup("Parallax")]
        [LabelText("Parameters")]
        [InlineProperty]
        [SerializeField] ParallaxParams parameters = default;

        [BoxGroup("Performance")]
        [LabelText("Update Every N Frames")]
        [MinValue(1)]
        [SerializeField] int updateEveryNFrames = 1;

        [BoxGroup("Debug")]
        [LabelText("Debug Log")]
        [SerializeField] bool debugLogEnabled;

        public bool EnabledOnAcquire => enabledOnAcquire;
        public ParallaxDriverMode DriverMode => driverMode;
        public Transform? DirectTarget => directTarget;
        public ActorSource AnimationHubActorSource => animationHubActorSource;
        public string TransformAnimationChannelTag => transformAnimationChannelTag;
        public ActorSource ControllerActorSource => controllerActorSource;
        public ParallaxCameraBindMode CameraBindMode => cameraBindMode;
        public Transform? SpecificCameraTransform => specificCameraTransform;
        public ActorSource CameraActorSource => cameraActorSource;
        public ParallaxWriteMode WriteMode => writeMode;
        public bool AllowUnsafeRigidbody2DWrite => allowUnsafeRigidbody2DWrite;
        public ParallaxParams Parameters => parameters;
        public int UpdateEveryNFrames => Mathf.Max(1, updateEveryNFrames);
        public bool DebugLogEnabled => debugLogEnabled;

        bool ShowDirectTarget() => driverMode == ParallaxDriverMode.DirectObject;
        bool ShowTransformAnimationSettings() => driverMode == ParallaxDriverMode.TransformAnimationChannel;
        bool ShowTransformControllerSettings() => driverMode == ParallaxDriverMode.TransformController;
        bool ShowSpecificCameraTransform() => cameraBindMode == ParallaxCameraBindMode.SpecificTransform;
        bool ShowActorSourceCamera() => cameraBindMode == ParallaxCameraBindMode.ActorSource;

        public override void EnsureIntegrity(Component owner)
        {
            base.EnsureIntegrity(owner);

            if (string.IsNullOrWhiteSpace(transformAnimationChannelTag))
                transformAnimationChannelTag = "default";

            if (updateEveryNFrames < 1)
                updateEveryNFrames = 1;

            if (parameters.SmoothTime < 0f)
                parameters.SmoothTime = 0f;

            if (parameters.MaxOffsetMagnitude < 0f)
                parameters.MaxOffsetMagnitude = 0f;
        }
    }
}
