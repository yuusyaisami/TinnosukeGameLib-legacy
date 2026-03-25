#nullable enable
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using Game.Search;
using Game.Targeting;
using Unity.Mathematics;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class TargetChannelControlExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.TargetChannelControl;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not TargetChannelControlCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "TargetChannelControlCommandData is required.");

            var hub = await ResolveHubAsync(typed.HubSource, ctx, ct);
            if (hub == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "ITargetChannelHub is missing.");

            switch (typed.Operation)
            {
                case TargetChannelControlOperation.RegisterOrReplace:
                    await ExecuteRegisterAsync(hub, typed, ctx, ct);
                    return;

                case TargetChannelControlOperation.Unregister:
                    EnsureTag(typed.Tag);
                    if (!hub.Unregister(typed.Tag))
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Target channel '{typed.Tag}' not found.");
                    return;

                case TargetChannelControlOperation.SwapPreset:
                    EnsureTag(typed.Tag);
                    if (!typed.Preset.TryGet(ctx, out TargetChannelPreset? swapPreset) || swapPreset == null)
                        throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "TargetChannel preset could not be resolved.");
                    if (!hub.SwapPreset(typed.Tag, swapPreset))
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Target channel '{typed.Tag}' not found or preset tag mismatch.");
                    return;

                case TargetChannelControlOperation.MutateSettings:
                    EnsureTag(typed.Tag);
                    if (typed.Mutation == null || !typed.Mutation.HasAnyMutation())
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "TargetChannel mutation is empty.");
                    if (!hub.MutateSettings(typed.Tag, typed.Mutation))
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Target channel '{typed.Tag}' not found.");
                    return;

                case TargetChannelControlOperation.ResetRuntimeOverrides:
                    EnsureTag(typed.Tag);
                    if (!hub.ResetRuntimeOverrides(typed.Tag))
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Target channel '{typed.Tag}' not found.");
                    return;

                case TargetChannelControlOperation.SetDirectTargets:
                    EnsureTag(typed.Tag);
                    var hits = await ResolveDirectTargetsAsync(typed.DirectTargets, ctx, ct);
                    if (!hub.SetDirectTargets(typed.Tag, hits))
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Target channel '{typed.Tag}' not found or does not support direct targets.");
                    return;

                case TargetChannelControlOperation.ClearDirectTargets:
                    EnsureTag(typed.Tag);
                    if (!hub.ClearDirectTargets(typed.Tag))
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Target channel '{typed.Tag}' not found or does not support direct targets.");
                    return;
            }
        }

        static async UniTask ExecuteRegisterAsync(ITargetChannelHub hub, TargetChannelControlCommandData typed, CommandContext ctx, CancellationToken ct)
        {
            _ = ct;
            if (!typed.Preset.TryGet(ctx, out TargetChannelPreset? preset) || preset == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "TargetChannel preset could not be resolved.");

            hub.RegisterOrReplace(preset);
            await UniTask.CompletedTask;
        }

        static async UniTask<ITargetChannelHub?> ResolveHubAsync(ActorSource source, CommandContext ctx, CancellationToken ct)
        {
            var (scope, _) = await ActorScopeResolver.ResolveAsync(source, ctx, ct);
            if (scope == null)
                return null;

            for (var current = scope; current != null; current = current.Parent)
            {
                if (TargetChannelTargetPositionSourceHelper.TryResolveHubAtScope(current, out var hub) && hub != null)
                    return hub;
            }

            return null;
        }

        static async UniTask<List<DynamicSearchHit>> ResolveDirectTargetsAsync(List<ActorSource>? sources, CommandContext ctx, CancellationToken ct)
        {
            var hits = new List<DynamicSearchHit>(sources?.Count ?? 0);
            if (sources == null || sources.Count == 0)
                return hits;

            for (int i = 0; i < sources.Count; i++)
            {
                var (scope, error) = await ActorScopeResolver.ResolveAsync(sources[i], ctx, ct);
                if (scope == null)
                    throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, error ?? $"Direct target[{i}] could not be resolved.");

                if (!TryCreateHit(scope, out var hit))
                    throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"Direct target[{i}] does not have a valid transform.");

                hits.Add(hit);
            }

            return hits;
        }

        static bool TryCreateHit(IScopeNode scope, out DynamicSearchHit hit)
        {
            hit = default;
            var identity = scope.Identity;
            if (identity == null)
                return false;

            Transform? transform = identity.SelfTransform;
            if (transform == null && scope is Component component)
                transform = component.transform;

            if (transform == null)
                return false;

            var pos = transform.position;
            hit = new DynamicSearchHit(scope, identity, 0f, new float2(pos.x, pos.y));
            return true;
        }

        static void EnsureTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "TargetChannel tag is required.");
        }
    }
}
