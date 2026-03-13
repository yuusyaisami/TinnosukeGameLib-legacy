#nullable enable

using System;
using Game;
using Game.Commands.VNext;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Common
{
    /// <summary>
    /// Returns selected actor world position as Vector2.
    /// Intended for DynamicValue&lt;Vector2&gt;.
    /// </summary>
    [Serializable]
    public sealed class ActorWorldPosition2Source : IDynamicSource
    {
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(actorSource)")]
        [SerializeField]
        ActorSource actorSource;

        [NonSerialized] ActorSourceResolveCache _cache;

        public string SourceTypeName => "ActorPos";
        public string GetDebugData => $"{actorSource.Kind} (Vector2)";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            var scope = ActorWorldPositionSourceHelper.ResolveScope(context, actorSource, ref _cache);
            if (!ActorWorldPositionSourceHelper.TryGetScopeTransform(scope, out var t) || t == null)
                return DynamicVariant.FromVector2(Vector2.zero);

            var p = t.position;
            return DynamicVariant.FromVector2(new Vector2(p.x, p.y));
        }
    }

    /// <summary>
    /// Returns selected actor world position X as float.
    /// Intended for DynamicValue&lt;float&gt;.
    /// </summary>
    [Serializable]
    public sealed class ActorWorldPositionXSource : IDynamicSource
    {
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(actorSource)")]
        [SerializeField]
        ActorSource actorSource;

        [NonSerialized] ActorSourceResolveCache _cache;

        public string SourceTypeName => "ActorPosX";
        public string GetDebugData => $"{actorSource.Kind} (X)";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            var scope = ActorWorldPositionSourceHelper.ResolveScope(context, actorSource, ref _cache);
            if (!ActorWorldPositionSourceHelper.TryGetScopeTransform(scope, out var t) || t == null)
                return DynamicVariant.FromFloat(0f);

            return DynamicVariant.FromFloat(t.position.x);
        }
    }

    /// <summary>
    /// Returns selected actor world position Y as float.
    /// Intended for DynamicValue&lt;float&gt;.
    /// </summary>
    [Serializable]
    public sealed class ActorWorldPositionYSource : IDynamicSource
    {
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(actorSource)")]
        [SerializeField]
        ActorSource actorSource;

        [NonSerialized] ActorSourceResolveCache _cache;

        public string SourceTypeName => "ActorPosY";
        public string GetDebugData => $"{actorSource.Kind} (Y)";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            var scope = ActorWorldPositionSourceHelper.ResolveScope(context, actorSource, ref _cache);
            if (!ActorWorldPositionSourceHelper.TryGetScopeTransform(scope, out var t) || t == null)
                return DynamicVariant.FromFloat(0f);

            return DynamicVariant.FromFloat(t.position.y);
        }
    }

    /// <summary>
    /// Returns selected actor world position as Vector3.
    /// Intended for DynamicValue&lt;Vector3&gt;.
    /// </summary>
    [Serializable]
    public sealed class ActorWorldPosition3Source : IDynamicSource
    {
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(actorSource)")]
        [SerializeField]
        ActorSource actorSource;

        [NonSerialized] ActorSourceResolveCache _cache;

        public string SourceTypeName => "ActorPos";
        public string GetDebugData => $"{actorSource.Kind} (Vector3)";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            var scope = ActorWorldPositionSourceHelper.ResolveScope(context, actorSource, ref _cache);
            if (!ActorWorldPositionSourceHelper.TryGetScopeTransform(scope, out var t) || t == null)
                return DynamicVariant.FromVector3(Vector3.zero);

            return DynamicVariant.FromVector3(t.position);
        }

        public bool TryResolveTransform(IDynamicContext context, out Transform? transform)
        {
            var scope = ActorWorldPositionSourceHelper.ResolveScope(context, actorSource, ref _cache);
            return ActorWorldPositionSourceHelper.TryGetScopeTransform(scope, out transform) && transform != null;
        }
    }

    static class ActorWorldPositionSourceHelper
    {
        public static IScopeNode? ResolveScope(IDynamicContext context, in ActorSource actorSource, ref ActorSourceResolveCache cache)
        {
            if (context?.Scope == null)
                return null;

            return ActorSourceFastResolver.ResolveCached(
                context.Scope,
                actorSource,
                ref cache,
                context.CommandRootScope);
        }

        public static bool TryGetScopeTransform(IScopeNode? scope, out Transform? transform)
        {
            transform = null;
            if (scope == null)
                return false;

            var fromIdentity = scope.Identity?.SelfTransform;
            if (fromIdentity != null)
            {
                transform = fromIdentity;
                return true;
            }

            if (scope is Component c && c != null)
            {
                transform = c.transform;
                return true;
            }

            return false;
        }
    }
}
