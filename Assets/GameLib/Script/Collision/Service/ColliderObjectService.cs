#nullable enable
using Game;
using Unity.Mathematics;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Collision
{
    /// <summary>
    /// ColliderObjectMB の実行時ロジック。
    ///
    /// 役割:
    /// - CollisionService へ登録/解除
    /// - Dynamic の場合は毎フレーム同期（位置/半径/レイヤー/マスク/SetId）
    /// - Dynamic の handle を IHitColliderScopeRegistry に登録（handle -> scope 解決用）
    ///
    /// 注意:
    /// - [Inject] は使わない（コンストラクタDIのみ）
    /// </summary>
    public sealed class ColliderObjectService :
        IScopeAcquireHandler,
        IScopeReleaseHandler,
        ITickable
    {
        readonly ColliderObjectMB _mb;
        readonly ICollisionService _collision;
        readonly IHitColliderScopeRegistry _hitScopeRegistry;
        readonly IScopeNode _ownerScope;

        DynamicColliderHandle _dynamic;
        StaticColliderHandle _static;

        float2 _lastPos;
        float _lastRadius;
        int _lastLayer;
        uint _lastHitMask;
        DynamicColliderSetId _lastSetId;
        // もしLifetimeScopeがReleaseを打っても、当たり判定は残す場合は true にする。
        bool _retainOnScopeRelease;
        bool _onAcquirecash; // _retainOnScopeReleaseが有効の場合はAcquire一回でのみバインドする。

        public DynamicColliderHandle DynamicHandle => _dynamic;
        public StaticColliderHandle StaticHandle => _static;

        public bool IsEnabled => _dynamic.IsValid || _static.IsValid;

        public ColliderObjectService(
            ColliderObjectMB mb,
            ICollisionService collision,
            IHitColliderScopeRegistry hitScopeRegistry,
            IScopeNode ownerScope)
        {
            _mb = mb;
            _collision = collision;
            _hitScopeRegistry = hitScopeRegistry;
            _ownerScope = ownerScope;
            _retainOnScopeRelease = _mb.RetainOnScopeRelease;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            // Releaseの時にbindを切ってないため、ここで終了。
            if (_retainOnScopeRelease && _onAcquirecash) return;
            _onAcquirecash = true;

            // Defensive: if we are re-acquired without a paired release (e.g. reset), cleanup old bindings.
            if (_dynamic.IsValid)
            {
                _hitScopeRegistry.Unregister(_dynamic, _ownerScope);
            }

            UnregisterInternal();
            RegisterInternal();

            if (_dynamic.IsValid)
            {
                _hitScopeRegistry.Register(_dynamic, _ownerScope);
            }
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            if (_retainOnScopeRelease) return;
            if (_dynamic.IsValid)
            {
                _hitScopeRegistry.Unregister(_dynamic, _ownerScope);
            }

            UnregisterInternal();
        }

        public void Tick()
        {
            if (!_dynamic.IsValid)
                return;

            if (_mb == null || !_mb.isActiveAndEnabled)
                return;

            var pos = GetWorldPos(_mb);
            if (math.any(math.abs(pos - _lastPos) > new float2(0.0001f, 0.0001f)))
            {
                _collision.SetPosition(_dynamic, pos);
                _lastPos = pos;
            }

            var radius = ComputeRadius(_mb);
            if (math.abs(radius - _lastRadius) > 0.0001f)
            {
                _collision.SetRadius(_dynamic, radius);
                _lastRadius = radius;
            }

            if (_mb.LayerId != _lastLayer)
            {
                _collision.SetLayer(_dynamic, _mb.LayerId);
                _lastLayer = _mb.LayerId;
            }

            if (_mb.HitMask != _lastHitMask)
            {
                _collision.SetHitMask(_dynamic, _mb.HitMask);
                _lastHitMask = _mb.HitMask;
            }

            if (_mb.SetId != _lastSetId)
            {
                _collision.SetSetId(_dynamic, _mb.SetId);
                _lastSetId = _mb.SetId;
            }
        }

        public void SetEnabled(bool enabled)
        {
            if (enabled)
            {
                if (IsEnabled)
                    return;

                RegisterInternal();
                if (_dynamic.IsValid)
                    _hitScopeRegistry.Register(_dynamic, _ownerScope);

                return;
            }

            if (_dynamic.IsValid)
                _hitScopeRegistry.Unregister(_dynamic, _ownerScope);

            UnregisterInternal();
        }

        void RegisterInternal()
        {
            if (_mb == null)
                return;

            _dynamic = DynamicColliderHandle.Invalid;
            _static = StaticColliderHandle.Invalid;
            _mb.SetRegisteredHandles(_dynamic, _static);

            if (_mb.MobilityType == ColliderMobilityType.Dynamic)
            {
                var pos = GetWorldPos(_mb);
                var radius = ComputeRadius(_mb);

                var handle = _collision.RegisterDynamic(
                    position: pos,
                    radius: radius,
                    layerId: _mb.LayerId,
                    hitMask: _mb.HitMask,
                    setId: _mb.SetId,
                    userData: _mb.UserData);

                if (!handle.IsValid)
                    return;

                _dynamic = handle;
                _mb.SetRegisteredHandles(_dynamic, StaticColliderHandle.Invalid);

                _lastPos = pos;
                _lastRadius = radius;
                _lastLayer = _mb.LayerId;
                _lastHitMask = _mb.HitMask;
                _lastSetId = _mb.SetId;
                return;
            }

            // Static
            {
                var center = GetWorldPos(_mb);
                var half = ComputeHalfExtents(_mb);

                var handle = _collision.RegisterStatic(
                    center: center,
                    halfExtents: half,
                    layerId: _mb.LayerId,
                    kind: _mb.StaticKind,
                    userData: _mb.UserData);

                if (!handle.IsValid)
                    return;

                _static = handle;
                _mb.SetRegisteredHandles(DynamicColliderHandle.Invalid, _static);
            }
        }

        void UnregisterInternal()
        {
            if (_dynamic.IsValid)
            {
                _collision.UnregisterDynamic(_dynamic);
                _dynamic = DynamicColliderHandle.Invalid;
            }

            if (_static.IsValid)
            {
                _collision.UnregisterStatic(_static);
                _static = StaticColliderHandle.Invalid;
            }

            if (_mb != null)
            {
                _mb.SetRegisteredHandles(DynamicColliderHandle.Invalid, StaticColliderHandle.Invalid);
            }
        }

        static float2 GetWorldPos(ColliderObjectMB mb)
        {
            var t = mb.transform.position;
            var off = mb.Offset;
            return new float2(t.x + off.x, t.y + off.y);
        }

        static float ComputeRadius(ColliderObjectMB mb)
        {
            return mb.ShapeType == ColliderShapeType.Circle
                ? mb.Radius
                : math.length(new float2(mb.HalfExtents.x, mb.HalfExtents.y));
        }

        static float2 ComputeHalfExtents(ColliderObjectMB mb)
        {
            return mb.ShapeType == ColliderShapeType.Box
                ? new float2(mb.HalfExtents.x, mb.HalfExtents.y)
                : new float2(mb.Radius, mb.Radius);
        }
    }
}
