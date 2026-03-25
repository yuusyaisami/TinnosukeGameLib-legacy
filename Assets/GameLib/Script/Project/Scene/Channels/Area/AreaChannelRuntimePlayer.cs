#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Channel
{
    public sealed class AreaChannelRuntimePlayer : IAreaChannelPlayer
    {
        readonly AreaChannelDefinition _definition;
        readonly int _seedOffset;
        int _sequenceIndex;
        Vector3[] _recentPositions = Array.Empty<Vector3>();
        int _recentCount;
        int _recentIndex;

        public AreaChannelDefinition Definition => _definition;

        public AreaChannelRuntimePlayer(AreaChannelDefinition definition)
        {
            _definition = definition;
            _seedOffset = ResolveSeed(definition.Sample.SequenceSeed);
            _sequenceIndex = 0;
            ResetHistory();
        }

        public void ResetRuntime()
        {
            _sequenceIndex = 0;
            ResetHistory();
        }

        public Vector2 NextSequencePoint()
        {
            var index = _sequenceIndex + _seedOffset;
            _sequenceIndex++;

            var u = Halton(index, 2);
            var v = Halton(index, 3);
            return new Vector2(u, v);
        }

        public bool TrySamplePosition(Vector3 basePosition, in AreaSampleRequest request, out Vector3 position)
        {
            position = default;

            var shape = _definition.Shape;
            if (shape == null)
                return false;

            var maxRetry = Mathf.Max(1, _definition.Sample.MaxRetry);
            var shapeContext = new AreaShapeSampleContext(request.Mode, request.LayerKey);
            var jitter = _definition.Sample.JitterRate;

            for (int i = 0; i < maxRetry; i++)
            {
                var uv = NextSequencePoint();
                uv.x = ApplyJitter01(uv.x, jitter);
                uv.y = ApplyJitter01(uv.y, jitter);

                if (!shape.TrySample(in shapeContext, uv, out var local2))
                    continue;

                var candidate = basePosition + ToPlane(local2, _definition.Plane);
                if (!IsFarEnough(candidate))
                    continue;

                Remember(candidate);
                position = candidate;
                return true;
            }

            return false;
        }

        public bool ContainsPosition(Vector3 basePosition, Vector3 worldPosition)
        {
            var shape = _definition.Shape;
            if (shape == null)
                return false;

            var localOffset = worldPosition - basePosition;
            var localPosition = ToLocal(localOffset, _definition.Plane);
            return shape.ContainsLocalPosition(localPosition);
        }

        public bool TryGetContour(Vector3 basePosition, out AreaContourData contour)
        {
            contour = default;

            var shape = _definition.Shape;
            if (shape == null || !shape.TryGetContourLocal(out var localContour))
                return false;

            var worldPaths = new List<AreaContourPath>(localContour.Paths.Count);
            for (var i = 0; i < localContour.Paths.Count; i++)
            {
                var localPath = localContour.Paths[i];
                if (localPath.Points == null || localPath.Points.Count == 0)
                    continue;

                var worldPoints = new List<Vector2>(localPath.Points.Count);
                for (var p = 0; p < localPath.Points.Count; p++)
                {
                    var world = basePosition + ToPlane(localPath.Points[p], _definition.Plane);
                    worldPoints.Add(_definition.Plane == AreaPlane.XZ
                        ? new Vector2(world.x, world.z)
                        : new Vector2(world.x, world.y));
                }

                worldPaths.Add(new AreaContourPath(worldPoints, localPath.IsHole));
            }

            contour = new AreaContourData(_definition.Plane, worldPaths);
            return worldPaths.Count > 0;
        }

        public bool IsFarEnough(Vector3 candidate)
        {
            var minDistance = Mathf.Max(0f, _definition.Sample.MinDistance);
            if (minDistance <= 0f)
                return true;

            var minSqr = minDistance * minDistance;
            for (int i = 0; i < _recentCount; i++)
            {
                var delta = candidate - _recentPositions[i];
                if (delta.sqrMagnitude < minSqr)
                    return false;
            }

            return true;
        }

        public void Remember(Vector3 position)
        {
            if (_definition.Sample.MinDistance <= 0f)
                return;

            if (_recentPositions.Length == 0)
                return;

            _recentPositions[_recentIndex] = position;
            _recentIndex = (_recentIndex + 1) % _recentPositions.Length;
            if (_recentCount < _recentPositions.Length)
                _recentCount++;
        }

        void ResetHistory()
        {
            var capacity = Mathf.Max(1, _definition.Sample.MaxRetry);
            if (_recentPositions.Length != capacity)
                _recentPositions = new Vector3[capacity];

            _recentCount = 0;
            _recentIndex = 0;
        }

        static float ApplyJitter01(float t, float jitter)
        {
            if (jitter <= 0f)
                return Mathf.Repeat(t, 1f);

            var offset = UnityEngine.Random.Range(-jitter, jitter);
            return Mathf.Repeat(t + offset, 1f);
        }

        static Vector3 ToPlane(Vector2 offset, AreaPlane plane)
        {
            return plane == AreaPlane.XZ
                ? new Vector3(offset.x, 0f, offset.y)
                : new Vector3(offset.x, offset.y, 0f);
        }

        static Vector2 ToLocal(Vector3 offset, AreaPlane plane)
        {
            return plane == AreaPlane.XZ
                ? new Vector2(offset.x, offset.z)
                : new Vector2(offset.x, offset.y);
        }

        static int ResolveSeed(int seed)
        {
            if (seed == 0)
                seed = UnityEngine.Random.Range(1, int.MaxValue);
            if (seed < 0)
                seed = -seed;
            return seed;
        }

        static float Halton(int index, int b)
        {
            if (b <= 1)
                return 0f;

            float f = 1f;
            float result = 0f;
            int i = index + 1;
            while (i > 0)
            {
                f /= b;
                result += f * (i % b);
                i /= b;
            }
            return result;
        }
    }
}
