#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using VContainer;
using Game.Channel;
using Game.Commands.VNext;
using Game.Common;
using Game.DI;
using Game.Spawn;

namespace Game.UI
{
    public sealed class ToastSystemService : IToastSystemService, IToastSystemDebugTelemetry, global::Game.IScopeAcquireHandler, global::Game.IScopeReleaseHandler
    {
        sealed class ToastEntry
        {
            public int Id;
            public ToastRequest Request;
            public global::Game.IScopeNode Scope = null!;
            public Transform Transform = null!;
            public RectTransform? RectTransform;
            public Vector2 Size;
            public Rect LocalRect;
            public Vector2 StackOffset;
            public bool IsClosing;
            public CancellationTokenSource LifetimeCts = new();
        }

        readonly ToastSystemConfig _config;
        readonly global::Game.IScopeNode _ownerScope;
        readonly Queue<ToastRequest> _pending = new();
        readonly List<ToastEntry> _visible = new();
        readonly List<ToastLogDebugRow> _debugLogs = new();

        ISceneSpawnerRegistry? _spawnerRegistry;
        IScreenClampService? _screenClampService;
        IVarStore? _ownerVars;
        CancellationTokenSource? _lifetimeCts;
        bool _queueLoopRunning;
        int _idSequence;

        public ToastSystemService(ToastSystemConfig config, global::Game.IScopeNode ownerScope)
        {
            _config = config;
            _ownerScope = ownerScope;
        }

        public string SystemTag => _config.SystemTag;
        public int VisibleCount => _visible.Count;
        public int PendingCount => _pending.Count;
        public bool QueueLoopRunning => _queueLoopRunning;

        public void OnAcquire(global::Game.IScopeNode scope, bool isReset)
        {
            var resolver = scope.Resolver;
            if (resolver != null)
            {
                resolver.TryResolve(out _spawnerRegistry);
                resolver.TryResolve(out _screenClampService);
                resolver.TryResolve(out _ownerVars);
            }

            _lifetimeCts = new CancellationTokenSource();
            Trace($"OnAcquire: Scope={scope.Identity?.Id ?? "(none)"} Kind={scope.Kind}");
        }

        public void OnRelease(global::Game.IScopeNode scope, bool isReset)
        {
            Trace("OnRelease: begin");
            _lifetimeCts?.Cancel();
            _lifetimeCts?.Dispose();
            _lifetimeCts = null;

            for (int i = 0; i < _visible.Count; i++)
            {
                var entry = _visible[i];
                entry.LifetimeCts.Cancel();
                entry.LifetimeCts.Dispose();
                entry.Request.Handle?.MarkClosed();
                TryDespawn(entry, CancellationToken.None).Forget();
            }

            while (_pending.Count > 0)
            {
                var pending = _pending.Dequeue();
                pending.Handle?.MarkClosed();
            }

            _visible.Clear();
            _queueLoopRunning = false;
            Trace("OnRelease: completed");
        }

        public bool TryEnqueue(in ToastRequest request)
        {
            return TryEnqueue(request, out _);
        }

        public bool TryEnqueue(in ToastRequest request, out ToastRequestHandle? handle)
        {
            handle = request.Handle ?? new ToastRequestHandle();

            if (_config.ToastRoot == null || _spawnerRegistry == null)
            {
                handle.MarkClosed();
                Trace("TryEnqueue rejected: ToastRoot or SpawnerRegistry missing", true);
                return false;
            }

            var effectiveRequest = request.Handle == null
                ? new ToastRequest(
                    request.OverrideTemplatePreset,
                    request.OnSpawnCommands,
                    request.OnShowCommands,
                    request.OnCloseCommands,
                    request.OnStackAdjustedCommands,
                    request.LifetimeSecondsOverride,
                    handle,
                    request.SourceVars)
                : request;

            if (_config.DisplayMode == ToastDisplayMode.Queue)
            {
                _pending.Enqueue(effectiveRequest);
                Trace($"Enqueued(queue): Pending={_pending.Count} Visible={_visible.Count}");
                TryStartQueueLoop();
                return true;
            }

            var ct = _lifetimeCts != null ? _lifetimeCts.Token : CancellationToken.None;
            Trace($"Enqueued(stack): Visible={_visible.Count} Pending={_pending.Count}");
            ShowToastAsync(effectiveRequest, ct).Forget();
            return true;
        }

        void TryStartQueueLoop()
        {
            if (_queueLoopRunning)
                return;

            var ct = _lifetimeCts != null ? _lifetimeCts.Token : CancellationToken.None;
            _queueLoopRunning = true;
            Trace("QueueLoop start requested");
            UniTask.Void(async () => await QueueLoopAsync(ct));
        }

        async UniTask QueueLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    if (_visible.Count > 0)
                    {
                        await UniTask.Yield(PlayerLoopTiming.Update, ct);
                        continue;
                    }

                    if (_pending.Count == 0)
                        break;

                    var req = _pending.Dequeue();
                    Trace($"QueueLoop dequeue: Pending={_pending.Count}");
                    await ShowToastAsync(req, ct);
                }
            }
            catch (OperationCanceledException)
            {
                Trace("QueueLoop canceled");
            }
            finally
            {
                _queueLoopRunning = false;
                Trace("QueueLoop finished");
            }
        }

        async UniTask ShowToastAsync(ToastRequest request, CancellationToken ct)
        {
            try
            {
                var scope = await SpawnToastScopeAsync(request, ct);
                if (scope == null)
                {
                    request.Handle?.MarkClosed();
                    Trace("ShowToast aborted: SpawnToastScopeAsync returned null", true);
                    return;
                }

                var transform = GetTransformFromScope(scope);
                if (transform == null)
                {
                    request.Handle?.MarkClosed();
                    Trace("ShowToast aborted: transform is null", true);
                    return;
                }

                var rect = transform as RectTransform;
                if (rect != null)
                    ApplyAnchorPreset(rect);

                var size = ResolveToastSize(scope, rect, out var localRect);
                Trace($"ShowToast size resolved: Scope={scope.Identity?.Id ?? "(none)"} Size={size} LocalRect={localRect}");

                var entry = new ToastEntry
                {
                    Id = ++_idSequence,
                    Request = request,
                    Scope = scope,
                    Transform = transform,
                    RectTransform = rect,
                    Size = size,
                    LocalRect = localRect,
                    StackOffset = Vector2.zero,
                    IsClosing = false,
                    LifetimeCts = CancellationTokenSource.CreateLinkedTokenSource(ct),
                };

                _visible.Add(entry);
                Trace($"ShowToast spawned: EntryId={entry.Id} Name={entry.Transform.name} Visible={_visible.Count}");

                if (_config.DisplayMode == ToastDisplayMode.Stack)
                {
                    while (_visible.Count > _config.MaxVisibleCount)
                    {
                        if (_visible.Count == 0)
                            break;

                        var oldest = _visible[0];
                        if (!oldest.IsClosing)
                            CloseEntryAsync(oldest, oldest.LifetimeCts.Token).Forget();
                        else
                            break;
                    }

                    await ReflowStackAsync(ct);
                }

                Trace($"OnShowCommands start: EntryId={entry.Id} Count={request.OnShowCommands?.Count ?? 0}");
                var boundaryShown = ResolveShownBoundaryPosition(entry);
                var shown = GetShownPosition(entry);
                var showDistanceBase = ComputeTravelDistance(shown, size, rect, _config.ShowDirection, out var showTravelDebug);
                var showDistance = showDistanceBase * _config.ShowDistanceMultiplier;
                var showOffset = DirectionVector(_config.ShowDirection) * showDistance;
                var start = shown - showOffset;
                MovementDebug(
                    $"Show move: EntryId={entry.Id} Direction={_config.ShowDirection} Multiplier={_config.ShowDistanceMultiplier:F3} " +
                    $"BoundaryShown={boundaryShown} Shown={shown} Start={start} Offset={showOffset} Size={size} LocalRect={entry.LocalRect} " +
                    $"DistanceBase={showDistanceBase:F3} DistanceApplied={showDistance:F3} {showTravelDebug}");

                SetPosition(entry, start);
                await AnimateToAsync(entry, shown, _config.ShowTransformChannelTag, _config.ShowAnimationDuration, ct);
                request.Handle?.MarkShown();
                Trace($"ShowToast shown: EntryId={entry.Id} Pos={shown}");

                await ExecuteCommandsAsync(scope, request.OnShowCommands, ct, request.SourceVars);

                var lifeSeconds = request.LifetimeSecondsOverride > 0f ? request.LifetimeSecondsOverride : _config.AutoCloseSeconds;

                if (_config.DisplayMode == ToastDisplayMode.Queue)
                {
                    await WaitAndCloseAsync(entry, lifeSeconds, entry.LifetimeCts.Token);
                    return;
                }

                WaitAndCloseAsync(entry, lifeSeconds, entry.LifetimeCts.Token).Forget();
            }
            catch (OperationCanceledException)
            {
                Trace("ShowToast canceled");
                request.Handle?.MarkClosed();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ToastSystem] ShowToastAsync failed: {ex.Message}");
                Trace($"ShowToast exception: {ex.Message}", true);
                request.Handle?.MarkClosed();
            }
        }

        async UniTask WaitAndCloseAsync(ToastEntry entry, float lifeSeconds, CancellationToken ct)
        {
            try
            {
                if (lifeSeconds > 0f)
                    await UniTask.Delay(TimeSpan.FromSeconds(lifeSeconds), cancellationToken: ct);

                await CloseEntryAsync(entry, ct);
            }
            catch (OperationCanceledException)
            {
            }
        }

        async UniTask CloseEntryAsync(ToastEntry entry, CancellationToken ct)
        {
            if (entry.IsClosing)
                return;

            entry.IsClosing = true;
            Trace($"Close start: EntryId={entry.Id}");

            Trace($"OnCloseCommands start: EntryId={entry.Id} Count={entry.Request.OnCloseCommands?.Count ?? 0}");
            await ExecuteCommandsAsync(entry.Scope, entry.Request.OnCloseCommands, ct, entry.Request.SourceVars);

            var shown = GetShownPosition(entry);
            var closeMul = _config.DisplayMode == ToastDisplayMode.Stack
                ? _config.CloseDistanceMultiplierWhenStack
                : _config.CloseDistanceMultiplier;
            var closeDistanceBase = ComputeTravelDistance(shown, entry.Size, entry.RectTransform, _config.CloseDirection, out var closeTravelDebug);
            var closeDistance = closeDistanceBase * closeMul;
            if (_config.ApplyStackShiftToCloseMultiplier)
                closeDistance += Mathf.Abs(Vector2.Dot(entry.StackOffset, DirectionVector(_config.CloseDirection)));

            var closeTarget = shown + DirectionVector(_config.CloseDirection) * closeDistance;
            MovementDebug(
                $"Close move: EntryId={entry.Id} Direction={_config.CloseDirection} Multiplier={closeMul:F3} " +
                $"Shown={shown} Target={closeTarget} Distance={closeDistance:F3} Size={entry.Size} StackOffset={entry.StackOffset} " +
                $"DistanceBase={closeDistanceBase:F3} {closeTravelDebug}");

            if (_config.DisplayMode == ToastDisplayMode.Queue &&
                _config.QueueCloseAwaitMode == ToastQueueCloseAwaitMode.WaitCloseCommandsOnly)
            {
                AnimateToAsync(entry, closeTarget, _config.CloseTransformChannelTag, _config.CloseAnimationDuration, ct).Forget();
            }
            else
            {
                await AnimateToAsync(entry, closeTarget, _config.CloseTransformChannelTag, _config.CloseAnimationDuration, ct);
            }

            _visible.Remove(entry);
            entry.LifetimeCts.Cancel();
            entry.LifetimeCts.Dispose();

            await TryDespawn(entry, ct);
            entry.Request.Handle?.MarkClosed();
            Trace($"Close completed: EntryId={entry.Id} Visible={_visible.Count}");

            if (_config.DisplayMode == ToastDisplayMode.Stack)
                await ReflowStackAsync(ct);
        }

        async UniTask ReflowStackAsync(CancellationToken ct)
        {
            var cumulative = 0f;
            for (int i = _visible.Count - 1; i >= 0; i--)
            {
                var entry = _visible[i];
                var desired = DirectionVector(_config.StackShiftDirection) * cumulative;
                var changed = (desired - entry.StackOffset).sqrMagnitude > 0.0001f;
                entry.StackOffset = desired;

                if (changed)
                {
                    if (_config.AnchorReapplyOnRelayout && entry.RectTransform != null)
                        ApplyAnchorPreset(entry.RectTransform);

                    var shown = GetShownPosition(entry);
                    await AnimateToAsync(entry, shown, _config.StackShiftTransformChannelTag, _config.StackShiftDuration, ct);
                    await ExecuteCommandsAsync(entry.Scope, entry.Request.OnStackAdjustedCommands, ct, entry.Request.SourceVars);
                }

                cumulative += ComputeDistance(entry.Size, _config.StackShiftDirection) + _config.StackSpacing;
            }
        }

        async UniTask<global::Game.IScopeNode?> SpawnToastScopeAsync(ToastRequest request, CancellationToken ct)
        {
            if (_spawnerRegistry == null)
                return null;

            var preset = request.OverrideTemplatePreset ?? ResolveDefaultTemplatePreset();
            if (preset == null)
            {
                Debug.LogError("[ToastSystem] RuntimeTemplate preset is null.");
                Trace("SpawnToastScope failed: preset is null", true);
                return null;
            }

            var template = RuntimeTemplatePresetResolver.ResolveTemplateSO(preset);
            if (template == null)
            {
                Debug.LogError("[ToastSystem] RuntimeTemplate resolution failed.");
                Trace("SpawnToastScope failed: template resolution failed", true);
                return null;
            }

            var resolved = SceneSpawnerResolver.TryResolveAsyncSpawner(
                _spawnerRegistry,
                SpawnerKind.RuntimeUIElement,
                _config.SpawnerTag,
                allowTagFallback: string.IsNullOrEmpty(_config.SpawnerTag),
                allowRuntimeUiFallback: true);

            if (resolved.Spawner == null)
            {
                Trace($"SpawnToastScope failed: spawner not found. Tag={_config.SpawnerTag}", true);
                return null;
            }

            var spawnParams = SpawnParams.ForRuntime(
                template,
                Vector3.zero,
                Quaternion.identity,
                Vector3.one,
                identity: null,
                transformParent: _config.ToastRoot,
                lifetimeScopeParent: _ownerScope,
                worldSpace: false,
                allowPooling: template.UsePooling);

            var resolver = await resolved.Spawner.SpawnAsync(spawnParams, ct);
            if (resolver == null)
            {
                Trace("SpawnToastScope failed: SpawnAsync returned null resolver", true);
                return null;
            }

            if (!resolver.TryResolve<global::Game.IScopeNode>(out var scope) || scope == null)
            {
                Trace("SpawnToastScope failed: IScopeNode not resolved", true);
                return null;
            }

            EnsureScopeBuiltIfNeeded(scope);
            Trace($"OnSpawnCommands start: Count={request.OnSpawnCommands?.Count ?? 0}");
            await ExecuteCommandsAsync(scope, request.OnSpawnCommands, ct, request.SourceVars);
            Trace($"SpawnToastScope success: Scope={scope.Identity?.Id ?? "(none)"}");
            return scope;
        }

        BaseRuntimeTemplatePreset? ResolveDefaultTemplatePreset()
        {
            var vars = _ownerVars ?? NullVarStore.Instance;
            var dynCtx = new SimpleDynamicContext(vars, _ownerScope);
            if (_config.DefaultRuntimeTemplate.TryGet(dynCtx, out var preset))
                return preset;
            return null;
        }

        Vector2 ResolveToastSize(global::Game.IScopeNode scope, RectTransform? rectTransform, out Rect localRect)
        {
            localRect = ResolveFallbackLocalRect(_config.FallbackSize, rectTransform);

            if (!_config.UseVisualBounds)
                return _config.FallbackSize;

            var resolver = scope.Resolver;
            if (resolver == null)
                return _config.FallbackSize;

            if (!resolver.TryResolve<IVisualBoundsService>(out var boundsService) || boundsService == null)
                return _config.FallbackSize;

            boundsService.RebuildNow();
            if (!boundsService.TryGetLastOutput(out var output) || !output.HasBounds)
                return _config.FallbackSize;

            var size = output.LocalSize;
            localRect = output.LocalRect;
            if (!TrySanitizeSize(size, _config.FallbackSize, out var sanitized))
            {
                Trace($"ResolveToastSize fallback: Invalid VisualBounds size={size}", true);
                localRect = ResolveFallbackLocalRect(_config.FallbackSize, rectTransform);
                return _config.FallbackSize;
            }

            return sanitized;
        }

        static Rect ResolveFallbackLocalRect(Vector2 size, RectTransform? rectTransform)
        {
            if (rectTransform == null)
            {
                var half = size * 0.5f;
                return new Rect(-half.x, -half.y, size.x, size.y);
            }

            var pivot = rectTransform.pivot;
            return new Rect(
                -size.x * pivot.x,
                -size.y * pivot.y,
                size.x,
                size.y);
        }

        static bool TrySanitizeSize(Vector2 raw, Vector2 fallback, out Vector2 sanitized)
        {
            const float maxReasonableSize = 8192f;

            var width = raw.x;
            var height = raw.y;

            var invalid =
                float.IsNaN(width) || float.IsNaN(height) ||
                float.IsInfinity(width) || float.IsInfinity(height) ||
                width <= 0f || height <= 0f ||
                width > maxReasonableSize || height > maxReasonableSize;

            if (invalid)
            {
                sanitized = new Vector2(Mathf.Max(1f, fallback.x), Mathf.Max(1f, fallback.y));
                return false;
            }

            sanitized = new Vector2(width, height);
            return true;
        }

        Vector2 GetShownPosition(ToastEntry entry)
        {
            if (_config.DisplayMode == ToastDisplayMode.Stack)
            {
                var stackShown = entry.StackOffset;
                if (_config.ClampInsideScreen)
                    stackShown = ClampPosition(entry, stackShown);
                return stackShown;
            }

            return ResolveShownBoundaryPosition(entry);
        }

        Vector2 ResolveShownBoundaryPosition(ToastEntry entry)
        {
            // IMPORTANT:
            // 表示時の「境界合わせ」は Transform 原点ではなく VisualBounds.LocalRect の辺で行う。
            // これにより、見た目の実体(文字や画像)の辺が ToastRoot の境界線に一致する。
            // 以前は原点基準だったため、見た目が右(または上下)に数px〜数十pxずれる問題が発生した。
            //
            // ShowDirection ごとの意味:
            // - Left  : 可視右端(xMax)を境界線に合わせる  => shown.x = -xMax
            // - Right : 可視左端(xMin)を境界線に合わせる  => shown.x = -xMin
            // - Up    : 可視下端(yMin)を境界線に合わせる  => shown.y = -yMin
            // - Down  : 可視上端(yMax)を境界線に合わせる  => shown.y = -yMax
            var shown = Vector2.zero;
            var rect = entry.LocalRect;

            switch (_config.ShowDirection)
            {
                case ToastDirection.Left:
                    shown.x = -rect.xMax;
                    break;
                case ToastDirection.Right:
                    shown.x = -rect.xMin;
                    break;
                case ToastDirection.Up:
                    shown.y = -rect.yMin;
                    break;
                case ToastDirection.Down:
                    shown.y = -rect.yMax;
                    break;
            }

            return shown;
        }

        Vector2 ClampPosition(ToastEntry entry, Vector2 desired)
        {
            var parent = _config.ClampArea != null ? _config.ClampArea : _config.ToastRoot;
            if (parent == null)
                return desired;

            var pad = _config.ClampPadding;
            var rect = parent.rect;
            var extents = ResolvePivotExtents(entry.Size, entry.RectTransform);
            var anchorRef = ResolveAnchorReference(entry.RectTransform, parent);

            var minX = (rect.xMin + extents.Left + pad.x) - anchorRef.x;
            var maxX = (rect.xMax - extents.Right - pad.x) - anchorRef.x;
            var minY = (rect.yMin + extents.Bottom + pad.y) - anchorRef.y;
            var maxY = (rect.yMax - extents.Top - pad.y) - anchorRef.y;

            if (minX > maxX)
            {
                var centerX = (minX + maxX) * 0.5f;
                minX = centerX;
                maxX = centerX;
            }

            if (minY > maxY)
            {
                var centerY = (minY + maxY) * 0.5f;
                minY = centerY;
                maxY = centerY;
            }

            desired.x = Mathf.Clamp(desired.x, minX, maxX);
            desired.y = Mathf.Clamp(desired.y, minY, maxY);
            return desired;
        }

        float ComputeDistance(Vector2 size, ToastDirection direction)
        {
            switch (direction)
            {
                case ToastDirection.Left:
                case ToastDirection.Right:
                    return Mathf.Max(1f, size.x);
                default:
                    return Mathf.Max(1f, size.y);
            }
        }

        float ComputeTravelDistance(
            Vector2 shown,
            Vector2 size,
            RectTransform? rectTransform,
            ToastDirection direction,
            out string debug)
        {
            // IMPORTANT:
            // Show/Close の移動量は常に「VisualBounds のサイズ * Multiplier」。
            // 位置基準(Shown)とは独立しており、Root 端までの残距離などは使わない。
            // こうすることで Show/Close の対称性が保たれ、設計意図どおりの距離になる。
            var scale = rectTransform != null ? rectTransform.localScale : Vector3.one;
            var scaledSize = new Vector2(
                Mathf.Max(1f, size.x * Mathf.Abs(scale.x)),
                Mathf.Max(1f, size.y * Mathf.Abs(scale.y)));
            var distance = direction == ToastDirection.Left || direction == ToastDirection.Right
                ? scaledSize.x
                : scaledSize.y;
            debug =
                $"RootBased=True ShownAnchored={shown} Size={size} LocalScale={scale} ScaledSize={scaledSize} DistanceRaw={distance:F3}";
            return distance;
        }

        static PivotExtents ResolvePivotExtents(Vector2 size, RectTransform? rectTransform)
        {
            var scaledSize = size;
            if (rectTransform != null)
            {
                var scale = rectTransform.localScale;
                scaledSize = new Vector2(
                    size.x * Mathf.Abs(scale.x),
                    size.y * Mathf.Abs(scale.y));
            }

            if (rectTransform == null)
            {
                var half = scaledSize * 0.5f;
                return new PivotExtents(half.x, half.x, half.y, half.y);
            }

            var pivot = rectTransform.pivot;
            return new PivotExtents(
                left: scaledSize.x * pivot.x,
                right: scaledSize.x * (1f - pivot.x),
                bottom: scaledSize.y * pivot.y,
                top: scaledSize.y * (1f - pivot.y));
        }

        static Vector2 ResolveAnchorReference(RectTransform? rectTransform, RectTransform parent)
        {
            if (rectTransform == null)
                return Vector2.zero;

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

        readonly struct PivotExtents
        {
            public readonly float Left;
            public readonly float Right;
            public readonly float Bottom;
            public readonly float Top;

            public PivotExtents(float left, float right, float bottom, float top)
            {
                Left = left;
                Right = right;
                Bottom = bottom;
                Top = top;
            }
        }

        static Vector2 DirectionVector(ToastDirection direction)
        {
            switch (direction)
            {
                case ToastDirection.Up:
                    return Vector2.up;
                case ToastDirection.Down:
                    return Vector2.down;
                case ToastDirection.Left:
                    return Vector2.left;
                case ToastDirection.Right:
                    return Vector2.right;
                default:
                    return Vector2.up;
            }
        }

        void SetPosition(ToastEntry entry, Vector2 local)
        {
            if (entry.RectTransform != null)
            {
                entry.RectTransform.anchoredPosition = local;
                return;
            }

            var p = entry.Transform.localPosition;
            entry.Transform.localPosition = new Vector3(local.x, local.y, p.z);
        }

        async UniTask AnimateToAsync(ToastEntry entry, Vector2 targetLocal, string channelTag, float duration, CancellationToken ct)
        {
            var startLocal = entry.RectTransform != null
                ? entry.RectTransform.anchoredPosition
                : (Vector2)entry.Transform.localPosition;
            Trace($"AnimateTo start: EntryId={entry.Id} Start={startLocal} Target={targetLocal} Duration={duration:F3} ChannelTag={channelTag}");

            if (!string.IsNullOrEmpty(channelTag) &&
                entry.Scope.TryResolveInAncestors<ITransformAnimationHubService>(out var hub) &&
                hub != null &&
                hub.TryGetPlayer(channelTag, out var player) &&
                player != null)
            {
                var playerTarget = player.TargetTransform;
                if (playerTarget != null && !ReferenceEquals(playerTarget, entry.Transform))
                {
                    Debug.LogWarning(
                        $"[ToastSystem] TransformChannel target mismatch. Tag={channelTag}, " +
                        $"PlayerTarget={playerTarget.name}, ToastTarget={entry.Transform.name}. Fallback animation will be used.");
                    Trace($"AnimateTo channel skipped (target mismatch): PlayerTarget={playerTarget.name} ToastTarget={entry.Transform.name}", true);
                }
                else if (playerTarget == null)
                {
                    Debug.LogWarning(
                        $"[ToastSystem] TransformChannel target is null. Tag={channelTag}. Fallback animation will be used.");
                    Trace("AnimateTo channel skipped (player target null)", true);
                }
                else
                {
                    var step = new TransformAnimationPresetStep
                    {
                        operation = entry.RectTransform != null ? TransformAnimationOperation.AnchoredPosition : TransformAnimationOperation.LocalPosition,
                        duration = DynamicValueExtensions.FromLiteral(Mathf.Max(0f, duration)),
                        ease = Ease.OutCubic,
                        relative = false,
                        fireAndForget = false,
                    };

                    var current = entry.Transform.localPosition;
                    var to = new Vector3(targetLocal.x, targetLocal.y, current.z);
                    try
                    {
                        Trace($"AnimateTo using channel: Tag={channelTag} Op={step.operation}");
                        await player.PlayStepAsync(to, step);
                        SetPosition(entry, targetLocal);
                        MovementDebug($"AnimateTo snap applied: EntryId={entry.Id} Target={targetLocal}");
                        Trace("AnimateTo channel completed");
                        return;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[ToastSystem] TransformChannel step failed. Tag={channelTag}, Message={ex.Message}");
                        Trace($"AnimateTo channel exception: {ex.Message}", true);
                    }
                }
            }
            else
            {
                Trace("AnimateTo channel unavailable -> fallback");
            }

            if (duration <= 0f)
            {
                SetPosition(entry, targetLocal);
                Trace("AnimateTo fallback immediate set (duration<=0)");
                return;
            }

            var start = entry.RectTransform != null
                ? entry.RectTransform.anchoredPosition
                : (Vector2)entry.Transform.localPosition;

            var elapsed = 0f;
            while (elapsed < duration)
            {
                ct.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = DOVirtual.EasedValue(0f, 1f, t, Ease.OutCubic);
                SetPosition(entry, Vector2.LerpUnclamped(start, targetLocal, eased));
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            SetPosition(entry, targetLocal);
            Trace("AnimateTo fallback tween completed");
        }

        async UniTask ExecuteCommandsAsync(global::Game.IScopeNode scope, CommandListData? commands, CancellationToken ct, IVarStore? sourceVars)
        {
            if (commands == null || commands.Count == 0)
                return;

            var resolver = scope.Resolver;
            if (resolver == null)
                return;

            if (!resolver.TryResolve<ICommandRunner>(out var runner) || runner == null)
                return;

            var vars = BuildExecutionVars(scope, sourceVars);

            var runCtx = new CommandContext(scope, vars, runner, actor: scope, options: CommandRunOptions.Default);
            try
            {
                var result = await runner.ExecuteListAsync(commands, runCtx, ct, runCtx.Options);
                Trace($"ExecuteCommands: Scope={scope.Identity?.Id ?? "(none)"} Count={commands.Count} Status={result.Status}");
                if (result.Status == CommandRunStatus.Error && !string.IsNullOrEmpty(result.Message))
                    Debug.LogError($"[ToastSystem] CommandList failed: {result.Message}");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ToastSystem] CommandList exception: {ex.Message}");
                Trace($"ExecuteCommands exception: {ex.Message}", true);
            }
        }

        public void GetSnapshot(ToastSystemDebugSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            snapshot.ClearRows();
            snapshot.Frame = Time.frameCount;
            snapshot.UnscaledTime = Time.unscaledTime;
            snapshot.VisibleCount = _visible.Count;
            snapshot.PendingCount = _pending.Count;
            snapshot.QueueLoopRunning = _queueLoopRunning;

            for (int i = 0; i < _visible.Count; i++)
            {
                var entry = _visible[i];
                var pos = entry.RectTransform != null
                    ? entry.RectTransform.anchoredPosition
                    : (Vector2)entry.Transform.localPosition;

                snapshot.VisibleRows.Add(new ToastVisibleDebugRow
                {
                    Id = entry.Id,
                    IsClosing = entry.IsClosing,
                    Name = entry.Transform != null ? entry.Transform.name : "(null)",
                    Size = entry.Size,
                    StackOffset = entry.StackOffset,
                    Position = pos,
                });
            }

            for (int i = 0; i < _debugLogs.Count; i++)
            {
                var src = _debugLogs[i];
                snapshot.Logs.Add(new ToastLogDebugRow
                {
                    Frame = src.Frame,
                    UnscaledTime = src.UnscaledTime,
                    Message = src.Message,
                });
            }
        }

        void Trace(string message, bool forceUnityErrorLog = false)
        {
            var row = new ToastLogDebugRow
            {
                Frame = Time.frameCount,
                UnscaledTime = Time.unscaledTime,
                Message = message ?? string.Empty,
            };

            _debugLogs.Add(row);
            var cap = Mathf.Max(16, _config.DebugLogCapacity);
            if (_debugLogs.Count > cap)
                _debugLogs.RemoveAt(0);

            if (forceUnityErrorLog)
            {
                Debug.LogError($"[ToastSystem] {row.Message}");
                return;
            }

            if (_config.EnableDebugLog)
                Debug.Log($"[ToastSystem] {row.Message}");
        }

        void MovementDebug(string message)
        {
            if (!_config.EnableMovementDebugLog && !_config.EnableDebugLog)
                return;

            Debug.Log($"[ToastSystem][Move] {message}");
        }

        static IVarStore BuildExecutionVars(global::Game.IScopeNode scope, IVarStore? sourceVars)
        {
            var resolver = scope.Resolver;
            IVarStore? scopeVars = null;
            var hasScopeVars = resolver != null && resolver.TryResolve<IVarStore>(out scopeVars) && scopeVars != null;

            if (sourceVars == null)
                return hasScopeVars ? scopeVars! : NullVarStore.Instance;

            if (!hasScopeVars || ReferenceEquals(scopeVars, sourceVars))
                return sourceVars;

            var merged = new VarStore();
            scopeVars!.MergeInto(merged, overwrite: true);
            sourceVars.MergeInto(merged, overwrite: true);
            return merged;
        }

        async UniTask TryDespawn(ToastEntry entry, CancellationToken ct)
        {
            if (entry.Scope == null)
                return;

            var resolver = entry.Scope.Resolver;
            if (resolver != null && resolver.TryResolve<ICommandRunner>(out var runner) && runner != null)
            {
                var selfDespawn = new SelfDespawnCommandData();
                var list = new CommandListData();
                list.Add(selfDespawn);

                IVarStore vars;
                if (!resolver.TryResolve<IVarStore>(out vars) || vars == null)
                    vars = NullVarStore.Instance;

                var runCtx = new CommandContext(entry.Scope, vars, runner, actor: entry.Scope, options: CommandRunOptions.Default.WithSuppressCancelLog(true));
                try
                {
                    await runner.ExecuteListAsync(list, runCtx, ct, runCtx.Options);
                    return;
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ToastSystem] SelfDespawn failed: {ex.Message}");
                }
            }

            if (entry.Scope is Component comp && comp != null)
                UnityEngine.Object.Destroy(comp.gameObject);
        }

        void ApplyAnchorPreset(RectTransform rt)
        {
            if (rt == null || _config.AnchorPreset == ToastAnchorPreset.None)
                return;

            switch (_config.AnchorPreset)
            {
                case ToastAnchorPreset.TopLeft:
                    rt.anchorMin = new Vector2(0f, 1f);
                    rt.anchorMax = new Vector2(0f, 1f);
                    rt.pivot = new Vector2(0f, 1f);
                    break;
                case ToastAnchorPreset.TopRight:
                    rt.anchorMin = new Vector2(1f, 1f);
                    rt.anchorMax = new Vector2(1f, 1f);
                    rt.pivot = new Vector2(1f, 1f);
                    break;
                case ToastAnchorPreset.BottomLeft:
                    rt.anchorMin = new Vector2(0f, 0f);
                    rt.anchorMax = new Vector2(0f, 0f);
                    rt.pivot = new Vector2(0f, 0f);
                    break;
                case ToastAnchorPreset.BottomRight:
                    rt.anchorMin = new Vector2(1f, 0f);
                    rt.anchorMax = new Vector2(1f, 0f);
                    rt.pivot = new Vector2(1f, 0f);
                    break;
                case ToastAnchorPreset.Center:
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    break;
            }
        }

        static void EnsureScopeBuiltIfNeeded(global::Game.IScopeNode scope)
        {
            global::Game.ScopeFeatureInstallerUtility.EnsureScopeBuiltIfNeeded(scope);
        }

        static Transform? GetTransformFromScope(global::Game.IScopeNode scope)
        {
            var identity = scope.Identity;
            if (identity != null && identity.SelfTransform != null)
                return identity.SelfTransform;

            if (scope is Component component)
                return component.transform;

            return null;
        }
    }
}
