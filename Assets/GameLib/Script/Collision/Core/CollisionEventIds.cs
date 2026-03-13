// Game.Collision.CollisionEventIds.cs
//
// Generated EventIds for CollisionSystem.
// Dense range [0, MaxCollisionEventId). Do not add gaps.

using Game.Common;

namespace Game.Collision
{
    /// <summary>
    /// Event IDs for collision events.
    /// Must be dense (no gaps) for SyncEventBus optimization.
    /// </summary>
    public static class CollisionEventIds
    {
        /// <summary>Per-frame collision hits (DynDyn + DynStatic).</summary>
        public const int Frame = ProjectEventIds.Collision_Frame;

        // Future: Add more event IDs here (e.g., Enter, Exit)
        // public const int Enter = 1;
        // public const int Exit = 2;
    }
}
