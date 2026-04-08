#nullable enable

using Game;
using Game.Common;
using UnityEngine;
using VContainer;

namespace Game.Channel
{
    public static class AreaChannelDynamicContextUtility
    {
        public static IDynamicContext CreateContext(IScopeNode? scope)
        {
            if (scope == null)
                return EmptyDynamicContext.Instance;

            return new SimpleDynamicContext(ResolveVars(scope), scope);
        }

        public static IVarStore ResolveVars(IScopeNode? scope)
        {
            if (scope?.Resolver != null &&
                scope.Resolver.TryResolve<IBlackboardService>(out var blackboard) &&
                blackboard != null)
            {
                return blackboard.LocalVars;
            }

            return NullVarStore.Instance;
        }
    }

    internal static class AreaShapeRuntimeUtility
    {
        public static bool TrySample(IAreaShape shape, IDynamicContext context, in AreaShapeSampleContext sampleContext, Vector2 uv01, out Vector2 localPosition)
        {
            switch (shape)
            {
                case CircleAreaShape circle:
                    return circle.TrySample(in sampleContext, context, uv01, out localPosition);

                case DonutAreaShape donut:
                    return donut.TrySample(in sampleContext, context, uv01, out localPosition);

                case RectAreaShape rect:
                    return rect.TrySample(in sampleContext, context, uv01, out localPosition);

                case null:
                    localPosition = Vector2.zero;
                    return false;

                default:
                    return shape.TrySample(in sampleContext, uv01, out localPosition);
            }
        }

        public static bool ContainsLocalPosition(IAreaShape shape, IDynamicContext context, Vector2 localPosition)
        {
            switch (shape)
            {
                case CircleAreaShape circle:
                    return circle.ContainsLocalPosition(context, localPosition);

                case DonutAreaShape donut:
                    return donut.ContainsLocalPosition(context, localPosition);

                case RectAreaShape rect:
                    return rect.ContainsLocalPosition(context, localPosition);

                case null:
                    return false;

                default:
                    return shape.ContainsLocalPosition(localPosition);
            }
        }

        public static bool TryGetContourLocal(IAreaShape shape, IDynamicContext context, out AreaContourData contour)
        {
            switch (shape)
            {
                case CircleAreaShape circle:
                    return circle.TryGetContourLocal(context, out contour);

                case DonutAreaShape donut:
                    return donut.TryGetContourLocal(context, out contour);

                case RectAreaShape rect:
                    return rect.TryGetContourLocal(context, out contour);

                case null:
                    contour = default;
                    return false;

                default:
                    return shape.TryGetContourLocal(out contour);
            }
        }

        public static bool TryGetRectSnapshot(IAreaShape shape, IDynamicContext context, Vector3 basePosition, AreaPlane plane, out AreaRectSnapshot snapshot)
        {
            switch (shape)
            {
                case RectAreaShape rect:
                    return rect.TryGetRectSnapshot(context, basePosition, plane, out snapshot);

                case null:
                    snapshot = default;
                    return false;

                default:
                    snapshot = default;
                    return false;
            }
        }

        public static void DrawGizmo(IAreaShape shape, IDynamicContext context, Vector3 center, AreaPlane plane)
        {
            switch (shape)
            {
                case CircleAreaShape circle:
                    circle.DrawGizmo(context, center, plane);
                    break;

                case DonutAreaShape donut:
                    donut.DrawGizmo(context, center, plane);
                    break;

                case RectAreaShape rect:
                    rect.DrawGizmo(context, center, plane);
                    break;

                case null:
                    break;

                default:
                    shape.DrawGizmo(center, plane);
                    break;
            }
        }
    }
}