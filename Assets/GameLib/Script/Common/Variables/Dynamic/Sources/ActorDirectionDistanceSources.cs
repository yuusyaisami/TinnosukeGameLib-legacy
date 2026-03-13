#nullable enable

using System;
using Game;
using Game.Commands.VNext;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Common
{
    /// <summary>
    /// Returns world-space delta vector from Actor A to Actor B as Vector2 (XY).
    /// Intended for DynamicValue&lt;Vector2&gt;.
    /// </summary>
    [Serializable]
    public sealed class ActorDirectionDistance2Source : IDynamicSource
    {
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Actor A\", actorA)")]
        [SerializeField]
        ActorSource actorA;

        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Actor B\", actorB)")]
        [SerializeField]
        ActorSource actorB;

        [LabelText("Multiplier")]
        [SerializeField]
        float multiplier = 1f;

        [NonSerialized] ActorSourceResolveCache _cacheA;
        [NonSerialized] ActorSourceResolveCache _cacheB;

        public string SourceTypeName => "ActorDirDist";
        public string GetDebugData => $"{actorA.Kind}->{actorB.Kind} x{multiplier:0.###} (Vector2)";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            var scopeA = ActorWorldPositionSourceHelper.ResolveScope(context, actorA, ref _cacheA);
            var scopeB = ActorWorldPositionSourceHelper.ResolveScope(context, actorB, ref _cacheB);

            if (!ActorWorldPositionSourceHelper.TryGetScopeTransform(scopeA, out var transformA) || transformA == null)
                return DynamicVariant.FromVector2(Vector2.zero);
            if (!ActorWorldPositionSourceHelper.TryGetScopeTransform(scopeB, out var transformB) || transformB == null)
                return DynamicVariant.FromVector2(Vector2.zero);

            var delta = transformB.position - transformA.position;
            Debug.Log($"[ActorDirectionDistance2Source] Actor A: {transformA.position}, Actor B: {transformB.position}, Delta: {delta}, Multiplier: {multiplier}");
            var scopeName = context.Scope?.Identity?.SelfTransform != null
                ? context.Scope.Identity.SelfTransform.name
                : "(no scope transform)";
            Debug.Log($"[ActorDirectionDistance2Source] context.Scope: {scopeName}, context.CommandRootScope: {context.CommandRootScope?.Identity?.SelfTransform?.name ?? "(no command root scope)"}");
            return DynamicVariant.FromVector2(new Vector2(delta.x, delta.y) * multiplier);
        }
    }

    /// <summary>
    /// Returns world-space delta vector from Actor A to Actor B as Vector3.
    /// Intended for DynamicValue&lt;Vector3&gt;.
    /// </summary>
    [Serializable]
    public sealed class ActorDirectionDistance3Source : IDynamicSource
    {
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Actor A\", actorA)")]
        [SerializeField]
        ActorSource actorA;

        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Actor B\", actorB)")]
        [SerializeField]
        ActorSource actorB;

        [LabelText("Multiplier")]
        [SerializeField]
        float multiplier = 1f;

        [NonSerialized] ActorSourceResolveCache _cacheA;
        [NonSerialized] ActorSourceResolveCache _cacheB;

        public string SourceTypeName => "ActorDirDist";
        public string GetDebugData => $"{actorA.Kind}->{actorB.Kind} x{multiplier:0.###} (Vector3)";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            var scopeA = ActorWorldPositionSourceHelper.ResolveScope(context, actorA, ref _cacheA);
            var scopeB = ActorWorldPositionSourceHelper.ResolveScope(context, actorB, ref _cacheB);

            if (!ActorWorldPositionSourceHelper.TryGetScopeTransform(scopeA, out var transformA) || transformA == null)
                return DynamicVariant.FromVector3(Vector3.zero);
            if (!ActorWorldPositionSourceHelper.TryGetScopeTransform(scopeB, out var transformB) || transformB == null)
                return DynamicVariant.FromVector3(Vector3.zero);

            return DynamicVariant.FromVector3((transformB.position - transformA.position) * multiplier);
        }
    }
}
