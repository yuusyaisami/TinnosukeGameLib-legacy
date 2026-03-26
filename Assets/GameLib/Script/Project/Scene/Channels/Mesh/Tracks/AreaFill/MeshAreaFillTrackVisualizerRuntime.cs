#nullable enable
using System.Collections.Generic;

namespace Game.Channel
{
    sealed class MeshAreaFillTrackVisualizerRuntime : IMeshTrackVisualizerRuntime
    {
        public MeshAreaFillTrackVisualizerRuntime(MeshAreaFillTrackVisualizerPreset preset)
        {
            _ = preset;
        }

        public bool TryBuildPaths(
            MeshTrackEvaluationContext context,
            MeshRegularTrackRuntimeState track,
            MeshTrackPlayerEvaluation evaluation,
            List<MeshRuntimePath> outputPaths)
        {
            _ = context;
            _ = track;

            outputPaths.Clear();
            if (evaluation is not MeshContourEvaluation contour || contour.Paths.Count == 0)
                return false;

            for (var i = 0; i < contour.Paths.Count; i++)
                outputPaths.Add(contour.Paths[i].Clone());

            return outputPaths.Count > 0;
        }
    }
}
