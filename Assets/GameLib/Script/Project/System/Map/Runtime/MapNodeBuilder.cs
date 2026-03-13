#nullable enable
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using Game.Commands.VNext;
using UnityEngine;

namespace Game.MapNode
{
    public interface IMapNodeBuilder
    {
        UniTask<MapNodeRuntime> BuildAsync(
            MapNodeProfileSO profile,
            Transform parent,
            IScopeNode scopeParent,
            ICommandRunner runner,
            CancellationToken ct);
    }

    public sealed class MapNodeBuilder : IMapNodeBuilder
    {
        readonly MapNodeGenerator _generator;
        readonly IMapNodeVisualizer _visualizer;

        public MapNodeBuilder(MapNodeGenerator generator, IMapNodeVisualizer visualizer)
        {
            _generator = generator;
            _visualizer = visualizer;
        }

        public async UniTask<MapNodeRuntime> BuildAsync(
            MapNodeProfileSO profile,
            Transform parent,
            IScopeNode scopeParent,
            ICommandRunner runner,
            CancellationToken ct)
        {
            if (profile == null || profile.Generate == null || profile.Visualize == null)
                return new MapNodeRuntime(new MapGraph(new List<MapNodeLayer>(), new List<MapNode>(), 0, 0), new List<MapNodeInstance>(), new List<MapNodeConnectionInstance>());

            var graph = _generator.Generate(profile.Generate, scopeParent);
            return await _visualizer.BuildRuntimeAsync(
                graph,
                profile.Visualize,
                parent,
                scopeParent,
                runner,
                profile.FailurePolicy,
                ct);
        }
    }
}
