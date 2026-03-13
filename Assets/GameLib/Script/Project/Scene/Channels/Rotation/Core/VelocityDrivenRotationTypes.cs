#nullable enable
using System;
using Game.Scalar;
using Game.Scalar.Generated;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Rotation
{
    public enum VelocityRotationMode
    {
        /// <summary>速度に応じてオブジェクトを傾ける回転モード</summary>
        Tilt = 0,
        /// <summary>速度に応じてオブジェクトをスピン（回転）させるモード</summary>
        Spin = 1,
        /// <summary>速度の方向に向くようにオブジェクトを回転させるモード</summary>
        Facing = 2,
    }

    public enum VelocityRotationSourceKind
    {
        TransformController = 0,
        Rigidbody2D = 1,
    }

    public enum TiltReferenceMode
    {
        Absolute = 0,
        Relative = 1,
    }

    [Serializable]
    public struct TiltSettings
    {
        [LabelText("Reference Mode")] public TiltReferenceMode ReferenceMode;
        [LabelText("Base Euler")] public Vector3 BaseEuler;

        [LabelText("Axis Weight +")] public Vector2 AxisWeightPos;
        [LabelText("Axis Weight -")] public Vector2 AxisWeightNeg;

        [LabelText("Max Tilt Angle"), Tooltip("負値を入れると傾き方向を反転します。絶対値が角度上限として使われます。")] public float MaxTiltAngle;
        [LabelText("Speed To Tilt"), MinValue(0f)] public float SpeedToTilt;

        [LabelText("Lerp Speed"), MinValue(0f)] public float LerpSpeed;
        [LabelText("Error Boost"), MinValue(0f)] public float ErrorBoost;
        [LabelText("Error Boost Max"), MinValue(0f)] public float ErrorBoostMax;
        [LabelText("Max Angular Velocity"), MinValue(0f)] public float MaxAngularVelocity;

        public static TiltSettings Default => new TiltSettings
        {
            ReferenceMode = TiltReferenceMode.Relative,
            BaseEuler = Vector3.zero,
            AxisWeightPos = Vector2.one,
            AxisWeightNeg = Vector2.one,
            MaxTiltAngle = 20f,
            SpeedToTilt = 1f,
            LerpSpeed = 6f,
            ErrorBoost = 0f,
            ErrorBoostMax = 0f,
            MaxAngularVelocity = 0f,
        };
    }

    [Serializable]
    public struct SpinSettings
    {
        [LabelText("Max Angular Velocity"), MinValue(0f)] public float MaxAngularVelocity;
        [LabelText("Speed To Angular"), MinValue(0f)] public float SpeedToAngular;
        [LabelText("Lerp Speed"), MinValue(0f)] public float LerpSpeed;
        [LabelText("Use Signed Direction")] public bool UseSignedDirection;
        [LabelText("Direction Axis")] public Vector2 DirectionAxis;

        public static SpinSettings Default => new SpinSettings
        {
            MaxAngularVelocity = 360f,
            SpeedToAngular = 90f,
            LerpSpeed = 0f,
            UseSignedDirection = true,
            DirectionAxis = Vector2.right,
        };
    }

    [Serializable]
    public struct FacingSettings
    {
        [LabelText("Base Angle")] public float BaseAngle;
        [LabelText("Disable Lerp")] public bool DisableLerp;
        [LabelText("Lerp Speed"), MinValue(0f), ShowIf("@!DisableLerp")] public float LerpSpeed;
        [LabelText("Max Angular Velocity"), MinValue(0f)] public float MaxAngularVelocity;

        [LabelText("Flip When Negative X")] public bool FlipWhenNegativeX;
        [LabelText("Flip Duration"), MinValue(0f), ShowIf(nameof(FlipWhenNegativeX))] public float FlipDuration;
        [LabelText("Flip Target"), ShowIf(nameof(FlipWhenNegativeX))] public Transform? FlipTarget;

        public static FacingSettings Default => new FacingSettings
        {
            BaseAngle = 0f,
            DisableLerp = false,
            LerpSpeed = 6f,
            MaxAngularVelocity = 360f,
            FlipWhenNegativeX = true,
            FlipDuration = 0.15f,
            FlipTarget = null,
        };
    }

    [Serializable]
    public struct VelocityRotationSettings
    {
        [LabelText("Enabled")] public bool Enabled;

        [LabelText("Rotate Channel Key")] public string RotateChannelKey;
        [LabelText("Mode")] public VelocityRotationMode Mode;

        [LabelText("Velocity Source")] public VelocityRotationSourceKind Source;
        [LabelText("Source Transform")] public Transform? SourceTransform;
        [LabelText("Source Rigidbody2D"), ShowIf("@Source == VelocityRotationSourceKind.Rigidbody2D")] public Rigidbody2D? SourceRigidbody2D;

        [LabelText("Speed Scale"), MinValue(0f)] public float SpeedScale;
        [LabelText("Use Scalar Speed Scale")] public bool UseScalarSpeedScale;
        [LabelText("Speed Scale Scalar"), ShowIf("UseScalarSpeedScale")] public ScalarKey SpeedScaleScalar;

        [LabelText("Tilt Settings"), ShowIf("@Mode == VelocityRotationMode.Tilt"), InlineProperty] public TiltSettings Tilt;
        [LabelText("Spin Settings"), ShowIf("@Mode == VelocityRotationMode.Spin"), InlineProperty] public SpinSettings Spin;
        [LabelText("Facing Settings"), ShowIf("@Mode == VelocityRotationMode.Facing"), InlineProperty] public FacingSettings Facing;

        public static VelocityRotationSettings Default => new VelocityRotationSettings
        {
            Enabled = false,
            RotateChannelKey = "velocity",
            Mode = VelocityRotationMode.Tilt,
            Source = VelocityRotationSourceKind.TransformController,
            SourceTransform = null,
            SourceRigidbody2D = null,
            SpeedScale = 1f,
            UseScalarSpeedScale = false,
            SpeedScaleScalar = ScalarKeys.GameLib.Movement.SpeedMultiplier,
            Tilt = TiltSettings.Default,
            Spin = SpinSettings.Default,
            Facing = FacingSettings.Default,
        };
    }

    public interface IVelocityRotationSettingsAdapter
    {
        VelocityRotationSettings CurrentSettings { get; }
        void ApplySettings(in VelocityRotationSettings settings);
    }
}
