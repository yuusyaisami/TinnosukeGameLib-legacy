#nullable enable
using System;
using System.Collections.Generic;
using Game;
using Game.Times;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.LineDraw
{
    public interface ILineDrawTrailSettings
    {
        bool EnableTrail { get; }
        LineSpace Space { get; }
        LineStyle Style { get; }
        float DurationSeconds { get; }
        float MinDistance { get; }
        float MinTime { get; }
        float MaxSegmentLength { get; }
        float MaxJumpDistance { get; }
        bool UseSpeedAdaptiveTime { get; }
        float SpeedToMinTimeScale { get; }
        float MinTimeFloor { get; }
        int MaxSegments { get; }
        bool UseUnscaledTime { get; }
    }

    public interface ILineDrawTrailService
    {
        void SetEnabled(bool enabled);
        void Clear();
    }

    public sealed class LineDrawTrailService : ILineDrawTrailService, ITickable, IScopeAcquireHandler, IScopeReleaseHandler, IDisposable
    {
        const float PositionEpsilon = 0.0001f;

        readonly IObjectResolver _resolver;
        readonly List<LinePoint> _points = new();

        LineHandle _pathHandle = LineHandle.Invalid;
        bool _pathDirty;

        ILineDrawService? _lineDraw;
        ILineDrawTrailSettings? _settings;
        ILTSIdentityService? _identity;
        Transform? _selfTransform;

        bool _acquired;
        bool _disposed;
        bool _enabledOverride = true;
        bool _hasLast;
        Vector3 _lastPos;
        float _lastEmitTime;
        bool _useUnscaledTime;

        public LineDrawTrailService(IObjectResolver resolver)
        {
            _resolver = resolver;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            if (_disposed || _acquired)
                return;

            _acquired = true;
            ResolveDependencies();
            InitializeState();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            if (_disposed || !_acquired)
                return;

            _acquired = false;
            Clear();
            _hasLast = false;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            Clear();
        }

        public void Tick()
        {
            if (_disposed || !_acquired)
                return;

            if (_settings == null || _lineDraw == null || _selfTransform == null)
                ResolveDependencies();

            var now = GetTime();
            TrimExpired(now);

            if (_lineDraw == null || _settings == null)
                return;

            if (!IsEnabled())
            {
                UpdatePathIfNeeded();
                return;
            }

            if (_selfTransform == null)
                return;

            var pos = _selfTransform.position;
            if (!_hasLast)
            {
                _lastPos = pos;
                _lastEmitTime = now;
                _hasLast = true;
                UpdatePathIfNeeded();
                return;
            }

            var minDistance = Mathf.Max(0f, _settings.MinDistance);
            var minTime = Mathf.Max(0f, _settings.MinTime);
            var distance = Vector3.Distance(_lastPos, pos);
            var timeDelta = now - _lastEmitTime;

            var maxJumpDistance = Mathf.Max(0f, _settings.MaxJumpDistance);
            if (maxJumpDistance > 0f && distance > maxJumpDistance)
            {
                ResetPath();
                _lastPos = pos;
                _lastEmitTime = now;
                _hasLast = true;
                return;
            }

            var effectiveMinTime = ResolveMinTime(minTime, distance, timeDelta);
            if (distance >= minDistance && timeDelta >= effectiveMinTime)
            {
                AppendPointsForMove(_lastPos, _lastEmitTime, pos, now);
                TrimToMaxPoints();
                _lastPos = pos;
                _lastEmitTime = now;
            }

            UpdatePathIfNeeded();
        }

        public void SetEnabled(bool enabled)
        {
            _enabledOverride = enabled;
        }

        public void Clear()
        {
            ResetPath();
            _hasLast = false;
        }

        void ResolveDependencies()
        {
            _resolver?.TryResolve(out _lineDraw);
            _resolver?.TryResolve(out _settings);
            _resolver?.TryResolve(out _identity);

            if (_identity != null)
                _selfTransform = _identity.SelfTransform;

            _useUnscaledTime = (_settings != null && _settings.UseUnscaledTime) ||
                               (_identity != null && _identity.TimeScaleBehavior == TimeScaleBehavior.Unscaled);
        }

        void InitializeState()
        {
            ResetPath();
            _hasLast = false;
            _lastEmitTime = GetTime();

            if (_selfTransform != null)
            {
                _lastPos = _selfTransform.position;
                _hasLast = true;
            }
        }

        void ResetPath()
        {
            if (_lineDraw != null && _pathHandle.IsValid)
                _lineDraw.Release(_pathHandle);

            _pathHandle = LineHandle.Invalid;
            _points.Clear();
            _pathDirty = false;
        }

        void AppendPointsForMove(Vector3 from, float fromTime, Vector3 to, float toTime)
        {
            if (_settings == null)
                return;

            EnsureStartPoint(from, fromTime);

            float distance = Vector3.Distance(from, to);
            float maxSegmentLength = Mathf.Max(0f, _settings.MaxSegmentLength);
            if (maxSegmentLength > 0f && distance > maxSegmentLength)
            {
                int steps = Mathf.Max(1, Mathf.CeilToInt(distance / maxSegmentLength));
                var maxPoints = _settings.MaxSegments;
                if (maxPoints > 0)
                {
                    int limit = Mathf.Max(2, maxPoints);
                    if (steps > limit)
                        steps = limit;
                }
                for (int s = 1; s <= steps; s++)
                {
                    float t = (float)s / steps;
                    var pos = Vector3.LerpUnclamped(from, to, t);
                    float time = Mathf.LerpUnclamped(fromTime, toTime, t);
                    AppendPoint(pos, time);
                }
                return;
            }

            AppendPoint(to, toTime);
        }

        void EnsureStartPoint(Vector3 position, float time)
        {
            if (_points.Count == 0)
            {
                _points.Add(new LinePoint(position, time));
                _pathDirty = true;
                return;
            }

            var last = _points[_points.Count - 1].Position;
            if ((last - position).sqrMagnitude > PositionEpsilon * PositionEpsilon)
                AppendPoint(position, time);
        }

        void AppendPoint(Vector3 position, float time)
        {
            int count = _points.Count;
            if (count > 0)
            {
                var last = _points[count - 1].Position;
                if ((last - position).sqrMagnitude <= PositionEpsilon * PositionEpsilon)
                    return;
            }

            _points.Add(new LinePoint(position, time));
            _pathDirty = true;
        }

        void TrimToMaxPoints()
        {
            if (_settings == null)
                return;

            var max = _settings.MaxSegments;
            if (max <= 0)
                return;
            if (max < 2)
                max = 2;

            while (_points.Count > max)
            {
                _points.RemoveAt(0);
                _pathDirty = true;
            }
        }

        void UpdatePathIfNeeded()
        {
            if (_lineDraw == null || _settings == null)
                return;

            if (_points.Count < 2)
            {
                if (_pathHandle.IsValid)
                {
                    _lineDraw.Release(_pathHandle);
                    _pathHandle = LineHandle.Invalid;
                }
                _pathDirty = false;
                return;
            }

            if (!_pathDirty && _pathHandle.IsValid)
                return;

            var style = _settings.Style;
            var path = new LinePath(_points, false);
            var request = new LinePathRequest(path, _settings.Space, style);

            if (_pathHandle.IsValid && !_lineDraw.UpdatePath(_pathHandle, request))
                _pathHandle = LineHandle.Invalid;

            if (!_pathHandle.IsValid)
            {
                var handle = _lineDraw.CreatePath(request);
                if (handle.IsValid)
                    _pathHandle = handle;
            }

            _pathDirty = !_pathHandle.IsValid;
        }

        float ResolveMinTime(float minTime, float distance, float timeDelta)
        {
            if (_settings == null || !_settings.UseSpeedAdaptiveTime)
                return minTime;

            if (timeDelta <= 0f)
                return minTime;

            float scale = Mathf.Max(0f, _settings.SpeedToMinTimeScale);
            if (scale <= 0f)
                return minTime;

            float speed = distance / timeDelta;
            if (speed <= 0f)
                return minTime;

            float adjusted = minTime / (1f + speed * scale);
            float floor = Mathf.Max(0f, _settings.MinTimeFloor);
            if (adjusted < floor)
                adjusted = floor;

            return adjusted;
        }

        bool IsEnabled()
        {
            if (_settings == null)
                return false;

            return _enabledOverride && _settings.EnableTrail;
        }

        float GetTime()
        {
            return _useUnscaledTime ? Time.unscaledTime : Time.time;
        }

        void TrimExpired(float now)
        {
            if (_settings == null || _points.Count == 0)
                return;

            var duration = _settings.DurationSeconds;
            if (duration <= 0f)
                return;

            int removed = 0;
            while (_points.Count > 0 && now - _points[0].Time >= duration)
            {
                _points.RemoveAt(0);
                removed++;
            }

            if (removed > 0)
                _pathDirty = true;
        }
    }
}
