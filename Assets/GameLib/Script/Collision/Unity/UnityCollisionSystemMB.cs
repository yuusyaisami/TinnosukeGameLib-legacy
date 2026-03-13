#nullable enable
using System;
using Game.Common;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Collision
{
    /// <summary>
    /// Projectスコープに UnityCollider 版 CollisionSystem をインストールする。
    /// v0.1: BulkCollisionSystemMB と同時に有効化しない（Frameイベント多重発火を避ける）。
    /// </summary>
    public sealed class UnityCollisionSystemMB : MonoBehaviour, IFeatureInstaller
    {
        [Header("Configuration")]
        [SerializeField] CollisionSystemProfileSO? _profile;

        [Header("Debug")]
        [SerializeField] CollisionDebugView? _debugView;

        UnityCollisionManager? _manager;
        CollisionHitRouter? _router;

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            var kind = scope.Kind;
            if (kind != LifetimeScopeKind.Project)
            {
                Game.LTSLog.LogWarning($"[{nameof(UnityCollisionSystemMB)}] Expected Project scope but got {kind}.", this);
                return;
            }

            if (!CollisionPipelineModeResolver.IsEnabled(scope, CollisionPipelineKind.Unity, this))
            {
                Game.LTSLog.Log($"[{nameof(UnityCollisionSystemMB)}] Unity pipeline is disabled by CollisionPipelineMode. Skipping.", this);
                return;
            }

            if (_profile == null)
            {
                Game.LTSLog.LogWarning($"[{nameof(UnityCollisionSystemMB)}] Collision profile is not assigned. IUnityCollisionManager will not be registered.", this);
                return;
            }

            Game.LTSLog.Log($"[{nameof(UnityCollisionSystemMB)}] Installing Unity collision system. Profile={_profile.name}", this);

            builder.RegisterBuildCallback(container =>
            {
                var eventBus = container.Resolve<ISyncEventBus>();
                CollisionEventInstaller.Install(eventBus);

                _manager = new UnityCollisionManager(eventBus, _profile);
                _router = new CollisionHitRouter(eventBus);

                if (_debugView != null)
                    _debugView.Initialize(new UnityCollisionManagerAdapter(_manager));
            });

            builder.Register<IUnityCollisionManager>(c =>
            {
                if (_manager == null)
                {
                    var eventBus = c.Resolve<ISyncEventBus>();
                    _manager = new UnityCollisionManager(eventBus, _profile);
                }
                return _manager;
            }, Lifetime.Singleton);

            builder.Register<IHitColliderChannelRouter>(c =>
            {
                if (_router == null)
                {
                    var eventBus = c.Resolve<ISyncEventBus>();
                    _router = new CollisionHitRouter(eventBus);
                }
                return _router;
            }, Lifetime.Singleton);

            builder.Register<HitColliderScopeRegistry>(Lifetime.Singleton)
                .As<IHitColliderScopeRegistry>();

            builder.Register<UnityCollisionServiceAdapter>(Lifetime.Singleton)
                .As<ICollisionService>()
                .AsSelf();

            builder.Register<UnityCollisionDispatchService>(Lifetime.Singleton)
                .AsSelf()
                .As<ILateTickable>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .WithParameter(this);
        }

        void OnDestroy()
        {
            (_manager as IDisposable)?.Dispose();
            _manager = null;

            _router?.Dispose();
            _router = null;
        }

        internal void DispatchCollisionFrame(float deltaTime)
        {
            if (_manager == null)
                return;

            _debugView?.OnPreTick();
            _manager.CollectAndDispatch(deltaTime);
            _debugView?.OnPostDispatch(_manager.LastFrameHitCount);
        }

        sealed class UnityCollisionDispatchService : ILateTickable, IScopeAcquireHandler, IScopeReleaseHandler
        {
            readonly UnityCollisionSystemMB _owner;

            public UnityCollisionDispatchService(UnityCollisionSystemMB owner)
            {
                _owner = owner;
            }

            public void OnAcquire(IScopeNode scope, bool isReset)
            {
                // no-op
            }

            public void OnRelease(IScopeNode scope, bool isReset)
            {
                // no-op
            }

            public void LateTick()
            {
                _owner.DispatchCollisionFrame(Time.deltaTime);
            }
        }

        // Minimal adapter to reuse existing CollisionDebugView without changing its signature.
        sealed class UnityCollisionManagerAdapter : IBulkCollisionManager
        {
            readonly UnityCollisionManager _m;
            public UnityCollisionManagerAdapter(UnityCollisionManager m) { _m = m; }

            public DynamicColliderHandle RegisterDynamic(in DynamicColliderDesc desc) => DynamicColliderHandle.Invalid;
            public bool UnregisterDynamic(DynamicColliderHandle handle) => false;
            public StaticColliderHandle RegisterStatic(in StaticColliderDesc desc) => StaticColliderHandle.Invalid;
            public bool UnregisterStatic(StaticColliderHandle handle) => false;
            public void SetPosition(DynamicColliderHandle handle, Unity.Mathematics.float2 position) { }
            public void SetRadius(DynamicColliderHandle handle, float radius) { }
            public void SetLayer(DynamicColliderHandle handle, int newLayerId) { }
            public void SetSetId(DynamicColliderHandle handle, DynamicColliderSetId newSetId) { }
            public void AddHitLayer(DynamicColliderHandle handle, int targetLayerId) { }
            public void RemoveHitLayer(DynamicColliderHandle handle, int targetLayerId) { }
            public void SetHitMask(DynamicColliderHandle handle, uint mask) { }
            public void TickAsync(float deltaTime, Unity.Jobs.JobHandle dependency = default) { }
            public void CompleteAndDispatch() { }
            public void CompleteInFlight() { }
            public int DynamicCount => 0;
            public int StaticCount => 0;
            public bool IsValid(DynamicColliderHandle handle) => false;
            public bool IsValid(StaticColliderHandle handle) => false;
            public Unity.Jobs.JobHandle InFlightHandle => default;
        }
    }
}
