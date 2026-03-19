#nullable enable
using System;
using Game;
using Game.Commands.VNext;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Common
{
    public enum DistanceComparisonMode
    {
        LessOrEqual = 0,
        GreaterOrEqual = 1,
    }

    /// <summary>
    /// Resolves two actors via ActorSource and compares their distance.
    /// Intended for DynamicValue&lt;bool&gt;.
    /// </summary>
    [Serializable]
    public sealed class ActorDistanceCompareBoolSource : IDynamicSource
    {
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Actor A\", actorA)")]
        [SerializeField]
        ActorSource actorA;

        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Actor B\", actorB)")]
        [SerializeField]
        ActorSource actorB;

        [LabelText("Distance")]
        [SerializeField, Min(0f)]
        float distance = 1f;

        [LabelText("Comparison")]
        [SerializeField]
        DistanceComparisonMode comparison = DistanceComparisonMode.LessOrEqual;

        [LabelText("Use 2D (XY)")]
        [SerializeField]
        bool use2D = true;

        [NonSerialized] ActorSourceResolveCache _cacheA;
        [NonSerialized] ActorSourceResolveCache _cacheB;

        public string SourceTypeName => "ActorDistance";
        public string GetDebugData => $"{actorA.Kind}-{actorB.Kind} {comparison} {distance:0.###}";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (context?.Scope == null)
                return DynamicVariant.FromBool(false);

            var scopeA = ActorSourceFastResolver.ResolveCached(context, actorA, ref _cacheA);
            var scopeB = ActorSourceFastResolver.ResolveCached(context, actorB, ref _cacheB);

            if (!TryGetScopePosition(scopeA, out var posA) || !TryGetScopePosition(scopeB, out var posB))
                return DynamicVariant.FromBool(false);

            var threshold = Mathf.Max(0f, distance);
            var thresholdSq = threshold * threshold;

            float distSq;
            if (use2D)
            {
                var dx = posA.x - posB.x;
                var dy = posA.y - posB.y;
                distSq = dx * dx + dy * dy;
            }
            else
            {
                var delta = posA - posB;
                distSq = delta.sqrMagnitude;
            }

            var result = comparison == DistanceComparisonMode.LessOrEqual
                ? distSq <= thresholdSq
                : distSq >= thresholdSq;

            return DynamicVariant.FromBool(result);
        }

        static bool TryGetScopePosition(IScopeNode? scope, out Vector3 position)
        {
            position = Vector3.zero;
            if (scope == null)
                return false;

            var fromIdentity = scope.Identity?.SelfTransform;
            if (fromIdentity != null)
            {
                position = fromIdentity.position;
                return true;
            }

            if (scope is Component c && c != null)
            {
                position = c.transform.position;
                return true;
            }

            return false;
        }
    }
}
