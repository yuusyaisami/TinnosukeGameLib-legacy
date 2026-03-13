using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.LineDraw;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class LineDrawCreateExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.LineDrawCreate;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not LineDrawCreateCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "LineDrawCreateCommandData is required.");

            ct.ThrowIfCancellationRequested();

            // ターゲットスコープを解決
            var (targetScope, error) = await ActorScopeResolver.ResolveAsync(typed.Target, ctx, ct);
            if (targetScope == null)
            {
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, error ?? "Target scope could not be resolved.");
            }

            if (targetScope.Resolver == null)
            {
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "Target scope resolver is null.");
            }

            // ILineDrawServiceを取得
            if (!targetScope.Resolver.TryResolve<ILineDrawService>(out var lineDrawService) || lineDrawService == null)
            {
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "ILineDrawService is not available in the target scope.");
            }

            LineHandle handle;

            switch (typed.Kind)
            {
                case LineDrawCreateKind.Segment:
                    handle = CreateSegment(lineDrawService, typed);
                    break;

                case LineDrawCreateKind.Path:
                    handle = CreatePath(lineDrawService, typed);
                    break;

                default:
                    Debug.LogWarning($"[LineDrawCreateExecutor] Unknown kind: {typed.Kind}");
                    return;
            }

            if (!handle.IsValid)
            {
                Debug.LogWarning("[LineDrawCreateExecutor] Failed to create line.");
            }
        }

        static LineHandle CreateSegment(ILineDrawService service, LineDrawCreateCommandData typed)
        {
            var request = new LineSegmentRequest(
                from: LineAnchor.FromPosition(typed.FromPosition),
                to: LineAnchor.FromPosition(typed.ToPosition),
                space: typed.Space,
                style: typed.Style
            );

            return service.CreateSegment(request);
        }

        static LineHandle CreatePath(ILineDrawService service, LineDrawCreateCommandData typed)
        {
            var points = new List<LinePoint>();
            if (typed.PathPoints != null)
            {
                for (int i = 0; i < typed.PathPoints.Count; i++)
                {
                    points.Add(new LinePoint(typed.PathPoints[i]));
                }
            }

            var linePath = new LinePath(points, typed.Closed);

            var request = new LinePathRequest(
                path: linePath,
                space: typed.Space,
                style: typed.Style
            );

            return service.CreatePath(request);
        }
    }
}
