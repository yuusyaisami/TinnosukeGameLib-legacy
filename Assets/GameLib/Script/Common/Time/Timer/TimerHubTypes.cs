#nullable enable
using System;
using Game.Common;
using Game.Scalar;
using Game.Commands.VNext;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Times
{
    public enum TimerDeltaMode
    {
        Scaled = 0,
        Unscaled = 1,
    }

    public enum TimerDirection
    {
        Up = 0,
        Down = 1,
    }

    public enum TimerOutputKind
    {
        None = 0,
        Scalar = 1,
        Blackboard = 2,
    }

    public enum TimerScalarWriteScope
    {
        Local = 0,
        Global = 1,
    }

    public enum TimerBlackboardWriteScope
    {
        Local = 0,
        Global = 1,
    }

    [Serializable]
    public struct TimerOutputTarget
    {
        [SerializeField, LabelText("Output")]
        TimerOutputKind kind;

        [ShowIf(nameof(IsScalar))]
        [SerializeField, LabelText("Scalar Key")]
        ScalarKey scalarKey;

        [ShowIf(nameof(IsScalar))]
        [SerializeField, LabelText("Scalar Scope")]
        TimerScalarWriteScope scalarScope;

        [ShowIf(nameof(IsBlackboard))]
        [SerializeField, LabelText("Blackboard VarId"), VarIdDropdown]
        int blackboardVarId;

        [ShowIf(nameof(IsBlackboard))]
        [SerializeField, LabelText("Blackboard Scope")]
        TimerBlackboardWriteScope blackboardScope;

        public TimerOutputKind Kind => kind;
        public ScalarKey ScalarKey => scalarKey;
        public TimerScalarWriteScope ScalarScope => scalarScope;
        public int BlackboardVarId => blackboardVarId;
        public TimerBlackboardWriteScope BlackboardScope => blackboardScope;

        bool IsScalar() => kind == TimerOutputKind.Scalar;
        bool IsBlackboard() => kind == TimerOutputKind.Blackboard;
    }

    [Serializable]
    public sealed class TimerChannelDef
    {
        [LabelText("Key")]
        public string Key = "default";

        [LabelText("Initial Time")]
        public float InitialTime = 0f;

        [LabelText("Auto Start")]
        public bool AutoStart = false;

        [LabelText("Delta Mode")]
        public TimerDeltaMode DeltaMode = TimerDeltaMode.Scaled;

        [LabelText("Direction")]
        public TimerDirection Direction = TimerDirection.Up;

        [LabelText("Time Scale")]
        public float TimeScale = 1f;

        [LabelText("Min Time")]
        public float MinTime = float.NegativeInfinity;

        [LabelText("Max Time")]
        public float MaxTime = float.PositiveInfinity;

        [LabelText("Output")]
        public TimerOutputTarget Output;

        [LabelText("Triggers")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = false)]
        public TimerTriggerEntry[] Triggers = Array.Empty<TimerTriggerEntry>();
    }

    public interface ITimerHubSettings
    {
        TimerChannelDef[] Timers { get; }
        bool AutoInitializeOnStart { get; }
    }

    [Serializable]
    public sealed class TimerTriggerEntry
    {
        [LabelText("Time (sec)")]
        public float TimeSeconds = 0f;

        [LabelText("Commands")]
        public CommandListData Commands = new CommandListData();
    }
}
