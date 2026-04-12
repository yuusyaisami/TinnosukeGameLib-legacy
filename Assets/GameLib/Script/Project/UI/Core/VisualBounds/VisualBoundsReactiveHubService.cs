#nullable enable
using System;
using System.Collections.Generic;
using Game.Channel;
using Game.Commands.VNext;
using Game.Common;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

namespace Game.UI
{
    public sealed class VisualBoundsReactiveHubService :
        IVisualBoundsReactiveHubService,
        IScopeAcquireHandler,
        IScopeReleaseHandler,
        ILateTickable
    {
        sealed class ChannelEntry
        {
            public string Tag = "default";
            public int Order;
            public VisualBoundsReactiveChannelOptions SourceOptions = null!;
            public VisualBoundsReactiveChannelPreset SourcePreset = new();
            public VisualBoundsReactiveChannelPreset? RuntimeOverride;
            public ActorSourceResolveCache TargetActorSourceCache;
        }

        readonly IScopeNode _owner;
        readonly VisualBoundsReactiveHubMB _mb;
        readonly Dictionary<string, ChannelEntry> _channels = new(StringComparer.Ordinal);
        readonly List<ChannelEntry> _orderedChannels = new();
        readonly Vector3[] _boundsCorners = new Vector3[8];

        IScopeNode? _activeScope;
        IVisualBoundsService? _boundsService;
        IVisualBoundsOutput? _boundsOutput;
        IAnimationSpriteHubService? _spriteHub;
        ITransformAnimationHubService? _transformHub;

        bool _isAcquired;
        bool _hasLastRect;
        Rect _lastRect;

        public int ChannelCount => _orderedChannels.Count;

        public VisualBoundsReactiveHubService(IScopeNode owner, VisualBoundsReactiveHubMB mb)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _mb = mb ?? throw new ArgumentNullException(nameof(mb));
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            if (!ReferenceEquals(_owner, scope))
                return;

            _ = isReset;
            _activeScope = scope;
            _isAcquired = true;
            _hasLastRect = false;

            ResolveServices(scope);
            RebuildChannels(scope);

            if (_mb.HubSettings.ExecuteOnAcquire)
                EvaluateAndApply(force: true);
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            if (!ReferenceEquals(_owner, scope))
                return;

            _ = isReset;

            _activeScope = null;
            _boundsService = null;
            _boundsOutput = null;
            _spriteHub = null;
            _transformHub = null;
            _channels.Clear();
            _orderedChannels.Clear();
            _hasLastRect = false;
            _lastRect = default;
            _isAcquired = false;
        }

        public void LateTick()
        {
            if (!_isAcquired)
                return;

            EvaluateAndApply(force: false);
        }

        public bool Contains(string tag)
        {
            return _channels.ContainsKey(VisualBoundsReactiveTagUtility.Normalize(tag));
        }

        public bool RegisterOrReplace(string tag, VisualBoundsReactiveChannelPreset preset)
        {
            if (preset == null)
                return false;

            var normalizedTag = VisualBoundsReactiveTagUtility.Normalize(tag);
            var runtimeCopy = preset.CreateRuntimeCopy();

            if (_channels.TryGetValue(normalizedTag, out var existing))
            {
                existing.RuntimeOverride = runtimeCopy;
                Trace($"RegisterOrReplace override applied. tag={normalizedTag}");
                return true;
            }

            var options = new VisualBoundsReactiveChannelOptions
            {
                PresetValue = DynamicValue<VisualBoundsReactiveChannelPreset>.FromSource(
                    new ManagedRefLiteralSource<VisualBoundsReactiveChannelPreset>(runtimeCopy)),
            };

            var entry = new ChannelEntry
            {
                Tag = normalizedTag,
                Order = _orderedChannels.Count,
                SourceOptions = options,
                SourcePreset = runtimeCopy,
                RuntimeOverride = null,
            };

            _channels.Add(normalizedTag, entry);
            _orderedChannels.Add(entry);

            Trace($"RegisterOrReplace registered new channel. tag={normalizedTag}");
            return true;
        }

        public bool Unregister(string tag)
        {
            var normalizedTag = VisualBoundsReactiveTagUtility.Normalize(tag);
            if (!_channels.TryGetValue(normalizedTag, out var entry))
                return false;

            _channels.Remove(normalizedTag);
            _orderedChannels.Remove(entry);
            RefreshOrder();
            Trace($"Unregister channel. tag={normalizedTag}");
            return true;
        }

        public void Clear()
        {
            _channels.Clear();
            _orderedChannels.Clear();
            Trace("Clear all channels.");
        }

        public bool ResetRuntimeOverrides(string tag)
        {
            var normalizedTag = VisualBoundsReactiveTagUtility.Normalize(tag);
            if (!_channels.TryGetValue(normalizedTag, out var entry))
                return false;

            entry.RuntimeOverride = null;
            Trace($"Reset runtime override. tag={normalizedTag}");
            return true;
        }

        public void ResetAllRuntimeOverrides()
        {
            for (var i = 0; i < _orderedChannels.Count; i++)
                _orderedChannels[i].RuntimeOverride = null;

            Trace("Reset runtime overrides for all channels.");
        }

        public void GetTags(List<string> output)
        {
            if (output == null)
                return;

            output.Clear();
            for (var i = 0; i < _orderedChannels.Count; i++)
                output.Add(_orderedChannels[i].Tag);
        }

        void ResolveServices(IScopeNode scope)
        {
            if (!scope.TryResolveInAncestors<IVisualBoundsService>(out _boundsService))
                scope.Resolver?.TryResolve(out _boundsService);

            if (!scope.TryResolveInAncestors<IVisualBoundsOutput>(out _boundsOutput))
                scope.Resolver?.TryResolve(out _boundsOutput);

            if (!scope.TryResolveInAncestors<IAnimationSpriteHubService>(out _spriteHub))
                scope.Resolver?.TryResolve(out _spriteHub);

            if (!scope.TryResolveInAncestors<ITransformAnimationHubService>(out _transformHub))
                scope.Resolver?.TryResolve(out _transformHub);
        }

        void RebuildChannels(IScopeNode scope)
        {
            _channels.Clear();
            _orderedChannels.Clear();

            var definitions = _mb.Channels;
            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition == null)
                    continue;

                var tag = VisualBoundsReactiveTagUtility.Normalize(definition.ChannelTag);
                if (_channels.ContainsKey(tag))
                {
                    Trace($"Duplicate channel tag skipped. tag={tag}");
                    continue;
                }

                var options = definition.CreateOptions();

                var entry = new ChannelEntry
                {
                    Tag = tag,
                    Order = _orderedChannels.Count,
                    SourceOptions = options,
                    SourcePreset = ResolveSourcePreset(scope, options),
                    RuntimeOverride = null,
                };

                _channels.Add(tag, entry);
                _orderedChannels.Add(entry);
            }
        }

        VisualBoundsReactiveChannelPreset ResolveSourcePreset(IScopeNode scope, VisualBoundsReactiveChannelOptions options)
        {
            var vars = ResolveVars(scope);
            var context = new SimpleDynamicContext(vars, scope);
            if (options.PresetValue.TryGet(context, out VisualBoundsReactiveChannelPreset? preset) && preset != null)
                return preset.CreateRuntimeCopy();

            Trace("Preset resolve failed, fallback to default preset.");
            return new VisualBoundsReactiveChannelPreset();
        }

        static IVarStore ResolveVars(IScopeNode scope)
        {
            var resolver = scope.Resolver;
            if (resolver != null && resolver.TryResolve<IBlackboardService>(out var blackboard) && blackboard?.LocalVars != null)
                return blackboard.LocalVars;

            return NullVarStore.Instance;
        }

        void EvaluateAndApply(bool force)
        {
            if (_mb.HubSettings.RebuildBeforeEvaluate)
                _boundsService?.RebuildNow();

            var output = _boundsOutput;
            if (output == null || !output.HasBounds)
            {
                _hasLastRect = false;
                return;
            }

            var currentRect = output.LocalRect;
            if (!_hasLastRect)
                force = true;

            if (!force && !HasMeaningfulChange(_lastRect, currentRect, _mb.HubSettings.PositionEpsilon, _mb.HubSettings.SizeEpsilon))
                return;

            _lastRect = currentRect;
            _hasLastRect = true;

            ApplyChannels(output);
        }

        void ApplyChannels(IVisualBoundsOutput output)
        {
            var scope = _activeScope;
            if (scope == null)
                return;

            for (var i = 0; i < _orderedChannels.Count; i++)
            {
                var entry = _orderedChannels[i];
                var preset = entry.RuntimeOverride ?? entry.SourcePreset;
                if (preset == null || !preset.Enabled)
                    continue;

                ResolveTarget(scope, preset.Target, ref entry.TargetActorSourceCache, out var resolvedTransform, out var resolvedRect);
                if (resolvedTransform == null && resolvedRect == null)
                {
                    Trace($"Skip channel: target unresolved. tag={entry.Tag}");
                    continue;
                }

                var localRect = preset.InputEffect.ApplyToLocalRect(output.LocalRect);
                var worldBounds = preset.InputEffect.ApplyToWorldBounds(output.WorldBounds);

                switch (preset.Output)
                {
                    case VisualBoundsReactiveRectTransformOutputPreset rectOutput:
                        ApplyRectTransformOutput(entry.Tag, rectOutput, resolvedTransform, resolvedRect, output.LocalSpaceRoot, localRect, worldBounds);
                        break;

                    case VisualBoundsReactiveChannelOutputPreset channelOutput:
                        ApplyChannelOutput(entry.Tag, channelOutput, localRect, worldBounds);
                        break;

                    default:
                        Trace($"Skip channel: unsupported output preset. tag={entry.Tag}");
                        break;
                }
            }
        }

        void ResolveTarget(
            IScopeNode scope,
            VisualBoundsReactiveTargetBinding targetBinding,
            ref ActorSourceResolveCache actorSourceCache,
            out Transform? targetTransform,
            out RectTransform? targetRect)
        {
            targetTransform = null;
            targetRect = null;

            if (targetTransform == null && targetBinding.UseActorSource)
            {
                var targetScope = ActorSourceFastResolver.ResolveCached(scope, targetBinding.ActorSource, ref actorSourceCache);
                targetTransform = targetScope?.Identity?.SelfTransform;
                targetRect = targetTransform as RectTransform;
            }

            if (targetTransform == null)
                targetTransform = _owner.Identity?.SelfTransform;

            if (targetRect == null)
                targetRect = targetTransform as RectTransform;
        }

        void ApplyRectTransformOutput(
            string tag,
            VisualBoundsReactiveRectTransformOutputPreset outputPreset,
            Transform? fallbackTransform,
            RectTransform? resolvedRect,
            Transform? boundsRoot,
            in Rect effectedLocalRect,
            in Bounds effectedWorldBounds)
        {
            var targetRect = outputPreset.TargetRectTransform != null
                ? outputPreset.TargetRectTransform
                : resolvedRect;

            if (targetRect == null)
            {
                targetRect = fallbackTransform as RectTransform;
                if (targetRect == null)
                {
                    Trace($"Skip rect output: RectTransform target missing. tag={tag}");
                    return;
                }
            }

            var compatibleWithRoot = IsCompatibleWithBoundsRoot(targetRect.transform, boundsRoot);

            if (outputPreset.ApplyAnchoredPosition)
            {
                if (compatibleWithRoot)
                {
                    var desiredAnchored = effectedLocalRect.center;
                    if (targetRect.parent is RectTransform parentRect)
                        targetRect.anchoredPosition = desiredAnchored - ResolveAnchorReference(targetRect, parentRect);
                    else
                        targetRect.anchoredPosition = desiredAnchored;
                }
                else if (!TryApplyAnchoredPositionFromWorldCenter(targetRect, effectedWorldBounds.center))
                {
                    Trace($"Skip rect anchoredPosition apply: projection failed. tag={tag}");
                }
            }

            if (!outputPreset.ApplySizeDelta)
            {
                if (_mb.HubSettings.EnableDebugLog)
                {
                    Trace(
                        $"Apply rect output done. tag={tag} target={targetRect.name} compatible={compatibleWithRoot} " +
                        $"anchoredPosition={targetRect.anchoredPosition} sizeDelta={targetRect.sizeDelta} localRect={effectedLocalRect} worldCenter={effectedWorldBounds.center}");
                }

                return;
            }

            if (compatibleWithRoot)
            {
                targetRect.sizeDelta = new Vector2(
                    Mathf.Max(0f, effectedLocalRect.width),
                    Mathf.Max(0f, effectedLocalRect.height));
                return;
            }

            if (!TryResolveProjectedSizeDelta(targetRect, effectedWorldBounds, out var projectedSize))
            {
                Trace($"Skip rect sizeDelta apply: projection failed. tag={tag}");
                return;
            }

            targetRect.sizeDelta = projectedSize;

            if (_mb.HubSettings.EnableDebugLog)
            {
                Trace(
                    $"Apply rect output done. tag={tag} target={targetRect.name} compatible={compatibleWithRoot} " +
                    $"anchoredPosition={targetRect.anchoredPosition} sizeDelta={targetRect.sizeDelta} localRect={effectedLocalRect} worldCenter={effectedWorldBounds.center}");
            }
        }

        bool TryApplyAnchoredPositionFromWorldCenter(RectTransform targetRect, in Vector3 worldCenter)
        {
            if (targetRect.parent is not RectTransform parentRect)
                return false;

            var camera = ResolveCanvasCamera(targetRect);
            var screenPoint = RectTransformUtility.WorldToScreenPoint(camera, worldCenter);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, camera, out var localPoint))
                return false;

            targetRect.anchoredPosition = localPoint - ResolveAnchorReference(targetRect, parentRect);
            return true;
        }

        bool TryResolveProjectedSizeDelta(RectTransform targetRect, in Bounds worldBounds, out Vector2 size)
        {
            size = default;
            if (targetRect.parent is not RectTransform parentRect)
                return false;

            FillBoundsCorners(worldBounds, _boundsCorners);
            var camera = ResolveCanvasCamera(targetRect);
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

            for (var i = 0; i < _boundsCorners.Length; i++)
            {
                var screen = RectTransformUtility.WorldToScreenPoint(camera, _boundsCorners[i]);
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screen, camera, out var localPoint))
                    continue;

                min = Vector2.Min(min, localPoint);
                max = Vector2.Max(max, localPoint);
            }

            if (!IsFinite(min.x) || !IsFinite(min.y) || !IsFinite(max.x) || !IsFinite(max.y))
                return false;

            size = new Vector2(
                Mathf.Max(0f, max.x - min.x),
                Mathf.Max(0f, max.y - min.y));
            return true;
        }

        void ApplyChannelOutput(
            string tag,
            VisualBoundsReactiveChannelOutputPreset outputPreset,
            in Rect effectedLocalRect,
            in Bounds effectedWorldBounds)
        {
            if (outputPreset.SpriteMode == VisualBoundsReactiveSpriteApplyMode.SpriteOnly ||
                outputPreset.SpriteMode == VisualBoundsReactiveSpriteApplyMode.Both)
            {
                ApplySpriteChannelOutput(tag, outputPreset, effectedLocalRect, effectedWorldBounds);
            }

            if (outputPreset.SpriteMode == VisualBoundsReactiveSpriteApplyMode.TransformOnly ||
                outputPreset.SpriteMode == VisualBoundsReactiveSpriteApplyMode.Both)
            {
                ApplyTransformChannelOutput(tag, outputPreset, effectedWorldBounds);
            }
        }

        void ApplySpriteChannelOutput(
            string tag,
            VisualBoundsReactiveChannelOutputPreset outputPreset,
            in Rect effectedLocalRect,
            in Bounds effectedWorldBounds)
        {
            if (_spriteHub == null)
            {
                Trace($"Skip sprite output: sprite hub missing. tag={tag}");
                return;
            }

            if (!_spriteHub.TryGetPlayer(outputPreset.SpriteChannelTag, out var player) || player == null)
            {
                Trace($"Skip sprite output: player missing. tag={tag} channel={outputPreset.SpriteChannelTag}");
                return;
            }

            if (!outputPreset.ApplySpriteSize)
                return;

            if (player.SpriteRenderer != null)
            {
                if (outputPreset.ForceSlicedSpriteRenderer && player.SpriteRenderer.drawMode == SpriteDrawMode.Simple)
                    player.SpriteRenderer.drawMode = SpriteDrawMode.Sliced;

                if (player.SpriteRenderer.drawMode == SpriteDrawMode.Sliced || player.SpriteRenderer.drawMode == SpriteDrawMode.Tiled)
                {
                    player.SpriteRenderer.size = new Vector2(
                        Mathf.Max(0f, effectedWorldBounds.size.x),
                        Mathf.Max(0f, effectedWorldBounds.size.y));
                }

                return;
            }

            if (player.Image?.rectTransform != null)
            {
                player.Image.rectTransform.sizeDelta = new Vector2(
                    Mathf.Max(0f, effectedLocalRect.width),
                    Mathf.Max(0f, effectedLocalRect.height));
            }
        }

        void ApplyTransformChannelOutput(
            string tag,
            VisualBoundsReactiveChannelOutputPreset outputPreset,
            in Bounds effectedWorldBounds)
        {
            if (!outputPreset.ApplyTransformPosition)
                return;

            if (_transformHub == null)
            {
                Trace($"Skip transform output: transform hub missing. tag={tag}");
                return;
            }

            if (!_transformHub.TryGetPlayer(outputPreset.TransformChannelTag, out var player) || player == null)
            {
                Trace($"Skip transform output: player missing. tag={tag} channel={outputPreset.TransformChannelTag}");
                return;
            }

            var target = player.TargetTransform;
            if (target == null)
                return;

            var nextPosition = target.position;
            nextPosition.x = effectedWorldBounds.center.x;
            nextPosition.y = effectedWorldBounds.center.y;
            target.position = nextPosition;
        }

        static bool IsCompatibleWithBoundsRoot(Transform target, Transform? boundsRoot)
        {
            if (target == null || boundsRoot == null)
                return false;

            return ReferenceEquals(target, boundsRoot) || target.IsChildOf(boundsRoot);
        }

        static Camera? ResolveCanvasCamera(Component target)
        {
            var canvas = target.GetComponentInParent<Canvas>();
            if (canvas == null)
                return Camera.main;

            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return null;

            if (canvas.worldCamera != null)
                return canvas.worldCamera;

            return Camera.main;
        }

        static bool HasMeaningfulChange(in Rect previous, in Rect current, float positionEpsilon, float sizeEpsilon)
        {
            var previousCenter = previous.center;
            var currentCenter = current.center;
            var previousSize = previous.size;
            var currentSize = current.size;

            return
                Mathf.Abs(currentCenter.x - previousCenter.x) > positionEpsilon ||
                Mathf.Abs(currentCenter.y - previousCenter.y) > positionEpsilon ||
                Mathf.Abs(currentSize.x - previousSize.x) > sizeEpsilon ||
                Mathf.Abs(currentSize.y - previousSize.y) > sizeEpsilon;
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

        static void FillBoundsCorners(in Bounds bounds, Vector3[] corners)
        {
            var min = bounds.min;
            var max = bounds.max;
            corners[0] = new Vector3(min.x, min.y, min.z);
            corners[1] = new Vector3(max.x, min.y, min.z);
            corners[2] = new Vector3(max.x, max.y, min.z);
            corners[3] = new Vector3(min.x, max.y, min.z);
            corners[4] = new Vector3(min.x, min.y, max.z);
            corners[5] = new Vector3(max.x, min.y, max.z);
            corners[6] = new Vector3(max.x, max.y, max.z);
            corners[7] = new Vector3(min.x, max.y, max.z);
        }

        void RefreshOrder()
        {
            for (var i = 0; i < _orderedChannels.Count; i++)
                _orderedChannels[i].Order = i;
        }

        void Trace(string message)
        {
            if (!_mb.HubSettings.EnableDebugLog)
                return;

            Debug.Log($"[VisualBoundsReactiveHub] {message}");
        }

        static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}