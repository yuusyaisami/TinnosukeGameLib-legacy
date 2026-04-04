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

            if (evaluation is MeshCenterLineEvaluation centerLine)
                return TryBuildSinglePath(centerLine, context.TimeSeconds, outputPaths);

            if (evaluation is not MeshMultiCenterLineEvaluation multiCenterLine || multiCenterLine.Lines.Count == 0)
                return false;

            var linePaths = ListPool<MeshRuntimePath>.Get();
            try
            {
                var builtAny = false;
                for (var i = 0; i < multiCenterLine.Lines.Count; i++)
                {
                    var line = multiCenterLine.Lines[i];
                    if (!TryBuildSinglePath(line, context.TimeSeconds, linePaths))
                        continue;

                    builtAny = true;
                    outputPaths.AddRange(linePaths);
                    linePaths.Clear();
                }

                return builtAny;
            }
            finally
            {
                ListPool<MeshRuntimePath>.Release(linePaths);
            }
        }

        bool TryBuildSinglePath(MeshCenterLineEvaluation centerLine, float timeSeconds, List<MeshRuntimePath> outputPaths)
        {
            outputPaths.Clear();
            if (centerLine == null || centerLine.Points.Count < 2)
                return false;

            var smoothed = ListPool<Vector2>.Get();
            var resampled = ListPool<Vector2>.Get();
            var distances = ListPool<float>.Get();

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

                MeshChannelGeometryUtility.BuildLineVisualPaths(
                    resampled,
                    distances,
                    centerLine.Closed,
                    _preset,
                    timeSeconds,
                    outputPaths);
                return outputPaths.Count > 0;
            }
            finally
            {
                ListPool<Vector2>.Release(smoothed);
                ListPool<Vector2>.Release(resampled);
                ListPool<float>.Release(distances);
            }
        }
    }
}
