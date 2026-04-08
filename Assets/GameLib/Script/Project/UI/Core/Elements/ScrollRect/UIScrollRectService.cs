#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Channel;
using Game.Commands.VNext;
using Game.Common;
using Game.Spawn;
using Game.Times;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

namespace Game.UI
{
    public sealed class UIScrollRectService :
        IUIScrollRectService,
        IScopeAcquireHandler,
        IScopeReleaseHandler,
        ITickable,
        IUIInputPreviewObserver,
        IUIInputBubbleConsumer
    {
        const float Epsilon = 0.0001f;
        const float WheelSmoothTime = 0.08f;
        const int InputPriority = 220;
        const string HorizontalButtonTag = "scrollrect.horizontal";
        const string VerticalButtonTag = "scrollrect.vertical";

        sealed class AxisState
        {
            public readonly UIScrollRectAxisKind Kind;
            public readonly string ButtonTag;

            public UIScrollRectAxisPreset Preset = new();
            public ActorSourceResolveCache ScrollBarSourceCache;
            public ActorSourceResolveCache AreaSourceCache;
            public IUIScrollBarBindingService? Binding;
            public IScopeNode? BindingScope;
            public IObjectResolver? SpawnedResolver;

            public float ViewSize;
            public float ContentSize;
            public float HiddenLength;
            public float NormalizedPosition;
            public float HandleSizeNormalized = 1f;
            public bool Scrollable;
            public bool Visible;

            public bool HandleDragActive;
            public float HandleDragPointerOffset;
            public bool PageHoldActive;
            public float PageHoldTargetNormalized;
            public float NextPageRepeatTime;
            public bool WheelScrollActive;
            public float WheelScrollTargetNormalized;
            public float WheelScrollVelocity;
            public bool ExistingScopeBindErrorReported;

            public AxisState(UIScrollRectAxisKind kind, string buttonTag)
            {
                Kind = kind;
                ButtonTag = buttonTag;
            }

            public void ResetRuntime()
            {
                ScrollBarSourceCache = default;
                AreaSourceCache = default;
                Binding = null;
                BindingScope = null;
                SpawnedResolver = null;
                ViewSize = 0f;
                ContentSize = 0f;
                HiddenLength = 0f;
                NormalizedPosition = kindDefaultNormalized(Kind);
                HandleSizeNormalized = 1f;
                Scrollable = false;
                Visible = false;
                ResetInteraction();
            }

            public void ResetInteraction()
            {
                HandleDragActive = false;
                HandleDragPointerOffset = 0f;
                PageHoldActive = false;
                PageHoldTargetNormalized = 0f;
                NextPageRepeatTime = 0f;
                WheelScrollActive = false;
                WheelScrollTargetNormalized = 0f;
                WheelScrollVelocity = 0f;
                ExistingScopeBindErrorReported = false;
            }

            static float kindDefaultNormalized(UIScrollRectAxisKind kind)
            {
                return kind == UIScrollRectAxisKind.Vertical ? 1f : 0f;
            }
        }

        readonly struct ScreenRangePointerSnapshot
        {
            public readonly SliderScreenRangeSnapshot Range;
            public readonly Vector2 LocalPosition;
            public readonly bool PointerInside;

            public ScreenRangePointerSnapshot(
                in SliderScreenRangeSnapshot range,
                Vector2 localPosition,
                bool pointerInside)
            {
                Range = range;
                LocalPosition = localPosition;
                PointerInside = pointerInside;
            }
        }

        readonly struct SwipeCandidateState
        {
            public readonly bool IsActive;
            public readonly Vector2 StartScreenPosition;
            public readonly Vector2 StartViewportLocalPosition;
            public readonly Vector2 LastViewportLocalPosition;

            public SwipeCandidateState(
                bool isActive,
                Vector2 startScreenPosition,
                Vector2 startViewportLocalPosition,
                Vector2 lastViewportLocalPosition)
            {
                IsActive = isActive;
                StartScreenPosition = startScreenPosition;
                StartViewportLocalPosition = startViewportLocalPosition;
                LastViewportLocalPosition = lastViewportLocalPosition;
            }

            public SwipeCandidateState WithLastLocal(Vector2 localPosition)
            {
                return new SwipeCandidateState(IsActive, StartScreenPosition, StartViewportLocalPosition, localPosition);
            }
        }

        readonly IScopeNode _owner;
        readonly UIScrollRectMB _mb;
        readonly AxisState _horizontal;
        readonly AxisState _vertical;
        readonly Vector3[] _rectCorners = new Vector3[4];

        IScopeNode? _activeScope;
        RectTransform? _viewport;
        RectTransform? _content;
        Canvas? _canvas;
        RectTransform? _canvasRect;
        Camera? _uiCamera;
        IDynamicContext? _dynamicContext;
        IUIInputRoutingHub? _inputRoutingHub;
        IUISelectionState? _selectionState;
        IUISelectionNavigation? _selectionNavigation;
        IUISelectionBlockService? _selectionBlockService;
        ISceneSpawnerRegistry? _spawnerRegistry;
        CancellationTokenSource? _bindCts;
        IDisposable? _selectionBlockHandle;

        UIScrollRectPreset _preset = new();
        UIScrollRectSnapshot _snapshot;
        SwipeCandidateState _swipeCandidate;
        Bounds _viewBounds;
        Bounds _contentBounds;
        Vector2 _velocity;
        Vector2 _lastContentPosition;
        TimeScaleBehavior _timeScaleBehavior = TimeScaleBehavior.Scaled;

        bool _isAcquired;
        bool _contentDragActive;
        bool _hasLastContentPosition;
        bool _boundsValid;
        int _acquireFrame = -1;

        public UIScrollRectSnapshot Snapshot => _snapshot;
        int IUIInputPreviewObserver.Priority => InputPriority;
        int IUIInputBubbleConsumer.Priority => InputPriority;

        public UIScrollRectService(IScopeNode owner, UIScrollRectMB mb)
        {
            _owner = owner;
            _mb = mb;
            _horizontal = new AxisState(UIScrollRectAxisKind.Horizontal, HorizontalButtonTag);
            _vertical = new AxisState(UIScrollRectAxisKind.Vertical, VerticalButtonTag);
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            if (!ReferenceEquals(_owner, scope))
                return;

            _ = isReset;
            _activeScope = scope;
            _content = _mb.Content;
            _viewport = _mb.ViewportRect != null ? _mb.ViewportRect : _mb.transform as RectTransform;
            _canvas = _viewport != null ? _viewport.GetComponentInParent<Canvas>(true) : null;
            _canvasRect = _canvas != null ? _canvas.transform as RectTransform : null;
            _uiCamera = _canvas != null ? SliderRuntimeHelpers.ResolveCanvasCamera(_canvas) : null;
            _dynamicContext = new SimpleDynamicContext(ResolveVars(scope), scope);
            _preset = ResolvePreset(_mb.PresetValue, _dynamicContext);
            _timeScaleBehavior = SliderRuntimeHelpers.ResolveTimeScaleBehavior(scope);
            _acquireFrame = Time.frameCount;

            _horizontal.ResetRuntime();
            _vertical.ResetRuntime();
            _horizontal.Preset = _preset.Horizontal.CreateRuntimeCopy();
            _vertical.Preset = _preset.Vertical.CreateRuntimeCopy();

            if (_mb.EnableDebugLog)
            {
                Debug.Log(
                    $"[UIScrollRect][Acquire] owner='{_owner}' frame={Time.frameCount} acquireFrame={_acquireFrame} scope='{DescribeScope(scope)}' " +
                    $"viewport='{DescribeTransform(_viewport)}' content='{DescribeTransform(_content)}' " +
                    $"horizontalSource={ActorSourceOdinLabelHelper.GetLabel("ScrollBar Source", _horizontal.Preset.ScrollBarActorSource)} " +
                    $"verticalSource={ActorSourceOdinLabelHelper.GetLabel("ScrollBar Source", _vertical.Preset.ScrollBarActorSource)}");
            }

            scope.TryResolveInAncestors(out _inputRoutingHub);
            scope.TryResolveInAncestors(out _selectionState);
            scope.TryResolveInAncestors(out _selectionNavigation);
            scope.TryResolveInAncestors(out _selectionBlockService);
            scope.TryResolveInAncestors(out _spawnerRegistry);

            _inputRoutingHub?.RegisterPreview(_owner, this);
            _inputRoutingHub?.RegisterBubble(_owner, this);

            ResetTransientState();
            CancelBindTask();
            _bindCts = new CancellationTokenSource();
            _isAcquired = true;
            var contentPosText = _content != null ? _content.anchoredPosition.ToString() : "<null>";

            Debug.Log(
                $"[UIScrollRect][Acquire] owner='{_owner}' viewport='{DescribeTransform(_viewport)}' content='{DescribeTransform(_content)}' " +
                $"contentPos={contentPosText}");

            if (_content != null && _viewport != null)
            {
                UpdateBoundsAndVisuals();
                ApplyInitialAlignmentAndClamp();
            }

            UniTask.Void(async () => await BindScrollBarsAsync(_bindCts.Token));
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            if (!ReferenceEquals(_owner, scope))
                return;

            _ = scope;
            _ = isReset;

            _inputRoutingHub?.UnregisterPreview(_owner, this);
            _inputRoutingHub?.UnregisterBubble(_owner, this);

            CancelBindTask();
            ReleaseSelectionBlock();
            ReleaseAxisRuntime(_horizontal);
            ReleaseAxisRuntime(_vertical);

            _activeScope = null;
            _content = null;
            _viewport = null;
            _canvas = null;
            _canvasRect = null;
            _uiCamera = null;
            _dynamicContext = null;
            _inputRoutingHub = null;
            _selectionState = null;
            _selectionNavigation = null;
            _selectionBlockService = null;
            _spawnerRegistry = null;
            _boundsValid = false;
            _hasLastContentPosition = false;
            _isAcquired = false;
            _acquireFrame = -1;

            ResetTransientState();
        }

        public void Tick()
        {
            if (!_isAcquired || _content == null || _viewport == null)
                return;

            TryBindAxisIfMissing(_horizontal);
            TryBindAxisIfMissing(_vertical);

            UpdateBoundsAndVisuals();

            if (ApplyNonScrollableAxisAlignment())
                UpdateBoundsAndVisuals();

            var deltaTime = ResolveDeltaTime();
            var wheelAnimating = false;
            if (deltaTime > Epsilon)
            {
                wheelAnimating |= UpdateWheelScroll(_horizontal, deltaTime);
                wheelAnimating |= UpdateWheelScroll(_vertical, deltaTime);
                if (wheelAnimating)
                    UpdateBoundsAndVisuals();
            }

            if (!HasActiveInteraction() && !wheelAnimating)
            {
                if (deltaTime > Epsilon && UpdatePhysics(deltaTime))
                    UpdateBoundsAndVisuals();
            }

            var currentPosition = _content.anchoredPosition;
            if (_hasLastContentPosition)
            {
                var velocityDeltaTime = ResolveDeltaTime();
                if (HasActiveInteraction() && velocityDeltaTime > Epsilon)
                    _velocity = (currentPosition - _lastContentPosition) / velocityDeltaTime;
            }

            _lastContentPosition = currentPosition;
            _hasLastContentPosition = true;
        }

        public void RefreshLayout()
        {
            if (_content == null || _viewport == null)
                return;

            UpdateBoundsAndVisuals();
            ApplyInitialAlignmentAndClamp();
        }

        public bool SetNormalizedPosition(Vector2 value)
        {
            if (_content == null || _viewport == null)
                return false;

            UpdateBoundsAndVisuals();

            var changed = false;
            changed |= SetAxisNormalizedInternal(_horizontal, value.x, setVelocity: false);
            changed |= SetAxisNormalizedInternal(_vertical, value.y, setVelocity: false);
            if (changed)
                UpdateBoundsAndVisuals();

            return changed;
        }

        public bool SetHorizontalNormalized(float value)
        {
            if (_content == null || _viewport == null)
                return false;

            UpdateBoundsAndVisuals();
            var changed = SetAxisNormalizedInternal(_horizontal, value, setVelocity: false);
            if (changed)
                UpdateBoundsAndVisuals();
            return changed;
        }

        public bool SetVerticalNormalized(float value)
        {
            if (_content == null || _viewport == null)
                return false;

            UpdateBoundsAndVisuals();
            var changed = SetAxisNormalizedInternal(_vertical, value, setVelocity: false);
            if (changed)
                UpdateBoundsAndVisuals();
            return changed;
        }

        void IUIInputPreviewObserver.Observe(in UIInputEvent inputEvent)
        {
            if (!_isAcquired || _content == null || _viewport == null)
                return;

            switch (inputEvent.Type)
            {
                case UIInputEventType.SubmitDown:
                    HandlePreviewSubmitDown(inputEvent.PointerPosition);
                    break;
                case UIInputEventType.SubmitHeld:
                    HandlePreviewSubmitHeld();
                    break;
                case UIInputEventType.SubmitUp:
                    EndPointerInteractions(preserveVelocity: _preset.Physics.Inertia);
                    break;
                case UIInputEventType.CancelDown:
                    CancelAllInteractions();
                    break;
                case UIInputEventType.PointerMove:
                    HandlePreviewPointerMove(inputEvent.PointerPosition);
                    break;
            }
        }

        bool IUIInputBubbleConsumer.Consume(in UIInputEvent inputEvent)
        {
            if (!_isAcquired || _content == null || _viewport == null)
                return false;

            switch (inputEvent.Type)
            {
                case UIInputEventType.Scroll:
                    return HandleBubbleScroll(inputEvent.PointerPosition, inputEvent.Direction);
                case UIInputEventType.SubmitDown:
                case UIInputEventType.SubmitHeld:
                case UIInputEventType.SubmitUp:
                case UIInputEventType.CancelDown:
                case UIInputEventType.PointerMove:
                    return HasActiveInteraction();
                default:
                    return false;
            }
        }

        async UniTask BindScrollBarsAsync(CancellationToken ct)
        {
            try
            {
                if (_mb.EnableDebugLog)
                {
                    Debug.Log($"[UIScrollRect][BindScrollBarsStart] owner='{_owner}' frame={Time.frameCount} acquireFrame={_acquireFrame}");
                }

                await UniTask.SwitchToMainThread(ct);
                await BindAxisAsync(_horizontal, ct);
                await BindAxisAsync(_vertical, ct);

                if (_mb.EnableDebugLog)
                {
                    Debug.Log(
                        $"[UIScrollRect][BindScrollBarsComplete] owner='{_owner}' frame={Time.frameCount} " +
                        $"horizontalBound={_horizontal.Binding != null} verticalBound={_vertical.Binding != null}");
                }

                UpdateBoundsAndVisuals();
            }
            catch (OperationCanceledException)
            {
            }
        }

        async UniTask BindAxisAsync(AxisState axis, CancellationToken ct)
        {
            ReleaseAxisRuntime(axis);
            axis.Preset = ResolveAxisPreset(axis.Kind);

            if (_mb.EnableDebugLog)
            {
                Debug.Log(
                    $"[UIScrollRect][BindAxisStart] owner='{_owner}' axis={axis.Kind} frame={Time.frameCount} " +
                    $"sourceMode={axis.Preset.ScrollBarSourceMode} " +
                    $"source={ActorSourceOdinLabelHelper.GetLabel("ScrollBar Source", axis.Preset.ScrollBarActorSource)} " +
                    $"activeScope='{DescribeScope(_activeScope)}'");
            }

            if (_activeScope == null || !axis.Preset.Enabled)
                return;

            if (axis.Preset.ScrollBarSourceMode == UIScrollBarSourceMode.ExistingScope)
            {
                if (_mb.EnableDebugLog)
                {
                    Debug.Log(
                        $"[UIScrollRect][ResolveExistingScope] owner='{_owner}' axis={axis.Kind} frame={Time.frameCount} " +
                        $"source={ActorSourceOdinLabelHelper.GetLabel("ScrollBar Source", axis.Preset.ScrollBarActorSource)} " +
                        $"activeScope='{DescribeScope(_activeScope)}'");
                }

                var targetScope = ActorSourceFastResolver.ResolveCached(
                    _activeScope,
                    axis.Preset.ScrollBarActorSource,
                    ref axis.ScrollBarSourceCache);

                if (_mb.EnableDebugLog)
                {
                    Debug.Log(
                        $"[UIScrollRect][ResolveExistingScopeResult] owner='{_owner}' axis={axis.Kind} frame={Time.frameCount} " +
                        $"targetScope='{DescribeScope(targetScope)}'");
                }

                TryBindAxisFromScope(axis, targetScope, logExistingScopeError: true);
                return;
            }

            if (_spawnerRegistry == null || _dynamicContext == null || _viewport == null)
                return;

            var template = SliderRuntimeHelpers.ResolveRuntimeTemplate(axis.Preset.RuntimeTemplatePresetValue, _dynamicContext);
            if (template == null)
                return;

            var spawner = _spawnerRegistry.TryGet<IAsyncSpawnerService>(SpawnerKind.RuntimeUIElement);
            if (spawner == null)
                return;

            var resolver = await SliderRuntimeHelpers.SpawnRuntimeAsync(
                spawner,
                template,
                _viewport,
                _activeScope,
                allowPooling: true,
                ct);
            axis.SpawnedResolver = resolver;
            SliderRuntimeHelpers.ExtractSpawnedInfo(resolver, out _, out var runtimeScope, out _);
            TryBindAxisFromScope(axis, runtimeScope, logExistingScopeError: false);
        }

        bool TryBindAxisFromScope(AxisState axis, IScopeNode? scope, bool logExistingScopeError)
        {
            if (scope == null)
            {
                if (_mb.EnableDebugLog)
                {
                    Debug.Log(
                        $"[UIScrollRect][BindAxisFail] owner='{_owner}' axis={axis.Kind} frame={Time.frameCount} " +
                        $"source={ActorSourceOdinLabelHelper.GetLabel("ScrollBar Source", axis.Preset.ScrollBarActorSource)} " +
                        $"reason='resolved scope is null' targetScope='{DescribeScope(scope)}'");
                }

                if (logExistingScopeError && ShouldReportExistingScopeBindError(scope))
                    ReportExistingScopeBindError(axis, scope, "resolved scope is null");
                return false;
            }

            if (scope.Resolver == null)
            {
                var pendingBuild = scope is BaseLifetimeScope baseScope && !baseScope.IsBuildCompleted;
                if (_mb.EnableDebugLog)
                {
                    Debug.Log(
                        $"[UIScrollRect][BindAxis{(pendingBuild ? "Pending" : "Fail")}] owner='{_owner}' axis={axis.Kind} frame={Time.frameCount} " +
                        $"source={ActorSourceOdinLabelHelper.GetLabel("ScrollBar Source", axis.Preset.ScrollBarActorSource)} " +
                        $"reason='{(pendingBuild ? "scope exists but build is not complete" : "resolved scope has no resolver")}' targetScope='{DescribeScope(scope)}'");
                }

                if (!pendingBuild && logExistingScopeError)
                    ReportExistingScopeBindError(axis, scope, "resolved scope has no resolver");
                return false;
            }

            if (!scope.Resolver.TryResolve<IUIScrollBarBindingService>(out var binding) || binding == null)
            {
                if (_mb.EnableDebugLog)
                {
                    Debug.Log(
                        $"[UIScrollRect][BindAxisFail] owner='{_owner}' axis={axis.Kind} frame={Time.frameCount} " +
                        $"source={ActorSourceOdinLabelHelper.GetLabel("ScrollBar Source", axis.Preset.ScrollBarActorSource)} " +
                        $"reason='scope does not provide IUIScrollBarBindingService' targetScope='{DescribeScope(scope)}'");
                }

                if (logExistingScopeError)
                    ReportExistingScopeBindError(axis, scope, "scope does not provide IUIScrollBarBindingService");
                return false;
            }

            axis.BindingScope = scope;
            axis.Binding = binding;
            axis.ExistingScopeBindErrorReported = false;

            if (_mb.EnableDebugLog)
            {
                Debug.Log(
                    $"[UIScrollRect][BindAxisSuccess] owner='{_owner}' axis={axis.Kind} frame={Time.frameCount} " +
                    $"bindingScope='{DescribeScope(scope)}' buttonHub={(binding.ButtonChannelHub != null ? "present" : "<null>")} ");
            }

            EnsureBarButtonChannel(axis);
            return true;
        }

        bool ShouldReportExistingScopeBindError(IScopeNode? scope)
        {
            if (_acquireFrame >= 0 && Time.frameCount <= _acquireFrame)
                return false;

            if (scope is ICoordinatedBuildScope coordinated && !coordinated.IsBuildCompleted)
                return false;

            return true;
        }

        void TryBindAxisIfMissing(AxisState axis)
        {
            if (_activeScope == null)
                return;

            if (axis.Binding != null || !axis.Preset.Enabled)
                return;

            if (axis.Preset.ScrollBarSourceMode != UIScrollBarSourceMode.ExistingScope)
                return;

            if (_mb.EnableDebugLog)
            {
                Debug.Log(
                    $"[UIScrollRect][RetryExistingScopeBind] owner='{_owner}' axis={axis.Kind} frame={Time.frameCount} acquireFrame={_acquireFrame} " +
                    $"source={ActorSourceOdinLabelHelper.GetLabel("ScrollBar Source", axis.Preset.ScrollBarActorSource)} " +
                    $"activeScope='{DescribeScope(_activeScope)}'");
            }

            var targetScope = ActorSourceFastResolver.ResolveCached(
                _activeScope,
                axis.Preset.ScrollBarActorSource,
                ref axis.ScrollBarSourceCache);
            TryBindAxisFromScope(axis, targetScope, logExistingScopeError: true);
        }

        void EnsureBarButtonChannel(AxisState axis)
        {
            if (!axis.Preset.Enabled)
                return;

            var buttonHub = axis.Binding?.ButtonChannelHub;
            if (buttonHub == null)
                return;

            buttonHub.RegisterOrReplace(axis.ButtonTag, new ButtonChannelPreset());
        }

        void ReleaseAxisRuntime(AxisState axis)
        {
            axis.ResetInteraction();
            axis.Binding = null;
            axis.BindingScope = null;
            SliderRuntimeHelpers.ReleaseSpawnedRuntime(axis.SpawnedResolver);
            axis.SpawnedResolver = null;
        }

        void ReportExistingScopeBindError(AxisState axis, IScopeNode? scope, string reason)
        {
            if (axis.ExistingScopeBindErrorReported)
                return;

            axis.ExistingScopeBindErrorReported = true;

            var sourceLabel = ActorSourceOdinLabelHelper.GetLabel("ScrollBar Source", axis.Preset.ScrollBarActorSource);
            var buildState = scope is ICoordinatedBuildScope coordinated
                ? coordinated.IsBuildCompleted.ToString()
                : "n/a";

            Debug.LogError(
                $"[UIScrollRect][ExistingScopeError] owner='{_owner}' axis={axis.Kind} frame={Time.frameCount} acquireFrame={_acquireFrame} source={sourceLabel} reason={reason} targetScope='{DescribeScope(scope)}' buildCompleted={buildState}");
        }

        void HandlePreviewSubmitDown(Vector2 screenPosition)
        {
            if (!CanStartNewPointerInteraction())
            {
                _swipeCandidate = default;
                return;
            }

            UpdateBoundsAndVisuals();

            if (TryBeginBarInteraction(_horizontal, screenPosition))
            {
                _swipeCandidate = default;
                return;
            }

            if (TryBeginBarInteraction(_vertical, screenPosition))
            {
                _swipeCandidate = default;
                return;
            }

            if (!TryGetViewportLocal(screenPosition, out var viewportLocal, out var inside) || !inside)
            {
                _swipeCandidate = default;
                return;
            }

            _swipeCandidate = new SwipeCandidateState(
                isActive: true,
                startScreenPosition: screenPosition,
                startViewportLocalPosition: viewportLocal,
                lastViewportLocalPosition: viewportLocal);
        }

        void HandlePreviewSubmitHeld()
        {
            if (_horizontal.PageHoldActive)
                UpdatePageHold(_horizontal);
            if (_vertical.PageHoldActive)
                UpdatePageHold(_vertical);
        }

        void HandlePreviewPointerMove(Vector2 screenPosition)
        {
            if (_horizontal.HandleDragActive)
                UpdateHandleDrag(_horizontal, screenPosition);
            if (_vertical.HandleDragActive)
                UpdateHandleDrag(_vertical, screenPosition);

            if (_horizontal.PageHoldActive && TryTryResolvePointerNormalized(_horizontal, screenPosition, 0f, out var horizontalTarget, out _))
                _horizontal.PageHoldTargetNormalized = horizontalTarget;
            if (_vertical.PageHoldActive && TryTryResolvePointerNormalized(_vertical, screenPosition, 0f, out var verticalTarget, out _))
                _vertical.PageHoldTargetNormalized = verticalTarget;

            if (_contentDragActive)
            {
                if (!TryGetViewportLocal(screenPosition, out var currentLocal, out _))
                    return;

                var delta = (currentLocal - _swipeCandidate.LastViewportLocalPosition) * _preset.Interaction.DragSensitivity;
                _swipeCandidate = _swipeCandidate.WithLastLocal(currentLocal);
                ApplyContentDragDelta(delta);
                return;
            }

            if (!_swipeCandidate.IsActive)
                return;

            if (!TryGetViewportLocal(screenPosition, out var viewportLocal, out _))
                return;

            _swipeCandidate = _swipeCandidate.WithLastLocal(viewportLocal);
            var threshold = Mathf.Max(0f, _preset.Interaction.SwipeThresholdPixels);
            if ((screenPosition - _swipeCandidate.StartScreenPosition).sqrMagnitude < threshold * threshold)
                return;

            if (!CanStartNewPointerInteraction())
                return;

            BeginContentDrag(viewportLocal);
        }

        bool HandleBubbleScroll(Vector2 pointerPosition, Vector2 direction)
        {
            if (!CanHandleWheelScroll(pointerPosition))
                return false;

            UpdateBoundsAndVisuals();

            var handled = false;
            if (_vertical.Scrollable && Mathf.Abs(direction.y) > Epsilon)
                handled |= ApplyWheelScroll(_vertical, direction.y);
            else if (_horizontal.Scrollable && Mathf.Abs(direction.y) > Epsilon)
                handled |= ApplyWheelScroll(_horizontal, direction.y);

            if (_horizontal.Scrollable && Mathf.Abs(direction.x) > Epsilon)
                handled |= ApplyWheelScroll(_horizontal, direction.x);

            if (handled)
                UpdateBoundsAndVisuals();

            return handled;
        }

        bool ApplyWheelScroll(AxisState axis, float wheelDelta)
        {
            if (!axis.Scrollable)
                return false;

            var sensitivity = _preset.Interaction.WheelSensitivity;
            if (sensitivity <= Epsilon)
                return false;

            _velocity = Vector2.zero;

            var currentTarget = axis.WheelScrollActive ? axis.WheelScrollTargetNormalized : axis.NormalizedPosition;
            axis.WheelScrollTargetNormalized = Mathf.Clamp01(currentTarget + wheelDelta * sensitivity);
            axis.WheelScrollActive = true;
            axis.WheelScrollVelocity = 0f;
            return true;
        }

        bool UpdateWheelScroll(AxisState axis, float deltaTime)
        {
            if (!axis.WheelScrollActive)
                return false;

            if (!axis.Preset.Enabled || !axis.Scrollable || _content == null || axis.HiddenLength <= Epsilon)
            {
                ResetWheelScroll(axis);
                return false;
            }

            var nextNormalized = Mathf.SmoothDamp(
                axis.NormalizedPosition,
                axis.WheelScrollTargetNormalized,
                ref axis.WheelScrollVelocity,
                WheelSmoothTime,
                Mathf.Infinity,
                deltaTime);

            var changed = SetAxisNormalizedInternal(axis, nextNormalized, setVelocity: false);

            if (Mathf.Abs(axis.WheelScrollTargetNormalized - axis.NormalizedPosition) <= 0.0005f &&
                Mathf.Abs(axis.WheelScrollVelocity) <= 0.001f)
            {
                SetAxisNormalizedInternal(axis, axis.WheelScrollTargetNormalized, setVelocity: false);
                ResetWheelScroll(axis);
            }

            return changed || axis.WheelScrollActive;
        }

        bool ApplyNonScrollableAxisAlignment()
        {
            if (_content == null)
                return false;

            var nextPosition = _content.anchoredPosition;
            var changed = false;

            changed |= AlignNonScrollableAxisToDefaultEdgeAndResetWheel(_horizontal, ref nextPosition.x);
            changed |= AlignNonScrollableAxisToDefaultEdgeAndResetWheel(_vertical, ref nextPosition.y);

            if (!changed)
                return false;

            return SetContentAnchoredPosition(nextPosition);
        }

        bool AlignNonScrollableAxisToDefaultEdgeAndResetWheel(AxisState axis, ref float anchoredPosition)
        {
            if (!axis.Preset.Enabled)
                return false;

            if (axis.Scrollable)
            {
                ResetWheelScroll(axis);
                return false;
            }

            ResetWheelScroll(axis);

            var delta = axis.Kind == UIScrollRectAxisKind.Horizontal
                ? _viewBounds.min.x - _contentBounds.min.x
                : _viewBounds.max.y - _contentBounds.max.y;

            if (Mathf.Abs(delta) <= Epsilon)
                return false;

            anchoredPosition += delta;
            return true;
        }

        void ResetWheelScroll(AxisState axis)
        {
            axis.WheelScrollActive = false;
            axis.WheelScrollTargetNormalized = 0f;
            axis.WheelScrollVelocity = 0f;
        }

        bool TryBeginBarInteraction(AxisState axis, Vector2 screenPosition)
        {
            if (!axis.Preset.Enabled || axis.Binding == null || !axis.Scrollable)
                return false;

            var handleRect = axis.Binding.HandleRect;
            if (handleRect != null && RectTransformUtility.RectangleContainsScreenPoint(handleRect, screenPosition, ResolveRectCamera(handleRect)))
            {
                if (!TryTryResolvePointerNormalized(axis, screenPosition, 0f, out var currentPointerNormalized, out _))
                    return false;

                var handleCenterNormalized = ResolveHandleCenterNormalized(axis);
                axis.HandleDragPointerOffset = currentPointerNormalized - handleCenterNormalized;
                axis.HandleDragActive = true;
                axis.PageHoldActive = false;
                TryEnsureSelected();
                RefreshSelectionBlock();
                return true;
            }

            if (!TryTryResolvePointerNormalized(axis, screenPosition, 0f, out var pointerNormalized, out var pointerInside) || !pointerInside)
                return false;

            if (IsWithinHandle(axis, pointerNormalized))
                return false;

            axis.PageHoldActive = true;
            axis.PageHoldTargetNormalized = pointerNormalized;
            axis.NextPageRepeatTime = Time.unscaledTime + _preset.Interaction.PageRepeatDelay;
            TryEnsureSelected();
            RefreshSelectionBlock();
            StepPage(axis);
            return true;
        }

        void UpdateHandleDrag(AxisState axis, Vector2 screenPosition)
        {
            if (!axis.HandleDragActive)
                return;

            if (!TryTryResolvePointerNormalized(axis, screenPosition, axis.HandleDragPointerOffset, out var nextHandleCenterNormalized, out _))
                return;

            var nextContentNormalized = ResolveContentNormalizedFromHandleCenter(axis, nextHandleCenterNormalized);
            SetAxisNormalizedInternal(axis, nextContentNormalized, setVelocity: true);
            UpdateBoundsAndVisuals();
        }

        void UpdatePageHold(AxisState axis)
        {
            if (!axis.PageHoldActive)
                return;

            if (Time.unscaledTime < axis.NextPageRepeatTime)
                return;

            axis.NextPageRepeatTime = Time.unscaledTime + _preset.Interaction.PageRepeatInterval;
            StepPage(axis);
        }

        void StepPage(AxisState axis)
        {
            if (!axis.PageHoldActive || !axis.Scrollable)
                return;

            var handleSize = Mathf.Clamp01(axis.HandleSizeNormalized);
            var handleStart = axis.NormalizedPosition * (1f - handleSize);
            var handleEnd = handleStart + handleSize;
            if (axis.PageHoldTargetNormalized >= handleStart - Epsilon && axis.PageHoldTargetNormalized <= handleEnd + Epsilon)
                return;

            var step = Mathf.Max(Epsilon, _preset.Interaction.PageSize);
            var direction = axis.PageHoldTargetNormalized > handleEnd ? 1f : -1f;
            SetAxisNormalizedInternal(axis, axis.NormalizedPosition + direction * step, setVelocity: true);
            UpdateBoundsAndVisuals();
        }

        void BeginContentDrag(Vector2 currentLocal)
        {
            _contentDragActive = true;
            _swipeCandidate = new SwipeCandidateState(
                isActive: true,
                startScreenPosition: _swipeCandidate.StartScreenPosition,
                startViewportLocalPosition: _swipeCandidate.StartViewportLocalPosition,
                lastViewportLocalPosition: currentLocal);
            TryEnsureSelected();
            RefreshSelectionBlock();
        }

        void ApplyContentDragDelta(Vector2 deltaLocal)
        {
            if (_content == null)
                return;

            var delta = new Vector2(
                _horizontal.Scrollable ? deltaLocal.x : 0f,
                _vertical.Scrollable ? deltaLocal.y : 0f);
            if (delta.sqrMagnitude <= Epsilon * Epsilon)
                return;

            if (!_boundsValid)
                UpdateBoundsAndVisuals();

            if (_preset.Physics.MovementType == UIScrollRectMovementType.Elastic)
            {
                var projectedOffset = CalculateOffset(delta);
                var damping = _preset.Interaction.OverDragDamping;
                if (damping > Epsilon)
                {
                    if (_horizontal.Scrollable && Mathf.Abs(projectedOffset.x) > Epsilon)
                        delta.x *= GetElasticDragScale(projectedOffset.x, _horizontal.ViewSize, damping);
                    if (_vertical.Scrollable && Mathf.Abs(projectedOffset.y) > Epsilon)
                        delta.y *= GetElasticDragScale(projectedOffset.y, _vertical.ViewSize, damping);
                }
            }

            var currentPosition = _content.anchoredPosition;
            var nextPosition = currentPosition + delta;
            var offset = CalculateOffset(nextPosition - currentPosition);

            if (_preset.Physics.MovementType == UIScrollRectMovementType.Elastic)
            {
                if (_horizontal.Scrollable && Mathf.Abs(offset.x) > Epsilon)
                    nextPosition.x -= RubberDelta(offset.x, _horizontal.ViewSize);
                if (_vertical.Scrollable && Mathf.Abs(offset.y) > Epsilon)
                    nextPosition.y -= RubberDelta(offset.y, _vertical.ViewSize);
            }
            else
            {
                nextPosition += offset;
            }

            if (SetContentAnchoredPosition(nextPosition))
                UpdateBoundsAndVisuals();
        }

        static float GetElasticDragScale(float overscroll, float viewSize, float damping)
        {
            var baseScale = Mathf.Clamp01(1f - damping);
            if (baseScale <= Epsilon)
                return 0f;

            if (viewSize <= Epsilon)
                return baseScale;

            var overscrollRatio = Mathf.Clamp01(Mathf.Abs(overscroll) / viewSize);
            var farScale = baseScale * 0.25f;
            return Mathf.Lerp(baseScale, farScale, overscrollRatio);
        }

        bool SetAxisNormalizedInternal(AxisState axis, float normalizedValue, bool setVelocity)
        {
            if (_content == null || !axis.Scrollable || axis.HiddenLength <= Epsilon)
                return false;

            var clamped = Mathf.Clamp01(normalizedValue);
            var desiredOffset = clamped * axis.HiddenLength;
            var currentOffset = axis.NormalizedPosition * axis.HiddenLength;
            var positionDelta = currentOffset - desiredOffset;
            if (Mathf.Abs(positionDelta) <= Epsilon)
                return false;

            var nextPosition = _content.anchoredPosition;
            if (axis.Kind == UIScrollRectAxisKind.Horizontal)
                nextPosition.x += positionDelta;
            else
                nextPosition.y += positionDelta;

            var changed = SetContentAnchoredPosition(nextPosition);
            if (!changed)
                return false;

            if (setVelocity)
            {
                var deltaTime = Mathf.Max(ResolveDeltaTime(), 0.0001f);
                if (axis.Kind == UIScrollRectAxisKind.Horizontal)
                    _velocity.x = positionDelta / deltaTime;
                else
                    _velocity.y = positionDelta / deltaTime;
            }
            else
            {
                if (axis.Kind == UIScrollRectAxisKind.Horizontal)
                    _velocity.x = 0f;
                else
                    _velocity.y = 0f;
            }

            return true;
        }

        bool UpdatePhysics(float deltaTime)
        {
            if (_content == null || !_boundsValid)
                return false;

            var position = _content.anchoredPosition;
            var offset = CalculateOffset(Vector2.zero);
            var changed = false;

            if (_preset.Physics.MovementType == UIScrollRectMovementType.Elastic &&
                (Mathf.Abs(offset.x) > Epsilon || Mathf.Abs(offset.y) > Epsilon))
            {
                if (_horizontal.Scrollable || Mathf.Abs(offset.x) > Epsilon)
                {
                    position.x = Mathf.SmoothDamp(
                        _content.anchoredPosition.x,
                        _content.anchoredPosition.x + offset.x,
                        ref _velocity.x,
                        _preset.Physics.Elasticity,
                        Mathf.Infinity,
                        deltaTime);
                    changed = true;
                }

                if (_vertical.Scrollable || Mathf.Abs(offset.y) > Epsilon)
                {
                    position.y = Mathf.SmoothDamp(
                        _content.anchoredPosition.y,
                        _content.anchoredPosition.y + offset.y,
                        ref _velocity.y,
                        _preset.Physics.Elasticity,
                        Mathf.Infinity,
                        deltaTime);
                    changed = true;
                }
            }
            else if (_preset.Physics.Inertia)
            {
                if (_horizontal.Scrollable && Mathf.Abs(_velocity.x) > Epsilon)
                {
                    _velocity.x *= Mathf.Pow(_preset.Physics.DecelerationRate, deltaTime);
                    if (Mathf.Abs(_velocity.x) < 1f)
                        _velocity.x = 0f;
                    position.x += _velocity.x * deltaTime;
                    changed = true;
                }

                if (_vertical.Scrollable && Mathf.Abs(_velocity.y) > Epsilon)
                {
                    _velocity.y *= Mathf.Pow(_preset.Physics.DecelerationRate, deltaTime);
                    if (Mathf.Abs(_velocity.y) < 1f)
                        _velocity.y = 0f;
                    position.y += _velocity.y * deltaTime;
                    changed = true;
                }

                if (changed && _preset.Physics.MovementType == UIScrollRectMovementType.Clamped)
                    position += CalculateOffset(position - _content.anchoredPosition);
            }
            else
            {
                _velocity = Vector2.zero;
            }

            return changed && SetContentAnchoredPosition(position);
        }

        void UpdateBoundsAndVisuals()
        {
            if (_content == null || _viewport == null)
                return;

            _viewBounds = new Bounds(_viewport.rect.center, _viewport.rect.size);
            _contentBounds = CalculateContentBounds();
            _boundsValid = true;

            UpdateAxisMetrics(_horizontal);
            UpdateAxisMetrics(_vertical);
            ApplyBarVisual(_horizontal);
            ApplyBarVisual(_vertical);

            _snapshot = new UIScrollRectSnapshot(
                new Vector2(_horizontal.NormalizedPosition, _vertical.NormalizedPosition),
                _velocity,
                new Vector2(_horizontal.ViewSize, _vertical.ViewSize),
                new Vector2(_horizontal.ContentSize, _vertical.ContentSize),
                _horizontal.Visible,
                _vertical.Visible);
        }

        Bounds CalculateContentBounds()
        {
            if (_content == null || _viewport == null)
                return default;

            var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            var hasBounds = false;

            CollectChildBounds(_content, ref hasBounds, ref min, ref max);
            if (!hasBounds)
                AccumulateRectBounds(_content, ref hasBounds, ref min, ref max);

            if (!hasBounds)
                return new Bounds(_viewBounds.center, Vector3.zero);

            return new Bounds((min + max) * 0.5f, max - min);
        }

        void CollectChildBounds(Transform root, ref bool hasBounds, ref Vector3 min, ref Vector3 max)
        {
            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child == null || !child.gameObject.activeInHierarchy)
                    continue;

                if (child is RectTransform rect)
                    AccumulateRectBounds(rect, ref hasBounds, ref min, ref max);

                CollectChildBounds(child, ref hasBounds, ref min, ref max);
            }
        }

        void AccumulateRectBounds(RectTransform rect, ref bool hasBounds, ref Vector3 min, ref Vector3 max)
        {
            if (_viewport == null)
                return;

            rect.GetWorldCorners(_rectCorners);
            for (var i = 0; i < _rectCorners.Length; i++)
            {
                var local = _viewport.InverseTransformPoint(_rectCorners[i]);
                if (!hasBounds)
                {
                    min = local;
                    max = local;
                    hasBounds = true;
                }
                else
                {
                    min = Vector3.Min(min, local);
                    max = Vector3.Max(max, local);
                }
            }
        }

        void UpdateAxisMetrics(AxisState axis)
        {
            var enabled = axis.Preset.Enabled;
            axis.ViewSize = ResolveBoundsSize(_viewBounds, axis.Kind);
            axis.ContentSize = ResolveBoundsSize(_contentBounds, axis.Kind);
            axis.HiddenLength = Mathf.Max(0f, axis.ContentSize - axis.ViewSize);
            axis.Scrollable = enabled && axis.HiddenLength > Epsilon;
            axis.HandleSizeNormalized = axis.ContentSize <= Epsilon
                ? 1f
                : Mathf.Clamp01(axis.ViewSize / axis.ContentSize);

            if (axis.Scrollable)
            {
                var currentOffset = Mathf.Clamp(
                    ResolveBoundsMin(_viewBounds, axis.Kind) - ResolveBoundsMin(_contentBounds, axis.Kind),
                    0f,
                    axis.HiddenLength);
                axis.NormalizedPosition = Mathf.Clamp01(currentOffset / axis.HiddenLength);
            }
            else
            {
                axis.NormalizedPosition = axis.Kind == UIScrollRectAxisKind.Vertical ? 1f : 0f;
            }

            axis.Visible = enabled && (!axis.Preset.AutoHide || axis.Scrollable);
        }

        void ApplyBarVisual(AxisState axis)
        {
            var binding = axis.Binding;
            if (binding == null)
                return;

            if (binding.VisibilityRoot != null)
            {
                var bindingScopeGo = ResolveScopeGameObject(axis.BindingScope);
                if (!ReferenceEquals(binding.VisibilityRoot, bindingScopeGo) &&
                    binding.VisibilityRoot.activeSelf != axis.Visible)
                {
                    binding.VisibilityRoot.SetActive(axis.Visible);
                }
            }

            if (!axis.Visible || binding.HandleRect == null)
                return;

            var handleRect = binding.HandleRect;
            var start = ResolveHandleStartNormalized(axis);
            var end = Mathf.Clamp01(start + Mathf.Clamp01(axis.HandleSizeNormalized));

            var anchorMin = handleRect.anchorMin;
            var anchorMax = handleRect.anchorMax;
            var sizeDelta = handleRect.sizeDelta;
            if (axis.Kind == UIScrollRectAxisKind.Horizontal)
            {
                anchorMin.x = start;
                anchorMax.x = end;
                sizeDelta.x = 0f;
            }
            else
            {
                anchorMin.y = start;
                anchorMax.y = end;
                sizeDelta.y = 0f;
            }

            handleRect.anchorMin = anchorMin;
            handleRect.anchorMax = anchorMax;
            handleRect.sizeDelta = sizeDelta;
        }

        bool CanHandleWheelScroll(Vector2 pointerPosition)
        {
            if (IsPointerInsideViewport(pointerPosition))
                return true;

            var hovered = _selectionState?.HoveredElement;
            if (hovered != null && IsScopeSelfOrDescendant(hovered))
                return true;

            var current = _selectionState?.CurrentElement;
            return current != null && IsScopeSelfOrDescendant(current);
        }

        bool IsPointerInsideViewport(Vector2 screenPosition)
        {
            if (_viewport == null)
                return false;

            return RectTransformUtility.RectangleContainsScreenPoint(
                _viewport,
                screenPosition,
                ResolveRectCamera(_viewport));
        }

        bool CanStartNewPointerInteraction()
        {
            if (_selectionBlockHandle != null)
                return true;

            return _selectionBlockService == null || !_selectionBlockService.IsPointerBlocked;
        }

        bool HasActiveInteraction()
        {
            return _contentDragActive ||
                   _horizontal.HandleDragActive ||
                   _horizontal.PageHoldActive ||
                   _vertical.HandleDragActive ||
                   _vertical.PageHoldActive;
        }

        void RefreshSelectionBlock()
        {
            if (_selectionBlockService == null)
                return;

            if (HasActiveInteraction())
            {
                _selectionBlockHandle ??= _selectionBlockService.AcquireBlock(this, UISelectionBlockMask.All);
                return;
            }

            ReleaseSelectionBlock();
        }

        void ReleaseSelectionBlock()
        {
            _selectionBlockHandle?.Dispose();
            _selectionBlockHandle = null;
        }

        void EndPointerInteractions(bool preserveVelocity)
        {
            _swipeCandidate = default;
            _contentDragActive = false;
            _horizontal.ResetInteraction();
            _vertical.ResetInteraction();
            if (!preserveVelocity)
                _velocity = Vector2.zero;
            RefreshSelectionBlock();
        }

        void CancelAllInteractions()
        {
            EndPointerInteractions(preserveVelocity: false);
        }

        void ResetTransientState()
        {
            _velocity = Vector2.zero;
            _swipeCandidate = default;
            _contentDragActive = false;
            _boundsValid = false;
            _snapshot = default;
            _horizontal.ResetInteraction();
            _vertical.ResetInteraction();
            _hasLastContentPosition = false;
        }

        void CancelBindTask()
        {
            _bindCts?.Cancel();
            _bindCts?.Dispose();
            _bindCts = null;
        }

        void ApplyInitialAlignmentAndClamp()
        {
            if (_content == null || _viewport == null)
                return;

            if (!_boundsValid)
                UpdateBoundsAndVisuals();

            var nextPosition = _content.anchoredPosition;
            var changed = false;
            changed |= AlignNonScrollableAxisToDefaultEdge(_horizontal, ref nextPosition.x);
            changed |= AlignNonScrollableAxisToDefaultEdge(_vertical, ref nextPosition.y);

            if (changed && SetContentAnchoredPosition(nextPosition))
                UpdateBoundsAndVisuals();

            var clampOffset = CalculateOffset(Vector2.zero);
            if (Mathf.Abs(clampOffset.x) <= Epsilon && Mathf.Abs(clampOffset.y) <= Epsilon)
                return;

            if (SetContentAnchoredPosition(_content.anchoredPosition + clampOffset))
                UpdateBoundsAndVisuals();
        }

        bool AlignNonScrollableAxisToDefaultEdge(AxisState axis, ref float anchoredPosition)
        {
            if (!axis.Preset.Enabled || axis.Scrollable)
                return false;

            var delta = axis.Kind == UIScrollRectAxisKind.Horizontal
                ? _viewBounds.min.x - _contentBounds.min.x
                : _viewBounds.max.y - _contentBounds.max.y;

            if (Mathf.Abs(delta) <= Epsilon)
                return false;

            anchoredPosition += delta;
            return true;
        }

        bool SetContentAnchoredPosition(Vector2 position)
        {
            if (_content == null)
                return false;

            if ((_content.anchoredPosition - position).sqrMagnitude <= Epsilon * Epsilon)
                return false;

            _content.anchoredPosition = position;
            return true;
        }

        Vector2 CalculateOffset(Vector2 delta)
        {
            if (!_boundsValid)
                return Vector2.zero;

            var offset = Vector2.zero;
            if (_horizontal.Scrollable)
            {
                var min = _contentBounds.min.x + delta.x;
                var max = _contentBounds.max.x + delta.x;
                if (min > _viewBounds.min.x)
                    offset.x = _viewBounds.min.x - min;
                else if (max < _viewBounds.max.x)
                    offset.x = _viewBounds.max.x - max;
            }

            if (_vertical.Scrollable)
            {
                var min = _contentBounds.min.y + delta.y;
                var max = _contentBounds.max.y + delta.y;
                if (min > _viewBounds.min.y)
                    offset.y = _viewBounds.min.y - min;
                else if (max < _viewBounds.max.y)
                    offset.y = _viewBounds.max.y - max;
            }

            return offset;
        }

        bool TryGetViewportLocal(Vector2 screenPosition, out Vector2 localPosition, out bool inside)
        {
            localPosition = default;
            inside = false;
            if (_viewport == null)
                return false;

            var camera = ResolveRectCamera(_viewport);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, screenPosition, camera, out localPosition))
                return false;

            inside = _viewport.rect.Contains(localPosition);
            return true;
        }

        bool TryTryResolvePointerNormalized(
            AxisState axis,
            Vector2 screenPosition,
            float pointerOffset,
            out float normalizedPosition,
            out bool pointerInside)
        {
            normalizedPosition = 0f;
            pointerInside = false;
            if (!TryResolveRangePointerSnapshot(axis, screenPosition, out var snapshot))
                return false;

            pointerInside = snapshot.PointerInside;
            var local = snapshot.LocalPosition;
            if (axis.Kind == UIScrollRectAxisKind.Horizontal)
                local.x -= pointerOffset;
            else
                local.y -= pointerOffset;

            return SliderRuntimeHelpers.TryMapCanvasLocalToNormalized(
                snapshot.Range.LocalRect,
                axis.Kind == UIScrollRectAxisKind.Horizontal ? SliderAreaFillAxis.SizeX : SliderAreaFillAxis.SizeY,
                SliderAreaOriginSide.Min,
                0f,
                0f,
                local,
                out normalizedPosition);
        }

        bool TryResolveRangePointerSnapshot(
            AxisState axis,
            Vector2 screenPosition,
            out ScreenRangePointerSnapshot snapshot)
        {
            snapshot = default;
            if (!TryResolveRangeSnapshot(axis, out var rangeSnapshot))
                return false;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rangeSnapshot.CanvasRect,
                    screenPosition,
                    rangeSnapshot.UICamera,
                    out var localPosition))
            {
                return false;
            }

            snapshot = new ScreenRangePointerSnapshot(rangeSnapshot, localPosition, rangeSnapshot.LocalRect.Contains(localPosition));
            return true;
        }

        bool TryResolveRangeSnapshot(AxisState axis, out SliderScreenRangeSnapshot snapshot)
        {
            snapshot = default;
            switch (axis.Preset.RangeSourceMode)
            {
                case UIScrollBarRangeSourceMode.AreaChannel:
                    return TryResolveAreaRangeSnapshot(axis, out snapshot);
                case UIScrollBarRangeSourceMode.RectTransform:
                    return TryResolveRectRangeSnapshot(axis.Preset.RangeRectTransform, out snapshot);
                case UIScrollBarRangeSourceMode.TrackRect:
                default:
                    return TryResolveRectRangeSnapshot(axis.Binding?.TrackRect, out snapshot);
            }
        }

        bool TryResolveAreaRangeSnapshot(AxisState axis, out SliderScreenRangeSnapshot snapshot)
        {
            snapshot = default;
            if (_canvas == null || _activeScope == null)
                return false;

            var areaScope = ActorSourceFastResolver.ResolveCached(
                _activeScope,
                axis.Preset.AreaActorSource,
                ref axis.AreaSourceCache);
            if (areaScope?.Resolver == null)
                return false;

            if (!areaScope.Resolver.TryResolve<IAreaChannelHubService>(out var areaHub) || areaHub == null)
                return false;

            if (!areaHub.TryGetCanvasRectSnapshot(axis.Preset.AreaChannelTag, _canvas, out var areaSnapshot))
                return false;

            snapshot = new SliderScreenRangeSnapshot(areaSnapshot.CanvasRect, areaSnapshot.LocalRect, areaSnapshot.UICamera);
            return true;
        }

        bool TryResolveRectRangeSnapshot(RectTransform? rect, out SliderScreenRangeSnapshot snapshot)
        {
            snapshot = default;
            if (rect == null)
                return false;

            if (_canvasRect != null && !ReferenceEquals(_canvasRect, rect))
            {
                if (SliderRuntimeHelpers.TryBuildCanvasRectSnapshot(rect, _canvasRect, _uiCamera, out snapshot))
                    return true;
            }

            snapshot = new SliderScreenRangeSnapshot(rect, rect.rect, ResolveRectCamera(rect));
            return true;
        }

        bool IsScopeSelfOrDescendant(IScopeNode scope)
        {
            for (var current = scope; current != null; current = current.Parent)
            {
                if (ReferenceEquals(current, _owner))
                    return true;
            }

            return false;
        }

        static GameObject? ResolveScopeGameObject(IScopeNode? scope)
        {
            if (scope is Component component)
                return component.gameObject;

            return scope?.Identity?.SelfTransform != null
                ? scope.Identity.SelfTransform.gameObject
                : null;
        }

        void TryEnsureSelected()
        {
            _selectionNavigation?.TrySelect(_owner);
        }

        float ResolveHandleStartNormalized(AxisState axis)
        {
            var size = Mathf.Clamp01(axis.HandleSizeNormalized);
            return axis.Scrollable
                ? Mathf.Clamp01(axis.NormalizedPosition * Mathf.Max(0f, 1f - size))
                : axis.Kind == UIScrollRectAxisKind.Vertical ? 1f - size : 0f;
        }

        float ResolveHandleCenterNormalized(AxisState axis)
        {
            var size = Mathf.Clamp01(axis.HandleSizeNormalized);
            return ResolveHandleStartNormalized(axis) + size * 0.5f;
        }

        float ResolveContentNormalizedFromHandleCenter(AxisState axis, float handleCenterNormalized)
        {
            var size = Mathf.Clamp01(axis.HandleSizeNormalized);
            var denominator = Mathf.Max(Epsilon, 1f - size);
            var handleStart = handleCenterNormalized - size * 0.5f;
            return Mathf.Clamp01(handleStart / denominator);
        }

        bool IsWithinHandle(AxisState axis, float pointerNormalized)
        {
            var size = Mathf.Clamp01(axis.HandleSizeNormalized);
            var start = ResolveHandleStartNormalized(axis);
            var end = start + size;
            return pointerNormalized >= start - Epsilon && pointerNormalized <= end + Epsilon;
        }

        float ResolveDeltaTime()
        {
            return _timeScaleBehavior == TimeScaleBehavior.Unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
        }

        static float ResolveBoundsSize(Bounds bounds, UIScrollRectAxisKind axis)
        {
            return axis == UIScrollRectAxisKind.Horizontal ? bounds.size.x : bounds.size.y;
        }

        static float ResolveBoundsMin(Bounds bounds, UIScrollRectAxisKind axis)
        {
            return axis == UIScrollRectAxisKind.Horizontal ? bounds.min.x : bounds.min.y;
        }

        static Camera? ResolveRectCamera(RectTransform rect)
        {
            var canvas = rect.GetComponentInParent<Canvas>(true);
            if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return null;

            return canvas.worldCamera;
        }

        static float RubberDelta(float overStretching, float viewSize)
        {
            if (viewSize <= Epsilon)
                return 0f;

            return (1f - (1f / ((Mathf.Abs(overStretching) * 0.55f / viewSize) + 1f))) * viewSize * Mathf.Sign(overStretching);
        }

        static string DescribeTransform(Transform? target)
        {
            if (target == null)
                return "<null>";

            return $"{target.name} path='{BuildPath(target)}'";
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

        static string DescribeScope(IScopeNode? scope)
        {
            if (scope == null)
                return "<null>";

            var id = scope.Identity?.Id;
            var identityText = string.IsNullOrWhiteSpace(id) ? "<none>" : id;
            var buildStatus = scope is BaseLifetimeScope baseScope ? baseScope.DebugBuildStatus : null;
            var buildText = string.IsNullOrWhiteSpace(buildStatus) ? string.Empty : $" build='{buildStatus}'";
            return $"{scope.Kind} id='{identityText}' transform='{DescribeTransform(scope.Identity?.SelfTransform)}'{buildText}";
        }

        static UIScrollRectPreset ResolvePreset(DynamicValue<UIScrollRectPreset> value, IDynamicContext context)
        {
            if (value.TryGet(context, out UIScrollRectPreset? preset) && preset != null)
                return preset.CreateRuntimeCopy();

            return new UIScrollRectPreset();
        }

        static IVarStore ResolveVars(IScopeNode scope)
        {
            if (scope.Resolver != null &&
                scope.Resolver.TryResolve<IVarStore>(out var vars) &&
                vars != null)
            {
                return vars;
            }

            return NullVarStore.Instance;
        }

        UIScrollRectAxisPreset ResolveAxisPreset(UIScrollRectAxisKind axisKind)
        {
            return axisKind == UIScrollRectAxisKind.Horizontal
                ? _preset.Horizontal.CreateRuntimeCopy()
                : _preset.Vertical.CreateRuntimeCopy();
        }
    }
}
