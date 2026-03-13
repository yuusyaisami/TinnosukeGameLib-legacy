// Game.Collision.CollisionEventInstaller.cs
//
// Registers collision events with ISyncEventBus.
// Must be called at Project scope initialization (before any Subscribe/Publish).

using Game.Common;

namespace Game.Collision
{
    /// <summary>
    /// Installs collision event registrations into ISyncEventBus.
    /// Call from Project-level LifetimeScope.
    /// </summary>
    public static class CollisionEventInstaller
    {
        /// <summary>
        /// Register all collision event types. Must be called once at startup.
        /// </summary>
        public static void Install(ISyncEventBus eventBus)
        {
            // Frame event: per-frame collision hits
            // Use Propagate policy: collision handlers must not swallow errors
            eventBus.RegisterEvent<CollisionHitFrame>(
                CollisionEventIds.Frame,
                new EventOptions(EventExceptionPolicy.Propagate));

            // Future: register additional collision events here
            // eventBus.RegisterEvent<CollisionEnterEvent>(CollisionEventIds.Enter, ...);
        }
    }
}
