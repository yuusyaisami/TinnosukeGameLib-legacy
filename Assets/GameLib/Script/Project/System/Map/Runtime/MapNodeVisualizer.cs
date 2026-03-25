#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using VContainer;
using Game.Channel;
using Game.Commands.VNext;
using Game.Common;
using Game.Spawn;
using Game.UI;
using UnityEngine;
using Game.Vars.Generated;

namespace Game.MapNode
{
    public interface IMapNodeVisualizer
    {
        UniTask<MapNodeRuntime> BuildRuntimeAsync(
            MapGraph graph,
            MapNodeVisualizeSettingsSO settings,
            Transform parent,
            IScopeNode scopeParent,
            ICommandRunner runner,
            MapNodeFailurePolicy failurePolicy,
            CancellationToken ct);

        void UpdateConnectionsForNode(MapNodeManager manager, int nodeId, IMeshChannelControlService lineControlService);
    }

    public sealed class MapNodeVisualizer : IMapNodeVisualizer
    {
        readonly ISceneSpawnerRegistry _registry;

        public MapNodeVisualizer(ISceneSpawnerRegistry registry)
        {
            _registry = registry;
        }

        public async UniTask<MapNodeRuntime> BuildRuntimeAsync(
            MapGraph graph,
            MapNodeVisualizeSettingsSO settings,
            Transform parent,
            IScopeNode scopeParent,
            ICommandRunner runner,
            MapNodeFailurePolicy failurePolicy,
            CancellationToken ct)
        {
            if (graph == null || settings == null || parent == null || scopeParent == null || runner == null)
                return new MapNodeRuntime(CreateEmptyGraph(graph), new List<MapNodeInstance>(), new List<MapNodeConnectionInstance>());

            var resolver = scopeParent.Resolver;
            IMeshChannelControlService? lineControlService = null;
            MapNodeLineInstance? lineInstance = null;
            if (!settings.UseLineSpawn)
                resolver?.TryResolve(out lineControlService);

            var spawner = ResolveSpawner(settings.Space, settings.SpawnSource, failurePolicy, _registry);
            if (spawner == null)
                return new MapNodeRuntime(graph, new List<MapNodeInstance>(), new List<MapNodeConnectionInstance>(), lineControlService, settings.LineChannelTag);

            var dynamicContext = new SimpleDynamicContext(NullVarStore.Instance, scopeParent);

            IAsyncSpawnerService? lineSpawner = null;
            if (settings.UseLineSpawn)
                lineSpawner = ResolveSpawner(settings.Space, settings.LineSpawnSource, failurePolicy, _registry);

            await UniTask.SwitchToMainThread();

            if (settings.UseLineSpawn && lineSpawner != null)
            {
                var worldSpace = settings.Space == MapNodeSpace.World;
                var position = Vector3.zero;
                var rotation = Quaternion.identity;
                var scale = Vector3.one;
                var hasLineSpawnParams = false;
                var lineSpawnParams = SpawnParams.Default;

                if (settings.LineSpawnSource == MapNodeSpawnSource.RuntimeTemplate)
                {
                    if (!settings.TryResolveLineRuntimeTemplate(dynamicContext, out var lineRuntimeTemplate) || lineRuntimeTemplate == null)
                    {
                        if (failurePolicy == MapNodeFailurePolicy.FailFast)
                            throw new InvalidOperationException("[MapNodeVisualizer] Line RuntimeTemplate is null.");
                    }
                    else
                    {
                        lineSpawnParams = SpawnParams.ForRuntime(
                            lineRuntimeTemplate,
                            position,
                            rotation,
                            scale,
                            identity: null,
                            transformParent: parent,
                            lifetimeScopeParent: scopeParent,
                            worldSpace: worldSpace,
                            allowPooling: settings.LineAllowPooling);
                        hasLineSpawnParams = true;
                    }
                }
                else
                {
                    if (settings.LinePrefab == null)
                    {
                        if (failurePolicy == MapNodeFailurePolicy.FailFast)
                            throw new InvalidOperationException("[MapNodeVisualizer] Line Prefab is null.");
                    }
                    else
                    {
                        lineSpawnParams = SpawnParams.ForLTS(
                            settings.LinePrefab,
                            position,
                            rotation,
                            scale,
                            transformParent: parent,
                            lifetimeScopeParent: scopeParent,
                            worldSpace: worldSpace,
                            allowPooling: settings.LineAllowPooling);
                        hasLineSpawnParams = true;
                    }
                }

                if (hasLineSpawnParams)
                {
                    IObjectResolver? lineResolver = null;
                    try
                    {
                        lineResolver = await lineSpawner.SpawnAsync(lineSpawnParams, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[MapNodeVisualizer] Line spawn failed: {ex.Message}");
                        if (failurePolicy == MapNodeFailurePolicy.FailFast)
                            throw;
                    }

                    if (lineResolver != null)
                    {
                        ExtractSpawnedInfo(lineResolver, out var lineRoot, out var lineScope, out _, out _);
                        if (lineRoot == null || lineScope == null)
                        {
                            if (failurePolicy == MapNodeFailurePolicy.FailFast)
                                throw new InvalidOperationException("[MapNodeVisualizer] Spawned line missing scope or root.");
                        }
                        else
                        {
                            lineInstance = new MapNodeLineInstance(lineRoot, lineScope, lineResolver);
                            lineResolver.TryResolve(out lineControlService);
                            if (lineControlService == null && failurePolicy == MapNodeFailurePolicy.FailFast)
                                throw new InvalidOperationException("[MapNodeVisualizer] Mesh channel control service not found.");
                        }
                    }
                }
            }

            if (lineControlService == null && failurePolicy != MapNodeFailurePolicy.FailFast)
                resolver?.TryResolve(out lineControlService);

            var instances = new List<MapNodeInstance>(graph.Nodes.Count);
            var instanceLookup = new Dictionary<int, MapNodeInstance>(graph.Nodes.Count);

            var yieldEvery = 64;
            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var node = graph.Nodes[i];
                if (node == null)
                    continue;

                var layerWidth = ResolveLayerWidth(graph, node.LayerIndex);
                var localPos = ComputeNodePosition(settings, node.LayerIndex, node.WidthIndex, layerWidth);
                var worldSpace = settings.Space == MapNodeSpace.World;
                var pos = localPos;
                var rotation = Quaternion.identity;
                var scale = Vector3.one;

                SpawnParams spawnParams;
                if (settings.SpawnSource == MapNodeSpawnSource.RuntimeTemplate)
                {
                    if (!settings.TryResolveRuntimeTemplate(dynamicContext, out var runtimeTemplate) || runtimeTemplate == null)
                    {
                        if (failurePolicy == MapNodeFailurePolicy.FailFast)
                            throw new InvalidOperationException("[MapNodeVisualizer] RuntimeTemplate is null.");
                        continue;
                    }

                    spawnParams = SpawnParams.ForRuntime(
                        runtimeTemplate,
                        pos,
                        rotation,
                        scale,
                        identity: null,
                        transformParent: parent,
                        lifetimeScopeParent: scopeParent,
                        worldSpace: worldSpace,
                        allowPooling: settings.AllowPooling);
                }
                else
                {
                    if (settings.Prefab == null)
                    {
                        if (failurePolicy == MapNodeFailurePolicy.FailFast)
                            throw new InvalidOperationException("[MapNodeVisualizer] Prefab is null.");
                        continue;
                    }

                    spawnParams = SpawnParams.ForLTS(
                        settings.Prefab,
                        pos,
                        rotation,
                        scale,
                        transformParent: parent,
                        lifetimeScopeParent: scopeParent,
                        worldSpace: worldSpace,
                        allowPooling: settings.AllowPooling);
                }

                IObjectResolver? spawnedResolver = null;
                try
                {
                    spawnedResolver = await spawner.SpawnAsync(spawnParams, ct);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[MapNodeVisualizer] Spawn failed: {ex.Message}");
                    if (failurePolicy == MapNodeFailurePolicy.FailFast)
                        throw;
                    continue;
                }

                if (spawnedResolver == null)
                    continue;

                ExtractSpawnedInfo(spawnedResolver, out var root, out var scope, out _, out _);
                if (root == null || scope == null)
                {
                    if (failurePolicy == MapNodeFailurePolicy.FailFast)
                        throw new InvalidOperationException("[MapNodeVisualizer] Spawned node missing scope or root.");
                    continue;
                }

                var worldPos = root.position;
                var instance = new MapNodeInstance(node.Id, root, scope, spawnedResolver, worldPos);
                instances.Add(instance);
                instanceLookup[node.Id] = instance;

                ApplyBlackboard(node, graph, layerWidth, instance, settings.DefaultAnimPreset);
                await ExecuteCommandsAsync(node, graph, layerWidth, instance, settings, runner, failurePolicy, ct);
                ApplyUIButtonCommands(node, settings, instance);

                if (instances.Count % yieldEvery == 0)
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            var connections = BuildConnections(graph, settings, instanceLookup, lineControlService);
            if (graph.Nodes.Count > 0)
            {
                await UniTask.Yield(PlayerLoopTiming.PostLateUpdate, ct);
                if (lineControlService == null && lineInstance != null)
                    lineInstance.Resolver?.TryResolve(out lineControlService);
                if (lineControlService == null)
                    resolver?.TryResolve(out lineControlService);
                if (lineControlService != null)
                    RefreshConnections(graph, settings, instanceLookup, connections, lineControlService);
            }

            if (lineControlService == null)
                Debug.LogWarning("[MapNodeVisualizer] MeshChannel control service not resolved. Lines will not render.");

            return new MapNodeRuntime(graph, instances, connections, lineControlService, settings.LineChannelTag, lineInstance);
        }

        public void UpdateConnectionsForNode(MapNodeManager manager, int nodeId, IMeshChannelControlService lineControlService)
        {
            if (manager == null || lineControlService == null)
                return;

            var runtime = manager.Runtime;
            var graph = manager.Graph;
            var settings = manager.VisualSettings;
            if (runtime == null || graph == null || settings == null)
                return;

            if (!manager.TryGetNode(nodeId, out var _))
                return;

            var connections = manager.GetConnectionsForNode(nodeId);

            for (int i = 0; i < connections.Count; i++)
            {
                var connection = connections[i];
                if (connection == null)
                    continue;

                if (!manager.TryGetNode(connection.FromNodeId, out var fromNode) || fromNode == null)
                    continue;
                if (!manager.TryGetNodeInstance(connection.FromNodeId, out var fromInstance) || fromInstance == null)
                    continue;
                if (!manager.TryGetNodeInstance(connection.ToNodeId, out var toInstance) || toInstance == null)
                    continue;

                var style = ResolveLineStyle(settings.LineSettings, fromNode);
                if (!TryApplyConnection(settings.LineChannelTag, lineControlService, connection, fromInstance, toInstance, style))
                    continue;
            }
        }

        static IAsyncSpawnerService? ResolveSpawner(
            MapNodeSpace space,
            MapNodeSpawnSource source,
            MapNodeFailurePolicy failurePolicy,
            ISceneSpawnerRegistry registry)
        {
            var kind = ResolveSpawnerKind(space, source);
            if (registry == null)
                return null;

            var resolved = registry.TryGet<IAsyncSpawnerService>(kind, "");
            if (resolved == null && failurePolicy == MapNodeFailurePolicy.FailFast)
                throw new InvalidOperationException($"[MapNodeVisualizer] Spawner not found. kind={kind}");
            return resolved;
        }

        static SpawnerKind ResolveSpawnerKind(MapNodeSpace space, MapNodeSpawnSource source)
        {
            if (space == MapNodeSpace.UI)
                return source == MapNodeSpawnSource.RuntimeTemplate ? SpawnerKind.RuntimeUIElement : SpawnerKind.UIElement;

            return source == MapNodeSpawnSource.RuntimeTemplate ? SpawnerKind.RuntimeEntity : SpawnerKind.Entity;
        }

        static Vector3 ComputeNodePosition(MapNodeVisualizeSettingsSO settings, int layerIndex, int widthIndex, int layerWidth)
        {
            var centeredOffset = settings.Centered ? (layerWidth - 1) * 0.5f : 0f;
            var widthFactor = widthIndex - centeredOffset;

            var pos2 = new Vector2(
                settings.WidthSpacing.x * widthFactor + settings.LayerSpacing.x * layerIndex + settings.AlignOffset.x,
                settings.WidthSpacing.y * widthFactor + settings.LayerSpacing.y * layerIndex + settings.AlignOffset.y);

            if (settings.JitterRange.x != 0f || settings.JitterRange.y != 0f)
            {
                pos2.x += UnityEngine.Random.Range(-settings.JitterRange.x, settings.JitterRange.x);
                pos2.y += UnityEngine.Random.Range(-settings.JitterRange.y, settings.JitterRange.y);
            }

            return new Vector3(pos2.x, pos2.y, 0f);
        }

        static int ResolveLayerWidth(MapGraph graph, int layerIndex)
        {
            if (graph == null || graph.Layers == null || layerIndex < 0 || layerIndex >= graph.Layers.Count)
                return 1;
            var layer = graph.Layers[layerIndex];
            return layer != null ? layer.NodeIds.Count : 1;
        }

        static MapGraph CreateEmptyGraph(MapGraph? graph)
        {
            if (graph != null)
                return graph;

            return new MapGraph(new List<MapNodeLayer>(), new List<MapNode>(), depth: 0, maxWidth: 0);
        }

        static void ApplyBlackboard(
            MapNode node,
            MapGraph graph,
            int layerWidth,
            MapNodeInstance instance,
            AnimationSpritePreset? preset)
        {
            if (instance.Resolver == null)
                return;

            if (!instance.Resolver.TryResolve<IBlackboardService>(out var blackboard) || blackboard == null)
                return;

            var vars = blackboard.LocalVars;
            TrySetVariant(vars, VarIds.GameLib.MapNode.System.nodeId, DynamicVariant.FromInt(node.Id));
            TrySetVariant(vars, VarIds.GameLib.MapNode.System.nodeType, DynamicVariant.FromInt((int)node.Type));
            TrySetVariant(vars, VarIds.GameLib.MapNode.System.nodeState, DynamicVariant.FromInt((int)node.State));
            TrySetVariant(vars, VarIds.GameLib.MapNode.System.layerIndex, DynamicVariant.FromInt(node.LayerIndex));
            TrySetVariant(vars, VarIds.GameLib.MapNode.System.widthIndex, DynamicVariant.FromInt(node.WidthIndex));
            TrySetVariant(vars, VarIds.GameLib.MapNode.System.layerWidth, DynamicVariant.FromInt(layerWidth));
            TrySetVariant(vars, VarIds.GameLib.MapNode.System.totalDepth, DynamicVariant.FromInt(graph.Depth));
            TrySetVariant(vars, VarIds.GameLib.MapNode.System.totalWidth, DynamicVariant.FromInt(graph.MaxWidth));
            TrySetVariant(vars, VarIds.GameLib.MapNode.System.worldNodePos, DynamicVariant.FromVector3(instance.WorldPos));

            if (VarIds.GameLib.MapNode.System.prevNodeIds != 0)
                vars.TrySetManagedRef(VarIds.GameLib.MapNode.System.prevNodeIds, node.PrevIds.ToArray());
            if (VarIds.GameLib.MapNode.System.nextNodeIds != 0)
                vars.TrySetManagedRef(VarIds.GameLib.MapNode.System.nextNodeIds, node.NextIds.ToArray());

            if (preset != null && VarIds.GameLib.MapNode.System.animNodePreset != 0)
                vars.TrySetManagedRef(VarIds.GameLib.MapNode.System.animNodePreset, preset);
        }

        static async UniTask ExecuteCommandsAsync(
            MapNode node,
            MapGraph graph,
            int layerWidth,
            MapNodeInstance instance,
            MapNodeVisualizeSettingsSO settings,
            ICommandRunner runner,
            MapNodeFailurePolicy failurePolicy,
            CancellationToken ct)
        {
            var ctx = CreateCommandContext(node, graph, layerWidth, instance, settings.DefaultAnimPreset, runner);
            var commandTable = settings.CommandTable;

            try
            {
                if (commandTable.Common != null && commandTable.Common.Count > 0)
                    await runner.ExecuteListAsync(commandTable.Common, ctx, ct, CommandRunOptions.Default);

                if (TryResolveByType(commandTable.ByType, node.Type, out var typeCommands) && typeCommands != null && typeCommands.Count > 0)
                    await runner.ExecuteListAsync(typeCommands, ctx, ct, CommandRunOptions.Default);

                if (TryResolveByState(commandTable.ByState, node.State, out var stateCommands) && stateCommands != null && stateCommands.Count > 0)
                    await runner.ExecuteListAsync(stateCommands, ctx, ct, CommandRunOptions.Default);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MapNodeVisualizer] Command execution failed: {ex.Message}");
                if (failurePolicy == MapNodeFailurePolicy.FailFast)
                    throw;
            }
        }

        static CommandContext CreateCommandContext(
            MapNode node,
            MapGraph graph,
            int layerWidth,
            MapNodeInstance instance,
            AnimationSpritePreset? preset,
            ICommandRunner runner)
        {
            var vars = new VarStore(initialCapacity: 10);
            TrySetVariant(vars, VarIds.GameLib.MapNode.System.nodeId, DynamicVariant.FromInt(node.Id));
            TrySetVariant(vars, VarIds.GameLib.MapNode.System.nodeType, DynamicVariant.FromInt((int)node.Type));
            TrySetVariant(vars, VarIds.GameLib.MapNode.System.nodeState, DynamicVariant.FromInt((int)node.State));
            TrySetVariant(vars, VarIds.GameLib.MapNode.System.layerIndex, DynamicVariant.FromInt(node.LayerIndex));
            TrySetVariant(vars, VarIds.GameLib.MapNode.System.widthIndex, DynamicVariant.FromInt(node.WidthIndex));
            TrySetVariant(vars, VarIds.GameLib.MapNode.System.layerWidth, DynamicVariant.FromInt(layerWidth));
            TrySetVariant(vars, VarIds.GameLib.MapNode.System.totalDepth, DynamicVariant.FromInt(graph.Depth));
            TrySetVariant(vars, VarIds.GameLib.MapNode.System.totalWidth, DynamicVariant.FromInt(graph.MaxWidth));
            TrySetVariant(vars, VarIds.GameLib.MapNode.System.worldNodePos, DynamicVariant.FromVector3(instance.WorldPos));

            if (preset != null && VarIds.GameLib.MapNode.System.animNodePreset != 0)
                vars.TrySetManagedRef(VarIds.GameLib.MapNode.System.animNodePreset, preset);
            return new CommandContext(instance.Scope, vars, runner, actor: instance.Scope, options: CommandRunOptions.Default);
        }

        static void ApplyUIButtonCommands(MapNode node, MapNodeVisualizeSettingsSO settings, MapNodeInstance instance)
        {
            if (instance.Resolver == null)
                return;

            if (!instance.Resolver.TryResolve<IUIButtonService>(out var buttonService) || buttonService == null)
                return;


            SetNodeInfoToUIElementBlackboard(node, instance.Scope);

            var table = settings.UIButtonCommandTable;

            AppendIfAny(buttonService, table.SubmitUpAppendCommon);
            if (TryResolveByType(table.SubmitUpAppendByType, node.Type, out var typeCommands))
                AppendIfAny(buttonService, typeCommands);
            if (TryResolveByState(table.SubmitUpAppendByState, node.State, out var stateCommands))
                AppendIfAny(buttonService, stateCommands);
        }

        static void SetNodeInfoToUIElementBlackboard(MapNode node, IScopeNode scope)
        {
            if (scope.Resolver == null)
                return;

            if (!scope.Resolver.TryResolve<IBlackboardService>(out var blackboard) || blackboard == null)
                return;

            var vars = blackboard.LocalVars;
            TrySetVariant(vars, VarIds.GameLib.MapNode.System.nodeId, DynamicVariant.FromInt(node.Id));
            TrySetVariant(vars, VarIds.GameLib.MapNode.System.nodeType, DynamicVariant.FromInt((int)node.Type));
            TrySetVariant(vars, VarIds.GameLib.MapNode.System.nodeState, DynamicVariant.FromInt((int)node.State));
            TrySetVariant(vars, VarIds.GameLib.MapNode.System.layerIndex, DynamicVariant.FromInt(node.LayerIndex));
            TrySetVariant(vars, VarIds.GameLib.MapNode.System.widthIndex, DynamicVariant.FromInt(node.WidthIndex));
        }

        static void AppendIfAny(IUIButtonService buttonService, CommandListData? commands)
        {
            if (buttonService == null || commands == null || commands.Count == 0)
                return;

            buttonService.AppendSubmitUpCommands(commands.Commands);
        }

        static bool TryResolveByType(List<MapNodeTypeCommand>? list, MapNodeType type, out CommandListData? commands)
        {
            commands = null;
            if (list == null)
                return false;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Type != type)
                    continue;
                commands = list[i].Commands;
                return true;
            }

            return false;
        }

        static bool TryResolveByState(List<MapNodeStateCommand>? list, MapNodeState state, out CommandListData? commands)
        {
            commands = null;
            if (list == null)
                return false;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].State != state)
                    continue;
                commands = list[i].Commands;
                return true;
            }

            return false;
        }

        static List<MapNodeConnectionInstance> BuildConnections(
            MapGraph graph,
            MapNodeVisualizeSettingsSO settings,
            Dictionary<int, MapNodeInstance> instanceLookup,
            IMeshChannelControlService? lineControlService)
        {
            var connections = new List<MapNodeConnectionInstance>();
            if (graph == null || graph.Nodes == null)
                return connections;

            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                var fromNode = graph.Nodes[i];
                if (fromNode == null)
                    continue;

                if (!instanceLookup.TryGetValue(fromNode.Id, out var fromInstance))
                    continue;

                var style = ResolveLineStyle(settings.LineSettings, fromNode);

                for (int n = 0; n < fromNode.NextIds.Count; n++)
                {
                    var toNodeId = fromNode.NextIds[n];
                    if (!instanceLookup.TryGetValue(toNodeId, out var toInstance))
                        continue;

                    var trackKey = BuildConnectionTrackKey(fromNode.Id, toNodeId);
                    var connection = new MapNodeConnectionInstance(fromNode.Id, toNodeId, trackKey);
                    if (lineControlService != null)
                        TryApplyConnection(settings.LineChannelTag, lineControlService, connection, fromInstance, toInstance, style);
                    connections.Add(connection);
                }
            }

            return connections;
        }

        static void RefreshConnections(
            MapGraph graph,
            MapNodeVisualizeSettingsSO settings,
            Dictionary<int, MapNodeInstance> instanceLookup,
            List<MapNodeConnectionInstance> connections,
            IMeshChannelControlService lineControlService)
        {
            if (graph == null || settings == null || instanceLookup == null || connections == null || lineControlService == null)
                return;

            for (int i = 0; i < connections.Count; i++)
            {
                var connection = connections[i];
                if (connection == null)
                    continue;

                if (!instanceLookup.TryGetValue(connection.FromNodeId, out var fromInstance))
                    continue;
                if (!instanceLookup.TryGetValue(connection.ToNodeId, out var toInstance))
                    continue;
                if (fromInstance == null || toInstance == null)
                    continue;
                if (connection.FromNodeId < 0 || connection.FromNodeId >= graph.Nodes.Count)
                    continue;
                var fromNode = graph.Nodes[connection.FromNodeId];
                if (fromNode == null)
                    continue;

                var style = ResolveLineStyle(settings.LineSettings, fromNode);
                TryApplyConnection(settings.LineChannelTag, lineControlService, connection, fromInstance, toInstance, style);
            }
        }

        static bool TryApplyConnection(
            string channelTag,
            IMeshChannelControlService lineControlService,
            MapNodeConnectionInstance connection,
            MapNodeInstance fromInstance,
            MapNodeInstance toInstance,
            MapNodeLineStyle style)
        {
            if (lineControlService == null || connection == null || fromInstance == null || toInstance == null)
                return false;

            var fromPosition = fromInstance.Root != null ? fromInstance.Root.position : fromInstance.WorldPos;
            var toPosition = toInstance.Root != null ? toInstance.Root.position : toInstance.WorldPos;
            var definition = BuildConnectionTrackDefinition(connection.TrackKey, fromPosition, toPosition, style);
            return lineControlService.SwapTrackDefinition(channelTag, connection.TrackKey, definition);
        }

        static MapNodeLineStyle ResolveLineStyle(MeshLineSettings settings, MapNode node)
        {
            var style = settings.DefaultStyle ?? new MapNodeLineStyle();

            if (settings.ByType != null)
            {
                for (int i = 0; i < settings.ByType.Count; i++)
                {
                    if (settings.ByType[i].Type != node.Type)
                        continue;
                    style = settings.ByType[i].Style;
                    break;
                }
            }

            if (settings.ByState != null)
            {
                for (int i = 0; i < settings.ByState.Count; i++)
                {
                    if (settings.ByState[i].State != node.State)
                        continue;
                    style = settings.ByState[i].Style;
                    break;
                }
            }

            return style;
        }

        static string BuildConnectionTrackKey(int fromNodeId, int toNodeId)
        {
            return $"map.connection.{fromNodeId}.{toNodeId}";
        }

        static MeshTrackDefinition BuildConnectionTrackDefinition(string trackKey, Vector3 fromPosition, Vector3 toPosition, MapNodeLineStyle? style)
        {
            style ??= new MapNodeLineStyle();

            var linePlayer = new MeshLineTrackPlayerPreset
            {
                Points = new List<DynamicValue<Vector3>>
                {
                    DynamicValueExtensions.FromLiteral(fromPosition),
                    DynamicValueExtensions.FromLiteral(toPosition),
                },
                Closed = false,
                SmoothPath = true,
                SmoothingSubdivisions = 8,
            };

            var visualizer = style.Visualizer?.CreateRuntimeCopy() as MeshLineTrackVisualizerPreset ?? new MeshLineTrackVisualizerPreset();
            var material = style.Material?.CreateRuntimeCopy() ?? new MeshTrackMaterialPreset();
            var collider = new MeshPolygonTrackColliderPreset
            {
                SyncPolygonToCollider = false,
                EnableHitCapture = false,
                CaptureEnter = false,
                CaptureStay = false,
                CaptureExit = false,
                Sync = new MeshPolygonSyncSettings
                {
                    UpdateIntervalFrames = 2,
                    MaxPointCount = 64,
                },
            };

            return new MeshTrackDefinition
            {
                Key = trackKey,
                Tag = trackKey,
                Priority = 0,
                Enabled = true,
                Player = MeshChannelDynamicValueFactory.FromManaged<MeshTrackPlayerPresetBase>(linePlayer),
                Visualizer = MeshChannelDynamicValueFactory.FromManaged<MeshTrackVisualizerPresetBase>(visualizer),
                Collider = MeshChannelDynamicValueFactory.FromManaged<MeshTrackColliderPresetBase>(collider),
                Material = MeshChannelDynamicValueFactory.FromManaged(material),
            };
        }

        static void TrySetVariant(IVarStore vars, int varId, DynamicVariant value)
        {
            if (vars == null || varId == 0)
                return;
            vars.TrySetVariant(varId, value);
        }

        static void ExtractSpawnedInfo(
            IObjectResolver? resolver,
            out Transform? root,
            out IScopeNode? scopeNode,
            out RuntimeLifetimeScope? runtimeScope,
            out BaseLifetimeScope? baseScope)
        {
            root = null;
            scopeNode = null;
            runtimeScope = null;
            baseScope = null;

            if (resolver == null)
                return;

            resolver.TryResolve(out runtimeScope);

            if (runtimeScope != null)
                root = runtimeScope.transform;

            if (root == null)
            {
                if (resolver.TryResolve<Transform>(out var tr) && tr != null)
                    root = tr;
                else if (resolver.TryResolve<GameObject>(out var go) && go != null)
                    root = go.transform;
            }

            scopeNode = runtimeScope;
            if (scopeNode == null && resolver.TryResolve<IScopeNode>(out var resolved) && resolved != null)
                scopeNode = resolved;

            if (scopeNode == null && root != null)
            {
                var comps = root.GetComponents<Component>();
                for (int i = 0; i < comps.Length; i++)
                {
                    if (comps[i] is IScopeNode node)
                    {
                        scopeNode = node;
                        break;
                    }
                }
            }

            baseScope = scopeNode as BaseLifetimeScope;
        }
    }
}
