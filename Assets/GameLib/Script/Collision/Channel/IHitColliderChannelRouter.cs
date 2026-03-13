#nullable enable

namespace Game.Collision
{
    public interface IHitColliderChannelRouter
    {
        void RegisterWatcher(DynamicColliderHandle self, HitColliderChannelRuntime runtime, HitWatchFlags flags);
        void UnregisterWatcher(DynamicColliderHandle self, HitColliderChannelRuntime runtime);
        void UpdateWatcherFlags(DynamicColliderHandle self, HitColliderChannelRuntime runtime, HitWatchFlags flags);
    }
}
