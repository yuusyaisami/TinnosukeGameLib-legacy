#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Channel;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class MeshChannelControlExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.MeshChannelControl;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not MeshChannelControlCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "MeshChannelControlCommandData is required.");

            var service = await ResolveServiceAsync(typed.HubSource, ctx, ct);
            if (service == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "IMeshChannelControlService is missing.");

            switch (typed.Operation)
            {
                case MeshChannelControlOperation.SwapRootDefinition:
                    if (!typed.RootDefinition.TryGet(ctx, out MeshDefinitionPreset? rootPreset) || rootPreset == null)
                        throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "MeshDefinitionPreset could not be resolved.");
                    if (!service.SwapRootDefinition(typed.Tag, rootPreset))
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Mesh channel '{typed.Tag}' was not found.");
                    return;

                case MeshChannelControlOperation.SwapTrackDefinition:
                    EnsureTrackKey(typed.TrackKey);
                    if (!service.SwapTrackDefinition(typed.Tag, typed.TrackKey, typed.TrackDefinition))
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Mesh channel '{typed.Tag}' was not found.");
                    return;

                case MeshChannelControlOperation.MutateTrackVisualizer:
                    EnsureTrackKey(typed.TrackKey);
                    if (!typed.VisualizerMutation.HasAnyMutation())
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "Visualizer mutation is empty.");
                    if (!service.MutateTrackVisualizer(typed.Tag, typed.TrackKey, typed.VisualizerMutation))
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Mesh track '{typed.TrackKey}' was not found.");
                    return;

                case MeshChannelControlOperation.MutateTrackPlayer:
                    EnsureTrackKey(typed.TrackKey);
                    if (!typed.PlayerMutation.HasAnyMutation())
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "Player mutation is empty.");
                    if (!service.MutateTrackPlayer(typed.Tag, typed.TrackKey, typed.PlayerMutation))
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Mesh track '{typed.TrackKey}' was not found.");
                    return;

                case MeshChannelControlOperation.MutateTrackCollider:
                    EnsureTrackKey(typed.TrackKey);
                    if (!typed.ColliderMutation.HasAnyMutation())
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "Collider mutation is empty.");
                    if (!service.MutateTrackCollider(typed.Tag, typed.TrackKey, typed.ColliderMutation))
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Mesh track '{typed.TrackKey}' was not found.");
                    return;

                case MeshChannelControlOperation.MutateTrackMaterial:
                    EnsureTrackKey(typed.TrackKey);
                    if (!typed.MaterialMutation.HasAnyMutation())
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "Material mutation is empty.");
                    if (!service.MutateTrackMaterial(typed.Tag, typed.TrackKey, typed.MaterialMutation))
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Mesh track '{typed.TrackKey}' was not found.");
                    return;

                case MeshChannelControlOperation.MutateSimulationTrack:
                    EnsureTrackKey(typed.TrackKey);
                    if (!typed.SimulationMutation.HasAnyMutation())
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "Simulation mutation is empty.");
                    if (!service.MutateSimulationTrack(typed.Tag, typed.TrackKey, typed.SimulationMutation))
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Mesh simulation track '{typed.TrackKey}' was not found.");
                    return;

                case MeshChannelControlOperation.ResetRuntimeOverrides:
                    if (!service.ResetRuntimeOverrides(
                            typed.Tag,
                            typed.ResetVisualizer,
                            typed.ResetPlayer,
                            typed.ResetCollider,
                            typed.ResetMaterial,
                            typed.ResetSimulation))
                    {
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Mesh channel '{typed.Tag}' was not found.");
                    }
                    return;

                case MeshChannelControlOperation.SetTrackEnabled:
                    EnsureTrackKey(typed.TrackKey);
                    if (!service.SetTrackEnabled(typed.Tag, typed.TrackKey, typed.Enabled))
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Mesh track '{typed.TrackKey}' was not found.");
                    return;
            }
        }

        static async UniTask<IMeshChannelControlService?> ResolveServiceAsync(ActorSource source, CommandContext ctx, CancellationToken ct)
        {
            var (scope, _) = await ActorScopeResolver.ResolveAsync(source, ctx, ct);
            if (scope == null)
                return null;

            for (var current = scope; current != null; current = current.Parent)
            {
                if (current.Resolver != null &&
                    current.Resolver.TryResolve<IMeshChannelControlService>(out var service) &&
                    service != null)
                {
                    return service;
                }
            }

            return null;
        }

        static void EnsureTrackKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "Track key is required.");
        }
    }
}
