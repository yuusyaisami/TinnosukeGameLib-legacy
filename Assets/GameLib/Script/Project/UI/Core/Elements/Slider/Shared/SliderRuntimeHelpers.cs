#nullable enable
using System.Collections.Generic;
using Game.Channel;
using Game.Commands.VNext;
using Game.Common;
using Game.DI;
using Game.Spawn;
using Game.Vars.Generated;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.UI
{
    internal static partial class SliderRuntimeHelpers
    {
        public const string VarKeyTargetRaw = "GameLib.Slider.targetRaw";
        public const string VarKeyTargetNormalized = "GameLib.Slider.targetNormalized";
        public const string VarKeyDisplayedRaw = "GameLib.Slider.displayedRaw";
        public const string VarKeyDisplayedNormalized = "GameLib.Slider.displayedNormalized";
        public const string VarKeyDeltaRaw = "GameLib.Slider.deltaRaw";
        public const string VarKeyDeltaNormalized = "GameLib.Slider.deltaNormalized";
        public const string VarKeyCrossingDirection = "GameLib.Slider.crossingDirection";
        public const string VarKeyEntryIndex = "GameLib.Slider.entryIndex";
        public const string VarKeyEntryValue = "GameLib.Slider.entryValue";
        public const string VarKeyEntryNormalized = "GameLib.Slider.entryNormalized";
        public const string VarKeyUnitKind = "GameLib.Slider.unitKind";
        public const string VarKeyUnitIndex = "GameLib.Slider.unitIndex";
        public const string VarKeySegmentStartRaw = "GameLib.Slider.segmentStartRaw";
        public const string VarKeySegmentEndRaw = "GameLib.Slider.segmentEndRaw";
        public const string VarKeySegmentStartNormalized = "GameLib.Slider.segmentStartNormalized";
        public const string VarKeySegmentEndNormalized = "GameLib.Slider.segmentEndNormalized";

        public static SliderEnvironmentKind ResolveEnvironment(Transform ownerTransform, out Canvas? canvas)
        {
            canvas = ownerTransform != null ? ownerTransform.GetComponentInParent<Canvas>(true) : null;
            if (canvas != null && IsScreenCanvas(canvas))
                return SliderEnvironmentKind.ScreenUI;

            canvas = null;
            return SliderEnvironmentKind.World;
        }

        public static bool IsScreenCanvas(Canvas? canvas)
        {
            return canvas != null &&
                   (canvas.renderMode == RenderMode.ScreenSpaceOverlay ||
                    canvas.renderMode == RenderMode.ScreenSpaceCamera);
        }

        public static Camera? ResolveCanvasCamera(Canvas canvas)
        {
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return null;

            return canvas.worldCamera;
        }

        public static string NormalizeTag(string? tag)
        {
            return string.IsNullOrWhiteSpace(tag) ? "default" : tag.Trim();
        }

        public static BaseRuntimeTemplateSO? ResolveRuntimeTemplate(
            DynamicValue<BaseRuntimeTemplatePreset> value,
            IDynamicContext context)
        {
            if (!value.TryGet(context, out BaseRuntimeTemplatePreset? preset) || preset == null)
                return null;

            return RuntimeTemplatePresetResolver.ResolveTemplateSO(preset);
        }

        public static SliderResolvedSegmentLayout BuildSegmentLayout(
            SliderVisualizerPreset visualizerPreset,
            IDynamicContext context,
            float minValue,
            float maxValue)
        {
            var layout = new SliderResolvedSegmentLayout
            {
                MinValue = minValue,
                MaxValue = maxValue,
            };

            layout.Boundaries.Add(minValue);

            var segmented = visualizerPreset.Segmented;
            if (segmented.PlacementMode == SliderSegmentPlacementMode.EqualInterval)
            {
                var step = Mathf.Abs(segmented.IntervalStep.GetOrDefault(context, 0f));
                if (step > Mathf.Epsilon && maxValue - minValue > Mathf.Epsilon)
                {
                    int entryIndex = 0;
                    for (var value = minValue + step; value < maxValue - Mathf.Epsilon; value += step)
                    {
                        var clamped = Mathf.Clamp(value, minValue, maxValue);
                        var normalized = Normalize(clamped, minValue, maxValue);
                        layout.Entries.Add(new SliderResolvedEntry(entryIndex, clamped, normalized, null));
                        layout.Boundaries.Add(clamped);
                        entryIndex++;
                    }
                }
            }
            else
            {
                var temp = new List<(float RawValue, SliderSegmentEntryBase Entry)>();
                var sourceEntries = segmented.Entries;
                for (var i = 0; i < sourceEntries.Count; i++)
                {
                    var sourceEntry = sourceEntries[i];
                    if (sourceEntry == null)
                        continue;

                    var rawValue = Mathf.Clamp(sourceEntry.ResolveRawValue(context, minValue, maxValue), minValue, maxValue);
                    temp.Add((rawValue, sourceEntry));
                }

                temp.Sort(static (x, y) => x.RawValue.CompareTo(y.RawValue));

                float? lastRawValue = null;
                int entryIndex = 0;
                for (var i = 0; i < temp.Count; i++)
                {
                    var rawValue = temp[i].RawValue;
                    if (Mathf.Approximately(rawValue, minValue) || Mathf.Approximately(rawValue, maxValue))
                        continue;

                    if (lastRawValue.HasValue && Mathf.Abs(rawValue - lastRawValue.Value) <= 0.0001f)
                        continue;

                    lastRawValue = rawValue;
                    var normalized = Normalize(rawValue, minValue, maxValue);
                    layout.Entries.Add(new SliderResolvedEntry(entryIndex, rawValue, normalized, temp[i].Entry));
                    layout.Boundaries.Add(rawValue);
                    entryIndex++;
                }
            }

            layout.Boundaries.Add(maxValue);
            return layout;
        }

        public static int ResolveVisualSegmentBarCount(
            SliderSegmentedVisualizerSettings segmented,
            int boundaryCount)
        {
            if (segmented == null || boundaryCount <= 1)
                return 0;

            return segmented.SplitBarsByLayout
                ? Mathf.Max(0, boundaryCount - 1)
                : 1;
        }

        public static void ResolveVisualSegmentBarRange(
            SliderSegmentedVisualizerSettings segmented,
            ISliderPlayerRuntime output,
            int barIndex,
            out float startRawValue,
            out float endRawValue,
            out float startNormalizedValue,
            out float endNormalizedValue)
        {
            startRawValue = 0f;
            endRawValue = 0f;
            startNormalizedValue = 0f;
            endNormalizedValue = 0f;

            if (segmented == null || output == null || output.BoundaryCount <= 1)
                return;

            if (!segmented.SplitBarsByLayout)
            {
                var lastIndex = output.BoundaryCount - 1;
                startRawValue = output.ResolveBoundaryRawValue(0);
                endRawValue = output.ResolveBoundaryRawValue(lastIndex);
                startNormalizedValue = output.ResolveBoundaryNormalizedValue(0);
                endNormalizedValue = output.ResolveBoundaryNormalizedValue(lastIndex);
                return;
            }

            var clampedIndex = Mathf.Clamp(barIndex, 0, output.BoundaryCount - 2);
            startRawValue = output.ResolveBoundaryRawValue(clampedIndex);
            endRawValue = output.ResolveBoundaryRawValue(clampedIndex + 1);
            startNormalizedValue = output.ResolveBoundaryNormalizedValue(clampedIndex);
            endNormalizedValue = output.ResolveBoundaryNormalizedValue(clampedIndex + 1);
        }

        public static void ResolveDisplayedSegmentBarInterval(
            SliderSegmentDisplayMode displayMode,
            bool splitBarsByLayout,
            float displayedNormalizedValue,
            float startNormalizedValue,
            float endNormalizedValue,
            out float visibleStartNormalizedValue,
            out float visibleEndNormalizedValue,
            out bool isVisible)
        {
            var clampedDisplayed = Mathf.Clamp01(displayedNormalizedValue);
            if (!splitBarsByLayout)
            {
                visibleStartNormalizedValue = 0f;
                visibleEndNormalizedValue = clampedDisplayed;
                isVisible = visibleEndNormalizedValue - visibleStartNormalizedValue > 0.0001f;
                return;
            }

            visibleStartNormalizedValue = startNormalizedValue;
            if (displayMode == SliderSegmentDisplayMode.ReachedStageFloor)
            {
                visibleEndNormalizedValue = endNormalizedValue;
                isVisible = clampedDisplayed >= endNormalizedValue - 0.0001f;
                return;
            }

            visibleEndNormalizedValue = Mathf.Clamp(clampedDisplayed, startNormalizedValue, endNormalizedValue);
            isVisible = visibleEndNormalizedValue - visibleStartNormalizedValue > 0.0001f;
        }

        public static bool ShouldShowBackground(
            SliderBackgroundVisualizerSettings background,
            in SliderOutputSnapshot snapshot)
        {
            if (background == null)
                return true;

            if (!background.HideWhenFillIsMin)
                return true;

            return snapshot.DisplayedNormalizedValue > 0.0001f;
        }

        public static SliderRangeResolveStatus TryResolveWorldRangeSnapshot(
            IScopeNode scope,
            ISliderOptions options,
            ref ActorSourceResolveCache areaActorSourceCache,
            out AreaRectSnapshot snapshot)
        {
            snapshot = default;

            if (options.RangeSourceMode == SliderRangeSourceMode.RectTransform)
                return SliderRangeResolveStatus.UnsupportedWorldRectTransform;

            var areaScope = ActorSourceFastResolver.ResolveCached(scope, options.AreaActorSource, ref areaActorSourceCache);
            if (areaScope?.Resolver == null)
                return SliderRangeResolveStatus.AreaScopeUnavailable;

            if (!areaScope.Resolver.TryResolve<IAreaChannelHubService>(out var hub) || hub == null)
                return SliderRangeResolveStatus.AreaHubUnavailable;

            if (!hub.TryGetPlayer(options.AreaChannelTag, out var player) || player == null)
                return SliderRangeResolveStatus.AreaPlayerUnavailable;

            if (player.Definition.Shape is not RectAreaShape)
                return SliderRangeResolveStatus.UnsupportedShape;

            if (!player.TryGetRectSnapshot(ResolveAreaBasePosition(player.Definition, areaScope), out snapshot))
                return SliderRangeResolveStatus.AreaPlayerUnavailable;

            return SliderRangeResolveStatus.Success;
        }

        public static SliderRangeResolveStatus TryResolveScreenRangeSnapshot(
            IScopeNode scope,
            ISliderOptions options,
            Canvas canvas,
            ref ActorSourceResolveCache areaActorSourceCache,
            out SliderScreenRangeSnapshot snapshot)
        {
            snapshot = default;

            if (canvas.transform is not RectTransform canvasRect)
                return SliderRangeResolveStatus.CanvasUnavailable;

            var uiCamera = ResolveCanvasCamera(canvas);
            if (options.RangeSourceMode == SliderRangeSourceMode.RectTransform)
            {
                if (options.RangeRectTransform == null)
                    return SliderRangeResolveStatus.RectTransformUnavailable;

                if (!TryBuildCanvasRectSnapshot(options.RangeRectTransform, canvasRect, uiCamera, out snapshot))
                    return SliderRangeResolveStatus.RectTransformUnavailable;

                return SliderRangeResolveStatus.Success;
            }

            var areaScope = ActorSourceFastResolver.ResolveCached(scope, options.AreaActorSource, ref areaActorSourceCache);
            if (areaScope?.Resolver == null)
                return SliderRangeResolveStatus.AreaScopeUnavailable;

            if (!areaScope.Resolver.TryResolve<IAreaChannelHubService>(out var hub) || hub == null)
                return SliderRangeResolveStatus.AreaHubUnavailable;

            if (!hub.TryGetPlayer(options.AreaChannelTag, out var player) || player == null)
                return SliderRangeResolveStatus.AreaPlayerUnavailable;

            if (player.Definition.Shape is not RectAreaShape)
                return SliderRangeResolveStatus.UnsupportedShape;

            if (!hub.TryGetCanvasRectSnapshot(options.AreaChannelTag, canvas, out var areaSnapshot))
                return SliderRangeResolveStatus.AreaPlayerUnavailable;

            snapshot = new SliderScreenRangeSnapshot(areaSnapshot.CanvasRect, areaSnapshot.LocalRect, areaSnapshot.UICamera);
            return SliderRangeResolveStatus.Success;
        }

        public static bool TryBuildCanvasRectSnapshot(
            RectTransform source,
            RectTransform canvasRect,
            Camera? uiCamera,
            out SliderScreenRangeSnapshot snapshot)
        {
            snapshot = default;
            if (source == null || canvasRect == null)
                return false;

            var corners = new Vector3[4];
            source.GetWorldCorners(corners);

            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (var i = 0; i < corners.Length; i++)
            {
                var local = (Vector2)canvasRect.InverseTransformPoint(corners[i]);
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }

            var localRect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            snapshot = new SliderScreenRangeSnapshot(canvasRect, localRect, uiCamera);
            return true;
        }

        public static int ResolveVarId(VarKeyRef key)
        {
            if (key.VarId != 0)
                return key.VarId;

            if (!string.IsNullOrEmpty(key.StableKey) && VarIdResolver.TryResolve(key.StableKey, out var resolvedVarId))
                return resolvedVarId;

            return 0;
        }

        public static float Normalize(float rawValue, float minValue, float maxValue)
        {
            var range = maxValue - minValue;
            if (Mathf.Abs(range) <= Mathf.Epsilon)
                return 0f;

            return Mathf.Clamp01((rawValue - minValue) / range);
        }

        public static float ResolveAreaLength(in AreaRectSnapshot snapshot, SliderAreaFillAxis fillAxis)
        {
            return Mathf.Max(0f, fillAxis == SliderAreaFillAxis.SizeX ? snapshot.Size.x : snapshot.Size.y);
        }

        public static float ResolveAreaCrossLength(in AreaRectSnapshot snapshot, SliderAreaFillAxis fillAxis)
        {
            return Mathf.Max(0f, fillAxis == SliderAreaFillAxis.SizeX ? snapshot.Size.y : snapshot.Size.x);
        }

        public static void ResolveFilledBarGeometry(
            in AreaRectSnapshot snapshot,
            SliderAreaFillAxis fillAxis,
            SliderAreaOriginSide originSide,
            float fillNormalized,
            out Vector3 worldCenter,
            out float majorLength)
        {
            var zeroCoord = MapNormalizedToAreaCoordinate(snapshot, fillAxis, originSide, 0f);
            var filledCoord = MapNormalizedToAreaCoordinate(snapshot, fillAxis, originSide, fillNormalized);
            majorLength = Mathf.Abs(filledCoord - zeroCoord);
            worldCenter = ResolveAreaWorldPosition(snapshot, fillAxis, (zeroCoord + filledCoord) * 0.5f);
        }

        public static void ResolveIntervalBarGeometry(
            in AreaRectSnapshot snapshot,
            SliderAreaFillAxis fillAxis,
            SliderAreaOriginSide originSide,
            float startNormalized,
            float endNormalized,
            out Vector3 worldCenter,
            out float majorLength)
        {
            var startCoord = MapNormalizedToAreaCoordinate(snapshot, fillAxis, originSide, startNormalized);
            var endCoord = MapNormalizedToAreaCoordinate(snapshot, fillAxis, originSide, endNormalized);
            majorLength = Mathf.Abs(endCoord - startCoord);
            worldCenter = ResolveAreaWorldPosition(snapshot, fillAxis, (startCoord + endCoord) * 0.5f);
        }

        public static void ResolveVisibleIntervalBarGeometry(
            in AreaRectSnapshot snapshot,
            SliderAreaFillAxis fillAxis,
            SliderAreaOriginSide originSide,
            float startNormalized,
            float endNormalized,
            float gapBeforeUnits,
            float gapAfterUnits,
            out Vector3 worldCenter,
            out float majorLength)
        {
            var startCoord = MapNormalizedToAreaCoordinate(snapshot, fillAxis, originSide, startNormalized);
            var endCoord = MapNormalizedToAreaCoordinate(snapshot, fillAxis, originSide, endNormalized);
            var direction = endCoord >= startCoord ? 1f : -1f;

            var adjustedStart = startCoord + (direction * Mathf.Max(0f, gapBeforeUnits));
            var adjustedEnd = endCoord - (direction * Mathf.Max(0f, gapAfterUnits));
            var delta = adjustedEnd - adjustedStart;
            majorLength = Mathf.Max(0f, Mathf.Abs(delta));

            if (majorLength <= 0.0001f)
            {
                var collapsedCoordinate = (adjustedStart + adjustedEnd) * 0.5f;
                worldCenter = ResolveAreaWorldPosition(snapshot, fillAxis, collapsedCoordinate);
                majorLength = 0f;
                return;
            }

            worldCenter = ResolveAreaWorldPosition(snapshot, fillAxis, (adjustedStart + adjustedEnd) * 0.5f);
        }

        public static Vector3 ResolveMarkerWorldPosition(
            in AreaRectSnapshot snapshot,
            SliderAreaFillAxis fillAxis,
            SliderAreaOriginSide originSide,
            float normalizedValue)
        {
            var coordinate = MapNormalizedToAreaCoordinate(snapshot, fillAxis, originSide, normalizedValue);
            return ResolveAreaWorldPosition(snapshot, fillAxis, coordinate);
        }

        public static bool ApplySpawnedBarGeometry(
            in SliderSpawnedRuntimeInstance instance,
            in AreaRectSnapshot areaSnapshot,
            SliderAreaFillAxis fillAxis,
            Vector3 worldCenter,
            float majorLength,
            float minorLength)
        {
            if (instance.Root == null)
                return false;

            ApplyRootTransform(instance.Root, instance.BasePose, areaSnapshot, worldCenter);

            switch (instance.VisualTargetKind)
            {
                case SliderRuntimeVisualTargetKind.SpriteRenderer:
                    if (instance.SpriteRenderer == null)
                        return false;

                    return ApplySpriteVisualTargetGeometry(
                        instance.Root,
                        instance.SpriteRenderer,
                        instance.VisualPose,
                        instance.SpriteState,
                        fillAxis,
                        majorLength,
                        minorLength);

                case SliderRuntimeVisualTargetKind.Image:
                    if (instance.Image == null)
                        return false;

                    ApplyImageVisualTargetGeometry(
                        instance.Root,
                        instance.Image,
                        instance.VisualPose,
                        fillAxis,
                        majorLength,
                        minorLength);
                    return false;

                default:
                    return false;
            }
        }

        public static bool ApplyBarRendererGeometry(
            SpriteRenderer renderer,
            in SliderTransformPose basePose,
            in SliderSpriteRenderState spriteState,
            in AreaRectSnapshot areaSnapshot,
            SliderAreaFillAxis fillAxis,
            Vector3 worldCenter,
            float majorLength,
            float minorLength)
        {
            var transform = renderer.transform;
            transform.position = ApplyDepthOffset(worldCenter, basePose.LocalPosition, areaSnapshot.Plane);
            transform.localRotation = basePose.LocalRotation;

            var localScale = basePose.LocalScale;
            var resolvedLength = Mathf.Max(0f, majorLength);
            var resolvedCrossLength = Mathf.Max(0f, minorLength);
            var localMajorLength = ResolveLocalLengthForAxis(transform, fillAxis == SliderAreaFillAxis.SizeX, resolvedLength);
            var localCrossLength = ResolveLocalLengthForAxis(transform, fillAxis != SliderAreaFillAxis.SizeX, resolvedCrossLength);
            var drawMode = renderer.drawMode;
            var supportsRendererSize = drawMode == SpriteDrawMode.Sliced || drawMode == SpriteDrawMode.Tiled;
            if (supportsRendererSize)
            {
                renderer.size = fillAxis == SliderAreaFillAxis.SizeX
                    ? new Vector2(localMajorLength, localCrossLength)
                    : new Vector2(localCrossLength, localMajorLength);
                transform.localScale = basePose.LocalScale;
                return false;
            }

            renderer.size = spriteState.Size;
            var spriteLocalSize = ResolveSpriteLocalSize(renderer, spriteState);
            if (fillAxis == SliderAreaFillAxis.SizeX)
            {
                localScale.x = ResolveSpriteFallbackAxisScale(transform, basePose.LocalScale.x, spriteLocalSize.x, resolvedLength, useXAxis: true);
                localScale.y = ResolveSpriteFallbackAxisScale(transform, basePose.LocalScale.y, spriteLocalSize.y, resolvedCrossLength, useXAxis: false);
            }
            else
            {
                localScale.y = ResolveSpriteFallbackAxisScale(transform, basePose.LocalScale.y, spriteLocalSize.y, resolvedLength, useXAxis: false);
                localScale.x = ResolveSpriteFallbackAxisScale(transform, basePose.LocalScale.x, spriteLocalSize.x, resolvedCrossLength, useXAxis: true);
            }

            transform.localScale = localScale;
            return true;
        }

        public static bool TryResolveRuntimeVisualTarget(
            IObjectResolver resolver,
            Transform root,
            string channelTag,
            out SliderRuntimeVisualTargetKind visualTargetKind,
            out Transform? visualTransform,
            out SpriteRenderer? spriteRenderer,
            out Image? image)
        {
            visualTargetKind = SliderRuntimeVisualTargetKind.None;
            visualTransform = null;
            spriteRenderer = null;
            image = null;

            var normalizedTag = string.IsNullOrWhiteSpace(channelTag) ? "default" : channelTag.Trim();
            if (resolver.TryResolve<IAnimationSpriteHubService>(out var hub) &&
                hub != null &&
                hub.TryGetPlayer(normalizedTag, out var player) &&
                player != null)
            {
                if (player.SpriteRenderer != null)
                {
                    visualTargetKind = SliderRuntimeVisualTargetKind.SpriteRenderer;
                    spriteRenderer = player.SpriteRenderer;
                    visualTransform = player.SpriteRenderer.transform;
                    return true;
                }

                if (player.Image != null)
                {
                    visualTargetKind = SliderRuntimeVisualTargetKind.Image;
                    image = player.Image;
                    visualTransform = player.Image.rectTransform;
                    return visualTransform != null;
                }
            }

            if (TryResolvePrimarySpriteRenderer(root, out spriteRenderer) && spriteRenderer != null)
            {
                visualTargetKind = SliderRuntimeVisualTargetKind.SpriteRenderer;
                visualTransform = spriteRenderer.transform;
                return true;
            }

            var directImage = root.GetComponent<Image>();
            if (directImage == null)
                directImage = root.GetComponentInChildren<Image>(true);

            if (directImage != null && directImage.rectTransform != null)
            {
                visualTargetKind = SliderRuntimeVisualTargetKind.Image;
                image = directImage;
                visualTransform = directImage.rectTransform;
                return true;
            }

            return false;
        }

        public static float ResolveDisplayedRawValue(
            SliderSegmentDisplayMode mode,
            SliderResolvedSegmentLayout? layout,
            float continuousRawValue,
            float minValue,
            float maxValue)
        {
            var clamped = Mathf.Clamp(continuousRawValue, minValue, maxValue);
            if (mode == SliderSegmentDisplayMode.Continuous ||
                layout == null ||
                layout.Boundaries.Count == 0)
            {
                return clamped;
            }

            var result = minValue;
            for (var i = 0; i < layout.Boundaries.Count; i++)
            {
                var candidate = layout.Boundaries[i];
                if (candidate - clamped > 0.0001f)
                    break;

                result = candidate;
            }

            return Mathf.Clamp(result, minValue, maxValue);
        }
    }
}
