#nullable enable
using Game.Channel;
using Game.Layout;
using UnityEngine;
using VContainer;

namespace Game.UI
{
    internal enum TransformGridEnvironmentKind
    {
        ScreenUI = 10,
        World = 20,
    }

    internal static class TransformGridSharedUtility
    {
        public const int OrderRowMajor = 10;
        public const int OrderColumnMajor = 20;

        public const int HorizontalAlignmentLeft = 10;
        public const int HorizontalAlignmentCenter = 20;
        public const int HorizontalAlignmentRight = 30;

        public const int VerticalAlignmentTop = 10;
        public const int VerticalAlignmentCenter = 20;
        public const int VerticalAlignmentBottom = 30;

        public const int SizeSourceVisualBounds = 10;
        public const int SizeSourceRectTransform = 20;
        public const int SizeSourceFixed = 30;

        public static (int row, int column) ResolveRowColumn(int order, int listIndex, int rows, int columns)
        {
            if (order == OrderColumnMajor)
            {
                var safeColumns = Mathf.Max(1, columns);
                return (listIndex / safeColumns, listIndex % safeColumns);
            }

            return (listIndex / Mathf.Max(1, columns), listIndex % Mathf.Max(1, columns));
        }

        public static Vector3 ResolveTargetLocalPosition(
            Rect rect,
            int row,
            int column,
            int totalRows,
            int totalColumns,
            Vector2 itemSize,
            float rowSpacing,
            float columnSpacing,
            int areaHorizontalAlignment,
            int areaVerticalAlignment,
            Vector3 itemOffset)
        {
            var safeRows = Mathf.Max(1, totalRows);
            var safeColumns = Mathf.Max(1, totalColumns);
            var safeItemWidth = Mathf.Max(0f, itemSize.x);
            var safeItemHeight = Mathf.Max(0f, itemSize.y);
            var stepX = safeItemWidth + Mathf.Max(0f, columnSpacing);
            var stepY = safeItemHeight + Mathf.Max(0f, rowSpacing);

            var baseX = ResolveHorizontalBase(rect, areaHorizontalAlignment, safeColumns, stepX);
            var baseY = ResolveVerticalBase(rect, areaVerticalAlignment, safeRows, stepY);
            var horizontalDirection = areaHorizontalAlignment == HorizontalAlignmentRight ? -1f : 1f;
            var verticalDirection = areaVerticalAlignment == VerticalAlignmentBottom ? -1f : 1f;

            var x = baseX + itemOffset.x + (column * stepX * horizontalDirection);
            var y = baseY + itemOffset.y - (row * stepY * verticalDirection);
            return new Vector3(x, y, itemOffset.z);
        }

        public static TransformGridEnvironmentKind ResolveEnvironment(Transform ownerTransform, out Canvas? canvas)
        {
            canvas = ownerTransform != null ? ownerTransform.GetComponentInParent<Canvas>(true) : null;
            if (canvas != null &&
                (canvas.renderMode == RenderMode.ScreenSpaceOverlay || canvas.renderMode == RenderMode.ScreenSpaceCamera))
            {
                return TransformGridEnvironmentKind.ScreenUI;
            }

            canvas = null;
            return TransformGridEnvironmentKind.World;
        }

        public static bool TryResolveVisualBounds(IObjectResolver? resolver, out VisualBoundsOutput output)
        {
            output = default;
            if (resolver == null)
                return false;

            if (!resolver.TryResolve<IVisualBoundsService>(out var boundsService) || boundsService == null)
                return false;

            return boundsService.TryGetLastOutput(out output) && output.HasBounds;
        }

        public static bool TryResolveLayoutElementSize(
            IObjectResolver? resolver,
            RectTransform? rootRect,
            int sizeSource,
            Vector2 fixedSize,
            out Vector2 size)
        {
            size = Vector2.zero;

            switch (sizeSource)
            {
                case SizeSourceRectTransform:
                    if (rootRect != null)
                    {
                        var rectSize = rootRect.rect.size;
                        if (rectSize.x > 0f || rectSize.y > 0f)
                        {
                            size = rectSize;
                            return true;
                        }
                    }

                    if (TryResolveVisualBounds(resolver, out var rectFallbackBounds) &&
                        (rectFallbackBounds.LocalSize.x > 0f || rectFallbackBounds.LocalSize.y > 0f))
                    {
                        size = rectFallbackBounds.LocalSize;
                        return true;
                    }
                    return false;

                case SizeSourceVisualBounds:
                    if (TryResolveVisualBounds(resolver, out var bounds) &&
                        (bounds.LocalSize.x > 0f || bounds.LocalSize.y > 0f))
                    {
                        size = bounds.LocalSize;
                        return true;
                    }

                    if (rootRect != null)
                    {
                        var fallbackRect = rootRect.rect.size;
                        if (fallbackRect.x > 0f || fallbackRect.y > 0f)
                        {
                            size = fallbackRect;
                            return true;
                        }
                    }
                    return false;

                case SizeSourceFixed:
                    size = new Vector2(Mathf.Max(0f, fixedSize.x), Mathf.Max(0f, fixedSize.y));
                    return size.x > 0f || size.y > 0f;
            }

            return false;
        }

        public static Vector3 ResolvePlacementLocalPosition(
            IObjectResolver? resolver,
            RectTransform? rootRect,
            Vector3 targetLocalPosition,
            int horizontalAlignment,
            int verticalAlignment)
        {
            if (TryResolveVisualBounds(resolver, out var bounds))
            {
                var anchor = new Vector3(
                    ResolveHorizontalAnchor(bounds.LocalRect, horizontalAlignment),
                    ResolveVerticalAnchor(bounds.LocalRect, verticalAlignment),
                    0f);
                return targetLocalPosition - anchor;
            }

            if (rootRect == null)
                return targetLocalPosition;

            var fallbackRect = rootRect.rect;
            if (fallbackRect.width <= 0f && fallbackRect.height <= 0f)
                return targetLocalPosition;

            var fallbackAnchor = new Vector3(
                ResolveHorizontalAnchor(fallbackRect, horizontalAlignment),
                ResolveVerticalAnchor(fallbackRect, verticalAlignment),
                0f);
            return targetLocalPosition - fallbackAnchor;
        }

        public static void SetLocalPosition(
            Transform root,
            RectTransform? rootRect,
            Vector3 localPosition,
            TransformGridEnvironmentKind environmentKind)
        {
            if (environmentKind == TransformGridEnvironmentKind.ScreenUI && rootRect != null)
            {
                var parentRect = rootRect.parent as RectTransform;
                if (parentRect != null)
                {
                    var reference = ResolveAnchorReference(rootRect, parentRect);
                    rootRect.anchoredPosition3D = new Vector3(
                        localPosition.x - reference.x,
                        localPosition.y - reference.y,
                        localPosition.z);
                }
                else
                {
                    rootRect.anchoredPosition3D = localPosition;
                }

                return;
            }

            root.localPosition = localPosition;
        }

        public static Vector3 ResolveLocalPointFromTransform(
            Transform? listRoot,
            RectTransform? layoutRectTransform,
            Canvas? canvas,
            Transform? anchor,
            TransformGridEnvironmentKind environmentKind)
        {
            if (listRoot == null || anchor == null)
                return Vector3.zero;

            if (environmentKind == TransformGridEnvironmentKind.ScreenUI &&
                layoutRectTransform != null)
            {
                var camera = canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera
                    ? canvas.worldCamera
                    : null;
                var screenPoint = RectTransformUtility.WorldToScreenPoint(camera, anchor.position);
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        layoutRectTransform,
                        screenPoint,
                        camera,
                        out var localPoint))
                {
                    return new Vector3(localPoint.x, localPoint.y, 0f);
                }
            }

            return listRoot.InverseTransformPoint(anchor.position);
        }

        public static bool TryResolveTransformAnimationPlayer(
            IObjectResolver? resolver,
            string channelTag,
            out ITransformAnimationChannelPlayer? player)
        {
            player = null;
            if (resolver == null)
                return false;

            if (!resolver.TryResolve<ITransformAnimationHubService>(out var hub) || hub == null)
                return false;

            return hub.TryGetPlayer(channelTag, out player) && player != null;
        }

        public static void RefreshLayoutAndBounds(IObjectResolver? resolver)
        {
            if (resolver == null)
                return;

            if (resolver.TryResolve<ILayoutSystemService>(out var layoutService) && layoutService != null)
                layoutService.RebuildNow();

            if (resolver.TryResolve<IVisualBoundsService>(out var boundsService) && boundsService != null)
            {
                boundsService.MarkDirty();
                boundsService.RebuildNow();
            }
        }

        static float ResolveHorizontalBase(Rect rect, int alignment, int totalColumns, float stepX)
        {
            var span = Mathf.Max(0, totalColumns - 1) * stepX;
            return alignment switch
            {
                HorizontalAlignmentLeft => rect.xMin,
                HorizontalAlignmentRight => rect.xMax,
                HorizontalAlignmentCenter => rect.center.x - (span * 0.5f),
                _ => rect.xMin,
            };
        }

        static float ResolveVerticalBase(Rect rect, int alignment, int totalRows, float stepY)
        {
            var span = Mathf.Max(0, totalRows - 1) * stepY;
            return alignment switch
            {
                VerticalAlignmentTop => rect.yMax,
                VerticalAlignmentBottom => rect.yMin,
                VerticalAlignmentCenter => rect.center.y + (span * 0.5f),
                _ => rect.yMax,
            };
        }

        static float ResolveHorizontalAnchor(Rect localRect, int alignment)
        {
            return alignment switch
            {
                HorizontalAlignmentLeft => localRect.xMin,
                HorizontalAlignmentRight => localRect.xMax,
                HorizontalAlignmentCenter => localRect.center.x,
                _ => localRect.xMin,
            };
        }

        static float ResolveVerticalAnchor(Rect localRect, int alignment)
        {
            return alignment switch
            {
                VerticalAlignmentTop => localRect.yMax,
                VerticalAlignmentBottom => localRect.yMin,
                VerticalAlignmentCenter => localRect.center.y,
                _ => localRect.yMax,
            };
        }

        static Vector2 ResolveAnchorReference(RectTransform rectTransform, RectTransform parent)
        {
            var parentSize = parent.rect.size;
            var parentPivot = parent.pivot;
            var anchorMin = rectTransform.anchorMin;
            var anchorMax = rectTransform.anchorMax;
            var pivot = rectTransform.pivot;
            var normalized = new Vector2(
                Mathf.Lerp(anchorMin.x, anchorMax.x, pivot.x),
                Mathf.Lerp(anchorMin.y, anchorMax.y, pivot.y));

            return new Vector2(
                (normalized.x - parentPivot.x) * parentSize.x,
                (normalized.y - parentPivot.y) * parentSize.y);
        }
    }
}
