#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Movement
{
    public enum MoveToInputPointState
    {
        None = 0,
        Moving = 1,
        Arrived = 2,
        Blocked = 3,
    }

    [Flags]
    public enum MoveToInputPointDebugFlags
    {
        None = 0,
        DrawGizmos = 1 << 0,
        Log = 1 << 1,
    }

    public readonly struct MoveToInputPointRequest
    {
        public readonly float Speed;
        public readonly float ArrivalDistance;
        public readonly float WaypointAdvanceDistance;
        public readonly float LookAheadDistance;

        public readonly int RepathIntervalFrames;
        public readonly float StuckSpeedEpsilon;
        public readonly int StuckFramesToRepath;

        public readonly bool StopOnArrive;
        public readonly bool AllowArcOnClearLine;
        public readonly float ArcMaxOffset;
        public readonly float ArcOffsetFactor;
        public readonly int ArcSeed;

        public readonly LayerMask ObstacleMask;
        public readonly bool UseTriggers;

        public readonly MoveToInputPointDebugFlags DebugFlags;

        /// <summary>
        /// 移動入力の種別。ActionBlock と連携してブロック判定に使用。
        /// </summary>
        public readonly MovementInputType InputType;

        public MoveToInputPointRequest(
            float speed,
            float arrivalDistance = 0.25f,
            float waypointAdvanceDistance = 0.35f,
            float lookAheadDistance = 1.2f,
            int repathIntervalFrames = 8,
            float stuckSpeedEpsilon = 0.05f,
            int stuckFramesToRepath = 20,
            bool stopOnArrive = true,
            bool allowArcOnClearLine = true,
            float arcMaxOffset = 1.25f,
            float arcOffsetFactor = 0.15f,
            int arcSeed = 0,
            LayerMask obstacleMask = default,
            bool useTriggers = false,
            MoveToInputPointDebugFlags debugFlags = MoveToInputPointDebugFlags.None,
            MovementInputType inputType = MovementInputType.None)
        {
            Speed = speed;
            ArrivalDistance = arrivalDistance;
            WaypointAdvanceDistance = waypointAdvanceDistance;
            LookAheadDistance = lookAheadDistance;

            RepathIntervalFrames = repathIntervalFrames;
            StuckSpeedEpsilon = stuckSpeedEpsilon;
            StuckFramesToRepath = stuckFramesToRepath;

            StopOnArrive = stopOnArrive;
            AllowArcOnClearLine = allowArcOnClearLine;
            ArcMaxOffset = arcMaxOffset;
            ArcOffsetFactor = arcOffsetFactor;
            ArcSeed = arcSeed;

            ObstacleMask = obstacleMask;
            UseTriggers = useTriggers;

            DebugFlags = debugFlags;
            InputType = inputType;
        }
    }

    public readonly struct MoveToInputPointOutput
    {
        public readonly MoveToInputPointState State;
        public readonly Vector2 DesiredDirection;
        public readonly Vector2 DesiredVelocity;
        public readonly Vector2 LookAheadPoint;
        public readonly Vector2 FinalTarget;
        public readonly int PathVersion;
        public readonly float DistanceToTarget;

        public bool HasTarget => State != MoveToInputPointState.None;

        public MoveToInputPointOutput(
            MoveToInputPointState state,
            Vector2 desiredDirection,
            Vector2 desiredVelocity,
            Vector2 lookAheadPoint,
            Vector2 finalTarget,
            int pathVersion,
            float distanceToTarget)
        {
            State = state;
            DesiredDirection = desiredDirection;
            DesiredVelocity = desiredVelocity;
            LookAheadPoint = lookAheadPoint;
            FinalTarget = finalTarget;
            PathVersion = pathVersion;
            DistanceToTarget = distanceToTarget;
        }
    }

    public interface IMoveToInputPointService : IDisposable
    {
        bool HasTarget { get; }
        Vector2 Target { get; }
        int PathVersion { get; }
        MovementInputType CurrentRequestInputType { get; }

        void GetPathNonAlloc(List<Vector2> dst);

        void SetTarget(Vector2 target, in MoveToInputPointRequest request, bool forceRepath = true);

        void ClearTarget(bool clearPath = true);

        MoveToInputPointOutput Tick(float deltaTime, Vector2 currentPos, Vector2 currentVelocity);
    }
}
