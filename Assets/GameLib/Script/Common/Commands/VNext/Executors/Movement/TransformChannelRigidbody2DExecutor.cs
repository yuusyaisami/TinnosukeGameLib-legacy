#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.TransformSystem;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class TransformChannelRigidbody2DExecutor : ICommandExecutor
    {
        const int ResolveRetryFrames = 5;

        public int CommandId => CommandIds.TransformChannelRigidbody2D;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not TransformChannelRigidbody2DCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "TransformChannelRigidbody2DCommandData is required.");

            var (resolvedScope, resolveError) = await ActorScopeResolver.ResolveAsync(typed.Target, ctx, ct);
            if (resolvedScope == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"TransformChannelRigidbody2D target resolve failed: {resolveError}");

            var resolver = resolvedScope.Resolver;
            if (resolver == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "TransformChannelRigidbody2D target scope resolver is null.");

            ITransformChannelRuntime? runtime = null;
            for (var attempt = 0; attempt <= ResolveRetryFrames; attempt++)
            {
                if (TryResolveRuntime(resolver, typed.NormalizedChannelTag, out runtime) && runtime != null)
                    break;

                if (attempt == ResolveRetryFrames)
                    break;

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            if (runtime == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, $"TransformChannel runtime is missing. tag={typed.NormalizedChannelTag}");

            if (typed.ApplyForceZeroWhenBlocked)
                runtime.SetForceZeroVelocityWhenMovementBlocked(typed.ForceZeroWhenBlocked.GetOrDefault(ctx, true));

            if (typed.ApplyTransformChannelMovementBlock)
            {
                var blocked = typed.TransformChannelMovementBlocked.GetOrDefault(ctx, true);
                runtime.SetTransformChannelMovementBlocked(blocked, typed.BlockReason);
            }

            if (typed.ApplyMovementEnabled)
                runtime.SetMovementEnabled(typed.MovementEnabled.GetOrDefault(ctx, true));

            if (typed.ApplyRotationEnabled)
                runtime.SetRotationEnabled(typed.RotationEnabled.GetOrDefault(ctx, true));

            var applied = runtime.TryApplyRigidbody2DSettings(
                applySimulated: typed.ApplySimulated,
                simulated: typed.Simulated.GetOrDefault(ctx, true),
                applyGravityScale: typed.ApplyGravityScale,
                gravityScale: typed.GravityScale.GetOrDefault(ctx, 1f),
                applyFreezeRotation: typed.ApplyFreezeRotation,
                freezeRotation: typed.FreezeRotation.GetOrDefault(ctx, false),
                applyLinearVelocity: typed.ApplyLinearVelocity,
                linearVelocity: typed.LinearVelocity.GetOrDefault(ctx, UnityEngine.Vector2.zero),
                applyAngularVelocity: typed.ApplyAngularVelocity,
                angularVelocity: typed.AngularVelocity.GetOrDefault(ctx, 0f));

            if (!applied)
            {
                throw new CommandExecutionException(
                    CommandRunFailureKind.InvalidArgs,
                    $"TransformChannelRigidbody2D apply failed. tag={typed.NormalizedChannelTag} output is not Rigidbody2D or Rigidbody2D is unavailable.");
            }

            if (typed.ForceStopMovementNow)
                runtime.ForceStopMovementNow();
        }

        static bool TryResolveRuntime(IRuntimeResolver resolver, string channelTag, out ITransformChannelRuntime? runtime)
        {
            runtime = null;
            if (resolver == null)
                return false;

            if (!resolver.TryResolve<ITransformChannelHubService>(out var hub) || hub == null)
                return false;

            if (!hub.TryGetRuntime(channelTag, out var resolved) || resolved == null)
                return false;

            runtime = resolved;
            return true;
        }
    }
}
