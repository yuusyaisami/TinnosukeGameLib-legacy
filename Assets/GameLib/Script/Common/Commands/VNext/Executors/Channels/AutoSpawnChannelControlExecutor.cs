#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Channel;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class AutoSpawnChannelControlExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.AutoSpawnChannelControl;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not AutoSpawnChannelControlCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "AutoSpawnChannelControlCommandData is required.");

            var targetScope = await CommandListChannelHubControlExecutor.ResolveTargetScopeAsync(typed.HubSource, ctx, ct);
            CommandListChannelHubControlExecutor.EnsureScopeBuiltIfNeeded(targetScope);

            if (!TryResolveHub(targetScope, out var hub) || hub == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "IAutoSpawnChannelHubService is missing on target scope.");

            switch (typed.Operation)
            {
                case AutoSpawnChannelControlOperation.MutatePlayerSettings:
                    ExecuteMutation(hub, typed);
                    return;

                case AutoSpawnChannelControlOperation.SpawnExternal:
                    var spawnTask = ExecuteExternalSpawnAsync(hub, typed, ctx, ct);
                    if (typed.AwaitMode == FlowRunAwaitMode.WaitForCompletion)
                    {
                        await spawnTask;
                    }
                    else
                    {
                        spawnTask.Forget(ex =>
                        {
                            if (ex is OperationCanceledException)
                                return;

                            Debug.LogException(ex);
                        });
                    }
                    return;

                case AutoSpawnChannelControlOperation.ClearSpawned:
                    await hub.ClearSpawnedAsync(typed.NormalizedTag, ct);
                    return;

                default:
                    throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Unsupported AutoSpawn channel operation: {typed.Operation}");
            }
        }

        static void ExecuteMutation(IAutoSpawnChannelHubService hub, AutoSpawnChannelControlCommandData typed)
        {
            if (typed.Mutation == null || !typed.Mutation.HasAnyMutation())
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "AutoSpawn mutation is empty.");

            if (!hub.MutateChannel(typed.NormalizedTag, typed.Mutation))
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"AutoSpawn channel '{typed.NormalizedTag}' was not found or mutation had no effect.");
        }

        static async UniTask ExecuteExternalSpawnAsync(
            IAutoSpawnChannelHubService hub,
            AutoSpawnChannelControlCommandData typed,
            CommandContext ctx,
            CancellationToken ct)
        {
            if (!hub.TryGetChannelDef(typed.NormalizedTag, out var channelDef) || channelDef is not AutoSpawnChannelDefinition typedDef)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"AutoSpawn channel '{typed.NormalizedTag}' was not found.");

            if (!typedDef.AllowExternalSpawn)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"AutoSpawn channel '{typed.NormalizedTag}' does not allow external spawn.");

            var count = typed.Count.GetOrDefault(ctx, 1);
            if (count <= 0)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "AutoSpawn external spawn count must be greater than zero.");

            var intervalSeconds = typed.IntervalSeconds.GetOrDefault(ctx, 0f);
            if (intervalSeconds < 0f)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "AutoSpawn external spawn interval must be zero or greater.");

            var spawnedCount = await hub.SpawnExternallyAsync(typed.NormalizedTag, count, intervalSeconds, ct);
            if (spawnedCount <= 0)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"AutoSpawn channel '{typed.NormalizedTag}' could not spawn runtime instances.");
        }

        static bool TryResolveHub(IScopeNode scope, out IAutoSpawnChannelHubService? hub)
        {
            hub = null;

            for (var current = scope; current != null; current = current.Parent)
            {
                var resolver = current.Resolver;
                if (resolver != null && resolver.TryResolve<IAutoSpawnChannelHubService>(out var resolved) && resolved != null)
                {
                    hub = resolved;
                    return true;
                }
            }

            return false;
        }
    }
}
