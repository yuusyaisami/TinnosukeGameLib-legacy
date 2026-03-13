#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Scalar;
using Game.Input;
using Game.ActionBlock.Keys;
using Game.Scalar.Generated;

namespace Game.Movement
{
    public sealed class MoveToInputPointService : IMoveToInputPointService
    {
        readonly Game.Movement.MoveToInputPointProfileSO _profile;
        readonly IMoveToInputPointPathPlanner2D _planner;
        readonly IMoveToInputPointSteering2D _steering;
        readonly IBaseScalarService? _scalarService;
        readonly IActionBlockService? _actionBlockService;

        readonly List<Vector2> _path = new(8);
        readonly List<Vector2> _pathCopyTmp = new(8);

        bool _hasTarget;
        Vector2 _target;
        MoveToInputPointRequest _req;

        int _pathVersion;
        int _waypointIndex;

        int _lastRepathFrame = int.MinValue;
        int _stuckCounter;

        bool _disposed;

        public MoveToInputPointService(
            Game.Movement.MoveToInputPointProfileSO profile,
            IMoveToInputPointPathPlanner2D? planner = null,
            IMoveToInputPointSteering2D? steering = null,
            IBaseScalarService? scalarService = null,
            IActionBlockService? actionBlockService = null)
        {
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _planner = planner ?? new PhysicsDetourPlanner2D();
            _steering = steering ?? new ScoringSteering2D();
            _scalarService = scalarService;
            _actionBlockService = actionBlockService;
        }

        public bool HasTarget => _hasTarget;
        public Vector2 Target => _target;
        public int PathVersion => _pathVersion;
        public MovementInputType CurrentRequestInputType => _req.InputType;

        public void GetPathNonAlloc(List<Vector2> dst)
        {
            if (dst == null) throw new ArgumentNullException(nameof(dst));
            dst.Clear();
            int count = _path.Count;
            if (count == 0) return;
            if (dst.Capacity < count) dst.Capacity = count;
            for (int i = 0; i < count; i++) dst.Add(_path[i]);
        }

        public void SetTarget(Vector2 target, in MoveToInputPointRequest request, bool forceRepath = true)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MoveToInputPointService));

            _hasTarget = true;
            _target = target;
            _req = request;

            if (forceRepath)
            {
                InvalidatePath();
            }
        }

        public void ClearTarget(bool clearPath = true)
        {
            _hasTarget = false;
            if (clearPath)
            {
                _path.Clear();
                _waypointIndex = 0;
            }
        }

        public MoveToInputPointOutput Tick(float deltaTime, Vector2 currentPos, Vector2 currentVelocity)
        {
            if (_disposed) return new MoveToInputPointOutput(MoveToInputPointState.None, Vector2.zero, Vector2.zero, currentPos, currentPos, _pathVersion, 0f);
            if (!_hasTarget) return new MoveToInputPointOutput(MoveToInputPointState.None, Vector2.zero, Vector2.zero, currentPos, currentPos, _pathVersion, 0f);

            // ActionBlock によるブロック判定
            if (IsInputTypeBlocked(_req.InputType))
            {
                float distBlocked = Vector2.Distance(currentPos, _target);
                return new MoveToInputPointOutput(MoveToInputPointState.Blocked, Vector2.zero, Vector2.zero, currentPos, _target, _pathVersion, distBlocked);
            }

            float distToTarget = Vector2.Distance(currentPos, _target);

            if (distToTarget <= Mathf.Max(0.001f, _req.ArrivalDistance))
            {
                if (_req.StopOnArrive)
                {
                    ClearTarget(clearPath: true);
                    return new MoveToInputPointOutput(MoveToInputPointState.Arrived, Vector2.zero, Vector2.zero, _target, _target, _pathVersion, distToTarget);
                }

                return new MoveToInputPointOutput(MoveToInputPointState.Arrived, Vector2.zero, Vector2.zero, _target, _target, _pathVersion, distToTarget);
            }

            int frame = Time.frameCount;
            if (NeedRepath(frame, currentVelocity))
            {
                Repath(currentPos, _target, frame);
            }

            if (_path.Count == 0)
            {
                Repath(currentPos, _target, frame);
                if (_path.Count == 0) _path.Add(_target);
            }

            AdvanceWaypointIndex(currentPos);

            Vector2 lookAhead = ComputeLookAheadPoint(currentPos);

            Vector2 desiredDir = lookAhead - currentPos;
            float lenSq = desiredDir.sqrMagnitude;
            if (lenSq < 0.000001f)
            {
                return new MoveToInputPointOutput(MoveToInputPointState.Moving, Vector2.zero, Vector2.zero, lookAhead, _target, _pathVersion, distToTarget);
            }
            desiredDir /= Mathf.Sqrt(lenSq);

            float speed = Mathf.Max(0f, _req.Speed);
            if (_scalarService != null)
            {
                if (speed <= 0.0001f &&
                    _scalarService.GlobalTryGet(ScalarKeys.GameLib.Movement.DefaultSpeed, out var scalarSpeed))
                {
                    speed = Mathf.Max(0f, scalarSpeed);
                }
                if (_req.InputType == MovementInputType.Player &&
                    _scalarService.GlobalTryGet(ScalarKeys.GameLib.Movement.SpeedMultiplier, out var speedMul))
                {
                    speed = Mathf.Max(0f, speed * speedMul);
                }
            }

            var steerCtx = new MoveToInputPointSteeringContext(
                currentPos: currentPos,
                desiredDir: desiredDir,
                speed: speed,
                agentRadius: _profile.AgentRadius,
                probeDistance: _profile.ProbeDistance,
                maxAvoidAngleDeg: _profile.MaxAvoidAngleDeg,
                samplesPerSide: _profile.SamplesPerSide,
                alignmentWeight: _profile.AlignmentWeight,
                obstacleWeight: _profile.ObstacleWeight,
                sideBias: _profile.SideBias,
                obstacleMask: _req.ObstacleMask,
                useTriggers: _req.UseTriggers
            );

            Vector2 steerDir = _steering.Compute(in steerCtx);
            if (steerDir.sqrMagnitude < 0.000001f)
                steerDir = desiredDir;

            Vector2 vel = steerDir * Mathf.Max(0f, speed);

            if ((_req.DebugFlags & MoveToInputPointDebugFlags.DrawGizmos) != 0)
            {
                DebugDraw(currentPos, lookAhead, desiredDir, steerDir);
            }

            return new MoveToInputPointOutput(MoveToInputPointState.Moving, steerDir, vel, lookAhead, _target, _pathVersion, distToTarget);
        }

        /// <summary>
        /// 指定の MovementInputType が ActionBlock によりブロックされているか判定。
        /// </summary>
        bool IsInputTypeBlocked(MovementInputType inputType)
        {
            if (_actionBlockService == null || inputType == MovementInputType.None)
                return false;

            return inputType switch
            {
                MovementInputType.Player => _actionBlockService.IsBlocked(ActionBlockKeys.Entity.UserMovement),
                MovementInputType.AI => _actionBlockService.IsBlocked(ActionBlockKeys.Entity.AIMovement),
                MovementInputType.System => _actionBlockService.IsBlocked(ActionBlockKeys.Entity.SystemMovement),
                _ => false,
            };
        }

        bool NeedRepath(int frame, Vector2 currentVelocity)
        {
            int interval = Mathf.Max(1, _req.RepathIntervalFrames);
            if (frame - _lastRepathFrame >= interval)
                return true;

            float eps = Mathf.Max(0f, _req.StuckSpeedEpsilon);
            if (currentVelocity.magnitude <= eps)
                _stuckCounter++;
            else
                _stuckCounter = 0;

            if (_stuckCounter >= Mathf.Max(1, _req.StuckFramesToRepath))
            {
                _stuckCounter = 0;
                return true;
            }

            return false;
        }

        void InvalidatePath()
        {
            _path.Clear();
            _waypointIndex = 0;
            _pathVersion++;
            _lastRepathFrame = int.MinValue;
            _stuckCounter = 0;
        }

        void Repath(Vector2 start, Vector2 goal, int frame)
        {
            _path.Clear();
            _waypointIndex = 0;

            var ctx = new MoveToInputPointPlanContext(
                start: start,
                goal: goal,
                agentRadius: _profile.AgentRadius,
                detourClearance: _profile.DetourClearance,
                obstacleMask: _req.ObstacleMask,
                useTriggers: _req.UseTriggers,
                allowArcOnClearLine: _req.AllowArcOnClearLine,
                arcMaxOffset: _req.ArcMaxOffset,
                arcOffsetFactor: _req.ArcOffsetFactor,
                arcSeed: _req.ArcSeed
            );

            _planner.Plan(in ctx, _path);

            if (_path.Count == 0 || _path[_path.Count - 1] != goal)
                _path.Add(goal);

            _pathVersion++;
            _lastRepathFrame = frame;
        }

        void AdvanceWaypointIndex(Vector2 currentPos)
        {
            float adv = Mathf.Max(0.001f, _req.WaypointAdvanceDistance);

            while (_waypointIndex < _path.Count)
            {
                float d = Vector2.Distance(currentPos, _path[_waypointIndex]);
                if (d <= adv)
                    _waypointIndex++;
                else
                    break;
            }

            if (_waypointIndex >= _path.Count)
            {
                _waypointIndex = _path.Count - 1;
                if (_waypointIndex < 0) _waypointIndex = 0;
            }
        }

        Vector2 ComputeLookAheadPoint(Vector2 currentPos)
        {
            float look = Mathf.Max(0.001f, _req.LookAheadDistance);

            int idx = Mathf.Clamp(_waypointIndex, 0, Mathf.Max(0, _path.Count - 1));
            Vector2 p0 = currentPos;

            float remaining = look;

            for (int i = idx; i < _path.Count; i++)
            {
                Vector2 p1 = _path[i];
                Vector2 seg = p1 - p0;
                float segLen = seg.magnitude;

                if (segLen < 0.0001f)
                {
                    p0 = p1;
                    continue;
                }

                if (remaining <= segLen)
                {
                    float t = remaining / segLen;
                    return p0 + seg * t;
                }

                remaining -= segLen;
                p0 = p1;
            }

            return _path.Count > 0 ? _path[_path.Count - 1] : currentPos;
        }

        void DebugDraw(Vector2 currentPos, Vector2 lookAhead, Vector2 desiredDir, Vector2 steerDir)
        {
            Debug.DrawLine(currentPos, lookAhead, Color.cyan, 0f, false);
            Debug.DrawRay(currentPos, desiredDir * 1.0f, Color.yellow, 0f, false);
            Debug.DrawRay(currentPos, steerDir * 1.0f, Color.green, 0f, false);

            for (int i = 0; i < _path.Count - 1; i++)
            {
                Debug.DrawLine(_path[i], _path[i + 1], Color.magenta, 0f, false);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _path.Clear();
            _pathCopyTmp.Clear();
            _hasTarget = false;
        }
    }
}
