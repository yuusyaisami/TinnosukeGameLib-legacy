#nullable enable
using System;
using Game.Rotation;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    public enum VelocityDrivenRotationCommandMode
    {
        ApplySettings = 0,
        SetEnabled = 1,
        SetMode = 2,
        SetSpeedScale = 3,
        SetSource = 4,
        SetRotateChannelKey = 5,
        SetTiltSettings = 6,
        SetSpinSettings = 7,
        SetFacingSettings = 8,
    }

    [Serializable]
    public sealed class VelocityDrivenRotationCommandData : ICommandData
    {
        public int CommandId => CommandIds.VelocityDrivenRotation;
        public string DebugData
        {
            get
            {
                return Mode switch
                {
                    VelocityDrivenRotationCommandMode.SetEnabled => $"Mode=SetEnabled Enabled={Enabled}",
                    VelocityDrivenRotationCommandMode.SetMode => $"Mode=SetMode Value={RotationMode}",
                    VelocityDrivenRotationCommandMode.SetSpeedScale => $"Mode=SetSpeedScale Scale={SpeedScale}",
                    VelocityDrivenRotationCommandMode.SetSource => $"Mode=SetSource Source={Source}",
                    VelocityDrivenRotationCommandMode.SetRotateChannelKey => $"Mode=SetRotateChannelKey Key={RotateChannelKey}",
                    VelocityDrivenRotationCommandMode.SetTiltSettings => "Mode=SetTiltSettings",
                    VelocityDrivenRotationCommandMode.SetSpinSettings => "Mode=SetSpinSettings",
                    VelocityDrivenRotationCommandMode.SetFacingSettings => "Mode=SetFacingSettings",
                    _ => "Mode=ApplySettings",
                };
            }
        }

        [BoxGroup("Mode")]
        [LabelText("Mode")]
        [SerializeField]
        public VelocityDrivenRotationCommandMode Mode = VelocityDrivenRotationCommandMode.ApplySettings;

        [BoxGroup("Settings")]
        [LabelText("Settings")]
        [ShowIf("@Mode == VelocityDrivenRotationCommandMode.ApplySettings")]
        [InlineProperty, HideLabel]
        public VelocityRotationSettings Settings = VelocityRotationSettings.Default;

        [BoxGroup("Enabled")]
        [LabelText("Enabled")]
        [ShowIf("@Mode == VelocityDrivenRotationCommandMode.SetEnabled")]
        public bool Enabled = true;

        [BoxGroup("Mode")]
        [LabelText("Rotation Mode")]
        [ShowIf("@Mode == VelocityDrivenRotationCommandMode.SetMode")]
        public VelocityRotationMode RotationMode = VelocityRotationMode.Tilt;

        [BoxGroup("Speed Scale")]
        [LabelText("Speed Scale")]
        [ShowIf("@Mode == VelocityDrivenRotationCommandMode.SetSpeedScale")]
        [MinValue(0f)]
        public float SpeedScale = 1f;

        [BoxGroup("Speed Scale")]
        [LabelText("Use Scalar Speed Scale")]
        [ShowIf("@Mode == VelocityDrivenRotationCommandMode.SetSpeedScale")]
        public bool UseScalarSpeedScale;

        [BoxGroup("Speed Scale")]
        [LabelText("Speed Scale Scalar")]
        [ShowIf("@Mode == VelocityDrivenRotationCommandMode.SetSpeedScale && UseScalarSpeedScale")]
        public Game.Scalar.ScalarKey SpeedScaleScalar;

        [BoxGroup("Source")]
        [LabelText("Source Kind")]
        [ShowIf("@Mode == VelocityDrivenRotationCommandMode.SetSource")]
        public VelocityRotationSourceKind Source = VelocityRotationSourceKind.TransformChannel;

        [BoxGroup("Source")]
        [LabelText("Source Transform")]
        [ShowIf("@Mode == VelocityDrivenRotationCommandMode.SetSource")]
        public Transform? SourceTransform;

        [BoxGroup("Source")]
        [LabelText("Source Rigidbody2D")]
        [ShowIf("@Mode == VelocityDrivenRotationCommandMode.SetSource && Source == VelocityRotationSourceKind.Rigidbody2D")]
        public Rigidbody2D? SourceRigidbody2D;

        [BoxGroup("Rotate Channel")]
        [LabelText("Rotate Channel Key")]
        [ShowIf("@Mode == VelocityDrivenRotationCommandMode.SetRotateChannelKey")]
        public string RotateChannelKey = "velocity";

        [BoxGroup("Tilt")]
        [LabelText("Tilt Settings")]
        [ShowIf("@Mode == VelocityDrivenRotationCommandMode.SetTiltSettings")]
        [InlineProperty, HideLabel]
        public TiltSettings Tilt = TiltSettings.Default;

        [BoxGroup("Spin")]
        [LabelText("Spin Settings")]
        [ShowIf("@Mode == VelocityDrivenRotationCommandMode.SetSpinSettings")]
        [InlineProperty, HideLabel]
        public SpinSettings Spin = SpinSettings.Default;

        [BoxGroup("Facing")]
        [LabelText("Facing Settings")]
        [ShowIf("@Mode == VelocityDrivenRotationCommandMode.SetFacingSettings")]
        [InlineProperty, HideLabel]
        public FacingSettings Facing = FacingSettings.Default;
    }
}
