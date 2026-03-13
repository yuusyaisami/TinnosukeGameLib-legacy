#nullable enable
using System;
using System.Collections.Generic;
using Game.Common;
using Unity.Collections;
using UnityEngine;

namespace Game.Collision
{
    /// <summary>
    /// Unity標準のCollider2Dから衝突を収集し、CollisionHitFrame(Event)として出力する。
    /// v0.1: 2Dのみ。Frameイベントは 1 Unity frame につき 1 回 publish する前提。
    /// </summary>
    public sealed class UnityCollisionManager : IUnityCollisionManager, IDisposable
    {
        sealed class Entry
        {
            public Collider2D Collider = null!;
            public DynamicColliderHandle Handle;
            public int LayerId;
            public uint HitMask;
            public DynamicColliderSetId SetId;
            public int UserData;
        }

        readonly ISyncEventBus _eventBus;
        readonly CollisionSystemProfileSO _profile;

        readonly Dictionary<int, Entry> _byColliderId = new();
        readonly Dictionary<int, Entry> _byHandleId = new();
        readonly Dictionary<int, StaticColliderHandle> _staticHandleByColliderId = new();

        readonly ContactPoint2D[] _contacts = new ContactPoint2D[16];
        readonly Collider2D[] _overlaps = new Collider2D[32];
        ContactFilter2D _overlapFilter;
        readonly HashSet<int> _seenOtherColliderIds = new();

        NativeArray<CollisionHit> _dynDyn;
        NativeArray<CollisionHit> _dynStatic;

        // SetId ごとの登録数キャッシュ (DynamicColliderSetId は byte なので 256 で十分)
        readonly int[] _setCountCache = new int[256];

        int _nextId;
        int _nextStaticId;
        int _frameIndex;
        uint _frameStamp;

        public int LastFrameHitCount { get; private set; }
        public int DebugFrameIndex => _frameIndex;
        public int DebugRegisteredDynamicCount => _byColliderId.Count;

        public int GetDebugSetCount(DynamicColliderSetId setId)
        {
            return _setCountCache[(int)setId];
        }

        public UnityCollisionManager(ISyncEventBus eventBus, CollisionSystemProfileSO profile)
        {
            _eventBus = eventBus;
            _profile = profile;
            if (_profile == null)
                _profile = new CollisionSystemProfileSO(); // Safety fallback

            _dynDyn = new NativeArray<CollisionHit>(Mathf.Max(128, _profile.ResolvedDynDynCapacity), Allocator.Persistent);
            _dynStatic = new NativeArray<CollisionHit>(Mathf.Max(128, _profile.ResolvedDynStaticCapacity), Allocator.Persistent);

            // Trigger overlap queries should be explicit and cheap.
            _overlapFilter = new ContactFilter2D
            {
                useTriggers = true,
                useLayerMask = true,
                useDepth = false,
                useNormalAngle = false,
                useOutsideDepth = false,
            };
        }

        public void Dispose()
        {
            if (_dynDyn.IsCreated) _dynDyn.Dispose();
            if (_dynStatic.IsCreated) _dynStatic.Dispose();
            _byColliderId.Clear();
            _byHandleId.Clear();
            _staticHandleByColliderId.Clear();
            Array.Clear(_setCountCache, 0, _setCountCache.Length);
        }

        public DynamicColliderHandle RegisterDynamic(in UnityDynamicColliderDesc desc)
        {
            var collider = desc.Collider;
            if (collider == null)
                return DynamicColliderHandle.Invalid;

            int colliderId = collider.GetInstanceID();
            if (_byColliderId.TryGetValue(colliderId, out var existing))
            {
                existing.LayerId = desc.LayerId;
                existing.HitMask = desc.HitMask;
                if (existing.SetId != desc.SetId)
                {
                    _setCountCache[(int)existing.SetId]--;
                    _setCountCache[(int)desc.SetId]++;
                    existing.SetId = desc.SetId;
                }
                existing.UserData = desc.UserData;
                return existing.Handle;
            }

            int id = _nextId++;
            var handle = DynamicColliderHandle.FromId(id, generation: 1);

            var e = new Entry
            {
                Collider = collider,
                Handle = handle,
                LayerId = Mathf.Clamp(desc.LayerId, 0, 31),
                HitMask = desc.HitMask,
                SetId = desc.SetId,
                UserData = desc.UserData,
            };

            _byColliderId.Add(colliderId, e);
            _byHandleId.Add(handle.Id, e);
            _setCountCache[(int)e.SetId]++;
            return handle;
        }

        public bool UnregisterDynamic(DynamicColliderHandle handle)
        {
            if (!handle.IsValid)
                return false;

            if (!_byHandleId.TryGetValue(handle.Id, out var entry) || entry == null)
                return false;

            _byHandleId.Remove(handle.Id);
            if (entry.Collider != null)
                _byColliderId.Remove(entry.Collider.GetInstanceID());

            _setCountCache[(int)entry.SetId]--;
            return true;
        }

        public bool TryGetDynamicHandle(Collider2D collider, out DynamicColliderHandle handle)
        {
            handle = DynamicColliderHandle.Invalid;
            if (collider == null)
                return false;

            if (_byColliderId.TryGetValue(collider.GetInstanceID(), out var entry) && entry != null)
            {
                handle = entry.Handle;
                return handle.IsValid;
            }
            return false;
        }

        public bool IsValid(DynamicColliderHandle handle)
        {
            if (!handle.IsValid)
                return false;
            return _byHandleId.ContainsKey(handle.Id);
        }

        public bool SetLayer(DynamicColliderHandle handle, int layerId)
        {
            if (!handle.IsValid)
                return false;
            if (!_byHandleId.TryGetValue(handle.Id, out var entry) || entry == null)
                return false;

            entry.LayerId = Mathf.Clamp(layerId, 0, 31);
            return true;
        }

        public bool SetHitMask(DynamicColliderHandle handle, uint hitMask)
        {
            if (!handle.IsValid)
                return false;
            if (!_byHandleId.TryGetValue(handle.Id, out var entry) || entry == null)
                return false;

            entry.HitMask = hitMask;
            return true;
        }

        public bool SetSetId(DynamicColliderHandle handle, DynamicColliderSetId setId)
        {
            if (!handle.IsValid)
                return false;
            if (!_byHandleId.TryGetValue(handle.Id, out var entry) || entry == null)
                return false;

            var oldSetId = entry.SetId;
            if (oldSetId != setId)
            {
                _setCountCache[(int)oldSetId]--;
                _setCountCache[(int)setId]++;
            }
            entry.SetId = setId;
            return true;
        }

        public bool SetUserData(DynamicColliderHandle handle, int userData)
        {
            if (!handle.IsValid)
                return false;
            if (!_byHandleId.TryGetValue(handle.Id, out var entry) || entry == null)
                return false;

            entry.UserData = userData;
            return true;
        }

        public void CollectAndDispatch(float deltaTime)
        {
            int dynDynCount = 0;
            int dynStaticCount = 0;

            _frameIndex++;
            _frameStamp = (uint)UnityEngine.Random.Range(int.MinValue, int.MaxValue);

            foreach (var kv in _byColliderId)
            {
                var selfEntry = kv.Value;
                if (selfEntry == null)
                    continue;

                var selfCollider = selfEntry.Collider;
                if (selfCollider == null || !selfCollider.enabled)
                    continue;

                _seenOtherColliderIds.Clear();

                if (selfCollider.isTrigger)
                {
                    CollectOverlaps(selfEntry, onlyOtherTriggers: false, ref dynDynCount, ref dynStaticCount);
                    continue;
                }

                // NOTE:
                // Non-trigger colliders do not reliably report trigger overlaps via GetContacts.
                // To support Player(isTrigger=false) vs Obstacle(isTrigger=true), collect overlaps but
                // filter to trigger-only to avoid double-counting normal contacts.
                CollectOverlaps(selfEntry, onlyOtherTriggers: true, ref dynDynCount, ref dynStaticCount);

                int contactCount = 0;
                try
                {
                    contactCount = selfCollider.GetContacts(_contacts);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    continue;
                }

                for (int i = 0; i < contactCount; i++)
                {
                    var cp = _contacts[i];
                    var otherCollider = cp.otherCollider != null && cp.otherCollider != selfCollider
                        ? cp.otherCollider
                        : cp.collider;

                    if (otherCollider == null || otherCollider == selfCollider)
                        continue;

                    int otherColliderId = otherCollider.GetInstanceID();
                    if (!_seenOtherColliderIds.Add(otherColliderId))
                        continue;

                    uint selfLayerBit = 1u << Mathf.Clamp(selfEntry.LayerId, 0, 31);
                    uint otherLayerBit = 1u << Mathf.Clamp(otherCollider.gameObject.layer, 0, 31);

                    // 自前maskでもフィルタ（UnityのLayerMatrixに加えて）
                    if ((selfEntry.HitMask & otherLayerBit) == 0)
                        continue;

                    if (_byColliderId.TryGetValue(otherColliderId, out var otherEntry) && otherEntry != null)
                    {
                        // DynDyn: 片側のみ出す（Routerがmirror配送する）
                        if (selfEntry.Handle.Id >= otherEntry.Handle.Id)
                            continue;

                        if (dynDynCount >= _dynDyn.Length)
                            continue;

                        _dynDyn[dynDynCount++] = new CollisionHit
                        {
                            Kind = CollisionKind.DynamicDynamic,
                            Self = selfEntry.Handle,
                            OtherDynamic = otherEntry.Handle,
                            OtherStatic = StaticColliderHandle.Invalid,
                            SelfSetId = selfEntry.SetId,
                            OtherSetId = otherEntry.SetId,
                            OtherStaticKind = default,
                            Point = new Unity.Mathematics.float2(cp.point.x, cp.point.y),
                            Normal = new Unity.Mathematics.float2(cp.normal.x, cp.normal.y),
                            Penetration = -cp.separation,
                            Reflect = ReflectFlags.None,
                            SelfLayerBit = selfLayerBit,
                            OtherLayerBit = otherLayerBit,
                        };
                    }
                    else
                    {
                        // DynStatic: 相手が未登録の場合は Static とみなす（v0.1はKind固定）
                        if (dynStaticCount >= _dynStatic.Length)
                            continue;

                        _dynStatic[dynStaticCount++] = new CollisionHit
                        {
                            Kind = CollisionKind.DynamicStatic,
                            Self = selfEntry.Handle,
                            OtherDynamic = DynamicColliderHandle.Invalid,
                            OtherStatic = GetOrCreateStaticHandle(otherCollider),
                            SelfSetId = selfEntry.SetId,
                            OtherSetId = default,
                            OtherStaticKind = StaticColliderKind.StageGeometry,
                            Point = new Unity.Mathematics.float2(cp.point.x, cp.point.y),
                            Normal = new Unity.Mathematics.float2(cp.normal.x, cp.normal.y),
                            Penetration = -cp.separation,
                            Reflect = ReflectFlags.None,
                            SelfLayerBit = selfLayerBit,
                            OtherLayerBit = otherLayerBit,
                        };
                    }
                }
            }

            var frame = new CollisionHitFrame
            {
                FrameIndex = _frameIndex,
                FrameStamp = _frameStamp,
                DeltaTime = deltaTime,
                HitsDynDyn = _dynDyn,
                DynDynCount = dynDynCount,
                HitsDynStatic = _dynStatic,
                DynStaticCount = dynStaticCount,
            };

            LastFrameHitCount = dynDynCount + dynStaticCount;
            _eventBus.Publish(CollisionEventIds.Frame, in frame);
        }

        void CollectOverlaps(Entry selfEntry, bool onlyOtherTriggers, ref int dynDynCount, ref int dynStaticCount)
        {
            var selfCollider = selfEntry.Collider;
            if (selfCollider == null)
                return;

            // Apply the same hitMask filtering used in the contact path.
            _overlapFilter.layerMask = new LayerMask { value = unchecked((int)selfEntry.HitMask) };

            int overlapCount = 0;
            try
            {
                overlapCount = selfCollider.Overlap(_overlapFilter, _overlaps);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return;
            }

            for (int i = 0; i < overlapCount; i++)
            {
                var otherCollider = _overlaps[i];
                if (otherCollider == null || otherCollider == selfCollider)
                    continue;

                if (onlyOtherTriggers && !otherCollider.isTrigger)
                    continue;

                int otherColliderId = otherCollider.GetInstanceID();
                if (!_seenOtherColliderIds.Add(otherColliderId))
                    continue;

                uint selfLayerBit = 1u << Mathf.Clamp(selfEntry.LayerId, 0, 31);
                uint otherLayerBit = 1u << Mathf.Clamp(otherCollider.gameObject.layer, 0, 31);

                // Redundant safety filter (OverlapCollider already has layerMask), kept to match contact path semantics.
                if ((selfEntry.HitMask & otherLayerBit) == 0)
                    continue;

                if (_byColliderId.TryGetValue(otherColliderId, out var otherEntry) && otherEntry != null)
                {
                    // DynDyn: 片側のみ出す（Routerがmirror配送する）
                    if (selfEntry.Handle.Id >= otherEntry.Handle.Id)
                        continue;

                    if (dynDynCount >= _dynDyn.Length)
                        continue;

                    _dynDyn[dynDynCount++] = new CollisionHit
                    {
                        Kind = CollisionKind.DynamicDynamic,
                        Self = selfEntry.Handle,
                        OtherDynamic = otherEntry.Handle,
                        OtherStatic = StaticColliderHandle.Invalid,
                        SelfSetId = selfEntry.SetId,
                        OtherSetId = otherEntry.SetId,
                        OtherStaticKind = default,
                        // Trigger overlaps do not provide stable contact points/normals in this pipeline.
                        Point = default,
                        Normal = default,
                        Penetration = 0f,
                        Reflect = ReflectFlags.None,
                        SelfLayerBit = selfLayerBit,
                        OtherLayerBit = otherLayerBit,
                    };
                }
                else
                {
                    if (dynStaticCount >= _dynStatic.Length)
                        continue;

                    _dynStatic[dynStaticCount++] = new CollisionHit
                    {
                        Kind = CollisionKind.DynamicStatic,
                        Self = selfEntry.Handle,
                        OtherDynamic = DynamicColliderHandle.Invalid,
                        OtherStatic = GetOrCreateStaticHandle(otherCollider),
                        SelfSetId = selfEntry.SetId,
                        OtherSetId = default,
                        OtherStaticKind = StaticColliderKind.StageGeometry,
                        // Trigger overlaps do not provide stable contact points/normals in this pipeline.
                        Point = default,
                        Normal = default,
                        Penetration = 0f,
                        Reflect = ReflectFlags.None,
                        SelfLayerBit = selfLayerBit,
                        OtherLayerBit = otherLayerBit,
                    };
                }
            }

            // Clear reused buffer slots so stale refs don't keep objects alive longer than needed.
            // (Optional, but cheap at this small fixed size.)
            for (int i = 0; i < overlapCount; i++)
                _overlaps[i] = null!;
        }

        StaticColliderHandle GetOrCreateStaticHandle(Collider2D collider)
        {
            if (collider == null)
                return StaticColliderHandle.Invalid;

            var colliderId = collider.GetInstanceID();
            if (_staticHandleByColliderId.TryGetValue(colliderId, out var handle) && handle.IsValid)
                return handle;

            handle = StaticColliderHandle.FromId(_nextStaticId++, generation: 1);
            _staticHandleByColliderId[colliderId] = handle;
            return handle;
        }
    }
}
