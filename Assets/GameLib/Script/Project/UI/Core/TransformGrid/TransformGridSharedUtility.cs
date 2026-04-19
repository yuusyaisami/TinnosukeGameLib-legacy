#nullable enable
using Game.Commands.VNext;
using Game.Channel;
using Game.Common;
using Game.Layout;
using UnityEngine;
using VContainer;

namespace Game.UI
{
    public enum TransformGridLayoutRangeSourceMode
    {
        RectTransform = 10,
        AreaChannel = 20,
    }

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
                var safeRows = Mathf.Max(1, rows);
                return (listIndex % safeRows, listIndex / safeRows);
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
            return TryResolveVisualBounds(resolver, expectedRoot: null, expectedRootRect: null, out output);
        }

        public static bool TryResolveVisualBounds(
            IObjectResolver? resolver,
            Transform? expectedRoot,
            RectTransform? expectedRootRect,
            out VisualBoundsOutput output)
        {
            output = default;
            if (resolver == null)
                return false;

            if (!resolver.TryResolve<IVisualBoundsService>(out var boundsService) || boundsService == null)
                return false;

            if (!boundsService.TryGetLastOutput(out output) || !output.HasBounds)
                return false;

            if (!IsVisualBoundsRootCompatible(resolver, boundsService, expectedRoot, expectedRootRect))
            {
                output = default;
                return false;
            }

            return true;
        }

        public static bool TryResolveLayoutElementSize(
            IObjectResolver? resolver,
            Transform? root,
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

                    if (TryResolveVisualBounds(resolver, root, rootRect, out var rectFallbackBounds) &&
                        (rectFallbackBounds.LocalSize.x > 0f || rectFallbackBounds.LocalSize.y > 0f))
                    {
                        size = rectFallbackBounds.LocalSize;
                        return true;
                    }
                    return false;

                case SizeSourceVisualBounds:
                    if (TryResolveVisualBounds(resolver, root, rootRect, out var bounds) &&
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
            Transform? root,
            RectTransform? rootRect,
            Vector3 targetLocalPosition,
            int horizontalAlignment,
            int verticalAlignment)
        {
            if (TryResolveVisualBounds(resolver, root, rootRect, out var bounds))
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
            if (root == null)
                return;

            if (environmentKind == TransformGridEnvironmentKind.ScreenUI && rootRect != null)
            {
                rootRect.anchoredPosition3D = ResolveMotionTargetPosition(rootRect, localPosition, environmentKind);

                return;
            }

            root.localPosition = localPosition;
        }

        public static Vector3 ResolveMotionTargetPosition(
            RectTransform? rootRect,
            Vector3 localPosition,
            TransformGridEnvironmentKind environmentKind)
        {
            if (environmentKind != TransformGridEnvironmentKind.ScreenUI || rootRect == null)
                return localPosition;

            var parentRect = rootRect.parent as RectTransform;
            if (parentRect == null)
                return localPosition;

            var reference = ResolveAnchorReference(rootRect, parentRect);
            return new Vector3(
                localPosition.x - reference.x,
                localPosition.y - reference.y,
                localPosition.z);
        }

        public static void SetUiElementVisible(IObjectResolver? resolver, bool visible)
        {
            if (resolver == null)
                return;

            if (resolver.TryResolve<IUIElementStateController>(out var stateController) && stateController != null)
                stateController.SetVisible(visible);
        }

        public static Rect ResolveLayoutRect(
            Transform? listRoot,
            Transform? layoutReferenceTransform,
            RectTransform? layoutRectTransform,
            Canvas? canvas,
            IScopeNode? activeScope,
            TransformGridEnvironmentKind environmentKind,
            TransformGridLayoutRangeSourceMode rangeSourceMode,
            in ActorSource areaActorSource,
            ref ActorSourceResolveCache areaActorSourceCache,
            string? areaChannelTag)
        {
            if (rangeSourceMode == TransformGridLayoutRangeSourceMode.AreaChannel &&
                TryResolveAreaChannelLayoutRect(
                    listRoot,
                    layoutReferenceTransform,
                    layoutRectTransform,
                    canvas,
                    activeScope,
                    environmentKind,
                    areaActorSource,
                    ref areaActorSourceCache,
                    areaChannelTag,
                    out var resolvedRect))
            {
                return resolvedRect;
            }

            return ResolveRectTransformLayoutRect(listRoot, layoutRectTransform);
        }

        public static Vector3 ResolveLocalPointFromTransform(
            Transform? listRoot,
            Transform? layoutReferenceTransform,
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

            var referenceTransform = layoutReferenceTransform ?? listRoot;
            if (referenceTransform == null)
                return Vector3.zero;

            return referenceTransform.InverseTransformPoint(anchor.position);
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

        static bool IsVisualBoundsRootCompatible(
            IObjectResolver resolver,
            IVisualBoundsService boundsService,
            Transform? expectedRoot,
            RectTransform? expectedRootRect)
        {
            var expected = (Transform?)expectedRootRect ?? expectedRoot;
            if (expected == null)
                return true;

            Transform? actualRoot = null;
            if (boundsService is IVisualBoundsOutput boundsOutput)
                actualRoot = boundsOutput.LocalSpaceRoot;

            if (actualRoot == null &&
                resolver.TryResolve<IVisualBoundsOutput>(out var resolvedOutput) &&
                resolvedOutput != null)
            {
                actualRoot = resolvedOutput.LocalSpaceRoot;
            }

            if (actualRoot == null)
                return false;

            return ReferenceEquals(actualRoot, expected) || actualRoot.IsChildOf(expected);
        }

        static Rect ResolveRectTransformLayoutRect(Transform? listRoot, RectTransform? layoutRectTransform)
        {
            if (layoutRectTransform != null)
                return layoutRectTransform.rect;

            return listRoot is RectTransform rootRect
                ? rootRect.rect
                : new Rect(0f, 0f, 0f, 0f);
        }

        static bool TryResolveAreaChannelLayoutRect(
            Transform? listRoot,
            Transform? layoutReferenceTransform,
            RectTransform? layoutRectTransform,
            Canvas? canvas,
            IScopeNode? activeScope,
            TransformGridEnvironmentKind environmentKind,
            in ActorSource areaActorSource,
            ref ActorSourceResolveCache areaActorSourceCache,
            string? areaChannelTag,
            out Rect rect)
        {
            rect = default;
            if (activeScope?.Resolver == null)
                return false;

            var areaScope = ActorSourceFastResolver.ResolveCached(activeScope, areaActorSource, ref areaActorSourceCache);
            if (areaScope?.Resolver == null)
                return false;

            if (!areaScope.Resolver.TryResolve<IAreaChannelHubService>(out var hub) || hub == null)
                return false;

            var normalizedTag = string.IsNullOrWhiteSpace(areaChannelTag) ? "default" : areaChannelTag.Trim();
            var referenceTransform = layoutReferenceTransform ?? (Transform?)layoutRectTransform ?? listRoot;
            if (referenceTransform == null)
                return false;

            if (environmentKind == TransformGridEnvironmentKind.ScreenUI)
            {
                if (canvas == null || !hub.TryGetCanvasRectSnapshot(normalizedTag, canvas, out var canvasSnapshot))
                    return false;

                return TryConvertCanvasRectSnapshotToLocalRect(canvasSnapshot, referenceTransform, out rect);
            }

            if (!hub.TryGetRectSnapshot(normalizedTag, out var worldSnapshot))
                return false;

            return TryConvertWorldRectSnapshotToLocalRect(worldSnapshot, referenceTransform, out rect);
        }

        static bool TryConvertCanvasRectSnapshotToLocalRect(
            in AreaCanvasRectSnapshot snapshot,
            Transform referenceTransform,
            out Rect rect)
        {
            var corners = new Vector3[4];
            var source = snapshot.LocalRect;
            corners[0] = snapshot.CanvasRect.TransformPoint(new Vector3(source.xMin, source.yMin, 0f));
            corners[1] = snapshot.CanvasRect.TransformPoint(new Vector3(source.xMax, source.yMin, 0f));
            corners[2] = snapshot.CanvasRect.TransformPoint(new Vector3(source.xMax, source.yMax, 0f));
            corners[3] = snapshot.CanvasRect.TransformPoint(new Vector3(source.xMin, source.yMax, 0f));
            return TryBuildLocalRectFromWorldCorners(referenceTransform, corners, AreaPlane.XY, out rect);
        }

        static bool TryConvertWorldRectSnapshotToLocalRect(
            in AreaRectSnapshot snapshot,
            Transform referenceTransform,
            out Rect rect)
        {
            var halfSize = snapshot.Size * 0.5f;
            var corners = new Vector3[4];
            corners[0] = snapshot.Center + ToPlane(new Vector2(-halfSize.x, -halfSize.y), snapshot.Plane);
            corners[1] = snapshot.Center + ToPlane(new Vector2(halfSize.x, -halfSize.y), snapshot.Plane);
            corners[2] = snapshot.Center + ToPlane(new Vector2(halfSize.x, halfSize.y), snapshot.Plane);
            corners[3] = snapshot.Center + ToPlane(new Vector2(-halfSize.x, halfSize.y), snapshot.Plane);
            return TryBuildLocalRectFromWorldCorners(referenceTransform, corners, snapshot.Plane, out rect);
        }

        static bool TryBuildLocalRectFromWorldCorners(
            Transform referenceTransform,
            Vector3[] worldCorners,
            AreaPlane plane,
            out Rect rect)
        {
            rect = default;
            if (referenceTransform == null || worldCorners == null || worldCorners.Length == 0)
                return false;

            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (var i = 0; i < worldCorners.Length; i++)
            {
                var local3 = referenceTransform.InverseTransformPoint(worldCorners[i]);
                var local = plane == AreaPlane.XZ
                    ? new Vector2(local3.x, local3.z)
                    : new Vector2(local3.x, local3.y);
                if (!IsFinite(local))
                    return false;

                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }

            rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return true;
        }

        static Vector3 ToPlane(Vector2 point, AreaPlane plane)
        {
            return plane == AreaPlane.XZ
                ? new Vector3(point.x, 0f, point.y)
                : new Vector3(point.x, point.y, 0f);
        }

        static bool IsFinite(Vector2 value)
        {
            return !float.IsNaN(value.x) &&
                   !float.IsNaN(value.y) &&
                   !float.IsInfinity(value.x) &&
                   !float.IsInfinity(value.y);
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
