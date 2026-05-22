#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Channel;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class AreaChannelControlExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.AreaChannelControl;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not AreaChannelControlCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "AreaChannelControlCommandData is required.");

            var hub = await ResolveHubAsync(typed.HubSource, ctx, ct);
            if (hub == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "IAreaChannelHubService is missing.");

            switch (typed.Operation)
            {
                case AreaChannelControlOperation.RegisterOrReplace:
                    ExecuteRegisterOrReplace(hub, typed, ctx);
                    return;

                case AreaChannelControlOperation.Unregister:
                    EnsureTag(typed.NormalizedTag);
                    if (!hub.UnregisterChannel(typed.NormalizedTag))
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Area channel '{typed.NormalizedTag}' was not found.");
                    return;

                case AreaChannelControlOperation.MutateSettings:
                    EnsureTag(typed.NormalizedTag);
                    EnsureMutation(typed.Mutation);
                    if (!hub.MutateChannel(typed.NormalizedTag, typed.Mutation))
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Area channel '{typed.NormalizedTag}' was not found or mutation had no effect.");
                    return;

                case AreaChannelControlOperation.MutateSettingsByTags:
                    EnsureMutation(typed.Mutation);
                    ExecuteMutateByTags(hub, typed.Tags, typed.Mutation);
                    return;
            }
        }

        static void ExecuteRegisterOrReplace(IAreaChannelHubService hub, AreaChannelControlCommandData typed, CommandContext ctx)
        {
            if (!typed.Definition.TryGet(ctx, out AreaChannelDefinition? definition) || definition == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "AreaChannel definition could not be resolved.");

            if (!hub.RegisterChannel(definition, typed.Overwrite))
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Area channel '{definition.Tag}' registration failed. (overwrite={typed.Overwrite})");
        }

        static void ExecuteMutateByTags(IAreaChannelHubService hub, List<string>? tags, AreaChannelRuntimeMutation mutation)
        {
            if (tags == null || tags.Count == 0)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "At least one tag is required for MutateSettingsByTags.");

            var unique = new HashSet<string>(StringComparer.Ordinal);
            var changedCount = 0;
            for (var i = 0; i < tags.Count; i++)
            {
                var normalized = NormalizeTag(tags[i]);
                if (string.IsNullOrEmpty(normalized) || !unique.Add(normalized))
                    continue;

                if (hub.MutateChannel(normalized, mutation))
                    changedCount++;
            }

            if (changedCount <= 0)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "No area channels were mutated. Check tag names and mutation content.");
        }

        static async UniTask<IAreaChannelHubService?> ResolveHubAsync(ActorSource source, CommandContext ctx, CancellationToken ct)
        {
            var (scope, _) = await ActorScopeResolver.ResolveAsync(source, ctx, ct);
            if (scope == null)
                return null;

            EnsureScopeBuiltIfNeeded(scope);

            for (var current = scope; current != null; current = current.Parent)
            {
                var resolver = current.Resolver;
                if (resolver != null && resolver.TryResolve<IAreaChannelHubService>(out var hub) && hub != null)
                    return hub;
            }

            return null;
        }

        static void EnsureScopeBuiltIfNeeded(IScopeNode scope)
        {
            ScopeFeatureInstallerUtility.EnsureScopeBuiltIfNeeded(scope);
        }

        static void EnsureMutation(AreaChannelRuntimeMutation mutation)
        {
            if (mutation == null || !mutation.HasAnyMutation())
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "Area channel mutation is empty.");
        }

        static void EnsureTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "Area channel tag is required.");
        }

        static string NormalizeTag(string? tag)
        {
            return string.IsNullOrWhiteSpace(tag) ? "default" : tag.Trim();
        }
    }
}
