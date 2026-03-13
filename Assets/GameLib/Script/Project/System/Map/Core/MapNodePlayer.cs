#nullable enable
using System.Collections.Generic;

namespace Game.MapNode
{
    public sealed class MapNodePlayer
    {
        readonly MapNodeManager _manager;
        readonly MapNodePlayerState _state = new();
        readonly HashSet<int> _visitedSet = new();
        readonly List<int> _visitedOrder = new();
        readonly List<int> _nextNodeIds = new();
        readonly List<int> _notNextNodeIds = new();
        readonly HashSet<int> _nextNodeSet = new();

        public MapNodePlayerState Snapshot => _state;

        public MapNodePlayer(MapNodeManager manager)
        {
            _manager = manager;
        }

        public void Reset(MapNodeRuntime? runtime, MapNodeProfileSO? profile)
        {
            _state.Runtime = runtime;
            _state.ActiveProfile = profile;
            _state.CurrentNodeId = -1;
            _state.PreviousNodeId = -1;
            _visitedSet.Clear();
            _visitedOrder.Clear();
            _nextNodeIds.Clear();
            _notNextNodeIds.Clear();
            _nextNodeSet.Clear();
            UpdateSnapshot();
        }

        public bool IsVisited(int nodeId) => _visitedSet.Contains(nodeId);

        public void ApplyMove(int nodeId, in MapNodeMoveOptions options, bool refreshOnly)
        {
            if (!refreshOnly)
            {
                _state.PreviousNodeId = _state.CurrentNodeId;
                _state.CurrentNodeId = nodeId;
            }

            EnsureCurrentVisited();
            RefreshTargets(options);
        }

        public void RefreshTargets(in MapNodeMoveOptions options)
        {
            _nextNodeIds.Clear();
            _notNextNodeIds.Clear();
            _nextNodeSet.Clear();

            var runtime = _manager.Runtime;
            if (runtime?.Graph?.Nodes == null)
            {
                UpdateSnapshot();
                return;
            }

            if (_state.CurrentNodeId >= 0
                && _manager.TryGetNode(_state.CurrentNodeId, out var currentNode)
                && currentNode != null)
            {
                CollectNextAvailableIds(currentNode, options);
            }

            var nodes = runtime.Graph.Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null)
                    continue;
                if (node.Id == _state.CurrentNodeId)
                    continue;
                if (_nextNodeSet.Contains(node.Id))
                    continue;
                _notNextNodeIds.Add(node.Id);
            }

            UpdateSnapshot();
        }

        public static MapNodeLayerMoveDirection ResolveLayerDirection(in MapNodeMoveOptions options, int layerIndex)
        {
            var rules = options.LayerMoveRules;
            if (rules != null)
            {
                for (int i = 0; i < rules.Count; i++)
                {
                    var rule = rules[i];
                    if (rule.LayerIndex == layerIndex)
                        return rule.Direction;
                }
            }

            return options.DefaultLayerDirection;
        }

        void CollectNextAvailableIds(MapNode currentNode, in MapNodeMoveOptions options)
        {
            var direction = ResolveLayerDirection(options, currentNode.LayerIndex);
            switch (direction)
            {
                case MapNodeLayerMoveDirection.ForwardOnly:
                    AddNodeIds(currentNode.NextIds);
                    break;
                case MapNodeLayerMoveDirection.BackwardOnly:
                    AddNodeIds(currentNode.PrevIds);
                    break;
                default:
                    AddNodeIds(currentNode.NextIds);
                    AddNodeIds(currentNode.PrevIds);
                    break;
            }
        }

        void AddNodeIds(List<int> ids)
        {
            if (ids == null || ids.Count == 0)
                return;

            for (int i = 0; i < ids.Count; i++)
            {
                var nodeId = ids[i];
                if (_nextNodeSet.Contains(nodeId))
                    continue;
                if (!_manager.TryGetNode(nodeId, out var node) || node == null)
                    continue;
                if (node.State == MapNodeState.Disabled)
                    continue;
                _nextNodeSet.Add(nodeId);
                _nextNodeIds.Add(nodeId);
            }
        }

        void EnsureCurrentVisited()
        {
            if (_state.CurrentNodeId < 0)
                return;

            if (_visitedSet.Add(_state.CurrentNodeId))
                _visitedOrder.Add(_state.CurrentNodeId);
        }

        void UpdateSnapshot()
        {
            _state.NextNodeIds = _nextNodeIds.ToArray();
            _state.NotNextNodeIds = _notNextNodeIds.ToArray();
            _state.VisitedNodeIds = _visitedOrder.ToArray();
        }
    }
}
