#nullable enable

using System;
using System.Collections.Generic;
using Game.Common;
using Game.Entity;
using Game.Search;
using Unity.Mathematics;
using UnityEngine;
using VContainer;
using VNext = Game.Commands.VNext;

namespace Game.Targeting
{
    public sealed class TargetChannelRuntime : ITargetChannelRuntime
    {
        readonly IDynamicSearchService? _search;
        readonly TargetChannelOwner _owner;
        readonly List<DynamicSearchHit> _hits;
        readonly List<DynamicSearchHit> _directHits;

        TargetChannelPreset _basePreset;
        TargetChannelPreset _currentPreset;
        int _lastUpdatedFrame = int.MinValue;

        public TargetChannelRuntime(IDynamicSearchService? search, in TargetChannelOwner owner, TargetChannelPreset preset)
        {
            _search = search;
            _owner = owner;
            _basePreset = preset?.CreateRuntimeCopy() ?? throw new ArgumentNullException(nameof(preset));
            _currentPreset = _basePreset.CreateRuntimeCopy();
            _hits = new List<DynamicSearchHit>(Mathf.Max(0, _currentPreset.ExpectedResultCount));
            _directHits = new List<DynamicSearchHit>(Mathf.Max(0, _currentPreset.ExpectedResultCount));
        }

        public string Tag => _currentPreset.Tag;
        public bool Enabled
        {
            get => _currentPreset.Enabled;
            set
            {
                _currentPreset.Enabled = value;
                if (!value)
                    _hits.Clear();
            }
        }

        public TargetChannelPreset CurrentPreset => _currentPreset;
        public int LastUpdatedFrame => _lastUpdatedFrame;
        public List<DynamicSearchHit> Hits
        {
            get
            {
                EnsureUpdated(ignoreInterval: false);
                PruneInvalidHits();
                return _hits;
            }
        }

        public void Invalidate()
        {
            _lastUpdatedFrame = int.MinValue;
            if (!_currentPreset.IsNoneSearch)
                _hits.Clear();
        }

        public void ForceRefresh()
        {
            EnsureUpdated(ignoreInterval: true);
        }

        public bool SwapPreset(TargetChannelPreset preset)
        {
            if (preset == null || string.IsNullOrWhiteSpace(preset.Tag))
                return false;

            _basePreset = preset.CreateRuntimeCopy();
            _currentPreset = _basePreset.CreateRuntimeCopy();
            _directHits.Clear();
            ResizeHitCapacity();
            Invalidate();
            return true;
        }

        public bool MutateSettings(TargetChannelRuntimeMutation mutation)
        {
            if (mutation == null || !mutation.HasAnyMutation())
                return false;

            _currentPreset.ApplyMutation(mutation);
            ResizeHitCapacity();
            Invalidate();
            return true;
        }

        public bool ResetRuntimeOverrides()
        {
            _currentPreset = _basePreset.CreateRuntimeCopy();
            _directHits.Clear();
            ResizeHitCapacity();
            Invalidate();
            return true;
        }

        public bool SetDirectTargets(IReadOnlyList<DynamicSearchHit> hits)
        {
            if (!_currentPreset.IsNoneSearch)
                return false;

            _directHits.Clear();
            if (hits != null)
            {
                for (int i = 0; i < hits.Count; i++)
                    _directHits.Add(hits[i]);
            }

            _lastUpdatedFrame = Time.frameCount;
            _hits.Clear();
            _hits.AddRange(_directHits);
            ApplySelfExclusion();
            return true;
        }

        public bool ClearDirectTargets()
        {
            if (!_currentPreset.IsNoneSearch)
                return false;

            var changed = _directHits.Count > 0 || _hits.Count > 0;
            _directHits.Clear();
            _hits.Clear();
            _lastUpdatedFrame = Time.frameCount;
            return changed;
        }

        void EnsureUpdated(bool ignoreInterval)
        {
            MainThread.AssertMainThread();

            if (!_currentPreset.Enabled)
            {
                _hits.Clear();
                return;
            }

            int frame = Time.frameCount;
            if (frame == _lastUpdatedFrame)
                return;

            if (!ignoreInterval && _currentPreset.RefreshIntervalFrames > 1)
            {
                int delta = frame - _lastUpdatedFrame;
                if (delta > 0 && delta < _currentPreset.RefreshIntervalFrames)
                    return;
            }

            _lastUpdatedFrame = frame;
            _hits.Clear();

            switch (_currentPreset.SearchType)
            {
                case TargetChannelSearchType.DynamicSearch:
                    CollectDynamicHits();
                    break;
                case TargetChannelSearchType.ScopeSearch:
                    CollectScopeHits();
                    break;
                case TargetChannelSearchType.None:
                    _hits.AddRange(_directHits);
                    break;
            }

            ApplySelfExclusion();
        }

        void PruneInvalidHits()
        {
            for (int i = _hits.Count - 1; i >= 0; i--)
            {
                if (!TargetChannelTargetPositionSourceHelper.IsHitAlive(_hits[i]))
                    _hits.RemoveAt(i);
            }

            if (!_currentPreset.IsNoneSearch)
                return;

            for (int i = _directHits.Count - 1; i >= 0; i--)
            {
                if (!TargetChannelTargetPositionSourceHelper.IsHitAlive(_directHits[i]))
                    _directHits.RemoveAt(i);
            }
        }

        void CollectDynamicHits()
        {
            if (_search == null)
            {
                Debug.LogError($"[TargetChannelRuntime] IDynamicSearchService not found for '{_currentPreset.Tag}'.");
                return;
            }

            float2 origin = ResolveOrigin();
            float radius = Mathf.Max(0.01f, _currentPreset.Radius);
            if (_currentPreset.Kind == TargetQueryKind.Cone)
            {
                var q = new DynamicSearchQuery(
                    origin,
                    radius,
                    ResolveForward(),
                    _currentPreset.CosHalfAngle,
                    kindMask: _currentPreset.KindMask,
                    requireActive: true,
                    filterId: _currentPreset.FilterId,
                    filterCategory: _currentPreset.FilterCategory);
                _search.Query(in q, _hits);
                return;
            }

            var query = new DynamicSearchQuery(
                origin,
                radius,
                kindMask: _currentPreset.KindMask,
                requireActive: true,
                filterId: _currentPreset.FilterId,
                filterCategory: _currentPreset.FilterCategory);
            _search.Query(in query, _hits);
        }

        void CollectScopeHits()
        {
            var source = _currentPreset.ActorSource;
            if (source.Kind == VNext.ActorSourceKind.ByIdentity)
            {
                if (!TryResolveScopeRegistry(out var registry) || registry == null)
                    return;

                var scopes = registry.ResolveAll(source.Identity, _owner.OwnerScope);
                if (scopes == null || scopes.Count == 0)
                    return;

                var origin = ResolveOwnerOrigin();
                for (int i = 0; i < scopes.Count; i++)
                    AddScopeHit(scopes[i], origin, requireActive: false);

                return;
            }

            var scope = VNext.ActorSourceFastResolver.Resolve(_owner.OwnerScope, source);
            if (scope == null)
                return;

            AddScopeHit(scope, ResolveOwnerOrigin(), _currentPreset.ScopeRequireActive);
        }

        void AddScopeHit(IScopeNode scope, float2 origin, bool requireActive)
        {
            if (scope == null)
                return;

            var identity = scope.Identity;
            if (identity == null)
                return;

            if (requireActive && !identity.IsActive)
                return;

            if (!TryResolveScopePosition(scope, identity, out var pos))
                return;

            var delta = pos - origin;
            float distSq = math.dot(delta, delta);
            _hits.Add(new DynamicSearchHit(scope, identity, distSq, pos));
        }

        void ApplySelfExclusion()
        {
            if (!_currentPreset.ExcludeSelf || _owner.OwnerScope == null)
                return;

            for (int i = _hits.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(_hits[i].Scope, _owner.OwnerScope))
                    _hits.RemoveAt(i);
            }
        }

        void ResizeHitCapacity()
        {
            var capacity = Mathf.Max(0, _currentPreset.ExpectedResultCount);
            if (_hits.Capacity < capacity)
                _hits.Capacity = capacity;
            if (_directHits.Capacity < capacity)
                _directHits.Capacity = capacity;
        }

        float2 ResolveOrigin()
        {
            switch (_currentPreset.OriginSource)
            {
                case TargetOriginSource.OwnerFoot:
                    {
                        var foot = _owner.ResolveFootTransform();
                        if (foot != null)
                        {
                            var p = foot.FootWorldPosition;
                            return new float2(p.x, p.y);
                        }

                        var ownerPos = _owner.OwnerTransform.position;
                        return new float2(ownerPos.x, ownerPos.y);
                    }

                case TargetOriginSource.CustomTransform:
                    {
                        var tr = _currentPreset.CustomOriginTransform != null ? _currentPreset.CustomOriginTransform : _owner.OwnerTransform;
                        var p = tr.position;
                        return new float2(p.x, p.y);
                    }

                default:
                    {
                        var p = _owner.OwnerTransform.position;
                        return new float2(p.x, p.y);
                    }
            }
        }

        float2 ResolveForward()
        {
            Vector2 f;
            switch (_currentPreset.ForwardSource)
            {
                case TargetForwardSource.OwnerTransformRight:
                    f = _owner.OwnerTransform.right;
                    break;
                case TargetForwardSource.CustomTransformUp:
                    f = (_currentPreset.CustomForwardTransform ?? _owner.OwnerTransform).up;
                    break;
                case TargetForwardSource.CustomTransformRight:
                    f = (_currentPreset.CustomForwardTransform ?? _owner.OwnerTransform).right;
                    break;
                case TargetForwardSource.CustomVector:
                    f = _currentPreset.CustomForwardVector;
                    break;
                default:
                    f = _owner.OwnerTransform.up;
                    break;
            }

            float lenSq = f.sqrMagnitude;
            if (lenSq < 0.000001f)
                f = Vector2.up;
            else
                f /= Mathf.Sqrt(lenSq);

            return new float2(f.x, f.y);
        }

        float2 ResolveOwnerOrigin()
        {
            var p = _owner.OwnerTransform.position;
            return new float2(p.x, p.y);
        }

        bool TryResolveScopeRegistry(out IBaseLifetimeScopeRegistry? registry)
        {
            var current = _owner.OwnerScope;
            while (current != null)
            {
                var resolver = current.Resolver;
                if (resolver != null && resolver.TryResolve<IBaseLifetimeScopeRegistry>(out var resolved) && resolved != null)
                {
                    registry = resolved;
                    return true;
                }

                current = current.Parent;
            }

            registry = null;
            return false;
        }

        static bool TryResolveScopePosition(IScopeNode scope, ILTSIdentityService identity, out float2 pos)
        {
            pos = default;
            if (identity.SelfTransform != null)
            {
                var p = identity.SelfTransform.position;
                pos = new float2(p.x, p.y);
                return true;
            }

            if (scope is Component comp)
            {
                var p = comp.transform.position;
                pos = new float2(p.x, p.y);
                return true;
            }

            return false;
        }
    }

    public sealed class TargetChannelScopeRuntime : ITargetChannelRuntime
    {
        readonly TargetChannelRuntime _runtime;

        public TargetChannelScopeRuntime(in TargetChannelOwner owner, TargetChannelPreset preset)
        {
            _runtime = new TargetChannelRuntime(search: null, owner, preset);
        }

        public string Tag => _runtime.Tag;
        public bool Enabled { get => _runtime.Enabled; set => _runtime.Enabled = value; }
        public TargetChannelPreset CurrentPreset => _runtime.CurrentPreset;
        public int LastUpdatedFrame => _runtime.LastUpdatedFrame;
        public List<DynamicSearchHit> Hits => _runtime.Hits;
        public void Invalidate() => _runtime.Invalidate();
        public void ForceRefresh() => _runtime.ForceRefresh();
        public bool SwapPreset(TargetChannelPreset preset) => _runtime.SwapPreset(preset);
        public bool MutateSettings(TargetChannelRuntimeMutation mutation) => _runtime.MutateSettings(mutation);
        public bool ResetRuntimeOverrides() => _runtime.ResetRuntimeOverrides();
        public bool SetDirectTargets(IReadOnlyList<DynamicSearchHit> hits) => _runtime.SetDirectTargets(hits);
        public bool ClearDirectTargets() => _runtime.ClearDirectTargets();
    }
}
