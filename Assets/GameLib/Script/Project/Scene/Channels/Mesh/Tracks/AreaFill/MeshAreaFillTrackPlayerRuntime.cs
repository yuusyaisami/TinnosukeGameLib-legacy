#nullable enable
using Game.Commands.VNext;
using VContainer;

namespace Game.Channel
{
    sealed class MeshAreaFillTrackPlayerRuntime : IMeshTrackPlayerRuntime
    {
        readonly MeshAreaFillTrackPlayerPreset _preset;

        public MeshAreaFillTrackPlayerRuntime(MeshAreaFillTrackPlayerPreset preset)
        {
            _preset = preset;
        }

        public void Reset()
        {
        }

        public bool TryEvaluate(MeshTrackEvaluationContext context, out MeshTrackPlayerEvaluation evaluation)
        {
            evaluation = new MeshContourEvaluation();

            var scope = ActorSourceFastResolver.Resolve(context.DynamicContext, _preset.AreaHubSource);
            if (scope?.Resolver == null)
                return false;

            if (!scope.Resolver.TryResolve<IAreaChannelHubService>(out var hub) || hub == null)
                return false;

            if (!hub.TryGetContour(_preset.AreaTag, out var contour))
                return false;

            var result = (MeshContourEvaluation)evaluation;
            for (var i = 0; i < contour.Paths.Count; i++)
            {
                var contourPath = contour.Paths[i];
                if (contourPath.Points == null || contourPath.Points.Count < 3)
                    continue;

                var path = new MeshRuntimePath
                {
                    IsHole = contourPath.IsHole,
                };

                for (var p = 0; p < contourPath.Points.Count; p++)
                    path.Points.Add(contourPath.Points[p]);

                result.Paths.Add(path);
            }

            return result.Paths.Count > 0;
        }
    }
}
