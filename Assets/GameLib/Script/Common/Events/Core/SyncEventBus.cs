// Game.Common.SyncEventBus.cs
//
// Typed-only, high-performance event bus for hot-path events (Collision, etc.)
// Main-thread-only. No locks. No GC on Publish hot-path.
//
// Design:
// - EventId (int) only. String keys are not supported.
// - Dense EventId range [0, MaxEventIdExclusive) generated via ProjectEventIds. Sparse IDs are rejected.
// - RegisterEvent<T> must be called before Subscribe/Publish.
// - Type is locked per EventId. Mismatched types throw immediately.
// - No Dev/Release behavioral divergence (except log verbosity).

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Game.Common
{
    /// <summary>
    /// Delegate for typed event handlers with in parameter (zero-copy for large structs).
    /// </summary>
    public delegate void SyncEventHandler<T>(in T payload);

    /// <summary>
    /// Controls whether SyncEventBus catches and logs handler exceptions or lets them propagate.
    /// </summary>
    public enum EventExceptionPolicy
    {
        /// <summary>Catches exceptions and logs them via UnityEngine.Debug. Use for non-critical events.</summary>
        CatchAndLog = 0,
        /// <summary>Does not wrap handlers with try/catch. Hot-path / crash-on-fault semantics.</summary>
        Propagate = 1,
    }

    /// <summary>
    /// Options for event registration.
    /// </summary>
    public readonly struct EventOptions
    {
        public readonly EventExceptionPolicy ExceptionPolicy;

        public EventOptions(EventExceptionPolicy policy) => ExceptionPolicy = policy;

        public static EventOptions Default => new(EventExceptionPolicy.Propagate);
    }

    /// <summary>
    /// Typed-only sync event bus interface.
    /// Main-thread-only. Zero GC on Publish hot-path.
    /// </summary>
    public interface ISyncEventBus
    {
        /// <summary>
        /// Register an event type for the given eventId using the default options.
        /// </summary>
        void RegisterEvent<T>(int eventId);

        /// <summary>
        /// Register an event type for the given eventId with explicit options.
        /// Throws if eventId is out of range or already registered with a different configuration.
        /// </summary>
        void RegisterEvent<T>(int eventId, EventOptions options);

        /// <summary>
        /// Subscribe to an event. Returns IDisposable for unsubscription.
        /// Throws if eventId is unregistered or type mismatches.
        /// Throws if duplicate handler is registered.
        /// </summary>
        IDisposable Subscribe<T>(int eventId, SyncEventHandler<T> handler);

        /// <summary>
        /// Publish an event. Returns number of handlers invoked.
        /// Throws if eventId is unregistered or type mismatches.
        /// </summary>
        int Publish<T>(int eventId, in T payload);

        /// <summary>
        /// Get subscriber count for an eventId.
        /// </summary>
        int GetSubscriberCount(int eventId);

        /// <summary>
        /// Clear all subscribers but keep event registrations.
        /// </summary>
        void ClearAllSubscribers();

#if UNITY_EDITOR
        /// <summary>
        /// Reset everything including registrations. Test-only.
        /// </summary>
        void ResetForTestsOnly();
#endif
    }

    /// <summary>
    /// Typed-only sync event bus implementation.
    /// Main-thread-only. No locks.
    /// </summary>
    public sealed class SyncEventBus : ISyncEventBus
    {
        readonly int _maxEventId;
        readonly EventConfig[] _configs;
        readonly Bucket[] _buckets;

        public SyncEventBus(int maxEventIdExclusive)
        {
            if (maxEventIdExclusive <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxEventIdExclusive), "Must be > 0.");

            _maxEventId = maxEventIdExclusive;
            _configs = new EventConfig[maxEventIdExclusive];
            _buckets = new Bucket[maxEventIdExclusive];
        }

        public void RegisterEvent<T>(int eventId)
        {
            RegisterEvent<T>(eventId, EventOptions.Default);
        }

        public void RegisterEvent<T>(int eventId, EventOptions options)
        {
            MainThread.AssertMainThread();
            ValidateEventIdRange(eventId);

            ref var config = ref _configs[eventId];
            var requestedType = typeof(T);

            if (config.Registered)
            {
                throw new InvalidOperationException(
                    $"EventId {eventId} is already registered with type {config.PayloadType.Name} and policy {config.ExceptionPolicy}. Multiple registrations are forbidden.");
            }

            config.PayloadType = requestedType;
            config.ExceptionPolicy = options.ExceptionPolicy;
            config.Registered = true;
            EnsureBucket(eventId);
        }

        public IDisposable Subscribe<T>(int eventId, SyncEventHandler<T> handler)
        {
            MainThread.AssertMainThread();
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            ValidateEventIdRange(eventId);
            ref readonly var config = ref _configs[eventId];
            EnsureRegistered(eventId, in config);
            EnsureTypeMatch<T>(eventId, in config);

            var bucket = EnsureBucket(eventId);

            if (bucket.Contains(handler))
            {
                throw new InvalidOperationException(
                    $"Duplicate handler for EventId {eventId}. Delegate target '{DescribeDelegateTarget(handler)}' with method '{handler.Method?.Name}' is already subscribed.");
            }

            bucket.Add(handler);

            return new Subscription<T>(this, eventId, handler);
        }

        public int Publish<T>(int eventId, in T payload)
        {
            MainThread.AssertMainThread();
            ValidateEventIdRange(eventId);
            ref readonly var config = ref _configs[eventId];
            EnsureRegistered(eventId, in config);
            EnsureTypeMatch<T>(eventId, in config);

            var bucket = EnsureBucket(eventId);
            var snapshot = bucket.GetSnapshot();
            if (snapshot.Length == 0)
                return 0;

            bool propagate = config.ExceptionPolicy == EventExceptionPolicy.Propagate;

            for (int i = 0; i < snapshot.Length; i++)
            {
                InvokeHandler((SyncEventHandler<T>)snapshot[i], in payload, propagate);
            }

            return snapshot.Length;
        }

        public int GetSubscriberCount(int eventId)
        {
            MainThread.AssertMainThread();
            ValidateEventIdRange(eventId);
            ref readonly var config = ref _configs[eventId];
            EnsureRegistered(eventId, in config);
            var bucket = _buckets[eventId];
            return bucket?.Count ?? 0;
        }

        public void ClearAllSubscribers()
        {
            MainThread.AssertMainThread();
            for (int i = 0; i < _buckets.Length; i++)
            {
                _buckets[i]?.Clear();
            }
        }

#if UNITY_EDITOR
        public void ResetForTestsOnly()
        {
            MainThread.AssertMainThread();
            for (int i = 0; i < _maxEventId; i++)
            {
                _configs[i] = default;
                _buckets[i] = null;
            }
        }
#endif

        // ========== Internal Types ==========

        struct EventConfig
        {
            public Type PayloadType;
            public EventExceptionPolicy ExceptionPolicy;
            public bool Registered;
        }

        sealed class Bucket
        {
            readonly List<Delegate> _handlers = new();
            Delegate[] _snapshot = Array.Empty<Delegate>();
            int _version;
            int _snapshotVersion = -1;

            public int Count => _handlers.Count;

            public bool Contains(Delegate handler) => _handlers.Contains(handler);

            public void Add(Delegate handler)
            {
                _handlers.Add(handler);
                _version++;
            }

            public bool Remove(Delegate handler)
            {
                bool removed = false;
                for (int i = _handlers.Count - 1; i >= 0; i--)
                {
                    if (_handlers[i] == handler)
                    {
                        _handlers.RemoveAt(i);
                        removed = true;
                    }
                }

                if (removed)
                {
                    _version++;
                }

                return removed;
            }

            public Delegate[] GetSnapshot()
            {
                if (_snapshotVersion != _version)
                {
                    _snapshot = _handlers.Count > 0
                        ? _handlers.ToArray()
                        : Array.Empty<Delegate>();
                    _snapshotVersion = _version;
                }

                return _snapshot;
            }

            public void Clear()
            {
                _handlers.Clear();
                _snapshot = Array.Empty<Delegate>();
                _version++;
                _snapshotVersion = _version;
            }
        }

        // ========== Validation ==========

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void ValidateEventIdRange(int eventId)
        {
            if ((uint)eventId >= (uint)_maxEventId)
                throw new ArgumentOutOfRangeException(nameof(eventId),
                    $"EventId {eventId} is out of range [0, {_maxEventId}).");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void EnsureRegistered(int eventId, in EventConfig config)
        {
            if (!config.Registered)
                throw new InvalidOperationException(
                    $"EventId {eventId} is not registered. Call RegisterEvent<T> first.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void EnsureTypeMatch<T>(int eventId, in EventConfig config)
        {
            var expected = config.PayloadType;
            var actual = typeof(T);
            if (expected != actual)
                throw new InvalidOperationException(
                    $"EventId {eventId} is registered as {expected.Name} but {actual.Name} was used.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        Bucket EnsureBucket(int eventId)
        {
            var bucket = _buckets[eventId];
            if (bucket == null)
            {
                bucket = new Bucket();
                _buckets[eventId] = bucket;
            }

            return bucket;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static string DescribeDelegateTarget(Delegate handler)
        {
            if (handler == null)
                return "<null>";
            return handler.Target == null ? "static" : handler.Target.GetType().Name;
        }

        // ========== Invocation ==========

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void InvokeHandler<T>(SyncEventHandler<T> handler, in T payload, bool propagate)
        {
            if (propagate)
            {
                handler(in payload);
            }
            else
            {
                try
                {
                    handler(in payload);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogException(ex);
                }
            }
        }

        // ========== Unsubscribe ==========

        void Unsubscribe<T>(int eventId, SyncEventHandler<T> handler)
        {
            MainThread.AssertMainThread();
            if (handler == null) return;
            if ((uint)eventId >= (uint)_maxEventId) return;

            var bucket = _buckets[eventId];
            bucket?.Remove(handler);
        }

        // ========== Subscription ==========

        sealed class Subscription<T> : IDisposable
        {
            readonly SyncEventBus _owner;
            readonly int _eventId;
            readonly SyncEventHandler<T> _handler;
            bool _disposed;

            public Subscription(SyncEventBus owner, int eventId, SyncEventHandler<T> handler)
            {
                _owner = owner;
                _eventId = eventId;
                _handler = handler;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _owner.Unsubscribe(_eventId, _handler);
            }
        }
    }
}
