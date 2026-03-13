#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Movement
{
    public readonly struct MoveToInputPointPlanContext
    {
        public readonly Vector2 Start;
        public readonly Vector2 Goal;

        public readonly float AgentRadius;
        public readonly float DetourClearance;

        public readonly LayerMask ObstacleMask;
        public readonly bool UseTriggers;

        public readonly bool AllowArcOnClearLine;
        public readonly float ArcMaxOffset;
        public readonly float ArcOffsetFactor;
        public readonly int ArcSeed;

        public MoveToInputPointPlanContext(
            Vector2 start,
            Vector2 goal,
            float agentRadius,
            float detourClearance,
            LayerMask obstacleMask,
            bool useTriggers,
            bool allowArcOnClearLine,
            float arcMaxOffset,
            float arcOffsetFactor,
            int arcSeed)
        {
            Start = start;
            Goal = goal;

            AgentRadius = agentRadius;
            DetourClearance = detourClearance;

            ObstacleMask = obstacleMask;
            UseTriggers = useTriggers;

            AllowArcOnClearLine = allowArcOnClearLine;
            ArcMaxOffset = arcMaxOffset;
            ArcOffsetFactor = arcOffsetFactor;
            ArcSeed = arcSeed;
        }
    }

    public interface IMoveToInputPointPathPlanner2D
    {
        bool Plan(in MoveToInputPointPlanContext ctx, List<Vector2> outWaypoints);
    }

    public readonly struct MoveToInputPointSteeringContext
    {
        public readonly Vector2 CurrentPos;
        public readonly Vector2 DesiredDir;

        public readonly float Speed;

        public readonly float AgentRadius;
        public readonly float ProbeDistance;

        public readonly float MaxAvoidAngleDeg;
        public readonly int SamplesPerSide;

        public readonly float AlignmentWeight;
        public readonly float ObstacleWeight;
        public readonly float SideBias;

        public readonly LayerMask ObstacleMask;
        public readonly bool UseTriggers;

        public MoveToInputPointSteeringContext(
            Vector2 currentPos,
            Vector2 desiredDir,
            float speed,
            float agentRadius,
            float probeDistance,
            float maxAvoidAngleDeg,
            int samplesPerSide,
            float alignmentWeight,
            float obstacleWeight,
            float sideBias,
            LayerMask obstacleMask,
            bool useTriggers)
        {
            CurrentPos = currentPos;
            DesiredDir = desiredDir;
            Speed = speed;

            AgentRadius = agentRadius;
            ProbeDistance = probeDistance;

            MaxAvoidAngleDeg = maxAvoidAngleDeg;
            SamplesPerSide = samplesPerSide;

            AlignmentWeight = alignmentWeight;
            ObstacleWeight = obstacleWeight;
            SideBias = sideBias;

            ObstacleMask = obstacleMask;
            UseTriggers = useTriggers;
        }
    }

    public interface IMoveToInputPointSteering2D
    {
        Vector2 Compute(in MoveToInputPointSteeringContext ctx);
    }
}
