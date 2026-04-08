#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Commands.VNext;
using Game.Common;
using Game.DI;
using Game.SelectRuntime;
using Game.Spawn;
using UnityEngine;
using VContainer;

namespace Game.UI
{
    internal enum TooltipAutoTriggerKind
    {
        None = 0,
        Pointer = 10,
        Navigation = 20,
        PointerNavigation = 30,
    }

    internal sealed class TooltipChannelPlayerRuntime :
        ITooltipChannelPlayer,
        ITooltipChannelCommandService,
        ITooltipChannelControlService
    {
        const float UiHiddenLocalPosition = 100000f;
        const float WorldHiddenPosition = 100000f;

        readonly TooltipChannelHubService _hub;
        readonly IScopeNode _owner;
        readonly string _tag;
        readonly int _order;
        readonly TooltipChannelOptions _options;
        readonly Vector3[] _rectCorners8 = new Vector3[8];
        ActorSourceResolveCache _anchorActorCache;
        readonly List<ResolvedHitTestTarget> _resolvedHitTestTargets = new();
        TooltipHitTestPreset? _resolvedHitTestPresetSource;

        readonly struct ResolvedHitTestTarget
        {
            public readonly RectTransform? RectTransform;
            public readonly SpriteRenderer? SpriteRenderer;
            public readonly WorldPointerTargetMB? WorldPointerTarget;

            public ResolvedHitTestTarget(
                RectTransform? rectTransform,
                SpriteRenderer? spriteRenderer,
                WorldPointerTargetMB? worldPointerTarget)
            {
                RectTransform = rectTransform;
                SpriteRenderer = spriteRenderer;
                WorldPointerTarget = worldPointerTarget;
            }
        }

        TooltipPlayerPreset _basePlayerPreset = new();
        TooltipPlayerPreset _currentPlayerPreset = new();
        TooltipCommandsPreset _baseCommandsPreset = new();
        TooltipCommandsPreset _currentCommandsPreset = new();
        TooltipHitTestPreset _baseHitTestPreset = new();
        TooltipHitTestPreset _currentHitTestPreset = new();
        DynamicValue<BaseRuntimeTemplatePreset> _effectiveRuntimeTemplatePresetValue;
        string _effectiveSpawnerTag = string.Empty;
        bool _effectiveEnablePointerHover = true;
        bool _effectiveEnableSelectionHover = true;
        float _effectiveHoverDelaySeconds = 0.4f;
        float _effectiveSelectionDelaySeconds = 0.3f;
        float _effectivePointerMoveThreshold = 2f;
        ActorSource _effectiveAnchorActorSource = new() { Kind = ActorSourceKind.Current };
        TooltipChannelSpawnMode _effectiveSpawnMode = TooltipChannelSpawnMode.FollowPointer;
        Vector3 _effectiveFollowPointerDirectionOffset = Vector3.zero;
        Vector2 _effectiveFollowPointerMoveScale = Vector2.one;
        Vector3 _effectiveFixedDirectionOffset = Vector3.zero;
        TooltipChannelAnchorX _effectiveAnchorX = TooltipChannelAnchorX.Right;
        TooltipChannelAnchorY _effectiveAnchorY = TooltipChannelAnchorY.Up;

        IScopeNode? _activeScope;
        IDynamicContext? _dynamicContext;
        CancellationTokenSource? _lifetimeCts;
        CancellationTokenSource? _operationCts;
        int _operationVersion;

        IScopeNode? _spawnedScope;
        Transform? _activeTransform;
        RectTransform? _activeRectTransform;
        IVisualBoundsService? _boundsService;
        IVisualBoundsOutput? _boundsOutput;

        TooltipChannelOverrideMode _overrideMode;
        TooltipChannelVisibilityState _visibilityState = TooltipChannelVisibilityState.Hidden;
        TooltipAutoTriggerKind _candidateTrigger = TooltipAutoTriggerKind.None;
        bool _conditionEnabled;
        bool _autoTriggered;
        bool _isVisibleRequested;
        float _candidateStartTime;
        Vector2 _candidateStartPointer;
        Vector2 _lastPointerPos;
        bool _hasLastPointerPos;
        string _lastPointerOverDebugState = string.Empty;
        TooltipAutoTriggerKind _lastLoggedTrigger = TooltipAutoTriggerKind.None;
        bool _hasLoggedTrigger;
        bool _lastLoggedVisibleRequested;
        bool _hasLoggedVisibleRequested;

        Vector2 _followPointerStartUi;
        Vector2 _followPointerBaseUi;
        bool _hasFollowPointerUi;
        Vector3 _followPointerStartWorld;
        Vector3 _followPointerBaseWorld;
        bool _hasFollowPointerWorld;
        float _spawnBaseUiLocalZ;
        float _spawnBaseWorldZ;
        Rect _cachedUiLocalRect;
        Vector2 _cachedUiScreenSize;
        Vector3 _cachedUiWorldPosition;
        Quaternion _cachedUiWorldRotation;
        Vector3 _cachedUiLossyScale;
        Camera? _cachedUiCamera;
        int _cachedUiScreenWidth;
        int _cachedUiScreenHeight;
        bool _hasCachedUiScreenSize;
        Bounds _cachedWorldBounds;
        Vector2 _cachedWorldScreenSize;
        Vector3 _cachedWorldPosition;
        Quaternion _cachedWorldRotation;
        Vector3 _cachedWorldLossyScale;
        Camera? _cachedWorldCamera;
        int _cachedWorldScreenWidth;
        int _cachedWorldScreenHeight;
        bool _hasCachedWorldScreenSize;

        public TooltipChannelPlayerRuntime(
            TooltipChannelHubService hub,
            IScopeNode owner,
            string tag,
            int order,
            TooltipChannelOptions options)
        {
            _hub = hub ?? throw new ArgumentNullException(nameof(hub));
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _tag = string.IsNullOrWhiteSpace(tag) ? "default" : tag.Trim();
            _order = order;
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public string Tag => _tag;
        public bool IsConditionEnabled => _conditionEnabled;
        public bool IsAutoTriggered => _autoTriggered;
        public bool IsVisibleRequested => _isVisibleRequested;
        public bool IsSpawned => _spawnedScope != null;
        public int Priority => _currentPlayerPreset.Priority;
        public TooltipChannelOverrideMode OverrideMode => _overrideMode;
        public TooltipChannelVisibilityState VisibilityState => _visibilityState;

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _ = isReset;

            _activeScope = scope;
            _dynamicContext = TooltipChannelHubService.CreateDynamicContext(scope);
            _lifetimeCts = new CancellationTokenSource();
            _overrideMode = TooltipChannelOverrideMode.None;
            _visibilityState = TooltipChannelVisibilityState.Hidden;
            _candidateTrigger = TooltipAutoTriggerKind.None;
            _conditionEnabled = false;
            _autoTriggered = false;
            _isVisibleRequested = false;
            _candidateStartTime = 0f;
            _candidateStartPointer = Vector2.zero;
            _lastPointerPos = Vector2.zero;
            _hasLastPointerPos = false;
            _hasFollowPointerUi = false;
            _hasFollowPointerWorld = false;
            _spawnBaseUiLocalZ = 0f;
            _spawnBaseWorldZ = 0f;
            _anchorActorCache = default;
            _resolvedHitTestPresetSource = null;
            _resolvedHitTestTargets.Clear();
            ResetPlacementCaches();
            _lastPointerOverDebugState = string.Empty;
            _lastLoggedTrigger = TooltipAutoTriggerKind.None;
            _hasLoggedTrigger = false;
            _lastLoggedVisibleRequested = false;
            _hasLoggedVisibleRequested = false;

            ResolveSourcePresets();
            LogDebug($"Acquire. TriggerSpace={_hub.CurrentTriggerSpaceKind} RenderSpace={_hub.CurrentRenderSpaceKind} PointerHover={_effectiveEnablePointerHover} SelectionHover={_effectiveEnableSelectionHover}");
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;

            CancelOperation();
            _lifetimeCts?.Cancel();
            _lifetimeCts?.Dispose();
            _lifetimeCts = null;

            var spawnedScope = _spawnedScope;
            _spawnedScope = null;
            _activeTransform = null;
            _activeRectTransform = null;
            _boundsService = null;
            _boundsOutput = null;
            if (spawnedScope != null)
                TooltipChannelHubService.ReleaseSpawnedRuntime(spawnedScope);

            _dynamicContext = null;
            _activeScope = null;
            _basePlayerPreset = new TooltipPlayerPreset();
            _currentPlayerPreset = new TooltipPlayerPreset();
            _baseCommandsPreset = new TooltipCommandsPreset();
            _currentCommandsPreset = new TooltipCommandsPreset();
            _baseHitTestPreset = new TooltipHitTestPreset();
            _currentHitTestPreset = new TooltipHitTestPreset();
            ResetEffectiveSettings();
            _overrideMode = TooltipChannelOverrideMode.None;
            _visibilityState = TooltipChannelVisibilityState.Hidden;
            _candidateTrigger = TooltipAutoTriggerKind.None;
            _conditionEnabled = false;
            _autoTriggered = false;
            _isVisibleRequested = false;
            _hasFollowPointerUi = false;
            _hasFollowPointerWorld = false;
            _spawnBaseUiLocalZ = 0f;
            _spawnBaseWorldZ = 0f;
            _hasLastPointerPos = false;
            _anchorActorCache = default;
            _resolvedHitTestPresetSource = null;
            _resolvedHitTestTargets.Clear();
            ResetPlacementCaches();
        }

        public void Tick(TooltipChannelInputMode inputMode, Vector2 pointerScreen, Camera? camera)
        {
            if (_dynamicContext == null)
                return;

            _conditionEnabled = _currentPlayerPreset.Condition.GetOrDefault(_dynamicContext, true);
            _autoTriggered = EvaluateAutoTrigger(inputMode, pointerScreen, camera);
            if (!_hasLoggedTrigger || _lastLoggedTrigger != _candidateTrigger)
            {
                _lastLoggedTrigger = _candidateTrigger;
                _hasLoggedTrigger = true;
                LogDebug($"CandidateTrigger={_candidateTrigger} AutoTriggered={_autoTriggered} InputMode={inputMode}");
            }

            _isVisibleRequested = ResolveDesiredVisibility();
            if (!_hasLoggedVisibleRequested || _lastLoggedVisibleRequested != _isVisibleRequested)
            {
                _lastLoggedVisibleRequested = _isVisibleRequested;
                _hasLoggedVisibleRequested = true;
                LogDebug($"VisibleRequested={_isVisibleRequested} Condition={_conditionEnabled} AutoTriggered={_autoTriggered} Override={_overrideMode} State={_visibilityState}");
            }
            if (_isVisibleRequested)
            {
                if (_visibilityState == TooltipChannelVisibilityState.Hidden)
                    BeginSpawn(pointerScreen, camera);
            }
            else if (_visibilityState == TooltipChannelVisibilityState.Active || _visibilityState == TooltipChannelVisibilityState.Spawning)
            {
                BeginClose(runHideCommands: true);
            }
        }

        public bool ForceShow()
        {
            _overrideMode = TooltipChannelOverrideMode.ForceShow;
            return true;
        }

        public bool ForceHide()
        {
            _overrideMode = TooltipChannelOverrideMode.ForceHide;
            if (_visibilityState == TooltipChannelVisibilityState.Active || _visibilityState == TooltipChannelVisibilityState.Spawning)
                BeginClose(runHideCommands: true);
            return true;
        }

        public bool ClearForceOverride()
        {
            if (_overrideMode == TooltipChannelOverrideMode.None)
                return false;

            _overrideMode = TooltipChannelOverrideMode.None;
            return true;
        }

        public bool SwapPlayerPreset(TooltipPlayerPreset? preset)
        {
            if (preset == null)
                return false;

            _basePlayerPreset = preset.CreateRuntimeCopy();
            _currentPlayerPreset = _basePlayerPreset.CreateRuntimeCopy();
            ResolveDependentPresetsFromPlayer();
            ResolveEffectiveSettings();
            RequestRespawnForConfigurationChange();
            return true;
        }

        public bool SwapCommandsPreset(TooltipCommandsPreset? preset)
        {
            if (preset == null)
                return false;

            _baseCommandsPreset = preset.CreateRuntimeCopy();
            _currentCommandsPreset = _baseCommandsPreset.CreateRuntimeCopy();
            return true;
        }

        public bool ResetRuntimeOverrides(bool resetPlayer, bool resetCommands, bool resetForceOverride)
        {
            if (!resetPlayer && !resetCommands && !resetForceOverride)
                return false;

            if (resetPlayer)
            {
                _currentPlayerPreset = _basePlayerPreset.CreateRuntimeCopy();
                _currentHitTestPreset = _baseHitTestPreset.CreateRuntimeCopy();
                _currentCommandsPreset = _baseCommandsPreset.CreateRuntimeCopy();
                ResolveEffectiveSettings();
                RequestRespawnForConfigurationChange();
            }

            if (resetCommands && !resetPlayer)
                _currentCommandsPreset = _baseCommandsPreset.CreateRuntimeCopy();

            if (resetForceOverride)
                _overrideMode = TooltipChannelOverrideMode.None;

            return true;
        }

        internal bool TryBuildUiPlacementRequest(Vector2 pointerScreen, Camera? camera, out TooltipPlacementRequest request)
        {
            request = default;
            if (_visibilityState != TooltipChannelVisibilityState.Active || _activeRectTransform == null || _hub.UiRoot == null)
                return false;

            if (!TryResolveUiScreenSize(camera, out var localRect, out var screenSize))
                return false;

            var baseLocal = ResolveUiBaseLocal(pointerScreen, camera);
            var baseWorld = _hub.UiRoot.TransformPoint(new Vector3(baseLocal.x, baseLocal.y, _spawnBaseUiLocalZ));
            request = new TooltipPlacementRequest(
                this,
                Priority,
                _order,
                baseWorld,
                ResolveActiveDirectionOffset(),
                _hub.UiRoot.TransformVector(Vector3.right),
                _hub.UiRoot.TransformVector(Vector3.up),
                _hub.UiRoot.TransformVector(Vector3.forward),
                screenSize,
                _effectiveAnchorX,
                _effectiveAnchorY);
            return true;
        }

        internal bool TryBuildWorldPlacementRequest(Vector2 pointerScreen, Camera? camera, out TooltipPlacementRequest request)
        {
            request = default;
            if (_visibilityState != TooltipChannelVisibilityState.Active || _activeTransform == null || camera == null)
                return false;

            if (!TryResolveWorldScreenSize(camera, out var screenSize))
                return false;

            var baseWorld = ResolveWorldBase(pointerScreen, camera);
            request = new TooltipPlacementRequest(
                this,
                Priority,
                _order,
                baseWorld,
                ResolveActiveDirectionOffset(),
                Vector3.right,
                Vector3.up,
                Vector3.forward,
                screenSize,
                _effectiveAnchorX,
                _effectiveAnchorY);
            return true;
        }

        internal void ApplyUiPlacement(in TooltipPlacementSolution solution, Camera? camera)
        {
            if (_activeRectTransform == null || _hub.UiRoot == null)
                return;

            var localRect = ResolveLocalRect();
            var localAnchor = ResolveLocalAnchorPoint(localRect, solution.AnchorX, solution.AnchorY);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_hub.UiRoot, solution.AnchorScreenPosition, camera, out var localPoint))
                return;

            _activeRectTransform.localPosition = new Vector3(
                localPoint.x - localAnchor.x,
                localPoint.y - localAnchor.y,
                _spawnBaseUiLocalZ + ResolveActiveDirectionOffset().z);
        }

        internal void ApplyWorldPlacement(in TooltipPlacementSolution solution, Camera? camera)
        {
            if (_activeTransform == null || camera == null)
                return;

            var rootPos = _activeTransform.position;
            var depth = camera.WorldToScreenPoint(rootPos).z;
            var anchorWorld = camera.ScreenToWorldPoint(new Vector3(solution.AnchorScreenPosition.x, solution.AnchorScreenPosition.y, depth));
            anchorWorld.z = rootPos.z;

            var bounds = ResolveWorldBounds(rootPos);
            var currentAnchor = ResolveWorldAnchorPoint(bounds, solution.AnchorX, solution.AnchorY, rootPos);
            var offset = currentAnchor - rootPos;

            var target = anchorWorld - offset;
            target.z = _spawnBaseWorldZ + ResolveActiveDirectionOffset().z;
            _activeTransform.position = target;
        }

        internal void MoveUiOffscreen()
        {
            if (_activeRectTransform == null)
                return;

            var local = _activeRectTransform.localPosition;
            _activeRectTransform.localPosition = new Vector3(UiHiddenLocalPosition, UiHiddenLocalPosition, local.z);
        }

        internal void MoveWorldOffscreen()
        {
            if (_activeTransform == null)
                return;

            var pos = _activeTransform.position;
            _activeTransform.position = new Vector3(WorldHiddenPosition, WorldHiddenPosition, pos.z);
        }

        bool EvaluateAutoTrigger(TooltipChannelInputMode inputMode, Vector2 pointerScreen, Camera? camera)
        {
            if (!_conditionEnabled)
            {
                ResetCandidate();
                return false;
            }

            // While already visible, keep the tooltip alive as long as the original
            // trigger condition is still satisfied. PointerMoveThreshold is used only
            // to reset the initial hover delay before spawn, not to force-close an
            // already visible tooltip while the pointer remains over the target.
            if (_visibilityState == TooltipChannelVisibilityState.Active && _autoTriggered)
            {
                switch (inputMode)
                {
                    case TooltipChannelInputMode.Pointer:
                        {
                            if (_effectiveEnablePointerHover && IsPointerOver(pointerScreen, camera))
                                return true;
                            break;
                        }
                    case TooltipChannelInputMode.PointerNavigation:
                        {
                            if (_hub.CurrentTriggerSpaceKind == TooltipChannelSpaceKind.World)
                            {
                                if (_effectiveEnablePointerHover && IsPointerOver(pointerScreen, camera))
                                    return true;
                            }
                            else
                            {
                                if (_effectiveEnablePointerHover && IsSelectionMatch() && IsPointerOver(pointerScreen, camera))
                                    return true;
                            }
                            break;
                        }
                    case TooltipChannelInputMode.Navigation:
                        {
                            if (_effectiveEnableSelectionHover && IsSelectionMatch())
                                return true;
                            break;
                        }
                }
            }

            var trigger = ResolveEligibleTrigger(inputMode, pointerScreen, camera);
            if (trigger == TooltipAutoTriggerKind.None)
            {
                ResetCandidate();
                return false;
            }

            UpdateCandidate(trigger, pointerScreen);
            var delay = trigger == TooltipAutoTriggerKind.Navigation
                ? _effectiveSelectionDelaySeconds
                : _effectiveHoverDelaySeconds;
            return Time.unscaledTime - _candidateStartTime >= delay;
        }

        TooltipAutoTriggerKind ResolveEligibleTrigger(TooltipChannelInputMode inputMode, Vector2 pointerScreen, Camera? camera)
        {
            switch (inputMode)
            {
                case TooltipChannelInputMode.Pointer:
                    return _effectiveEnablePointerHover && IsPointerOver(pointerScreen, camera)
                        ? TooltipAutoTriggerKind.Pointer
                        : TooltipAutoTriggerKind.None;

                case TooltipChannelInputMode.PointerNavigation:
                    if (!_effectiveEnablePointerHover || !IsPointerOver(pointerScreen, camera))
                        return TooltipAutoTriggerKind.None;

                    if (_hub.CurrentTriggerSpaceKind == TooltipChannelSpaceKind.World)
                        return TooltipAutoTriggerKind.Pointer;

                    return IsSelectionMatch()
                        ? TooltipAutoTriggerKind.PointerNavigation
                        : TooltipAutoTriggerKind.None;

                case TooltipChannelInputMode.Navigation:
                    return _effectiveEnableSelectionHover && IsSelectionMatch()
                        ? TooltipAutoTriggerKind.Navigation
                        : TooltipAutoTriggerKind.None;

                default:
                    return TooltipAutoTriggerKind.None;
            }
        }

        bool ResolveDesiredVisibility()
        {
            if (_overrideMode == TooltipChannelOverrideMode.ForceHide)
                return false;

            if (_overrideMode == TooltipChannelOverrideMode.ForceShow)
                return true;

            return _conditionEnabled && _autoTriggered;
        }

        void UpdateCandidate(TooltipAutoTriggerKind trigger, Vector2 pointerScreen)
        {
            if (trigger != _candidateTrigger)
            {
                _candidateTrigger = trigger;
                _candidateStartTime = Time.unscaledTime;
                _candidateStartPointer = pointerScreen;
                return;
            }

            if (trigger == TooltipAutoTriggerKind.Pointer || trigger == TooltipAutoTriggerKind.PointerNavigation)
            {
                var threshold = Mathf.Max(0f, _effectivePointerMoveThreshold);
                if ((pointerScreen - _candidateStartPointer).sqrMagnitude > threshold * threshold)
                {
                    _candidateStartTime = Time.unscaledTime;
                    _candidateStartPointer = pointerScreen;
                }
            }
        }

        void ResetCandidate()
        {
            _candidateTrigger = TooltipAutoTriggerKind.None;
            _candidateStartTime = 0f;
            _candidateStartPointer = Vector2.zero;
        }

        bool HasPointerMoved(Vector2 pointerScreen, float threshold)
        {
            if (!_hasLastPointerPos)
            {
                _hasLastPointerPos = true;
                _lastPointerPos = pointerScreen;
                return false;
            }

            var moved = (pointerScreen - _lastPointerPos).sqrMagnitude > threshold * threshold;
            _lastPointerPos = pointerScreen;
            return moved;
        }

        bool IsPointerOver(Vector2 pointerScreen, Camera? camera)
        {
            if (!IsModalAllowed())
                return false;

            var hitTest = _currentHitTestPreset.HasAnyTarget
                ? _currentHitTestPreset
                : _hub.CurrentDefaultHitTest;

            if (!hitTest.HasAnyTarget)
            {
                LogPointerOverState("No hit test targets.");
                return false;
            }

            ResolveHitTestTargets();

            if (_hub.CurrentTriggerSpaceKind == TooltipChannelSpaceKind.UIScreen)
            {
                for (var i = 0; i < _resolvedHitTestTargets.Count; i++)
                {
                    var rect = _resolvedHitTestTargets[i].RectTransform;
                    if (rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, pointerScreen, camera))
                    {
                        LogPointerOverState($"UI rect hit. Index={i} Name={rect.name}");
                        return true;
                    }
                }

                LogPointerOverState($"UI miss. ResolvedTargets={_resolvedHitTestTargets.Count}");
                return false;
            }

            if (TryIsWorldPointerHoveringResolvedTarget(out var hoveredTargetName, out var hoverDebugState))
            {
                LogPointerOverState($"World pointer hover hit. Target={hoveredTargetName}");
                return true;
            }

            if (camera == null || _hub.PointerService == null)
            {
                LogPointerOverState($"World miss. CameraNull={camera == null} PointerServiceNull={_hub.PointerService == null}");
                return false;
            }

            for (var i = 0; i < _resolvedHitTestTargets.Count; i++)
            {
                var pointerTarget = _resolvedHitTestTargets[i].WorldPointerTarget;
                if (pointerTarget != null && TryHitWorldPointerTarget(pointerTarget, pointerScreen, camera, out var pointerTargetWorld))
                {
                    LogPointerOverState($"World pointer target hit. Index={i} Name={pointerTarget.name} World={pointerTargetWorld}");
                    return true;
                }

                var rect = _resolvedHitTestTargets[i].RectTransform;
                if (rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, pointerScreen, camera))
                {
                    LogPointerOverState($"World rect hit. Index={i} Name={rect.name}");
                    return true;
                }

                var sprite = _resolvedHitTestTargets[i].SpriteRenderer;
                if (sprite == null)
                    continue;

                var bounds = sprite.bounds;
                var spriteZ = bounds.center.z;
                var world2 = _hub.PointerService.PointerWorld(camera, spriteZ);
                var world = new Vector3(world2.x, world2.y, spriteZ);
                if (bounds.Contains(world))
                {
                    LogPointerOverState($"World sprite hit. Index={i} Name={sprite.name} World={world}");
                    return true;
                }
            }

            var fallbackZ = ResolveWorldPlaneZ();
            _ = fallbackZ;
            LogPointerOverState($"World miss. ResolvedTargets={_resolvedHitTestTargets.Count} HoverState={hoverDebugState}");
            return false;
        }

        bool TryIsWorldPointerHoveringResolvedTarget(out string hoveredTargetName, out string debugState)
        {
            hoveredTargetName = string.Empty;
            debugState = "Unavailable";

            var worldPointer = _hub.WorldPointerService;
            if (worldPointer == null)
            {
                debugState = "ServiceNull";
                return false;
            }

            if (!worldPointer.TryGetCurrentHover(out var hoverData))
            {
                debugState = "NoCurrentHover";
                return false;
            }

            if (hoverData.Target == null)
            {
                debugState = "HoverTargetNull";
                return false;
            }

            hoveredTargetName = hoverData.Target.name;
            for (var i = 0; i < _resolvedHitTestTargets.Count; i++)
            {
                var target = _resolvedHitTestTargets[i].WorldPointerTarget;
                if (target != null && ReferenceEquals(target, hoverData.Target))
                {
                    debugState = $"Matched:{hoveredTargetName}";
                    return true;
                }
            }

            debugState = $"Hovering:{hoveredTargetName}";
            return false;
        }

        bool TryHitWorldPointerTarget(WorldPointerTargetMB target, Vector2 pointerScreen, Camera camera, out Vector3 pointerWorld)
        {
            pointerWorld = Vector3.zero;
            if (_hub.PointerService == null)
                return false;

            var world2 = _hub.PointerService.PointerWorld(camera, target.transform.position.z);
            pointerWorld = new Vector3(world2.x, world2.y, target.transform.position.z);
            var colliders = target.ResolveColliders();
            for (var i = 0; i < colliders.Count; i++)
            {
                var collider = colliders[i];
                if (collider != null && collider.OverlapPoint(world2))
                    return true;
            }

            return false;
        }

        bool IsSelectionMatch()
        {
            return _hub.CurrentTriggerSpaceKind == TooltipChannelSpaceKind.UIScreen &&
                   _hub.SelectionState?.CurrentElement != null &&
                   ReferenceEquals(_hub.SelectionState.CurrentElement, _owner) &&
                   IsModalAllowed();
        }

        bool IsModalAllowed()
        {
            if (_hub.CurrentTriggerSpaceKind != TooltipChannelSpaceKind.UIScreen)
                return true;

            if (_hub.ModalStackService == null)
                return true;

            return _hub.ModalStackService.IsInAnyInputRoot(_owner);
        }

        RectTransform? ResolveRectTransform(TooltipHitTestTarget target)
        {
            return target.Kind switch
            {
                TooltipHitTestTargetKind.OwnerRectTransform => ResolveOwnerRectTransform(),
                TooltipHitTestTargetKind.ActorRectTransform => ResolveActorRectTransform(target.ActorSource),
                _ => null,
            };
        }

        SpriteRenderer? ResolveSpriteRenderer(TooltipHitTestTarget target)
        {
            return target.Kind switch
            {
                TooltipHitTestTargetKind.OwnerSpriteRenderer => ResolveOwnerSpriteRenderer(),
                TooltipHitTestTargetKind.ActorSpriteRenderer => ResolveActorSpriteRenderer(target.ActorSource),
                _ => null,
            };
        }

        WorldPointerTargetMB? ResolveWorldPointerTarget(TooltipHitTestTarget target)
        {
            return target.Kind switch
            {
                TooltipHitTestTargetKind.OwnerWorldPointerTarget => ResolveOwnerWorldPointerTarget(),
                TooltipHitTestTargetKind.ActorWorldPointerTarget => ResolveActorWorldPointerTarget(target.ActorSource),
                TooltipHitTestTargetKind.OwnerSelectablePointerTarget => ResolveOwnerSelectablePointerTarget(),
                TooltipHitTestTargetKind.ActorSelectablePointerTarget => ResolveActorSelectablePointerTarget(target.ActorSource),
                _ => null,
            };
        }

        RectTransform? ResolveOwnerRectTransform()
        {
            if (_owner.Identity?.SelfTransform is RectTransform rectTransform)
                return rectTransform;

            var transform = _owner.Identity?.SelfTransform;
            return transform != null ? transform.GetComponentInChildren<RectTransform>(true) : null;
        }

        RectTransform? ResolveActorRectTransform(ActorSource actorSource)
        {
            var scope = ActorSourceFastResolver.Resolve(_activeScope ?? _owner, actorSource);
            if (scope?.Identity?.SelfTransform is RectTransform rectTransform)
                return rectTransform;

            var transform = scope?.Identity?.SelfTransform;
            return transform != null ? transform.GetComponentInChildren<RectTransform>(true) : null;
        }

        SpriteRenderer? ResolveOwnerSpriteRenderer()
        {
            var transform = _owner.Identity?.SelfTransform;
            return transform != null ? transform.GetComponentInChildren<SpriteRenderer>(true) : null;
        }

        SpriteRenderer? ResolveActorSpriteRenderer(ActorSource actorSource)
        {
            var scope = ActorSourceFastResolver.Resolve(_activeScope ?? _owner, actorSource);
            var transform = scope?.Identity?.SelfTransform;
            return transform != null ? transform.GetComponentInChildren<SpriteRenderer>(true) : null;
        }

        WorldPointerTargetMB? ResolveOwnerWorldPointerTarget()
        {
            var transform = _owner.Identity?.SelfTransform;
            return transform != null ? transform.GetComponentInChildren<WorldPointerTargetMB>(true) : null;
        }

        WorldPointerTargetMB? ResolveActorWorldPointerTarget(ActorSource actorSource)
        {
            var scope = ActorSourceFastResolver.Resolve(_activeScope ?? _owner, actorSource);
            var transform = scope?.Identity?.SelfTransform;
            return transform != null ? transform.GetComponentInChildren<WorldPointerTargetMB>(true) : null;
        }

        WorldPointerTargetMB? ResolveOwnerSelectablePointerTarget()
        {
            var transform = _owner.Identity?.SelfTransform;
            if (transform == null)
                return null;

            var selectable = transform.GetComponentInChildren<SelectableRuntimeMB>(true);
            return selectable != null ? selectable.ResolveTarget() : null;
        }

        WorldPointerTargetMB? ResolveActorSelectablePointerTarget(ActorSource actorSource)
        {
            var scope = ActorSourceFastResolver.Resolve(_activeScope ?? _owner, actorSource);
            var transform = scope?.Identity?.SelfTransform;
            if (transform == null)
                return null;

            var selectable = transform.GetComponentInChildren<SelectableRuntimeMB>(true);
            return selectable != null ? selectable.ResolveTarget() : null;
        }

        Transform? ResolveAnchorTransform()
        {
            var scope = ActorSourceFastResolver.ResolveCached(_activeScope ?? _owner, _effectiveAnchorActorSource, ref _anchorActorCache);
            return scope?.Identity?.SelfTransform ?? _owner.Identity?.SelfTransform;
        }

        void ResolveSourcePresets()
        {
            if (_dynamicContext == null)
                return;

            if (_options.PresetValue.TryGet(_dynamicContext, out TooltipPlayerPreset? preset) && preset != null)
                _basePlayerPreset = preset.CreateRuntimeCopy();
            else
                _basePlayerPreset = new TooltipPlayerPreset();

            _currentPlayerPreset = _basePlayerPreset.CreateRuntimeCopy();
            ResolveDependentPresetsFromPlayer();
            ResolveEffectiveSettings();
        }

        void ResolveDependentPresetsFromPlayer()
        {
            if (_dynamicContext == null)
            {
                _baseCommandsPreset = new TooltipCommandsPreset();
                _currentCommandsPreset = new TooltipCommandsPreset();
                _baseHitTestPreset = new TooltipHitTestPreset();
                _currentHitTestPreset = new TooltipHitTestPreset();
            }
            else
            {
                _baseCommandsPreset = ResolveCommandsPreset(_currentPlayerPreset.CommandsPresetValue, _dynamicContext);
                _currentCommandsPreset = _baseCommandsPreset.CreateRuntimeCopy();
                _baseHitTestPreset = TooltipChannelHubService.ResolveHitTestPreset(_currentPlayerPreset.HitTestValue, _dynamicContext, new TooltipHitTestPreset());
                _currentHitTestPreset = _baseHitTestPreset.CreateRuntimeCopy();
            }

            ResolveHitTestTargets();
        }

        void ResolveHitTestTargets()
        {
            var sourcePreset = _currentHitTestPreset.HasAnyTarget ? _currentHitTestPreset : _hub.CurrentDefaultHitTest;
            if (ReferenceEquals(_resolvedHitTestPresetSource, sourcePreset))
                return;

            _resolvedHitTestPresetSource = sourcePreset;
            _resolvedHitTestTargets.Clear();

            if (sourcePreset == null || !sourcePreset.HasAnyTarget)
                return;

            for (var i = 0; i < sourcePreset.Targets.Count; i++)
            {
                var target = sourcePreset.Targets[i];
                if (target == null)
                    continue;

                switch (target.Kind)
                {
                    case TooltipHitTestTargetKind.OwnerRectTransform:
                        _resolvedHitTestTargets.Add(new ResolvedHitTestTarget(ResolveOwnerRectTransform(), null, null));
                        break;
                    case TooltipHitTestTargetKind.OwnerSpriteRenderer:
                        _resolvedHitTestTargets.Add(new ResolvedHitTestTarget(null, ResolveOwnerSpriteRenderer(), null));
                        break;
                    case TooltipHitTestTargetKind.ActorRectTransform:
                        _resolvedHitTestTargets.Add(new ResolvedHitTestTarget(ResolveActorRectTransform(target.ActorSource), null, null));
                        break;
                    case TooltipHitTestTargetKind.ActorSpriteRenderer:
                        _resolvedHitTestTargets.Add(new ResolvedHitTestTarget(null, ResolveActorSpriteRenderer(target.ActorSource), null));
                        break;
                    case TooltipHitTestTargetKind.OwnerWorldPointerTarget:
                    case TooltipHitTestTargetKind.ActorWorldPointerTarget:
                    case TooltipHitTestTargetKind.OwnerSelectablePointerTarget:
                    case TooltipHitTestTargetKind.ActorSelectablePointerTarget:
                        _resolvedHitTestTargets.Add(new ResolvedHitTestTarget(null, null, ResolveWorldPointerTarget(target)));
                        break;
                }
            }

            LogResolvedHitTargets();
        }

        void ResolveEffectiveSettings()
        {
            var sharedDefaults = _hub.TooltipSystemService?.SharedDefaults;
            var runtimeDefaults = sharedDefaults?.RuntimeDefaults;
            var inputDefaults = sharedDefaults?.InputDefaults;
            var placementDefaults = sharedDefaults?.PlacementDefaults;

            if (_currentPlayerPreset.ApplyRuntimeOverride || runtimeDefaults == null)
            {
                _effectiveRuntimeTemplatePresetValue = _currentPlayerPreset.RuntimeTemplatePresetValue;
                _effectiveSpawnerTag = _currentPlayerPreset.SpawnerTag;
            }
            else
            {
                _effectiveRuntimeTemplatePresetValue = runtimeDefaults.RuntimeTemplatePresetValue;
                _effectiveSpawnerTag = runtimeDefaults.SpawnerTag;
            }

            if (_currentPlayerPreset.ApplyInputOverride || inputDefaults == null)
            {
                _effectiveEnablePointerHover = _currentPlayerPreset.EnablePointerHover;
                _effectiveEnableSelectionHover = _currentPlayerPreset.EnableSelectionHover;
                _effectiveHoverDelaySeconds = _currentPlayerPreset.HoverDelaySeconds;
                _effectiveSelectionDelaySeconds = _currentPlayerPreset.SelectionDelaySeconds;
                _effectivePointerMoveThreshold = _currentPlayerPreset.PointerMoveThreshold;
            }
            else
            {
                _effectiveEnablePointerHover = inputDefaults.EnablePointerHover;
                _effectiveEnableSelectionHover = inputDefaults.EnableSelectionHover;
                _effectiveHoverDelaySeconds = inputDefaults.HoverDelaySeconds;
                _effectiveSelectionDelaySeconds = inputDefaults.SelectionDelaySeconds;
                _effectivePointerMoveThreshold = inputDefaults.PointerMoveThreshold;
            }

            if (_currentPlayerPreset.ApplyPlacementOverride || placementDefaults == null)
            {
                _effectiveAnchorActorSource = _currentPlayerPreset.AnchorActorSource;
                _effectiveSpawnMode = _currentPlayerPreset.SpawnMode;
                _effectiveFollowPointerDirectionOffset = _currentPlayerPreset.FollowPointerDirectionOffset;
                _effectiveFollowPointerMoveScale = _currentPlayerPreset.FollowPointerMoveScale;
                _effectiveFixedDirectionOffset = _currentPlayerPreset.FixedDirectionOffset;
                _effectiveAnchorX = _currentPlayerPreset.AnchorX;
                _effectiveAnchorY = _currentPlayerPreset.AnchorY;
            }
            else
            {
                _effectiveAnchorActorSource = placementDefaults.AnchorActorSource;
                _effectiveSpawnMode = placementDefaults.SpawnMode;
                _effectiveFollowPointerDirectionOffset = placementDefaults.FollowPointerDirectionOffset;
                _effectiveFollowPointerMoveScale = placementDefaults.FollowPointerMoveScale;
                _effectiveFixedDirectionOffset = placementDefaults.FixedDirectionOffset;
                _effectiveAnchorX = placementDefaults.AnchorX;
                _effectiveAnchorY = placementDefaults.AnchorY;
            }

            _anchorActorCache = default;
        }

        void ResetEffectiveSettings()
        {
            _effectiveRuntimeTemplatePresetValue = default;
            _effectiveSpawnerTag = string.Empty;
            _effectiveEnablePointerHover = true;
            _effectiveEnableSelectionHover = true;
            _effectiveHoverDelaySeconds = 0.4f;
            _effectiveSelectionDelaySeconds = 0.3f;
            _effectivePointerMoveThreshold = 2f;
            _effectiveAnchorActorSource = new ActorSource { Kind = ActorSourceKind.Current };
            _effectiveSpawnMode = TooltipChannelSpawnMode.FollowPointer;
            _effectiveFollowPointerDirectionOffset = Vector3.zero;
            _effectiveFollowPointerMoveScale = Vector2.one;
            _effectiveFixedDirectionOffset = Vector3.zero;
            _effectiveAnchorX = TooltipChannelAnchorX.Right;
            _effectiveAnchorY = TooltipChannelAnchorY.Up;
        }

        static TooltipCommandsPreset ResolveCommandsPreset(DynamicValue<TooltipCommandsPreset> value, IDynamicContext context)
        {
            if (value.TryGet(context, out TooltipCommandsPreset? preset) && preset != null)
                return preset.CreateRuntimeCopy();

            return new TooltipCommandsPreset();
        }

        void RequestRespawnForConfigurationChange()
        {
            ResetCandidate();
            _hasFollowPointerUi = false;
            _hasFollowPointerWorld = false;
            ResetPlacementCaches();

            if (_visibilityState == TooltipChannelVisibilityState.Active || _visibilityState == TooltipChannelVisibilityState.Spawning)
                BeginClose(runHideCommands: true);
        }

        void BeginSpawn(Vector2 pointerScreen, Camera? camera)
        {
            if (_lifetimeCts == null || _dynamicContext == null || _visibilityState == TooltipChannelVisibilityState.Spawning)
                return;

            LogDebug($"BeginSpawn. Pointer={pointerScreen} Camera={(camera != null ? camera.name : "null")}");
            CancelOperation();
            _visibilityState = TooltipChannelVisibilityState.Spawning;
            _operationCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);
            var version = ++_operationVersion;
            RunSpawnAsync(version, pointerScreen, camera, _operationCts.Token).Forget();
        }

        void BeginClose(bool runHideCommands)
        {
            if (_visibilityState == TooltipChannelVisibilityState.Hidden || _visibilityState == TooltipChannelVisibilityState.Closing)
                return;

            LogDebug($"BeginClose. RunHideCommands={runHideCommands}");
            CancelOperation();
            _visibilityState = TooltipChannelVisibilityState.Closing;
            var lifetimeToken = _lifetimeCts != null ? _lifetimeCts.Token : CancellationToken.None;
            _operationCts = CancellationTokenSource.CreateLinkedTokenSource(lifetimeToken);
            var version = ++_operationVersion;
            RunCloseAsync(version, runHideCommands, _operationCts.Token).Forget();
        }

        void CancelOperation()
        {
            _operationVersion++;
            _operationCts?.Cancel();
            _operationCts?.Dispose();
            _operationCts = null;
        }

        async UniTaskVoid RunSpawnAsync(int version, Vector2 pointerScreen, Camera? camera, CancellationToken ct)
        {
            IScopeNode? spawnedScope = null;
            try
            {
                var runtimeTemplate = ResolveRuntimeTemplate();
                if (runtimeTemplate == null)
                {
                    if (IsCurrentVersion(version))
                        _visibilityState = TooltipChannelVisibilityState.Hidden;
                    return;
                }

                var registry = _hub.SpawnerRegistry;
                if (registry == null)
                {
                    if (IsCurrentVersion(version))
                        _visibilityState = TooltipChannelVisibilityState.Hidden;
                    return;
                }

                var spawnerKind = _hub.CurrentRenderSpaceKind == TooltipChannelRenderSpaceKind.UIScreen
                    ? SpawnerKind.RuntimeUIElement
                    : SpawnerKind.RuntimeEntity;
                var resolved = SceneSpawnerResolver.TryResolveAsyncSpawner(
                    registry,
                    spawnerKind,
                    _effectiveSpawnerTag,
                    allowTagFallback: string.IsNullOrEmpty(_effectiveSpawnerTag),
                    allowRuntimeUiFallback: true);
                if (resolved.Spawner == null)
                {
                    if (IsCurrentVersion(version))
                        _visibilityState = TooltipChannelVisibilityState.Hidden;
                    return;
                }

                var transformParent = _hub.CurrentRenderSpaceKind == TooltipChannelRenderSpaceKind.UIScreen
                    ? _hub.UiRoot
                    : _hub.WorldRoot;
                var spawnParams = SpawnParams.ForRuntime(
                    runtimeTemplate,
                    Vector3.zero,
                    Quaternion.identity,
                    Vector3.one,
                    identity: null,
                    transformParent: transformParent,
                    lifetimeScopeParent: _owner,
                    worldSpace: _hub.CurrentRenderSpaceKind == TooltipChannelRenderSpaceKind.World,
                    allowPooling: runtimeTemplate.UsePooling);

                var resolver = await resolved.Spawner.SpawnAsync(spawnParams, ct);
                if (resolver == null || !resolver.TryResolve<IScopeNode>(out spawnedScope) || spawnedScope == null)
                {
                    if (IsCurrentVersion(version))
                        _visibilityState = TooltipChannelVisibilityState.Hidden;
                    return;
                }

                TooltipChannelHubService.EnsureScopeBuiltIfNeeded(spawnedScope);
                var transform = TooltipChannelHubService.GetTransformFromScope(spawnedScope);
                if (transform == null)
                {
                    TooltipChannelHubService.ReleaseSpawnedRuntime(spawnedScope);
                    if (IsCurrentVersion(version))
                        _visibilityState = TooltipChannelVisibilityState.Hidden;
                    return;
                }

                _spawnedScope = spawnedScope;
                _activeTransform = transform;
                _activeRectTransform = transform as RectTransform;
                _spawnBaseUiLocalZ = _activeRectTransform != null ? _activeRectTransform.localPosition.z : 0f;
                _spawnBaseWorldZ = _activeTransform.position.z;
                ResetPlacementCaches();
                ResolveBoundsService(spawnedScope);
                CaptureFollowPointerReference(pointerScreen, camera);
                MoveOffscreen();

                await ExecuteCommandsAsync(spawnedScope, _currentCommandsPreset.ShowCommands, ct);
                await WaitSpawnWarmupFrames(ct);

                RefreshBounds(force: true);
                if (!IsCurrentVersion(version))
                {
                    TooltipChannelHubService.ReleaseSpawnedRuntime(spawnedScope);
                    return;
                }

                _visibilityState = TooltipChannelVisibilityState.Active;
                LogDebug($"Spawn completed. Scope={spawnedScope.Identity?.Id ?? TooltipChannelHubService.GetTransformFromScope(spawnedScope)?.name ?? "(unknown)"}");
            }
            catch (OperationCanceledException)
            {
                if (!IsCurrentVersion(version) && spawnedScope != null)
                    TooltipChannelHubService.ReleaseSpawnedRuntime(spawnedScope);
            }
            catch (Exception ex)
            {
                if (spawnedScope != null)
                    TooltipChannelHubService.ReleaseSpawnedRuntime(spawnedScope);

                if (IsCurrentVersion(version))
                {
                    Debug.LogError($"[TooltipChannel] Spawn failed. Tag={_tag} Message={ex.Message}");
                    _spawnedScope = null;
                    _activeTransform = null;
                    _activeRectTransform = null;
                    _boundsService = null;
                    _boundsOutput = null;
                    _spawnBaseUiLocalZ = 0f;
                    _spawnBaseWorldZ = 0f;
                    ResetPlacementCaches();
                    _visibilityState = TooltipChannelVisibilityState.Hidden;
                }
            }
        }

        async UniTaskVoid RunCloseAsync(int version, bool runHideCommands, CancellationToken ct)
        {
            var scope = _spawnedScope;
            try
            {
                if (scope != null && runHideCommands)
                {
                    var hideList = BuildHideList();
                    await ExecuteCommandsAsync(scope, hideList, ct);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TooltipChannel] Close failed. Tag={_tag} Message={ex.Message}");
            }
            finally
            {
                if (scope != null && ReferenceEquals(scope, _spawnedScope))
                    TooltipChannelHubService.ReleaseSpawnedRuntime(scope);

                if (IsCurrentVersion(version))
                {
                    _spawnedScope = null;
                    _activeTransform = null;
                    _activeRectTransform = null;
                    _boundsService = null;
                    _boundsOutput = null;
                    _spawnBaseUiLocalZ = 0f;
                    _spawnBaseWorldZ = 0f;
                    ResetPlacementCaches();
                    _hasFollowPointerUi = false;
                    _hasFollowPointerWorld = false;
                    _visibilityState = TooltipChannelVisibilityState.Hidden;
                    LogDebug("Close completed.");
                }
            }
        }

        void LogResolvedHitTargets()
        {
            if (!_hub.EnableDebugLog)
                return;

            Debug.Log($"[TooltipChannel] Resolved hit targets. Tag={_tag} Count={_resolvedHitTestTargets.Count} TriggerSpace={_hub.CurrentTriggerSpaceKind} RenderSpace={_hub.CurrentRenderSpaceKind}");
            for (var i = 0; i < _resolvedHitTestTargets.Count; i++)
            {
                var rect = _resolvedHitTestTargets[i].RectTransform;
                var sprite = _resolvedHitTestTargets[i].SpriteRenderer;
                var pointerTarget = _resolvedHitTestTargets[i].WorldPointerTarget;
                var label = rect != null
                    ? $"RectTransform({rect.name})"
                    : sprite != null
                        ? $"SpriteRenderer({sprite.name})"
                        : pointerTarget != null
                            ? $"WorldPointerTarget({pointerTarget.name})"
                            : "null";
                Debug.Log($"[TooltipChannel] HitTarget[{i}] Tag={_tag} {label}");
            }
        }

        void LogPointerOverState(string state)
        {
            if (!_hub.EnableDebugLog || string.Equals(_lastPointerOverDebugState, state, StringComparison.Ordinal))
                return;

            _lastPointerOverDebugState = state;
            Debug.Log($"[TooltipChannel] PointerOver Tag={_tag} {state}");
        }

        void LogDebug(string message)
        {
            if (!_hub.EnableDebugLog)
                return;

            Debug.Log($"[TooltipChannel] {message} Tag={_tag}");
        }

        bool IsCurrentVersion(int version)
        {
            return version == _operationVersion;
        }

        BaseRuntimeTemplateSO? ResolveRuntimeTemplate()
        {
            if (_dynamicContext == null)
                return null;

            if (!_effectiveRuntimeTemplatePresetValue.TryGet(_dynamicContext, out var preset) || preset == null)
                return null;

            return RuntimeTemplatePresetResolver.ResolveTemplateSO(preset);
        }

        void ResolveBoundsService(IScopeNode scope)
        {
            _boundsService = null;
            _boundsOutput = null;
            ResetPlacementCaches();
            var resolver = scope.Resolver;
            if (resolver == null)
                return;

            if (resolver.TryResolve<IVisualBoundsService>(out var boundsService) && boundsService != null)
            {
                _boundsService = boundsService;
                _boundsOutput = boundsService as IVisualBoundsOutput;
            }
        }

        async UniTask WaitSpawnWarmupFrames(CancellationToken ct)
        {
            var frames = _hub.ResolveSpawnWarmupFrames();
            for (var i = 0; i < frames; i++)
            {
                ct.ThrowIfCancellationRequested();
                await UniTask.NextFrame(ct);
                RefreshBounds(force: true);
                MoveOffscreen();
            }
        }

        void MoveOffscreen()
        {
            if (_hub.CurrentRenderSpaceKind == TooltipChannelRenderSpaceKind.UIScreen)
                MoveUiOffscreen();
            else
                MoveWorldOffscreen();
        }

        CommandListData BuildHideList()
        {
            var list = new CommandListData();
            var hideCommands = _currentCommandsPreset.HideCommands;
            if (hideCommands?.Commands != null)
            {
                for (var i = 0; i < hideCommands.Commands.Count; i++)
                {
                    var source = hideCommands.Commands[i];
                    if (source != null)
                        list.Add(source);
                }
            }

            list.Add(_currentCommandsPreset.SelfDespawn);
            return list;
        }

        async UniTask ExecuteCommandsAsync(IScopeNode scope, CommandListData? commands, CancellationToken ct)
        {
            if (commands == null || commands.Count == 0)
                return;

            var resolver = scope.Resolver;
            if (resolver == null || !resolver.TryResolve<ICommandRunner>(out var runner) || runner == null)
                return;

            var vars = TooltipChannelHubService.ResolveVars(scope);
            var options = CommandRunOptions.Default;
            var context = new CommandContext(scope, vars, runner, scope, options);
            ApplyPresetContextSlots(context, _owner, _currentCommandsPreset);
            var result = await runner.ExecuteListAsync(commands, context, ct, options);
            if (result.Status == CommandRunStatus.Error && !string.IsNullOrEmpty(result.Message))
                Debug.LogError($"[TooltipChannel] Command execution failed. Tag={_tag} Message={result.Message}");
        }

        void ApplyPresetContextSlots(CommandContext context, IScopeNode tooltipOwner, TooltipCommandsPreset commandsPreset)
        {
            if (commandsPreset.WriteTooltipOwnerToContext)
                TrySetContextSlot(context, commandsPreset.TooltipOwnerContextSlot, tooltipOwner, "tooltip owner");
        }

        void TrySetContextSlot(CommandContext context, CommandLtsSlot slot, IScopeNode value, string label)
        {
            if (!CommandLtsSlotUtility.IsContextSlot(slot))
            {
                Debug.LogError($"[TooltipChannel] {label} context slot must be ContextA-D. Tag={_tag} Slot={slot}");
                return;
            }

            context.SetScope(slot, value);
        }

        void CaptureFollowPointerReference(Vector2 pointerScreen, Camera? camera)
        {
            _hasFollowPointerUi = false;
            _hasFollowPointerWorld = false;

            if (_effectiveSpawnMode != TooltipChannelSpawnMode.FollowPointer)
                return;

            if (_hub.CurrentRenderSpaceKind == TooltipChannelRenderSpaceKind.UIScreen)
            {
                var root = _hub.UiRoot;
                if (root == null)
                    return;

                RectTransformUtility.ScreenPointToLocalPointInRectangle(root, pointerScreen, camera, out var local);
                _followPointerStartUi = local;
                _followPointerBaseUi = local;
                _hasFollowPointerUi = true;
                return;
            }

            if (_hub.PointerService == null || camera == null)
                return;

            var z = ResolveWorldPlaneZ();
            var world2 = _hub.PointerService.PointerWorld(camera, z);
            _followPointerStartWorld = new Vector3(world2.x, world2.y, z);
            _followPointerBaseWorld = _followPointerStartWorld;
            _hasFollowPointerWorld = true;
        }

        Vector2 ResolveUiBaseLocal(Vector2 pointerScreen, Camera? camera)
        {
            var root = _hub.UiRoot;
            if (root == null)
                return Vector2.zero;

            if (_effectiveSpawnMode == TooltipChannelSpawnMode.FollowPointer)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(root, pointerScreen, camera, out var local);
                if (!_hasFollowPointerUi)
                    return local;

                var delta = local - _followPointerStartUi;
                return _followPointerBaseUi + Vector2.Scale(delta, _effectiveFollowPointerMoveScale);
            }

            var anchor = ResolveAnchorTransform() ?? _hub.UiRoot ?? _hub.WorldRoot;
            var anchorLocal = anchor != null ? (Vector2)root.InverseTransformPoint(anchor.position) : Vector2.zero;
            return anchorLocal;
        }

        Vector3 ResolveWorldBase(Vector2 pointerScreen, Camera camera)
        {
            if (_effectiveSpawnMode == TooltipChannelSpawnMode.FollowPointer && _hub.PointerService != null)
            {
                var z = ResolveWorldPlaneZ();
                var world2 = _hub.PointerService.PointerWorld(camera, z);
                var current = new Vector3(world2.x, world2.y, z);
                if (!_hasFollowPointerWorld)
                    return current;

                var delta = current - _followPointerStartWorld;
                var scaled = new Vector3(
                    delta.x * _effectiveFollowPointerMoveScale.x,
                    delta.y * _effectiveFollowPointerMoveScale.y,
                    0f);
                return _followPointerBaseWorld + scaled;
            }

            var anchor = ResolveAnchorTransform() ?? _activeTransform ?? _hub.WorldRoot;
            var pos = anchor != null ? anchor.position : Vector3.zero;
            return new Vector3(pos.x, pos.y, ResolveWorldPlaneZ());
        }

        Vector3 ResolveActiveDirectionOffset()
        {
            return _effectiveSpawnMode == TooltipChannelSpawnMode.FollowPointer
                ? _effectiveFollowPointerDirectionOffset
                : _effectiveFixedDirectionOffset;
        }

        float ResolveWorldPlaneZ()
        {
            if (_activeTransform != null)
                return _spawnBaseWorldZ;

            var anchor = ResolveAnchorTransform();
            if (anchor != null)
                return anchor.position.z;

            return _hub.WorldRoot != null ? _hub.WorldRoot.position.z : 0f;
        }

        bool TryResolveUiScreenSize(Camera? camera, out Rect localRect, out Vector2 screenSize)
        {
            localRect = ResolveLocalRect();
            screenSize = Vector2.zero;
            if (_activeRectTransform == null)
                return false;

            RefreshBounds(force: false);
            localRect = ResolveLocalRect();
            if (TryGetCachedUiScreenSize(localRect, camera, out screenSize))
                return screenSize.x > 0f && screenSize.y > 0f;

            var screenRect = ComputeScreenRectFromLocal(localRect, _activeRectTransform, camera);
            screenSize = SanitizeSize(screenRect.size);
            CacheUiScreenSize(localRect, screenSize, camera);
            return screenSize.x > 0f && screenSize.y > 0f;
        }

        bool TryResolveWorldScreenSize(Camera camera, out Vector2 screenSize)
        {
            screenSize = Vector2.zero;
            if (_activeTransform == null)
                return false;

            RefreshBounds(force: false);
            var bounds = ResolveWorldBounds(_activeTransform.position);
            if (TryGetCachedWorldScreenSize(bounds, camera, out screenSize))
                return screenSize.x > 0f && screenSize.y > 0f;

            var screenRect = ComputeScreenRectFromWorld(bounds, camera);
            screenSize = SanitizeSize(screenRect.size);
            CacheWorldScreenSize(bounds, screenSize, camera);
            return screenSize.x > 0f && screenSize.y > 0f;
        }

        Rect ResolveLocalRect()
        {
            if (_boundsOutput != null && _boundsOutput.HasBounds)
                return _boundsOutput.LocalRect;

            if (_activeRectTransform != null)
            {
                var rect = _activeRectTransform.rect;
                var pivot = _activeRectTransform.pivot;
                return new Rect(
                    -rect.width * pivot.x,
                    -rect.height * pivot.y,
                    rect.width,
                    rect.height);
            }

            return new Rect(-0.5f, -0.5f, 1f, 1f);
        }

        Bounds ResolveWorldBounds(Vector3 rootPos)
        {
            if (_boundsOutput != null && _boundsOutput.HasBounds)
                return _boundsOutput.WorldBounds;

            var center = rootPos;
            return new Bounds(center, Vector3.one);
        }

        void RefreshBounds(bool force)
        {
            if (_boundsService == null)
                return;

            if (!force && _boundsOutput != null && _boundsOutput.HasBounds)
                return;

            if (_boundsService.TryGetLastOutput(out var output) && output.HasBounds)
            {
                _boundsOutput = _boundsService as IVisualBoundsOutput;
                ResetPlacementCaches();
            }
        }

        void ResetPlacementCaches()
        {
            _cachedUiLocalRect = default;
            _cachedUiScreenSize = default;
            _cachedUiWorldPosition = default;
            _cachedUiWorldRotation = default;
            _cachedUiLossyScale = default;
            _cachedUiCamera = null;
            _cachedUiScreenWidth = 0;
            _cachedUiScreenHeight = 0;
            _hasCachedUiScreenSize = false;

            _cachedWorldBounds = default;
            _cachedWorldScreenSize = default;
            _cachedWorldPosition = default;
            _cachedWorldRotation = default;
            _cachedWorldLossyScale = default;
            _cachedWorldCamera = null;
            _cachedWorldScreenWidth = 0;
            _cachedWorldScreenHeight = 0;
            _hasCachedWorldScreenSize = false;
        }

        bool TryGetCachedUiScreenSize(Rect localRect, Camera? camera, out Vector2 screenSize)
        {
            screenSize = default;
            if (!_hasCachedUiScreenSize || _activeRectTransform == null)
                return false;

            if (!ReferenceEquals(_cachedUiCamera, camera) ||
                _cachedUiScreenWidth != Screen.width ||
                _cachedUiScreenHeight != Screen.height ||
                _cachedUiLocalRect != localRect ||
                _cachedUiWorldPosition != _activeRectTransform.position ||
                _cachedUiWorldRotation != _activeRectTransform.rotation ||
                _cachedUiLossyScale != _activeRectTransform.lossyScale)
            {
                return false;
            }

            screenSize = _cachedUiScreenSize;
            return true;
        }

        void CacheUiScreenSize(Rect localRect, Vector2 screenSize, Camera? camera)
        {
            if (_activeRectTransform == null)
                return;

            _cachedUiLocalRect = localRect;
            _cachedUiScreenSize = screenSize;
            _cachedUiWorldPosition = _activeRectTransform.position;
            _cachedUiWorldRotation = _activeRectTransform.rotation;
            _cachedUiLossyScale = _activeRectTransform.lossyScale;
            _cachedUiCamera = camera;
            _cachedUiScreenWidth = Screen.width;
            _cachedUiScreenHeight = Screen.height;
            _hasCachedUiScreenSize = true;
        }

        bool TryGetCachedWorldScreenSize(Bounds bounds, Camera camera, out Vector2 screenSize)
        {
            screenSize = default;
            if (!_hasCachedWorldScreenSize || _activeTransform == null)
                return false;

            if (!ReferenceEquals(_cachedWorldCamera, camera) ||
                _cachedWorldScreenWidth != Screen.width ||
                _cachedWorldScreenHeight != Screen.height ||
                _cachedWorldBounds.center != bounds.center ||
                _cachedWorldBounds.size != bounds.size ||
                _cachedWorldPosition != _activeTransform.position ||
                _cachedWorldRotation != _activeTransform.rotation ||
                _cachedWorldLossyScale != _activeTransform.lossyScale)
            {
                return false;
            }

            screenSize = _cachedWorldScreenSize;
            return true;
        }

        void CacheWorldScreenSize(Bounds bounds, Vector2 screenSize, Camera camera)
        {
            if (_activeTransform == null)
                return;

            _cachedWorldBounds = bounds;
            _cachedWorldScreenSize = screenSize;
            _cachedWorldPosition = _activeTransform.position;
            _cachedWorldRotation = _activeTransform.rotation;
            _cachedWorldLossyScale = _activeTransform.lossyScale;
            _cachedWorldCamera = camera;
            _cachedWorldScreenWidth = Screen.width;
            _cachedWorldScreenHeight = Screen.height;
            _hasCachedWorldScreenSize = true;
        }

        static Vector2 SanitizeSize(Vector2 raw)
        {
            var width = raw.x;
            var height = raw.y;
            if (float.IsNaN(width) || float.IsNaN(height) || float.IsInfinity(width) || float.IsInfinity(height))
                return new Vector2(1f, 1f);

            return new Vector2(Mathf.Max(1f, width), Mathf.Max(1f, height));
        }

        static Vector2 ResolveLocalAnchorPoint(Rect localRect, TooltipChannelAnchorX anchorX, TooltipChannelAnchorY anchorY)
        {
            float x = anchorX switch
            {
                TooltipChannelAnchorX.Left => localRect.max.x,
                TooltipChannelAnchorX.Center => localRect.center.x,
                TooltipChannelAnchorX.Right => localRect.min.x,
                _ => localRect.min.x,
            };

            float y = anchorY switch
            {
                TooltipChannelAnchorY.Up => localRect.min.y,
                TooltipChannelAnchorY.Center => localRect.center.y,
                TooltipChannelAnchorY.Down => localRect.max.y,
                _ => localRect.min.y,
            };

            return new Vector2(x, y);
        }

        static Vector3 ResolveWorldAnchorPoint(Bounds bounds, TooltipChannelAnchorX anchorX, TooltipChannelAnchorY anchorY, Vector3 rootPos)
        {
            float x = anchorX switch
            {
                TooltipChannelAnchorX.Left => bounds.max.x,
                TooltipChannelAnchorX.Center => bounds.center.x,
                TooltipChannelAnchorX.Right => bounds.min.x,
                _ => bounds.min.x,
            };

            float y = anchorY switch
            {
                TooltipChannelAnchorY.Up => bounds.min.y,
                TooltipChannelAnchorY.Center => bounds.center.y,
                TooltipChannelAnchorY.Down => bounds.max.y,
                _ => bounds.min.y,
            };

            return new Vector3(x, y, rootPos.z);
        }

        Rect ComputeScreenRectFromLocal(Rect localRect, Transform root, Camera? camera)
        {
            var w0 = root.TransformPoint(new Vector3(localRect.min.x, localRect.min.y, 0f));
            var w1 = root.TransformPoint(new Vector3(localRect.min.x, localRect.max.y, 0f));
            var w2 = root.TransformPoint(new Vector3(localRect.max.x, localRect.max.y, 0f));
            var w3 = root.TransformPoint(new Vector3(localRect.max.x, localRect.min.y, 0f));

            var s0 = RectTransformUtility.WorldToScreenPoint(camera, w0);
            var s1 = RectTransformUtility.WorldToScreenPoint(camera, w1);
            var s2 = RectTransformUtility.WorldToScreenPoint(camera, w2);
            var s3 = RectTransformUtility.WorldToScreenPoint(camera, w3);

            var min = Vector2.Min(Vector2.Min(s0, s1), Vector2.Min(s2, s3));
            var max = Vector2.Max(Vector2.Max(s0, s1), Vector2.Max(s2, s3));
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        Rect ComputeScreenRectFromWorld(Bounds bounds, Camera camera)
        {
            FillBoundsCorners(bounds, _rectCorners8);
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (var i = 0; i < _rectCorners8.Length; i++)
            {
                var screen = camera.WorldToScreenPoint(_rectCorners8[i]);
                min = Vector2.Min(min, screen);
                max = Vector2.Max(max, screen);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        static void FillBoundsCorners(Bounds bounds, Vector3[] corners)
        {
            var min = bounds.min;
            var max = bounds.max;

            corners[0] = new Vector3(min.x, min.y, min.z);
            corners[1] = new Vector3(min.x, min.y, max.z);
            corners[2] = new Vector3(min.x, max.y, min.z);
            corners[3] = new Vector3(min.x, max.y, max.z);
            corners[4] = new Vector3(max.x, min.y, min.z);
            corners[5] = new Vector3(max.x, min.y, max.z);
            corners[6] = new Vector3(max.x, max.y, min.z);
            corners[7] = new Vector3(max.x, max.y, max.z);
        }
    }
}
