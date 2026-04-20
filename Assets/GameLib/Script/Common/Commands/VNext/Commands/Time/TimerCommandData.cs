#nullable enable
using System;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    public enum TimerCommandMode
    {
        Start = 0,
        Stop = 1,
        Reset = 2,
        SetTime = 3,
        SetTimeScale = 4,
        GetTime = 5,
        AddCurrent = 6,
    }

    [Serializable]
    public sealed class TimerCommandData : ICommandData
    {
        public int CommandId => CommandIds.TimerControl;
        public string DebugData
        {
            get
            {
                var key = string.IsNullOrWhiteSpace(TimerKey) ? "(none)" : TimerKey;
                return $"Timer={key} Mode={Mode}";
            }
        }

        [BoxGroup("Target")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(ActorSource)")]
        public ActorSource ActorSource;

        [BoxGroup("Target")]
        [LabelText("Timer Key")]
        public string TimerKey = "default";

        [BoxGroup("Mode")]
        [LabelText("Mode")]
        [EnumToggleButtons]
        public TimerCommandMode Mode = TimerCommandMode.Start;

        [BoxGroup("Set Time")]
        [ShowIf(nameof(ShowSetTime))]
        [LabelText("Time")]
        public DynamicValue<float> Time;

        [BoxGroup("Set TimeScale")]
        [ShowIf(nameof(ShowSetTimeScale))]
        [LabelText("Time Scale")]
        public DynamicValue<float> TimeScale;

        [BoxGroup("Add Current")]
        [ShowIf(nameof(ShowAddCurrent))]
        [LabelText("Add Value")]
        [Tooltip("Inspector setting.")]
        public DynamicValue<float> AddValue;

        [BoxGroup("Reset")]
        [ShowIf(nameof(ShowResetRestart))]
        [LabelText("Restart After Reset")]
        [Tooltip("Inspector setting.")]
        public bool RestartAfterReset;

        [BoxGroup("Get Time")]
        [ShowIf(nameof(ShowGetTime))]
        [LabelText("Output Target")]
        [EnumToggleButtons]
        public VarStoreTarget OutputTarget = VarStoreTarget.CommandVars;

        [BoxGroup("Get Time")]
        [ShowIf(nameof(ShowGetTime))]
        [LabelText("Output Var")]
        public VarKeyRef OutputVar;

        bool ShowSetTime() => Mode == TimerCommandMode.SetTime;
        bool ShowSetTimeScale() => Mode == TimerCommandMode.SetTimeScale;
        bool ShowAddCurrent() => Mode == TimerCommandMode.AddCurrent;
        bool ShowResetRestart() => Mode == TimerCommandMode.Reset;
        bool ShowGetTime() => Mode == TimerCommandMode.GetTime;
    }
}
