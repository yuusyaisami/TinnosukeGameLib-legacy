#nullable enable
// Game.Targeting
// ================================================================================
// TargetChannelScopeRuntime - LifetimeScope search-based TargetChannel runtime
// ================================================================================

using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Game.Common;
using Game.Search;
using VContainer;
using VNext = Game.Commands.VNext;

namespace Game.Targeting
{
    /// <summary>
    /// TargetChannel runtime that resolves targets via LifetimeScope lookup.
    /// ActorSource is used to resolve scopes and cache results.
    /// </summary>
    public sealed class TargetChannelScopeRuntime : ITargetChannelRuntime
    {
        readonly TargetChannelOwner _owner;
        readonly TargetChannelDef _def;
        readonly List<DynamicSearchHit> _hits;

        int _lastUpdatedFrame = int.MinValue;

        public TargetChannelScopeRuntime(in TargetChannelOwner owner, TargetChannelDef def)
        {
            _owner = owner;
            _def = def ?? throw new System.ArgumentNullException(nameof(def));
            _hits = new List<DynamicSearchHit>(Mathf.Max(0, def.ExpectedResultCount));
        }

        public string Tag => _def.Tag;

        public bool Enabled
        {
            get => _def.Enabled;
            set => _def.Enabled = value;
        }

        public int LastUpdatedFrame => _lastUpdatedFrame;

        public List<DynamicSearchHit> Hits
        {
            get
            {
                EnsureUpdated(ignoreInterval: false);
                return _hits;
            }
        }

        public void Invalidate()
        {
            _lastUpdatedFrame = int.MinValue;
        }

        public void ForceRefresh()
        {
            EnsureUpdated(ignoreInterval: true);
        }

        void EnsureUpdated(bool ignoreInterval)
        {
            MainThread.AssertMainThread();

            if (!_def.Enabled)
            {
                _hits.Clear();
                return;
            }

            int frame = Time.frameCount;
            if (frame == _lastUpdatedFrame)
                return;

            if (!ignoreInterval && _def.RefreshIntervalFrames > 1)
            {
                int delta = frame - _lastUpdatedFrame;
                if (delta > 0 && delta < _def.RefreshIntervalFrames)
                    return;
            }

            _lastUpdatedFrame = frame;
            _hits.Clear();

            CollectHits();

            if (_def.ExcludeSelf && _owner.OwnerScope != null)
            {
                var self = _owner.OwnerScope;
                for (int i = _hits.Count - 1; i >= 0; i--)
                {
                    if (ReferenceEquals(_hits[i].Scope, self))
                        _hits.RemoveAt(i);
                }
            }
        }

        void CollectHits()
        {
            if (_def.SearchType != TargetChannelSearchType.ScopeSearch)
                return;

            var source = _def.ActorSource;
            if (source.Kind == VNext.ActorSourceKind.ByIdentity)
            {
                if (!TryResolveScopeRegistry(out var registry) || registry == null)
                    return;

                var scopes = registry.ResolveAll(source.Identity, _owner.OwnerScope);
                if (scopes == null || scopes.Count == 0)
                    return;

                var origin = ResolveOwnerOrigin();
                for (int i = 0; i < scopes.Count; i++)
                {
                    AddScopeHit(scopes[i], origin, requireActive: false);
                }

                return;
            }

            if (TryResolveScope(source, out var scope) && scope != null)
            {
                var origin = ResolveOwnerOrigin();
                AddScopeHit(scope, origin, requireActive: _def.ScopeRequireActive);
            }
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

        float2 ResolveOwnerOrigin()
        {
            var t = _owner.OwnerTransform;
            var p = t.position;
            return new float2(p.x, p.y);
        }

        static bool TryResolveScopePosition(IScopeNode scope, ILTSIdentityService identity, out float2 pos)
        {
            pos = default;

            if (identity != null && identity.SelfTransform != null)
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

        bool TryResolveScope(VNext.ActorSource source, out IScopeNode? scope)
        {
            scope = null;
            switch (source.Kind)
            {
                case VNext.ActorSourceKind.Current:
                    scope = _owner.OwnerScope;
                    return scope != null;
                case VNext.ActorSourceKind.Parent:
                    scope = _owner.OwnerScope?.Parent;
                    return scope != null;
                case VNext.ActorSourceKind.Root:
                    {
                        var path = _owner.OwnerScope?.GetPathFromRoot();
                        if (path == null || path.Count == 0)
                            return false;
                        scope = path[0];
                        return scope != null;
                    }
                case VNext.ActorSourceKind.GameLogicRoot:
                    scope = ScopeNodeHierarchy.FindNearestGameLogicRoot(_owner.OwnerScope, includeSelf: true);
                    return scope != null;
                case VNext.ActorSourceKind.Player:
                    scope = VNext.ActorSourceFastResolver.Resolve(_owner.OwnerScope, source);
                    return scope != null;
                case VNext.ActorSourceKind.FromUnityObject:
                    return TryResolveFromUnityObject(source.UnityObject, out scope);
                default:
                    return false;
            }
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

        static bool TryResolveFromUnityObject(UnityEngine.Object? obj, out IScopeNode? scope)
        {
            scope = null;
            if (obj == null)
                return false;

            if (obj is IScopeNode node)
            {
                scope = node;
                return true;
            }

            if (obj is Component comp)
            {
                scope = FindScopeNode(comp.gameObject);
                return scope != null;
            }

            if (obj is GameObject go)
            {
                scope = FindScopeNode(go);
                return scope != null;
            }

            return false;
        }

        static IScopeNode? FindScopeNode(GameObject go)
        {
            if (go == null)
                return null;

            var baseScope = go.GetComponentInParent<BaseLifetimeScope>();
            if (baseScope != null)
                return baseScope;

            var runtimeScope = go.GetComponentInParent<RuntimeLifetimeScope>();
            if (runtimeScope != null)
                return runtimeScope;

            return null;
        }
    }
}
