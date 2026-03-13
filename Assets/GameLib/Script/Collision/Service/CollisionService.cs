// Game.Collision.CollisionService.cs
//
// CollisionSystem の登録/更新 API を提供する薄いサービス。
// - 実体は IBulkCollisionManager
// - コライダー個別のライフサイクルは ColliderObjectService 側で扱う

#nullable enable
using Unity.Mathematics;

namespace Game.Collision
{
    public interface ICollisionService
    {
        // ========== Registration ==========

        DynamicColliderHandle RegisterDynamic(
            float2 position,
            float radius,
            int layerId,
            uint hitMask,
            DynamicColliderSetId setId,
            int userData = 0);

        StaticColliderHandle RegisterStatic(
            float2 center,
            float2 halfExtents,
            int layerId,
            StaticColliderKind kind,
            int userData = 0);

        bool UnregisterDynamic(DynamicColliderHandle handle);
        bool UnregisterStatic(StaticColliderHandle handle);

        // ========== Updates ==========

        void SetPosition(DynamicColliderHandle handle, float2 position);
        void SetRadius(DynamicColliderHandle handle, float radius);
        void SetLayer(DynamicColliderHandle handle, int layerId);
        void SetSetId(DynamicColliderHandle handle, DynamicColliderSetId setId);
        void SetHitMask(DynamicColliderHandle handle, uint mask);
        void AddHitLayer(DynamicColliderHandle handle, int layerId);
        void RemoveHitLayer(DynamicColliderHandle handle, int layerId);

        // ========== Queries ==========

        bool IsValid(DynamicColliderHandle handle);
        bool IsValid(StaticColliderHandle handle);
    }

    /// <summary>
    /// デフォルト実装。例外を投げず、IBulkCollisionManager へ委譲する。
    /// </summary>
    public sealed class CollisionService : ICollisionService
    {
        readonly IBulkCollisionManager _manager;

        public CollisionService(IBulkCollisionManager manager)
        {
            _manager = manager;
        }

        public DynamicColliderHandle RegisterDynamic(
            float2 position,
            float radius,
            int layerId,
            uint hitMask,
            DynamicColliderSetId setId,
            int userData = 0)
        {
            var desc = new DynamicColliderDesc
            {
                Position = position,
                Radius = radius,
                LayerId = layerId,
                HitLayerMask = hitMask,
                SetId = setId,
                UserData = userData,
            };
            return _manager.RegisterDynamic(in desc);
        }

        public StaticColliderHandle RegisterStatic(
            float2 center,
            float2 halfExtents,
            int layerId,
            StaticColliderKind kind,
            int userData = 0)
        {
            var desc = new StaticColliderDesc
            {
                Center = center,
                HalfExtents = halfExtents,
                LayerId = layerId,
                Kind = kind,
                UserData = userData,
            };
            return _manager.RegisterStatic(in desc);
        }

        public bool UnregisterDynamic(DynamicColliderHandle handle) =>
            _manager.UnregisterDynamic(handle);

        public bool UnregisterStatic(StaticColliderHandle handle) =>
            _manager.UnregisterStatic(handle);

        public void SetPosition(DynamicColliderHandle handle, float2 position) =>
            _manager.SetPosition(handle, position);

        public void SetRadius(DynamicColliderHandle handle, float radius) =>
            _manager.SetRadius(handle, radius);

        public void SetLayer(DynamicColliderHandle handle, int layerId) =>
            _manager.SetLayer(handle, layerId);

        public void SetSetId(DynamicColliderHandle handle, DynamicColliderSetId setId) =>
            _manager.SetSetId(handle, setId);

        public void SetHitMask(DynamicColliderHandle handle, uint mask) =>
            _manager.SetHitMask(handle, mask);

        public void AddHitLayer(DynamicColliderHandle handle, int layerId) =>
            _manager.AddHitLayer(handle, layerId);

        public void RemoveHitLayer(DynamicColliderHandle handle, int layerId) =>
            _manager.RemoveHitLayer(handle, layerId);

        public bool IsValid(DynamicColliderHandle handle) =>
            _manager.IsValid(handle);

        public bool IsValid(StaticColliderHandle handle) =>
            _manager.IsValid(handle);
    }
}
