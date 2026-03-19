#nullable enable

using System;
using Game.Commands.VNext;
using Game.UI;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace Game.Common
{
    public enum VisualBoundsValue2Space
    {
        LocalRect = 0,
        WorldBoundsXY = 1,
    }

    public enum VisualBoundsSampleAnchor
    {
        Center = 0,
        Size = 1,
        Min = 2,
        Max = 3,
        Left = 4,
        Right = 5,
        Top = 6,
        Bottom = 7,
        LeftTop = 8,
        RightTop = 9,
        LeftBottom = 10,
        RightBottom = 11,
    }

    [Serializable]
    public sealed class VisualBoundsValue2Source : IDynamicSource
    {
        [SerializeField]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Bounds Owner\", boundsOwnerActorSource)")]
        ActorSource boundsOwnerActorSource;

        [SerializeField, LabelText("Space")]
        VisualBoundsValue2Space space = VisualBoundsValue2Space.LocalRect;

        [SerializeField, LabelText("Anchor")]
        VisualBoundsSampleAnchor anchor = VisualBoundsSampleAnchor.Center;

        [SerializeField, LabelText("Rebuild Before Read")]
        bool rebuildBeforeRead;

        [SerializeField, LabelText("Multiplier")]
        float multiplier = 1f;

        [SerializeField, LabelText("Offset")]
        Vector2 offset = Vector2.zero;

        [SerializeField, LabelText("Fallback")]
        Vector2 fallback = Vector2.zero;

        [NonSerialized] ActorSourceResolveCache _ownerCache;

        public VisualBoundsValue2Space Space => space;
        public VisualBoundsSampleAnchor Anchor => anchor;

        public string SourceTypeName => "VisualBounds";
        public string GetDebugData => $"{boundsOwnerActorSource.Kind}:{space}:{anchor} (Vector2)";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (!VisualBoundsDynamicSourceHelper.TryResolveOutput(
                    context,
                    boundsOwnerActorSource,
                    ref _ownerCache,
                    out var output,
                    out var service))
            {
                return DynamicVariant.FromVector2(fallback);
            }

            if (rebuildBeforeRead && service != null)
                service.RebuildNow();

            if (!output.HasBounds)
                return DynamicVariant.FromVector2(fallback);

            var sampled = space == VisualBoundsValue2Space.LocalRect
                ? VisualBoundsDynamicSourceHelper.SampleRect(output.LocalRect, anchor)
                : VisualBoundsDynamicSourceHelper.SampleBoundsXY(output.WorldBounds, anchor);

            sampled = sampled * multiplier + offset;
            return DynamicVariant.FromVector2(sampled);
        }
    }

    public enum VisualBoundsValue3Space
    {
        WorldBounds = 0,
        LocalRectXY = 1,
    }

    [Serializable]
    public sealed class VisualBoundsValue3Source : IDynamicSource
    {
        [SerializeField]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Bounds Owner\", boundsOwnerActorSource)")]
        ActorSource boundsOwnerActorSource;

        [SerializeField, LabelText("Space")]
        VisualBoundsValue3Space space = VisualBoundsValue3Space.WorldBounds;

        [SerializeField, LabelText("Anchor")]
        VisualBoundsSampleAnchor anchor = VisualBoundsSampleAnchor.Center;

        [SerializeField, LabelText("Local Z")]
        [ShowIf(nameof(UsesLocalRectSpace))]
        float localZ;

        [SerializeField, LabelText("Rebuild Before Read")]
        bool rebuildBeforeRead;

        [SerializeField, LabelText("Multiplier")]
        float multiplier = 1f;

        [SerializeField, LabelText("Offset")]
        Vector3 offset = Vector3.zero;

        [SerializeField, LabelText("Fallback")]
        Vector3 fallback = Vector3.zero;

        [NonSerialized] ActorSourceResolveCache _ownerCache;

        public string SourceTypeName => "VisualBounds";
        public string GetDebugData => $"{boundsOwnerActorSource.Kind}:{space}:{anchor} (Vector3)";

        bool UsesLocalRectSpace() => space == VisualBoundsValue3Space.LocalRectXY;

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (!VisualBoundsDynamicSourceHelper.TryResolveOutput(
                    context,
                    boundsOwnerActorSource,
                    ref _ownerCache,
                    out var output,
                    out var service))
            {
                return DynamicVariant.FromVector3(fallback);
            }

            if (rebuildBeforeRead && service != null)
                service.RebuildNow();

            if (!output.HasBounds)
                return DynamicVariant.FromVector3(fallback);

            Vector3 sampled;
            if (space == VisualBoundsValue3Space.LocalRectXY)
            {
                var local = VisualBoundsDynamicSourceHelper.SampleRect(output.LocalRect, anchor);
                sampled = new Vector3(local.x, local.y, localZ);
            }
            else
            {
                sampled = VisualBoundsDynamicSourceHelper.SampleBounds3(output.WorldBounds, anchor);
            }

            sampled = sampled * multiplier + offset;
            return DynamicVariant.FromVector3(sampled);
        }
    }

    static class VisualBoundsDynamicSourceHelper
    {
        public static bool TryResolveOutput(
            IDynamicContext? context,
            in ActorSource boundsOwnerActorSource,
            ref ActorSourceResolveCache ownerCache,
            out IVisualBoundsOutput output,
            out IVisualBoundsService? service)
        {
            output = null!;
            service = null;

            if (context == null)
                return false;

            var scope = context.Scope;
            if (scope == null)
                return false;

            var commandRootScope = context.CommandRootScope;
            var ownerScope = ActorSourceFastResolver.ResolveCached(
                context,
                boundsOwnerActorSource,
                ref ownerCache,
                scope);

            if (TryResolveOutputFromScopeChain(ownerScope, out output, out service))
                return true;

            if (commandRootScope != null &&
                !ReferenceEquals(commandRootScope, ownerScope) &&
                TryResolveOutputFromScopeChain(commandRootScope, out output, out service))
            {
                return true;
            }

            if (!ReferenceEquals(scope, ownerScope) &&
                TryResolveOutputFromScopeChain(scope, out output, out service))
            {
                return true;
            }

            return false;
        }

        static bool TryResolveOutputFromScopeChain(
            IScopeNode? startScope,
            out IVisualBoundsOutput output,
            out IVisualBoundsService? service)
        {
            output = null!;
            service = null;
            if (startScope == null)
                return false;

            for (var current = startScope; current != null; current = current.Parent)
            {
                var resolver = current.Resolver;
                if (resolver == null)
                    continue;

                IVisualBoundsService? resolvedService = null;
                if (resolver.TryResolve<IVisualBoundsService>(out var s) && s != null)
                    resolvedService = s;

                if (resolver.TryResolve<IVisualBoundsOutput>(out var o) && o != null)
                {
                    output = o;
                    service = resolvedService ?? o as IVisualBoundsService;
                    return true;
                }

                if (resolvedService is IVisualBoundsOutput outputFromService)
                {
                    output = outputFromService;
                    service = resolvedService;
                    return true;
                }
            }

            return false;
        }

        public static Vector2 SampleRect(in Rect rect, VisualBoundsSampleAnchor anchor)
        {
            var min = rect.min;
            var max = rect.max;
            var center = rect.center;

            return anchor switch
            {
                VisualBoundsSampleAnchor.Center => center,
                VisualBoundsSampleAnchor.Size => rect.size,
                VisualBoundsSampleAnchor.Min => min,
                VisualBoundsSampleAnchor.Max => max,
                VisualBoundsSampleAnchor.Left => new Vector2(min.x, center.y),
                VisualBoundsSampleAnchor.Right => new Vector2(max.x, center.y),
                VisualBoundsSampleAnchor.Top => new Vector2(center.x, max.y),
                VisualBoundsSampleAnchor.Bottom => new Vector2(center.x, min.y),
                VisualBoundsSampleAnchor.LeftTop => new Vector2(min.x, max.y),
                VisualBoundsSampleAnchor.RightTop => new Vector2(max.x, max.y),
                VisualBoundsSampleAnchor.LeftBottom => new Vector2(min.x, min.y),
                VisualBoundsSampleAnchor.RightBottom => new Vector2(max.x, min.y),
                _ => center,
            };
        }

        public static Vector2 SampleBoundsXY(in Bounds bounds, VisualBoundsSampleAnchor anchor)
        {
            var min = bounds.min;
            var max = bounds.max;
            var center = bounds.center;
            var size = bounds.size;

            return anchor switch
            {
                VisualBoundsSampleAnchor.Center => new Vector2(center.x, center.y),
                VisualBoundsSampleAnchor.Size => new Vector2(size.x, size.y),
                VisualBoundsSampleAnchor.Min => new Vector2(min.x, min.y),
                VisualBoundsSampleAnchor.Max => new Vector2(max.x, max.y),
                VisualBoundsSampleAnchor.Left => new Vector2(min.x, center.y),
                VisualBoundsSampleAnchor.Right => new Vector2(max.x, center.y),
                VisualBoundsSampleAnchor.Top => new Vector2(center.x, max.y),
                VisualBoundsSampleAnchor.Bottom => new Vector2(center.x, min.y),
                VisualBoundsSampleAnchor.LeftTop => new Vector2(min.x, max.y),
                VisualBoundsSampleAnchor.RightTop => new Vector2(max.x, max.y),
                VisualBoundsSampleAnchor.LeftBottom => new Vector2(min.x, min.y),
                VisualBoundsSampleAnchor.RightBottom => new Vector2(max.x, min.y),
                _ => new Vector2(center.x, center.y),
            };
        }

        public static Vector3 SampleBounds3(in Bounds bounds, VisualBoundsSampleAnchor anchor)
        {
            var min = bounds.min;
            var max = bounds.max;
            var center = bounds.center;

            return anchor switch
            {
                VisualBoundsSampleAnchor.Center => center,
                VisualBoundsSampleAnchor.Size => bounds.size,
                VisualBoundsSampleAnchor.Min => min,
                VisualBoundsSampleAnchor.Max => max,
                VisualBoundsSampleAnchor.Left => new Vector3(min.x, center.y, center.z),
                VisualBoundsSampleAnchor.Right => new Vector3(max.x, center.y, center.z),
                VisualBoundsSampleAnchor.Top => new Vector3(center.x, max.y, center.z),
                VisualBoundsSampleAnchor.Bottom => new Vector3(center.x, min.y, center.z),
                VisualBoundsSampleAnchor.LeftTop => new Vector3(min.x, max.y, center.z),
                VisualBoundsSampleAnchor.RightTop => new Vector3(max.x, max.y, center.z),
                VisualBoundsSampleAnchor.LeftBottom => new Vector3(min.x, min.y, center.z),
                VisualBoundsSampleAnchor.RightBottom => new Vector3(max.x, min.y, center.z),
                _ => center,
            };
        }
    }
}
