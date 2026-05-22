#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using VContainer;
using Game.Commands.VNext;
using Game.Common;
using Game.Channel;
using UnityEngine;
using Game.Vars.Generated;

namespace Game.MapNode
{
    public interface IMapNodeSystemService
    {
        MapNodeRuntime? Current { get; }

        UniTask<MapNodeRuntime> BuildAsync(
            MapNodeProfileSO profile,
            Transform parent,
            IScopeNode scopeParent,
            CancellationToken ct);

        UniTask ClearAsync(CancellationToken ct);

        bool TrySetNodeState(int nodeId, MapNodeState state);
        bool TryGetNode(int nodeId, out MapNode? node);
    }

    public sealed class MapNodeSystemService :
        IMapNodeSystemService,
        IScopeAcquireHandler,
        IScopeReleaseHandler
    {
        readonly IScopeNode _owner;
        readonly IMapNodeBuilder _builder;
        readonly IMapNodeVisualizer _visualizer;
        readonly MapNodeManager _manager;
        readonly IMapNodeSystemOptions _options;
        readonly ICommandRunner _runner;

        readonly SemaphoreSlim _mutex = new(1, 1);
        CancellationTokenSource? _buildCts;

        public MapNodeRuntime? Current => _manager.Runtime;

        public MapNodeSystemService(
            IScopeNode owner,
            IMapNodeBuilder builder,
            IMapNodeVisualizer visualizer,
            MapNodeManager manager,
            IMapNodeSystemOptions options,
            ICommandRunner runner)
        {
            _owner = owner;
            _builder = builder;
            _visualizer = visualizer;
            _manager = manager;
            _options = options;
            _runner = runner;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            if (!_options.AutoBuildOnAcquire || _options.DefaultProfile == null)
                return;

            var parent = _options.DefaultParentTransform != null ? _options.DefaultParentTransform : (_owner as Component)?.transform;
            if (parent == null)
                return;

            UniTask.Void(async () =>
            {
                try
                {
                    await BuildAsync(_options.DefaultProfile, parent, _owner, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[MapNodeSystemService] AutoBuild failed: {ex.Message}");
                }
            });
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            CancelBuild();
            _manager.Clear();
        }

        public async UniTask<MapNodeRuntime> BuildAsync(
            MapNodeProfileSO profile,
            Transform parent,
            IScopeNode scopeParent,
            CancellationToken ct)
        {
            if (profile == null)
                return CreateEmptyRuntime();

            await _mutex.WaitAsync(ct);
            try
            {
                CancelBuild();
                _buildCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                var buildCt = _buildCts.Token;

                await ClearInternalAsync();

                var runtime = await _builder.BuildAsync(profile, parent, scopeParent, _runner, buildCt);
                if (profile.Visualize != null)
                    _manager.Register(runtime.Graph, runtime, profile.Visualize);
                return runtime;
            }
            catch (OperationCanceledException)
            {
                await ClearInternalAsync();
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MapNodeSystemService] BuildAsync failed: {ex.Message}");
                if (profile.FailurePolicy == MapNodeFailurePolicy.FailFast)
                {
                    await ClearInternalAsync();
                    throw;
                }

                return CreateEmptyRuntime();
            }
            finally
            {
                _mutex.Release();
            }
        }

        public async UniTask ClearAsync(CancellationToken ct)
        {
            await _mutex.WaitAsync(ct);
            try
            {
                CancelBuild();
                await ClearInternalAsync();
            }
            finally
            {
                _mutex.Release();
            }
        }

        public bool TrySetNodeState(int nodeId, MapNodeState state)
        {
            if (!_manager.TrySetNodeState(nodeId, state))
                return false;

            if (_manager.TryGetNodeInstance(nodeId, out var instance) && instance != null)
                UpdateNodeStateBlackboard(instance, state);

            IMeshChannelControlService? lineControl = _manager.Runtime?.LineControlService;
            if (lineControl == null && _owner != null && _owner.Resolver != null)
                _owner.Resolver.TryResolve<IMeshChannelControlService>(out lineControl);

            if (lineControl != null)
                _visualizer.UpdateConnectionsForNode(_manager, nodeId, lineControl);

            return true;
        }

        public bool TryGetNode(int nodeId, out MapNode? node)
        {
            return _manager.TryGetNode(nodeId, out node);
        }

        void CancelBuild()
        {
            _buildCts?.Cancel();
            _buildCts?.Dispose();
            _buildCts = null;
        }

        async UniTask ClearInternalAsync()
        {
            var runtime = _manager.Runtime;
            if (runtime == null)
            {
                _manager.Clear();
                return;
            }

            await UniTask.SwitchToMainThread();

            var resolver = _owner?.Resolver;
            IMeshChannelControlService? lineControl = runtime.LineControlService;
            if (lineControl == null && resolver != null)
                resolver.TryResolve<IMeshChannelControlService>(out lineControl);

            if (lineControl != null)
            {
                if (runtime.Connections != null)
                {
                    for (int i = 0; i < runtime.Connections.Count; i++)
                    {
                        var connection = runtime.Connections[i];
                        if (connection == null || string.IsNullOrWhiteSpace(connection.TrackKey))
                            continue;
                        lineControl.SetTrackEnabled(runtime.LineChannelTag, connection.TrackKey, enabled: false);
                    }
                }
            }

            if (runtime.LineInstance != null)
                await ReleaseLineInstanceAsync(runtime.LineInstance);

            if (runtime.NodeInstances != null)
            {
                for (int i = 0; i < runtime.NodeInstances.Count; i++)
                {
                    var instance = runtime.NodeInstances[i];
                    if (instance == null)
                        continue;
                    await ReleaseNodeInstanceAsync(instance);
                }
            }

            _manager.Clear();
        }

        static void UpdateNodeStateBlackboard(MapNodeInstance instance, MapNodeState state)
        {
            if (instance.Resolver == null)
                return;

            if (!instance.Resolver.TryResolve<IBlackboardService>(out var blackboard) || blackboard == null)
                return;

            blackboard.LocalVars.TrySetVariant(VarIds.GameLib.MapNode.System.nodeState, DynamicVariant.FromInt((int)state));
        }

        static async UniTask ReleaseNodeInstanceAsync(MapNodeInstance instance)
        {
            if (instance == null || instance.Resolver == null)
                return;

            await ReleaseSpawnedInstanceAsync(instance.Root, instance.Scope, instance.Resolver);
        }

        static async UniTask ReleaseLineInstanceAsync(MapNodeLineInstance instance)
        {
            if (instance == null || instance.Resolver == null)
                return;

            await ReleaseSpawnedInstanceAsync(instance.Root, instance.Scope, instance.Resolver);
        }

        static async UniTask ReleaseSpawnedInstanceAsync(Transform? root, IScopeNode? scope, IRuntimeResolver? resolver)
        {
            if (resolver == null)
                return;

            try
            {
                await ScopeFeatureInstallerUtility.ReleaseSpawnedLifetimeAsync(resolver, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MapNodeSystemService] Release failed: {ex.Message}");
            }
        }

        static MapNodeRuntime CreateEmptyRuntime()
        {
            return new MapNodeRuntime(new MapGraph(new List<MapNodeLayer>(), new List<MapNode>(), 0, 0), new List<MapNodeInstance>(), new List<MapNodeConnectionInstance>());
        }
    }
}
