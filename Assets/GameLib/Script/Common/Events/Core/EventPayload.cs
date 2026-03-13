// Game.Common.EventPayload.cs
//
// Shared lightweight EventPayload type for sync events.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Common
{
    /// <summary>
    /// Event payload used by SyncEventService (lightweight, non-async).
    /// </summary>
    public sealed class EventPayload
    {
        object _typedData;
        Type _typedDataType;
        Dictionary<string, object> _extras; // Legacy extras (Collision keys forbid to avoid GC/memory churn)
        int _frameIndex;
        float _deltaTime;
        bool _extrasAllowed = true;

        public int FrameIndex { get => _frameIndex; set => _frameIndex = value; }
        public float DeltaTime { get => _deltaTime; set => _deltaTime = value; }

        public void SetData<T>(T data) where T : class
        {
            _typedData = data;
            _typedDataType = typeof(T);
        }
        public T GetData<T>() where T : class
        {
            if (_typedDataType == typeof(T)) return (T)_typedData;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_typedDataType != null && _typedDataType != typeof(T))
                throw new InvalidOperationException($"EventPayload stored value of type {_typedDataType.Name} but GetData<{typeof(T).Name}>() was requested.");
#endif
            return _typedData as T;
        }
        public bool TryGetData<T>(out T data) where T : class
        {
            if (_typedDataType == typeof(T))
            {
                data = (T)_typedData;
                return data != null;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_typedDataType != null && _typedDataType != typeof(T))
                throw new InvalidOperationException($"EventPayload stored value of type {_typedDataType.Name} but TryGetData<{typeof(T).Name}>() was requested.");
#endif
            data = _typedData as T;
            return data != null;
        }
        public void Set<T>(string key, T value)
        {
            EnsureExtrasAllowed();
            _extras ??= new Dictionary<string, object>(StringComparer.Ordinal);
            _extras[key] = value;
        }
        public bool TryGet<T>(string key, out T value)
        {
            EnsureExtrasAllowed();
            value = default;
            if (_extras == null || !_extras.TryGetValue(key, out var obj)) return false;
            if (obj is T typed) { value = typed; return true; }
            return false;
        }
        public T Get<T>(string key, T defaultValue = default)
        {
            return TryGet<T>(key, out var v) ? v : defaultValue;
        }
        public void Clear()
        {
            _typedData = null;
            _typedDataType = null;
            _extras?.Clear();
            _frameIndex = 0; _deltaTime = 0f;
            _extrasAllowed = true;
        }

        internal void SetExtrasAllowed(bool allowed)
        {
            _extrasAllowed = allowed;
            if (!allowed && _extras != null && _extras.Count > 0)
                _extras.Clear();
        }

        void EnsureExtrasAllowed()
        {
            if (!_extrasAllowed)
                throw new InvalidOperationException("EventPayload extras are disabled for this event.");
        }
    }

    public static class EventPayloadPool
    {
        const int InitialCapacity = 32;
        public const int MaxPoolSize = 64;
        static readonly Stack<EventPayload> _pool = new(InitialCapacity);
        // Main-thread-only pool: no locking
        public static EventPayload Rent() { MainThread.AssertMainThread(); if (_pool.Count > 0) return _pool.Pop(); return new EventPayload(); }
        public static void Return(EventPayload p) { MainThread.AssertMainThread(); if (p == null) return; p.Clear(); if (_pool.Count < MaxPoolSize) _pool.Push(p); }
    }
}
