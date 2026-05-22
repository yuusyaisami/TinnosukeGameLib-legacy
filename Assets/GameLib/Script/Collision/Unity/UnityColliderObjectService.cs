#nullable enable
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using UnityEngine;

namespace Game.Collision
{
    /// <summary>
    /// UnityColliderObjectMB の実行時ロジック。
    /// - IUnityCollisionManager へ複数 Collider2D を登録/解除
    /// - IHitColliderScopeRegistry へ handle->scope を登録
    /// </summary>
    public sealed class UnityColliderObjectService :
        IScopeAcquireHandler,
        IScopeReleaseHandler
    {
        sealed class RegisteredCollider
        {
            public Collider2D Collider = null!;
            public DynamicColliderHandle Handle;
            public string Tag = UnityColliderObjectMB.DefaultColliderTag;
        }

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

        readonly List<RegisteredCollider> _registered = new(8);
        readonly List<Collider2D> _configuredColliders = new(8);
        readonly List<string> _configuredTags = new(8);
        readonly List<DynamicColliderHandle> _registeredHandlesScratch = new(8);

        int _retryCount;
        DynamicColliderHandle _primaryHandle;

        const int RegisterRetryFrames = 60;

        public bool IsEnabled => _registered.Count > 0;
        public DynamicColliderHandle DynamicHandle => _primaryHandle;

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
                    isRegistered: _registered.Count > 0,
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
                    isRegistered: _registered.Count > 0,
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
                    Debug.LogWarning("[UnityColliderObjectService] IUnityCollisionManager is not registered. UnityCollisionSystemMB profile may be missing.", _mb);
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
                    Debug.LogWarning("[UnityColliderObjectService] IHitColliderScopeRegistry is not registered. UnityCollisionSystemMB may be missing.", _mb);
                    Debug.LogWarning("[UnityColliderObjectService] IHitColliderScopeRegistry is not registered. UnityCollisionSystemMB may be missing.");
                }
            }

            var ready = _manager != null && _hitScopeRegistry != null;
            _mb.SetDebugState(
                resolverAvailable: true,
                managerResolved: _manager != null,
                scopeRegistryResolved: _hitScopeRegistry != null,
                isRegistered: _registered.Count > 0,
                retryCount: _retryCount,
                state: ready ? "DependenciesReady" : "DependenciesMissing",
                failureReason: ready ? string.Empty : "manager/registry unresolved");
            return ready;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _retryCount = 0;
            TryRegisterConfiguredCollidersNow();

#if UNITY_EDITOR
            _lastObservedManagerFrameIndex = -1;
            _monitorCts?.Cancel();
            _monitorCts?.Dispose();
            _monitorCts = new CancellationTokenSource();
            MonitorManagerStateAsync(_monitorCts.Token).Forget();
#endif

            if (_registered.Count > 0)
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

            DisableAllConfiguredColliders("UnityColliderObjectService.OnRelease");
            UnregisterAll("OnRelease.UnregisterDynamic");

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
            for (var i = 0; i < RegisterRetryFrames; i++)
            {
                if (ct.IsCancellationRequested)
                    return;

                _retryCount = i + 1;
                TryRegisterConfiguredCollidersNow();
                if (_registered.Count > 0)
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
                Debug.LogWarning($"[UnityColliderObjectService] Failed to register dynamic colliders after {RegisterRetryFrames} frames. object='{_mb.gameObject.name}'");
            }
        }

#if UNITY_EDITOR
        const int MonitorThrottleFrames = 30;

        async UniTaskVoid MonitorManagerStateAsync(CancellationToken ct)
        {
            var phaseOffset = Mathf.Abs(_mb.GetInstanceID()) % MonitorThrottleFrames;
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

                await UniTask.DelayFrame(MonitorThrottleFrames, PlayerLoopTiming.Update, ct);
            }
        }
#endif

        void TryRegisterConfiguredCollidersNow()
        {
            if (!TryEnsureDependencies())
                return;

            UnregisterAll("TryRegisterConfiguredCollidersNow.UnregisterPreviousHandles");

            _mb.FillConfiguredColliders(_configuredColliders, _configuredTags);
            if (_configuredColliders.Count == 0)
            {
                _mb.SetDebugState(
                    resolverAvailable: _ownerScope?.Resolver != null,
                    managerResolved: _manager != null,
                    scopeRegistryResolved: _hitScopeRegistry != null,
                    isRegistered: false,
                    retryCount: _retryCount,
                    state: "RegisterSkipped",
                    failureReason: "No configured colliders");
                return;
            }

            var anyRegistered = false;
            for (var i = 0; i < _configuredColliders.Count; i++)
            {
                var collider = _configuredColliders[i];
                if (collider == null)
                    continue;

                var shouldBeEnabled = _mb.GetDesiredEnabledState(collider);
                collider.enabled = shouldBeEnabled;
                _mb.RecordColliderEnabledChange(shouldBeEnabled, "UnityColliderObjectService.TryRegisterConfiguredCollidersNow.Prepare");
                if (!shouldBeEnabled)
                    continue;

                var tag = NormalizeTag(_configuredTags[i]);
                if (TryRegisterColliderInternal(collider, tag, out _))
                    anyRegistered = true;
            }

            SyncRegisteredHandleDebug();

            if (!anyRegistered)
            {
                _mb.SetDebugState(
                    resolverAvailable: _ownerScope?.Resolver != null,
                    managerResolved: _manager != null,
                    scopeRegistryResolved: _hitScopeRegistry != null,
                    isRegistered: false,
                    retryCount: _retryCount,
                    state: "RegisterSkipped",
                    failureReason: "All configured colliders are disabled or registration failed");
                _mb.RecordRegistrationEvent("TryRegisterConfiguredCollidersNow.NoEnabledCollider");
                return;
            }

            _mb.RecordRegistrationEvent($"TryRegisterConfiguredCollidersNow.Registered count={_registered.Count}");
            _mb.SetDebugState(
                resolverAvailable: _ownerScope?.Resolver != null,
                managerResolved: _manager != null,
                scopeRegistryResolved: _hitScopeRegistry != null,
                isRegistered: true,
                retryCount: _retryCount,
                state: "Registered",
                failureReason: string.Empty);
        }

        public int FillRegisteredHandles(List<DynamicColliderHandle> destination)
        {
            if (destination == null)
                return 0;

            destination.Clear();
            for (var i = 0; i < _registered.Count; i++)
            {
                var handle = _registered[i].Handle;
                if (handle.IsValid)
                    destination.Add(handle);
            }

            return destination.Count;
        }

        public void NotifyColliderReplaced(Collider2D previous, Collider2D current, string colliderTag)
        {
            if (!TryEnsureDependencies())
                return;

            if (previous != null && !ReferenceEquals(previous, current))
                UnregisterCollider(previous, "NotifyColliderReplaced.UnregisterPrevious", updateDebug: false);

            if (current != null)
            {
                if (current.enabled)
                    EnsureColliderRegistered(current, NormalizeTag(colliderTag), "NotifyColliderReplaced.RegisterCurrent", updateDebug: false);
                else
                    UnregisterCollider(current, "NotifyColliderReplaced.CurrentDisabled", updateDebug: false);
            }

            SyncRegisteredHandleDebug();
        }

        public void SyncColliderRegistration(Collider2D collider, string colliderTag)
        {
            if (collider == null)
                return;
            if (!TryEnsureDependencies())
                return;

            if (collider.enabled)
            {
                EnsureColliderRegistered(collider, NormalizeTag(colliderTag), "SyncColliderRegistration.RegisterOrUpdate");
                return;
            }

            UnregisterCollider(collider, "SyncColliderRegistration.Unregister");
        }

        public void SetEnabled(bool enabled)
        {
            if (!TryEnsureDependencies())
                return;

            _mb.FillConfiguredColliders(_configuredColliders, _configuredTags);
            for (var i = 0; i < _configuredColliders.Count; i++)
            {
                var collider = _configuredColliders[i];
                if (collider == null)
                    continue;

                collider.enabled = enabled;
                _mb.RecordColliderEnabledChange(enabled, enabled
                    ? "UnityColliderObjectService.SetEnabled(true)"
                    : "UnityColliderObjectService.SetEnabled(false)");

                var tag = NormalizeTag(_configuredTags[i]);
                if (enabled)
                    EnsureColliderRegistered(collider, tag, "SetEnabled(true).Registered", updateDebug: false);
                else
                    UnregisterCollider(collider, "SetEnabled(false).Unregister", updateDebug: false);
            }

            SyncRegisteredHandleDebug();
        }

        public void SetEnabled(Collider2D collider, bool enabled, string colliderTag)
        {
            if (collider == null)
                return;
            if (!TryEnsureDependencies())
                return;

            collider.enabled = enabled;
            _mb.RecordColliderEnabledChange(enabled, enabled
                ? "UnityColliderObjectService.SetEnabled(target=true)"
                : "UnityColliderObjectService.SetEnabled(target=false)");

            if (enabled)
            {
                EnsureColliderRegistered(collider, NormalizeTag(colliderTag), "SetEnabled(target).Registered");
                return;
            }

            UnregisterCollider(collider, "SetEnabled(target).Unregister");
        }

        public void SetTrigger(bool isTrigger)
        {
            _mb.FillConfiguredColliders(_configuredColliders, _configuredTags);
            for (var i = 0; i < _configuredColliders.Count; i++)
            {
                var collider = _configuredColliders[i];
                if (collider != null)
                    collider.isTrigger = isTrigger;
            }
        }

        public void SetTrigger(Collider2D collider, bool isTrigger)
        {
            if (collider == null)
                return;

            collider.isTrigger = isTrigger;
        }

        public void SetLayerId(int layerId)
        {
            var clamped = Mathf.Clamp(layerId, 0, 31);
            _mb.SetLayerId(clamped);

            _mb.FillConfiguredColliders(_configuredColliders, _configuredTags);
            for (var i = 0; i < _configuredColliders.Count; i++)
            {
                var collider = _configuredColliders[i];
                if (collider != null)
                    collider.gameObject.layer = clamped;
            }

            if (_manager == null)
                return;

            for (var i = 0; i < _registered.Count; i++)
                _manager.SetLayer(_registered[i].Handle, clamped);
        }

        public void SetHitMask(uint hitMask)
        {
            _mb.SetHitMask(hitMask);

            if (_manager == null)
                return;

            for (var i = 0; i < _registered.Count; i++)
                _manager.SetHitMask(_registered[i].Handle, hitMask);
        }

        public void SetSetId(DynamicColliderSetId setId)
        {
            _mb.SetSetId(setId);

            if (_manager == null)
                return;

            for (var i = 0; i < _registered.Count; i++)
                _manager.SetSetId(_registered[i].Handle, setId);
        }

        public void SetUserData(int userData)
        {
            _mb.SetUserData(userData);

            if (_manager == null)
                return;

            for (var i = 0; i < _registered.Count; i++)
                _manager.SetUserData(_registered[i].Handle, userData);
        }

        bool TryRegisterColliderInternal(Collider2D collider, string colliderTag, out DynamicColliderHandle handle)
        {
            handle = DynamicColliderHandle.Invalid;
            if (collider == null || _manager == null || _hitScopeRegistry == null)
                return false;

            var normalizedTag = NormalizeTag(colliderTag);
            if (TryGetRegisteredIndex(collider, out var existingIndex))
            {
                var existing = _registered[existingIndex];
                existing.Tag = normalizedTag;
                ApplyManagerMetadata(existing);
                handle = existing.Handle;
                _registered[existingIndex] = existing;
                return handle.IsValid;
            }

            var registeredHandle = _manager.RegisterDynamic(new UnityDynamicColliderDesc(
                collider: collider,
                layerId: _mb.LayerId,
                hitMask: _mb.HitMask,
                setId: _mb.SetId,
                userData: _mb.UserData,
                colliderTag: normalizedTag));

            if (!registeredHandle.IsValid)
                return false;

            _hitScopeRegistry.Register(registeredHandle, _ownerScope);

            var entry = new RegisteredCollider
            {
                Collider = collider,
                Handle = registeredHandle,
                Tag = normalizedTag,
            };
            _registered.Add(entry);
            ApplyManagerMetadata(entry);

            handle = registeredHandle;
            return true;
        }

        void EnsureColliderRegistered(Collider2D collider, string colliderTag, string reason, bool updateDebug = true)
        {
            if (collider == null)
                return;

            if (TryRegisterColliderInternal(collider, colliderTag, out var registeredHandle) && registeredHandle.IsValid)
            {
                _mb.RecordRegistrationEvent($"{reason} handle={registeredHandle.Id}:{registeredHandle.Generation}");
            }
            else
            {
                _mb.RecordRegistrationEvent($"{reason}.RegisterDynamicInvalid");
            }

            if (updateDebug)
                SyncRegisteredHandleDebug();
        }

        bool UnregisterCollider(Collider2D collider, string reason, bool updateDebug = true)
        {
            if (collider == null)
                return false;
            if (!TryGetRegisteredIndex(collider, out var index))
                return false;

            UnregisterAt(index, reason, updateDebug);
            return true;
        }

        void UnregisterAt(int index, string reason, bool updateDebug)
        {
            if (index < 0 || index >= _registered.Count)
                return;

            var entry = _registered[index];
            _registered.RemoveAt(index);

            if (_hitScopeRegistry != null && entry.Handle.IsValid)
                _hitScopeRegistry.Unregister(entry.Handle, _ownerScope);

            if (_manager != null && entry.Handle.IsValid)
                _manager.UnregisterDynamic(entry.Handle);

            _mb.RecordRegistrationEvent(reason);
            if (updateDebug)
                SyncRegisteredHandleDebug();
        }

        void UnregisterAll(string reason)
        {
            if (_registered.Count == 0)
            {
                SyncRegisteredHandleDebug();
                return;
            }

            for (var i = _registered.Count - 1; i >= 0; i--)
                UnregisterAt(i, reason, updateDebug: false);

            SyncRegisteredHandleDebug();
        }

        void DisableAllConfiguredColliders(string reason)
        {
            _mb.FillConfiguredColliders(_configuredColliders, _configuredTags);
            for (var i = 0; i < _configuredColliders.Count; i++)
            {
                var collider = _configuredColliders[i];
                if (collider == null)
                    continue;

                collider.enabled = false;
                _mb.RecordColliderEnabledChange(false, reason);
            }
        }

        void ApplyManagerMetadata(RegisteredCollider entry)
        {
            if (_manager == null || !entry.Handle.IsValid)
                return;

            var clampedLayer = Mathf.Clamp(_mb.LayerId, 0, 31);
            entry.Collider.gameObject.layer = clampedLayer;

            _manager.SetLayer(entry.Handle, clampedLayer);
            _manager.SetHitMask(entry.Handle, _mb.HitMask);
            _manager.SetSetId(entry.Handle, _mb.SetId);
            _manager.SetUserData(entry.Handle, _mb.UserData);
            _manager.SetColliderTag(entry.Handle, entry.Tag);
        }

        bool TryGetRegisteredIndex(Collider2D collider, out int index)
        {
            index = -1;
            for (var i = 0; i < _registered.Count; i++)
            {
                if (!ReferenceEquals(_registered[i].Collider, collider))
                    continue;

                index = i;
                return true;
            }

            return false;
        }

        void SyncRegisteredHandleDebug()
        {
            _registeredHandlesScratch.Clear();
            _primaryHandle = DynamicColliderHandle.Invalid;

            for (var i = 0; i < _registered.Count; i++)
            {
                var handle = _registered[i].Handle;
                if (!handle.IsValid)
                    continue;

                if (!_primaryHandle.IsValid)
                    _primaryHandle = handle;

                _registeredHandlesScratch.Add(handle);
            }

            _mb.SetRegisteredHandles(_registeredHandlesScratch);
        }

        static string NormalizeTag(string? tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return UnityColliderObjectMB.DefaultColliderTag;

            return tag.Trim();
        }
    }
}
