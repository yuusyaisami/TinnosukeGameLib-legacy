#nullable enable
using System;
using System.Collections.Generic;
using Game;
using VNext = Game.Commands.VNext;
using Game.Common;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.Collision
{
    // Per-spec runtime (Hub-managed)
    /// <summary>
    /// 1つの HitContactWatchSpec (= 1つの判定ルール) を担当する Runtime。
    /// Hub から生成され、Router に self handle 単位で登録される。
    /// </summary>
    public sealed class HitColliderChannelRuntime : IDisposable
    {
        readonly IHitColliderChannelRouter? _router;
        readonly DynamicColliderHandle _self;
        readonly HitContactWatchSpec _spec;

        bool _registered;

        readonly HashSet<StaticColliderHandle> _staticContacts = new();
        readonly HashSet<DynamicColliderHandle> _dynamicContacts = new();

        readonly Dictionary<StaticColliderHandle, int> _staticLastSeen = new();
        readonly Dictionary<DynamicColliderHandle, int> _dynamicLastSeen = new();

        readonly List<StaticColliderHandle> _staticRemoveScratch = new(8);
        readonly List<DynamicColliderHandle> _dynamicRemoveScratch = new(8);

        int _lastCleanupFrameIndex = -1;

        int _cachedFrameCount = -1;
        int _cachedHitCount;

        public event Action<HitContactEvent>? Enter;
        public event Action<HitContactEvent>? Stay;
        public event Action<HitContactEvent>? Exit;
        public event Action<HitContactState>? StateChanged;

        public HitColliderChannelRuntime(IHitColliderChannelRouter router, DynamicColliderHandle self, in HitContactWatchSpec spec)
        {
            _router = router;
            _self = self;
            _spec = spec;

            if (_router != null && _self.IsValid)
            {
                _router.RegisterWatcher(_self, this, _spec.WatchFlags);
                _registered = true;
            }
        }

        public int GetHitCount()
        {
            int frame = Time.frameCount;
            if (_cachedFrameCount == frame)
                return _cachedHitCount;

            _cachedFrameCount = frame;
            _cachedHitCount = _staticContacts.Count + _dynamicContacts.Count;
            return _cachedHitCount;
        }

        public bool HasAnyHit => GetHitCount() > 0;

        public int FillDynamicContacts(List<DynamicColliderHandle> dst)
        {
            if (dst == null)
                return 0;

            foreach (var handle in _dynamicContacts)
            {
                if (handle.IsValid)
                    dst.Add(handle);
            }

            return _dynamicContacts.Count;
        }

        internal void OnRoutedHit(in RoutedHit routedHit)
        {
            if (!_spec.Matches(routedHit))
                return;

            if (!HitContact.TryCreate(routedHit, out var contact))
                return;

            if (_spec.StaleFrameThreshold >= 1)
            {
                int frameIndex = routedHit.Meta.FrameIndex;
                if (frameIndex != _lastCleanupFrameIndex)
                {
                    CleanupStale(frameIndex);
                    _lastCleanupFrameIndex = frameIndex;
                }
            }

            int before = _staticContacts.Count + _dynamicContacts.Count;

            switch (routedHit.Event)
            {
                case HitEventType.Enter:
                    HandleEnterOrStay(in contact, in routedHit, isStay: false);
                    break;
                case HitEventType.Stay:
                    HandleEnterOrStay(in contact, in routedHit, isStay: true);
                    break;
                case HitEventType.Exit:
                    HandleExit(in contact, in routedHit);
                    break;
            }

            int after = _staticContacts.Count + _dynamicContacts.Count;
            if (after != before)
                StateChanged?.Invoke(new HitContactState(_staticContacts.Count, _dynamicContacts.Count));

            _cachedFrameCount = -1;
        }

        void HandleEnterOrStay(in HitContact contact, in RoutedHit rh, bool isStay)
        {
            int frameIndex = rh.Meta.FrameIndex;

            if (contact.IsStatic)
            {
                var key = contact.Static;
                bool isNew = _staticContacts.Add(key);
                _staticLastSeen[key] = frameIndex;

                var evt = new HitContactEvent(in contact, in rh);
                if (isStay && !isNew) Stay?.Invoke(evt);
                else Enter?.Invoke(evt);
                return;
            }

            if (contact.IsDynamic)
            {
                var key = contact.Dynamic;
                bool isNew = _dynamicContacts.Add(key);
                _dynamicLastSeen[key] = frameIndex;

                var evt = new HitContactEvent(in contact, in rh);
                if (isStay && !isNew) Stay?.Invoke(evt);
                else Enter?.Invoke(evt);
            }
        }

        void HandleExit(in HitContact contact, in RoutedHit rh)
        {
            if (contact.IsStatic)
            {
                var key = contact.Static;
                bool removed = _staticContacts.Remove(key);
                _staticLastSeen.Remove(key);
                if (removed)
                    Exit?.Invoke(new HitContactEvent(in contact, in rh));
                return;
            }

            if (contact.IsDynamic)
            {
                var key = contact.Dynamic;
                bool removed = _dynamicContacts.Remove(key);
                _dynamicLastSeen.Remove(key);
                if (removed)
                    Exit?.Invoke(new HitContactEvent(in contact, in rh));
            }
        }

        void CleanupStale(int frameIndex)
        {
            int before = _staticContacts.Count + _dynamicContacts.Count;
            int threshold = frameIndex - _spec.StaleFrameThreshold;

            _staticRemoveScratch.Clear();
            foreach (var kv in _staticLastSeen)
            {
                if (kv.Value < threshold)
                    _staticRemoveScratch.Add(kv.Key);
            }
            for (int i = 0; i < _staticRemoveScratch.Count; i++)
            {
                var k = _staticRemoveScratch[i];
                _staticContacts.Remove(k);
                _staticLastSeen.Remove(k);
            }

            _dynamicRemoveScratch.Clear();
            foreach (var kv in _dynamicLastSeen)
            {
                if (kv.Value < threshold)
                    _dynamicRemoveScratch.Add(kv.Key);
            }
            for (int i = 0; i < _dynamicRemoveScratch.Count; i++)
            {
                var k = _dynamicRemoveScratch[i];
                _dynamicContacts.Remove(k);
                _dynamicLastSeen.Remove(k);
            }

            int after = _staticContacts.Count + _dynamicContacts.Count;
            if (after != before)
            {
                StateChanged?.Invoke(new HitContactState(_staticContacts.Count, _dynamicContacts.Count));
                _cachedFrameCount = -1;
            }
        }

        public void Dispose()
        {
            if (_registered && _router != null)
            {
                try { _router.UnregisterWatcher(_self, this); } catch { }
                _registered = false;
            }

            _staticContacts.Clear();
            _dynamicContacts.Clear();
            _staticLastSeen.Clear();
            _dynamicLastSeen.Clear();
            _staticRemoveScratch.Clear();
            _dynamicRemoveScratch.Clear();
        }
    }
}
