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
                    _ => $"Tag={tag} Wait={WaitForCompletion}",
                };
            }
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
    }
}
