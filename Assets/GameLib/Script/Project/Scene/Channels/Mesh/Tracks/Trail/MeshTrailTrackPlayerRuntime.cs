#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace Game.Channel
{
    sealed class MeshTrailTrackPlayerRuntime : IMeshTrackPlayerRuntime
    {
        readonly MeshTrailTrackPlayerPreset _preset;
        readonly List<(Vector2 Point, float Time)> _samples = new();
        float _lastCaptureTime = float.MinValue;

        public MeshTrailTrackPlayerRuntime(MeshTrailTrackPlayerPreset preset)
        {
            _preset = preset;
        }

        public void Reset()
        {
            _samples.Clear();
            _lastCaptureTime = float.MinValue;
        }

        public bool TryEvaluate(MeshTrackEvaluationContext context, out MeshTrackPlayerEvaluation evaluation)
        {
            if (!_preset.TargetPosition.TryGet(context.DynamicContext, out Vector3 worldPoint))
            {
                evaluation = new MeshCenterLineEvaluation();
                return false;
            }

            var point = new Vector2(worldPoint.x, worldPoint.y);
            var shouldCapture = _samples.Count == 0;
            if (!shouldCapture)
            {
                var last = _samples[_samples.Count - 1];
                shouldCapture = (point - last.Point).sqrMagnitude >= _preset.MinDistance * _preset.MinDistance;
                if (!shouldCapture)
                    shouldCapture = context.TimeSeconds - _lastCaptureTime >= _preset.MinTime;
            }

            if (shouldCapture)
            {
                _samples.Add((point, context.TimeSeconds));
                _lastCaptureTime = context.TimeSeconds;
            }

            var expireBefore = context.TimeSeconds - _preset.DurationSeconds;
            while (_samples.Count > 0 && _samples[0].Time < expireBefore)
                _samples.RemoveAt(0);

            while (_samples.Count > _preset.MaxPoints)
                _samples.RemoveAt(0);

            var result = new MeshCenterLineEvaluation
            {
                Closed = false,
                SmoothPath = _preset.SmoothPath,
                SmoothingSubdivisions = _preset.SmoothingSubdivisions,
            };

            for (var i = 0; i < _samples.Count; i++)
                result.Points.Add(_samples[i].Point);

            evaluation = result;
            return result.Points.Count >= 2;
        }
    }
}
