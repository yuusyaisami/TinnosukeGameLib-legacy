#nullable enable
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Channel;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class ParallaxChannelExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.ParallaxChannel;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not ParallaxChannelCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "ParallaxChannelCommandData is required.");

            if (!ctx.Resolver.TryResolve<IParallaxChannelHubService>(out var hub) || hub == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "IParallaxChannelHubService is missing.");

            if (typed.ApplyToAllChannels)
            {
                ApplyToPlayers(hub.Players, typed, ctx);
                return UniTask.CompletedTask;
            }

            if (!hub.TryGetPlayer(typed.ChannelTag, out var player) || player == null)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Parallax channel '{typed.ChannelTag}' not found.");

            Apply(player, typed, ctx);
            return UniTask.CompletedTask;
        }

        static void ApplyToPlayers(IReadOnlyList<IParallaxChannelPlayer> players, ParallaxChannelCommandData typed, CommandContext ctx)
        {
            if (players == null)
                return;

            for (int i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player == null)
                    continue;

                Apply(player, typed, ctx);
            }
        }

        static void Apply(IParallaxChannelPlayer player, ParallaxChannelCommandData typed, CommandContext ctx)
        {
            switch (typed.Operation)
            {
                case ParallaxChannelOperation.SetEnabled:
                    player.SetEnabled(typed.Enabled.GetOrDefault(ctx, true));
                    break;

                case ParallaxChannelOperation.ToggleEnabled:
                    player.ToggleEnabled();
                    break;

                case ParallaxChannelOperation.SetWriteMode:
                    player.SetWriteMode(typed.WriteMode);
                    break;

                case ParallaxChannelOperation.SetFactor:
                    player.SetFactor(typed.Factor.GetOrDefault(ctx, Vector3.one));
                    break;

                case ParallaxChannelOperation.SetExtraOffset:
                    player.SetExtraOffset(typed.ExtraOffset.GetOrDefault(ctx, Vector3.zero));
                    break;

                case ParallaxChannelOperation.SetAffectAxes:
                    player.SetAffectAxes(
                        typed.AffectX.GetOrDefault(ctx, true),
                        typed.AffectY.GetOrDefault(ctx, true),
                        typed.AffectZ.GetOrDefault(ctx, false));
                    break;

                case ParallaxChannelOperation.SetSmoothing:
                    player.SetSmoothing(
                        typed.UseSmoothing.GetOrDefault(ctx, true),
                        typed.SmoothTime.GetOrDefault(ctx, 0.1f));
                    break;

                case ParallaxChannelOperation.SetMaxOffsetMagnitude:
                    player.SetMaxOffsetMagnitude(typed.MaxOffsetMagnitude.GetOrDefault(ctx, 0f));
                    break;

                case ParallaxChannelOperation.SetUpdateEveryNFrames:
                    player.SetUpdateEveryNFrames(typed.UpdateEveryNFrames.GetOrDefault(ctx, 1));
                    break;

                case ParallaxChannelOperation.SetAllowUnsafeRigidbody2DWrite:
                    player.SetAllowUnsafeRigidbody2DWrite(typed.AllowUnsafeRigidbody2DWrite.GetOrDefault(ctx, false));
                    break;

                case ParallaxChannelOperation.SetDriverMode:
                    player.SetDriverMode(typed.DriverMode);
                    break;

                case ParallaxChannelOperation.SetCameraBindMode:
                    player.SetCameraBindMode(typed.CameraBindMode);
                    break;

                case ParallaxChannelOperation.SetDirectTarget:
                    if (typed.DirectTarget.TryGet(ctx, out var target))
                        player.SetDirectTarget(target);
                    else
                        player.SetDirectTarget(null);
                    break;

                case ParallaxChannelOperation.SetAnimationChannelTag:
                    player.SetAnimationChannelTag(typed.AnimationChannelTag.GetOrDefault(ctx, "default"));
                    break;

                case ParallaxChannelOperation.ResetCameraOrigin:
                    player.ResetCameraOrigin();
                    break;

                case ParallaxChannelOperation.ResetRuntimeOverrides:
                    player.ResetRuntimeOverrides();
                    break;
            }
        }
    }
}
