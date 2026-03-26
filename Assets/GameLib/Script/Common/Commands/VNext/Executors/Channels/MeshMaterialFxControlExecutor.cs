#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Channel;
using Game.VariableLayer;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class MeshMaterialFxControlExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.MeshMaterialFxControl;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not MeshMaterialFxControlCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "MeshMaterialFxControlCommandData is required.");

            var service = await ResolveServiceAsync(typed.HubSource, ctx, ct);
            if (service == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "IMeshMaterialFxControlService is missing.");

            switch (typed.Operation)
            {
                case MeshMaterialFxControlOperation.SetEntry:
                    EnsureNode(typed.NodeId);
                    EnsureTag(typed.LayerTag);
                    if (!typed.Value.TryResolve(ctx, out var setValue))
                        throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "MeshMaterialFx value could not be resolved.");
                    if (!service.SetEntry(typed.ChannelTag, typed.CompositeTag, typed.NodeId, typed.LayerTag, setValue, typed.LifetimeSeconds))
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "MeshMaterialFx target could not be resolved.");
                    return;

                case MeshMaterialFxControlOperation.SetEntryFade:
                    EnsureNode(typed.NodeId);
                    EnsureTag(typed.LayerTag);
                    if (!typed.Value.TryResolve(ctx, out var fadeValue))
                        throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "MeshMaterialFx value could not be resolved.");
                    if (!service.SetEntryFade(typed.ChannelTag, typed.CompositeTag, typed.NodeId, typed.LayerTag, fadeValue, typed.DurationSeconds, typed.Ease, typed.LifetimeSeconds))
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "MeshMaterialFx target could not be resolved.");
                    return;

                case MeshMaterialFxControlOperation.RemoveTag:
                    EnsureNode(typed.NodeId);
                    EnsureTag(typed.LayerTag);
                    if (!service.RemoveTag(typed.ChannelTag, typed.CompositeTag, typed.NodeId, typed.LayerTag))
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "MeshMaterialFx layer tag could not be removed.");
                    return;

                case MeshMaterialFxControlOperation.ClearContext:
                    EnsureTag(typed.LayerTag);
                    if (!service.ClearContext(typed.ChannelTag, typed.CompositeTag, typed.LayerTag))
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "MeshMaterialFx context could not be cleared.");
                    return;

                case MeshMaterialFxControlOperation.ClearNode:
                    EnsureNode(typed.NodeId);
                    if (!service.ClearNode(typed.ChannelTag, typed.CompositeTag, typed.NodeId))
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "MeshMaterialFx node could not be cleared.");
                    return;

                case MeshMaterialFxControlOperation.ResetDefaults:
                    if (!service.ResetDefaults(typed.ChannelTag, typed.CompositeTag))
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "MeshMaterialFx defaults could not be reset.");
                    return;
            }
        }

        static async UniTask<IMeshMaterialFxControlService?> ResolveServiceAsync(ActorSource source, CommandContext ctx, CancellationToken ct)
        {
            var (scope, _) = await ActorScopeResolver.ResolveAsync(source, ctx, ct);
            if (scope == null)
                return null;

            for (var current = scope; current != null; current = current.Parent)
            {
                if (current.Resolver != null &&
                    current.Resolver.TryResolve<IMeshMaterialFxControlService>(out var service) &&
                    service != null)
                {
                    return service;
                }
            }

            return null;
        }

        static void EnsureNode(int nodeId)
        {
            if (nodeId <= 0)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "MeshMaterialFx node id is required.");
        }

        static void EnsureTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "MeshMaterialFx layer tag is required.");
        }
    }
}
