#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Commands.VNext;
using Game.Common;
using Game.DI;
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
        Vector2 _effectiveFollowPointerOffset = Vector2.zero;
        Vector2 _effectiveFollowPointerMoveScale = Vector2.one;
        Vector2 _effectiveFixedOffset = Vector2.zero;
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

        Vector2 _followPointerStartUi;
        Vector2 _followPointerBaseUi;
        bool _hasFollowPointerUi;
        Vector3 _followPointerStartWorld;
        Vector3 _followPointerBaseWorld;
        bool _hasFollowPointerWorld;

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
            _anchorActorCache = default;

            ResolveSourcePresets();
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
            _hasLastPointerPos = false;
            _anchorActorCache = default;
        }

        public void Tick(TooltipChannelInputMode inputMode, Vector2 pointerScreen, Camera? camera)
        {
            if (_dynamicContext == null)
                return;

            _conditionEnabled = _currentPlayerPreset.Condition.GetOrDefault(_dynamicContext, true);
            _autoTriggered = EvaluateAutoTrigger(inputMode, pointerScreen, camera);

            _isVisibleRequested = ResolveDesiredVisibility();
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
            var anchorWorld = _hub.UiRoot.TransformPoint(new Vector3(baseLocal.x, baseLocal.y, 0f));
            var anchorScreen = RectTransformUtility.WorldToScreenPoint(camera, anchorWorld);
            request = new TooltipPlacementRequest(
                this,
                Priority,
                _order,
                anchorScreen,
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
            var anchorScreen3 = camera.WorldToScreenPoint(baseWorld);
            request = new TooltipPlacementRequest(
                this,
                Priority,
                _order,
                new Vector2(anchorScreen3.x, anchorScreen3.y),
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

            var currentLocal = _activeRectTransform.localPosition;
            _activeRectTransform.localPosition = new Vector3(
                localPoint.x - localAnchor.x,
                localPoint.y - localAnchor.y,
                currentLocal.z);
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
            target.z = rootPos.z;
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

            var trigger = ResolveEligibleTrigger(inputMode, pointerScreen, camera);
            if (trigger == TooltipAutoTriggerKind.None)
            {
                ResetCandidate();
                return false;
            }

            if (_visibilityState == TooltipChannelVisibilityState.Active &&
                (trigger == TooltipAutoTriggerKind.Pointer || trigger == TooltipAutoTriggerKind.PointerNavigation) &&
                HasPointerMoved(pointerScreen, _effectivePointerMoveThreshold))
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
                    return _effectiveEnablePointerHover && IsPointerOver(pointerScreen, camera) && IsSelectionMatch()
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
                return false;

            if (_hub.CurrentSpaceKind == TooltipChannelSpaceKind.UIScreen)
            {
                for (var i = 0; i < hitTest.Targets.Count; i++)
                {
                    var target = hitTest.Targets[i];
                    if (target == null)
                        continue;

                    var rect = ResolveRectTransform(target);
                    if (rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, pointerScreen, camera))
                        return true;
                }

                return false;
            }

            if (camera == null || _hub.PointerService == null)
                return false;

            var z = ResolveWorldPlaneZ();
            var world2 = _hub.PointerService.PointerWorld(camera, z);
            var world = new Vector3(world2.x, world2.y, z);
            for (var i = 0; i < hitTest.Targets.Count; i++)
            {
                var target = hitTest.Targets[i];
                if (target == null)
                    continue;

                var sprite = ResolveSpriteRenderer(target);
                if (sprite != null && sprite.bounds.Contains(world))
                    return true;
            }

            return false;
        }

        bool IsSelectionMatch()
        {
            return _hub.CurrentSpaceKind == TooltipChannelSpaceKind.UIScreen &&
                   _hub.SelectionState?.CurrentElement != null &&
                   ReferenceEquals(_hub.SelectionState.CurrentElement, _owner) &&
                   IsModalAllowed();
        }

        bool IsModalAllowed()
        {
            if (_hub.CurrentSpaceKind != TooltipChannelSpaceKind.UIScreen)
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
                return;
            }

            _baseCommandsPreset = ResolveCommandsPreset(_currentPlayerPreset.CommandsPresetValue, _dynamicContext);
            _currentCommandsPreset = _baseCommandsPreset.CreateRuntimeCopy();
            _baseHitTestPreset = TooltipChannelHubService.ResolveHitTestPreset(_currentPlayerPreset.HitTestValue, _dynamicContext, new TooltipHitTestPreset());
            _currentHitTestPreset = _baseHitTestPreset.CreateRuntimeCopy();
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
                _effectiveFollowPointerOffset = _currentPlayerPreset.FollowPointerOffset;
                _effectiveFollowPointerMoveScale = _currentPlayerPreset.FollowPointerMoveScale;
                _effectiveFixedOffset = _currentPlayerPreset.FixedOffset;
                _effectiveAnchorX = _currentPlayerPreset.AnchorX;
                _effectiveAnchorY = _currentPlayerPreset.AnchorY;
            }
            else
            {
                _effectiveAnchorActorSource = placementDefaults.AnchorActorSource;
                _effectiveSpawnMode = placementDefaults.SpawnMode;
                _effectiveFollowPointerOffset = placementDefaults.FollowPointerOffset;
                _effectiveFollowPointerMoveScale = placementDefaults.FollowPointerMoveScale;
                _effectiveFixedOffset = placementDefaults.FixedOffset;
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
            _effectiveFollowPointerOffset = Vector2.zero;
            _effectiveFollowPointerMoveScale = Vector2.one;
            _effectiveFixedOffset = Vector2.zero;
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

            if (_visibilityState == TooltipChannelVisibilityState.Active || _visibilityState == TooltipChannelVisibilityState.Spawning)
                BeginClose(runHideCommands: true);
        }

        void BeginSpawn(Vector2 pointerScreen, Camera? camera)
        {
            if (_lifetimeCts == null || _dynamicContext == null || _visibilityState == TooltipChannelVisibilityState.Spawning)
                return;

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

                var spawnerKind = _hub.CurrentSpaceKind == TooltipChannelSpaceKind.UIScreen
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

                var transformParent = _hub.CurrentSpaceKind == TooltipChannelSpaceKind.UIScreen
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
                    worldSpace: _hub.CurrentSpaceKind == TooltipChannelSpaceKind.World,
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
                ResolveBoundsService(spawnedScope);
                CaptureFollowPointerReference(pointerScreen, camera);
                MoveOffscreen();

                await ExecuteCommandsAsync(spawnedScope, _currentCommandsPreset.ShowCommands, ct);
                await WaitSpawnWarmupFrames(ct);

                RefreshBounds();
                if (!IsCurrentVersion(version))
                {
                    TooltipChannelHubService.ReleaseSpawnedRuntime(spawnedScope);
                    return;
                }

                _visibilityState = TooltipChannelVisibilityState.Active;
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
                    _hasFollowPointerUi = false;
                    _hasFollowPointerWorld = false;
                    _visibilityState = TooltipChannelVisibilityState.Hidden;
                }
            }
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
                RefreshBounds();
                MoveOffscreen();
            }
        }

        void MoveOffscreen()
        {
            if (_hub.CurrentSpaceKind == TooltipChannelSpaceKind.UIScreen)
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
            var result = await runner.ExecuteListAsync(commands, context, ct, options);
            if (result.Status == CommandRunStatus.Error && !string.IsNullOrEmpty(result.Message))
                Debug.LogError($"[TooltipChannel] Command execution failed. Tag={_tag} Message={result.Message}");
        }

        void CaptureFollowPointerReference(Vector2 pointerScreen, Camera? camera)
        {
            _hasFollowPointerUi = false;
            _hasFollowPointerWorld = false;

            if (_effectiveSpawnMode != TooltipChannelSpawnMode.FollowPointer)
                return;

            if (_hub.CurrentSpaceKind == TooltipChannelSpaceKind.UIScreen)
            {
                var root = _hub.UiRoot;
                if (root == null)
                    return;

                RectTransformUtility.ScreenPointToLocalPointInRectangle(root, pointerScreen, camera, out var local);
                _followPointerStartUi = local;
                _followPointerBaseUi = local + _effectiveFollowPointerOffset;
                _hasFollowPointerUi = true;
                return;
            }

            if (_hub.PointerService == null || camera == null)
                return;

            var z = ResolveWorldPlaneZ();
            var world2 = _hub.PointerService.PointerWorld(camera, z);
            _followPointerStartWorld = new Vector3(world2.x, world2.y, z);
            _followPointerBaseWorld = _followPointerStartWorld + new Vector3(_effectiveFollowPointerOffset.x, _effectiveFollowPointerOffset.y, 0f);
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
                    return local + _effectiveFollowPointerOffset;

                var delta = local - _followPointerStartUi;
                return _followPointerBaseUi + Vector2.Scale(delta, _effectiveFollowPointerMoveScale);
            }

            var anchor = ResolveAnchorTransform() ?? _hub.UiRoot ?? _hub.WorldRoot;
            var anchorLocal = anchor != null ? (Vector2)root.InverseTransformPoint(anchor.position) : Vector2.zero;
            return anchorLocal + _effectiveFixedOffset;
        }

        Vector3 ResolveWorldBase(Vector2 pointerScreen, Camera camera)
        {
            if (_effectiveSpawnMode == TooltipChannelSpawnMode.FollowPointer && _hub.PointerService != null)
            {
                var z = ResolveWorldPlaneZ();
                var world2 = _hub.PointerService.PointerWorld(camera, z);
                var current = new Vector3(world2.x, world2.y, z);
                if (!_hasFollowPointerWorld)
                    return current + new Vector3(_effectiveFollowPointerOffset.x, _effectiveFollowPointerOffset.y, 0f);

                var delta = current - _followPointerStartWorld;
                var scaled = new Vector3(
                    delta.x * _effectiveFollowPointerMoveScale.x,
                    delta.y * _effectiveFollowPointerMoveScale.y,
                    0f);
                return _followPointerBaseWorld + scaled;
            }

            var anchor = ResolveAnchorTransform() ?? _activeTransform ?? _hub.WorldRoot;
            var pos = anchor != null ? anchor.position : Vector3.zero;
            return pos + new Vector3(_effectiveFixedOffset.x, _effectiveFixedOffset.y, 0f);
        }

        float ResolveWorldPlaneZ()
        {
            if (_activeTransform != null)
                return _activeTransform.position.z;

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

            RefreshBounds();
            var screenRect = ComputeScreenRectFromLocal(localRect, _activeRectTransform, camera);
            screenSize = SanitizeSize(screenRect.size);
            return screenSize.x > 0f && screenSize.y > 0f;
        }

        bool TryResolveWorldScreenSize(Camera camera, out Vector2 screenSize)
        {
            screenSize = Vector2.zero;
            if (_activeTransform == null)
                return false;

            RefreshBounds();
            var bounds = ResolveWorldBounds(_activeTransform.position);
            var screenRect = ComputeScreenRectFromWorld(bounds, camera);
            screenSize = SanitizeSize(screenRect.size);
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

        void RefreshBounds()
        {
            if (_boundsService == null)
                return;

            _boundsService.RebuildNow();
            _boundsOutput = _boundsService as IVisualBoundsOutput;
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
