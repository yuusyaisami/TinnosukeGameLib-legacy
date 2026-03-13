// Game.Common.SyncEventService.cs
//
// Sync-first Event System for high-frequency game events (Collision, etc.)
// Main-thread-only. No locks.
//
// Design:
// - EventId (int) for fastest lookup — string keys as legacy fallback
//   (SO で StableKey(string) を定義 → コード生成で EventId(int) に変換)
// - SyncEventHandler<T>(in T) delegate for large struct payloads (zero copy)
// - Release builds skip try/catch for maximum performance
// - CollisionSystem などのホットパスは typed API のみ使用、EventPayload はレガシー用途に限定
// - Unsubscribe removes ALL matching handlers
// - UNITY_ASSERTIONS で Release でも軽いメインスレッドガード有効

using System;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Game;

namespace Game.Common
{
    /// <summary>
    /// Delegate for typed event handlers with in parameter (no copy for large structs).
    /// </summary>
    // SyncEventHandler<T> is defined elsewhere in Game.Common — avoid duplicate declaration here.

    [Flags]
    public enum EventPayloadPolicy
    {
        None = 0,
        AllowPublish = 1 << 0,
        AllowExtras = 1 << 1,
        Default = AllowPublish | AllowExtras,
    }

    // EventExceptionPolicy is intentionally not defined here to avoid duplicate definitions;
    // the enum is defined elsewhere in the Game.Common namespace and is used throughout this file.

    /// <summary>
    /// High-performance sync event interface (Project scope only).
    /// Main-thread-only. Zero GC on hot paths（Subscribe 系はインストール時のみなので IDisposable 生成を許容）。
    /// </summary>
    public interface ISyncEventService
    {
        // ========== Legacy EventPayload API ==========
        IDisposable SubscribeSync(string key, Action<EventPayload> handler);
        void UnsubscribeSync(string key, Action<EventPayload> handler);
        void PublishSync(string key, EventPayload payload);

        // ========== Typed API (int key, in T) — Recommended ==========
        /// <summary>Subscribe with int key and in-parameter handler (zero copy).</summary>
        IDisposable SubscribeSync<T>(int key, SyncEventHandler<T> handler);
        /// <summary>Publish with int key. Returns number of handlers invoked.</summary>
        int PublishSync<T>(int key, in T payload);

        // ========== Typed API (string key) — Legacy ==========
        IDisposable SubscribeSync<T>(string key, SyncEventHandler<T> handler);
        int PublishSync<T>(string key, in T payload);

        void Clear(int key);
        void Clear(string key);
        void ClearSubscribers();
        int GetSubscriberCount(int key);
        int GetSubscriberCount(string key);
        void SetExceptionPolicy(int key, EventExceptionPolicy policy);
        void SetExceptionPolicy(string key, EventExceptionPolicy policy);
        void SetPayloadPolicy(int key, EventPayloadPolicy policy);
        void SetPayloadPolicy(string key, EventPayloadPolicy policy);
        void SetTypedPayloadType<T>(int key);
        void SetTypedPayloadType<T>(string key);
        void BindStableKey(int key, string stableKey);
    }

    /// <summary>
    /// Sync-first EventService implementation.
    /// Main-thread-only. No locks.
    /// </summary>
    public sealed class SyncEventService : ISyncEventService
    {
        // --- ITypedBucket for reflection-free count ---
        interface ITypedBucket
        {
            int Count { get; }
            bool IsEmpty { get; }
        }

        sealed class TypedBucket<T> : ITypedBucket
        {
            public readonly List<SyncEventHandler<T>> List = new();
            public SyncEventHandler<T>[] Snapshot;
            public int ListVersion;
            public int SnapshotVersion;

            public int Count => List.Count;
            public bool IsEmpty => List.Count == 0;
        }

        sealed class HandlerBucket
        {
            public readonly List<Action<EventPayload>> SyncHandlers = new();
            public Action<EventPayload>[] SyncSnapshot;
            public int SyncSnapshotVersion;
            public int SyncListVersion;

            Type _singleTypedType;
            ITypedBucket _singleTypedBucket;
            Dictionary<Type, ITypedBucket> _typedBuckets;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            bool _missingFixedTypeLogged;
#endif

            public EventExceptionPolicy ExceptionPolicy = DefaultExceptionPolicy;
            public EventPayloadPolicy PayloadPolicy = DefaultPayloadPolicy;
            public Type FixedTypedPayloadType;

            public TypedBucket<T> GetOrCreateTypedBucket<T>()
            {
                var type = typeof(T);
                if (_singleTypedBucket == null)
                {
                    var tb = new TypedBucket<T>();
                    _singleTypedBucket = tb;
                    _singleTypedType = type;
                    return tb;
                }

                if (_singleTypedType == type)
                    return (TypedBucket<T>)_singleTypedBucket;

                var map = EnsureTypedBucketDictionary();
                if (map.TryGetValue(type, out var existing))
                    return (TypedBucket<T>)existing;

                var bucket = new TypedBucket<T>();
                map[type] = bucket;
                return bucket;
            }

            public bool TryGetTypedBucket(Type type, out ITypedBucket bucket)
            {
                if (_singleTypedBucket != null && _singleTypedType == type)
                {
                    bucket = _singleTypedBucket;
                    return true;
                }

                if (_typedBuckets != null && _typedBuckets.TryGetValue(type, out var existing))
                {
                    bucket = existing;
                    return true;
                }

                bucket = null;
                return false;
            }

            public void RemoveTypedBucket(Type type)
            {
                if (_singleTypedBucket != null && _singleTypedType == type)
                {
                    _singleTypedBucket = null;
                    _singleTypedType = null;
                    return;
                }

                _typedBuckets?.Remove(type);
                if (_typedBuckets != null && _typedBuckets.Count == 0)
                    _typedBuckets = null;
            }

            public bool HasTypedBuckets => _singleTypedBucket != null || (_typedBuckets != null && _typedBuckets.Count > 0);

            public IEnumerable<ITypedBucket> EnumerateTypedBuckets()
            {
                if (_singleTypedBucket != null)
                    yield return _singleTypedBucket;
                if (_typedBuckets == null) yield break;
                foreach (var kv in _typedBuckets)
                    yield return kv.Value;
            }

            Dictionary<Type, ITypedBucket> EnsureTypedBucketDictionary()
            {
                if (_typedBuckets == null)
                {
                    _typedBuckets = new Dictionary<Type, ITypedBucket>();
                    if (_singleTypedBucket != null)
                    {
                        _typedBuckets[_singleTypedType] = _singleTypedBucket;
                        _singleTypedBucket = null;
                        _singleTypedType = null;
                    }
                }
                return _typedBuckets;
            }

            public void EnsureTypedType(Type type, string keyLabel)
            {
                if (type == null) return;
                if (FixedTypedPayloadType == null)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (!_missingFixedTypeLogged)
                    {
                        _missingFixedTypeLogged = true;
                        Debug.LogError($"Event '{keyLabel}' has no typed payload lock. Call ISyncEventService.SetTypedPayloadType before subscribing/publishing.");
                    }
#endif
                    return;
                }
                if (FixedTypedPayloadType != type)
                    throw new InvalidOperationException($"Event '{keyLabel}' expects payload type {FixedTypedPayloadType.Name} but received {type.Name}.");
            }

            public void SetFixedTypedType(Type type, string keyLabel)
            {
                if (type == null) return;
                if (FixedTypedPayloadType != null && FixedTypedPayloadType != type)
                    throw new InvalidOperationException($"Event '{keyLabel}' already locked to payload type {FixedTypedPayloadType.Name}; cannot override with {type.Name}.");
                FixedTypedPayloadType = type;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                _missingFixedTypeLogged = false;
#endif
            }
        }

        const EventExceptionPolicy DefaultExceptionPolicy = EventExceptionPolicy.CatchAndLog;
        const EventPayloadPolicy DefaultPayloadPolicy = EventPayloadPolicy.Default;

        // Primary: int key buckets (fastest)
        readonly List<HandlerBucket> _intBuckets = new();
        readonly Dictionary<int, EventExceptionPolicy> _intExceptionPolicies = new();
        readonly Dictionary<int, EventPayloadPolicy> _intPayloadPolicies = new();
        readonly Dictionary<int, Type> _intFixedTypes = new();
        readonly Dictionary<int, string> _intStableKeys = new();
        // Legacy: string key buckets
        readonly Dictionary<string, HandlerBucket> _stringBuckets = new(StringComparer.Ordinal);
        readonly Dictionary<string, EventExceptionPolicy> _stringExceptionPolicies = new(StringComparer.Ordinal);
        readonly Dictionary<string, EventPayloadPolicy> _stringPayloadPolicies = new(StringComparer.Ordinal);
        readonly Dictionary<string, Type> _stringFixedTypes = new(StringComparer.Ordinal);
        readonly Dictionary<string, int> _stableKeyToInt = new(StringComparer.Ordinal);

        // ========== EventPayload-based (legacy string key) ==========

        public IDisposable SubscribeSync(string key, Action<EventPayload> handler)
        {
            MainThread.AssertMainThread();
            ValidateKey(key);
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            EnsureStringKeyNotBound(key, "EventPayload SubscribeSync");

            var bucket = GetOrCreateBucket(key);
            if (!AllowsPayload(bucket.PayloadPolicy))
                throw new InvalidOperationException($"EventPayload API is disabled for key '{key}'. Use typed publish instead.");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (bucket.SyncHandlers.Contains(handler))
            {
                Debug.LogWarning($"Duplicate EventPayload handler detected for key '{key}'.");
            }
#endif
            bucket.SyncHandlers.Add(handler);
            bucket.SyncListVersion++;
            return new SyncSubscription(this, key, handler);
        }

        public void UnsubscribeSync(string key, Action<EventPayload> handler)
        {
            MainThread.AssertMainThread();
            if (string.IsNullOrEmpty(key) || handler == null) return;
            EnsureStringKeyNotBound(key, "EventPayload UnsubscribeSync");
            if (!_stringBuckets.TryGetValue(key, out var bucket)) return;

            bool changed = false;
            for (int i = bucket.SyncHandlers.Count - 1; i >= 0; i--)
            {
                if (bucket.SyncHandlers[i] == handler)
                {
                    bucket.SyncHandlers.RemoveAt(i);
                    changed = true;
                }
            }
            if (changed) bucket.SyncListVersion++;
            TryRemoveEmptyBucket(key, bucket);
        }

        public void PublishSync(string key, EventPayload payload)
        {
            MainThread.AssertMainThread();
            if (string.IsNullOrEmpty(key)) return;
            EnsureStringKeyNotBound(key, "EventPayload PublishSync");
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));
            if (!_stringBuckets.TryGetValue(key, out var bucket)) return;

            if (!AllowsPayload(bucket.PayloadPolicy))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                throw new InvalidOperationException($"EventPayload API is disabled for key '{key}'.");
#else
                return;
#endif
            }

            var snapshot = GetSyncSnapshot(bucket);
            if (snapshot == null || snapshot.Length == 0) return;
            bool catchExceptions = ShouldCatchExceptions(bucket.ExceptionPolicy);
            payload.SetExtrasAllowed(AllowsExtras(bucket.PayloadPolicy));

            for (int i = 0; i < snapshot.Length; i++)
            {
                InvokePayloadHandler(snapshot[i], payload, catchExceptions);
            }
        }

        // ========== Typed API (int key) — Primary ==========

        public IDisposable SubscribeSync<T>(int key, SyncEventHandler<T> handler)
        {
            MainThread.AssertMainThread();
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            var bucket = GetOrCreateBucket(key);
            bucket.EnsureTypedType(typeof(T), GetKeyLabel(key));
            var typedBucket = GetOrCreateTypedBucket<T>(bucket);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (typedBucket.List.Contains(handler))
            {
                Debug.LogWarning($"Duplicate typed handler detected for int key {key} ({typeof(T).Name}).");
            }
#endif
            typedBucket.List.Add(handler);
            typedBucket.ListVersion++;
            return new TypedIntSubscription<T>(this, key, handler);
        }

        public int PublishSync<T>(int key, in T payload)
        {
            MainThread.AssertMainThread();
            var bucket = GetBucketIfExists(key);
            if (bucket == null) return 0;
            bucket.EnsureTypedType(typeof(T), GetKeyLabel(key));

            var snapshot = GetTypedSnapshot<T>(bucket);
            if (snapshot == null || snapshot.Length == 0) return 0;
            bool catchExceptions = ShouldCatchExceptions(bucket.ExceptionPolicy);

            for (int i = 0; i < snapshot.Length; i++)
            {
                InvokeTypedHandler(snapshot[i], in payload, catchExceptions);
            }
            return snapshot.Length;
        }

        void UnsubscribeTyped<T>(int key, SyncEventHandler<T> handler)
        {
            MainThread.AssertMainThread();
            if (handler == null) return;
            var bucket = GetBucketIfExists(key);
            if (bucket == null) return;
            if (!bucket.TryGetTypedBucket(typeof(T), out var itb)) return;
            if (itb is not TypedBucket<T> typed) return;

            bool changed = false;
            for (int i = typed.List.Count - 1; i >= 0; i--)
            {
                if (typed.List[i] == handler)
                {
                    typed.List.RemoveAt(i);
                    changed = true;
                }
            }
            if (changed) typed.ListVersion++;

            if (typed.IsEmpty)
                bucket.RemoveTypedBucket(typeof(T));
            TryRemoveEmptyBucket(key, bucket);
        }

        // ========== Typed API (string key) — Legacy ==========

        public IDisposable SubscribeSync<T>(string key, SyncEventHandler<T> handler)
        {
            MainThread.AssertMainThread();
            ValidateKey(key);
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            EnsureStringKeyNotBound(key, "Typed SubscribeSync");

            var bucket = GetOrCreateBucket(key);
            bucket.EnsureTypedType(typeof(T), GetKeyLabel(key));
            var typedBucket = GetOrCreateTypedBucket<T>(bucket);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (typedBucket.List.Contains(handler))
            {
                Debug.LogWarning($"Duplicate typed handler detected for key '{key}' ({typeof(T).Name}).");
            }
#endif
            typedBucket.List.Add(handler);
            typedBucket.ListVersion++;
            return new TypedStringSubscription<T>(this, key, handler);
        }

        public int PublishSync<T>(string key, in T payload)
        {
            MainThread.AssertMainThread();
            if (string.IsNullOrEmpty(key)) return 0;
            EnsureStringKeyNotBound(key, "Typed PublishSync");
            if (!_stringBuckets.TryGetValue(key, out var bucket)) return 0;
            bucket.EnsureTypedType(typeof(T), GetKeyLabel(key));

            var snapshot = GetTypedSnapshot<T>(bucket);
            if (snapshot == null || snapshot.Length == 0) return 0;
            bool catchExceptions = ShouldCatchExceptions(bucket.ExceptionPolicy);

            for (int i = 0; i < snapshot.Length; i++)
            {
                InvokeTypedHandler(snapshot[i], in payload, catchExceptions);
            }
            return snapshot.Length;
        }

        void UnsubscribeTyped<T>(string key, SyncEventHandler<T> handler)
        {
            MainThread.AssertMainThread();
            if (string.IsNullOrEmpty(key) || handler == null) return;
            EnsureStringKeyNotBound(key, "Typed UnsubscribeSync");
            if (!_stringBuckets.TryGetValue(key, out var bucket)) return;
            if (!bucket.TryGetTypedBucket(typeof(T), out var itb)) return;
            if (itb is not TypedBucket<T> typed) return;

            bool changed = false;
            for (int i = typed.List.Count - 1; i >= 0; i--)
            {
                if (typed.List[i] == handler)
                {
                    typed.List.RemoveAt(i);
                    changed = true;
                }
            }
            if (changed) typed.ListVersion++;

            if (typed.IsEmpty)
                bucket.RemoveTypedBucket(typeof(T));
            TryRemoveEmptyBucket(key, bucket);
        }

        // ========== Clear / Count ==========

        public void Clear(int key)
        {
            MainThread.AssertMainThread();
            if ((uint)key < _intBuckets.Count)
                _intBuckets[key] = null;
            _intExceptionPolicies.Remove(key);
            _intPayloadPolicies.Remove(key);
            _intFixedTypes.Remove(key);
        }

        public void Clear(string key)
        {
            MainThread.AssertMainThread();
            if (string.IsNullOrEmpty(key)) return;
            if (TryGetMappedIntKey(key, out var mapped))
            {
                Clear(mapped);
                return;
            }
            _stringBuckets.Remove(key);
            _stringExceptionPolicies.Remove(key);
            _stringPayloadPolicies.Remove(key);
            _stringFixedTypes.Remove(key);
        }

        public void ClearSubscribers()
        {
            MainThread.AssertMainThread();
            for (int i = 0; i < _intBuckets.Count; i++)
                _intBuckets[i] = null;
            _stringBuckets.Clear();
        }

#if UNITY_EDITOR
        public void ResetForTestsOnly()
        {
            MainThread.AssertMainThread();
            ClearSubscribers();
            _intExceptionPolicies.Clear();
            _intPayloadPolicies.Clear();
            _intFixedTypes.Clear();
            _intStableKeys.Clear();
            _stringExceptionPolicies.Clear();
            _stringPayloadPolicies.Clear();
            _stringFixedTypes.Clear();
            _stableKeyToInt.Clear();
        }
#endif

        public int GetSubscriberCount(int key)
        {
            MainThread.AssertMainThread();
            var bucket = GetBucketIfExists(key);
            if (bucket == null) return 0;
            return CountBucket(bucket);
        }

        public int GetSubscriberCount(string key)
        {
            MainThread.AssertMainThread();
            if (string.IsNullOrEmpty(key)) return 0;
            if (TryGetMappedIntKey(key, out var mapped))
                return GetSubscriberCount(mapped);
            if (!_stringBuckets.TryGetValue(key, out var bucket)) return 0;
            return CountBucket(bucket);
        }

        public void SetExceptionPolicy(int key, EventExceptionPolicy policy)
        {
            MainThread.AssertMainThread();
            if (key < 0)
                throw new ArgumentOutOfRangeException(nameof(key), "EventId must be >= 0.");
            if (policy == DefaultExceptionPolicy)
                _intExceptionPolicies.Remove(key);
            else
                _intExceptionPolicies[key] = policy;

            var bucket = GetBucketIfExists(key);
            if (bucket != null)
                bucket.ExceptionPolicy = policy;
        }

        public void SetExceptionPolicy(string key, EventExceptionPolicy policy)
        {
            MainThread.AssertMainThread();
            if (string.IsNullOrEmpty(key)) return;

            if (TryGetMappedIntKey(key, out var mapped))
            {
                SetExceptionPolicy(mapped, policy);
                return;
            }

            if (policy == DefaultExceptionPolicy)
                _stringExceptionPolicies.Remove(key);
            else
                _stringExceptionPolicies[key] = policy;

            if (_stringBuckets.TryGetValue(key, out var bucket))
                bucket.ExceptionPolicy = policy;
        }

        public void SetPayloadPolicy(int key, EventPayloadPolicy policy)
        {
            MainThread.AssertMainThread();
            if (key < 0)
                throw new ArgumentOutOfRangeException(nameof(key), "EventId must be >= 0.");
            if (policy == DefaultPayloadPolicy)
                _intPayloadPolicies.Remove(key);
            else
                _intPayloadPolicies[key] = policy;

            var bucket = GetBucketIfExists(key);
            if (bucket != null)
                bucket.PayloadPolicy = policy;
        }

        public void SetPayloadPolicy(string key, EventPayloadPolicy policy)
        {
            MainThread.AssertMainThread();
            if (string.IsNullOrEmpty(key)) return;

            if (TryGetMappedIntKey(key, out var mapped))
            {
                SetPayloadPolicy(mapped, policy);
                return;
            }

            if (policy == DefaultPayloadPolicy)
                _stringPayloadPolicies.Remove(key);
            else
                _stringPayloadPolicies[key] = policy;

            if (_stringBuckets.TryGetValue(key, out var bucket))
                bucket.PayloadPolicy = policy;
        }

        public void SetTypedPayloadType<T>(int key)
        {
            MainThread.AssertMainThread();
            if (key < 0)
                throw new ArgumentOutOfRangeException(nameof(key), "EventId must be >= 0.");

            var type = typeof(T);
            if (_intFixedTypes.TryGetValue(key, out var existing) && existing != type)
                throw new InvalidOperationException($"{GetKeyLabel(key)} already locked to {existing.Name}; cannot override with {type.Name}.");

            _intFixedTypes[key] = type;

            var bucket = GetBucketIfExists(key);
            bucket?.SetFixedTypedType(type, GetKeyLabel(key));
        }

        public void SetTypedPayloadType<T>(string key)
        {
            MainThread.AssertMainThread();
            if (string.IsNullOrEmpty(key)) return;

            if (TryGetMappedIntKey(key, out var mapped))
            {
                SetTypedPayloadType<T>(mapped);
                return;
            }

            var type = typeof(T);
            if (_stringFixedTypes.TryGetValue(key, out var existing) && existing != type)
                throw new InvalidOperationException($"{GetKeyLabel(key)} already locked to {existing.Name}; cannot override with {type.Name}.");

            _stringFixedTypes[key] = type;

            if (_stringBuckets.TryGetValue(key, out var bucket))
                bucket.SetFixedTypedType(type, GetKeyLabel(key));
        }

        public void BindStableKey(int key, string stableKey)
        {
            MainThread.AssertMainThread();
            if (key < 0)
                throw new ArgumentOutOfRangeException(nameof(key), "EventId must be >= 0.");
            ValidateKey(stableKey);

            if (_intStableKeys.TryGetValue(key, out var existing) && existing != stableKey)
                throw new InvalidOperationException($"EventId {key} is already bound to '{existing}'.");
            if (_stableKeyToInt.TryGetValue(stableKey, out var mapped) && mapped != key)
                throw new InvalidOperationException($"Stable key '{stableKey}' is already bound to EventId {mapped}.");

            if (_stringBuckets.TryGetValue(stableKey, out var legacyBucket) && legacyBucket != null && (legacyBucket.SyncHandlers.Count > 0 || legacyBucket.HasTypedBuckets))
                throw new InvalidOperationException($"Cannot bind '{stableKey}' to EventId {key} because legacy handlers already exist. Remove them first.");

            _intStableKeys[key] = stableKey;
            _stableKeyToInt[stableKey] = key;

            _stringBuckets.Remove(stableKey);

            // Migrate policies from whichever side was configured first
            if (_stringExceptionPolicies.TryGetValue(stableKey, out var stringException))
            {
                if (_intExceptionPolicies.TryGetValue(key, out var intException) && intException != stringException)
                    throw new InvalidOperationException($"Exception policy mismatch while binding '{stableKey}'.");
                _intExceptionPolicies[key] = stringException;
                _stringExceptionPolicies.Remove(stableKey);
            }
            if (_stringPayloadPolicies.TryGetValue(stableKey, out var stringPayload))
            {
                if (_intPayloadPolicies.TryGetValue(key, out var intPayload) && intPayload != stringPayload)
                    throw new InvalidOperationException($"Payload policy mismatch while binding '{stableKey}'.");
                _intPayloadPolicies[key] = stringPayload;
                _stringPayloadPolicies.Remove(stableKey);
            }
            if (_stringFixedTypes.TryGetValue(stableKey, out var stringFixedType))
            {
                if (_intFixedTypes.TryGetValue(key, out var intFixedType) && intFixedType != stringFixedType)
                    throw new InvalidOperationException($"Typed payload type mismatch while binding '{stableKey}'.");
                _intFixedTypes[key] = stringFixedType;
                _stringFixedTypes.Remove(stableKey);
            }
        }

        static int CountBucket(HandlerBucket bucket)
        {
            int count = bucket.SyncHandlers.Count;
            foreach (var typed in bucket.EnumerateTypedBuckets())
                count += typed.Count;
            return count;
        }

        // ========== Handler Invocation (conditional try/catch) ==========

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        static void InvokePayloadHandler(Action<EventPayload> handler, EventPayload payload, bool catchExceptions)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            catchExceptions = true;
#endif
            if (catchExceptions)
            {
                try { handler(payload); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
            else
            {
                handler(payload);
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        static void InvokeTypedHandler<T>(SyncEventHandler<T> handler, in T payload, bool catchExceptions)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            catchExceptions = true;
#endif
            if (catchExceptions)
            {
                try { handler(in payload); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
            else
            {
                handler(in payload);
            }
        }

        static bool ShouldCatchExceptions(EventExceptionPolicy policy)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return true;
#else
            return policy == EventExceptionPolicy.CatchAndLog;
#endif
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        static bool AllowsPayload(EventPayloadPolicy policy)
            => (policy & EventPayloadPolicy.AllowPublish) != 0;

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        static bool AllowsExtras(EventPayloadPolicy policy)
            => (policy & EventPayloadPolicy.AllowExtras) != 0;

        // ========== Internals (int key) ==========

        HandlerBucket GetOrCreateBucket(int key)
        {
            if (key < 0)
                throw new ArgumentOutOfRangeException(nameof(key), "EventId must be >= 0.");

            EnsureIntBucketCapacity(key);
            var bucket = _intBuckets[key];
            if (bucket == null)
            {
                bucket = new HandlerBucket();
                ApplyIntPolicies(key, bucket);
                _intBuckets[key] = bucket;
            }
            return bucket;
        }

        HandlerBucket GetBucketIfExists(int key)
        {
            if ((uint)key >= _intBuckets.Count)
                return null;
            return _intBuckets[key];
        }

        void EnsureIntBucketCapacity(int key)
        {
            while (_intBuckets.Count <= key)
                _intBuckets.Add(null);
        }

        void ApplyIntPolicies(int key, HandlerBucket bucket)
        {
            if (_intExceptionPolicies.TryGetValue(key, out var exceptionPolicy))
                bucket.ExceptionPolicy = exceptionPolicy;
            if (_intPayloadPolicies.TryGetValue(key, out var payloadPolicy))
                bucket.PayloadPolicy = payloadPolicy;
            if (_intFixedTypes.TryGetValue(key, out var fixedType))
                bucket.SetFixedTypedType(fixedType, GetKeyLabel(key));
        }

        void TryRemoveEmptyBucket(int key, HandlerBucket bucket)
        {
            if (bucket.SyncHandlers.Count == 0 && !bucket.HasTypedBuckets)
                _intBuckets[key] = null;
        }

        // ========== Internals (string key) ==========

        HandlerBucket GetOrCreateBucket(string key)
        {
            EnsureStringKeyNotBound(key, "legacy API");
            if (!_stringBuckets.TryGetValue(key, out var bucket))
            {
                bucket = new HandlerBucket();
                ApplyStringPolicies(key, bucket);
                _stringBuckets[key] = bucket;
            }
            return bucket;
        }

        void ApplyStringPolicies(string key, HandlerBucket bucket)
        {
            if (_stringExceptionPolicies.TryGetValue(key, out var exceptionPolicy))
                bucket.ExceptionPolicy = exceptionPolicy;
            if (_stringPayloadPolicies.TryGetValue(key, out var payloadPolicy))
                bucket.PayloadPolicy = payloadPolicy;
            if (_stringFixedTypes.TryGetValue(key, out var fixedType))
                bucket.SetFixedTypedType(fixedType, GetKeyLabel(key));
        }

        void TryRemoveEmptyBucket(string key, HandlerBucket bucket)
        {
            if (bucket.SyncHandlers.Count == 0 && !bucket.HasTypedBuckets)
                _stringBuckets.Remove(key);
        }

        // ========== Shared Internals ==========

        TypedBucket<T> GetOrCreateTypedBucket<T>(HandlerBucket bucket)
            => bucket.GetOrCreateTypedBucket<T>();

        Action<EventPayload>[] GetSyncSnapshot(HandlerBucket bucket)
        {
            if (bucket.SyncSnapshot == null || bucket.SyncSnapshotVersion != bucket.SyncListVersion)
            {
                bucket.SyncSnapshot = bucket.SyncHandlers.Count > 0
                    ? bucket.SyncHandlers.ToArray()
                    : Array.Empty<Action<EventPayload>>();
                bucket.SyncSnapshotVersion = bucket.SyncListVersion;
            }
            return bucket.SyncSnapshot;
        }

        SyncEventHandler<T>[] GetTypedSnapshot<T>(HandlerBucket bucket)
        {
            if (!bucket.TryGetTypedBucket(typeof(T), out var itb))
                return Array.Empty<SyncEventHandler<T>>();
            if (itb is not TypedBucket<T> typed)
                return Array.Empty<SyncEventHandler<T>>();

            if (typed.Snapshot == null || typed.SnapshotVersion != typed.ListVersion)
            {
                typed.Snapshot = typed.List.Count > 0 ? typed.List.ToArray() : Array.Empty<SyncEventHandler<T>>();
                typed.SnapshotVersion = typed.ListVersion;
            }
            return typed.Snapshot;
        }

        static void ValidateKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Event key cannot be null or empty.", nameof(key));
        }

        string GetKeyLabel(int key)
        {
            if (_intStableKeys.TryGetValue(key, out var stable))
                return $"Event '{stable}' (id={key})";
            return $"EventId {key}";
        }

        string GetKeyLabel(string key)
        {
            if (string.IsNullOrEmpty(key))
                return "Event '<empty>'";
            if (_stableKeyToInt.TryGetValue(key, out var mapped))
                return $"Event '{key}' (id={mapped})";
            return $"Event '{key}'";
        }

        void EnsureStringKeyNotBound(string key, string operation)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (_stableKeyToInt.TryGetValue(key, out var mappedId))
                throw new InvalidOperationException($"{operation} is disabled for legacy key '{key}' because it is bound to EventId {mappedId}. Use the int overloads instead.");
        }

        bool TryGetMappedIntKey(string key, out int eventId)
        {
            if (string.IsNullOrEmpty(key))
            {
                eventId = default;
                return false;
            }
            return _stableKeyToInt.TryGetValue(key, out eventId);
        }

        // ========== Subscriptions ==========

        sealed class SyncSubscription : IDisposable
        {
            readonly SyncEventService _owner;
            readonly string _key;
            readonly Action<EventPayload> _handler;
            bool _disposed;

            public SyncSubscription(SyncEventService owner, string key, Action<EventPayload> handler)
            {
                _owner = owner;
                _key = key;
                _handler = handler;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _owner.UnsubscribeSync(_key, _handler);
            }
        }

        sealed class TypedIntSubscription<T> : IDisposable
        {
            readonly SyncEventService _owner;
            readonly int _key;
            readonly SyncEventHandler<T> _handler;
            bool _disposed;

            public TypedIntSubscription(SyncEventService owner, int key, SyncEventHandler<T> handler)
            {
                _owner = owner;
                _key = key;
                _handler = handler;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _owner.UnsubscribeTyped(_key, _handler);
            }
        }

        sealed class TypedStringSubscription<T> : IDisposable
        {
            readonly SyncEventService _owner;
            readonly string _key;
            readonly SyncEventHandler<T> _handler;
            bool _disposed;

            public TypedStringSubscription(SyncEventService owner, string key, SyncEventHandler<T> handler)
            {
                _owner = owner;
                _key = key;
                _handler = handler;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _owner.UnsubscribeTyped(_key, _handler);
            }
        }
    }
}
