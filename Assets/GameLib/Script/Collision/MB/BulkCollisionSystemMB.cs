// Game.Collision.BulkCollisionSystemMB.cs
//
// MonoBehaviour installer for BulkCollisionSystem v2.3.
// Attaches to ProjectLifetimeScope to register as DDOL singleton.

using System;
using Game.Common;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Collision
{
    /// <summary>
    /// Installs BulkCollisionSystem into ProjectLifetimeScope.
    /// Attach to the same GameObject as ProjectLifetimeScope.
    /// </summary>
    public sealed class BulkCollisionSystemMB : MonoBehaviour, IFeatureInstaller
    {
        [Header("Configuration")]
        [SerializeField] CollisionSystemProfileSO _profile;

        [Header("Debug")]
        [SerializeField] CollisionDebugView _debugView;

        IBulkCollisionManager _manager;
        CollisionHitRouter _router;

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            var kind = scope.Kind;
            if (kind != LifetimeScopeKind.Project)
            {
                Debug.LogWarning($"[CollisionSystemMB] Expected Project scope, got {kind}. Skipping.");
                return;
            }

            if (!CollisionPipelineModeResolver.IsEnabled(scope, CollisionPipelineKind.Custom, this))
            {
                Debug.Log("[CollisionSystemMB] Custom pipeline is disabled by CollisionPipelineMode. Skipping BulkCollisionSystemMB.");
                return;
            }

            if (_profile == null)
            {
                Debug.LogError("[CollisionSystemMB] Profile is not assigned!");
                return;
            }

            // Register event first
            builder.RegisterBuildCallback(container =>
            {
                var eventBus = container.Resolve<ISyncEventBus>();
                CollisionEventInstaller.Install(eventBus);

                // Create manager with resolved dependencies
#if UNITY_WEBGL && !UNITY_EDITOR
                _manager = new BulkCollisionManagerWebGL(eventBus, _profile);
#else
                _manager = new BulkCollisionManagerJob(eventBus, _profile);
#endif

                // Create router (must never throw; SyncEventBus is Propagate)
                _router = new CollisionHitRouter(eventBus);

                if (_debugView != null)
                {
                    _debugView.Initialize(_manager);
                }
            });

            // Register singleton
            builder.Register<IBulkCollisionManager>(c =>
            {
                if (_manager == null)
                {
                    var eventBus = c.Resolve<ISyncEventBus>();
#if UNITY_WEBGL && !UNITY_EDITOR
                    _manager = new BulkCollisionManagerWebGL(eventBus, _profile);
#else
                    _manager = new BulkCollisionManagerJob(eventBus, _profile);
#endif
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

            // Collision API (no RegisterEntryPoint; logic lives in ColliderObjectService)
            builder.Register<CollisionService>(Lifetime.Singleton)
                .As<ICollisionService>();
        }

        void OnDestroy()
        {
            (_manager as IDisposable)?.Dispose();
            _manager = null;

            _router?.Dispose();
            _router = null;
        }

        void Update()
        {
            if (_manager == null)
                return;

            _debugView?.OnPreTick();
            _manager.TickAsync(Time.deltaTime);
        }

        void LateUpdate()
        {
            if (_manager == null)
                return;

            _manager.CompleteAndDispatch();
            int hits = 0;
#if !UNITY_WEBGL || UNITY_EDITOR
            if (_manager is BulkCollisionManagerJob job)
                hits = job.LastFrameHitCount;
#endif
#if UNITY_WEBGL && !UNITY_EDITOR
            if (_manager is BulkCollisionManagerWebGL webgl)
                hits = webgl.LastFrameHitCount;
#endif

            _debugView?.OnPostDispatch(hits);
        }

        void OnValidate()
        {
            if (_profile == null)
            {
                Debug.LogWarning("[CollisionSystemMB] Profile not assigned. Create one via Create > Game/Collision/System Profile");
            }
        }
    }

    /// <summary>
    /// Serializable debug view for CollisionSystem runtime state.
    /// </summary>
    [Serializable]
    public class CollisionDebugView
    {
        [Header("Runtime Stats")]
        [SerializeField, Sirenix.OdinInspector.ReadOnly]
        int _dynamicCount;

        [SerializeField, Sirenix.OdinInspector.ReadOnly]
        int _staticCount;

        [SerializeField, Sirenix.OdinInspector.ReadOnly]
        int _lastFrameHits;

        [SerializeField, Sirenix.OdinInspector.ReadOnly]
        float _lastTickTimeMs;

        IBulkCollisionManager _manager;
        System.Diagnostics.Stopwatch _tickTimer;

        public int DynamicCount => _dynamicCount;
        public int StaticCount => _staticCount;
        public int LastFrameHits => _lastFrameHits;
        public float LastTickTimeMs => _lastTickTimeMs;

        public void Initialize(IBulkCollisionManager manager)
        {
            _manager = manager;
            _tickTimer = new System.Diagnostics.Stopwatch();
        }

        public void OnPreTick()
        {
            _tickTimer.Restart();
        }

        public void OnPostDispatch(int hitCount)
        {
            _tickTimer.Stop();
            _lastTickTimeMs = (float)_tickTimer.Elapsed.TotalMilliseconds;
            _lastFrameHits = hitCount;

            if (_manager != null)
            {
                _dynamicCount = _manager.DynamicCount;
                _staticCount = _manager.StaticCount;
            }
        }

        public void Refresh()
        {
            if (_manager != null)
            {
                _dynamicCount = _manager.DynamicCount;
                _staticCount = _manager.StaticCount;
            }
        }
    }
}
