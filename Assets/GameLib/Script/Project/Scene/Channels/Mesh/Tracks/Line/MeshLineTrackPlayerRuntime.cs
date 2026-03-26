#nullable enable
using UnityEngine;

namespace Game.Channel
{
    sealed class MeshLineTrackPlayerRuntime : IMeshTrackPlayerRuntime
    {
        readonly MeshLineTrackPlayerPreset _preset;

        public MeshLineTrackPlayerRuntime(MeshLineTrackPlayerPreset preset)
        {
            _preset = preset;
        }

        public void Reset()
        {
        }

        public bool TryEvaluate(MeshTrackEvaluationContext context, out MeshTrackPlayerEvaluation evaluation)
        {
            var result = new MeshCenterLineEvaluation
            {
                Closed = _preset.Closed,
                SmoothPath = _preset.SmoothPath,
                SmoothingSubdivisions = _preset.SmoothingSubdivisions,
            };

            for (var i = 0; i < _preset.Points.Count; i++)
            {
                if (_preset.Points[i].TryGet(context.DynamicContext, out Vector3 worldPoint))
                    result.Points.Add(new Vector2(worldPoint.x, worldPoint.y));
            }

            evaluation = result;
            return result.Points.Count >= 2;
        }
    }
}
