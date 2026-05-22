#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using Game.Commands.VNext;
using Game.Common;
using Game.Scalar;
using Game.Scalar.Generated;
using Game.Vars.Generated;
using UnityEngine;
using VContainer;

namespace Game.MapNode
{
    public interface IMapNodePlayerService
    {
        MapNodeRuntime? CurrentRuntime { get; }
        MapNodePlayerState Snapshot { get; }

        bool TryGetNode(int nodeId, out MapNode? node);
        bool TryGetNodeInstance(int nodeId, out MapNodeInstance? instance);
        bool TryGetNodeScope(int nodeId, out IScopeNode? scope);
        bool TryGetNodeRunner(int nodeId, out ICommandRunner? runner);

        IReadOnlyList<MapNode> GetNextNodes();
        IReadOnlyList<MapNode> GetNotNextNodes();
        IReadOnlyList<MapNode> GetNodesByState(MapNodeState state);
        IReadOnlyList<MapNode> GetNodesByType(MapNodeType type);
        IReadOnlyList<MapNode> GetNodesByLayerIndex(int layerIndex);

        MapNodeMoveResult TryMoveToNode(int nodeId, in MapNodeMoveOptions options);

        UniTask<MapNodeRuntime> BuildAsync(MapNodeProfileSO profile, Transform parent, IScopeNode scopeParent, CancellationToken ct);
        UniTask ClearAsync(CancellationToken ct);

        UniTask<CommandRunResult> ExecuteCommandListAtTargetAsync(
            MapNodeTarget target,
            CommandListData commands,
            CommandRunOptions options,
            CancellationToken ct);

        UniTask<bool> WriteStateAsync(MapNodePlayerSaveOptions options, CancellationToken ct);
    }

    public sealed class MapNodePlayerService :
        IMapNodePlayerService,
        IScopeAcquireHandler,
        IScopeReleaseHandler
    {
        readonly IScopeNode _owner;
        readonly IMapNodeSystemService _system;
        readonly MapNodeManager _manager;
        readonly IMapNodePlayerOptions _options;
        readonly MapNodePlayer _player;

        public MapNodeRuntime? CurrentRuntime => _manager.Runtime;
        public MapNodePlayerState Snapshot => _player.Snapshot;

        public MapNodePlayerService(
            IScopeNode owner,
            IMapNodeSystemService system,
            MapNodeManager manager,
            IMapNodePlayerOptions options)
        {
            _owner = owner;
            _system = system;
            _manager = manager;
            _options = options;
            _player = new MapNodePlayer(manager);
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            if (!_options.AutoBuildOnAcquire || _options.DefaultProfile == null)
                return;

            var parent = _options.DefaultParentTransform != null
                ? _options.DefaultParentTransform
                : (_owner as Component)?.transform;
            if (parent == null)
                return;

            UniTask.Void(async () =>
            {
                try
                {
                    await BuildAsync(_options.DefaultProfile, parent, _owner, CancellationToken.None);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[MapNodePlayerService] AutoBuild failed: {ex.Message}");
                }
            });
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _player.Reset(null, null);
        }

        public bool TryGetNode(int nodeId, out MapNode? node)
        {
            return _manager.TryGetNode(nodeId, out node);
        }

        public bool TryGetNodeInstance(int nodeId, out MapNodeInstance? instance)
        {
            return _manager.TryGetNodeInstance(nodeId, out instance);
        }

        public bool TryGetNodeScope(int nodeId, out IScopeNode? scope)
        {
            scope = null;
            if (!_manager.TryGetNodeInstance(nodeId, out var instance) || instance == null)
                return false;
            scope = instance.Scope;
            return scope != null;
        }

        public bool TryGetNodeRunner(int nodeId, out ICommandRunner? runner)
        {
            runner = null;
            if (!TryGetNodeScope(nodeId, out var scope) || scope == null)
                return false;

            var resolver = scope.Resolver;
            if (resolver == null)
                return false;

            return resolver.TryResolve(out runner) && runner != null;
        }

        public IReadOnlyList<MapNode> GetNextNodes()
        {
            return ResolveNodesByIds(Snapshot.NextNodeIds);
        }

        public IReadOnlyList<MapNode> GetNotNextNodes()
        {
            return ResolveNodesByIds(Snapshot.NotNextNodeIds);
        }

        public IReadOnlyList<MapNode> GetNodesByState(MapNodeState state)
        {
            return ResolveNodesByPredicate(node => node.State == state);
        }

        public IReadOnlyList<MapNode> GetNodesByType(MapNodeType type)
        {
            return ResolveNodesByPredicate(node => node.Type == type);
        }

        public IReadOnlyList<MapNode> GetNodesByLayerIndex(int layerIndex)
        {
            var runtime = _manager.Runtime;
            if (runtime?.Graph?.Layers == null)
                return Array.Empty<MapNode>();

            MapNodeLayer? layer = null;
            var layers = runtime.Graph.Layers;
            for (int i = 0; i < layers.Count; i++)
            {
                var candidate = layers[i];
                if (candidate != null && candidate.LayerIndex == layerIndex)
                {
                    layer = candidate;
                    break;
                }
            }

            if (layer == null || layer.NodeIds == null || layer.NodeIds.Count == 0)
                return Array.Empty<MapNode>();

            var results = new List<MapNode>(layer.NodeIds.Count);
            for (int i = 0; i < layer.NodeIds.Count; i++)
            {
                var nodeId = layer.NodeIds[i];
                if (_manager.TryGetNode(nodeId, out var node) && node != null)
                    results.Add(node);
            }

            return results.Count == 0 ? Array.Empty<MapNode>() : results.ToArray();
        }

        public MapNodeMoveResult TryMoveToNode(int nodeId, in MapNodeMoveOptions options)
        {
            if (_manager.Runtime == null)
                return MapNodeMoveResult.NotBuilt;

            if (!_manager.TryGetNode(nodeId, out var node) || node == null)
                return MapNodeMoveResult.InvalidNodeId;

            var currentId = Snapshot.CurrentNodeId;
            if (currentId >= 0)
            {
                if (!_manager.TryGetNode(currentId, out var currentNode) || currentNode == null)
                    return MapNodeMoveResult.NoCurrentNode;

                if (nodeId != currentId && !IsNextAvailable(currentNode, nodeId, options))
                    return MapNodeMoveResult.NotAllowedByState;
            }

            var refreshOnly = currentId >= 0 && currentId == nodeId;
            if (!refreshOnly && !IsMoveAllowed(nodeId, node, options))
                return MapNodeMoveResult.NotAllowedByState;
            _player.ApplyMove(nodeId, options, refreshOnly);

            if (!refreshOnly && currentId >= 0)
            {
                TrySetNodeState(currentId, options.StateForPrevious);
            }

            if (nodeId >= 0)
            {
                TrySetNodeState(nodeId, options.StateForCurrent);
            }

            if (options.AutoUnlockNext || options.AutoLockOthers)
            {
                ApplyAutoNodeStates(nodeId, options);
            }

            if (_options.AutoSaveOnMove)
            {
                UniTask.Void(async () =>
                {
                    try
                    {
                        await WriteStateAsync(_options.DefaultSaveOptions, CancellationToken.None);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[MapNodePlayerService] AutoSave failed: {ex.Message}");
                    }
                });
            }

            if (!refreshOnly && nodeId != currentId)
            {
                TryExecuteOnSelectedNodeChangedCommands(nodeId);
            }

            UpdateIsVisitableNodeBlackboard();

            return MapNodeMoveResult.Ok;
        }

        void TryExecuteOnSelectedNodeChangedCommands(int currentNodeId)
        {
            if (currentNodeId < 0)
                return;

            var commands = _options.OnSelectedNodeChangedCommands;
            if (commands == null || commands.Count == 0)
                return;

            var target = new MapNodeTarget
            {
                Kind = MapNodeTargetKind.Current
            };

            UniTask.Void(async () =>
            {
                try
                {
                    var result = await ExecuteCommandListAtTargetAsync(
                        target,
                        commands,
                        CommandRunOptions.Default,
                        CancellationToken.None);

                    if (result.Status == CommandRunStatus.Error || result.FailureCount > 0)
                    {
                        Debug.LogError($"[MapNodePlayerService] OnSelectedNodeChanged command failed: FailureCount={result.FailureCount} ErrorIndex={result.ErrorIndex} Message={result.Message}");
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[MapNodePlayerService] OnSelectedNodeChanged command failed: {ex.Message}");
                }
            });
        }

        public async UniTask<MapNodeRuntime> BuildAsync(
            MapNodeProfileSO profile,
            Transform parent,
            IScopeNode scopeParent,
            CancellationToken ct)
        {
            try
            {
                var runtime = await _system.BuildAsync(profile, parent, scopeParent, ct);
                _player.Reset(runtime, profile);
                UpdateIsVisitableNodeBlackboard();
                return runtime;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MapNodePlayerService] BuildAsync failed: {ex.Message}");
                var runtime = CreateEmptyRuntime();
                _player.Reset(runtime, profile);
                return runtime;
            }
        }

        public async UniTask ClearAsync(CancellationToken ct)
        {
            try
            {
                await _system.ClearAsync(ct);
            }
            finally
            {
                _player.Reset(null, null);
            }
        }

        public async UniTask<CommandRunResult> ExecuteCommandListAtTargetAsync(
            MapNodeTarget target,
            CommandListData commands,
            CommandRunOptions options,
            CancellationToken ct)
        {
            if (commands == null || commands.Count == 0)
                return CommandRunResult.Completed(-1, 0, CommandRunFailureKind.None, -1, string.Empty, null, null);

            var targetIds = ResolveTargetNodeIds(target);
            if (targetIds.Count == 0)
                return CommandRunResult.Error(-1, -1, CommandRunFailureKind.ResolveFailed, "Target resolved to no nodes.", null, null);

            CommandRunResult lastResult = CommandRunResult.Completed(-1, 0, CommandRunFailureKind.None, -1, string.Empty, null, null);
            for (int i = 0; i < targetIds.Count; i++)
            {
                var nodeId = targetIds[i];
                if (!TryGetNodeScope(nodeId, out var nodeScope) || nodeScope == null)
                {
                    return CommandRunResult.Error(-1, -1, CommandRunFailureKind.ResolveFailed, $"Node scope not found. nodeId={nodeId}", null, null);
                }

                EnsureScopeBuiltIfNeeded(nodeScope);
                if (nodeScope.Resolver == null)
                {
                    return CommandRunResult.Error(-1, -1, CommandRunFailureKind.ResolveFailed, $"Node scope resolver is null. nodeId={nodeId}", null, null);
                }
                if (!TryResolveRunner(nodeScope, out var runner) || runner == null)
                {
                    return CommandRunResult.Error(-1, -1, CommandRunFailureKind.ExecutorMissing, $"ICommandRunner not found. nodeId={nodeId}", null, null);
                }

                var ctx = new CommandContext(nodeScope, new VarStore(), runner, nodeScope, options);
                try
                {
                    var result = await runner.ExecuteListAsync(commands, ctx, ct, options);
                    lastResult = result;
                    if (result.Status == CommandRunStatus.Canceled)
                        return result;
                    if (result.Status == CommandRunStatus.Error || result.FailureCount > 0)
                        return result;
                }
                catch (OperationCanceledException)
                {
                    return CommandRunResult.Canceled(-1, -1, "Canceled.", null);
                }
                catch (Exception ex)
                {
                    return CommandRunResult.Error(-1, -1, CommandRunFailureKind.Exception, ex.Message, null, CommandExceptionInfo.FromException(ex, true));
                }
            }

            return lastResult;
        }

        public async UniTask<bool> WriteStateAsync(MapNodePlayerSaveOptions options, CancellationToken ct)
        {
            if (options.Target == MapNodePlayerSaveTarget.None)
                return false;

            var scope = await ResolveSaveScopeAsync(options, ct);
            if (scope == null || scope.Resolver == null)
                return false;

            var wrote = false;
            if (options.Target == MapNodePlayerSaveTarget.Blackboard || options.Target == MapNodePlayerSaveTarget.Both)
            {
                if (scope.Resolver.TryResolve<IBlackboardService>(out var blackboard) && blackboard != null)
                {
                    WriteToBlackboard(blackboard, options);
                    wrote = true;
                }
            }

            if (options.Target == MapNodePlayerSaveTarget.Scalar || options.Target == MapNodePlayerSaveTarget.Both)
            {
                if (scope.Resolver.TryResolve<IBaseScalarService>(out var scalar) && scalar != null)
                {
                    WriteToScalar(scalar, options);
                    wrote = true;
                }
            }

            return wrote;
        }

        IReadOnlyList<int> ResolveTargetNodeIds(MapNodeTarget target)
        {
            switch (target.Kind)
            {
                case MapNodeTargetKind.Current:
                    return Snapshot.CurrentNodeId >= 0
                        ? new[] { Snapshot.CurrentNodeId }
                        : Array.Empty<int>();
                case MapNodeTargetKind.NextAvailable:
                    return Snapshot.NextNodeIds;
                case MapNodeTargetKind.NotNext:
                    return Snapshot.NotNextNodeIds;
                case MapNodeTargetKind.ByIdList:
                    if (target.NodeIds != null && target.NodeIds.Length > 0)
                        return target.NodeIds;
                    return target.SingleNodeId >= 0
                        ? new[] { target.SingleNodeId }
                        : Array.Empty<int>();
                case MapNodeTargetKind.ByState:
                    return ExtractNodeIds(GetNodesByState(target.State));
                case MapNodeTargetKind.ByType:
                    return ExtractNodeIds(GetNodesByType(target.Type));
                case MapNodeTargetKind.ByLayerIndex:
                    return ExtractNodeIds(GetNodesByLayerIndex(target.LayerIndex));
                default:
                    return Array.Empty<int>();
            }
        }

        static IReadOnlyList<int> ExtractNodeIds(IReadOnlyList<MapNode> nodes)
        {
            if (nodes == null || nodes.Count == 0)
                return Array.Empty<int>();

            var ids = new List<int>(nodes.Count);
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node != null)
                    ids.Add(node.Id);
            }

            return ids.Count == 0 ? Array.Empty<int>() : ids.ToArray();
        }

        IReadOnlyList<MapNode> ResolveNodesByPredicate(Func<MapNode, bool> predicate)
        {
            var runtime = _manager.Runtime;
            if (runtime?.Graph?.Nodes == null)
                return Array.Empty<MapNode>();

            var nodes = runtime.Graph.Nodes;
            var results = new List<MapNode>();
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null)
                    continue;
                if (predicate(node))
                    results.Add(node);
            }

            return results.Count == 0 ? Array.Empty<MapNode>() : results.ToArray();
        }

        IReadOnlyList<MapNode> ResolveNodesByIds(IReadOnlyList<int> ids)
        {
            if (ids == null || ids.Count == 0)
                return Array.Empty<MapNode>();

            var results = new List<MapNode>(ids.Count);
            for (int i = 0; i < ids.Count; i++)
            {
                var nodeId = ids[i];
                if (_manager.TryGetNode(nodeId, out var node) && node != null)
                    results.Add(node);
            }

            return results.Count == 0 ? Array.Empty<MapNode>() : results.ToArray();
        }

        bool IsMoveAllowed(int nodeId, MapNode node, in MapNodeMoveOptions options)
        {
            if (node.State == MapNodeState.Disabled && !options.AllowMoveToDisabled)
                return false;
            if (node.State == MapNodeState.Locked && !options.AllowMoveToLocked)
                return false;
            if (node.State == MapNodeState.Completed && !options.AllowMoveToCompleted)
                return false;
            if (node.State == MapNodeState.Visited && !options.AllowMoveToVisited)
                return false;

            if (_player.IsVisited(nodeId) && !options.AllowMoveToVisited)
                return false;

            return true;
        }

        bool IsNextAvailable(MapNode currentNode, int targetNodeId, in MapNodeMoveOptions options)
        {
            var direction = MapNodePlayer.ResolveLayerDirection(options, currentNode.LayerIndex);
            var allowed = direction switch
            {
                MapNodeLayerMoveDirection.ForwardOnly => currentNode.NextIds.Contains(targetNodeId),
                MapNodeLayerMoveDirection.BackwardOnly => currentNode.PrevIds.Contains(targetNodeId),
                _ => currentNode.NextIds.Contains(targetNodeId) || currentNode.PrevIds.Contains(targetNodeId),
            };

            if (!allowed)
                return false;

            if (_manager.TryGetNode(targetNodeId, out var target) && target != null)
                return target.State != MapNodeState.Disabled;

            return false;
        }

        void ApplyAutoNodeStates(int currentNodeId, in MapNodeMoveOptions options)
        {
            if (!_manager.TryGetNode(currentNodeId, out var currentNode) || currentNode == null)
                return;

            if (options.AutoUnlockNext)
            {
                for (int i = 0; i < currentNode.NextIds.Count; i++)
                {
                    var nodeId = currentNode.NextIds[i];
                    TrySetNodeState(nodeId, MapNodeState.Available);
                }
            }

            if (!options.AutoLockOthers)
                return;

            var runtime = _manager.Runtime;
            if (runtime?.Graph?.Nodes == null)
                return;

            var nodes = runtime.Graph.Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null)
                    continue;
                if (_player.IsVisited(node.Id))
                    continue;
                if (node.State == MapNodeState.Disabled)
                    continue;
                TrySetNodeState(node.Id, MapNodeState.Locked);
            }
        }

        bool TrySetNodeState(int nodeId, MapNodeState state)
        {
            if (_system.TrySetNodeState(nodeId, state))
                return true;

            return _manager.TrySetNodeState(nodeId, state);
        }

        async UniTask<IScopeNode?> ResolveSaveScopeAsync(MapNodePlayerSaveOptions options, CancellationToken ct)
        {
            if (!options.UseOverrideScope)
                return _owner;

            if (_owner == null || _owner.Resolver == null)
                return _owner;

            if (!_owner.Resolver.TryResolve<ICommandRunner>(out var runner) || runner == null)
                return _owner;

            var ctx = new CommandContext(_owner, new VarStore(), runner, _owner, CommandRunOptions.Default);
            try
            {
                var (scope, error) = await ActorScopeResolver.ResolveAsync(options.OverrideScope, ctx, ct);
                if (scope != null)
                    return scope;
                if (!string.IsNullOrEmpty(error))
                    Debug.LogError($"[MapNodePlayerService] Override scope resolve failed: {error}");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MapNodePlayerService] Override scope resolve failed: {ex.Message}");
            }

            return _owner;
        }

        void WriteToBlackboard(IBlackboardService blackboard, MapNodePlayerSaveOptions options)
        {
            if (blackboard == null)
                return;

            var vars = blackboard.LocalVars;
            if (vars == null)
                return;

            if (options.WriteCurrentNode && Snapshot.CurrentNodeId >= 0)
            {
                if (_manager.TryGetNode(Snapshot.CurrentNodeId, out var node) && node != null)
                {
                    var runtime = _manager.Runtime;
                    var graph = runtime?.Graph;
                    var layerWidth = ResolveLayerWidth(graph, node.LayerIndex);
                    var worldPos = ResolveNodeWorldPos(node.Id);

                    vars.TrySetVariant(VarIds.GameLib.MapNode.System.nodeId, DynamicVariant.FromInt(node.Id));
                    vars.TrySetVariant(VarIds.GameLib.MapNode.System.nodeType, DynamicVariant.FromInt((int)node.Type));
                    vars.TrySetVariant(VarIds.GameLib.MapNode.System.nodeState, DynamicVariant.FromInt((int)node.State));
                    vars.TrySetVariant(VarIds.GameLib.MapNode.System.layerIndex, DynamicVariant.FromInt(node.LayerIndex));
                    vars.TrySetVariant(VarIds.GameLib.MapNode.System.widthIndex, DynamicVariant.FromInt(node.WidthIndex));
                    vars.TrySetVariant(VarIds.GameLib.MapNode.System.layerWidth, DynamicVariant.FromInt(layerWidth));
                    vars.TrySetVariant(VarIds.GameLib.MapNode.System.totalDepth, DynamicVariant.FromInt(graph?.Depth ?? 0));
                    vars.TrySetVariant(VarIds.GameLib.MapNode.System.totalWidth, DynamicVariant.FromInt(graph?.MaxWidth ?? 0));
                    vars.TrySetVariant(VarIds.GameLib.MapNode.System.worldNodePos, DynamicVariant.FromVector3(worldPos));

                    if (VarIds.GameLib.MapNode.System.prevNodeIds != 0)
                        vars.TrySetManagedRef(VarIds.GameLib.MapNode.System.prevNodeIds, node.PrevIds.ToArray());
                    if (VarIds.GameLib.MapNode.System.nextNodeIds != 0)
                        vars.TrySetManagedRef(VarIds.GameLib.MapNode.System.nextNodeIds, node.NextIds.ToArray());
                }
            }

            if (options.WriteNodeLists)
            {
                if (VarIds.GameLib.MapNode.Player.availableNodeIds != 0)
                    vars.TrySetManagedRef(VarIds.GameLib.MapNode.Player.availableNodeIds, ToArray(Snapshot.NextNodeIds));

                var notNextKey = ResolveVarId("GameLib/MapNode/Player/notNextNodeIds");
                if (notNextKey != 0)
                    vars.TrySetManagedRef(notNextKey, ToArray(Snapshot.NotNextNodeIds));

                if (VarIds.GameLib.MapNode.Player.visitedNodeIds != 0)
                    vars.TrySetManagedRef(VarIds.GameLib.MapNode.Player.visitedNodeIds, ToArray(Snapshot.VisitedNodeIds));
            }
        }

        void WriteToScalar(IBaseScalarService scalar, MapNodePlayerSaveOptions options)
        {
            if (scalar == null)
                return;

            if (!options.WriteCurrentNode || Snapshot.CurrentNodeId < 0)
                return;

            if (!_manager.TryGetNode(Snapshot.CurrentNodeId, out var node) || node == null)
                return;

            var runtime = _manager.Runtime;
            var graph = runtime?.Graph;
            scalar.SetLocalBase(ScalarKeys.GameLib.MapNodePlayer.CurrentNodeId, node.Id);
            scalar.SetLocalBase(ScalarKeys.GameLib.MapNodePlayer.LayerIndex, node.LayerIndex);
            scalar.SetLocalBase(ScalarKeys.GameLib.MapNodePlayer.WidthIndex, node.WidthIndex);
            scalar.SetLocalBase(ScalarKeys.GameLib.MapNodePlayer.NodeType, (int)node.Type);
            scalar.SetLocalBase(ScalarKeys.GameLib.MapNodePlayer.NodeState, (int)node.State);
            scalar.SetLocalBase(ScalarKeys.GameLib.MapNodePlayer.TotalDepth, graph?.Depth ?? 0);
            scalar.SetLocalBase(ScalarKeys.GameLib.MapNodePlayer.TotalWidth, graph?.MaxWidth ?? 0);
        }

        static int ResolveLayerWidth(MapGraph? graph, int layerIndex)
        {
            if (graph == null || graph.Layers == null)
                return 1;
            if (layerIndex < 0 || layerIndex >= graph.Layers.Count)
                return 1;
            var layer = graph.Layers[layerIndex];
            return layer != null ? layer.NodeIds.Count : 1;
        }

        Vector3 ResolveNodeWorldPos(int nodeId)
        {
            if (_manager.TryGetNodeInstance(nodeId, out var instance) && instance != null)
                return instance.WorldPos;
            return Vector3.zero;
        }

        static int ResolveVarId(string stableKey)
        {
            return VarIdResolver.TryResolve(stableKey, out var varId) ? varId : 0;
        }

        static int[] ToArray(IReadOnlyList<int> list)
        {
            if (list == null || list.Count == 0)
                return Array.Empty<int>();
            if (list is int[] arr)
                return arr;

            var buffer = new int[list.Count];
            for (int i = 0; i < list.Count; i++)
                buffer[i] = list[i];
            return buffer;
        }

        static void EnsureScopeBuiltIfNeeded(IScopeNode scope)
        {
            ScopeFeatureInstallerUtility.EnsureScopeBuiltIfNeeded(scope);
        }

        static bool TryResolveRunner(IScopeNode scope, out ICommandRunner? runner)
        {
            runner = null;
            var resolver = scope?.Resolver;
            if (resolver == null)
                return false;

            return resolver.TryResolve(out runner) && runner != null;
        }

        void UpdateIsVisitableNodeBlackboard()
        {
            var runtime = _manager.Runtime;
            if (runtime?.NodeInstances == null)
                return;

            var nextNodeIds = Snapshot.NextNodeIds;
            var nextSet = new HashSet<int>();
            if (nextNodeIds != null)
            {
                for (int i = 0; i < nextNodeIds.Count; i++)
                    nextSet.Add(nextNodeIds[i]);
            }

            var isVisitableNodeVarId = ResolveVarId("GameLib/MapNode/Player/isVisitableNode");
            if (isVisitableNodeVarId == 0)
                return;

            var nodeInstances = runtime.NodeInstances;
            for (int i = 0; i < nodeInstances.Count; i++)
            {
                var instance = nodeInstances[i];
                if (instance == null || instance.Resolver == null)
                    continue;

                if (!instance.Resolver.TryResolve<IBlackboardService>(out var blackboard) || blackboard == null)
                    continue;

                var isVisitable = nextSet.Contains(instance.NodeId);
                blackboard.LocalVars.TrySetVariant(isVisitableNodeVarId, DynamicVariant.FromBool(isVisitable));
            }
        }

        static MapNodeRuntime CreateEmptyRuntime()
        {
            return new MapNodeRuntime(new MapGraph(new List<MapNodeLayer>(), new List<MapNode>(), 0, 0), new List<MapNodeInstance>(), new List<MapNodeConnectionInstance>());
        }
    }
}
