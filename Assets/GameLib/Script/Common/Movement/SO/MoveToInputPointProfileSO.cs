#nullable enable
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Movement
{
    [CreateAssetMenu(menuName = "Game/Movement/MoveToInputPoint Profile", fileName = "MoveToInputPointProfile")]
    public sealed class MoveToInputPointProfileSO : ScriptableObject
    {
        // 存在してないことになってる。
        [BoxGroup("Base")]
        [LabelText("Default Speed")]
        [MinValue(0f)]
        public float DefaultSpeed = 4f;

        [BoxGroup("Base")]
        [LabelText("Arrival Distance")]
        [MinValue(0.001f)]
        public float ArrivalDistance = 0.25f;

        [BoxGroup("Base")]
        [LabelText("Waypoint Advance Distance")]
        [MinValue(0.001f)]
        public float WaypointAdvanceDistance = 0.35f;

        [BoxGroup("Base")]
        [LabelText("LookAhead Distance")]
        [MinValue(0.001f)]
        public float LookAheadDistance = 1.2f;

        [BoxGroup("Repath")]
        [LabelText("Repath Interval (frames)")]
        [MinValue(1)]
        public int RepathIntervalFrames = 8;

        [BoxGroup("Repath")]
        [LabelText("Stuck Speed Epsilon")]
        [MinValue(0f)]
        public float StuckSpeedEpsilon = 0.05f;

        [BoxGroup("Repath")]
        [LabelText("Stuck Frames To Repath")]
        [MinValue(1)]
        public int StuckFramesToRepath = 20;

        [BoxGroup("Arc")]
        [LabelText("Allow Arc On Clear Line")]
        public bool AllowArcOnClearLine = true;

        [BoxGroup("Arc")]
        [LabelText("Arc Max Offset")]
        [ShowIf(nameof(AllowArcOnClearLine))]
        [MinValue(0f)]
        public float ArcMaxOffset = 1.25f;

        [BoxGroup("Arc")]
        [LabelText("Arc Offset Factor")]
        [ShowIf(nameof(AllowArcOnClearLine))]
        [MinValue(0f)]
        public float ArcOffsetFactor = 0.15f;

        [BoxGroup("Physics")]
        [LabelText("Agent Radius")]
        [MinValue(0.001f)]
        public float AgentRadius = 0.35f;

        [BoxGroup("Physics")]
        [LabelText("Obstacle Mask")]
        public LayerMask ObstacleMask;

        [BoxGroup("Physics")]
        [LabelText("Use Triggers")]
        public bool UseTriggers = false;

        [BoxGroup("Planner")]
        [LabelText("Detour Clearance")]
        [MinValue(0f)]
        public float DetourClearance = 0.2f;

        [BoxGroup("Steering")]
        [LabelText("Probe Distance")]
        [MinValue(0.01f)]
        public float ProbeDistance = 1.2f;

        [BoxGroup("Steering")]
        [LabelText("Max Avoid Angle (deg)")]
        [Range(5f, 179f)]
        public float MaxAvoidAngleDeg = 70f;

        [BoxGroup("Steering")]
        [LabelText("Samples Per Side")]
        [MinValue(1)]
        public int SamplesPerSide = 6;

        [BoxGroup("Steering")]
        [LabelText("Alignment Weight")]
        [MinValue(0f)]
        public float AlignmentWeight = 1.0f;

        [BoxGroup("Steering")]
        [LabelText("Obstacle Weight")]
        [MinValue(0f)]
        public float ObstacleWeight = 2.0f;

        [BoxGroup("Steering")]
        [LabelText("Side Bias")]
        [Range(-0.5f, 0.5f)]
        public float SideBias = 0.08f;

        [BoxGroup("Debug")]
        [LabelText("Debug Flags")]
        public MoveToInputPointDebugFlags DebugFlags = MoveToInputPointDebugFlags.None;

        public MoveToInputPointRequest ToRequest(float? overrideSpeed = null, int arcSeed = 0)
        {
            return new MoveToInputPointRequest(
                speed: overrideSpeed ?? DefaultSpeed,
                arrivalDistance: ArrivalDistance,
                waypointAdvanceDistance: WaypointAdvanceDistance,
                lookAheadDistance: LookAheadDistance,
                repathIntervalFrames: RepathIntervalFrames,
                stuckSpeedEpsilon: StuckSpeedEpsilon,
                stuckFramesToRepath: StuckFramesToRepath,
                stopOnArrive: true,
                allowArcOnClearLine: AllowArcOnClearLine,
                arcMaxOffset: ArcMaxOffset,
                arcOffsetFactor: ArcOffsetFactor,
                arcSeed: arcSeed,
                obstacleMask: ObstacleMask,
                useTriggers: UseTriggers,
                debugFlags: DebugFlags
            );
        }
    }
}
