#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace Game.Channel
{
    interface IMeshTrackPlayerRuntime
    {
        void Reset();
        bool TryEvaluate(MeshTrackEvaluationContext context, out MeshTrackPlayerEvaluation evaluation);
    }

    interface IMeshTrackVisualizerRuntime
    {
        bool TryBuildPaths(
            MeshTrackEvaluationContext context,
            MeshRegularTrackRuntimeState track,
            MeshTrackPlayerEvaluation evaluation,
            List<MeshRuntimePath> outputPaths);
    }

    interface IMeshTrackColliderRuntime
    {
        MeshPolygonTrackColliderPreset? Preset { get; }
    }

    interface IMeshSimulationTrackRuntime
    {
        void Reset();
        void Apply(MeshSimulationContext context, MeshSimulationTrackRuntimeState track, MeshCompositeDraft composite);
    }

    abstract class MeshTrackPlayerEvaluation
    {
    }

    sealed class MeshCenterLineEvaluation : MeshTrackPlayerEvaluation
    {
        public readonly List<Vector2> Points = new();
        public bool Closed;
        public bool SmoothPath;
        public int SmoothingSubdivisions;
    }

    sealed class MeshContourEvaluation : MeshTrackPlayerEvaluation
    {
        public readonly List<MeshRuntimePath> Paths = new();
    }

    sealed class MeshRuntimePath
    {
        public readonly List<Vector2> Points = new();
        public bool IsHole;

        public MeshRuntimePath Clone()
        {
            var clone = new MeshRuntimePath
            {
                IsHole = IsHole,
            };
            clone.Points.AddRange(Points);
            return clone;
        }
    }
}
