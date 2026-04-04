#nullable enable
using UnityEngine;

namespace Game.Collision
{
    public readonly struct UnityDynamicColliderDesc
    {
        public readonly Collider2D Collider;
        public readonly int LayerId;
        public readonly uint HitMask;
        public readonly DynamicColliderSetId SetId;
        public readonly int UserData;
        public readonly string ColliderTag;

        public UnityDynamicColliderDesc(
            Collider2D collider,
            int layerId,
            uint hitMask,
            DynamicColliderSetId setId,
            int userData,
            string colliderTag = UnityColliderObjectMB.DefaultColliderTag)
        {
            Collider = collider;
            LayerId = layerId;
            HitMask = hitMask;
            SetId = setId;
            UserData = userData;
            ColliderTag = string.IsNullOrWhiteSpace(colliderTag)
                ? UnityColliderObjectMB.DefaultColliderTag
                : colliderTag.Trim();
        }
    }

    public readonly struct UnityDynamicColliderMetadata
    {
        public readonly Collider2D Collider;
        public readonly int LayerId;
        public readonly uint HitMask;
        public readonly DynamicColliderSetId SetId;
        public readonly int UserData;
        public readonly string ColliderTag;

        public UnityDynamicColliderMetadata(
            Collider2D collider,
            int layerId,
            uint hitMask,
            DynamicColliderSetId setId,
            int userData,
            string colliderTag = UnityColliderObjectMB.DefaultColliderTag)
        {
            Collider = collider;
            LayerId = layerId;
            HitMask = hitMask;
            SetId = setId;
            UserData = userData;
            ColliderTag = string.IsNullOrWhiteSpace(colliderTag)
                ? UnityColliderObjectMB.DefaultColliderTag
                : colliderTag.Trim();
        }
    }

    public interface IUnityCollisionManager
    {
        int LastFrameHitCount { get; }
        int DebugFrameIndex { get; }
        int DebugRegisteredDynamicCount { get; }
        int GetDebugSetCount(DynamicColliderSetId setId);

        DynamicColliderHandle RegisterDynamic(in UnityDynamicColliderDesc desc);
        bool UnregisterDynamic(DynamicColliderHandle handle);
        bool TryGetDynamicHandle(Collider2D collider, out DynamicColliderHandle handle);
        bool TryGetDynamicMetadata(DynamicColliderHandle handle, out UnityDynamicColliderMetadata metadata);
        bool IsValid(DynamicColliderHandle handle);
        bool SetLayer(DynamicColliderHandle handle, int layerId);
        bool SetHitMask(DynamicColliderHandle handle, uint hitMask);
        bool SetSetId(DynamicColliderHandle handle, DynamicColliderSetId setId);
        bool SetUserData(DynamicColliderHandle handle, int userData);
        bool SetColliderTag(DynamicColliderHandle handle, string colliderTag);
    }
}
