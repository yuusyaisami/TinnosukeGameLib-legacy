#nullable enable

using System;
using Game.Commands.VNext;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Common
{
    /// <summary>
    /// Returns direction vector from selected actor to target-channel hit as Vector2.
    /// Intended for DynamicValue&lt;Vector2&gt;.
    /// </summary>
    [Serializable]
    public sealed class TargetChannelDirectionFromActor2Source : IDynamicSource
    {
        [SerializeField]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(actorSource)")]
        ActorSource actorSource;

        [SerializeField]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Channel Owner\", channelOwnerActorSource)")]
        ActorSource channelOwnerActorSource;

        [SerializeField, LabelText("Channel Tag")]
        string channelTag = "default";

        [SerializeField, LabelText("Target Select")]
        TargetChannelTargetSelectMode targetSelectMode = TargetChannelTargetSelectMode.First;

        [SerializeField]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Target Filter Actor\", targetFilterActorSource)")]
        [ShowIf(nameof(UseTargetFilterActor))]
        ActorSource targetFilterActorSource;

        [SerializeField, LabelText("Fallback To First On Miss")]
        [ShowIf(nameof(UseTargetFilterActor))]
        bool fallbackToFirstIfFilterMiss = true;

        [SerializeField, LabelText("Normalize")]
        bool normalize = true;

        [SerializeField, LabelText("Multiplier")]
        float multiplier = 1f;

        [SerializeField, LabelText("Offset Angle (deg)")]
        DynamicValue<float> offsetAngle = DynamicValueExtensions.FromLiteral(0f);

        [SerializeField, LabelText("Fallback")]
        Vector2 fallback = Vector2.zero;

        [NonSerialized] ActorSourceResolveCache _actorCache;
        [NonSerialized] ActorSourceResolveCache _channelOwnerCache;
        [NonSerialized] ActorSourceResolveCache _targetFilterCache;

        public string SourceTypeName => "TargetDir";
        public string GetDebugData =>
            $"{actorSource.Kind}->{channelOwnerActorSource.Kind}:{channelTag}:{targetSelectMode} N={normalize} x{multiplier:0.###} +a={offsetAngle.SourceTypeName} (Vector2)";

        bool UseTargetFilterActor() => targetSelectMode == TargetChannelTargetSelectMode.FilterByActorSource;

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            var originScope = ActorWorldPositionSourceHelper.ResolveScope(context, actorSource, ref _actorCache);
            if (!ActorWorldPositionSourceHelper.TryGetScopeTransform(originScope, out var originTransform) || originTransform == null)
            {
                Debug.LogWarning($"[TargetChannelDirectionFromActor2Source] Failed to resolve origin transform from actor source {actorSource.Kind}");
                return DynamicVariant.FromVector2(fallback);
            }

            if (!TargetChannelTargetPositionSourceHelper.TryResolveTargetHit(
                    context,
                    channelTag,
                    channelOwnerActorSource,
                    ref _channelOwnerCache,
                    targetSelectMode,
                    targetFilterActorSource,
                    fallbackToFirstIfFilterMiss,
                    ref _targetFilterCache,
                    out var hit))
            {
                Debug.LogWarning($"[TargetChannelDirectionFromActor2Source] Failed to resolve target hit for channel '{channelTag}' and owner source {channelOwnerActorSource.Kind}");
                return DynamicVariant.FromVector2(fallback);
            }

            var origin = originTransform.position;
            var delta = new Vector2(hit.Position.x - origin.x, hit.Position.y - origin.y);
            var angle = offsetAngle.GetOrDefault(context, 0f);
            delta = TargetChannelDirectionSourceMath.Rotate2(delta, angle);
            var output = TargetChannelDirectionSourceMath.ApplyScaleAndNormalize(delta, normalize, multiplier);
            //Debug.Log($"[TargetChannelDirectionFromActor2Source] Resolved direction {output} from origin {origin} to target hit {hit.Position} for channel '{channelTag}'");
            return DynamicVariant.FromVector2(output);
        }
    }

    /// <summary>
    /// Returns direction vector from selected actor to target-channel hit as Vector3.
    /// Intended for DynamicValue&lt;Vector3&gt;.
    /// </summary>
    [Serializable]
    public sealed class TargetChannelDirectionFromActor3Source : IDynamicSource
    {
        [SerializeField]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(actorSource)")]
        ActorSource actorSource;

        [SerializeField]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Channel Owner\", channelOwnerActorSource)")]
        ActorSource channelOwnerActorSource;

        [SerializeField, LabelText("Channel Tag")]
        string channelTag = "default";

        [SerializeField, LabelText("Target Select")]
        TargetChannelTargetSelectMode targetSelectMode = TargetChannelTargetSelectMode.First;

        [SerializeField]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Target Filter Actor\", targetFilterActorSource)")]
        [ShowIf(nameof(UseTargetFilterActor))]
        ActorSource targetFilterActorSource;

        [SerializeField, LabelText("Fallback To First On Miss")]
        [ShowIf(nameof(UseTargetFilterActor))]
        bool fallbackToFirstIfFilterMiss = true;

        [SerializeField, LabelText("Normalize")]
        bool normalize = true;

        [SerializeField, LabelText("Multiplier")]
        float multiplier = 1f;

        [SerializeField, LabelText("Offset Angle (deg)")]
        DynamicValue<float> offsetAngle = DynamicValueExtensions.FromLiteral(0f);

        [SerializeField, LabelText("Fallback")]
        Vector3 fallback = Vector3.zero;

        [NonSerialized] ActorSourceResolveCache _actorCache;
        [NonSerialized] ActorSourceResolveCache _channelOwnerCache;
        [NonSerialized] ActorSourceResolveCache _targetFilterCache;

        public string SourceTypeName => "TargetDir";
        public string GetDebugData =>
            $"{actorSource.Kind}->{channelOwnerActorSource.Kind}:{channelTag}:{targetSelectMode} N={normalize} x{multiplier:0.###} +a={offsetAngle.SourceTypeName} (Vector3)";

        bool UseTargetFilterActor() => targetSelectMode == TargetChannelTargetSelectMode.FilterByActorSource;

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            var originScope = ActorWorldPositionSourceHelper.ResolveScope(context, actorSource, ref _actorCache);
            if (!ActorWorldPositionSourceHelper.TryGetScopeTransform(originScope, out var originTransform) || originTransform == null)
                return DynamicVariant.FromVector3(fallback);

            if (!TargetChannelTargetPositionSourceHelper.TryResolveTargetHit(
                    context,
                    channelTag,
                    channelOwnerActorSource,
                    ref _channelOwnerCache,
                    targetSelectMode,
                    targetFilterActorSource,
                    fallbackToFirstIfFilterMiss,
                    ref _targetFilterCache,
                    out var hit))
            {
                return DynamicVariant.FromVector3(fallback);
            }

            var origin = originTransform.position;
            var target = new Vector3(hit.Position.x, hit.Position.y, TargetChannelTargetPositionSourceHelper.ResolveWorldZ(hit));
            var delta = target - origin;
            var angle = offsetAngle.GetOrDefault(context, 0f);
            delta = TargetChannelDirectionSourceMath.Rotate3AroundZ(delta, angle);
            var output = TargetChannelDirectionSourceMath.ApplyScaleAndNormalize(delta, normalize, multiplier);
            return DynamicVariant.FromVector3(output);
        }
    }

    static class TargetChannelDirectionSourceMath
    {
        const float Epsilon = 0.000001f;

        public static Vector2 ApplyScaleAndNormalize(Vector2 value, bool normalize, float multiplier)
        {
            if (normalize && value.sqrMagnitude > Epsilon)
                value.Normalize();

            return value * multiplier;
        }

        public static Vector3 ApplyScaleAndNormalize(Vector3 value, bool normalize, float multiplier)
        {
            if (normalize && value.sqrMagnitude > Epsilon)
                value.Normalize();

            return value * multiplier;
        }

        public static Vector2 Rotate2(Vector2 value, float degrees)
        {
            if (Mathf.Abs(degrees) <= Epsilon)
                return value;

            var rad = degrees * Mathf.Deg2Rad;
            var c = Mathf.Cos(rad);
            var s = Mathf.Sin(rad);
            return new Vector2(
                value.x * c - value.y * s,
                value.x * s + value.y * c);
        }

        public static Vector3 Rotate3AroundZ(Vector3 value, float degrees)
        {
            if (Mathf.Abs(degrees) <= Epsilon)
                return value;

            var rotatedXY = Rotate2(new Vector2(value.x, value.y), degrees);
            return new Vector3(rotatedXY.x, rotatedXY.y, value.z);
        }
    }
}
