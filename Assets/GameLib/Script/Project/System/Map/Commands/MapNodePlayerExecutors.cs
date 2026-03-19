#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using Game.Common;
using Game.MapNode;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class BuildMapNodeExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.BuildMapNode;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not BuildMapNodeCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "BuildMapNodeCommandData is required.");

            if (!typed.ProfileSource.TryResolve(ctx.Vars, out var profile) || profile == null)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "Profile is null (and could not be resolved from vars).");

            var svc = MapNodePlayerCommandExecutorUtility.ResolvePlayerServiceOrThrow(ctx, out var svcScope, out var options);
            var (buildScope, error) = await ActorScopeResolver.ResolveAsync(typed.BuildScope, ctx, ct);
            if (buildScope == null)
            {
                var reason = string.IsNullOrEmpty(error) ? "BuildScope could not be resolved." : error;
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, reason);
            }

            MapNodePlayerCommandExecutorUtility.EnsureScopeBuiltIfNeeded(buildScope);
            Transform? parent = (buildScope as Component)?.transform;
            if (parent == null && options != null)
                parent = options.DefaultParentTransform;
            if (parent == null && svcScope is Component svcComponent)
                parent = svcComponent.transform;
            if (parent == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "Parent transform is null.");

            try
            {
                await svc.BuildAsync(profile, parent, buildScope, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                throw;
            }
        }
    }

    public sealed class MoveMapNodeExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.MoveMapNode;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not MoveMapNodeCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "MoveMapNodeCommandData is required.");

            var (targetScope, targetError) = await ActorScopeResolver.ResolveAsync(typed.TargetNodeSource, ctx, ct);
            if (targetScope == null)
            {
                var reason = string.IsNullOrEmpty(targetError) ? "TargetNodeSource could not be resolved." : targetError;
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, reason);
            }

            if (targetScope.Resolver == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "TargetNodeSource scope has no resolver.");

            if (!targetScope.Resolver.TryResolve<IBlackboardService>(out var blackboard) || blackboard == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "TargetNodeSource has no blackboard.");

            var nodeIdContext = new SimpleDynamicContext(blackboard.LocalVars, targetScope);
            if (!typed.TargetNodeId.TryGet(nodeIdContext, out int nodeId))
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "TargetNodeId could not be resolved.");

            var (executeScope, executeError) = await ActorScopeResolver.ResolveAsync(typed.ExecuteScope, ctx, ct);
            if (executeScope == null)
            {
                var reason = string.IsNullOrEmpty(executeError) ? "ExecuteScope could not be resolved." : executeError;
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, reason);
            }

            var executeCtx = new CommandContext(executeScope, ctx.Vars, ctx.Runner, actor: executeScope, options: ctx.Options, commandRootScope: ctx.CommandRootScope, rootActor: ctx.RootActor, callerActor: ctx.Actor, sourceContext: ctx);
            var svc = MapNodePlayerCommandExecutorUtility.ResolvePlayerServiceOrThrow(executeCtx, out _, out _);
            var result = svc.TryMoveToNode(nodeId, typed.MoveOptions);
            if (result != MapNodeMoveResult.Ok)
                throw MapNodePlayerCommandExecutorUtility.BuildMoveException(result, nodeId);

            return;
        }
    }

    public sealed class RunMapNodeCommandsExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.RunMapNodeCommands;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not RunMapNodeCommandsCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "RunMapNodeCommandsCommandData is required.");

            if (typed.Commands == null || typed.Commands.Count == 0)
                return;

            var svc = MapNodePlayerCommandExecutorUtility.ResolvePlayerServiceOrThrow(ctx, out _, out _);
            var target = typed.Target;
            if (!typed.ExecuteForAllTargets)
            {
                var targets = MapNodePlayerCommandExecutorUtility.ResolveTargetNodeIds(typed.Target, svc);
                if (targets.Count == 0)
                    throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "Target resolved to no nodes.");

                target = new MapNodeTarget
                {
                    Kind = MapNodeTargetKind.ByIdList,
                    SingleNodeId = targets[0],
                    NodeIds = new[] { targets[0] }
                };
            }

            var result = await svc.ExecuteCommandListAtTargetAsync(target, typed.Commands, ctx.Options, ct);
            if (result.Status == CommandRunStatus.Canceled)
                throw new OperationCanceledException();
            if (result.Status == CommandRunStatus.Error || result.FailureCount > 0)
            {
                var msg = $"MapNode command list failed. FailureCount={result.FailureCount} ErrorIndex={result.ErrorIndex} Message={result.Message}";
                throw new CommandExecutionException(result.FailureKind, msg);
            }
        }
    }

    public sealed class RefreshMapNodeStateExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.RefreshMapNodeState;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not RefreshMapNodeStateCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "RefreshMapNodeStateCommandData is required.");

            var svc = MapNodePlayerCommandExecutorUtility.ResolvePlayerServiceOrThrow(ctx, out _, out var options);
            var currentId = svc.Snapshot.CurrentNodeId;
            if (currentId < 0)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "CurrentNode is not set.");

            var moveResult = svc.TryMoveToNode(currentId, typed.MoveOptions);
            if (moveResult != MapNodeMoveResult.Ok)
                throw MapNodePlayerCommandExecutorUtility.BuildMoveException(moveResult, currentId);

            if (!typed.WriteState)
                return;

            var saveOptions = options != null
                ? options.DefaultSaveOptions
                : new MapNodePlayerSaveOptions
                {
                    Target = MapNodePlayerSaveTarget.Blackboard,
                    WriteCurrentNode = true,
                    WriteNodeLists = true,
                };

            var wrote = await svc.WriteStateAsync(saveOptions, ct);
            if (!wrote)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "WriteState failed.");
        }
    }

    public sealed class WriteMapNodePlayerStateExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.WriteMapNodePlayerState;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not WriteMapNodePlayerStateCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "WriteMapNodePlayerStateCommandData is required.");

            var svc = MapNodePlayerCommandExecutorUtility.ResolvePlayerServiceOrThrow(ctx, out _, out _);
            var wrote = await svc.WriteStateAsync(typed.SaveOptions, ct);
            if (!wrote)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "WriteState failed.");
        }
    }

    static class MapNodePlayerCommandExecutorUtility
    {
        public static IMapNodePlayerService ResolvePlayerServiceOrThrow(
            CommandContext ctx,
            out IScopeNode ownerScope,
            out IMapNodePlayerOptions? options)
        {
            if (ctx == null)
                throw new CommandExecutionException(CommandRunFailureKind.Exception, "CommandContext is null.");

            var origin = ctx.Scope;
            if (origin == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "Scope is null.");

            var candidates = new List<LifetimeScopeKind>
            {
                LifetimeScopeKind.Scene,
                LifetimeScopeKind.Field,
                LifetimeScopeKind.Project
            };

            foreach (var kind in candidates)
            {
                var node = ScopeNodeHierarchy.FindNearestAncestorByKind(origin, kind, includeSelf: true);
                if (node == null)
                    continue;
                var resolver = node.Resolver;
                if (resolver == null)
                    continue;
                if (resolver.TryResolve<IMapNodePlayerService>(out var svc) && svc != null)
                {
                    ownerScope = node;
                    options = resolver.TryResolve<IMapNodePlayerOptions>(out var resolved) ? resolved : null;
                    return svc;
                }
            }

            var originResolver = origin.Resolver;
            if (originResolver != null && originResolver.TryResolve<IMapNodePlayerService>(out var originSvc) && originSvc != null)
            {
                ownerScope = origin;
                options = originResolver.TryResolve<IMapNodePlayerOptions>(out var resolved) ? resolved : null;
                return originSvc;
            }

            throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "IMapNodePlayerService is not registered in the nearest Scene/Field/Project scope. Add MapNodePlayerMB to the appropriate scope.");
        }

        public static List<int> ResolveTargetNodeIds(MapNodeTarget target, IMapNodePlayerService svc)
        {
            var results = new List<int>();
            var seen = new HashSet<int>();

            void AddId(int nodeId)
            {
                if (nodeId < 0)
                    return;
                if (seen.Add(nodeId))
                    results.Add(nodeId);
            }

            switch (target.Kind)
            {
                case MapNodeTargetKind.Current:
                    AddId(svc.Snapshot.CurrentNodeId);
                    break;
                case MapNodeTargetKind.NextAvailable:
                    AddNodes(svc.GetNextNodes());
                    break;
                case MapNodeTargetKind.NotNext:
                    AddNodes(svc.GetNotNextNodes());
                    break;
                case MapNodeTargetKind.ByIdList:
                    if (target.NodeIds != null)
                    {
                        for (int i = 0; i < target.NodeIds.Length; i++)
                            AddId(target.NodeIds[i]);
                    }
                    else
                    {
                        AddId(target.SingleNodeId);
                    }
                    break;
                case MapNodeTargetKind.ByState:
                    AddNodes(svc.GetNodesByState(target.State));
                    break;
                case MapNodeTargetKind.ByType:
                    AddNodes(svc.GetNodesByType(target.Type));
                    break;
                case MapNodeTargetKind.ByLayerIndex:
                    AddNodes(svc.GetNodesByLayerIndex(target.LayerIndex));
                    break;
            }

            return results;

            void AddNodes(IReadOnlyList<Game.MapNode.MapNode> nodes)
            {
                if (nodes == null)
                    return;
                for (int i = 0; i < nodes.Count; i++)
                {
                    var node = nodes[i];
                    if (node != null)
                        AddId(node.Id);
                }
            }
        }

        public static CommandExecutionException BuildMoveException(MapNodeMoveResult result, int nodeId)
        {
            var failureKind = result switch
            {
                MapNodeMoveResult.NotBuilt => CommandRunFailureKind.ResolveFailed,
                MapNodeMoveResult.InvalidNodeId => CommandRunFailureKind.InvalidArgs,
                MapNodeMoveResult.NoCurrentNode => CommandRunFailureKind.ResolveFailed,
                _ => CommandRunFailureKind.InvalidArgs,
            };

            var message = $"Move failed: result={result} nodeId={nodeId}";
            return new CommandExecutionException(failureKind, message);
        }

        public static void EnsureScopeBuiltIfNeeded(IScopeNode scope)
        {
            if (scope is BaseLifetimeScope baseScope)
            {
                baseScope.EnsureScopeBuilt();
                return;
            }

            if (scope is RuntimeLifetimeScope runtimeScope)
            {
                runtimeScope.EnsureScopeBuilt();
                return;
            }
        }
    }
}
