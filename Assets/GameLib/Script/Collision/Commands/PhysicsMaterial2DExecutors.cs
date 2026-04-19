#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.Commands.VNext
{
    public sealed class SetColliderSharedMaterialExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.SetColliderSharedMaterial;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not SetColliderSharedMaterialCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "SetColliderSharedMaterialCommandData is required.");

            var collider = await ColliderPhysicsMaterialCommandUtility.ResolvePrimaryColliderAsync(typed.Target, ctx, ct);
            if (collider == null)
                return;

            collider.sharedMaterial = typed.SharedMaterial;
        }
    }

    public sealed class SetColliderPhysicsMaterialValuesExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.SetColliderPhysicsMaterialValues;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not SetColliderPhysicsMaterialValuesCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "SetColliderPhysicsMaterialValuesCommandData is required.");

            var collider = await ColliderPhysicsMaterialCommandUtility.ResolvePrimaryColliderAsync(typed.Target, ctx, ct);
            if (collider == null)
                return;

            if (!typed.ApplyFriction && !typed.ApplyBounciness)
                return;

            var runtimeMaterial = ColliderPhysicsMaterialCommandUtility.EnsureRuntimeMaterialInstance(collider, typed.TemplateMaterial);
            if (runtimeMaterial == null)
                return;

            if (typed.ApplyFriction)
                runtimeMaterial.friction = Mathf.Max(0f, typed.Friction.GetOrDefault(ctx, runtimeMaterial.friction));

            if (typed.ApplyBounciness)
                runtimeMaterial.bounciness = Mathf.Max(0f, typed.Bounciness.GetOrDefault(ctx, runtimeMaterial.bounciness));
        }
    }

    public sealed class SetGlobalPhysics2DExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.SetGlobalPhysics2D;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not SetGlobalPhysics2DCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "SetGlobalPhysics2DCommandData is required.");

            if (typed.ApplyGravity)
                Physics2D.gravity = typed.Gravity.GetOrDefault(ctx, Physics2D.gravity);

            if (typed.ApplySimulationMode)
                Physics2D.simulationMode = typed.SimulationMode;

            if (typed.ApplyVelocityIterations)
                Physics2D.velocityIterations = Mathf.Max(1, typed.VelocityIterations.GetOrDefault(ctx, Physics2D.velocityIterations));

            if (typed.ApplyPositionIterations)
                Physics2D.positionIterations = Mathf.Max(1, typed.PositionIterations.GetOrDefault(ctx, Physics2D.positionIterations));

            if (typed.ApplyDefaultContactOffset)
                Physics2D.defaultContactOffset = Mathf.Max(0.0001f, typed.DefaultContactOffset.GetOrDefault(ctx, Physics2D.defaultContactOffset));

            if (typed.ApplyBaumgarteScale)
                Physics2D.baumgarteScale = Mathf.Max(0f, typed.BaumgarteScale.GetOrDefault(ctx, Physics2D.baumgarteScale));

            if (typed.ApplyBaumgarteTOIScale)
                Physics2D.baumgarteTOIScale = Mathf.Max(0f, typed.BaumgarteTOIScale.GetOrDefault(ctx, Physics2D.baumgarteTOIScale));

            if (typed.ApplyQueriesHitTriggers)
                Physics2D.queriesHitTriggers = typed.QueriesHitTriggers.GetOrDefault(ctx, Physics2D.queriesHitTriggers);

            if (typed.ApplyQueriesStartInColliders)
                Physics2D.queriesStartInColliders = typed.QueriesStartInColliders.GetOrDefault(ctx, Physics2D.queriesStartInColliders);

            if (typed.ApplyCallbacksOnDisable)
                Physics2D.callbacksOnDisable = typed.CallbacksOnDisable.GetOrDefault(ctx, Physics2D.callbacksOnDisable);

            if (typed.ApplyReuseCollisionCallbacks)
                Physics2D.reuseCollisionCallbacks = typed.ReuseCollisionCallbacks.GetOrDefault(ctx, Physics2D.reuseCollisionCallbacks);

            return UniTask.CompletedTask;
        }
    }

    static class ColliderPhysicsMaterialCommandUtility
    {
        const string RuntimeMaterialSuffix = " (Runtime)";

        public static async UniTask<Collider2D?> ResolvePrimaryColliderAsync(ActorSource target, CommandContext ctx, CancellationToken ct)
        {
            var (resolvedScope, _) = await ActorScopeResolver.ResolveAsync(target, ctx, ct);
            var scope = resolvedScope ?? ctx.Scope;
            var root = scope?.Identity?.SelfTransform;
            if (root == null)
                return null;

            if (root.TryGetComponent<Collider2D>(out var ownCollider) && ownCollider != null)
                return ownCollider;

            var colliders = root.GetComponentsInChildren<Collider2D>(includeInactive: true);
            if (colliders == null || colliders.Length == 0)
                return null;

            for (var i = 0; i < colliders.Length; i++)
            {
                var collider = colliders[i];
                if (collider != null)
                    return collider;
            }

            return null;
        }

        public static PhysicsMaterial2D? EnsureRuntimeMaterialInstance(Collider2D collider, PhysicsMaterial2D? templateMaterial)
        {
            var current = collider.sharedMaterial;
            if (IsRuntimeMaterial(current))
                return current;

            var runtimeMaterial = CreateRuntimeMaterial(current, templateMaterial, collider);
            if (runtimeMaterial == null)
                return null;

            collider.sharedMaterial = runtimeMaterial;
            return runtimeMaterial;
        }

        static bool IsRuntimeMaterial(PhysicsMaterial2D? material)
        {
            return material != null
                && (material.hideFlags & HideFlags.DontSave) != 0
                && material.name.EndsWith(RuntimeMaterialSuffix, System.StringComparison.Ordinal);
        }

        static PhysicsMaterial2D? CreateRuntimeMaterial(PhysicsMaterial2D? current, PhysicsMaterial2D? templateMaterial, Collider2D collider)
        {
            var source = templateMaterial != null ? templateMaterial : current;
            PhysicsMaterial2D runtimeMaterial;
            if (source != null)
            {
                runtimeMaterial = Object.Instantiate(source);
                runtimeMaterial.name = source.name + RuntimeMaterialSuffix;
            }
            else
            {
                runtimeMaterial = new PhysicsMaterial2D(collider.name + RuntimeMaterialSuffix);
            }

            runtimeMaterial.hideFlags = HideFlags.DontSave;
            return runtimeMaterial;
        }
    }
}
