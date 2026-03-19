// Game.Collision.ColliderObjectMB.cs
//
// Authoring/Installer MonoBehaviour for CollisionSystem colliders.
//
// 方針:
// - MB は「設定」と「DI登録(FeatureInstaller)」だけを持つ
// - 実行時ロジック(登録/解除/毎フレーム同期)は ColliderObjectService に集約
// - [Inject] は使わない（RuntimeLifetimeScope 互換）

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
    /// 1つの GameObject に Collider を付与するための MB。
    ///
    /// 使い方:
    /// - Entity/Field など任意のスコープ配下に配置
    /// - 同スコープに CommandRunnerMB を入れて ICommandRunner を登録（コマンド実行する場合）
    /// - Hit を拾う場合は同スコープに HitColliderChannelHubMB を入れて Hub を登録
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ColliderObjectMB : MonoBehaviour, IFeatureInstaller
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
        // もしLifetimeScopeがReleaseを打っても、当たり判定は残す場合は true にする。
        [SerializeField, Tooltip("trueの場合、LifetimeScopeが解放されてもコライダーは残存する。")]
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
        // IFeatureInstaller
        // ================================================================

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            if (!CollisionPipelineModeResolver.IsEnabled(scope, CollisionPipelineKind.Custom, this))
                return;

            // この MB に紐づくサービス（メインロジック）
            builder.Register<ColliderObjectService>(Lifetime.Singleton)
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .As<ITickable>()
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
