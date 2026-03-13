#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.TransformSystem;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class TransformControllerRigidbody2DExecutor : ICommandExecutor
    {
        const int ResolveRetryFrames = 5;

        public int CommandId => CommandIds.TransformControllerRigidbody2D;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not TransformControllerRigidbody2DCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "TransformControllerRigidbody2DCommandData is required.");

            var (resolvedScope, resolveError) = await ActorScopeResolver.ResolveAsync(typed.Target, ctx, ct);
            if (resolvedScope == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"TransformControllerRigidbody2D target resolve failed: {resolveError}");

            var scope = resolvedScope;
            var resolver = scope?.Resolver;
            if (resolver == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "TransformControllerRigidbody2D target scope resolver is null.");

            TransformControllerService? service = null;
            for (var attempt = 0; attempt <= ResolveRetryFrames; attempt++)
            {
                if (TryResolveService(resolver, out service) && service != null)
                    break;

                if (attempt == ResolveRetryFrames)
                    break;

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            if (service == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "TransformControllerService is missing on resolved target scope.");

            if (typed.ApplyForceZeroWhenBlocked)
                service.SetForceZeroVelocityWhenMovementBlocked(typed.ForceZeroWhenBlocked.GetOrDefault(ctx, true));

            if (typed.ApplyTransformControllerMovementBlock)
            {
                var blocked = typed.TransformControllerMovementBlocked.GetOrDefault(ctx, true);
                service.SetTransformControllerMovementBlocked(blocked, typed.BlockReason);
            }

            if (typed.ApplyMovementEnabled)
                service.SetMovementEnabled(typed.MovementEnabled.GetOrDefault(ctx, true));

            if (typed.ApplyRotationEnabled)
                service.SetRotationEnabled(typed.RotationEnabled.GetOrDefault(ctx, true));

            var applied = service.TryApplyRigidbody2DSettings(
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
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "TransformControllerRigidbody2D apply failed. OutputTarget is not Rigidbody2D or Rigidbody2D is unavailable.");

            if (typed.ForceStopMovementNow)
                service.ForceStopMovementNow();
        }

        static bool TryResolveService(IObjectResolver resolver, out TransformControllerService? service)
        {
            service = null;
            if (resolver == null)
                return false;

            if (resolver.TryResolve<TransformControllerService>(out var direct) && direct != null)
            {
                service = direct;
                return true;
            }

            if (resolver.TryResolve<ITransformControllerTelemetry>(out var telemetry) && telemetry is TransformControllerService byTelemetry)
            {
                service = byTelemetry;
                return true;
            }

            if (resolver.TryResolve<ITransformTeleportService>(out var teleport) && teleport is TransformControllerService byTeleport)
            {
                service = byTeleport;
                return true;
            }

            if (resolver.TryResolve<ITransformControllerPoseReader>(out var poseReader) && poseReader is TransformControllerService byPoseReader)
            {
                service = byPoseReader;
                return true;
            }

            return false;
        }
    }
}
