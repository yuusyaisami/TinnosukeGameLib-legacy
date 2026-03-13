#nullable enable
using UnityEngine;
using VContainer;

namespace Game.Collision
{
    public enum CollisionPipelineKind
    {
        Custom = 0,
        Unity = 1,
    }

    public interface ICollisionPipelineModeService
    {
        CollisionPipelineKind Mode { get; }
    }

    public sealed class CollisionPipelineModeService : ICollisionPipelineModeService
    {
        public CollisionPipelineKind Mode { get; }

        public CollisionPipelineModeService(CollisionPipelineKind mode)
        {
            Mode = mode;
        }
    }

    static class CollisionPipelineModeResolver
    {
        public static CollisionPipelineKind Resolve(
            IScopeNode scope,
            Component? owner = null,
            CollisionPipelineKind fallback = CollisionPipelineKind.Unity)
        {
            if (scope?.Resolver != null &&
                scope.Resolver.TryResolve<ICollisionPipelineModeService>(out var service) &&
                service != null)
            {
                return service.Mode;
            }

            if (owner != null)
            {
                if (owner.TryGetComponent<CollisionPipelineModeMB>(out var localMode) && localMode != null)
                    return localMode.Mode;

                var parentMode = owner.GetComponentInParent<CollisionPipelineModeMB>(includeInactive: true);
                if (parentMode != null)
                    return parentMode.Mode;
            }

            return fallback;
        }

        public static bool IsEnabled(
            IScopeNode scope,
            CollisionPipelineKind requiredMode,
            Component? owner = null,
            CollisionPipelineKind fallback = CollisionPipelineKind.Unity)
        {
            return Resolve(scope, owner, fallback) == requiredMode;
        }
    }
}
