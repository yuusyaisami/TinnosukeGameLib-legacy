// Game.Collision.ColliderObjectMB.cs
//
// Authoring/Installer MonoBehaviour for CollisionSystem colliders.
//
// 譁ｹ驥・
// - MB 縺ｯ縲瑚ｨｭ螳壹阪→縲轡I逋ｻ骭ｲ(FeatureInstaller)縲阪□縺代ｒ謖√▽
// - 螳溯｡梧凾繝ｭ繧ｸ繝・け(逋ｻ骭ｲ/隗｣髯､/豈弱ヵ繝ｬ繝ｼ繝蜷梧悄)縺ｯ ColliderObjectService 縺ｫ髮・ｴ・
// - [Inject] 縺ｯ菴ｿ繧上↑縺・ｼ・untimeLifetimeScope 莠呈鋤・・

#nullable enable
using System;
using Game;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Collision
{
    /// <summary>
    /// Collider shape type.
    /// </summary>
    public enum ColliderShapeType
    {
        Circle,
        Box,
    }

    /// <summary>
    /// Collider mobility type.
    /// </summary>
    public enum ColliderMobilityType
    {
        Dynamic,
        Static,
    }

    /// <summary>
    /// 1縺､縺ｮ GameObject 縺ｫ Collider 繧剃ｻ倅ｸ弱☆繧九◆繧√・ MB縲・
    ///
    /// 菴ｿ縺・婿:
    /// - Entity/Field 縺ｪ縺ｩ莉ｻ諢上・繧ｹ繧ｳ繝ｼ繝鈴・荳九↓驟咲ｽｮ
    /// - 蜷後せ繧ｳ繝ｼ繝励↓ CommandRunnerMB 繧貞・繧後※ ICommandRunner 繧堤匳骭ｲ・医さ繝槭Φ繝牙ｮ溯｡後☆繧句ｴ蜷茨ｼ・
    /// - Hit 繧呈鏡縺・ｴ蜷医・蜷後せ繧ｳ繝ｼ繝励↓ HitColliderChannelHubMB 繧貞・繧後※ Hub 繧堤匳骭ｲ
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ColliderObjectMB : MonoBehaviour, IScopeInstaller
    {
        [Header("Shape")]
        [SerializeField] ColliderShapeType _shapeType = ColliderShapeType.Circle;
        [SerializeField] ColliderMobilityType _mobilityType = ColliderMobilityType.Dynamic;

        [Header("Transform")]
        [SerializeField] Vector2 _offset = Vector2.zero;

        [Header("Circle (if Shape = Circle)"), ShowIf(nameof(_shapeType), ColliderShapeType.Circle)]
        [SerializeField, Min(0.001f)] float _radius = 0.1f;

        [Header("Box (if Shape = Box)"), ShowIf(nameof(_shapeType), ColliderShapeType.Box)]
        [SerializeField] Vector2 _halfExtents = new(0.5f, 0.5f);

        [Header("Layer")]
        [SerializeField, Range(0, 31)] int _layerId = 0;
        [SerializeField] uint _hitMask = ~0u;

        [Header("Dynamic Settings")]
        [SerializeField] DynamicColliderSetRef _setId = new(DynamicColliderSetId.EnemyBullet);

        [Header("Static Settings")]
        [SerializeField] StaticColliderKindRef _staticKind = new(StaticColliderKind.StageGeometry);

        [Header("User Data")]
        [SerializeField] int _userData = 0;

        [Header("Advanced Settings")]
        // 繧ゅ＠LifetimeScope縺軍elease繧呈遠縺｣縺ｦ繧ゅ∝ｽ薙◆繧雁愛螳壹・谿九☆蝣ｴ蜷医・ true 縺ｫ縺吶ｋ縲・
        // Removed malformed inspector attribute.
        bool _retainOnScopeRelease = false;

        [Header("Runtime (ReadOnly)")]
        [SerializeField, ReadOnly] DynamicColliderHandle _dynamicHandle;
        [SerializeField, ReadOnly] StaticColliderHandle _staticHandle;

        // ---- Public properties (authoring + runtime tweaking) ----

        public ColliderShapeType ShapeType => _shapeType;
        public ColliderMobilityType MobilityType => _mobilityType;

        public Vector2 Offset { get => _offset; set => _offset = value; }

        public float Radius
        {
            get => _radius;
            set => _radius = Mathf.Max(0.001f, value);
        }

        public Vector2 HalfExtents
        {
            get => _halfExtents;
            set => _halfExtents = new Vector2(Mathf.Max(0.001f, value.x), Mathf.Max(0.001f, value.y));
        }

        public int LayerId
        {
            get => _layerId;
            set => _layerId = Mathf.Clamp(value, 0, 31);
        }

        public bool RetainOnScopeRelease => _retainOnScopeRelease;

        public uint HitMask { get => _hitMask; set => _hitMask = value; }
        public DynamicColliderSetId SetId
        {
            get => _setId.Id;
            set => _setId.Set(value);
        }

        public StaticColliderKind StaticKind
        {
            get => _staticKind.Id;
            set => _staticKind.Set(value);
        }
        public int UserData { get => _userData; set => _userData = value; }

        public DynamicColliderHandle DynamicHandle => _dynamicHandle;
        public StaticColliderHandle StaticHandle => _staticHandle;

        internal void SetRegisteredHandles(DynamicColliderHandle dynamicHandle, StaticColliderHandle staticHandle)
        {
            _dynamicHandle = dynamicHandle;
            _staticHandle = staticHandle;
        }

        // ================================================================
        // IScopeInstaller
        // ================================================================

        public void InstallScopeServices(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            if (!CollisionPipelineModeResolver.IsEnabled(scope, CollisionPipelineKind.Custom, this))
                return;

            // 縺薙・ MB 縺ｫ邏舌▼縺上し繝ｼ繝薙せ・医Γ繧､繝ｳ繝ｭ繧ｸ繝・け・・
            builder.Register<ColliderObjectService>(RuntimeLifetime.Singleton)
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .As<IScopeTickHandler>()
                .WithParameter(this)
                .WithParameter(scope);
        }

        void OnValidate()
        {
            _radius = Mathf.Max(0.001f, _radius);
            _halfExtents = new Vector2(
                Mathf.Max(0.001f, _halfExtents.x),
                Mathf.Max(0.001f, _halfExtents.y)
            );
            _layerId = Mathf.Clamp(_layerId, 0, 31);
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            var center = (Vector2)transform.position + _offset;

            Gizmos.color = _mobilityType == ColliderMobilityType.Dynamic
                ? new Color(0.2f, 1f, 0.2f, 0.5f)
                : new Color(1f, 0.5f, 0.2f, 0.5f);

            if (_shapeType == ColliderShapeType.Circle)
            {
                DrawCircleGizmo(center, _radius);
            }
            else
            {
                Gizmos.DrawWireCube(new Vector3(center.x, center.y, 0), new Vector3(_halfExtents.x * 2, _halfExtents.y * 2, 0));
            }
        }

        static void DrawCircleGizmo(Vector2 center, float radius)
        {
            const int segments = 32;
            float angleStep = 360f / segments;
            Vector3 prevPoint = center + new Vector2(radius, 0);

            for (int i = 1; i <= segments; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 newPoint = new Vector3(
                    center.x + Mathf.Cos(angle) * radius,
                    center.y + Mathf.Sin(angle) * radius,
                    0
                );
                Gizmos.DrawLine(prevPoint, newPoint);
                prevPoint = newPoint;
            }
        }
#endif
    }
}

