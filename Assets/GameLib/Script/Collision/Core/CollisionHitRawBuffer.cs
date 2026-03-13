// Game.Collision.CollisionHitRawBuffer.cs
//
// Fixed-length buffer for CollisionSystem hit accumulation.
// Jobs must never throw on overflow. Excess hits are discarded.

using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.Collision
{
    /// <summary>
    /// Fixed-capacity buffer shared by Collision jobs.
    ///
    /// NOTE: Avoid unsafe code. Jobs should write to the provided ParallelWriter (NativeQueue<T>.ParallelWriter).
    /// The buffer drains on the main thread during CompleteAndDispatch(). Overflow is counted during drain.
    /// </summary>
    public struct CollisionHitRawBuffer : IDisposable
    {
        public NativeArray<CollisionHitRaw> Buffer;

        NativeQueue<CollisionHitRaw> _queue;

        int _overflowCount;

        public int MaxHits => Buffer.IsCreated ? Buffer.Length : 0;

        public bool IsCreated => Buffer.IsCreated;

        public void Init(int maxHits, Allocator allocator)
        {
            if (maxHits <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxHits), "Must be greater than zero.");

            Dispose();

            Buffer = new NativeArray<CollisionHitRaw>(maxHits, allocator);
            _queue = new NativeQueue<CollisionHitRaw>(allocator);
            _overflowCount = 0;
        }

        public void ClearForFrame()
        {
            EnsureCreated();

            // Ensure queue is empty (should normally be empty after drain, but be defensive)
            while (_queue.TryDequeue(out _)) { }
            _overflowCount = 0;
        }

        public int Count => Buffer.IsCreated ? math.min(_queue.Count, Buffer.Length) : 0;
        public int OverflowCount => _overflowCount;

        public NativeQueue<CollisionHitRaw>.ParallelWriter AsParallelWriter()
        {
            EnsureCreated();
            return _queue.AsParallelWriter();
        }

        public bool TryDequeue(out CollisionHitRaw raw)
        {
            raw = default;
            if (!_queue.IsCreated)
                return false;
            return _queue.TryDequeue(out raw);
        }

        internal void AddOverflow(int count)
        {
            _overflowCount += count;
        }

        public void Dispose()
        {
            if (Buffer.IsCreated) Buffer.Dispose();
            if (_queue.IsCreated) _queue.Dispose();
        }

        void EnsureCreated()
        {
            if (!IsCreated)
                throw new InvalidOperationException("CollisionHitRawBuffer is not initialized.");
        }
    }
}
