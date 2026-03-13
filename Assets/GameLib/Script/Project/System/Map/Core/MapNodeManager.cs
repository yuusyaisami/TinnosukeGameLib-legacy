#nullable enable
using System.Collections.Generic;

namespace Game.MapNode
{
    public sealed class MapNodeManager
    {
        MapGraph? _graph;
        MapNodeRuntime? _runtime;
        MapNodeVisualizeSettingsSO? _visualSettings;

        readonly Dictionary<int, MapNode> _nodesById = new();
        readonly Dictionary<int, MapNodeInstance> _instancesById = new();
        readonly Dictionary<int, List<MapNodeConnectionInstance>> _connectionsByNode = new();

        public MapGraph? Graph => _graph;
        public MapNodeRuntime? Runtime => _runtime;
        public MapNodeVisualizeSettingsSO? VisualSettings => _visualSettings;

        public void Register(MapGraph graph, MapNodeRuntime runtime, MapNodeVisualizeSettingsSO visualSettings)
        {
            _graph = graph;
            _runtime = runtime;
            _visualSettings = visualSettings;

            _nodesById.Clear();
            _instancesById.Clear();
            _connectionsByNode.Clear();

            if (graph != null && graph.Nodes != null)
            {
                for (int i = 0; i < graph.Nodes.Count; i++)
                {
                    var node = graph.Nodes[i];
                    if (node == null)
                        continue;
                    _nodesById[node.Id] = node;
                }
            }

            if (runtime != null && runtime.NodeInstances != null)
            {
                for (int i = 0; i < runtime.NodeInstances.Count; i++)
                {
                    var instance = runtime.NodeInstances[i];
                    if (instance == null)
                        continue;
                    _instancesById[instance.NodeId] = instance;
                }
            }

            if (runtime != null && runtime.Connections != null)
            {
                for (int i = 0; i < runtime.Connections.Count; i++)
                {
                    var connection = runtime.Connections[i];
                    if (connection == null)
                        continue;

                    AddConnectionIndex(connection.FromNodeId, connection);
                    AddConnectionIndex(connection.ToNodeId, connection);
                }
            }
        }

        public void Clear()
        {
            _graph = null;
            _runtime = null;
            _visualSettings = null;
            _nodesById.Clear();
            _instancesById.Clear();
            _connectionsByNode.Clear();
        }

        public bool TryGetNode(int nodeId, out MapNode? node)
        {
            return _nodesById.TryGetValue(nodeId, out node);
        }

        public bool TryGetNodeInstance(int nodeId, out MapNodeInstance? instance)
        {
            return _instancesById.TryGetValue(nodeId, out instance);
        }

        public bool TrySetNodeState(int nodeId, MapNodeState state)
        {
            if (!_nodesById.TryGetValue(nodeId, out var node) || node == null)
                return false;

            if (node.State == state)
                return false;

            node.State = state;
            return true;
        }

        public IReadOnlyList<MapNodeConnectionInstance> GetConnectionsForNode(int nodeId)
        {
            return _connectionsByNode.TryGetValue(nodeId, out var list) ? list : System.Array.Empty<MapNodeConnectionInstance>();
        }

        void AddConnectionIndex(int nodeId, MapNodeConnectionInstance connection)
        {
            if (!_connectionsByNode.TryGetValue(nodeId, out var list))
            {
                list = new List<MapNodeConnectionInstance>();
                _connectionsByNode[nodeId] = list;
            }

            list.Add(connection);
        }
    }
}
