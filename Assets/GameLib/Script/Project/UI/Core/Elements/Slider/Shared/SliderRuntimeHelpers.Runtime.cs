#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Channel;
using Game.Common;
using Game.DI;
using Game.Spawn;
using Game.Times;
using Game.Vars.Generated;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.UI
{
    internal static partial class SliderRuntimeHelpers
    {
        public static void ApplyMarkerTransform(
            Transform transform,
            in SliderTransformPose basePose,
            in AreaRectSnapshot areaSnapshot,
            Vector3 worldPosition)
        {
            transform.position = ApplyDepthOffset(worldPosition, basePose.LocalPosition, areaSnapshot.Plane);
            transform.localRotation = basePose.LocalRotation;
            transform.localScale = basePose.LocalScale;
        }

        public static bool TryResolvePrimarySpriteRenderer(Transform root, out SpriteRenderer? renderer)
        {
            renderer = root.GetComponent<SpriteRenderer>();
            if (renderer != null)
                return true;

            renderer = root.GetComponentInChildren<SpriteRenderer>(true);
            return renderer != null;
        }

        public static void WriteCommonCommandVars(
            IVarStore vars,
            in SliderOutputSnapshot snapshot,
            float deltaRaw,
            float deltaNormalized)
        {
            TrySetFloatVar(vars, VarIds.GameLib.Slider.targetRaw, snapshot.TargetRawValue);
            TrySetFloatVar(vars, VarIds.GameLib.Slider.targetNormalized, snapshot.TargetNormalizedValue);
            TrySetFloatVar(vars, VarIds.GameLib.Slider.displayedRaw, snapshot.DisplayedRawValue);
            TrySetFloatVar(vars, VarIds.GameLib.Slider.displayedNormalized, snapshot.DisplayedNormalizedValue);
            TrySetFloatVar(vars, VarIds.GameLib.Slider.deltaRaw, deltaRaw);
            TrySetFloatVar(vars, VarIds.GameLib.Slider.deltaNormalized, deltaNormalized);
        }

        public static void WriteCrossingCommandVars(
            IVarStore vars,
            in SliderResolvedEntry entry,
            SliderSegmentCrossingDirection direction)
        {
            TrySetIntVar(vars, VarIds.GameLib.Slider.crossingDirection, (int)direction);
            TrySetIntVar(vars, VarIds.GameLib.Slider.entryIndex, entry.Index);
            TrySetFloatVar(vars, VarIds.GameLib.Slider.entryValue, entry.RawValue);
            TrySetFloatVar(vars, VarIds.GameLib.Slider.entryNormalized, entry.NormalizedValue);
            entry.SourceEntry?.WritePayloadVars(vars, entry.Index, entry.RawValue, entry.NormalizedValue);
        }

        public static void WriteSpawnCommandVars(
            IVarStore vars,
            in SliderSpawnedRuntimeInstance instance)
        {
            TrySetIntVar(vars, VarIds.GameLib.Slider.unitKind, (int)instance.UnitKind);
            TrySetIntVar(vars, VarIds.GameLib.Slider.unitIndex, instance.UnitIndex);
            TrySetFloatVar(vars, VarIds.GameLib.Slider.segmentStartRaw, instance.StartRawValue);
            TrySetFloatVar(vars, VarIds.GameLib.Slider.segmentEndRaw, instance.EndRawValue);
            TrySetFloatVar(vars, VarIds.GameLib.Slider.segmentStartNormalized, instance.StartNormalized);
            TrySetFloatVar(vars, VarIds.GameLib.Slider.segmentEndNormalized, instance.EndNormalized);

            if (instance.EntryIndex >= 0)
                TrySetIntVar(vars, VarIds.GameLib.Slider.entryIndex, instance.EntryIndex);

            if (instance.EntryIndex >= 0 || Mathf.Abs(instance.EntryRawValue) > 0.0001f)
                TrySetFloatVar(vars, VarIds.GameLib.Slider.entryValue, instance.EntryRawValue);

            if (instance.EntryIndex >= 0 || Mathf.Abs(instance.EntryNormalized) > 0.0001f)
                TrySetFloatVar(vars, VarIds.GameLib.Slider.entryNormalized, instance.EntryNormalized);
        }

        public static async UniTask<IObjectResolver?> SpawnRuntimeAsync(
            IAsyncSpawnerService spawner,
            BaseRuntimeTemplateSO template,
            Transform parent,
            IScopeNode owner,
            bool allowPooling,
            CancellationToken ct)
        {
            await UniTask.SwitchToMainThread(ct);
            var spawnParams = SpawnParams.ForRuntime(
                template,
                Vector3.zero,
                Quaternion.identity,
                Vector3.one,
                identity: null,
                transformParent: parent,
                lifetimeScopeParent: owner,
                worldSpace: false,
                allowPooling: allowPooling);
            return await spawner.SpawnAsync(spawnParams, ct);
        }

        public static void ExtractSpawnedInfo(
            IObjectResolver? resolver,
            out Transform? root,
            out IScopeNode? scopeNode,
            out RuntimeLifetimeScope? runtimeScope)
        {
            root = null;
            scopeNode = null;
            runtimeScope = null;

            if (resolver == null)
                return;

            resolver.TryResolve(out runtimeScope);
            if (runtimeScope != null)
                root = runtimeScope.transform;

            if (root == null)
            {
                if (resolver.TryResolve<Transform>(out var resolvedTransform) && resolvedTransform != null)
                    root = resolvedTransform;
                else if (resolver.TryResolve<GameObject>(out var gameObject) && gameObject != null)
                    root = gameObject.transform;
            }

            scopeNode = runtimeScope;
            if (scopeNode == null && resolver.TryResolve<IScopeNode>(out var resolvedScope) && resolvedScope != null)
                scopeNode = resolvedScope;
        }

        public static void RestoreSpawnedRuntime(in SliderSpawnedRuntimeInstance instance)
        {
            if (instance.Root != null)
            {
                instance.Root.gameObject.SetActive(true);
                instance.BasePose.ApplyTo(instance.Root);
            }

            if (instance.SpriteRenderer != null)
            {
                if (instance.VisualTransform != null && instance.VisualTransform != instance.Root)
                    instance.VisualPose.ApplyTo(instance.VisualTransform);
                instance.SpriteState.ApplyTo(instance.SpriteRenderer);
            }

            if (instance.Image != null)
            {
                if (instance.VisualTransform != null && instance.VisualTransform != instance.Root)
                    instance.VisualPose.ApplyTo(instance.VisualTransform);
                instance.ImageState.ApplyTo(instance.Image);
            }
        }

        public static void ReleaseSpawnedRuntime(IObjectResolver? resolver)
        {
            if (resolver == null)
                return;

            if (resolver.TryResolve<RuntimeLifetimeScope>(out var runtimeScope) && runtimeScope != null)
            {
                if (runtimeScope.Resolver != null &&
                    runtimeScope.Resolver.TryResolve<IRuntimeLifetimeScopePool>(out var pool) &&
                    pool != null)
                {
                    pool.Release(runtimeScope);
                    return;
                }

                Object.Destroy(runtimeScope.gameObject);
            }
        }

        public static TimeScaleBehavior ResolveTimeScaleBehavior(IScopeNode scope)
        {
            return scope?.Identity?.TimeScaleBehavior ?? TimeScaleBehavior.Scaled;
        }

        public static float ResolveRectLength(Rect rect, SliderAreaFillAxis axis)
        {
            return Mathf.Max(0f, axis == SliderAreaFillAxis.SizeX ? rect.width : rect.height);
        }

        public static float ResolveRectCrossLength(Rect rect, SliderAreaFillAxis axis)
        {
            return Mathf.Max(0f, axis == SliderAreaFillAxis.SizeX ? rect.height : rect.width);
        }

        public static float MapNormalizedToRectCoordinate(
            Rect rect,
            SliderAreaFillAxis axis,
            SliderAreaOriginSide originSide,
            float normalizedValue)
        {
            normalizedValue = Mathf.Clamp01(normalizedValue);

            var min = axis == SliderAreaFillAxis.SizeX ? rect.xMin : rect.yMin;
            var max = axis == SliderAreaFillAxis.SizeX ? rect.xMax : rect.yMax;
            return originSide == SliderAreaOriginSide.Min
                ? Mathf.Lerp(min, max, normalizedValue)
                : Mathf.Lerp(max, min, normalizedValue);
        }

        public static void ResolveRectIntervalGeometry(
            Rect rect,
            SliderAreaFillAxis axis,
            SliderAreaOriginSide originSide,
            float startNormalized,
            float endNormalized,
            out Vector2 center,
            out Vector2 size)
        {
            var start = MapNormalizedToRectCoordinate(rect, axis, originSide, startNormalized);
            var end = MapNormalizedToRectCoordinate(rect, axis, originSide, endNormalized);
            var majorCenter = (start + end) * 0.5f;
            var majorSize = Mathf.Abs(end - start);

            if (axis == SliderAreaFillAxis.SizeX)
            {
                center = new Vector2(majorCenter, rect.center.y);
                size = new Vector2(majorSize, rect.height);
            }
            else
            {
                center = new Vector2(rect.center.x, majorCenter);
                size = new Vector2(rect.width, majorSize);
            }
        }

        public static Vector2 ResolveRectMarkerPosition(
            Rect rect,
            SliderAreaFillAxis axis,
            SliderAreaOriginSide originSide,
            float normalizedValue)
        {
            var coordinate = MapNormalizedToRectCoordinate(rect, axis, originSide, normalizedValue);
            return axis == SliderAreaFillAxis.SizeX
                ? new Vector2(coordinate, rect.center.y)
                : new Vector2(rect.center.x, coordinate);
        }

        public static bool TryMapCanvasLocalToNormalized(
            Rect rect,
            SliderAreaFillAxis axis,
            SliderAreaOriginSide originSide,
            float paddingStart,
            float paddingEnd,
            Vector2 localPosition,
            out float normalizedValue)
        {
            normalizedValue = 0f;
            var coordinate = axis == SliderAreaFillAxis.SizeX ? localPosition.x : localPosition.y;
            return TryMapCoordinateToNormalized(
                axis == SliderAreaFillAxis.SizeX ? rect.xMin : rect.yMin,
                axis == SliderAreaFillAxis.SizeX ? rect.xMax : rect.yMax,
                originSide,
                paddingStart,
                paddingEnd,
                coordinate,
                out normalizedValue);
        }

        public static bool TryMapWorldPointToNormalized(
            in AreaRectSnapshot snapshot,
            SliderAreaFillAxis axis,
            SliderAreaOriginSide originSide,
            float paddingStart,
            float paddingEnd,
            Vector3 worldPosition,
            out float normalizedValue)
        {
            normalizedValue = 0f;
            var coordinate = axis == SliderAreaFillAxis.SizeX
                ? worldPosition.x
                : snapshot.Plane == AreaPlane.XZ
                    ? worldPosition.z
                    : worldPosition.y;

            var min = axis == SliderAreaFillAxis.SizeX
                ? snapshot.Center.x - snapshot.Size.x * 0.5f
                : snapshot.Plane == AreaPlane.XZ
                    ? snapshot.Center.z - snapshot.Size.y * 0.5f
                    : snapshot.Center.y - snapshot.Size.y * 0.5f;
            var max = axis == SliderAreaFillAxis.SizeX
                ? snapshot.Center.x + snapshot.Size.x * 0.5f
                : snapshot.Plane == AreaPlane.XZ
                    ? snapshot.Center.z + snapshot.Size.y * 0.5f
                    : snapshot.Center.y + snapshot.Size.y * 0.5f;

            return TryMapCoordinateToNormalized(min, max, originSide, paddingStart, paddingEnd, coordinate, out normalizedValue);
        }

        public static bool ApproximatelyEquals(Rect a, Rect b)
        {
            return Mathf.Abs(a.xMin - b.xMin) <= 0.0001f &&
                   Mathf.Abs(a.xMax - b.xMax) <= 0.0001f &&
                   Mathf.Abs(a.yMin - b.yMin) <= 0.0001f &&
                   Mathf.Abs(a.yMax - b.yMax) <= 0.0001f;
        }

        public static bool ApproximatelyEquals(in AreaRectSnapshot a, in AreaRectSnapshot b)
        {
            return a.Plane == b.Plane &&
                   Vector3.SqrMagnitude(a.Center - b.Center) <= 0.0000001f &&
                   Vector2.SqrMagnitude(a.Size - b.Size) <= 0.0000001f;
        }

        static float MapNormalizedToAreaCoordinate(
            in AreaRectSnapshot snapshot,
            SliderAreaFillAxis fillAxis,
            SliderAreaOriginSide originSide,
            float normalizedValue)
        {
            var length = ResolveAreaLength(snapshot, fillAxis);
            var halfLength = length * 0.5f;
            var clamped = Mathf.Clamp01(normalizedValue);
            return originSide == SliderAreaOriginSide.Min
                ? -halfLength + (length * clamped)
                : halfLength - (length * clamped);
        }

        static Vector3 ResolveAreaWorldPosition(
            in AreaRectSnapshot snapshot,
            SliderAreaFillAxis fillAxis,
            float fillCoordinate)
        {
            var local = fillAxis == SliderAreaFillAxis.SizeX
                ? new Vector2(fillCoordinate, 0f)
                : new Vector2(0f, fillCoordinate);
            return snapshot.Center + ToPlane(local, snapshot.Plane);
        }

        static Vector3 ToPlane(Vector2 local, AreaPlane plane)
        {
            return plane == AreaPlane.XZ
                ? new Vector3(local.x, 0f, local.y)
                : new Vector3(local.x, local.y, 0f);
        }

        static Vector3 ResolveAreaBasePosition(AreaChannelDefinition definition, IScopeNode scope)
        {
            var anchor = definition.Anchor != null ? definition.Anchor : scope.Identity?.SelfTransform;
            return anchor != null ? anchor.position + definition.CenterOffset : definition.CenterOffset;
        }

        static Vector3 ApplyDepthOffset(Vector3 position, Vector3 baseLocalPosition, AreaPlane plane)
        {
            if (plane == AreaPlane.XZ)
            {
                position.y += baseLocalPosition.y;
                return position;
            }

            position.z += baseLocalPosition.z;
            return position;
        }

        static void ApplyRootTransform(
            Transform root,
            in SliderTransformPose basePose,
            in AreaRectSnapshot areaSnapshot,
            Vector3 worldCenter)
        {
            root.position = ApplyDepthOffset(worldCenter, basePose.LocalPosition, areaSnapshot.Plane);
            root.localRotation = basePose.LocalRotation;
            root.localScale = basePose.LocalScale;
        }

        static bool ApplySpriteVisualTargetGeometry(
            Transform root,
            SpriteRenderer renderer,
            in SliderTransformPose visualPose,
            in SliderSpriteRenderState spriteState,
            SliderAreaFillAxis fillAxis,
            float majorLength,
            float minorLength)
        {
            var targetTransform = renderer.transform;
            var isRootTarget = targetTransform == root;
            if (!isRootTarget)
            {
                targetTransform.localPosition = visualPose.LocalPosition;
                targetTransform.localRotation = visualPose.LocalRotation;
                targetTransform.localScale = visualPose.LocalScale;
            }

            var baseScale = isRootTarget ? root.localScale : visualPose.LocalScale;
            var localScale = baseScale;
            var resolvedLength = Mathf.Max(0f, majorLength);
            var resolvedCrossLength = Mathf.Max(0f, minorLength);
            var localMajorLength = ResolveLocalLengthForAxis(targetTransform, fillAxis == SliderAreaFillAxis.SizeX, resolvedLength);
            var localCrossLength = ResolveLocalLengthForAxis(targetTransform, fillAxis != SliderAreaFillAxis.SizeX, resolvedCrossLength);
            var drawMode = renderer.drawMode;
            var supportsRendererSize = drawMode == SpriteDrawMode.Sliced || drawMode == SpriteDrawMode.Tiled;
            if (supportsRendererSize)
            {
                renderer.size = fillAxis == SliderAreaFillAxis.SizeX
                    ? new Vector2(localMajorLength, localCrossLength)
                    : new Vector2(localCrossLength, localMajorLength);
                targetTransform.localScale = baseScale;
                return false;
            }

            renderer.size = spriteState.Size;
            var spriteLocalSize = ResolveSpriteLocalSize(renderer, spriteState);
            if (fillAxis == SliderAreaFillAxis.SizeX)
            {
                localScale.x = ResolveSpriteFallbackAxisScale(targetTransform, baseScale.x, spriteLocalSize.x, resolvedLength, useXAxis: true);
                localScale.y = ResolveSpriteFallbackAxisScale(targetTransform, baseScale.y, spriteLocalSize.y, resolvedCrossLength, useXAxis: false);
            }
            else
            {
                localScale.y = ResolveSpriteFallbackAxisScale(targetTransform, baseScale.y, spriteLocalSize.y, resolvedLength, useXAxis: false);
                localScale.x = ResolveSpriteFallbackAxisScale(targetTransform, baseScale.x, spriteLocalSize.x, resolvedCrossLength, useXAxis: true);
            }

            targetTransform.localScale = localScale;
            return true;
        }

        static void ApplyImageVisualTargetGeometry(
            Transform root,
            Image image,
            in SliderTransformPose visualPose,
            SliderAreaFillAxis fillAxis,
            float majorLength,
            float minorLength)
        {
            var rectTransform = image.rectTransform;
            if (rectTransform == null)
                return;

            var isRootTarget = rectTransform == root;
            if (!isRootTarget)
            {
                rectTransform.localPosition = visualPose.LocalPosition;
                rectTransform.localRotation = visualPose.LocalRotation;
                rectTransform.localScale = visualPose.LocalScale;
            }

            var localMajorLength = ResolveLocalLengthForAxis(rectTransform, fillAxis == SliderAreaFillAxis.SizeX, Mathf.Max(0f, majorLength));
            var localCrossLength = ResolveLocalLengthForAxis(rectTransform, fillAxis != SliderAreaFillAxis.SizeX, Mathf.Max(0f, minorLength));
            rectTransform.sizeDelta = fillAxis == SliderAreaFillAxis.SizeX
                ? new Vector2(localMajorLength, localCrossLength)
                : new Vector2(localCrossLength, localMajorLength);
        }

        static float ResolveLocalLengthForAxis(Transform transform, bool useXAxis, float worldLength)
        {
            var lossyScaleAxis = Mathf.Abs(useXAxis ? transform.lossyScale.x : transform.lossyScale.y);
            if (lossyScaleAxis <= 0.0001f)
                return Mathf.Max(0f, worldLength);

            return Mathf.Max(0f, worldLength) / lossyScaleAxis;
        }

        static Vector2 ResolveSpriteLocalSize(SpriteRenderer renderer, in SliderSpriteRenderState spriteState)
        {
            var sprite = renderer.sprite;
            if (sprite != null)
            {
                var boundsSize = sprite.bounds.size;
                return new Vector2(
                    Mathf.Max(0.0001f, boundsSize.x),
                    Mathf.Max(0.0001f, boundsSize.y));
            }

            return new Vector2(
                Mathf.Max(0.0001f, spriteState.Size.x),
                Mathf.Max(0.0001f, spriteState.Size.y));
        }

        static float ResolveSpriteFallbackAxisScale(
            Transform transform,
            float baseLocalScaleAxis,
            float baseSpriteLocalSize,
            float desiredWorldLength,
            bool useXAxis)
        {
            if (baseSpriteLocalSize <= 0.0001f)
                return baseLocalScaleAxis;

            var baseScaleMagnitude = Mathf.Abs(baseLocalScaleAxis);
            if (baseScaleMagnitude <= 0.0001f)
                return 0f;

            var lossyAxis = Mathf.Abs(useXAxis ? transform.lossyScale.x : transform.lossyScale.y);
            var parentLossyAxis = lossyAxis / baseScaleMagnitude;
            if (parentLossyAxis <= 0.0001f)
                return 0f;

            var resolvedWorldLength = Mathf.Max(0f, desiredWorldLength);
            var sign = Mathf.Sign(baseLocalScaleAxis);
            return sign * (resolvedWorldLength / (baseSpriteLocalSize * parentLossyAxis));
        }

        static bool TryMapCoordinateToNormalized(
            float min,
            float max,
            SliderAreaOriginSide originSide,
            float paddingStart,
            float paddingEnd,
            float coordinate,
            out float normalizedValue)
        {
            normalizedValue = 0f;

            paddingStart = Mathf.Max(0f, paddingStart);
            paddingEnd = Mathf.Max(0f, paddingEnd);

            if (originSide == SliderAreaOriginSide.Min)
            {
                var start = min + paddingStart;
                var end = max - paddingEnd;
                var length = end - start;
                if (length <= 0.0001f)
                    return false;

                normalizedValue = Mathf.Clamp01((coordinate - start) / length);
                return true;
            }

            var reversedStart = max - paddingStart;
            var reversedEnd = min + paddingEnd;
            var reversedLength = reversedStart - reversedEnd;
            if (reversedLength <= 0.0001f)
                return false;

            normalizedValue = Mathf.Clamp01((reversedStart - coordinate) / reversedLength);
            return true;
        }

        static void TrySetFloatVar(IVarStore vars, int varId, float value)
        {
            if (varId == 0)
                return;

            vars.TrySetVariant(varId, DynamicVariant.FromFloat(value));
        }

        static void TrySetIntVar(IVarStore vars, int varId, int value)
        {
            if (varId == 0)
                return;

            vars.TrySetVariant(varId, DynamicVariant.FromInt(value));
        }
    }
}
