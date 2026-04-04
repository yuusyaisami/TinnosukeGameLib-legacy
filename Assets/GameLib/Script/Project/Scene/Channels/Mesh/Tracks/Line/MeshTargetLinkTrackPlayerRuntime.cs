#nullable enable
using System.Collections.Generic;
using Game.Commands.VNext;
using Game.Common;
using Game.Search;
using Game.Targeting;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Channel
{
    sealed class MeshTargetLinkTrackPlayerRuntime : IMeshTrackPlayerRuntime
    {
        readonly struct TargetCandidate
        {
            public readonly DynamicSearchHit Hit;
            public readonly int StableIndex;

            public TargetCandidate(in DynamicSearchHit hit, int stableIndex)
            {
                Hit = hit;
                StableIndex = stableIndex;
            }
        }

        readonly MeshTargetLinkTrackPlayerPreset _preset;
        readonly HashSet<IScopeNode> _seenScopes = new();

        ActorSourceResolveCache _selfCache;

        public MeshTargetLinkTrackPlayerRuntime(MeshTargetLinkTrackPlayerPreset preset)
        {
            _preset = preset;
        }

        public void Reset()
        {
            _selfCache = default;
            _seenScopes.Clear();
        }

        public bool TryEvaluate(MeshTrackEvaluationContext context, out MeshTrackPlayerEvaluation evaluation)
        {
            evaluation = new MeshMultiCenterLineEvaluation();

            var selfScope = ActorSourceFastResolver.ResolveCached(
                context.DynamicContext,
                _preset.SelfActorSource,
                ref _selfCache,
                context.Scope);
            if (selfScope == null)
                return false;

            if (!TryResolveScopePosition(selfScope, out var selfPosition))
                return false;

            if (!TryResolveRuntime(selfScope, out var runtime) || runtime == null)
                return false;

            var hits = runtime.Hits;
            if (hits == null || hits.Count == 0)
                return false;

            var candidates = ListPool<TargetCandidate>.Get();
            try
            {
                CollectCandidates(hits, selfScope, selfPosition, candidates);
                if (candidates.Count == 0)
                    return false;

                candidates.Sort(static (a, b) =>
                {
                    var distanceCompare = a.Hit.DistanceSq.CompareTo(b.Hit.DistanceSq);
                    if (distanceCompare != 0)
                        return distanceCompare;

                    return a.StableIndex.CompareTo(b.StableIndex);
                });

                var limit = ResolveTargetCountLimit(candidates.Count);
                if (limit <= 0)
                    return false;

                switch (_preset.Topology)
                {
                    case MeshTargetLinkTopology.ChainPath:
                        return BuildChainPath(selfPosition, candidates, limit, out evaluation);

                    case MeshTargetLinkTopology.ChainTree:
                        return BuildChainTree(selfPosition, candidates, limit, out evaluation);

                    default:
                        return BuildIndependent(selfPosition, candidates, limit, out evaluation);
                }
            }
            finally
            {
                ListPool<TargetCandidate>.Release(candidates);
            }
        }

        bool TryResolveRuntime(IScopeNode selfScope, out ITargetChannelRuntime? runtime)
        {
            var tag = string.IsNullOrWhiteSpace(_preset.TargetChannelTag)
                ? "default"
                : _preset.TargetChannelTag.Trim();

            return TargetChannelTargetPositionSourceHelper.TryResolveRuntimeFromScopeChain(selfScope, tag, out runtime);
        }

        void CollectCandidates(
            List<DynamicSearchHit> hits,
            IScopeNode selfScope,
            in float2 selfPosition,
            List<TargetCandidate> candidates)
        {
            _seenScopes.Clear();

            var stableIndex = 0;
            for (var i = 0; i < hits.Count; i++)
            {
                var hit = hits[i];
                if (!TargetChannelTargetPositionSourceHelper.IsHitAlive(hit))
                    continue;

                if (ReferenceEquals(hit.Scope, selfScope))
                    continue;

                if (!_seenScopes.Add(hit.Scope))
                    continue;

                if (!TryResolveHitPosition(hit, out var targetPosition))
                    continue;

                var delta = targetPosition - selfPosition;
                var distanceSq = math.dot(delta, delta);
                var normalized = new DynamicSearchHit(hit.Scope, hit.Identity, distanceSq, targetPosition);
                candidates.Add(new TargetCandidate(normalized, stableIndex));
                stableIndex++;
            }
        }

        int ResolveTargetCountLimit(int candidateCount)
        {
            var topN = Mathf.Max(0, _preset.TopN);
            if (topN <= 0)
                return candidateCount;

            return Mathf.Min(topN, candidateCount);
        }

        static bool BuildIndependent(
            in float2 selfPosition,
            List<TargetCandidate> candidates,
            int limit,
            out MeshTrackPlayerEvaluation evaluation)
        {
            var result = new MeshMultiCenterLineEvaluation();
            var selfPoint = new Vector2(selfPosition.x, selfPosition.y);

            for (var i = 0; i < limit; i++)
            {
                var line = new MeshCenterLineEvaluation
                {
                    Closed = false,
                    SmoothPath = false,
                    SmoothingSubdivisions = 1,
                };
                line.Points.Add(selfPoint);
                line.Points.Add(new Vector2(candidates[i].Hit.Position.x, candidates[i].Hit.Position.y));
                result.Lines.Add(line);
            }

            evaluation = result;
            return result.Lines.Count > 0;
        }

        static bool BuildChainPath(
            in float2 selfPosition,
            List<TargetCandidate> candidates,
            int limit,
            out MeshTrackPlayerEvaluation evaluation)
        {
            var chain = new MeshCenterLineEvaluation
            {
                Closed = false,
                SmoothPath = false,
                SmoothingSubdivisions = 1,
            };
            chain.Points.Add(new Vector2(selfPosition.x, selfPosition.y));

            for (var i = 0; i < limit; i++)
                chain.Points.Add(new Vector2(candidates[i].Hit.Position.x, candidates[i].Hit.Position.y));

            evaluation = chain;
            return chain.Points.Count >= 2;
        }

        static bool BuildChainTree(
            in float2 selfPosition,
            List<TargetCandidate> candidates,
            int limit,
            out MeshTrackPlayerEvaluation evaluation)
        {
            var result = new MeshMultiCenterLineEvaluation();
            var nodeCount = limit + 1;
            var nodes = new Vector2[nodeCount];
            var selected = new bool[nodeCount];

            nodes[0] = new Vector2(selfPosition.x, selfPosition.y);
            for (var i = 0; i < limit; i++)
                nodes[i + 1] = new Vector2(candidates[i].Hit.Position.x, candidates[i].Hit.Position.y);

            selected[0] = true;
            var selectedCount = 1;

            while (selectedCount < nodeCount)
            {
                var bestFrom = -1;
                var bestTo = -1;
                var bestDistanceSq = float.MaxValue;

                for (var from = 0; from < nodeCount; from++)
                {
                    if (!selected[from])
                        continue;

                    for (var to = 0; to < nodeCount; to++)
                    {
                        if (selected[to])
                            continue;

                        var delta = nodes[to] - nodes[from];
                        var distanceSq = delta.sqrMagnitude;
                        if (distanceSq < bestDistanceSq)
                        {
                            bestDistanceSq = distanceSq;
                            bestFrom = from;
                            bestTo = to;
                        }
                    }
                }

                if (bestFrom < 0 || bestTo < 0)
                    break;

                selected[bestTo] = true;
                selectedCount++;

                var line = new MeshCenterLineEvaluation
                {
                    Closed = false,
                    SmoothPath = false,
                    SmoothingSubdivisions = 1,
                };
                line.Points.Add(nodes[bestFrom]);
                line.Points.Add(nodes[bestTo]);
                result.Lines.Add(line);
            }

            evaluation = result;
            return result.Lines.Count > 0;
        }

        static bool TryResolveScopePosition(IScopeNode scope, out float2 position)
        {
            position = default;

            var identityTransform = scope.Identity?.SelfTransform;
            if (identityTransform != null)
            {
                var p = identityTransform.position;
                position = new float2(p.x, p.y);
                return true;
            }

            if (scope is Component component)
            {
                var p = component.transform.position;
                position = new float2(p.x, p.y);
                return true;
            }

            return false;
        }

        static bool TryResolveHitPosition(in DynamicSearchHit hit, out float2 position)
        {
            position = hit.Position;

            var identityTransform = hit.Identity?.SelfTransform;
            if (identityTransform != null)
            {
                var p = identityTransform.position;
                position = new float2(p.x, p.y);
                return true;
            }

            if (hit.Scope is Component component)
            {
                var p = component.transform.position;
                position = new float2(p.x, p.y);
                return true;
            }

            return true;
        }
    }
}
