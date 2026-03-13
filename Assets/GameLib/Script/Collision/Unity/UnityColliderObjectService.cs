#nullable enable
using Game;
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace Game.Collision
{
    /// <summary>
    /// UnityColliderObjectMB の実行時ロジック。
    /// - IUnityCollisionManager へ登録/解除
    /// - IHitColliderScopeRegistry へ handle->scope を登録
    /// - (同スコープにあれば) HitColliderChannelRuntime へ self を bind
    /// </summary>
    public sealed class UnityColliderObjectService :
        IScopeAcquireHandler,
        IScopeReleaseHandler
    {
        readonly UnityColliderObjectMB _mb;
        readonly IScopeNode _ownerScope;
        IUnityCollisionManager? _manager;
        IHitColliderScopeRegistry? _hitScopeRegistry;
        bool _loggedMissingManager;
        bool _loggedMissingRegistry;
        CancellationTokenSource? _retryCts;
#if UNITY_EDITOR
        CancellationTokenSource? _monitorCts;
        int _lastObservedManagerFrameIndex = -1;
#endif
        int _retryCount;

        DynamicColliderHandle _handle;
        const int RegisterRetryFrames = 60;

        public bool IsEnabled => _handle.IsValid;
        public DynamicColliderHandle DynamicHandle => _handle;

        public UnityColliderObjectService(
            UnityColliderObjectMB mb,
            IScopeNode ownerScope)
        {
            _mb = mb;
            _ownerScope = ownerScope;
        }

        bool TryEnsureDependencies()
        {
            if (_manager != null && _hitScopeRegistry != null)
            {
                _mb.SetDebugState(
                    resolverAvailable: true,
                    managerResolved: true,
                    scopeRegistryResolved: true,
                    isRegistered: _handle.IsValid,
                    retryCount: _retryCount,
                    state: "DependenciesReady",
                    failureReason: string.Empty);
                return true;
            }

            var resolver = _ownerScope?.Resolver;
            if (resolver == null)
            {
                _mb.SetDebugState(
                    resolverAvailable: false,
                    managerResolved: _manager != null,
                    scopeRegistryResolved: _hitScopeRegistry != null,
                    isRegistered: _handle.IsValid,
                    retryCount: _retryCount,
                    state: "MissingResolver",
                    failureReason: "scope.Resolver is null");
                return false;
            }

            if (_manager == null)
            {
                if (resolver.TryResolve(typeof(IUnityCollisionManager), out var managerObj) &&
                    managerObj is IUnityCollisionManager manager)
                {
                    _manager = manager;
                }
                else if (!_loggedMissingManager)
                {
                    _loggedMissingManager = true;
                    Game.LTSLog.LogWarning("[UnityColliderObjectService] IUnityCollisionManager is not registered. UnityCollisionSystemMB profile may be missing.", _mb);
                    Debug.LogWarning("[UnityColliderObjectService] IUnityCollisionManager is not registered. UnityCollisionSystemMB profile may be missing.");
                }
            }

            if (_hitScopeRegistry == null)
            {
                if (resolver.TryResolve(typeof(IHitColliderScopeRegistry), out var registryObj) &&
                    registryObj is IHitColliderScopeRegistry registry)
                {
                    _hitScopeRegistry = registry;
                }
                else if (!_loggedMissingRegistry)
                {
                    _loggedMissingRegistry = true;
                    Game.LTSLog.LogWarning("[UnityColliderObjectService] IHitColliderScopeRegistry is not registered. UnityCollisionSystemMB may be missing.", _mb);
                    Debug.LogWarning("[UnityColliderObjectService] IHitColliderScopeRegistry is not registered. UnityCollisionSystemMB may be missing.");
                }
            }

            var ready = _manager != null && _hitScopeRegistry != null;
            _mb.SetDebugState(
                resolverAvailable: true,
                managerResolved: _manager != null,
                scopeRegistryResolved: _hitScopeRegistry != null,
                isRegistered: _handle.IsValid,
                retryCount: _retryCount,
                state: ready ? "DependenciesReady" : "DependenciesMissing",
                failureReason: ready ? string.Empty : "manager/registry unresolved");
            return ready;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _retryCount = 0;
            TryRegisterSelfColliderNow();

#if UNITY_EDITOR
            _lastObservedManagerFrameIndex = -1;
            _monitorCts?.Cancel();
            _monitorCts?.Dispose();
            _monitorCts = new CancellationTokenSource();
            MonitorManagerStateAsync(_monitorCts.Token).Forget();
#endif

            if (_handle.IsValid)
                return;

            _retryCts?.Cancel();
            _retryCts?.Dispose();
            _retryCts = new CancellationTokenSource();
            RetryRegisterAsync(_retryCts.Token).Forget();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _retryCts?.Cancel();
            _retryCts?.Dispose();
            _retryCts = null;
#if UNITY_EDITOR
            _monitorCts?.Cancel();
            _monitorCts?.Dispose();
            _monitorCts = null;
#endif
            _retryCount = 0;

            if (_manager == null || _hitScopeRegistry == null)
                return;

            if (_handle.IsValid)
            {
                _hitScopeRegistry.Unregister(_handle, _ownerScope);
                _manager.UnregisterDynamic(_handle);
                _handle = DynamicColliderHandle.Invalid;
                _mb.SetRegisteredHandle(_handle);
                if (_mb.Collider != null)
                {
                    _mb.Collider.enabled = false;
                    _mb.RecordColliderEnabledChange(false, "UnityColliderObjectService.OnRelease");
                }
                _mb.RecordRegistrationEvent("OnRelease.UnregisterDynamic");
            }

            _mb.SetDebugState(
                resolverAvailable: _ownerScope?.Resolver != null,
                managerResolved: _manager != null,
                scopeRegistryResolved: _hitScopeRegistry != null,
                isRegistered: false,
                retryCount: 0,
                state: "Released",
                failureReason: string.Empty);
            _mb.SetManagerDebugSnapshot(
                frameIndex: -1,
                lastFrameHitCount: 0,
                registeredDynamicCount: 0,
                isTicking: false);
        }

        async UniTaskVoid RetryRegisterAsync(CancellationToken ct)
        {
            for (int i = 0; i < RegisterRetryFrames; i++)
            {
                if (ct.IsCancellationRequested)
                    return;

                _retryCount = i + 1;
                TryRegisterSelfColliderNow();
                if (_handle.IsValid)
                    return;

                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            if (!ct.IsCancellationRequested)
            {
                _mb.SetDebugState(
                    resolverAvailable: _ownerScope?.Resolver != null,
                    managerResolved: _manager != null,
                    scopeRegistryResolved: _hitScopeRegistry != null,
                    isRegistered: false,
                    retryCount: _retryCount,
                    state: "RegisterFailed",
                    failureReason: $"retry timeout ({RegisterRetryFrames} frames)");
                Debug.LogWarning($"[UnityColliderObjectService] Failed to register dynamic collider after {RegisterRetryFrames} frames. object='{_mb.gameObject.name}'");
            }
        }

#if UNITY_EDITOR
        const int MonitorThrottleFrames = 30;

        async UniTaskVoid MonitorManagerStateAsync(CancellationToken ct)
        {
            // Phase offset: distribute monitors across frames to avoid synchronized burst
            int phaseOffset = Mathf.Abs(_mb.GetInstanceID()) % MonitorThrottleFrames;
            if (phaseOffset > 0)
                await UniTask.DelayFrame(phaseOffset, PlayerLoopTiming.Update, ct);

            while (!ct.IsCancellationRequested)
            {
                if (_manager != null)
                {
                    var frameIndex = _manager.DebugFrameIndex;
                    var ticking = frameIndex != _lastObservedManagerFrameIndex;
                    _lastObservedManagerFrameIndex = frameIndex;
                    _mb.SetManagerDebugSnapshot(
                        frameIndex: frameIndex,
                        lastFrameHitCount: _manager.LastFrameHitCount,
                        registeredDynamicCount: _manager.DebugRegisteredDynamicCount,
                        isTicking: ticking);
                    _mb.SetManagerSetCounts(
                        playerHurtbox: _manager.GetDebugSetCount(DynamicColliderSetId.PlayerHurtbox),
                        enemyHurtbox: _manager.GetDebugSetCount(DynamicColliderSetId.EnemyHurtbox),
                        playerBullet: _manager.GetDebugSetCount(DynamicColliderSetId.PlayerBullet),
                        enemyBullet: _manager.GetDebugSetCount(DynamicColliderSetId.EnemyBullet));
                }
                else
                {
                    _mb.SetManagerDebugSnapshot(
                        frameIndex: -1,
                        lastFrameHitCount: 0,
                        registeredDynamicCount: 0,
                        isTicking: false);
                    _mb.SetManagerSetCounts(
                        playerHurtbox: 0,
                        enemyHurtbox: 0,
                        playerBullet: 0,
                        enemyBullet: 0);
                }

                // Sleep for throttle interval instead of yielding every frame
                await UniTask.DelayFrame(MonitorThrottleFrames, PlayerLoopTiming.Update, ct);
            }
        }
#endif

        void TryRegisterSelfColliderNow()
        {
            if (!TryEnsureDependencies())
                return;

            // Preserve the collider's intended enabled state
            var col = _mb.Collider;
            var shouldBeEnabled = _mb.GetDesiredEnabledState();
            if (col != null)
            {
                col.enabled = shouldBeEnabled;
                _mb.RecordColliderEnabledChange(shouldBeEnabled, "UnityColliderObjectService.TryRegisterSelfColliderNow.Prepare");
            }

            if (_handle.IsValid)
            {
                _hitScopeRegistry!.Unregister(_handle, _ownerScope);
                _manager!.UnregisterDynamic(_handle);
                _handle = DynamicColliderHandle.Invalid;
                _mb.RecordRegistrationEvent("TryRegisterSelfColliderNow.UnregisterPreviousHandle");
            }

            col = _mb.Collider;
            if (col == null)
            {
                _mb.SetDebugState(
                    resolverAvailable: _ownerScope?.Resolver != null,
                    managerResolved: _manager != null,
                    scopeRegistryResolved: _hitScopeRegistry != null,
                    isRegistered: false,
                    retryCount: _retryCount,
                    state: "RegisterSkipped",
                    failureReason: "Collider is null");
                return;
            }

            if (!col.enabled)
            {
                _mb.SetDebugState(
                    resolverAvailable: _ownerScope?.Resolver != null,
                    managerResolved: _manager != null,
                    scopeRegistryResolved: _hitScopeRegistry != null,
                    isRegistered: false,
                    retryCount: _retryCount,
                    state: "RegisterSkipped",
                    failureReason: "Collider is disabled before RegisterDynamic");
                _mb.RecordRegistrationEvent("TryRegisterSelfColliderNow.ColliderDisabled");
                return;
            }

            var h = _manager!.RegisterDynamic(new UnityDynamicColliderDesc(
                collider: col,
                layerId: _mb.LayerId,
                hitMask: _mb.HitMask,
                setId: _mb.SetId,
                userData: _mb.UserData));

            if (!h.IsValid)
            {
                _mb.SetDebugState(
                    resolverAvailable: _ownerScope?.Resolver != null,
                    managerResolved: _manager != null,
                    scopeRegistryResolved: _hitScopeRegistry != null,
                    isRegistered: false,
                    retryCount: _retryCount,
                    state: "RegisterFailed",
                    failureReason: "IUnityCollisionManager.RegisterDynamic returned invalid handle");
                _mb.RecordRegistrationEvent("TryRegisterSelfColliderNow.RegisterDynamicInvalid");
                return;
            }

            _handle = h;
            _mb.SetRegisteredHandle(_handle);
            _hitScopeRegistry!.Register(_handle, _ownerScope);
            _mb.RecordRegistrationEvent($"TryRegisterSelfColliderNow.Registered handle={_handle.Id}:{_handle.Generation}");
            _mb.SetDebugState(
                resolverAvailable: _ownerScope?.Resolver != null,
                managerResolved: _manager != null,
                scopeRegistryResolved: _hitScopeRegistry != null,
                isRegistered: true,
                retryCount: _retryCount,
                state: "Registered",
                failureReason: string.Empty);
        }

        public void SetEnabled(bool enabled)
        {
            if (!TryEnsureDependencies())
                return;

            if (enabled)
            {
                if (_mb.Collider != null)
                {
                    _mb.Collider.enabled = true;
                    _mb.RecordColliderEnabledChange(true, "UnityColliderObjectService.SetEnabled(true)");
                }

                if (_handle.IsValid)
                    return;

                var col = _mb.Collider;
                if (col == null)
                    return;

                var h = _manager!.RegisterDynamic(new UnityDynamicColliderDesc(
                    collider: col,
                    layerId: _mb.LayerId,
                    hitMask: _mb.HitMask,
                    setId: _mb.SetId,
                    userData: _mb.UserData));

                if (!h.IsValid)
                {
                    _mb.RecordRegistrationEvent("SetEnabled(true).RegisterDynamicInvalid");
                    return;
                }

                _handle = h;
                _mb.SetRegisteredHandle(_handle);
                _hitScopeRegistry!.Register(_handle, _ownerScope);
                _mb.RecordRegistrationEvent($"SetEnabled(true).Registered handle={_handle.Id}:{_handle.Generation}");
                return;
            }

            if (_handle.IsValid)
            {
                _hitScopeRegistry!.Unregister(_handle, _ownerScope);
                _manager!.UnregisterDynamic(_handle);
                _handle = DynamicColliderHandle.Invalid;
                _mb.SetRegisteredHandle(_handle);
                _mb.RecordRegistrationEvent("SetEnabled(false).UnregisterDynamic");
            }

            if (_mb.Collider != null)
            {
                _mb.Collider.enabled = false;
                _mb.RecordColliderEnabledChange(false, "UnityColliderObjectService.SetEnabled(false)");
            }
        }

        public void SetTrigger(bool isTrigger)
        {
            if (_mb.Collider != null)
                _mb.Collider.isTrigger = isTrigger;
        }

        public void SetLayerId(int layerId)
        {
            var clamped = Mathf.Clamp(layerId, 0, 31);
            _mb.SetLayerId(clamped);

            var col = _mb.Collider;
            if (col != null)
                col.gameObject.layer = clamped;

            if (_handle.IsValid && _manager != null)
                _manager.SetLayer(_handle, clamped);
        }

        public void SetHitMask(uint hitMask)
        {
            _mb.SetHitMask(hitMask);

            if (_handle.IsValid && _manager != null)
                _manager.SetHitMask(_handle, hitMask);
        }

        public void SetSetId(DynamicColliderSetId setId)
        {
            _mb.SetSetId(setId);

            if (_handle.IsValid && _manager != null)
                _manager.SetSetId(_handle, setId);
        }

        public void SetUserData(int userData)
        {
            _mb.SetUserData(userData);

            if (_handle.IsValid && _manager != null)
                _manager.SetUserData(_handle, userData);
        }
    }
}
