#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Channel;
using Game.Commands.VNext;
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
    internal enum WorldSliderAreaResolveStatus
    {
        Success = 10,
        AreaScopeUnavailable = 20,
        AreaHubUnavailable = 30,
        AreaPlayerUnavailable = 40,
        UnsupportedShape = 50,
    }

    internal readonly struct WorldSliderResolvedEntry
    {
        public readonly int Index;
        public readonly float RawValue;
        public readonly float NormalizedValue;
        public readonly WorldSliderSegmentEntryBase? SourceEntry;

        public WorldSliderResolvedEntry(int index, float rawValue, float normalizedValue, WorldSliderSegmentEntryBase? sourceEntry)
        {
            Index = index;
            RawValue = rawValue;
            NormalizedValue = normalizedValue;
            SourceEntry = sourceEntry;
        }
    }

    internal sealed class WorldSliderResolvedSegmentLayout
    {
        public float MinValue;
        public float MaxValue;
        public readonly List<WorldSliderResolvedEntry> Entries = new();
        public readonly List<float> Boundaries = new();
    }

    internal readonly struct WorldSliderTransformPose
    {
        public readonly Vector3 LocalPosition;
        public readonly Quaternion LocalRotation;
        public readonly Vector3 LocalScale;

        public WorldSliderTransformPose(Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
        {
            LocalPosition = localPosition;
            LocalRotation = localRotation;
            LocalScale = localScale;
        }

        public WorldSliderTransformPose(Transform transform)
        {
            LocalPosition = transform.localPosition;
            LocalRotation = transform.localRotation;
            LocalScale = transform.localScale;
        }

        public void ApplyTo(Transform transform)
        {
            transform.localPosition = LocalPosition;
            transform.localRotation = LocalRotation;
            transform.localScale = LocalScale;
        }
    }

    internal readonly struct WorldSliderSpriteRenderState
    {
        public readonly SpriteDrawMode DrawMode;
        public readonly Vector2 Size;

        public WorldSliderSpriteRenderState(SpriteRenderer renderer)
        {
            DrawMode = renderer.drawMode;
            Size = renderer.size;
        }

        public void ApplyTo(SpriteRenderer renderer)
        {
            renderer.drawMode = DrawMode;
            renderer.size = Size;
        }
    }

    internal readonly struct WorldSliderImageRenderState
    {
        public readonly Vector2 SizeDelta;

        public WorldSliderImageRenderState(Image image)
        {
            SizeDelta = image.rectTransform != null ? image.rectTransform.sizeDelta : Vector2.zero;
        }

        public void ApplyTo(Image image)
        {
            if (image.rectTransform != null)
                image.rectTransform.sizeDelta = SizeDelta;
        }
    }

    internal enum WorldSliderRuntimeVisualTargetKind
    {
        None = 0,
        SpriteRenderer = 10,
        Image = 20,
    }

    internal readonly struct WorldSliderAreaSnapshot
    {
        public readonly Vector3 Center;
        public readonly Vector2 Size;
        public readonly AreaPlane Plane;

        public WorldSliderAreaSnapshot(Vector3 center, Vector2 size, AreaPlane plane)
        {
            Center = center;
            Size = size;
            Plane = plane;
        }

        public bool ApproximatelyEquals(in WorldSliderAreaSnapshot other)
        {
            return Plane == other.Plane &&
                   Vector3.SqrMagnitude(Center - other.Center) <= 0.0000001f &&
                   Vector2.SqrMagnitude(Size - other.Size) <= 0.0000001f;
        }
    }

    internal sealed class WorldSliderSpawnedRuntimeInstance
    {
        public Transform Root = null!;
        public IScopeNode? Scope;
        public IObjectResolver Resolver = null!;
        public WorldSliderTransformPose BasePose;
        public Transform? VisualTransform;
        public WorldSliderTransformPose VisualPose;
        public WorldSliderRuntimeVisualTargetKind VisualTargetKind;
        public SpriteRenderer? SpriteRenderer;
        public WorldSliderSpriteRenderState SpriteState;
        public Image? Image;
        public WorldSliderImageRenderState ImageState;
        public WorldSliderSpawnUnitKind UnitKind;
        public int UnitIndex;
        public float StartRawValue;
        public float EndRawValue;
        public float StartNormalized;
        public float EndNormalized;
        public int EntryIndex = -1;
        public float EntryRawValue;
        public float EntryNormalized;
    }

    internal static class WorldSliderRuntimeHelpers
    {
        public const string VarKeyTargetRaw = "GameLib.WorldSlider.targetRaw";
        public const string VarKeyTargetNormalized = "GameLib.WorldSlider.targetNormalized";
        public const string VarKeyDisplayedRaw = "GameLib.WorldSlider.displayedRaw";
        public const string VarKeyDisplayedNormalized = "GameLib.WorldSlider.displayedNormalized";
        public const string VarKeyDeltaRaw = "GameLib.WorldSlider.deltaRaw";
        public const string VarKeyDeltaNormalized = "GameLib.WorldSlider.deltaNormalized";
        public const string VarKeyCrossingDirection = "GameLib.WorldSlider.crossingDirection";
        public const string VarKeyEntryIndex = "GameLib.WorldSlider.entryIndex";
        public const string VarKeyEntryValue = "GameLib.WorldSlider.entryValue";
        public const string VarKeyEntryNormalized = "GameLib.WorldSlider.entryNormalized";
        public const string VarKeyUnitKind = "GameLib.WorldSlider.unitKind";
        public const string VarKeyUnitIndex = "GameLib.WorldSlider.unitIndex";
        public const string VarKeySegmentStartRaw = "GameLib.WorldSlider.segmentStartRaw";
        public const string VarKeySegmentEndRaw = "GameLib.WorldSlider.segmentEndRaw";
        public const string VarKeySegmentStartNormalized = "GameLib.WorldSlider.segmentStartNormalized";
        public const string VarKeySegmentEndNormalized = "GameLib.WorldSlider.segmentEndNormalized";

        public static WorldSliderVisualizerPreset ResolveVisualizerPreset(
            DynamicValue<WorldSliderVisualizerPreset> value,
            IDynamicContext context)
        {
            return value.GetOrDefault(context, new WorldSliderVisualizerPreset());
        }

        public static WorldSliderPlayerPreset ResolvePlayerPreset(
            DynamicValue<WorldSliderPlayerPreset> value,
            IDynamicContext context)
        {
            return value.GetOrDefault(context, new WorldSliderPlayerPreset());
        }

        public static BaseRuntimeTemplateSO? ResolveRuntimeTemplate(
            DynamicValue<BaseRuntimeTemplatePreset> value,
            IDynamicContext context)
        {
            if (!value.TryGet(context, out BaseRuntimeTemplatePreset? preset) || preset == null)
                return null;

            return RuntimeTemplatePresetResolver.ResolveTemplateSO(preset);
        }

        public static WorldSliderResolvedSegmentLayout BuildSegmentLayout(
            WorldSliderVisualizerPreset visualizerPreset,
            IDynamicContext context,
            float minValue,
            float maxValue)
        {
            var layout = new WorldSliderResolvedSegmentLayout
            {
                MinValue = minValue,
                MaxValue = maxValue,
            };

            layout.Boundaries.Add(minValue);

            var segmented = visualizerPreset.Segmented;
            if (segmented.PlacementMode == WorldSliderSegmentPlacementMode.EqualInterval)
            {
                var step = Mathf.Abs(segmented.IntervalStep.GetOrDefault(context, 0f));
                if (step > Mathf.Epsilon && maxValue - minValue > Mathf.Epsilon)
                {
                    int entryIndex = 0;
                    for (var value = minValue + step; value < maxValue - Mathf.Epsilon; value += step)
                    {
                        var clamped = Mathf.Clamp(value, minValue, maxValue);
                        var normalized = Normalize(clamped, minValue, maxValue);
                        layout.Entries.Add(new WorldSliderResolvedEntry(entryIndex, clamped, normalized, null));
                        layout.Boundaries.Add(clamped);
                        entryIndex++;
                    }
                }
            }
            else
            {
                var temp = new List<(float RawValue, WorldSliderSegmentEntryBase Entry)>();
                var sourceEntries = segmented.Entries;
                for (int i = 0; i < sourceEntries.Count; i++)
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
                for (int i = 0; i < temp.Count; i++)
                {
                    var rawValue = temp[i].RawValue;
                    if (Mathf.Approximately(rawValue, minValue) || Mathf.Approximately(rawValue, maxValue))
                        continue;

                    if (lastRawValue.HasValue && Mathf.Abs(rawValue - lastRawValue.Value) <= 0.0001f)
                        continue;

                    lastRawValue = rawValue;
                    var normalized = Normalize(rawValue, minValue, maxValue);
                    layout.Entries.Add(new WorldSliderResolvedEntry(entryIndex, rawValue, normalized, temp[i].Entry));
                    layout.Boundaries.Add(rawValue);
                    entryIndex++;
                }
            }

            layout.Boundaries.Add(maxValue);
            return layout;
        }

        public static WorldSliderAreaResolveStatus TryResolveAreaSnapshot(
            IScopeNode scope,
            ActorSource actorSource,
            string channelTag,
            ref ActorSourceResolveCache actorSourceCache,
            out WorldSliderAreaSnapshot snapshot)
        {
            snapshot = default;

            var areaScope = ActorSourceFastResolver.ResolveCached(scope, actorSource, ref actorSourceCache);
            if (areaScope?.Resolver == null)
                return WorldSliderAreaResolveStatus.AreaScopeUnavailable;

            if (!areaScope.Resolver.TryResolve<IAreaChannelHubService>(out var hub) || hub == null)
                return WorldSliderAreaResolveStatus.AreaHubUnavailable;

            var normalizedTag = string.IsNullOrWhiteSpace(channelTag) ? "default" : channelTag.Trim();
            if (!hub.TryGetPlayer(normalizedTag, out var player) || player == null)
                return WorldSliderAreaResolveStatus.AreaPlayerUnavailable;

            if (player.Definition.Shape is not RectAreaShape rectShape)
                return WorldSliderAreaResolveStatus.UnsupportedShape;

            var center = ResolveAreaBasePosition(player.Definition, areaScope);
            var size = new Vector2(Mathf.Max(0f, rectShape.Size.x), Mathf.Max(0f, rectShape.Size.y));
            snapshot = new WorldSliderAreaSnapshot(center, size, player.Definition.Plane);
            return WorldSliderAreaResolveStatus.Success;
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

        public static float ResolveAreaLength(
            in WorldSliderAreaSnapshot snapshot,
            WorldSliderAreaFillAxis fillAxis)
        {
            return Mathf.Max(0f, fillAxis == WorldSliderAreaFillAxis.SizeX ? snapshot.Size.x : snapshot.Size.y);
        }

        public static float ResolveAreaCrossLength(
            in WorldSliderAreaSnapshot snapshot,
            WorldSliderAreaFillAxis fillAxis)
        {
            return Mathf.Max(0f, fillAxis == WorldSliderAreaFillAxis.SizeX ? snapshot.Size.y : snapshot.Size.x);
        }

        public static void ResolveFilledBarGeometry(
            in WorldSliderAreaSnapshot snapshot,
            WorldSliderAreaFillAxis fillAxis,
            WorldSliderAreaOriginSide originSide,
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
            in WorldSliderAreaSnapshot snapshot,
            WorldSliderAreaFillAxis fillAxis,
            WorldSliderAreaOriginSide originSide,
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
            in WorldSliderAreaSnapshot snapshot,
            WorldSliderAreaFillAxis fillAxis,
            WorldSliderAreaOriginSide originSide,
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
            in WorldSliderAreaSnapshot snapshot,
            WorldSliderAreaFillAxis fillAxis,
            WorldSliderAreaOriginSide originSide,
            float normalizedValue)
        {
            var coordinate = MapNormalizedToAreaCoordinate(snapshot, fillAxis, originSide, normalizedValue);
            return ResolveAreaWorldPosition(snapshot, fillAxis, coordinate);
        }

        public static bool ApplySpawnedBarGeometry(
            in WorldSliderSpawnedRuntimeInstance instance,
            in WorldSliderAreaSnapshot areaSnapshot,
            WorldSliderAreaFillAxis fillAxis,
            Vector3 worldCenter,
            float majorLength,
            float minorLength)
        {
            if (instance.Root == null)
                return false;

            ApplyRootTransform(instance.Root, instance.BasePose, areaSnapshot, worldCenter);

            switch (instance.VisualTargetKind)
            {
                case WorldSliderRuntimeVisualTargetKind.SpriteRenderer:
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

                case WorldSliderRuntimeVisualTargetKind.Image:
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
            in WorldSliderTransformPose basePose,
            in WorldSliderSpriteRenderState spriteState,
            in WorldSliderAreaSnapshot areaSnapshot,
            WorldSliderAreaFillAxis fillAxis,
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
            var localMajorLength = ResolveLocalLengthForAxis(transform, fillAxis == WorldSliderAreaFillAxis.SizeX, resolvedLength);
            var localCrossLength = ResolveLocalLengthForAxis(transform, fillAxis != WorldSliderAreaFillAxis.SizeX, resolvedCrossLength);
            var drawMode = renderer.drawMode;
            var supportsRendererSize = drawMode == SpriteDrawMode.Sliced || drawMode == SpriteDrawMode.Tiled;
            if (supportsRendererSize)
            {
                renderer.size = fillAxis == WorldSliderAreaFillAxis.SizeX
                    ? new Vector2(localMajorLength, localCrossLength)
                    : new Vector2(localCrossLength, localMajorLength);
                transform.localScale = basePose.LocalScale;
                return false;
            }

            renderer.size = spriteState.Size;
            var spriteLocalSize = ResolveSpriteLocalSize(renderer, spriteState);
            if (fillAxis == WorldSliderAreaFillAxis.SizeX)
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
            out WorldSliderRuntimeVisualTargetKind visualTargetKind,
            out Transform? visualTransform,
            out SpriteRenderer? spriteRenderer,
            out Image? image)
        {
            visualTargetKind = WorldSliderRuntimeVisualTargetKind.None;
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
                    visualTargetKind = WorldSliderRuntimeVisualTargetKind.SpriteRenderer;
                    spriteRenderer = player.SpriteRenderer;
                    visualTransform = player.SpriteRenderer.transform;
                    return true;
                }

                if (player.Image != null)
                {
                    visualTargetKind = WorldSliderRuntimeVisualTargetKind.Image;
                    image = player.Image;
                    visualTransform = player.Image.rectTransform;
                    return visualTransform != null;
                }
            }

            if (TryResolvePrimarySpriteRenderer(root, out spriteRenderer) && spriteRenderer != null)
            {
                visualTargetKind = WorldSliderRuntimeVisualTargetKind.SpriteRenderer;
                visualTransform = spriteRenderer.transform;
                return true;
            }

            return false;
        }

        public static float ResolveDisplayedRawValue(
            WorldSliderSegmentDisplayMode mode,
            WorldSliderVisualizerMode visualizerMode,
            WorldSliderResolvedSegmentLayout? layout,
            float continuousRawValue,
            float minValue,
            float maxValue)
        {
            var clamped = Mathf.Clamp(continuousRawValue, minValue, maxValue);
            if (mode == WorldSliderSegmentDisplayMode.Continuous ||
                visualizerMode != WorldSliderVisualizerMode.Segmented ||
                layout == null ||
                layout.Boundaries.Count == 0)
            {
                return clamped;
            }

            var result = minValue;
            for (int i = 0; i < layout.Boundaries.Count; i++)
            {
                var candidate = layout.Boundaries[i];
                if (candidate - clamped > 0.0001f)
                    break;

                result = candidate;
            }

            return Mathf.Clamp(result, minValue, maxValue);
        }

        public static void ApplyMarkerTransform(
            Transform transform,
            in WorldSliderTransformPose basePose,
            in WorldSliderAreaSnapshot areaSnapshot,
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
            in WorldSliderOutputSnapshot snapshot,
            float deltaRaw,
            float deltaNormalized)
        {
            TrySetFloatVar(vars, VarIds.GameLib.WorldSlider.targetRaw, snapshot.TargetRawValue);
            TrySetFloatVar(vars, VarIds.GameLib.WorldSlider.targetNormalized, snapshot.TargetNormalizedValue);
            TrySetFloatVar(vars, VarIds.GameLib.WorldSlider.displayedRaw, snapshot.DisplayedRawValue);
            TrySetFloatVar(vars, VarIds.GameLib.WorldSlider.displayedNormalized, snapshot.DisplayedNormalizedValue);
            TrySetFloatVar(vars, VarIds.GameLib.WorldSlider.deltaRaw, deltaRaw);
            TrySetFloatVar(vars, VarIds.GameLib.WorldSlider.deltaNormalized, deltaNormalized);
        }

        public static void WriteCrossingCommandVars(
            IVarStore vars,
            in WorldSliderResolvedEntry entry,
            WorldSliderSegmentCrossingDirection direction)
        {
            TrySetIntVar(vars, VarIds.GameLib.WorldSlider.crossingDirection, (int)direction);
            TrySetIntVar(vars, VarIds.GameLib.WorldSlider.entryIndex, entry.Index);
            TrySetFloatVar(vars, VarIds.GameLib.WorldSlider.entryValue, entry.RawValue);
            TrySetFloatVar(vars, VarIds.GameLib.WorldSlider.entryNormalized, entry.NormalizedValue);
            entry.SourceEntry?.WritePayloadVars(vars, entry.Index, entry.RawValue, entry.NormalizedValue);
        }

        public static void WriteSpawnCommandVars(
            IVarStore vars,
            in WorldSliderSpawnedRuntimeInstance instance)
        {
            TrySetIntVar(vars, VarIds.GameLib.WorldSlider.unitKind, (int)instance.UnitKind);
            TrySetIntVar(vars, VarIds.GameLib.WorldSlider.unitIndex, instance.UnitIndex);
            TrySetFloatVar(vars, VarIds.GameLib.WorldSlider.segmentStartRaw, instance.StartRawValue);
            TrySetFloatVar(vars, VarIds.GameLib.WorldSlider.segmentEndRaw, instance.EndRawValue);
            TrySetFloatVar(vars, VarIds.GameLib.WorldSlider.segmentStartNormalized, instance.StartNormalized);
            TrySetFloatVar(vars, VarIds.GameLib.WorldSlider.segmentEndNormalized, instance.EndNormalized);

            if (instance.EntryIndex >= 0)
                TrySetIntVar(vars, VarIds.GameLib.WorldSlider.entryIndex, instance.EntryIndex);

            if (instance.EntryIndex >= 0 || Mathf.Abs(instance.EntryRawValue) > 0.0001f)
                TrySetFloatVar(vars, VarIds.GameLib.WorldSlider.entryValue, instance.EntryRawValue);

            if (instance.EntryIndex >= 0 || Mathf.Abs(instance.EntryNormalized) > 0.0001f)
                TrySetFloatVar(vars, VarIds.GameLib.WorldSlider.entryNormalized, instance.EntryNormalized);
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

        public static void RestoreSpawnedRuntime(in WorldSliderSpawnedRuntimeInstance instance)
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

                UnityEngine.Object.Destroy(runtimeScope.gameObject);
            }
        }

        public static TimeScaleBehavior ResolveTimeScaleBehavior(IScopeNode scope)
        {
            return scope?.Identity?.TimeScaleBehavior ?? TimeScaleBehavior.Scaled;
        }

        static Vector3 ResolveAreaBasePosition(AreaChannelDefinition definition, IScopeNode scope)
        {
            var anchor = definition.Anchor != null ? definition.Anchor : scope.Identity?.SelfTransform;
            return anchor != null ? anchor.position + definition.CenterOffset : definition.CenterOffset;
        }

        static float MapNormalizedToAreaCoordinate(
            in WorldSliderAreaSnapshot snapshot,
            WorldSliderAreaFillAxis fillAxis,
            WorldSliderAreaOriginSide originSide,
            float normalizedValue)
        {
            var length = ResolveAreaLength(snapshot, fillAxis);
            var halfLength = length * 0.5f;
            var clamped = Mathf.Clamp01(normalizedValue);
            return originSide == WorldSliderAreaOriginSide.Min
                ? -halfLength + (length * clamped)
                : halfLength - (length * clamped);
        }

        static Vector3 ResolveAreaWorldPosition(
            in WorldSliderAreaSnapshot snapshot,
            WorldSliderAreaFillAxis fillAxis,
            float fillCoordinate)
        {
            var local = fillAxis == WorldSliderAreaFillAxis.SizeX
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
            in WorldSliderTransformPose basePose,
            in WorldSliderAreaSnapshot areaSnapshot,
            Vector3 worldCenter)
        {
            root.position = ApplyDepthOffset(worldCenter, basePose.LocalPosition, areaSnapshot.Plane);
            root.localRotation = basePose.LocalRotation;
            root.localScale = basePose.LocalScale;
        }

        static bool ApplySpriteVisualTargetGeometry(
            Transform root,
            SpriteRenderer renderer,
            in WorldSliderTransformPose visualPose,
            in WorldSliderSpriteRenderState spriteState,
            WorldSliderAreaFillAxis fillAxis,
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
            var localMajorLength = ResolveLocalLengthForAxis(targetTransform, fillAxis == WorldSliderAreaFillAxis.SizeX, resolvedLength);
            var localCrossLength = ResolveLocalLengthForAxis(targetTransform, fillAxis != WorldSliderAreaFillAxis.SizeX, resolvedCrossLength);
            var drawMode = renderer.drawMode;
            var supportsRendererSize = drawMode == SpriteDrawMode.Sliced || drawMode == SpriteDrawMode.Tiled;
            if (supportsRendererSize)
            {
                renderer.size = fillAxis == WorldSliderAreaFillAxis.SizeX
                    ? new Vector2(localMajorLength, localCrossLength)
                    : new Vector2(localCrossLength, localMajorLength);
                targetTransform.localScale = baseScale;
                return false;
            }

            renderer.size = spriteState.Size;
            var spriteLocalSize = ResolveSpriteLocalSize(renderer, spriteState);
            if (fillAxis == WorldSliderAreaFillAxis.SizeX)
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
            in WorldSliderTransformPose visualPose,
            WorldSliderAreaFillAxis fillAxis,
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

            var localMajorLength = ResolveLocalLengthForAxis(rectTransform, fillAxis == WorldSliderAreaFillAxis.SizeX, Mathf.Max(0f, majorLength));
            var localCrossLength = ResolveLocalLengthForAxis(rectTransform, fillAxis != WorldSliderAreaFillAxis.SizeX, Mathf.Max(0f, minorLength));
            rectTransform.sizeDelta = fillAxis == WorldSliderAreaFillAxis.SizeX
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

        static Vector2 ResolveSpriteLocalSize(SpriteRenderer renderer, in WorldSliderSpriteRenderState spriteState)
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
