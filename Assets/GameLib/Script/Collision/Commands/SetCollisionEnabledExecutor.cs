#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Collision;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class SetCollisionEnabledExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.SetCollisionEnabled;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not SetCollisionEnabledCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "SetCollisionEnabledCommandData is required.");

            var actor = ctx.Actor ?? ctx.Scope;
            var resolver = actor.Resolver;

            bool enabled = typed.Enabled.GetOrDefault(ctx, defaultValue: false);
            bool isTrigger = typed.Trigger.GetOrDefault(ctx, defaultValue: false);

            bool any = false;

            if (typed.Kind == CollisionTargetKind.ColliderObject || typed.Kind == CollisionTargetKind.Both)
            {
                if (resolver.TryResolve<ColliderObjectService>(out var colliderObject) && colliderObject != null)
                {
                    colliderObject.SetEnabled(enabled);
                    any = true;
                }
            }

            if (typed.Kind == CollisionTargetKind.UnityColliderObject || typed.Kind == CollisionTargetKind.Both)
            {
                if (resolver.TryResolve<UnityColliderObjectService>(out var unityColliderObject) && unityColliderObject != null)
                {
                    unityColliderObject.SetEnabled(enabled);
                    unityColliderObject.SetTrigger(isTrigger);
                    any = true;
                }
            }

            if (!any)
            {
                //Debug.LogWarning($"[SetCollisionEnabledExecutor] No collision services found on actor scope '{actor}'. Kind={typed.Kind}, Enabled={enabled}");
                var selfTransform = actor.Identity?.SelfTransform;
                if (selfTransform != null)
                {
                    // Apply to all Collider2D components on the actor root and its children to handle cases
                    // where colliders are on child GameObjects instead of the root.
                    var cols = selfTransform.GetComponentsInChildren<Collider2D>(includeInactive: true);
                    if (cols != null && cols.Length > 0)
                    {
                        for (int i = 0; i < cols.Length; i++)
                        {
                            var c = cols[i];
                            if (c == null) continue;
                            c.enabled = enabled;
                            c.isTrigger = isTrigger;
                        }
                    }
                    else
                    {
                        // No colliders found on the actor hierarchy; nothing to do.
                    }
                }
                else
                {
                    //Debug.LogWarning($"[SetCollisionEnabledExecutor] No Collider component found on actor GameObject '{actor.Identity?.SelfTransform.gameObject.name}'.");
                }
            }

            return UniTask.CompletedTask;
        }
    }
}
