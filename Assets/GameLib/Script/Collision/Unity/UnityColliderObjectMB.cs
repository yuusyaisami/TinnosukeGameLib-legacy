#nullable enable
using System;
using System.Collections.Generic;
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
        public const string DefaultColliderTag = "default";

        [Serializable]
        public sealed class UnityColliderEntry
        {
            [SerializeField] Collider2D? _collider;
            [SerializeField] string _tag = DefaultColliderTag;

            public Collider2D? Collider => _collider;
            public string Tag => NormalizeTag(_tag);

            public UnityColliderEntry()
            {
            }

            public UnityColliderEntry(Collider2D? collider, string tag)
            {
                _collider = collider;
                _tag = NormalizeTag(tag);
            }

            internal void SetCollider(Collider2D? collider)
            {
                _collider = collider;
            }

            internal void SetTag(string tag)
            {
                _tag = NormalizeTag(tag);
            }

            internal void Normalize()
            {
                _tag = NormalizeTag(_tag);
            }

            static string NormalizeTag(string? tag)
            {
                if (string.IsNullOrWhiteSpace(tag))
                    return DefaultColliderTag;

                return tag.Trim();
            }
        }

        [Header("Unity Collider")]
        [SerializeField] UnityColliderEntry[] _colliders = Array.Empty<UnityColliderEntry>();
        [SerializeField, HideInInspector] Collider2D? _collider;

        [Header("Layer")]
        [SerializeField, Range(0, 31)] int _layerId = 0;
        [SerializeField] uint _hitMask = ~0u;

        [Header("Dynamic Settings")]
        [SerializeField] DynamicColliderSetRef _setId = new(DynamicColliderSetId.EnemyBullet);

        [Header("User Data")]
        [SerializeField] int _userData = 0;

        [Header("Collider Initial State")]
        [SerializeField] bool _enabledByDefault = true;

        [NonSerialized] bool _initialColliderEnabledCaptured;
        [NonSerialized] Dictionary<int, bool>? _initialColliderEnabledByInstanceId;
        [NonSerialized] readonly List<Collider2D> _initialStateCaptureColliders = new(8);
        [NonSerialized] readonly List<string> _initialStateCaptureTags = new(8);

        [Header("Runtime (ReadOnly)")]
        [SerializeField, ReadOnly] DynamicColliderHandle _dynamicHandle;
        [SerializeField, ReadOnly] int _debugRegisteredHandleCount;
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

        public Collider2D? Collider
        {
            get
            {
                EnsureColliderEntries();

                for (var i = 0; i < _colliders.Length; i++)
                {
                    var entry = _colliders[i];
                    if (entry?.Collider != null)
                        return entry.Collider;
                }

                return GetFallbackCollider();
            }
        }

        public int ColliderCount
        {
            get
            {
                EnsureColliderEntries();

                var count = 0;
                for (var i = 0; i < _colliders.Length; i++)
                {
                    if (_colliders[i]?.Collider != null)
                        count++;
                }

                if (count > 0)
                    return count;

                return GetFallbackCollider() != null ? 1 : 0;
            }
        }

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
            if (!handle.IsValid)
                _debugRegisteredHandleCount = 0;
            else if (_debugRegisteredHandleCount <= 0)
                _debugRegisteredHandleCount = 1;
        }

        internal void SetRegisteredHandles(List<DynamicColliderHandle> handles)
        {
            if (handles == null || handles.Count == 0)
            {
                _dynamicHandle = DynamicColliderHandle.Invalid;
                _debugRegisteredHandleCount = 0;
                _debugIsRegistered = false;
                return;
            }

            _dynamicHandle = handles[0];
            _debugRegisteredHandleCount = handles.Count;
            _debugIsRegistered = _dynamicHandle.IsValid;
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
            EnsureColliderEntries();

            if (collider == null)
                return;

            if (_colliders.Length == 0)
            {
                _colliders = new[] { new UnityColliderEntry(collider, DefaultColliderTag) };
                return;
            }

            var entry = _colliders[0] ?? new UnityColliderEntry();
            entry.SetCollider(collider);
            entry.SetTag(DefaultColliderTag);
            _colliders[0] = entry;
        }

        internal bool ReplaceCollider(Collider2D previous, Collider2D replacement, string tag)
        {
            if (previous == null || replacement == null)
                return false;

            EnsureColliderEntries();

            var normalizedTag = NormalizeTag(tag);
            var replaced = false;
            for (var i = 0; i < _colliders.Length; i++)
            {
                var entry = _colliders[i];
                if (entry == null || !ReferenceEquals(entry.Collider, previous))
                    continue;

                entry.SetCollider(replacement);
                entry.SetTag(normalizedTag);
                replaced = true;
            }

            if (_collider != null && ReferenceEquals(_collider, previous))
                _collider = replacement;

            if (!replaced && _colliders.Length == 0)
            {
                _colliders = new[] { new UnityColliderEntry(replacement, normalizedTag) };
                return true;
            }

            return replaced;
        }

        public int FillConfiguredColliders(List<Collider2D> colliders, List<string> tags)
        {
            if (colliders == null || tags == null)
                return 0;

            colliders.Clear();
            tags.Clear();

            EnsureColliderEntries();

            for (var i = 0; i < _colliders.Length; i++)
            {
                var entry = _colliders[i];
                var collider = entry?.Collider;
                if (collider == null)
                    continue;

                if (ContainsReference(colliders, collider))
                    continue;

                colliders.Add(collider);
                tags.Add(entry?.Tag ?? DefaultColliderTag);
            }

            if (colliders.Count > 0)
                return colliders.Count;

            var fallback = GetFallbackCollider();
            if (fallback != null)
            {
                colliders.Add(fallback);
                tags.Add(DefaultColliderTag);
            }

            return colliders.Count;
        }

        public bool TryGetTagForCollider(Collider2D? collider, out string tag)
        {
            tag = DefaultColliderTag;

            if (collider == null)
                return false;

            EnsureColliderEntries();
            for (var i = 0; i < _colliders.Length; i++)
            {
                var entry = _colliders[i];
                if (entry == null || !ReferenceEquals(entry.Collider, collider))
                    continue;

                tag = entry.Tag;
                return true;
            }

            var fallback = GetFallbackCollider();
            if (fallback != null && ReferenceEquals(fallback, collider))
            {
                tag = DefaultColliderTag;
                return true;
            }

            return false;
        }

        internal string GetTagOrDefault(Collider2D? collider)
        {
            return TryGetTagForCollider(collider, out var tag) ? tag : DefaultColliderTag;
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

        internal bool GetDesiredEnabledState(Collider2D? collider)
        {
            if (collider == null)
                return false;

            if (!_initialColliderEnabledCaptured)
                CaptureInitialColliderEnabledState();

            if (_initialColliderEnabledByInstanceId != null &&
                _initialColliderEnabledByInstanceId.TryGetValue(collider.GetInstanceID(), out var initialEnabled))
            {
                // Keep colliders that were initially disabled as disabled at registration time.
                if (!initialEnabled)
                    return false;

                if (_enabledByDefault)
                    return true;

                return true;
            }

            if (_enabledByDefault)
                return true;

            return collider.enabled;
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
            EnsureColliderEntries();
            CaptureInitialColliderEnabledState();
            _layerId = Mathf.Clamp(_layerId, 0, 31);
        }

        void Awake()
        {
            EnsureColliderEntries();
            CaptureInitialColliderEnabledState();
        }

        void CaptureInitialColliderEnabledState()
        {
            _initialColliderEnabledByInstanceId ??= new Dictionary<int, bool>(8);
            _initialColliderEnabledByInstanceId.Clear();

            _initialStateCaptureColliders.Clear();
            _initialStateCaptureTags.Clear();
            FillConfiguredColliders(_initialStateCaptureColliders, _initialStateCaptureTags);

            for (var i = 0; i < _initialStateCaptureColliders.Count; i++)
            {
                var collider = _initialStateCaptureColliders[i];
                if (collider == null)
                    continue;

                _initialColliderEnabledByInstanceId[collider.GetInstanceID()] = collider.enabled;
            }

            _initialColliderEnabledCaptured = _initialColliderEnabledByInstanceId.Count > 0;
            if (_initialStateCaptureColliders.Count > 0 && _initialStateCaptureColliders[0] != null)
                RecordColliderEnabledChange(_initialStateCaptureColliders[0].enabled, "CaptureInitialColliderEnabledState");
        }

        void EnsureColliderEntries()
        {
            _colliders ??= Array.Empty<UnityColliderEntry>();

            if (_colliders.Length == 0 && _collider != null)
                _colliders = new[] { new UnityColliderEntry(_collider, DefaultColliderTag) };

            for (var i = 0; i < _colliders.Length; i++)
            {
                var entry = _colliders[i];
                if (entry == null)
                {
                    _colliders[i] = new UnityColliderEntry();
                    continue;
                }

                entry.Normalize();
            }
        }

        Collider2D? GetFallbackCollider()
        {
            return _collider != null ? _collider : GetComponent<Collider2D>();
        }

        static string NormalizeTag(string? tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return DefaultColliderTag;

            return tag.Trim();
        }

        static bool ContainsReference(List<Collider2D> colliders, Collider2D candidate)
        {
            for (var i = 0; i < colliders.Count; i++)
            {
                if (ReferenceEquals(colliders[i], candidate))
                    return true;
            }

            return false;
        }
    }
}
