#nullable enable
using System;
using Game.Channel;
using Game.Common;
using Game.TransformSystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    public enum TransformAnimationCommandMode
    {
        Preset = 0,
        Follow = 1,
        Stop = 2,
        Shake = 3,
        Rotate = 4,
    }

    public enum TransformAnimationRotateAction
    {
        Speed = 0,
        Angle = 1,
        StopSpeed = 2,
        StopAngle = 3,
    }

    public enum TransformAnimationRotateSpeedMode
    {
        Override = 0,
        Add = 1,
    }

    public enum TransformAnimationShakeAction
    {
        Play = 0,
        Stop = 1,
    }

    public enum TransformFollowTargetKind
    {
        Transform = 0,
        Position = 1,
    }

    public enum TransformFollowCommandAction
    {
        ApplyFollowSettings = 0,
        SnapToCurrentFollowTarget = 1,
    }

    [Serializable]
    public sealed class TransformAnimationChannelCommandData : ICommandData
    {
        public int CommandId => CommandIds.TransformAnimation;
        public string DebugData
        {
            get
            {
                var tag = string.IsNullOrEmpty(ChannelTag) ? "<none>" : ChannelTag;
                return Mode switch
                {
                    TransformAnimationCommandMode.Follow => $"Tag={tag} Mode=Follow Action={FollowAction}",
                    TransformAnimationCommandMode.Stop => $"Tag={tag} Mode=Stop",
                    TransformAnimationCommandMode.Shake => $"Tag={tag} Mode=Shake Action={ShakeAction}",
                    TransformAnimationCommandMode.Rotate => BuildRotateDebugData(tag),
                    _ => $"Tag={tag} Wait={WaitForCompletion}",
                };
            }
        }

        string BuildRotateDebugData(string tag)
        {
            return RotateAction switch
            {
                TransformAnimationRotateAction.Speed =>
                    $"Tag={tag} Mode=Rotate Action=Speed WriteMode={RotateSpeedMode} Speed={RotateSpeed.GetOrDefaultWithoutContext(Vector3.zero)} Fade={RotateSpeedFadeSeconds.GetOrDefaultWithoutContext(0f)} Damping={RotateSpeedDampingRate.GetOrDefaultWithoutContext(1f)}",
                TransformAnimationRotateAction.Angle =>
                    $"Tag={tag} Mode=Rotate Action=Angle Target={RotateAngleTarget.GetOrDefaultWithoutContext(Vector3.zero)} SmoothTime={RotateAngleSmoothTime.GetOrDefaultWithoutContext(0f)} MaxSpeed={RotateAngleMaxSpeed.GetOrDefaultWithoutContext(0f)}",
                TransformAnimationRotateAction.StopSpeed =>
                    $"Tag={tag} Mode=Rotate Action=StopSpeed Immediate={RotateStopImmediate} Fade={RotateStopFadeSeconds.GetOrDefaultWithoutContext(0f)}",
                TransformAnimationRotateAction.StopAngle =>
                    $"Tag={tag} Mode=Rotate Action=StopAngle Immediate={RotateStopImmediate} Fade={RotateStopFadeSeconds.GetOrDefaultWithoutContext(0f)}",
                _ => $"Tag={tag} Mode=Rotate Action={RotateAction}",
            };
        }

        [BoxGroup("Target")]
        [LabelText("Channel Tag"), LabelWidth(100)]
        public string ChannelTag = "default";

        [BoxGroup("Mode")]
        [LabelText("Mode"), LabelWidth(100)]
        public TransformAnimationCommandMode Mode = TransformAnimationCommandMode.Preset;

        [BoxGroup("Shake")]
        [LabelText("Action"), LabelWidth(100)]
        [ShowIf("@Mode == TransformAnimationCommandMode.Shake")]
        public TransformAnimationShakeAction ShakeAction = TransformAnimationShakeAction.Play;

        [BoxGroup("Shake")]
        [LabelText("Settings"), LabelWidth(100)]
        [ShowIf("@Mode == TransformAnimationCommandMode.Shake && ShakeAction == TransformAnimationShakeAction.Play")]
        [InlineProperty, HideLabel]
        public TransformShakeSettings ShakeSettings = TransformShakeSettings.Default;

        [BoxGroup("Animation")]
        [LabelText("Preset"), LabelWidth(100)]
        [InlineProperty, HideLabel]
        [ShowIf("@Mode == TransformAnimationCommandMode.Preset")]
        public DynamicValue<TransformAnimationPreset> Preset;

        [BoxGroup("Animation")]
        [LabelText("Execution Policy"), LabelWidth(100)]
        [ShowIf("@Mode == TransformAnimationCommandMode.Preset")]
        public TransformPresetExecutionPolicy PresetExecutionPolicy = TransformPresetExecutionPolicy.StopPrevious;

        [BoxGroup("Options")]
        [LabelText("Wait For Completion"), LabelWidth(100)]
        [ShowIf("@Mode == TransformAnimationCommandMode.Preset")]
        public bool WaitForCompletion = true;

        [BoxGroup("Follow")]
        [LabelText("Action"), LabelWidth(100)]
        [ShowIf("@Mode == TransformAnimationCommandMode.Follow")]
        public TransformFollowCommandAction FollowAction = TransformFollowCommandAction.ApplyFollowSettings;

        [BoxGroup("Follow")]
        [LabelText("Await Mode"), LabelWidth(100)]
        [ShowIf("@Mode == TransformAnimationCommandMode.Follow && FollowAction == TransformFollowCommandAction.ApplyFollowSettings")]
        public FlowRunAwaitMode FollowAwaitMode = FlowRunAwaitMode.RunInBackground;

        [BoxGroup("Follow")]
        [LabelText("Target Kind"), LabelWidth(100)]
        [ShowIf("@Mode == TransformAnimationCommandMode.Follow && FollowAction == TransformFollowCommandAction.ApplyFollowSettings")]
        public TransformFollowTargetKind FollowTargetKind = TransformFollowTargetKind.Transform;

        [BoxGroup("Follow")]
        [LabelText("Target Transform"), LabelWidth(100)]
        [ShowIf("@Mode == TransformAnimationCommandMode.Follow && FollowAction == TransformFollowCommandAction.ApplyFollowSettings && FollowTargetKind == TransformFollowTargetKind.Transform")]
        public DynamicValue<Transform> FollowTargetTransform;

        [BoxGroup("Follow")]
        [LabelText("Target Position"), LabelWidth(100)]
        [ShowIf("@Mode == TransformAnimationCommandMode.Follow && FollowAction == TransformFollowCommandAction.ApplyFollowSettings && FollowTargetKind == TransformFollowTargetKind.Position")]
        public DynamicValue<Vector3> FollowTargetPosition;

        [BoxGroup("Follow")]
        [InlineProperty, HideLabel]
        [ShowIf("@Mode == TransformAnimationCommandMode.Follow && FollowAction == TransformFollowCommandAction.ApplyFollowSettings")]
        public TransformFollowOptions FollowOptions = new TransformFollowOptions
        {
            SmoothTime = 0.12f,
            FollowX = true,
            FollowY = true,
            MaxSpeed = 20f,
            UseVelocityOffset = false,
            BaseTargetOffset = Vector3.zero,
            VelocityOffsetScale = Vector2.one,
            VelocityWeight = TransformFollowVelocityWeightSettings.Default,
            LimitTurnRate = false,
            TurnRate = 360f,
        };

        [BoxGroup("Rotate")]
        [LabelText("Action")]
        [ShowIf("@Mode == TransformAnimationCommandMode.Rotate")]
        public TransformAnimationRotateAction RotateAction = TransformAnimationRotateAction.Speed;

        [BoxGroup("Rotate Speed")]
        [LabelText("Speed Write Mode")]
        [ShowIf("@Mode == TransformAnimationCommandMode.Rotate && RotateAction == TransformAnimationRotateAction.Speed")]
        public TransformAnimationRotateSpeedMode RotateSpeedMode = TransformAnimationRotateSpeedMode.Override;

        [BoxGroup("Rotate Speed")]
        [LabelText("Speed")]
        [ShowIf("@Mode == TransformAnimationCommandMode.Rotate && RotateAction == TransformAnimationRotateAction.Speed")]
        public DynamicValue<Vector3> RotateSpeed;

        [BoxGroup("Rotate Speed")]
        [LabelText("Fade Seconds")]
        [ShowIf("@Mode == TransformAnimationCommandMode.Rotate && RotateAction == TransformAnimationRotateAction.Speed")]
        public DynamicValue<float> RotateSpeedFadeSeconds = DynamicValue<float>.FromSource(new LiteralFloatSource(0f));

        [BoxGroup("Rotate Speed")]
        [LabelText("Damping Rate")]
        [Tooltip("Inspector setting.")]
        [ShowIf("@Mode == TransformAnimationCommandMode.Rotate && RotateAction == TransformAnimationRotateAction.Speed")]
        public DynamicValue<float> RotateSpeedDampingRate = DynamicValue<float>.FromSource(new LiteralFloatSource(1f));

        [BoxGroup("Rotate Angle")]
        [LabelText("Target Euler")]
        [ShowIf("@Mode == TransformAnimationCommandMode.Rotate && RotateAction == TransformAnimationRotateAction.Angle")]
        public DynamicValue<Vector3> RotateAngleTarget;

        [BoxGroup("Rotate Angle")]
        [LabelText("Smooth Time")]
        [Tooltip("Inspector setting.")]
        [ShowIf("@Mode == TransformAnimationCommandMode.Rotate && RotateAction == TransformAnimationRotateAction.Angle")]
        public DynamicValue<float> RotateAngleSmoothTime = DynamicValue<float>.FromSource(new LiteralFloatSource(0.12f));

        [BoxGroup("Rotate Angle")]
        [LabelText("Max Speed")]
        [Tooltip("Inspector setting.")]
        [ShowIf("@Mode == TransformAnimationCommandMode.Rotate && RotateAction == TransformAnimationRotateAction.Angle")]
        public DynamicValue<float> RotateAngleMaxSpeed = DynamicValue<float>.FromSource(new LiteralFloatSource(0f));

        [BoxGroup("Rotate Stop")]
        [LabelText("Stop Immediate")]
        [ShowIf("@Mode == TransformAnimationCommandMode.Rotate && (RotateAction == TransformAnimationRotateAction.StopSpeed || RotateAction == TransformAnimationRotateAction.StopAngle)")]
        public bool RotateStopImmediate = true;

        [BoxGroup("Rotate Stop")]
        [LabelText("Stop Fade Seconds")]
        [ShowIf("@Mode == TransformAnimationCommandMode.Rotate && (RotateAction == TransformAnimationRotateAction.StopSpeed || RotateAction == TransformAnimationRotateAction.StopAngle)")]
        public DynamicValue<float> RotateStopFadeSeconds = DynamicValue<float>.FromSource(new LiteralFloatSource(0f));
    }
}
