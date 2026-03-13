#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Channel;
using Game.Common;
using Game.TransformSystem;
using VContainer;
namespace Game.Commands.VNext
{
    public sealed class TransformAnimationChannelExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.TransformAnimation;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not TransformAnimationChannelCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "TransformAnimationChannelCommandData is required.");

            if (!TryResolveHub(ctx, typed.ChannelTag, out var hub) || hub == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "ITransformAnimationHubService is missing.");

            var enableDebugLog = hub.EnableDebugLog;
            void LogDebug(string message)
            {
                if (!enableDebugLog)
                    return;
                UnityEngine.Debug.Log($"[TransformAnimationChannelExecutor] {message}");
            }

            LogDebug($"Received command. {typed.DebugData}, CtCanBeCanceled={ct.CanBeCanceled}, IsCancellationRequested={ct.IsCancellationRequested}");

            if (!hub.TryGetPlayer(typed.ChannelTag, out var player) || player == null)
            {
                LogDebug($"Player not found. Tag={typed.ChannelTag}");
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"TransformAnimation channel '{typed.ChannelTag}' not found.");
            }

            if (typed.Mode == TransformAnimationCommandMode.Follow)
            {
                StopConflictingPlayers(hub, player, LogDebug);
            }

            if (typed.Mode == TransformAnimationCommandMode.Stop)
            {
                LogDebug($"Mode=Stop. Tag={typed.ChannelTag}");
                player.Stop();
                return UniTask.CompletedTask;
            }

            if (typed.Mode == TransformAnimationCommandMode.Shake)
            {
                LogDebug($"Mode=Shake. Action={typed.ShakeAction}, Tag={typed.ChannelTag}");
                if (typed.ShakeAction == TransformAnimationShakeAction.Play)
                    player.PlayShake(typed.ShakeSettings);
                else
                    player.StopShake();

                return UniTask.CompletedTask;
            }

            if (typed.Mode == TransformAnimationCommandMode.Follow)
            {
                if (typed.FollowAction == TransformFollowCommandAction.SnapToCurrentFollowTarget)
                {
                    LogDebug($"Mode=Follow. Action=SnapToCurrentFollowTarget, Tag={typed.ChannelTag}");
                    _ = player.TrySnapToCurrentFollowTarget();
                    return UniTask.CompletedTask;
                }

                var options = typed.FollowOptions;
                var hasTransformTarget = typed.FollowTargetKind == TransformFollowTargetKind.Transform;
                LogDebug($"Mode=Follow. TargetKind={typed.FollowTargetKind}, AwaitMode={typed.FollowAwaitMode}, SmoothTime={options.SmoothTime}, FollowX={options.FollowX}, FollowY={options.FollowY}, MaxSpeed={options.MaxSpeed}");

                if (hasTransformTarget)
                {
                    if (!typed.FollowTargetTransform.HasSource)
                    {
                        LogDebug("Follow skipped: FollowTargetTransform has no source");
                        return UniTask.CompletedTask;
                    }
                    if (!typed.FollowTargetTransform.TryGet(ctx, out var targetTransform) || targetTransform == null)
                    {
                        LogDebug("Follow skipped: FollowTargetTransform.TryGet failed or result null");
                        return UniTask.CompletedTask;
                    }

                    LogDebug($"Follow start with transform target. Name={targetTransform.name}");

                    var followTask = player.PlayFollowAsync(targetTransform, options, ct);
                    if (typed.FollowAwaitMode == FlowRunAwaitMode.WaitForCompletion)
                    {
                        LogDebug("Follow awaiting completion");
                        return followTask;
                    }

                    followTask.Forget(ex =>
                    {
                        if (ex is OperationCanceledException)
                        {
                            LogDebug("Follow canceled");
                            return;
                        }
                        if (ex != null)
                        {
                            LogDebug($"Follow exception: {ex.GetType().Name}: {ex.Message}");
                            UnityEngine.Debug.LogException(ex);
                        }
                    });
                    LogDebug("Follow started in background");
                    return UniTask.CompletedTask;
                }

                if (!typed.FollowTargetPosition.HasSource)
                {
                    LogDebug("Follow skipped: FollowTargetPosition has no source");
                    return UniTask.CompletedTask;
                }
                if (!typed.FollowTargetPosition.TryGet(ctx, out var targetPosition))
                {
                    LogDebug("Follow skipped: FollowTargetPosition.TryGet failed");
                    return UniTask.CompletedTask;
                }

                LogDebug($"Follow start with position target. Position={targetPosition}");

                var positionFollowTask = player.PlayFollowAsync(targetPosition, options, ct);
                if (typed.FollowAwaitMode == FlowRunAwaitMode.WaitForCompletion)
                {
                    LogDebug("Follow awaiting completion");
                    return positionFollowTask;
                }

                positionFollowTask.Forget(ex =>
                {
                    if (ex is OperationCanceledException)
                    {
                        LogDebug("Follow canceled");
                        return;
                    }
                    if (ex != null)
                    {
                        LogDebug($"Follow exception: {ex.GetType().Name}: {ex.Message}");
                        UnityEngine.Debug.LogException(ex);
                    }
                });
                LogDebug("Follow started in background");
                return UniTask.CompletedTask;
            }

            TransformAnimationPreset? preset = null;

            // [RoomMap Support] Override preset from vars if available (VarId 54 = animPreset)
            // This allows RoomMapVisualizer to pass a specific preset to a generic command.
            if (ctx.Vars != null && ctx.Vars.TryGetManagedRef(Game.Vars.Generated.VarIds.GameLib.RoomMap.animPreset, out var objArg) && objArg is Game.Channel.TransformAnimationPreset overridePreset)
            {
                preset = overridePreset;
                LogDebug("Preset overridden from vars (RoomMap.animPreset)");
            }
            else if (typed.Preset.HasSource && typed.Preset.TryGet(ctx, out var resolvedPreset) && resolvedPreset != null)
            {
                preset = resolvedPreset;
            }

            if (preset == null || preset.Steps == null || preset.Steps.Count == 0)
            {
                LogDebug($"Preset skipped: empty or null. Tag={typed.ChannelTag}, HasPreset={preset != null}, HasSteps={preset?.Steps != null}, StepCount={preset?.Steps?.Count ?? 0}");
                return UniTask.CompletedTask;
            }

            if (typed.PresetExecutionPolicy != TransformPresetExecutionPolicy.Parallel)
            {
                StopConflictingPlayers(hub, player, LogDebug);
            }

            LogDebug($"Mode=Preset. StepCount={preset.Steps.Count}, WaitForCompletion={typed.WaitForCompletion}, Policy={typed.PresetExecutionPolicy}");

            var variables = ctx.Vars ?? NullVarStore.Instance;
            var task = player.PlayPresetAsync(preset, variables, typed.PresetExecutionPolicy, ct);
            if (typed.WaitForCompletion)
            {
                LogDebug("Preset awaiting completion");
                return task;
            }

            task.Forget(ex =>
            {
                if (ex is OperationCanceledException)
                {
                    LogDebug("Preset canceled");
                    return;
                }
                if (ex != null)
                {
                    LogDebug($"Preset exception: {ex.GetType().Name}: {ex.Message}");
                    UnityEngine.Debug.LogException(ex);
                }
            });
            LogDebug("Preset started in background");
            return UniTask.CompletedTask;
    }

        static bool TryResolveHub(CommandContext ctx, string channelTag, out ITransformAnimationHubService? hub)
        {
            hub = null;
            var origin = ctx.Actor ?? ctx.Scope;
            if (TryResolveHubWithPlayer(origin, channelTag, out hub))
                return true;

            if (ctx.CommandRootScope != null &&
                !ReferenceEquals(ctx.CommandRootScope, origin) &&
                TryResolveHubWithPlayer(ctx.CommandRootScope, channelTag, out hub))
            {
                return true;
            }

            if (ctx.Resolver != null &&
                ctx.Resolver.TryResolve<ITransformAnimationHubService>(out var directWithPlayer) &&
                directWithPlayer != null &&
                directWithPlayer.TryGetPlayer(channelTag, out _))
            {
                hub = directWithPlayer;
                return true;
            }

            if (TryResolveAnyHub(origin, out hub))
                return true;

            if (ctx.CommandRootScope != null &&
                !ReferenceEquals(ctx.CommandRootScope, origin) &&
                TryResolveAnyHub(ctx.CommandRootScope, out hub))
            {
                return true;
            }

            if (ctx.Resolver != null &&
                ctx.Resolver.TryResolve<ITransformAnimationHubService>(out var direct) &&
                direct != null)
            {
                hub = direct;
                return true;
            }

            return false;
        }

        static void StopConflictingPlayers(
            ITransformAnimationHubService hub,
            ITransformAnimationChannelPlayer currentPlayer,
            Action<string> logDebug)
        {
            var currentTarget = currentPlayer.TargetTransform;
            if (currentTarget == null)
                return;

            var players = hub.Players;
            for (var i = 0; i < players.Count; i++)
            {
                var other = players[i];
                if (other == null || ReferenceEquals(other, currentPlayer))
                    continue;

                if (!ReferenceEquals(other.TargetTransform, currentTarget))
                    continue;

                other.Stop();
                other.StopShake();
                logDebug($"Stopped conflicting player. CurrentTag={currentPlayer.Tag}, StoppedTag={other.Tag}, Target={currentTarget.name}");
            }
        }

        static bool TryResolveHubWithPlayer(IScopeNode? origin, string channelTag, out ITransformAnimationHubService? hub)
        {
            hub = null;
            if (origin == null)
                return false;

            foreach (var node in ScopeNodeHierarchy.EnumerateSubtree(origin, includeSelf: true))
            {
                var resolver = node?.Resolver;
                if (resolver == null)
                    continue;

                if (!resolver.TryResolve<ITransformAnimationHubService>(out var candidate) || candidate == null)
                    continue;

                if (candidate.TryGetPlayer(channelTag, out _))
                {
                    hub = candidate;
                    return true;
                }
            }

            foreach (var node in origin.EnumerateAncestors(includeSelf: true))
            {
                var resolver = node?.Resolver;
                if (resolver == null)
                    continue;

                if (!resolver.TryResolve<ITransformAnimationHubService>(out var candidate) || candidate == null)
                    continue;

                if (candidate.TryGetPlayer(channelTag, out _))
                {
                    hub = candidate;
                    return true;
                }
            }

            return false;
        }

        static bool TryResolveAnyHub(IScopeNode? origin, out ITransformAnimationHubService? hub)
        {
            hub = null;
            if (origin == null)
                return false;

            foreach (var node in ScopeNodeHierarchy.EnumerateSubtree(origin, includeSelf: true))
            {
                var resolver = node?.Resolver;
                if (resolver == null)
                    continue;

                if (resolver.TryResolve<ITransformAnimationHubService>(out var candidate) && candidate != null)
                {
                    hub = candidate;
                    return true;
                }
            }

            foreach (var node in origin.EnumerateAncestors(includeSelf: true))
            {
                var resolver = node?.Resolver;
                if (resolver == null)
                    continue;

                if (resolver.TryResolve<ITransformAnimationHubService>(out var candidate) && candidate != null)
                {
                    hub = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}
