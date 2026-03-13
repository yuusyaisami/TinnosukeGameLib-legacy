#nullable enable
// Game.Common.EventService.cs
//
// Async-first Event System.
//
// Notes:
// - payload は vNext 方針に合わせて IVarStore に統一する。
// - VariableBag を介した payload は撤去（保存/資産化/型安全の妨げになるため）。

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Runtime.CompilerServices;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.Common
{
    /// <summary>
    /// Observer that collects published event metadata for debugging.
    /// </summary>
    public interface IEventLogStore
    {
        IReadOnlyList<EventLogEntry> Entries { get; }
        int Count { get; }
        int Capacity { get; }
        bool Enabled { get; set; }
        void Log(EventLogEntry entry);
        void Log(string key, string payloadSummary);
        void Clear();
    }

    /// <summary>
    /// Holds a single published event record.
    /// </summary>
    [Serializable]
    public sealed class EventLogEntry
    {
        public EventLogEntry(DateTime recordedAt, string key, string payloadSummary)
        {
            RecordedAt = recordedAt;
            Key = key ?? string.Empty;
            PayloadSummary = payloadSummary ?? string.Empty;
        }

        public DateTime RecordedAt { get; }
        public string Timestamp => RecordedAt.ToString("HH:mm:ss.fff");
        public string Key { get; }
        public string PayloadSummary { get; }
    }

    /// <summary>
    /// Circular store of log entries with optional capture.
    /// </summary>
    public sealed class EventLogStore : IEventLogStore
    {
        readonly object _gate = new();
        readonly Queue<EventLogEntry> _entries;

        public EventLogStore(int capacity = 64)
        {
            Capacity = Math.Max(1, capacity);
            _entries = new Queue<EventLogEntry>(Capacity);
            Enabled = true;
        }

        public IReadOnlyList<EventLogEntry> Entries
        {
            get
            {
                lock (_gate)
                {
                    return _entries.Count == 0 ? Array.Empty<EventLogEntry>() : _entries.ToArray();
                }
            }
        }

        public int Count
        {
            get
            {
                lock (_gate)
                {
                    return _entries.Count;
                }
            }
        }

        public int Capacity { get; }

        public bool Enabled { get; set; }

        public void Log(EventLogEntry entry)
        {
            if (!Enabled) return;
            lock (_gate)
            {
                if (_entries.Count >= Capacity)
                    _entries.Dequeue();
                _entries.Enqueue(entry);
            }
        }

        public void Log(string key, string payloadSummary)
        {
            Log(new EventLogEntry(DateTime.UtcNow, key, payloadSummary));
        }

        public void Clear()
        {
            lock (_gate)
            {
                _entries.Clear();
            }
        }
    }

    /// <summary>
    /// Async-first Event Service interface.
    /// payload は IVarStore を使用する。
    /// </summary>
    public interface IEventService
    {
        IDisposable Subscribe(string key, Func<IVarStore, CancellationToken, UniTask> handler);
        void Unsubscribe(string key, Func<IVarStore, CancellationToken, UniTask> handler);
        UniTask PublishAsync(string key, IVarStore payload, CancellationToken ct = default);

        void Clear(string key);
        void ClearAll();
    }

    // LifetimeScope marker interfaces
    public interface IProjectEventService : IEventService { }
    public interface IPlatformEventService : IEventService { }
    public interface IGlobalEventService : IEventService { }
    public interface ISceneEventService : IEventService { }
    public interface IFieldEventService : IEventService { }
    public interface IEntityEventService : IEventService { }
    public interface IUIEventService : IEventService { }
    public interface IUIElementEventService : IEventService { }

    /// <summary>
    /// EventService with async handlers.
    /// </summary>
    public sealed class EventService : IEventService,
        IProjectEventService,
        IPlatformEventService,
        IGlobalEventService,
        ISceneEventService,
        IFieldEventService,
        IEntityEventService,
        IUIEventService,
        IUIElementEventService
    {
        sealed class HandlerEntry
        {
            public readonly Func<IVarStore, CancellationToken, UniTask> Handler;
            public HandlerEntry(Func<IVarStore, CancellationToken, UniTask> handler) => Handler = handler;
        }

        readonly Dictionary<string, List<HandlerEntry>> _handlers = new(StringComparer.Ordinal);
        readonly object _gate = new();
        readonly IEventLogStore? _logStore;

        public EventService(IEventLogStore? logStore = null)
        {
            _logStore = logStore;
        }

        public IDisposable Subscribe(string key, Func<IVarStore, CancellationToken, UniTask> handler)
        {
            if (string.IsNullOrWhiteSpace(key) || handler == null)
                return NullSubscription.Instance;

            var entry = new HandlerEntry(handler);
            lock (_gate)
            {
                if (!_handlers.TryGetValue(key, out var list))
                {
                    list = new List<HandlerEntry>();
                    _handlers[key] = list;
                }
                list.Add(entry);
            }

            return new Subscription(this, key, handler);
        }

        public void Unsubscribe(string key, Func<IVarStore, CancellationToken, UniTask> handler)
        {
            if (string.IsNullOrWhiteSpace(key) || handler == null)
                return;

            lock (_gate)
            {
                if (!_handlers.TryGetValue(key, out var list))
                    return;

                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (list[i].Handler == handler)
                        list.RemoveAt(i);
                }

                if (list.Count == 0)
                    _handlers.Remove(key);
            }
        }

        public async UniTask PublishAsync(string key, IVarStore payload, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            HandlerEntry[] snapshot;
            lock (_gate)
            {
                if (!_handlers.TryGetValue(key, out var list) || list.Count == 0)
                    return;
                snapshot = list.ToArray();
            }

            var vars = payload ?? NullVarStore.Instance;
            LogEvent(key, vars);

            for (int i = 0; i < snapshot.Length; i++)
            {
                ct.ThrowIfCancellationRequested();
                var entry = snapshot[i];
                if (entry?.Handler == null)
                    continue;
                try
                {
                    await entry.Handler(vars, ct);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        public void Clear(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;
            lock (_gate)
            {
                _handlers.Remove(key);
            }
        }

        public void ClearAll()
        {
            lock (_gate)
            {
                _handlers.Clear();
            }
        }

        public int GetSubscriberCount(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return 0;
            lock (_gate)
            {
                if (!_handlers.TryGetValue(key, out var list)) return 0;
                return list.Count;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        List<HandlerEntry> GetOrCreateHandlerList(string key)
        {
            if (!_handlers.TryGetValue(key, out var list))
            {
                list = new List<HandlerEntry>();
                _handlers[key] = list;
            }
            return list;
        }

        void LogEvent(string key, IVarStore payload)
        {
            if (_logStore == null || string.IsNullOrEmpty(key))
                return;

            var summary = FormatPayload(payload);
            _logStore.Log(key, summary);
        }

        static string FormatPayload(IVarStore payload)
        {
            if (payload == null)
                return "null";

            var sb = new StringBuilder();
            foreach (var varId in payload.EnumerateVarIds())
            {
                if (varId == 0)
                    continue;

                var name = VarIdResolver.TryGetStableKey(varId, out var stableKey) && !string.IsNullOrEmpty(stableKey)
                    ? stableKey
                    : varId.ToString();

                string val;
                var kind = payload.GetVarKind(varId);
                if (kind == ValueKind.ManagedRef)
                {
                    if (payload.TryGetManagedRef(varId, out var managed))
                        val = managed != null ? $"<{managed.GetType().Name}> {managed}" : "null";
                    else
                        val = "?";
                }
                else if (payload.TryGetVariant(varId, out var variant))
                {
                    val = variant.ToString();
                }
                else if (payload.TryGetManagedRef(varId, out var managed))
                {
                    val = managed != null ? $"<{managed.GetType().Name}> {managed}" : "null";
                }
                else
                {
                    val = "?";
                }

                if (sb.Length > 0)
                    sb.Append(", ");
                sb.Append(name).Append("=").Append(val);
            }

            return sb.Length > 0 ? sb.ToString() : "empty";
        }

        sealed class Subscription : IDisposable
        {
            readonly EventService _owner;
            readonly string _key;
            readonly Func<IVarStore, CancellationToken, UniTask> _handler;
            bool _disposed;

            public Subscription(EventService owner, string key, Func<IVarStore, CancellationToken, UniTask> handler)
            {
                _owner = owner;
                _key = key;
                _handler = handler;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _owner.Unsubscribe(_key, _handler);
            }
        }

        sealed class NullSubscription : IDisposable
        {
            public static readonly NullSubscription Instance = new();
            NullSubscription() { }
            public void Dispose() { }
        }
    }
}
