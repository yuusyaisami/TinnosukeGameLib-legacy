#nullable enable
using System;
using DG.Tweening;
using Game.Common;
using Game.Times;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    public enum TimeScaleCommandMode
    {
        Immediate = 0,
        Animate = 1,
    }

    public enum TimeScaleTemporaryRestoreMode
    {
        Immediate = 0,
        Animate = 1,
    }

    public enum TimeScaleTemporaryRestoreTargetMode
    {
        Previous = 0,
        Custom = 1,
    }

    [Serializable]
    public sealed class TimeScaleCommandData : ICommandData
    {
        public int CommandId => CommandIds.SetTimeScale;
        public string DebugData
        {
            get
            {
                var scale = CommandDebugDataHelper.GetDynamicDebugData(Scale);
                if (Mode == TimeScaleCommandMode.Animate)
                {
                    var duration = CommandDebugDataHelper.GetDynamicDebugData(Duration);
                    if (UseTemporaryDuration)
                    {
                        var temporary = CommandDebugDataHelper.GetDynamicDebugData(TemporaryDurationSeconds);
                        return $"Kind={Kind} Mode=Animate Scale={scale} Duration={duration} Temporary={temporary}";
                    }
                    return $"Kind={Kind} Mode=Animate Scale={scale} Duration={duration}";
                }
                if (UseTemporaryDuration)
                {
                    var temporary = CommandDebugDataHelper.GetDynamicDebugData(TemporaryDurationSeconds);
                    return $"Kind={Kind} Mode=Immediate Scale={scale} Temporary={temporary}";
                }
                return $"Kind={Kind} Mode=Immediate Scale={scale}";
            }
        }

        [BoxGroup("Target")]
        [LabelText("Kind")]
        [SerializeField]
        public TimeScaleKind Kind = TimeScaleKind.GamePlay;

        [BoxGroup("Scale")]
        [LabelText("Scale")]
        [SerializeField]
        public DynamicValue<float> Scale;

        [BoxGroup("Mode")]
        [LabelText("Mode")]
        [EnumToggleButtons]
        [SerializeField]
        public TimeScaleCommandMode Mode = TimeScaleCommandMode.Immediate;

        [BoxGroup("Animation")]
        [LabelText("Duration")]
        [ShowIf(nameof(IsAnimate))]
        [SerializeField]
        public DynamicValue<float> Duration;

        [BoxGroup("Animation")]
        [LabelText("Ease")]
        [ShowIf(nameof(IsAnimate))]
        [SerializeField]
        public Ease Ease = Ease.OutQuad;

        [BoxGroup("Temporary")]
        [LabelText("Use Temporary Duration")]
        [SerializeField]
        public bool UseTemporaryDuration;

        [BoxGroup("Temporary")]
        [LabelText("Duration (Realtime sec)")]
        [ShowIf(nameof(UseTemporaryDuration))]
        [SerializeField]
        public DynamicValue<float> TemporaryDurationSeconds;

        [BoxGroup("Temporary Restore")]
        [LabelText("Restore Mode")]
        [ShowIf(nameof(UseTemporaryDuration))]
        [SerializeField]
        public TimeScaleTemporaryRestoreMode TemporaryRestoreMode = TimeScaleTemporaryRestoreMode.Immediate;

        [BoxGroup("Temporary Restore")]
        [LabelText("Restore Target")]
        [ShowIf(nameof(UseTemporaryDuration))]
        [SerializeField]
        public TimeScaleTemporaryRestoreTargetMode TemporaryRestoreTarget = TimeScaleTemporaryRestoreTargetMode.Previous;

        [BoxGroup("Temporary Restore")]
        [LabelText("Restore Scale")]
        [ShowIf("@UseTemporaryDuration && TemporaryRestoreTarget == TimeScaleTemporaryRestoreTargetMode.Custom")]
        [SerializeField]
        public DynamicValue<float> TemporaryRestoreScale;

        [BoxGroup("Temporary Restore")]
        [LabelText("Restore Duration")]
        [ShowIf("@UseTemporaryDuration && TemporaryRestoreMode == TimeScaleTemporaryRestoreMode.Animate")]
        [SerializeField]
        public DynamicValue<float> TemporaryRestoreDurationSeconds;

        [BoxGroup("Temporary Restore")]
        [LabelText("Restore Ease")]
        [ShowIf("@UseTemporaryDuration && TemporaryRestoreMode == TimeScaleTemporaryRestoreMode.Animate")]
        [SerializeField]
        public Ease TemporaryRestoreEase = Ease.OutQuad;

        bool IsAnimate => Mode == TimeScaleCommandMode.Animate;
    }
}
