#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Channel;
using Game.Common;
using Game.TransformSystem;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class TeleportExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.Teleport;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not TeleportCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "TeleportCommandData is required.");

            if (ctx == null || ctx.Actor == null)
                return;

            var (targetScope, resolveError) = await ActorScopeResolver.ResolveAsync(typed.TargetActorSource, ctx, ct);
            if (targetScope == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, resolveError ?? "Teleport target actor could not be resolved.");

            var targetTransform = targetScope.Identity?.SelfTransform;
            if (targetTransform == null && targetScope is Component targetComponent)
                targetTransform = targetComponent.transform;

            if (targetTransform == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "Teleport target actor does not expose a Transform.");

            var targetResolver = targetScope.Resolver ?? ctx.Resolver;

            if (!typed.Position.TryGet(ctx, out Vector3 pos))
                return;

            if (typed.Relative)
            {
                pos += targetTransform.position;
            }

            // 1) Stop any transform animations in the target scope (they can overwrite transform position).
            if (typed.StopTransformAnimations &&
                targetResolver != null &&
                targetResolver.TryResolve<ITransformAnimationHubService>(out var hub) &&
                hub != null)
            {
                var players = hub.Players;
                if (players != null)
                {
                    for (int i = 0; i < players.Count; i++)
                    {
                        players[i]?.Stop();
                    }
                }
            }

            // 2) Teleport using the authoritative transform system if present on the target actor.
            if (targetResolver != null &&
                targetResolver.TryResolve<ITransformTeleportService>(out var teleporter) &&
                teleporter != null)
            {
                var ok = teleporter.TryTeleportWorld(pos, typed.ResetVelocity);
                if (ok)
                {
                    if (targetResolver.TryResolve<Game.CameraSystem.ICameraSystemService>(out var camSvc2) && camSvc2 != null)
                    {
                        camSvc2.ResetAll();
                    }
                }
            }

            // Fallback: direct transform write.
            if (targetTransform != null)
            {
                targetTransform.position = pos;

                // If BulkTransform is managing this transform, also update its internal buffer.
                // Otherwise BulkTransform may snap the Transform back on the next Late tick.
                if (targetResolver != null
                    && targetResolver.TryResolve<Game.TransformSystem.IBulkTransformTransformBridge>(out var bulkBridge)
                    && bulkBridge != null)
                {
                    var managed = bulkBridge.IsManaged(targetTransform);
                    if (managed)
                    {
                        var ok = bulkBridge.TryTeleportTransform(targetTransform, (Unity.Mathematics.float3)pos);
                        if (bulkBridge.TryGetManagedPosition(targetTransform, out var managedPos))
                        {
                            // BulkTransform sync info removed.
                        }
                        else
                        {
                            // BulkTransform sync info removed.
                        }
                    }
                }

                // Diagnostic: if CameraPoseOutput is registered in the actor resolver, trigger ResetAll.
                if (targetResolver != null && targetResolver.TryResolve<Game.CameraSystem.ICameraSystemService>(out var camSvc3) && camSvc3 != null)
                {

                    return;
                }
            }
            return;
        }
    }
}
