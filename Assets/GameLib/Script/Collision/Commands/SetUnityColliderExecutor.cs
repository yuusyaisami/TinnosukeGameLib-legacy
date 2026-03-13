#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Collision;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class SetUnityColliderExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.SetUnityCollider;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not SetUnityColliderCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "SetUnityColliderCommandData is required.");

            var actor = ctx.Actor ?? ctx.Scope;
            if (actor == null)
                return UniTask.CompletedTask;

            var root = actor.Identity?.SelfTransform;
            if (root == null)
                return UniTask.CompletedTask;

            var resolver = actor.Resolver;
            UnityColliderObjectService? unityService = null;
            if (resolver != null && resolver.TryResolve<UnityColliderObjectService>(out var resolvedService))
                unityService = resolvedService;

            var unityColliderMb = root.GetComponentInChildren<UnityColliderObjectMB>(includeInactive: true);
            var collider = ResolvePrimaryCollider(root, unityColliderMb);
            var host = ResolveHost(root, unityColliderMb, collider);
            if (host == null)
                return UniTask.CompletedTask;

            var wasServiceEnabled = unityService?.IsEnabled ?? false;
            var colliderEnabledBeforeReconfigure = collider?.enabled ?? false;
            var colliderTriggerBeforeReconfigure = collider?.isTrigger ?? false;

            if (unityService != null && wasServiceEnabled)
                unityService.SetEnabled(false);

            collider = EnsureModeCollider(host, collider, typed.Mode);
            if (collider == null)
                return UniTask.CompletedTask;

            if (unityColliderMb != null)
                unityColliderMb.SetCollider(collider);

            var currentEnabled = colliderEnabledBeforeReconfigure;
            var currentIsTrigger = colliderTriggerBeforeReconfigure;

            var finalEnabled = typed.ApplyEnabled
                ? typed.Enabled.GetOrDefault(ctx, true)
                : currentEnabled;
            var finalIsTrigger = typed.ApplyIsTrigger
                ? typed.IsTrigger.GetOrDefault(ctx, currentIsTrigger)
                : currentIsTrigger;

            if (typed.ApplyOffset)
            {
                var offset = typed.Offset.GetOrDefault(ctx, collider.offset);
                collider.offset = offset;
            }

            if (typed.ApplySize)
            {
                var size = typed.Size.GetOrDefault(ctx, Vector2.one);
                ApplyGenericSize(collider, size);
            }

            ApplyModeSpecificSettings(collider, typed, ctx);

            if (typed.ApplySharedMaterial)
                collider.sharedMaterial = typed.SharedMaterial;

            if (typed.ApplyLayerId)
            {
                var layerId = Mathf.Clamp(typed.LayerId.GetOrDefault(ctx, collider.gameObject.layer), 0, 31);
                collider.gameObject.layer = layerId;
                unityColliderMb?.SetLayerId(layerId);
                unityService?.SetLayerId(layerId);
            }

            if (typed.ApplyHitMask)
            {
                var hitMaskRaw = typed.HitMask.GetOrDefault(ctx, unchecked((int)~0u));
                var hitMask = unchecked((uint)hitMaskRaw);
                unityColliderMb?.SetHitMask(hitMask);
                unityService?.SetHitMask(hitMask);
            }

            if (typed.ApplySetId)
            {
                var setIdRaw = typed.SetId.GetOrDefault(ctx, (int)DynamicColliderSetId.EnemyBullet);
                var setId = (DynamicColliderSetId)setIdRaw;
                unityColliderMb?.SetSetId(setId);
                unityService?.SetSetId(setId);
            }

            if (typed.ApplyUserData)
            {
                var userData = typed.UserData.GetOrDefault(ctx, 0);
                unityColliderMb?.SetUserData(userData);
                unityService?.SetUserData(userData);
            }

            collider.isTrigger = finalIsTrigger;

            if (unityService != null)
            {
                unityService.SetTrigger(finalIsTrigger);
                unityService.SetEnabled(finalEnabled);
            }
            else
            {
                collider.enabled = finalEnabled;
            }

            return UniTask.CompletedTask;
        }

        static Collider2D? ResolvePrimaryCollider(Transform root, UnityColliderObjectMB? unityColliderMb)
        {
            var viaMb = unityColliderMb?.Collider;
            if (viaMb != null)
                return viaMb;

            if (root.TryGetComponent<Collider2D>(out var own) && own != null)
                return own;

            var colliders = root.GetComponentsInChildren<Collider2D>(includeInactive: true);
            if (colliders == null || colliders.Length == 0)
                return null;

            for (int i = 0; i < colliders.Length; i++)
            {
                var c = colliders[i];
                if (c != null)
                    return c;
            }

            return null;
        }

        static GameObject? ResolveHost(Transform root, UnityColliderObjectMB? unityColliderMb, Collider2D? currentCollider)
        {
            if (currentCollider != null)
                return currentCollider.gameObject;
            if (unityColliderMb != null)
                return unityColliderMb.gameObject;
            return root.gameObject;
        }

        static Collider2D? EnsureModeCollider(GameObject host, Collider2D? current, UnityColliderShapeMode mode)
        {
            return mode switch
            {
                UnityColliderShapeMode.Circle => EnsureTypedCollider<CircleCollider2D>(host, current),
                UnityColliderShapeMode.Box => EnsureTypedCollider<BoxCollider2D>(host, current),
                UnityColliderShapeMode.Capsule => EnsureTypedCollider<CapsuleCollider2D>(host, current),
                _ => current ?? host.GetComponent<Collider2D>(),
            };
        }

        static T EnsureTypedCollider<T>(GameObject host, Collider2D? current) where T : Collider2D
        {
            if (current is T typedCurrent)
                return typedCurrent;

            if (host.TryGetComponent<T>(out var existingTyped) && existingTyped != null)
            {
                DisableOtherColliders(host, existingTyped);
                return existingTyped;
            }

            var added = host.AddComponent<T>();
            DisableOtherColliders(host, added);
            return added;
        }

        static void DisableOtherColliders(GameObject host, Collider2D active)
        {
            var all = host.GetComponents<Collider2D>();
            for (int i = 0; i < all.Length; i++)
            {
                var c = all[i];
                if (c == null || ReferenceEquals(c, active))
                    continue;
                c.enabled = false;
            }
        }

        static void ApplyGenericSize(Collider2D collider, Vector2 size)
        {
            var safeSize = new Vector2(Mathf.Max(0.001f, size.x), Mathf.Max(0.001f, size.y));

            switch (collider)
            {
                case CircleCollider2D circle:
                    circle.radius = Mathf.Max(0.0005f, Mathf.Max(safeSize.x, safeSize.y) * 0.5f);
                    break;
                case BoxCollider2D box:
                    box.size = safeSize;
                    break;
                case CapsuleCollider2D capsule:
                    capsule.size = safeSize;
                    break;
            }
        }

        static void ApplyModeSpecificSettings(Collider2D collider, SetUnityColliderCommandData typed, CommandContext ctx)
        {
            switch (typed.Mode)
            {
                case UnityColliderShapeMode.Circle:
                    if (collider is CircleCollider2D circle)
                    {
                        var radius = typed.CircleRadius.GetOrDefault(ctx, circle.radius);
                        circle.radius = Mathf.Max(0.0005f, radius);
                    }
                    break;
                case UnityColliderShapeMode.Box:
                    if (collider is BoxCollider2D box)
                    {
                        var edgeRadius = typed.BoxEdgeRadius.GetOrDefault(ctx, box.edgeRadius);
                        box.edgeRadius = Mathf.Max(0f, edgeRadius);
                    }
                    break;
                case UnityColliderShapeMode.Capsule:
                    if (collider is CapsuleCollider2D capsule)
                    {
                        capsule.direction = typed.CapsuleDirection;
                    }
                    break;
            }
        }
    }
}
