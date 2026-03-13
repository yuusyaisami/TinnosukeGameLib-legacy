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

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not TeleportCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "TeleportCommandData is required.");

            if (ctx == null || ctx.Actor == null)
                return UniTask.CompletedTask;

            var actorResolver = ctx.Actor.Resolver ?? ctx.Resolver;
            if (actorResolver == null)
                return UniTask.CompletedTask;

            if (!typed.Position.TryGet(ctx, out Vector3 pos))
                return UniTask.CompletedTask;

            if (typed.Relative)
            {
                var current = ctx.Actor?.Identity?.SelfTransform;
                if (current != null)
                    pos += current.position;
            }

            // 1) Stop any transform animations in the actor scope (they can overwrite transform position).
            if (typed.StopTransformAnimations && actorResolver.TryResolve<ITransformAnimationHubService>(out var hub) && hub != null)
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
            // 2) Teleport using the authoritative transform system if present.
            if (actorResolver.TryResolve<ITransformTeleportService>(out var teleporter) && teleporter != null)
            {
                var ok = teleporter.TryTeleportWorld(pos, typed.ResetVelocity);
                if (ok)
                {
                    // If camera system is available for this actor, trigger a ResetAll to ensure pose is applied immediately
                    var actorResolver2 = ctx.Actor?.Resolver ?? ctx.Resolver;
                    if (actorResolver2 != null && actorResolver2.TryResolve<Game.CameraSystem.ICameraSystemService>(out var camSvc2) && camSvc2 != null)
                    {
                        camSvc2.ResetAll();
                    }
                    //return UniTask.CompletedTask;
                }
            }

            // Fallback: direct transform write.
            var t = ctx.Actor?.Identity?.SelfTransform;
            if (t != null)
            {
                t.position = pos;

                // If BulkTransform is managing this transform, also update its internal buffer.
                // Otherwise BulkTransform may snap the Transform back on the next Late tick.
                if (actorResolver != null
                    && actorResolver.TryResolve<Game.TransformSystem.IBulkTransformTransformBridge>(out var bulkBridge)
                    && bulkBridge != null)
                {
                    var managed = bulkBridge.IsManaged(t);
                    if (managed)
                    {
                        var ok = bulkBridge.TryTeleportTransform(t, (Unity.Mathematics.float3)pos);
                        if (bulkBridge.TryGetManagedPosition(t, out var managedPos))
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
                if (actorResolver != null && actorResolver.TryResolve<Game.CameraSystem.ICameraSystemService>(out var camSvc3) && camSvc3 != null)
                {

                    return UniTask.CompletedTask;
                }
            }
            return UniTask.CompletedTask;
        }
    }
}
