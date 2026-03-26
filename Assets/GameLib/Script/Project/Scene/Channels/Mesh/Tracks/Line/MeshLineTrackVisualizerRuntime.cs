#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace Game.Channel
{
    sealed class MeshLineTrackVisualizerRuntime : IMeshTrackVisualizerRuntime
    {
        readonly MeshLineTrackVisualizerPreset _preset;

        public MeshLineTrackVisualizerRuntime(MeshLineTrackVisualizerPreset preset)
        {
            _preset = preset;
        }

        public bool TryBuildPaths(
            MeshTrackEvaluationContext context,
            MeshRegularTrackRuntimeState track,
            MeshTrackPlayerEvaluation evaluation,
            List<MeshRuntimePath> outputPaths)
        {
            outputPaths.Clear();

            if (evaluation is not MeshCenterLineEvaluation centerLine || centerLine.Points.Count < 2)
                return false;

            var smoothed = ListPool<Vector2>.Get();
            var resampled = ListPool<Vector2>.Get();
            var distances = ListPool<float>.Get();
            var left = ListPool<Vector2>.Get();
            var right = ListPool<Vector2>.Get();

            try
            {
                if (centerLine.SmoothPath && centerLine.Points.Count > 2)
                    MeshChannelGeometryUtility.BuildCatmullRom(centerLine.Points, centerLine.Closed, Mathf.Max(1, centerLine.SmoothingSubdivisions), smoothed);
                else
                    smoothed.AddRange(centerLine.Points);

                if (smoothed.Count < 2)
                    return false;

                MeshChannelGeometryUtility.Resample(smoothed, _preset.MinSegmentLength, _preset.MaxPointCount, resampled, distances);
                if (resampled.Count < 2)
                    return false;

                MeshChannelGeometryUtility.BuildRibbonOutline(
                    resampled,
                    distances,
                    centerLine.Closed,
                    _preset,
                    context.TimeSeconds,
                    left,
                    right);

                if (left.Count < 2 || right.Count < 2)
                    return false;

                var path = new MeshRuntimePath();
                for (var i = 0; i < left.Count; i++)
                    path.Points.Add(left[i]);
                for (var i = right.Count - 1; i >= 0; i--)
                    path.Points.Add(right[i]);

                if (path.Points.Count < 3)
                    return false;

                outputPaths.Add(path);
                return true;
            }
            finally
            {
                ListPool<Vector2>.Release(smoothed);
                ListPool<Vector2>.Release(resampled);
                ListPool<float>.Release(distances);
                ListPool<Vector2>.Release(left);
                ListPool<Vector2>.Release(right);
            }
        }
    }
}
