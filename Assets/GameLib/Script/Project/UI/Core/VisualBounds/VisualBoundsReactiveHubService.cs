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
        int _suppressInitialOutputUntilFrame = -1;
        bool _needsInitialChannelOutputApply;

        public int ChannelCount => _orderedChannels.Count;
        public bool EnableDebugLog => _mb.HubSettings.EnableDebugLog;

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
            _suppressInitialOutputUntilFrame = Time.frameCount + 1;
            _needsInitialChannelOutputApply = true;

            Trace($"[Acquire] scope={DescribeScope(scope)} frame={Time.frameCount} executeOnAcquire={_mb.HubSettings.ExecuteOnAcquire} suppressUntil={_suppressInitialOutputUntilFrame} channels={_mb.Channels.Count}");

            ResolveServices(scope);
            RebuildChannels(scope);

            Trace($"[Acquire] services boundsService={_boundsService != null} boundsOutput={_boundsOutput != null} spriteHub={_spriteHub != null} transformHub={_transformHub != null} channelCount={_orderedChannels.Count}");

            if (_mb.HubSettings.ExecuteOnAcquire)
            {
                Trace("[Acquire] ExecuteOnAcquire=true, evaluating immediately.");
                EvaluateAndApply(force: true);
            }
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
            _suppressInitialOutputUntilFrame = -1;
            _needsInitialChannelOutputApply = false;
            _isAcquired = false;

            Trace($"[Release] scope={DescribeScope(scope)} frame={Time.frameCount}");
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

            Trace($"[RegisterOrReplace] tag={normalizedTag} existing={_channels.ContainsKey(normalizedTag)} preset={DescribePreset(runtimeCopy)} frame={Time.frameCount}");

            if (_channels.TryGetValue(normalizedTag, out var existing))
            {
                existing.RuntimeOverride = runtimeCopy;
                Trace($"[RegisterOrReplace] override applied. tag={normalizedTag} totalChannels={_orderedChannels.Count}");
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

            Trace($"[RegisterOrReplace] registered new channel. tag={normalizedTag} totalChannels={_orderedChannels.Count}");
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
            Trace($"[Unregister] tag={normalizedTag} totalChannels={_orderedChannels.Count}");
            return true;
        }

        public void Clear()
        {
            _channels.Clear();
            _orderedChannels.Clear();
            Trace("[Clear] all channels cleared.");
        }

        public bool ResetRuntimeOverrides(string tag)
        {
            var normalizedTag = VisualBoundsReactiveTagUtility.Normalize(tag);
            if (!_channels.TryGetValue(normalizedTag, out var entry))
                return false;

            entry.RuntimeOverride = null;
            Trace($"[ResetRuntimeOverrides] tag={normalizedTag}");
            return true;
        }

        public void ResetAllRuntimeOverrides()
        {
            for (var i = 0; i < _orderedChannels.Count; i++)
                _orderedChannels[i].RuntimeOverride = null;

            Trace("[ResetAllRuntimeOverrides] all overrides cleared.");
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

            Trace($"[ResolveServices] scope={DescribeScope(scope)} boundsService={_boundsService != null} boundsOutput={_boundsOutput != null} spriteHub={_spriteHub != null} transformHub={_transformHub != null}");
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
                    SourcePreset = ResolveSourcePreset(scope, tag, options),
                    RuntimeOverride = null,
                };

                _channels.Add(tag, entry);
                _orderedChannels.Add(entry);

                Trace($"[RebuildChannels] registered tag={tag} order={entry.Order} preset={DescribePreset(entry.SourcePreset)}");
            }

            Trace($"[RebuildChannels] completed. definitions={definitions.Count} channels={_orderedChannels.Count}");
        }

        VisualBoundsReactiveChannelPreset ResolveSourcePreset(IScopeNode scope, string tag, VisualBoundsReactiveChannelOptions options)
        {
            var vars = ResolveVars(scope);
            var context = new SimpleDynamicContext(vars, scope);
            if (options.PresetValue.TryGet(context, out VisualBoundsReactiveChannelPreset? preset) && preset != null)
            {
                Trace($"[PresetResolve] tag={tag} resolved={DescribePreset(preset)}");
                return preset.CreateRuntimeCopy();
            }

            var fallback = new VisualBoundsReactiveChannelPreset();
            Trace($"[PresetResolve] tag={tag} failed, fallback={DescribePreset(fallback)}");
            return fallback;
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
                Trace($"[Evaluate] No bounds available. force={force} frame={Time.frameCount} hasLastRect={_hasLastRect} needsInitialApply={_needsInitialChannelOutputApply} suppressUntil={_suppressInitialOutputUntilFrame}");
                _hasLastRect = false;
                return;
            }

            var currentRect = output.LocalRect;
            if (!_hasLastRect)
                force = true;
            else if (_needsInitialChannelOutputApply && Time.frameCount > _suppressInitialOutputUntilFrame)
                force = true;

            if (!force && !HasMeaningfulChange(_lastRect, currentRect, _mb.HubSettings.PositionEpsilon, _mb.HubSettings.SizeEpsilon))
            {
                Trace($"[Evaluate] Skip no meaningful change. current={DescribeRect(currentRect)} last={DescribeRect(_lastRect)} epsPos={_mb.HubSettings.PositionEpsilon:0.###} epsSize={_mb.HubSettings.SizeEpsilon:0.###}");
                return;
            }

            Trace($"[Evaluate] Apply. force={force} frame={Time.frameCount} current={DescribeRect(currentRect)} world={DescribeBounds(output.WorldBounds)} root={DescribeTransform(output.LocalSpaceRoot)}");
            _lastRect = currentRect;
            _hasLastRect = true;

            ApplyChannels(output);

            if (_needsInitialChannelOutputApply && Time.frameCount > _suppressInitialOutputUntilFrame)
            {
                _needsInitialChannelOutputApply = false;
                Trace("[Evaluate] Initial apply completed.");
            }
        }

        void ApplyChannels(IVisualBoundsOutput output)
        {
            var scope = _activeScope;
            if (scope == null)
                return;

            Trace($"[ApplyChannels] count={_orderedChannels.Count} localRect={DescribeRect(output.LocalRect)} worldBounds={DescribeBounds(output.WorldBounds)} root={DescribeTransform(output.LocalSpaceRoot)} clampHasValue={output.LastClamp.HasValue} clampMaxRate={output.LastClamp.MaxRate:0.###}");

            for (var i = 0; i < _orderedChannels.Count; i++)
            {
                var entry = _orderedChannels[i];
                var preset = entry.RuntimeOverride ?? entry.SourcePreset;
                if (preset == null || !preset.Enabled)
                    continue;

                if (Time.frameCount <= _suppressInitialOutputUntilFrame)
                {
                    if (preset.Output is VisualBoundsReactiveChannelOutputPreset)
                        Trace($"Skip channel output on acquire frame. tag={entry.Tag}");
                    else
                        Trace($"Skip rect output on acquire frame. tag={entry.Tag}");

                    continue;
                }

                Trace($"[Channel] tag={entry.Tag} order={entry.Order} preset={DescribePreset(preset)} override={entry.RuntimeOverride != null}");
                ResolveTarget(scope, preset.Target, ref entry.TargetActorSourceCache, out var resolvedTransform, out var resolvedRect);
                if (resolvedTransform == null && resolvedRect == null)
                {
                    Trace($"[Channel] Skip target unresolved. tag={entry.Tag} target={DescribeTargetBinding(preset.Target)}");
                    continue;
                }

                var localRect = preset.InputEffect.ApplyToLocalRect(output.LocalRect);
                var worldBounds = preset.InputEffect.ApplyToWorldBounds(output.WorldBounds);

                Trace($"[Channel] targetTransform={DescribeTransform(resolvedTransform)} targetRect={DescribeRectTransform(resolvedRect)} effectedLocalRect={DescribeRect(localRect)} effectedWorldBounds={DescribeBounds(worldBounds)}");

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

            Trace($"[TargetResolve] binding={DescribeTargetBinding(targetBinding)} resolvedTransform={DescribeTransform(targetTransform)} resolvedRect={DescribeRectTransform(targetRect)}");
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
                    Trace($"[RectOutput] Skip target missing. tag={tag} fallbackTransform={DescribeTransform(fallbackTransform)} resolvedRect={DescribeRectTransform(resolvedRect)} presetTarget={DescribeRectTransform(outputPreset.TargetRectTransform)}");
                    return;
                }
            }

            var compatibleWithRoot = IsCompatibleWithBoundsRoot(targetRect.transform, boundsRoot);

            Trace($"[RectOutput] tag={tag} target={DescribeRectTransform(targetRect)} presetTarget={DescribeRectTransform(outputPreset.TargetRectTransform)} boundsRoot={DescribeTransform(boundsRoot)} compatible={compatibleWithRoot} applyPos={outputPreset.ApplyAnchoredPosition} applySize={outputPreset.ApplySizeDelta} local={DescribeRect(effectedLocalRect)} world={DescribeBounds(effectedWorldBounds)}");

            if (outputPreset.ApplyAnchoredPosition)
            {
                if (compatibleWithRoot)
                {
                    var desiredAnchored = effectedLocalRect.center;
                    if (targetRect.parent is RectTransform parentRect)
                    {
                        targetRect.anchoredPosition = desiredAnchored - ResolveAnchorReference(targetRect, parentRect);
                        Trace($"[RectOutput] anchoredPosition applied (local). target={DescribeRectTransform(targetRect)} desired={desiredAnchored}");
                    }
                    else
                    {
                        targetRect.anchoredPosition = desiredAnchored;
                        Trace($"[RectOutput] anchoredPosition applied (no parent). target={DescribeRectTransform(targetRect)} desired={desiredAnchored}");
                    }
                }
                else if (!TryApplyAnchoredPositionFromWorldCenter(targetRect, effectedWorldBounds.center))
                {
                    Trace($"[RectOutput] anchoredPosition projection failed. tag={tag} target={DescribeRectTransform(targetRect)} worldCenter={effectedWorldBounds.center}");
                }
            }

            if (!outputPreset.ApplySizeDelta)
            {
                if (_mb.HubSettings.EnableDebugLog)
                {
                    Trace(
                        $"[RectOutput] size skipped. tag={tag} target={DescribeRectTransform(targetRect)} compatible={compatibleWithRoot} " +
                        $"anchoredPosition={targetRect.anchoredPosition} sizeDelta={targetRect.sizeDelta} localRect={effectedLocalRect} worldCenter={effectedWorldBounds.center}");
                }

                return;
            }

            if (compatibleWithRoot)
            {
                ApplyRectTransformSize(targetRect, effectedLocalRect.size);
                Trace($"[RectOutput] size applied (local). tag={tag} target={DescribeRectTransform(targetRect)} size={effectedLocalRect.size}");
                return;
            }

            if (!TryResolveProjectedSizeDelta(targetRect, effectedWorldBounds, out var projectedSize))
            {
                Trace($"[RectOutput] size projection failed. tag={tag} target={DescribeRectTransform(targetRect)} world={DescribeBounds(effectedWorldBounds)}");
                return;
            }

            ApplyRectTransformSize(targetRect, projectedSize);
            Trace($"[RectOutput] size applied (projected). tag={tag} target={DescribeRectTransform(targetRect)} size={projectedSize}");

            if (_mb.HubSettings.EnableDebugLog)
            {
                Trace(
                    $"[RectOutput] done. tag={tag} target={DescribeRectTransform(targetRect)} compatible={compatibleWithRoot} " +
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
            Trace($"[ChannelOutput] tag={tag} mode={outputPreset.SpriteMode} spriteTag={outputPreset.SpriteChannelTag} transformTag={outputPreset.TransformChannelTag} local={DescribeRect(effectedLocalRect)} world={DescribeBounds(effectedWorldBounds)}");

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
                Trace($"[ChannelOutput] Skip sprite output: sprite hub missing. tag={tag}");
                return;
            }

            if (!_spriteHub.TryGetPlayer(outputPreset.SpriteChannelTag, out var player) || player == null)
            {
                Trace($"[ChannelOutput] Skip sprite output: player missing. tag={tag} channel={outputPreset.SpriteChannelTag}");
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
                    Trace($"[ChannelOutput] SpriteRenderer size applied. tag={tag} target={player.SpriteRenderer.name} drawMode={player.SpriteRenderer.drawMode} size={player.SpriteRenderer.size}");
                }

                return;
            }

            if (player.Image?.rectTransform != null)
            {
                ApplyRectTransformSize(player.Image.rectTransform, effectedLocalRect.size);
                Trace($"[ChannelOutput] Image rect size applied. tag={tag} image={player.Image.name} rect={DescribeRectTransform(player.Image.rectTransform)}");
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
                Trace($"[ChannelOutput] Skip transform output: transform hub missing. tag={tag}");
                return;
            }

            if (!_transformHub.TryGetPlayer(outputPreset.TransformChannelTag, out var player) || player == null)
            {
                Trace($"[ChannelOutput] Skip transform output: player missing. tag={tag} channel={outputPreset.TransformChannelTag}");
                return;
            }

            var target = player.TargetTransform;
            if (target == null)
                return;

            var nextPosition = target.position;
            nextPosition.x = effectedWorldBounds.center.x;
            nextPosition.y = effectedWorldBounds.center.y;
            target.position = nextPosition;
            Trace($"[ChannelOutput] Transform applied. tag={tag} target={DescribeTransform(target)} position={target.position}");
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

        static void ApplyRectTransformSize(RectTransform rectTransform, in Vector2 size)
        {
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(0f, size.x));
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(0f, size.y));
        }

        static string DescribeScope(IScopeNode? scope)
        {
            if (scope == null)
                return "<null>";

            var id = scope.Identity?.Id;
            var identityText = string.IsNullOrWhiteSpace(id) ? "<none>" : id;
            return $"{scope.Kind} id='{identityText}' transform='{DescribeTransform(scope.Identity?.SelfTransform)}'";
        }

        static string DescribeTargetBinding(VisualBoundsReactiveTargetBinding? target)
        {
            if (target == null)
                return "<null>";

            return target.UseActorSource
                ? $"UseActorSource=True actorSource={DescribeActorSource(target.ActorSource)}"
                : "UseActorSource=False actorSource=Current";
        }

        static string DescribeActorSource(ActorSource source)
        {
            return source.Kind switch
            {
                ActorSourceKind.ByIdentity => $"ByIdentity(kind={source.Identity.kind}, id='{source.Identity.id}', category='{source.Identity.category}', requireActive={source.Identity.requireActive}, searchScope={source.Identity.searchScope})",
                ActorSourceKind.FromUnityObject => source.UnityObject != null ? $"FromUnityObject('{source.UnityObject.name}')" : "FromUnityObject(null)",
                ActorSourceKind.Shared => source.Shared == null ? "Shared(null)" : $"Shared(tag='{source.Shared.SharedTag}', kind={source.Shared.SharedHubActorSource.Kind})",
                ActorSourceKind.ContextSlot => $"ContextSlot({source.ContextSlot})",
                _ => source.Kind.ToString(),
            };
        }

        static string DescribePreset(VisualBoundsReactiveChannelPreset? preset)
        {
            if (preset == null)
                return "<null>";

            return $"Enabled={preset.Enabled} Target={DescribeTargetBinding(preset.Target)} Input={DescribeInputEffect(preset.InputEffect)} Output={DescribeOutputPreset(preset.Output)}";
        }

        static string DescribeInputEffect(VisualBoundsReactiveInputEffectPreset? effect)
        {
            if (effect == null)
                return "<null>";

            return $"Offset={effect.Offset} ExpandL={effect.ExpandLeft:0.###} ExpandR={effect.ExpandRight:0.###} ExpandT={effect.ExpandTop:0.###} ExpandB={effect.ExpandBottom:0.###}";
        }

        static string DescribeOutputPreset(VisualBoundsReactiveOutputPreset? output)
        {
            if (output == null)
                return "<null>";

            return output switch
            {
                VisualBoundsReactiveRectTransformOutputPreset rect => $"RectTransform target={DescribeRectTransform(rect.TargetRectTransform)} applyPos={rect.ApplyAnchoredPosition} applySize={rect.ApplySizeDelta}",
                VisualBoundsReactiveChannelOutputPreset channel => $"Channel spriteMode={channel.SpriteMode} spriteTag='{channel.SpriteChannelTag}' transformTag='{channel.TransformChannelTag}' forceSliced={channel.ForceSlicedSpriteRenderer} applySpriteSize={channel.ApplySpriteSize} applyTransformPosition={channel.ApplyTransformPosition}",
                _ => output.GetType().Name,
            };
        }

        static string DescribeTransform(Transform? target)
        {
            if (target == null)
                return "<null>";

            return $"{target.name} path='{BuildPath(target)}' local={target.localPosition} world={target.position} scale={target.localScale}";
        }

        static string DescribeRectTransform(RectTransform? target)
        {
            if (target == null)
                return "<null>";

            return $"{target.name} path='{BuildPath(target)}' rect={target.rect} anchored={target.anchoredPosition3D} sizeDelta={target.sizeDelta} anchorMin={target.anchorMin} anchorMax={target.anchorMax} pivot={target.pivot} scale={target.localScale}";
        }

        static string DescribeRect(in Rect rect)
        {
            return $"x={rect.x:0.###} y={rect.y:0.###} w={rect.width:0.###} h={rect.height:0.###} center={rect.center}";
        }

        static string DescribeBounds(in Bounds bounds)
        {
            return $"center={bounds.center} size={bounds.size}";
        }

        static string BuildPath(Transform target)
        {
            if (target == null)
                return "<null>";

            var current = target;
            var path = current.name;
            while (current.parent != null)
            {
                current = current.parent;
                path = $"{current.name}/{path}";
            }

            return path;
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