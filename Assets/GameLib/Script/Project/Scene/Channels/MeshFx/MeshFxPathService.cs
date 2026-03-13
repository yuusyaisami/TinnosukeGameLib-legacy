#nullable enable
using Game;
using Game.Commands.VNext;
using Game.Common;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Channel
{
    interface IMeshFxPathService
    {
        void OnAcquire();
        void OnRelease();
        void UpdateTracking(float deltaTime);
        bool ResolvePath(List<Vector3> points);
    }

    sealed class MeshFxPathService : IMeshFxPathService
    {
        readonly MeshFxChannelDef _def;
        readonly IScopeNode _ownerScope;
        readonly Transform? _ownerTransform;
        readonly IDynamicContext _dynamicContext;

        ActorSourceResolveCache _scopeToScopeFromCache;
        ActorSourceResolveCache _scopeToScopeToCache;
        ActorSourceResolveCache _singleDirectionOriginCache;
        ActorSourceResolveCache _trajectoryTargetCache;

        readonly List<Vector3> _trajectoryPoints = new(64);
        readonly List<float> _trajectoryTimes = new(64);

        float _lastTrajectorySampleTime;
        Vector3 _lastTrajectorySamplePos;
        bool _hasTrajectorySeed;

        public MeshFxPathService(MeshFxChannelDef def, IScopeNode ownerScope)
        {
            _def = def;
            _ownerScope = ownerScope;
            _ownerTransform = ownerScope.Identity?.SelfTransform;
            _dynamicContext = new SimpleDynamicContext(NullVarStore.Instance, ownerScope);
        }

        public void OnAcquire()
        {
            ResetTrajectory();
        }

        public void OnRelease()
        {
            ResetTrajectory();
        }

        public void UpdateTracking(float deltaTime)
        {
            if (_def.PathMode != MeshFxPathMode.TrajectoryTrack)
                return;

            if (deltaTime < 0f)
                deltaTime = 0f;

            UpdateTrajectoryInternal();
        }

        public bool ResolvePath(List<Vector3> points)
        {
            points.Clear();

            switch (_def.PathMode)
            {
                case MeshFxPathMode.ScopeToScope:
                    BuildScopeToScopePath(points);
                    break;
                case MeshFxPathMode.SingleDirection:
                    BuildSingleDirectionPath(points);
                    break;
                case MeshFxPathMode.TrajectoryTrack:
                    UpdateTrajectoryInternal();
                    BuildTrajectoryPath(points);
                    break;
                default:
                    break;
            }

            return points.Count >= 2;
        }

        void BuildScopeToScopePath(List<Vector3> points)
        {
            var settings = _def.ScopeToScopeSettings;
            if (!TryResolveActorTransform(settings.From, ref _scopeToScopeFromCache, out var from))
                return;
            if (!TryResolveActorTransform(settings.To, ref _scopeToScopeToCache, out var to))
                return;

            var fromOffset = settings.FromLocalOffset.GetOrDefault(_dynamicContext, Vector3.zero);
            var toOffset = settings.ToLocalOffset.GetOrDefault(_dynamicContext, Vector3.zero);

            var fromPos = from.TransformPoint(fromOffset);
            var toPos = to.TransformPoint(toOffset);

            points.Add(fromPos);
            points.Add(toPos);
        }

        void BuildSingleDirectionPath(List<Vector3> points)
        {
            var settings = _def.SingleDirectionSettings;
            if (!TryResolveActorTransform(settings.Origin, ref _singleDirectionOriginCache, out var origin))
                return;

            var originOffset = settings.OriginLocalOffset.GetOrDefault(_dynamicContext, Vector3.zero);
            var originPos = origin.TransformPoint(originOffset);
            var direction = MeshFxMath.SafeNormal2D(settings.Direction.GetOrDefault(_dynamicContext, Vector2.right), Vector2.right);

            Vector3 worldDir;
            if (settings.UseWorldDirection)
            {
                worldDir = new Vector3(direction.x, direction.y, 0f);
            }
            else
            {
                worldDir = origin.TransformDirection(new Vector3(direction.x, direction.y, 0f));
            }

            if (worldDir.sqrMagnitude <= 1e-8f)
                worldDir = Vector3.right;
            else
                worldDir.Normalize();

            var length = Mathf.Max(0.01f, settings.Length.GetOrDefault(_dynamicContext, 4f));
            var toPos = originPos + worldDir * length;

            points.Add(originPos);
            points.Add(toPos);
        }

        void BuildTrajectoryPath(List<Vector3> points)
        {
            if (_trajectoryPoints.Count == 0)
                return;

            points.AddRange(_trajectoryPoints);
            if (points.Count == 1)
            {
                points.Add(points[0] + Vector3.right * 0.01f);
            }
        }

        void UpdateTrajectoryInternal()
        {
            var settings = _def.TrajectoryTrackSettings;
            if (!TryResolveActorTransform(settings.Target, ref _trajectoryTargetCache, out var target))
                return;

            var now = settings.UseUnscaledTime ? Time.unscaledTime : Time.time;
            var currentPos = target.position;

            if (!_hasTrajectorySeed)
            {
                _hasTrajectorySeed = true;
                _lastTrajectorySamplePos = currentPos;
                _lastTrajectorySampleTime = now;
                _trajectoryPoints.Clear();
                _trajectoryTimes.Clear();
                _trajectoryPoints.Add(currentPos);
                _trajectoryTimes.Add(now);
                return;
            }

            var minDistance = Mathf.Max(0f, settings.MinDistance);
            var minTime = Mathf.Max(0f, settings.MinTime);
            var dt = now - _lastTrajectorySampleTime;
            var dist = Vector3.Distance(currentPos, _lastTrajectorySamplePos);

            if (dist >= minDistance && dt >= minTime)
            {
                _trajectoryPoints.Add(currentPos);
                _trajectoryTimes.Add(now);
                _lastTrajectorySamplePos = currentPos;
                _lastTrajectorySampleTime = now;
            }

            var duration = Mathf.Max(0.1f, settings.DurationSeconds);
            var cutoff = now - duration;
            var expiredCount = 0;
            while (expiredCount < _trajectoryTimes.Count && _trajectoryTimes[expiredCount] < cutoff)
            {
                expiredCount++;
            }

            if (expiredCount > 0)
            {
                _trajectoryTimes.RemoveRange(0, expiredCount);
                _trajectoryPoints.RemoveRange(0, expiredCount);
            }

            var maxPoints = Mathf.Max(2, settings.MaxPoints);
            var overflow = _trajectoryPoints.Count - maxPoints;
            if (overflow > 0)
            {
                _trajectoryPoints.RemoveRange(0, overflow);
                _trajectoryTimes.RemoveRange(0, overflow);
            }
        }

        void ResetTrajectory()
        {
            _trajectoryPoints.Clear();
            _trajectoryTimes.Clear();
            _hasTrajectorySeed = false;
            _lastTrajectorySamplePos = Vector3.zero;
            _lastTrajectorySampleTime = 0f;
        }

        bool TryResolveActorTransform(in ActorSource source, ref ActorSourceResolveCache cache, out Transform transform)
        {
            transform = null!;

            var resolvedScope = ActorSourceFastResolver.ResolveCached(_ownerScope, source, ref cache);
            if (TryGetTransform(resolvedScope, out var resolved))
            {
                transform = resolved;
                return true;
            }

            if (_ownerTransform != null)
            {
                transform = _ownerTransform;
                return true;
            }

            return false;
        }

        static bool TryGetTransform(IScopeNode? scope, out Transform transform)
        {
            transform = null!;
            if (scope == null)
                return false;

            var fromIdentity = scope.Identity?.SelfTransform;
            if (fromIdentity != null)
            {
                transform = fromIdentity;
                return true;
            }

            if (scope is Component c && c != null)
            {
                transform = c.transform;
                return true;
            }

            return false;
        }
    }
}
