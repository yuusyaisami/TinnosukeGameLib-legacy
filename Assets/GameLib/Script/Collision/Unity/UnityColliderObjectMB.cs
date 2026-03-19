#nullable enable
using System;
using Game;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace Game.Collision
{
    /// <summary>
    /// Unity標準のCollider2Dを CollisionSystem の self(dynamic) として登録するMB。
    /// Hit の購読/コマンドは既存の HitColliderChannelRuntime を利用する。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UnityColliderObjectMB : MonoBehaviour, IFeatureInstaller
    {
        [Header("Unity Collider")]
        [SerializeField] Collider2D? _collider;

        [Header("Layer")]
        [SerializeField, Range(0, 31)] int _layerId = 0;
        [SerializeField] uint _hitMask = ~0u;

        [Header("Dynamic Settings")]
        [SerializeField] DynamicColliderSetRef _setId = new(DynamicColliderSetId.EnemyBullet);

        [Header("User Data")]
        [SerializeField] int _userData = 0;

        [Header("Collider Initial State")]
        [SerializeField] bool _enabledByDefault = true;

        [NonSerialized] bool _initialColliderEnabled;
        [NonSerialized] bool _initialColliderEnabledCaptured;

        [Header("Runtime (ReadOnly)")]
        [SerializeField, ReadOnly] DynamicColliderHandle _dynamicHandle;
        [SerializeField, ReadOnly] bool _debugIsRegistered;
        [SerializeField, ReadOnly] bool _debugResolverAvailable;
        [SerializeField, ReadOnly] bool _debugManagerResolved;
        [SerializeField, ReadOnly] bool _debugScopeRegistryResolved;
        [SerializeField, ReadOnly] int _debugLastAttemptFrame = -1;
        [SerializeField, ReadOnly] int _debugRetryCount;
        [SerializeField, ReadOnly] string _debugLastState = "Idle";
        [SerializeField, ReadOnly] string _debugLastFailureReason = string.Empty;
        [SerializeField, ReadOnly] int _debugManagerFrameIndex = -1;
        [SerializeField, ReadOnly] int _debugManagerLastFrameHitCount;
        [SerializeField, ReadOnly] int _debugManagerRegisteredDynamicCount;
        [SerializeField, ReadOnly] bool _debugManagerTicking;
        [SerializeField, ReadOnly] int _debugSetCountPlayerHurtbox;
        [SerializeField, ReadOnly] int _debugSetCountEnemyHurtbox;
        [SerializeField, ReadOnly] int _debugSetCountPlayerBullet;
        [SerializeField, ReadOnly] int _debugSetCountEnemyBullet;
        [SerializeField, ReadOnly] bool _debugColliderEnabled;
        [SerializeField, ReadOnly] int _debugLastEnabledChangeFrame = -1;
        [SerializeField, ReadOnly] string _debugLastEnabledChangeReason = string.Empty;
        [SerializeField, ReadOnly] string _debugLastRegistrationEvent = string.Empty;

        public Collider2D? Collider => _collider != null ? _collider : GetComponent<Collider2D>();
        public int LayerId => _layerId;
        public uint HitMask => _hitMask;
        public DynamicColliderSetId SetId => _setId.Id;
        public int UserData => _userData;
        public bool EnabledByDefault => _enabledByDefault;

        public DynamicColliderHandle DynamicHandle => _dynamicHandle;

        internal void SetRegisteredHandle(DynamicColliderHandle handle)
        {
            _dynamicHandle = handle;
            _debugIsRegistered = handle.IsValid;
        }

        internal void RecordColliderEnabledChange(bool enabled, string reason)
        {
            _debugColliderEnabled = enabled;
            _debugLastEnabledChangeFrame = Time.frameCount;
            _debugLastEnabledChangeReason = reason ?? string.Empty;
        }

        internal void RecordRegistrationEvent(string reason)
        {
            _debugLastRegistrationEvent = reason ?? string.Empty;
        }

        internal void SetCollider(Collider2D? collider)
        {
            _collider = collider;
        }

        internal void SetLayerId(int layerId)
        {
            _layerId = Mathf.Clamp(layerId, 0, 31);
        }

        internal void SetHitMask(uint hitMask)
        {
            _hitMask = hitMask;
        }

        internal void SetSetId(DynamicColliderSetId setId)
        {
            _setId.Set(setId);
        }

        internal void SetUserData(int userData)
        {
            _userData = userData;
        }

        internal bool GetDesiredEnabledState()
        {
            if (!_initialColliderEnabledCaptured)
                CaptureInitialColliderEnabledState();

            return _enabledByDefault || _initialColliderEnabled;
        }

        internal void SetDebugState(
            bool resolverAvailable,
            bool managerResolved,
            bool scopeRegistryResolved,
            bool isRegistered,
            int retryCount,
            string state,
            string failureReason)
        {
            _debugResolverAvailable = resolverAvailable;
            _debugManagerResolved = managerResolved;
            _debugScopeRegistryResolved = scopeRegistryResolved;
            _debugIsRegistered = isRegistered;
            _debugRetryCount = retryCount;
            _debugLastAttemptFrame = Time.frameCount;
            _debugLastState = string.IsNullOrEmpty(state) ? "Unknown" : state;
            _debugLastFailureReason = failureReason ?? string.Empty;
        }

        internal void SetManagerDebugSnapshot(int frameIndex, int lastFrameHitCount, int registeredDynamicCount, bool isTicking)
        {
            _debugManagerFrameIndex = frameIndex;
            _debugManagerLastFrameHitCount = lastFrameHitCount;
            _debugManagerRegisteredDynamicCount = registeredDynamicCount;
            _debugManagerTicking = isTicking;
        }

        internal void SetManagerSetCounts(int playerHurtbox, int enemyHurtbox, int playerBullet, int enemyBullet)
        {
            _debugSetCountPlayerHurtbox = playerHurtbox;
            _debugSetCountEnemyHurtbox = enemyHurtbox;
            _debugSetCountPlayerBullet = playerBullet;
            _debugSetCountEnemyBullet = enemyBullet;
        }

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            if (!CollisionPipelineModeResolver.IsEnabled(scope, CollisionPipelineKind.Unity, this))
                return;

            builder.Register<UnityColliderObjectService>(Lifetime.Singleton)
                .AsSelf()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .WithParameter(this)
                .WithParameter(scope);
        }

        void OnValidate()
        {
            CaptureInitialColliderEnabledState();
            _layerId = Mathf.Clamp(_layerId, 0, 31);
        }

        void Awake()
        {
            CaptureInitialColliderEnabledState();
        }

        void CaptureInitialColliderEnabledState()
        {
            var collider = Collider;
            if (collider == null)
                return;

            _initialColliderEnabled = collider.enabled;
            _initialColliderEnabledCaptured = true;
            RecordColliderEnabledChange(collider.enabled, "CaptureInitialColliderEnabledState");
        }
    }
}
