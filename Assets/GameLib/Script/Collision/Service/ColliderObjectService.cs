#nullable enable
using Game;
using Unity.Mathematics;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Collision
{
    /// <summary>
    /// ColliderObjectMB 縺ｮ螳溯｡梧凾繝ｭ繧ｸ繝・け縲・
    ///
    /// 蠖ｹ蜑ｲ:
    /// - CollisionService 縺ｸ逋ｻ骭ｲ/隗｣髯､
    /// - Dynamic 縺ｮ蝣ｴ蜷医・豈弱ヵ繝ｬ繝ｼ繝蜷梧悄・井ｽ咲ｽｮ/蜊雁ｾ・繝ｬ繧､繝､繝ｼ/繝槭せ繧ｯ/SetId・・
    /// - Dynamic 縺ｮ handle 繧・IHitColliderScopeRegistry 縺ｫ逋ｻ骭ｲ・・andle -> scope 隗｣豎ｺ逕ｨ・・
    ///
    /// 豕ｨ諢・
    /// - [Inject] 縺ｯ菴ｿ繧上↑縺・ｼ医さ繝ｳ繧ｹ繝医Λ繧ｯ繧ｿDI縺ｮ縺ｿ・・
    /// </summary>
    public sealed class ColliderObjectService :
        IScopeAcquireHandler,
        IScopeReleaseHandler,
        IScopeTickHandler
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
        // 繧ゅ＠LifetimeScope縺軍elease繧呈遠縺｣縺ｦ繧ゅ∝ｽ薙◆繧雁愛螳壹・谿九☆蝣ｴ蜷医・ true 縺ｫ縺吶ｋ縲・
        bool _retainOnScopeRelease;
        bool _onAcquirecash; // _retainOnScopeRelease縺梧怏蜉ｹ縺ｮ蝣ｴ蜷医・Acquire荳蝗槭〒縺ｮ縺ｿ繝舌う繝ｳ繝峨☆繧九・

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
            // Release縺ｮ譎ゅ↓bind繧貞・縺｣縺ｦ縺ｪ縺・◆繧√√％縺薙〒邨ゆｺ・・
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
