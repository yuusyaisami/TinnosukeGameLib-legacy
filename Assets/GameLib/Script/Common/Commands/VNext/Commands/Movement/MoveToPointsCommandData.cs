#nullable enable
using System;
using System.Collections.Generic;
using Game.Common;
using Game.Movement;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    public enum MoveToPointSpace
    {
        World = 0,
        RelativeToAgent = 1,
    }

    public enum MoveToPointAwaitMode
    {
        WaitForCompletion = 0,
        RunInBackground = 1,
    }

    public enum MoveToPointFinishMode
    {
        FinishOnLastPoint = 0,
        Loop = 1,
        PingPong = 2,
    }

    [Serializable]
    public sealed class MoveToPointSequenceConfig
    {
        [LabelText("Await Mode")]
        public MoveToPointAwaitMode AwaitMode = MoveToPointAwaitMode.WaitForCompletion;

        [LabelText("Finish Mode")]
        public MoveToPointFinishMode FinishMode = MoveToPointFinishMode.FinishOnLastPoint;

        [LabelText("Cancel Existing Target")]
        public bool CancelExistingTarget = true;

        [LabelText("Clear Target On Cancel")]
        public bool ClearTargetOnCancel = true;

        [LabelText("Clear Target On Complete")]
        public bool ClearTargetOnComplete;
    }

    [Serializable]
    public sealed class MoveToPointRequestOverrides
    {
        [LabelText("Input Type")]
        public MovementInputType InputType = MovementInputType.AI;

        [LabelText("Override Speed")]
        public bool OverrideSpeed;

        [ShowIf(nameof(OverrideSpeed))]
        [LabelText("Speed")]
        public DynamicValue<float> Speed;

        [LabelText("Arc Seed")]
        public int ArcSeed;
    }

    [Serializable]
    public struct MoveToPointEntry
    {
        [LabelText("Point"), LabelWidth(40)]
        public DynamicValue<Vector2> Point;

        [LabelText("Space")]
        public MoveToPointSpace Space;

        [LabelText("Repath"), LabelWidth(40)]
        public bool ForceRepath;

        [InlineProperty]
        [HideLabel]
        public CommandListData OnArriveCommands;
    }

    [Serializable]
    public sealed class MoveToPointsCommandData : ICommandData
    {
        public int CommandId => CommandIds.MoveToPoints;
        public string DebugData
        {
            get
            {
                var points = Points?.Count ?? 0;
                var awaitMode = Sequence?.AwaitMode.ToString() ?? "None";
                var finishMode = Sequence?.FinishMode.ToString() ?? "None";
                return $"Points={points} Await={awaitMode} Finish={finishMode}";
            }
        }

        [BoxGroup("MoveTo")]
        [InlineProperty]
        [HideLabel]
        [SerializeField]
        public MoveToPointSequenceConfig Sequence = new();

        [BoxGroup("MoveTo")]
        [InlineProperty]
        [HideLabel]
        [SerializeField]
        public MoveToPointRequestOverrides Request = new();

        [BoxGroup("Points")]
        [TableList(AlwaysExpanded = true)]
        [SerializeField]
        public List<MoveToPointEntry> Points = new();

#if UNITY_EDITOR
        [BoxGroup("Editor Preview")]
        [LabelText("Preview Origin (Relative)")]
        [SerializeField]
        public Vector2 PreviewOrigin;
#endif
    }
}
