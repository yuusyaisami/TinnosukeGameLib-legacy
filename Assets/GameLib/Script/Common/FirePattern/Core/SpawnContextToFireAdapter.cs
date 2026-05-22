#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Search;
using Game.Spawn;
using Game.Targeting;
using Unity.Mathematics;
using UnityEngine;
using VContainer;

namespace Game.Fire
{
    public enum DynamicSearchHitAcquireMode
    {
        TargetChannelHub = 0,
        DynamicSearchQuery = 1,
    }

    public readonly struct DynamicSearchHitQuerySettings
    {
        public readonly float Radius;
        public readonly LifetimeScopeMask KindMask;
        public readonly bool RequireActive;
        public readonly string FilterId;
        public readonly string FilterCategory;

        public DynamicSearchHitQuerySettings(
            float radius,
            LifetimeScopeMask kindMask,
            bool requireActive,
            string filterId,
            string filterCategory)
        {
            Radius = radius;
            KindMask = kindMask;
            RequireActive = requireActive;
            FilterId = filterId ?? "";
            FilterCategory = filterCategory ?? "";
        }
    }

    public sealed class SpawnContextToFireAdapter :
        ISpawnContextConsumer,
        IFirePatternOverrideReceiver,
        IScopeAcquireHandler,
        IScopeReleaseHandler,
        IDisposable
    {
        readonly IFirePatternService _fireService;
        ITargetChannelHub? _targetHub;
        IDynamicSearchService? _searchService;

        readonly BaseFirePattern[] _defaultPatterns;
        BaseFirePattern[] _patterns;
        readonly DynamicSearchHitAcquireMode _hitAcquireMode;
        readonly string _targetChannelTag;
        readonly DynamicSearchHitQuerySettings _query;

        readonly List<DynamicSearchHit> _queryBuffer = new(32);

        IScopeNode? _scope;
        CancellationTokenSource? _cts;
        bool _disposed;

        public SpawnContextToFireAdapter(
            IFirePatternService fireService,
            BaseFirePattern[] patterns,
            DynamicSearchHitAcquireMode hitAcquireMode,
            string targetChannelTag,
            DynamicSearchHitQuerySettings query)
        {
            _fireService = fireService ?? throw new ArgumentNullException(nameof(fireService));
            _defaultPatterns = patterns ?? Array.Empty<BaseFirePattern>();
            _patterns = _defaultPatterns;
            _hitAcquireMode = hitAcquireMode;
            _targetChannelTag = targetChannelTag ?? "";
            _query = query;
            // Do NOT resolve optional services in constructor. They will be TryResolve'd
            // from the scope's resolver during OnAcquire via EnsureServicesResolved(IRuntimeResolver?).
        }

        public void SetOverridePattern(BaseFirePattern? pattern)
        {
            if (_disposed)
                return;

            _patterns = pattern != null ? new[] { pattern } : _defaultPatterns;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            if (_disposed)
                return;

            _scope = scope;

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            // IMPORTANT:
            // Do NOT try to find/register to an emitter via IScopeNode.Parent.
            // Spawned KernelScopeHost hierarchy is not guaranteed to match emitter ownership.
            // EmitterService already registers all ISpawnContextConsumer from the spawned unit resolver.
            EnsureServicesResolved(scope.Resolver);

            // Reset overrides on (re)acquire. Template hook runs after this, so it can override again.
            _patterns = _defaultPatterns;
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            if (_disposed)
                return;

            _scope = null;

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            _queryBuffer.Clear();
            _patterns = _defaultPatterns;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _scope = null;

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            _queryBuffer.Clear();
            _patterns = _defaultPatterns;
        }

        public void OnSpawnContextReceived(in UnitSpawnContext context)
        {
            if (_disposed)
                return;

            if (_patterns == null || _patterns.Length == 0)
                return;

            var token = _cts?.Token ?? CancellationToken.None;
            var ctx = context; // copy 'in' param to local to allow capture in lambda
            var hits = ResolveHits(in ctx);

            UniTask.Void(async () =>
            {
                try
                {
                    await _fireService.ExecuteAsync(_patterns, ctx, hits, token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SpawnContextToFireAdapter] ExecuteAsync failed: {ex.Message}");
                }
            });
        }

        IReadOnlyList<DynamicSearchHit> ResolveHits(in UnitSpawnContext context)
        {
            // Don't rely on OnAcquire being called; resolve services lazily from the unit's resolver.
            EnsureServicesResolved(context.UnitResolver);

            switch (_hitAcquireMode)
            {
                case DynamicSearchHitAcquireMode.TargetChannelHub:
                    return ResolveHitsFromTargetHub();

                case DynamicSearchHitAcquireMode.DynamicSearchQuery:
                    return ResolveHitsFromSearch(in context);

                default:
                    return Array.Empty<DynamicSearchHit>();
            }
        }

        IReadOnlyList<DynamicSearchHit> ResolveHitsFromTargetHub()
        {
            if (_targetHub == null)
                return Array.Empty<DynamicSearchHit>();

            if (!_targetHub.TryGetRuntime(_targetChannelTag, out var runtime) || runtime == null)
                return Array.Empty<DynamicSearchHit>();

            var hits = runtime.Hits;
            return hits as IReadOnlyList<DynamicSearchHit> ?? Array.Empty<DynamicSearchHit>();
        }

        IReadOnlyList<DynamicSearchHit> ResolveHitsFromSearch(in UnitSpawnContext context)
        {
            _queryBuffer.Clear();

            if (_searchService == null)
                return _queryBuffer;

            var origin = ResolveUnitPosition(context.UnitResolver, context.Base.Position);

            string? filterId = string.IsNullOrEmpty(_query.FilterId) ? null : _query.FilterId;
            string? filterCategory = string.IsNullOrEmpty(_query.FilterCategory) ? null : _query.FilterCategory;

            var q = new DynamicSearchQuery(
                origin: new float2(origin.x, origin.y),
                radius: _query.Radius,
                kindMask: _query.KindMask,
                requireActive: _query.RequireActive,
                filterId: filterId,
                filterCategory: filterCategory);

            _searchService.Query(in q, _queryBuffer);
            return _queryBuffer;
        }

        static Vector3 ResolveUnitPosition(IRuntimeResolver resolver, Vector3 fallback)
        {
            if (resolver != null)
            {
                var handle = ScopeFeatureInstallerUtility.CaptureSpawnedLifetime(resolver);
                if (handle.Root != null)
                    return handle.Root.transform.position;
            }

            return fallback;
        }

        void EnsureServicesResolved(IRuntimeResolver? resolver)
        {
            if (resolver == null)
                return;

            if (_targetHub == null && resolver.TryResolve<ITargetChannelHub>(out var hub) && hub != null)
                _targetHub = hub;

            if (_searchService == null && resolver.TryResolve<IDynamicSearchService>(out var ss) && ss != null)
                _searchService = ss;
        }
    }
}


