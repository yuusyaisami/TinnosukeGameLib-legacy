// Game.Collision.IBulkCollisionManager.cs
//
// Interface for the CollisionSystem manager.
// Provides registration, runtime updates, and async job scheduling.

using Unity.Jobs;

namespace Game.Collision
{
    /// <summary>
    /// Manager interface for bulk collision detection.
    /// Main-thread-only for registration/dispatch. Jobs run asynchronously.
    /// </summary>
    public interface IBulkCollisionManager
    {
        // ========== Registration ==========

        /// <summary>
        /// Register a dynamic (circle) collider. Returns handle or Invalid if capacity exceeded.
        /// </summary>
        DynamicColliderHandle RegisterDynamic(in DynamicColliderDesc desc);

        /// <summary>
        /// Unregister a dynamic collider. Returns true if handle was valid and removed.
        /// </summary>
        bool UnregisterDynamic(DynamicColliderHandle handle);

        /// <summary>
        /// Register a static (AABB) collider. Returns handle or Invalid if capacity exceeded.
        /// </summary>
        StaticColliderHandle RegisterStatic(in StaticColliderDesc desc);

        /// <summary>
        /// Unregister a static collider. Returns true if handle was valid and removed.
        /// </summary>
        bool UnregisterStatic(StaticColliderHandle handle);

        // ========== Runtime Updates ==========

        /// <summary>
        /// Update position for a dynamic collider.
        /// </summary>
        void SetPosition(DynamicColliderHandle handle, Unity.Mathematics.float2 position);

        /// <summary>
        /// Update radius for a dynamic collider.
        /// </summary>
        void SetRadius(DynamicColliderHandle handle, float radius);

        /// <summary>
        /// Update layer for a dynamic collider.
        /// </summary>
        void SetLayer(DynamicColliderHandle handle, int newLayerId);

        /// <summary>
        /// Update set ID for a dynamic collider.
        /// </summary>
        void SetSetId(DynamicColliderHandle handle, DynamicColliderSetId newSetId);

        /// <summary>
        /// Add a target layer to the hit mask.
        /// </summary>
        void AddHitLayer(DynamicColliderHandle handle, int targetLayerId);

        /// <summary>
        /// Remove a target layer from the hit mask.
        /// </summary>
        void RemoveHitLayer(DynamicColliderHandle handle, int targetLayerId);

        /// <summary>
        /// Set the entire hit mask.
        /// </summary>
        void SetHitMask(DynamicColliderHandle handle, uint mask);

        // ========== Frame Pipeline ==========

        /// <summary>
        /// Start async collision detection jobs. Can depend on external jobs.
        /// </summary>
        void TickAsync(float deltaTime, JobHandle dependency = default);

        /// <summary>
        /// Complete jobs and publish collision events via ISyncEventBus.
        /// Must be called on main thread.
        /// </summary>
        void CompleteAndDispatch();

        /// <summary>
        /// Complete any in-flight jobs without dispatching events.
        /// Useful for cleanup or synchronization.
        /// </summary>
        void CompleteInFlight();

        // ========== Queries ==========

        /// <summary>Number of active dynamic colliders.</summary>
        int DynamicCount { get; }

        /// <summary>Number of active static colliders.</summary>
        int StaticCount { get; }

        /// <summary>Check if a dynamic handle is still valid.</summary>
        bool IsValid(DynamicColliderHandle handle);

        /// <summary>Check if a static handle is still valid.</summary>
        bool IsValid(StaticColliderHandle handle);

        /// <summary>Handle for current in-flight jobs (can be used as dependency).</summary>
        JobHandle InFlightHandle { get; }
    }
}
