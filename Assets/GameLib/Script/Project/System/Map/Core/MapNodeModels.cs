#nullable enable
using System.Collections.Generic;
using Game.Channel;
using UnityEngine;
using VContainer;

namespace Game.MapNode
{
    public sealed class MapNode
    {
        public int Id;
        public int LayerIndex;
        public int WidthIndex;
        public MapNodeType Type;
        public MapNodeState State;
        public readonly List<int> PrevIds = new();
        public readonly List<int> NextIds = new();
    }

    public sealed class MapNodeLayer
    {
        public int LayerIndex;
        public readonly List<int> NodeIds = new();
    }

    public sealed class MapGraph
    {
        readonly List<MapNodeLayer> _layers;
        readonly List<MapNode> _nodes;

        public IReadOnlyList<MapNodeLayer> Layers => _layers;
        public IReadOnlyList<MapNode> Nodes => _nodes;
        public int Depth { get; }
        public int MaxWidth { get; }

        public MapGraph(List<MapNodeLayer> layers, List<MapNode> nodes, int depth, int maxWidth)
        {
            _layers = layers ?? new List<MapNodeLayer>();
            _nodes = nodes ?? new List<MapNode>();
            Depth = depth;
            MaxWidth = maxWidth;
        }
    }

    public sealed class MapNodeInstance
    {
        public int NodeId;
        public Transform Root;
        public IScopeNode Scope;
        public IRuntimeResolver Resolver;
        public Vector3 WorldPos;

        public MapNodeInstance(int nodeId, Transform root, IScopeNode scope, IRuntimeResolver resolver, Vector3 worldPos)
        {
            NodeId = nodeId;
            Root = root;
            Scope = scope;
            Resolver = resolver;
            WorldPos = worldPos;
        }
    }

    public sealed class MapNodeLineInstance
    {
        public Transform Root;
        public IScopeNode Scope;
        public IRuntimeResolver Resolver;

        public MapNodeLineInstance(Transform root, IScopeNode scope, IRuntimeResolver resolver)
        {
            Root = root;
            Scope = scope;
            Resolver = resolver;
        }
    }

    public sealed class MapNodeConnectionInstance
    {
        public int FromNodeId;
        public int ToNodeId;
        public string TrackKey;

        public MapNodeConnectionInstance(int fromNodeId, int toNodeId, string trackKey)
        {
            FromNodeId = fromNodeId;
            ToNodeId = toNodeId;
            TrackKey = trackKey ?? string.Empty;
        }
    }

    public sealed class MapNodeRuntime
    {
        public MapGraph Graph;
        public IReadOnlyList<MapNodeInstance> NodeInstances;
        public IReadOnlyList<MapNodeConnectionInstance> Connections;
        public IMeshChannelControlService? LineControlService;
        public string LineChannelTag;
        public MapNodeLineInstance? LineInstance;

        public MapNodeRuntime(
            MapGraph graph,
            IReadOnlyList<MapNodeInstance> nodes,
            IReadOnlyList<MapNodeConnectionInstance> connections,
            IMeshChannelControlService? lineControlService = null,
            string lineChannelTag = "default",
            MapNodeLineInstance? lineInstance = null)
        {
            Graph = graph;
            NodeInstances = nodes ?? new List<MapNodeInstance>();
            Connections = connections ?? new List<MapNodeConnectionInstance>();
            LineControlService = lineControlService;
            LineChannelTag = string.IsNullOrWhiteSpace(lineChannelTag) ? "default" : lineChannelTag;
            LineInstance = lineInstance;
        }
    }
}
