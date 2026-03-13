#nullable enable
using System;
using System.Buffers;
using System.Collections.Generic;
using Game.Common;
using UnityEngine;

namespace Game.Collision
{
    /// <summary>
    /// Projectスコープの衝突ヒット分配Router。
    /// SyncEventBus(Propagate)前提のため、絶対に例外を外へ投げない。
    /// </summary>
    public sealed class CollisionHitRouter : IHitColliderChannelRouter, IDisposable
    {
        readonly ISyncEventBus _eventBus;
        IDisposable? _subscription;

        WatcherBucket?[] _bucketsById = Array.Empty<WatcherBucket?>();
        int _dispatchDepth;
        PendingOp[] _pendingOps = Array.Empty<PendingOp>();
        int _pendingCount;

        int[] _touched = Array.Empty<int>();
        int _touchedCount;

        int _droppedOutOfBounds;

        public int DroppedHit_OutOfBoundsId => _droppedOutOfBounds;

        bool IsDispatching => _dispatchDepth > 0;

        public CollisionHitRouter(ISyncEventBus eventBus)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _subscription = _eventBus.Subscribe<CollisionHitFrame>(CollisionEventIds.Frame, OnFrame);
        }

        public void Dispose()
        {
            _subscription?.Dispose();
            _subscription = null;

            for (int i = 0; i < _bucketsById.Length; i++)
            {
                var b = _bucketsById[i];
                if (b != null) b.Dispose();
            }

            _bucketsById = Array.Empty<WatcherBucket?>();

            if (_pendingOps.Length > 0)
            {
                // no pooled resources inside
                _pendingOps = Array.Empty<PendingOp>();
                _pendingCount = 0;
            }

            if (_touched.Length > 0)
            {
                _touched = Array.Empty<int>();
                _touchedCount = 0;
            }
        }

        public void RegisterWatcher(DynamicColliderHandle self, HitColliderChannelRuntime runtime, HitWatchFlags flags)
        {
            if (runtime == null || !self.IsValid)
                return;

            if (IsDispatching)
            {
                EnqueuePending(PendingOpKind.Register, self, runtime, flags);
                return;
            }

            GetOrCreateBucket(self.Id).Register(self, runtime, flags);
        }

        public void UnregisterWatcher(DynamicColliderHandle self, HitColliderChannelRuntime runtime)
        {
            if (runtime == null || !self.IsValid)
                return;

            if (IsDispatching)
            {
                EnqueuePending(PendingOpKind.Unregister, self, runtime, HitWatchFlags.None);
                return;
            }

            if (!TryGetBucket(self.Id, out var bucket) || bucket == null)
                return;

            bucket.Unregister(runtime, self.Generation);
        }

        public void UpdateWatcherFlags(DynamicColliderHandle self, HitColliderChannelRuntime runtime, HitWatchFlags flags)
        {
            if (runtime == null || !self.IsValid)
                return;

            if (IsDispatching)
            {
                EnqueuePending(PendingOpKind.UpdateFlags, self, runtime, flags);
                return;
            }

            if (!TryGetBucket(self.Id, out var bucket) || bucket == null)
                return;

            bucket.UpdateFlags(runtime, self.Generation, flags);
        }

        void OnFrame(in CollisionHitFrame frame)
        {
            // Propagate policyのため、ここからは絶対にthrowしない。
            HitFrameMeta meta = default;
            try
            {
                _dispatchDepth++;
                if (_dispatchDepth == 1)
                {
                    _droppedOutOfBounds = 0;
                    _touchedCount = 0;
                }

                meta = new HitFrameMeta(frame.FrameIndex, frame.FrameStamp, frame.DeltaTime);

                // DynDyn
                for (int i = 0; i < frame.DynDynCount; i++)
                {
                    var hit = frame.HitsDynDyn[i];
                    DispatchSelf(in hit, in meta);

                    if (hit.OtherDynamic.IsValid)
                    {
                        var mirrored = MirrorDynDyn(in hit);
                        DispatchOther(in mirrored, in meta);
                    }
                }

                // DynStatic
                for (int i = 0; i < frame.DynStaticCount; i++)
                {
                    var hit = frame.HitsDynStatic[i];
                    DispatchSelf(in hit, in meta);
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            finally
            {
                if (_dispatchDepth > 0)
                    _dispatchDepth--;

                // Nested dispatch is allowed; defer state mutation to the outermost frame.
                if (!IsDispatching)
                {
                    // Apply deferred ops
                    if (_pendingCount > 0)
                    {
                        ApplyPending();
                    }

                    // End-frame processing: emit Exit events for buckets and rotate per-bucket maps
                    var buckets = _bucketsById;
                    for (int i = 0; i < buckets.Length; i++)
                    {
                        var b = buckets[i];
                        if (b == null)
                            continue;
                        b.EndFrame(meta);
                    }

                    // Compact touched buckets (generation mismatch / dead watchers)
                    for (int i = 0; i < _touchedCount; i++)
                    {
                        int id = _touched[i];
                        if ((uint)id >= (uint)buckets.Length)
                            continue;
                        var b = buckets[id];
                        if (b == null)
                            continue;
                        b.Compact();
                    }
                }
            }
        }

        void DispatchSelf(in CollisionHit hit, in HitFrameMeta meta)
        {
            var self = hit.Self;
            if (!self.IsValid)
                return;

            int id = self.Id;
            if ((uint)id >= (uint)_bucketsById.Length)
            {
                _droppedOutOfBounds++;
                return;
            }

            var b = _bucketsById[id];
            if (b == null)
                return;

            TouchBucket(id, meta.FrameIndex, b);
            b.Dispatch(in hit, in meta, isOtherSide: false);
        }

        void DispatchOther(in CollisionHit mirroredHit, in HitFrameMeta meta)
        {
            var self = mirroredHit.Self;
            if (!self.IsValid)
                return;

            int id = self.Id;
            if ((uint)id >= (uint)_bucketsById.Length)
            {
                _droppedOutOfBounds++;
                return;
            }

            var b = _bucketsById[id];
            if (b == null)
                return;

            TouchBucket(id, meta.FrameIndex, b);
            b.Dispatch(in mirroredHit, in meta, isOtherSide: true);
        }

        static CollisionHit MirrorDynDyn(in CollisionHit hit)
        {
            // Swap Self/OtherDynamic and invert normal.
            var m = hit;
            var tmpHandle = m.Self;
            m.Self = m.OtherDynamic;
            m.OtherDynamic = tmpHandle;

            var tmpSet = m.SelfSetId;
            m.SelfSetId = m.OtherSetId;
            m.OtherSetId = tmpSet;

            var tmpLayer = m.SelfLayerBit;
            m.SelfLayerBit = m.OtherLayerBit;
            m.OtherLayerBit = tmpLayer;

            m.Normal = -m.Normal;
            return m;
        }

        void EnsureCapacity(int requiredId)
        {
            if (requiredId < 0)
                return;

            if (requiredId < _bucketsById.Length)
                return;

            int newLen = _bucketsById.Length == 0 ? 64 : _bucketsById.Length;
            while (newLen <= requiredId)
                newLen = newLen < 1024 ? newLen * 2 : newLen + 1024;

            int oldLen = _bucketsById.Length;
            var next = new WatcherBucket?[newLen];
            Array.Copy(_bucketsById, next, oldLen);
            _bucketsById = next;
        }

        WatcherBucket GetOrCreateBucket(int id)
        {
            EnsureCapacity(id);
            var b = _bucketsById[id];
            if (b != null)
                return b;
            b = new WatcherBucket();
            _bucketsById[id] = b;
            return b;
        }

        bool TryGetBucket(int id, out WatcherBucket? bucket)
        {
            bucket = null;
            if ((uint)id >= (uint)_bucketsById.Length)
                return false;
            bucket = _bucketsById[id];
            return bucket != null;
        }

        void EnqueuePending(PendingOpKind kind, DynamicColliderHandle self, HitColliderChannelRuntime runtime, HitWatchFlags flags)
        {
            if (_pendingOps.Length == _pendingCount)
            {
                var next = new PendingOp[Math.Max(8, _pendingOps.Length * 2)];
                Array.Copy(_pendingOps, next, _pendingCount);
                _pendingOps = next;
            }

            _pendingOps[_pendingCount++] = new PendingOp
            {
                Kind = kind,
                Self = self,
                Runtime = runtime,
                Flags = flags,
            };
        }

        void ApplyPending()
        {
            for (int i = 0; i < _pendingCount; i++)
            {
                var op = _pendingOps[i];
                if (!op.Self.IsValid || op.Runtime == null)
                    continue;

                var id = op.Self.Id;

                switch (op.Kind)
                {
                    case PendingOpKind.Register:
                        GetOrCreateBucket(id).Register(op.Self, op.Runtime, op.Flags);
                        break;
                    case PendingOpKind.Unregister:
                        if (TryGetBucket(id, out var b0) && b0 != null)
                            b0.Unregister(op.Runtime, op.Self.Generation);
                        break;
                    case PendingOpKind.UpdateFlags:
                        if (TryGetBucket(id, out var b1) && b1 != null)
                            b1.UpdateFlags(op.Runtime, op.Self.Generation, op.Flags);
                        break;
                }
            }

            _pendingCount = 0;
        }

        void TouchBucket(int id, int frameIndex, WatcherBucket bucket)
        {
            if (bucket.LastTouchedFrame == frameIndex)
                return;

            bucket.LastTouchedFrame = frameIndex;

            if (_touched.Length == _touchedCount)
            {
                var next = new int[Math.Max(32, _touched.Length * 2)];
                Array.Copy(_touched, next, _touchedCount);
                _touched = next;
            }

            _touched[_touchedCount++] = id;
        }

        enum PendingOpKind : byte
        {
            Register,
            Unregister,
            UpdateFlags,
        }

        struct PendingOp
        {
            public PendingOpKind Kind;
            public DynamicColliderHandle Self;
            public HitColliderChannelRuntime Runtime;
            public HitWatchFlags Flags;
        }

        struct Watcher
        {
            public int Generation;
            public HitColliderChannelRuntime Runtime;
            public HitWatchFlags Flags;

            public int Failures;
            public int BackoffSteps;
            public int NextAllowedFrame;
            public bool IsDead;
        }

        sealed class WatcherBucket : IDisposable
        {
            const int InlineCapacity = 2;

            public int LastTouchedFrame = -1;

            int _count;
            Watcher _w0;
            Watcher _w1;
            Watcher[]? _more;

            // Track per-other last/this-frame hits to compute Enter/Stay/Exit
            Dictionary<int, CollisionHit>? _prevHits;
            Dictionary<int, CollisionHit>? _currHits;

            public void Register(DynamicColliderHandle self, HitColliderChannelRuntime runtime, HitWatchFlags flags)
            {
                if (flags == HitWatchFlags.None)
                    return;

                // Update existing
                for (int i = 0; i < _count; i++)
                {
                    ref var w = ref GetRef(i);
                    if (w.Runtime == runtime)
                    {
                        w.Generation = self.Generation;
                        w.Flags = flags;
                        w.IsDead = false;
                        w.Failures = 0;
                        w.BackoffSteps = 0;
                        w.NextAllowedFrame = 0;
                        return;
                    }
                }

                // Add
                if (_count < InlineCapacity)
                {
                    ref var w = ref GetInlineRef(_count);
                    w = new Watcher
                    {
                        Generation = self.Generation,
                        Runtime = runtime,
                        Flags = flags,
                    };
                    _count++;
                    return;
                }

                EnsureMore(_count + 1);
                _more![_count - InlineCapacity] = new Watcher
                {
                    Generation = self.Generation,
                    Runtime = runtime,
                    Flags = flags,
                };
                _count++;
            }

            public void Unregister(HitColliderChannelRuntime runtime, int generation)
            {
                for (int i = 0; i < _count; i++)
                {
                    ref var w = ref GetRef(i);
                    if (w.Runtime == runtime)
                    {
                        w.IsDead = true;
                        w.Flags = HitWatchFlags.None;
                        return;
                    }
                }
            }

            public void UpdateFlags(HitColliderChannelRuntime runtime, int generation, HitWatchFlags flags)
            {
                for (int i = 0; i < _count; i++)
                {
                    ref var w = ref GetRef(i);
                    if (w.Runtime == runtime)
                    {
                        w.Generation = generation;
                        w.Flags = flags;
                        w.Failures = 0;
                        w.BackoffSteps = 0;
                        w.NextAllowedFrame = 0;
                        w.IsDead = flags == HitWatchFlags.None;
                        return;
                    }
                }
            }

            public void Dispatch(in CollisionHit hit, in HitFrameMeta meta, bool isOtherSide)
            {
                var self = hit.Self;

                // compute a compact other-key: positive = dynamic (id+1), negative = -(staticId+1)
                int otherKey;
                if (hit.OtherDynamic.IsValid)
                    otherKey = hit.OtherDynamic.Id + 1;
                else if (hit.OtherStatic.IsValid)
                    otherKey = -(hit.OtherStatic.Id + 1);
                else
                    otherKey = int.MinValue; // boundary

                // ensure maps
                _currHits ??= new Dictionary<int, CollisionHit>();
                _prevHits ??= new Dictionary<int, CollisionHit>();

                _currHits[otherKey] = hit;

                bool existed = _prevHits.ContainsKey(otherKey);
                var evt = existed ? HitEventType.Stay : HitEventType.Enter;
                var routed = new RoutedHit(in hit, in meta, isOtherSide, evt);

                for (int i = 0; i < _count; i++)
                {
                    ref var w = ref GetRef(i);

                    if (w.IsDead)
                        continue;

                    // generation mismatch => mark dead
                    if (w.Generation != self.Generation)
                    {
                        w.IsDead = true;
                        continue;
                    }

                    // other-side delivery is gated by watcher flags
                    if (isOtherSide && (w.Flags & HitWatchFlags.SelfAndOther) == 0)
                        continue;

                    if (w.Failures > 0 && meta.FrameIndex < w.NextAllowedFrame)
                        continue;

                    try
                    {
                        w.Runtime.OnRoutedHit(in routed);
                    }
                    catch (Exception ex)
                    {
                        w.Failures++;
                        if (w.Failures == 1)
                        {
                            w.BackoffSteps = 0;
                            Debug.LogWarning($"[CollisionHitRouter] Listener fault; entering backoff. scope={w.Runtime}");
                        }

                        if (w.Failures > 50)
                        {
                            w.IsDead = true;
                            Debug.LogError("[CollisionHitRouter] Listener hard-killed (too many failures).");
                        }
                        else
                        {
                            w.BackoffSteps = Math.Min(w.BackoffSteps + 1, 10);
                            int delay = 1 << w.BackoffSteps;
                            w.NextAllowedFrame = meta.FrameIndex + delay;
                        }

                        Debug.LogException(ex);
                    }
                }
            }

            /// <summary>
            /// Called at end of frame to emit Exit events for previous hits that did not appear this frame.
            /// </summary>
            public void EndFrame(in HitFrameMeta meta)
            {
                if ((_prevHits == null || _prevHits.Count == 0) && (_currHits == null || _currHits.Count == 0))
                {
                    // nothing to do
                    _prevHits = _currHits;
                    _currHits = null;
                    return;
                }

                if (_prevHits != null && _prevHits.Count > 0)
                {
                    foreach (var kv in _prevHits)
                    {
                        int otherKey = kv.Key;
                        if (_currHits != null && _currHits.ContainsKey(otherKey))
                            continue; // still colliding

                        var prevHit = kv.Value;

                        // emit Exit event
                        var routed = new RoutedHit(in prevHit, in meta, isOtherSide: false, HitEventType.Exit);

                        // deliver to all watchers (same faults/backoff rules apply)
                        for (int i = 0; i < _count; i++)
                        {
                            ref var w = ref GetRef(i);

                            if (w.IsDead)
                                continue;

                            // generation mismatch => mark dead
                            if (w.Generation != prevHit.Self.Generation)
                            {
                                w.IsDead = true;
                                continue;
                            }

                            // other-side delivery is gated by watcher flags; Exit is self-side by definition here
                            if (w.Failures > 0 && meta.FrameIndex < w.NextAllowedFrame)
                                continue;

                            try
                            {
                                w.Runtime.OnRoutedHit(in routed);
                            }
                            catch (Exception ex)
                            {
                                w.Failures++;
                                if (w.Failures == 1)
                                {
                                    w.BackoffSteps = 0;
                                    Debug.LogWarning($"[CollisionHitRouter] Listener fault; entering backoff. scope={w.Runtime}");
                                }

                                if (w.Failures > 50)
                                {
                                    w.IsDead = true;
                                    Debug.LogError("[CollisionHitRouter] Listener hard-killed (too many failures).");
                                }
                                else
                                {
                                    w.BackoffSteps = Math.Min(w.BackoffSteps + 1, 10);
                                    int delay = 1 << w.BackoffSteps;
                                    w.NextAllowedFrame = meta.FrameIndex + delay;
                                }

                                Debug.LogException(ex);
                            }
                        }
                    }
                }

                // rotate maps for next frame
                _prevHits = _currHits ?? new Dictionary<int, CollisionHit>();
                _currHits = null;
            }

            public void Compact()
            {
                if (_count == 0)
                    return;

                int write = 0;
                for (int read = 0; read < _count; read++)
                {
                    ref var w = ref GetRef(read);
                    if (w.IsDead || w.Flags == HitWatchFlags.None || w.Runtime == null)
                        continue;

                    if (write != read)
                        GetInlineOrMoreRef(write) = w;

                    write++;
                }

                // clear tail
                for (int i = write; i < _count; i++)
                    GetInlineOrMoreRef(i) = default;

                _count = write;

                if (_more != null && _count <= InlineCapacity)
                {
                    ArrayPool<Watcher>.Shared.Return(_more, clearArray: true);
                    _more = null;
                }
                else if (_more != null)
                {
                    int needed = _count - InlineCapacity;
                    if (needed <= 0)
                        return;

                    // shrink if very sparse
                    if (_more.Length >= needed * 2)
                    {
                        var next = ArrayPool<Watcher>.Shared.Rent(needed);
                        Array.Copy(_more, 0, next, 0, needed);
                        ArrayPool<Watcher>.Shared.Return(_more, clearArray: true);
                        _more = next;
                    }
                }
            }

            void EnsureMore(int desiredTotal)
            {
                int desiredMore = desiredTotal - InlineCapacity;
                if (desiredMore <= 0)
                    return;

                if (_more == null)
                {
                    _more = ArrayPool<Watcher>.Shared.Rent(desiredMore);
                    return;
                }

                if (_more.Length >= desiredMore)
                    return;

                var next = ArrayPool<Watcher>.Shared.Rent(Math.Max(desiredMore, _more.Length * 2));
                Array.Copy(_more, 0, next, 0, _more.Length);
                ArrayPool<Watcher>.Shared.Return(_more, clearArray: true);
                _more = next;
            }

            ref Watcher GetRef(int index)
            {
                if (index < InlineCapacity)
                    return ref GetInlineRef(index);

                return ref _more![index - InlineCapacity];
            }

            ref Watcher GetInlineRef(int index)
            {
                if (index == 0)
                    return ref _w0;

                return ref _w1;
            }

            ref Watcher GetInlineOrMoreRef(int index)
            {
                if (index < InlineCapacity)
                    return ref GetInlineRef(index);

                return ref _more![index - InlineCapacity];
            }

            public void Dispose()
            {
                if (_more != null)
                {
                    ArrayPool<Watcher>.Shared.Return(_more, clearArray: true);
                    _more = null;
                }
                _w0 = default;
                _w1 = default;
                _count = 0;
            }
        }
    }
}
