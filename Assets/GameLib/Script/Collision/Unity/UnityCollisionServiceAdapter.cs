#nullable enable
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Collision
{
    /// <summary>
    /// Bridges ICollisionService calls onto UnityCollider-based collision backend.
    /// Dynamic/Static colliders are materialized as hidden Collider2D GameObjects.
    /// </summary>
    public sealed class UnityCollisionServiceAdapter : ICollisionService, System.IDisposable
    {
        sealed class DynamicEntry
        {
            public GameObject GameObject = null!;
            public CircleCollider2D Collider = null!;
            public DynamicColliderHandle Handle;
            public uint HitMask;
            public DynamicColliderSetId SetId;
        }

        sealed class StaticEntry
        {
            public StaticColliderHandle StaticHandle;
            public DynamicColliderHandle BackingDynamicHandle;
            public GameObject GameObject = null!;
            public BoxCollider2D Collider = null!;
        }

        readonly IUnityCollisionManager _manager;
        readonly Dictionary<int, DynamicEntry> _dynamicByHandleId = new();
        readonly Dictionary<int, StaticEntry> _staticByHandleId = new();
        GameObject? _rootObject;
        int _nextStaticHandleId;

        public UnityCollisionServiceAdapter(IUnityCollisionManager manager)
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
            EnsureRootObject();

            var go = new GameObject("UnityCollision.Dynamic");
            go.transform.SetParent(_rootObject!.transform, worldPositionStays: false);
            go.transform.position = new Vector3(position.x, position.y, 0f);
            go.layer = Mathf.Clamp(layerId, 0, 31);

            var collider = go.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = Mathf.Max(0.001f, radius);

            var handle = _manager.RegisterDynamic(new UnityDynamicColliderDesc(
                collider: collider,
                layerId: layerId,
                hitMask: hitMask,
                setId: setId,
                userData: userData));

            if (!handle.IsValid)
            {
                DestroyRuntimeObject(go);
                return DynamicColliderHandle.Invalid;
            }

            _dynamicByHandleId[handle.Id] = new DynamicEntry
            {
                GameObject = go,
                Collider = collider,
                Handle = handle,
                HitMask = hitMask,
                SetId = setId,
            };

            return handle;
        }

        public StaticColliderHandle RegisterStatic(
            float2 center,
            float2 halfExtents,
            int layerId,
            StaticColliderKind kind,
            int userData = 0)
        {
            EnsureRootObject();

            var go = new GameObject("UnityCollision.StaticProxy");
            go.transform.SetParent(_rootObject!.transform, worldPositionStays: false);
            go.transform.position = new Vector3(center.x, center.y, 0f);
            go.layer = Mathf.Clamp(layerId, 0, 31);

            var collider = go.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(
                Mathf.Max(0.001f, halfExtents.x * 2f),
                Mathf.Max(0.001f, halfExtents.y * 2f));

            var backingHandle = _manager.RegisterDynamic(new UnityDynamicColliderDesc(
                collider: collider,
                layerId: layerId,
                hitMask: ~0u,
                setId: MapStaticKind(kind),
                userData: userData));

            if (!backingHandle.IsValid)
            {
                DestroyRuntimeObject(go);
                return StaticColliderHandle.Invalid;
            }

            var staticHandle = StaticColliderHandle.FromId(_nextStaticHandleId++, generation: 1);
            _staticByHandleId[staticHandle.Id] = new StaticEntry
            {
                StaticHandle = staticHandle,
                BackingDynamicHandle = backingHandle,
                GameObject = go,
                Collider = collider,
            };
            return staticHandle;
        }

        public bool UnregisterDynamic(DynamicColliderHandle handle)
        {
            if (!handle.IsValid)
                return false;
            if (!_dynamicByHandleId.TryGetValue(handle.Id, out var entry) || entry == null)
                return false;

            _dynamicByHandleId.Remove(handle.Id);
            _manager.UnregisterDynamic(handle);
            DestroyRuntimeObject(entry.GameObject);
            return true;
        }

        public bool UnregisterStatic(StaticColliderHandle handle)
        {
            if (!handle.IsValid)
                return false;
            if (!_staticByHandleId.TryGetValue(handle.Id, out var entry) || entry == null)
                return false;

            _staticByHandleId.Remove(handle.Id);
            _manager.UnregisterDynamic(entry.BackingDynamicHandle);
            DestroyRuntimeObject(entry.GameObject);
            return true;
        }

        public void SetPosition(DynamicColliderHandle handle, float2 position)
        {
            if (!handle.IsValid)
                return;
            if (!_dynamicByHandleId.TryGetValue(handle.Id, out var entry) || entry == null)
                return;

            entry.GameObject.transform.position = new Vector3(position.x, position.y, 0f);
        }

        public void SetRadius(DynamicColliderHandle handle, float radius)
        {
            if (!handle.IsValid)
                return;
            if (!_dynamicByHandleId.TryGetValue(handle.Id, out var entry) || entry == null)
                return;

            entry.Collider.radius = Mathf.Max(0.001f, radius);
        }

        public void SetLayer(DynamicColliderHandle handle, int layerId)
        {
            if (!handle.IsValid)
                return;
            if (!_dynamicByHandleId.TryGetValue(handle.Id, out var entry) || entry == null)
                return;

            var clamped = Mathf.Clamp(layerId, 0, 31);
            entry.GameObject.layer = clamped;
            _manager.SetLayer(handle, clamped);
        }

        public void SetSetId(DynamicColliderHandle handle, DynamicColliderSetId setId)
        {
            if (!handle.IsValid)
                return;
            if (!_dynamicByHandleId.TryGetValue(handle.Id, out var entry) || entry == null)
                return;

            entry.SetId = setId;
            _manager.SetSetId(handle, setId);
        }

        public void SetHitMask(DynamicColliderHandle handle, uint mask)
        {
            if (!handle.IsValid)
                return;
            if (!_dynamicByHandleId.TryGetValue(handle.Id, out var entry) || entry == null)
                return;

            entry.HitMask = mask;
            _manager.SetHitMask(handle, mask);
        }

        public void AddHitLayer(DynamicColliderHandle handle, int layerId)
        {
            if (!handle.IsValid)
                return;
            if (!_dynamicByHandleId.TryGetValue(handle.Id, out var entry) || entry == null)
                return;

            var bit = 1u << Mathf.Clamp(layerId, 0, 31);
            entry.HitMask |= bit;
            _manager.SetHitMask(handle, entry.HitMask);
        }

        public void RemoveHitLayer(DynamicColliderHandle handle, int layerId)
        {
            if (!handle.IsValid)
                return;
            if (!_dynamicByHandleId.TryGetValue(handle.Id, out var entry) || entry == null)
                return;

            var bit = 1u << Mathf.Clamp(layerId, 0, 31);
            entry.HitMask &= ~bit;
            _manager.SetHitMask(handle, entry.HitMask);
        }

        public bool IsValid(DynamicColliderHandle handle)
        {
            if (!handle.IsValid)
                return false;
            return _dynamicByHandleId.ContainsKey(handle.Id) && _manager.IsValid(handle);
        }

        public bool IsValid(StaticColliderHandle handle)
        {
            if (!handle.IsValid)
                return false;
            return _staticByHandleId.ContainsKey(handle.Id);
        }

        public void Dispose()
        {
            foreach (var entry in _dynamicByHandleId.Values)
            {
                _manager.UnregisterDynamic(entry.Handle);
                DestroyRuntimeObject(entry.GameObject);
            }
            _dynamicByHandleId.Clear();

            foreach (var entry in _staticByHandleId.Values)
            {
                _manager.UnregisterDynamic(entry.BackingDynamicHandle);
                DestroyRuntimeObject(entry.GameObject);
            }
            _staticByHandleId.Clear();

            if (_rootObject != null)
            {
                DestroyRuntimeObject(_rootObject);
                _rootObject = null;
            }
        }

        void EnsureRootObject()
        {
            if (_rootObject != null)
                return;

            _rootObject = new GameObject("UnityCollisionServiceAdapter");
            _rootObject.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
        }

        static DynamicColliderSetId MapStaticKind(StaticColliderKind kind)
        {
            CollisionIdCatalogLocator.TryResolveStaticProxySet(kind, out var setId);
            return setId;
        }

        static void DestroyRuntimeObject(GameObject target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
            {
                Object.Destroy(target);
                return;
            }

            Object.DestroyImmediate(target);
        }
    }
}
