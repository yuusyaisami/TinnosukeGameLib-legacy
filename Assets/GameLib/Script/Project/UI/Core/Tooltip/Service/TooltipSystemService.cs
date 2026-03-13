#nullable enable
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using System;
using VContainer.Unity;
using Game.Commands.VNext;
using Game.Common;
using VContainer;
using Game.Input;
using Game.Spawn;
using Game.TransformSystem;
using Game.Vars.Generated;

namespace Game.UI
{
    public sealed class TooltipSystemConfig
    {
        public RectTransform TooltipRoot = null!;
        public Transform? WorldRoot;
        public RectTransform? ClampArea;
        public Camera? UiCamera;
        public Camera? WorldCamera;
        public TooltipInputMode InputMode = TooltipInputMode.AutoByInputService;
        public TooltipClampSettings ClampSettings = TooltipClampSettings.Default;
        public bool RunInLateUpdate = true;
        public int SpawnWarmupFrames = 2;
    }

    public sealed class TooltipSystemService :
        ITooltipSystemService,
        ITickable,
        ILateTickable,
        ITickPhase,
        IScopeAcquireHandler,
        IScopeReleaseHandler
    {
        const float UiHiddenLocalPosition = 100000f;
        const float WorldHiddenPosition = 100000f;

        enum TooltipState
        {
            Idle,
            Spawning,
            Active,
            Closing,
        }

        enum TriggerMode
        {
            None,
            Pointer,
            Navigation,
            PointerNavigation,
        }

        readonly TooltipSystemConfig _config;
        readonly IScreenClampService _clampService;
        readonly List<ITooltipAdapter> _adapters = new List<ITooltipAdapter>(32);

        IUIInputService? _uiInputService;
        IPointerService? _pointerService;
        IUISelectionState? _selectionState;
        IUIModalStackService? _modalStackService;
        ISceneSpawnerRegistry? _spawnerRegistry;

        TooltipState _state = TooltipState.Idle;
        TooltipInputMode _lastInputMode = TooltipInputMode.AutoByInputService;

        ITooltipAdapter? _activeAdapter;
        IScopeNode? _activeScope;
        Transform? _activeTransform;
        RectTransform? _activeRectTransform;
        IVisualBoundsService? _activeBoundsService;
        IVisualBoundsOutput? _activeBoundsOutput;
        TooltipAnchorX _activeAnchorX;
        TooltipAnchorY _activeAnchorY;

        ITooltipAdapter? _candidateAdapter;
        TriggerMode _candidateTrigger;
        float _candidateStartTime;
        Vector2 _candidateStartPointer;

        Vector2 _lastPointerPos;
        bool _hasLastPointerPos;

        CancellationTokenSource? _lifetimeCts;

        public TooltipSystemService(TooltipSystemConfig config, IScreenClampService clampService)
        {
            _config = config;
            _clampService = clampService;
        }

        public TickPhase Phase => _config != null && _config.RunInLateUpdate ? TickPhase.Late : TickPhase.Default;

        public ITooltipAdapter? ActiveAdapter => _activeAdapter;

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            var resolver = scope?.Resolver;
            if (resolver != null)
            {
                resolver.TryResolve(out _uiInputService);
                resolver.TryResolve(out _pointerService);
                resolver.TryResolve(out _selectionState);
                resolver.TryResolve(out _modalStackService);
                resolver.TryResolve(out _spawnerRegistry);
            }

            _lifetimeCts = new CancellationTokenSource();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _lifetimeCts?.Cancel();
            _lifetimeCts?.Dispose();
            _lifetimeCts = null;

            _state = TooltipState.Idle;
            _activeAdapter = null;
            _activeScope = null;
            _activeTransform = null;
            _activeRectTransform = null;
            _activeBoundsService = null;
            _activeBoundsOutput = null;
            _candidateAdapter = null;
            _candidateTrigger = TriggerMode.None;
            _hasLastPointerPos = false;
        }

        public void RegisterAdapter(ITooltipAdapter adapter)
        {
            if (adapter == null)
                return;

            if (!_adapters.Contains(adapter))
                _adapters.Add(adapter);
        }

        public void UnregisterAdapter(ITooltipAdapter adapter)
        {
            if (adapter == null)
                return;

            _adapters.Remove(adapter);
            if (ReferenceEquals(_activeAdapter, adapter))
            {
                BeginClose();
            }
        }

        public void Tick()
        {
            TickInternal();
        }

        public void LateTick()
        {
            TickInternal();
        }

        void TickInternal()
        {
            if (_config == null || _config.TooltipRoot == null)
                return;

            if (_adapters.Count == 0)
                return;

            var mode = ResolveInputMode();
            if (mode != _lastInputMode)
            {
                ResetCandidate();
                if (_state == TooltipState.Active)
                    BeginClose();
                _lastInputMode = mode;
            }

            var pointerScreen = _pointerService != null ? _pointerService.PointerScreen() : Vector2.zero;

            if (_state == TooltipState.Active)
            {
                if (_activeAdapter == null)
                {
                    _state = TooltipState.Idle;
                    return;
                }

                if (!IsAdapterEligible(_activeAdapter, mode, pointerScreen))
                {
                    BeginClose();
                    return;
                }

                if (mode == TooltipInputMode.Pointer || mode == TooltipInputMode.PointerNavigation)
                {
                    if (HasPointerMoved(pointerScreen, _activeAdapter.PointerMoveThreshold))
                    {
                        BeginClose();
                        return;
                    }
                }

                UpdateActivePlacement(pointerScreen);
                return;
            }

            if (_state == TooltipState.Spawning || _state == TooltipState.Closing)
                return;

            var desired = EvaluateDesiredAdapter(mode, pointerScreen);
            if (desired == null)
                return;

            BeginSpawn(desired, pointerScreen);
        }

        TooltipInputMode ResolveInputMode()
        {
            if (_config == null)
                return TooltipInputMode.Pointer;

            var mode = _config.InputMode;
            if (mode != TooltipInputMode.AutoByInputService)
                return mode;

            if (_uiInputService == null)
                return TooltipInputMode.Pointer;

            if (_uiInputService.IsPointerModeActive)
                return TooltipInputMode.Pointer;

            if (_uiInputService.IsNavigationModeActive)
                return TooltipInputMode.Navigation;

            return TooltipInputMode.Pointer;
        }

        ITooltipAdapter? EvaluateDesiredAdapter(TooltipInputMode mode, Vector2 pointerScreen)
        {
            ITooltipAdapter? candidate = null;
            var trigger = TriggerMode.None;

            switch (mode)
            {
                case TooltipInputMode.Pointer:
                    candidate = FindPointerAdapter(pointerScreen, requireSelection: false);
                    trigger = TriggerMode.Pointer;
                    break;
                case TooltipInputMode.PointerNavigation:
                    candidate = FindPointerAdapter(pointerScreen, requireSelection: true);
                    trigger = TriggerMode.PointerNavigation;
                    break;
                case TooltipInputMode.Navigation:
                    candidate = FindSelectionAdapter();
                    trigger = TriggerMode.Navigation;
                    break;
            }

            UpdateCandidate(candidate, trigger, pointerScreen);

            if (_candidateAdapter == null)
                return null;

            float delay = trigger == TriggerMode.Navigation
                ? Mathf.Max(0f, _candidateAdapter.SelectionDelaySeconds)
                : Mathf.Max(0f, _candidateAdapter.HoverDelaySeconds);

            var elapsed = Time.unscaledTime - _candidateStartTime;
            if (elapsed < delay)
                return null;

            return _candidateAdapter;
        }

        void UpdateCandidate(ITooltipAdapter? candidate, TriggerMode trigger, Vector2 pointerScreen)
        {
            if (candidate == null)
            {
                ResetCandidate();
                return;
            }

            if (!ReferenceEquals(candidate, _candidateAdapter) || trigger != _candidateTrigger)
            {
                _candidateAdapter = candidate;
                _candidateTrigger = trigger;
                _candidateStartTime = Time.unscaledTime;
                _candidateStartPointer = pointerScreen;
                return;
            }

            if (trigger == TriggerMode.Pointer || trigger == TriggerMode.PointerNavigation)
            {
                var threshold = Mathf.Max(0f, candidate.PointerMoveThreshold);
                var sq = threshold * threshold;
                if ((pointerScreen - _candidateStartPointer).sqrMagnitude > sq)
                {
                    _candidateStartTime = Time.unscaledTime;
                    _candidateStartPointer = pointerScreen;
                }
            }
        }

        void ResetCandidate()
        {
            _candidateAdapter = null;
            _candidateTrigger = TriggerMode.None;
            _candidateStartTime = 0f;
            _candidateStartPointer = Vector2.zero;
        }

        ITooltipAdapter? FindSelectionAdapter()
        {
            var current = _selectionState?.CurrentElement;
            if (current == null)
                return null;

            var adapter = FindAdapterByOwner(current, uiOnly: true);
            if (adapter == null)
                return null;

            if (!adapter.EnableSelectionHover)
                return null;

            if (!IsModalAllowed(adapter))
                return null;

            return adapter;
        }

        ITooltipAdapter? FindPointerAdapter(Vector2 pointerScreen, bool requireSelection)
        {
            if (_pointerService == null)
                return null;

            var hovered = _selectionState?.HoveredElement;
            var current = _selectionState?.CurrentElement;

            if (hovered != null)
            {
                var hoveredAdapter = FindAdapterByOwner(hovered, uiOnly: true);
                if (hoveredAdapter != null)
                {
                    if (!hoveredAdapter.EnablePointerHover)
                        return null;

                    if (!IsModalAllowed(hoveredAdapter))
                        return null;

                    if (requireSelection && !IsSelectionMatch(hoveredAdapter, current))
                        return null;

                    return hoveredAdapter;
                }
            }

            ITooltipAdapter? best = null;
            var bestPriority = int.MinValue;

            for (int i = 0; i < _adapters.Count; i++)
            {
                var adapter = _adapters[i];
                if (adapter == null)
                    continue;

                if (!adapter.EnablePointerHover)
                    continue;

                if (adapter.Kind == TooltipAdapterKind.UIScreen && !IsModalAllowed(adapter))
                    continue;

                if (requireSelection && adapter.Kind == TooltipAdapterKind.UIScreen && !IsSelectionMatch(adapter, current))
                    continue;

                if (!IsPointerOver(adapter, pointerScreen))
                    continue;

                if (adapter.Priority >= bestPriority)
                {
                    best = adapter;
                    bestPriority = adapter.Priority;
                }
            }

            return best;
        }

        ITooltipAdapter? FindAdapterByOwner(IScopeNode owner, bool uiOnly)
        {
            ITooltipAdapter? best = null;
            var bestPriority = int.MinValue;

            for (int i = 0; i < _adapters.Count; i++)
            {
                var adapter = _adapters[i];
                if (adapter == null)
                    continue;
                if (!ReferenceEquals(adapter.Owner, owner))
                    continue;
                if (uiOnly && adapter.Kind != TooltipAdapterKind.UIScreen)
                    continue;

                if (adapter.Priority >= bestPriority)
                {
                    best = adapter;
                    bestPriority = adapter.Priority;
                }
            }

            return best;
        }

        bool IsPointerOver(ITooltipAdapter adapter, Vector2 pointerScreen)
        {
            var hitRects = adapter.HitRects;
            if (hitRects != null && hitRects.Count > 0)
            {
                var cam = ResolveUiCamera(adapter);
                for (int i = 0; i < hitRects.Count; i++)
                {
                    var rt = hitRects[i];
                    if (rt == null)
                        continue;
                    if (RectTransformUtility.RectangleContainsScreenPoint(rt, pointerScreen, cam))
                        return true;
                }
            }

            var sprites = adapter.HitSprites;
            if (sprites != null && sprites.Count > 0)
            {
                var cam = ResolveWorldCamera(adapter);
                if (cam == null || _pointerService == null)
                    return false;

                var z = ResolveWorldPlaneZ(adapter);
                var world = _pointerService.PointerWorld(cam, z);
                for (int i = 0; i < sprites.Count; i++)
                {
                    var sr = sprites[i];
                    if (sr == null)
                        continue;
                    if (sr.bounds.Contains(world))
                        return true;
                }
            }

            return false;
        }

        bool IsSelectionMatch(ITooltipAdapter adapter, IScopeNode? current)
        {
            if (current == null)
                return false;
            return ReferenceEquals(adapter.Owner, current);
        }

        bool IsModalAllowed(ITooltipAdapter adapter)
        {
            if (_modalStackService == null)
                return true;

            return _modalStackService.IsInAnyInputRoot(adapter.Owner);
        }

        bool IsAdapterEligible(ITooltipAdapter adapter, TooltipInputMode mode, Vector2 pointerScreen)
        {
            switch (mode)
            {
                case TooltipInputMode.Pointer:
                    if (!adapter.EnablePointerHover)
                        return false;
                    if (adapter.Kind == TooltipAdapterKind.UIScreen && !IsModalAllowed(adapter))
                        return false;
                    return IsPointerOver(adapter, pointerScreen);
                case TooltipInputMode.PointerNavigation:
                    if (!adapter.EnablePointerHover)
                        return false;
                    if (adapter.Kind == TooltipAdapterKind.UIScreen && !IsModalAllowed(adapter))
                        return false;
                    if (adapter.Kind == TooltipAdapterKind.UIScreen && !IsSelectionMatch(adapter, _selectionState?.CurrentElement))
                        return false;
                    return IsPointerOver(adapter, pointerScreen);
                case TooltipInputMode.Navigation:
                    if (!adapter.EnableSelectionHover)
                        return false;
                    if (adapter.Kind == TooltipAdapterKind.UIScreen && !IsModalAllowed(adapter))
                        return false;
                    return IsSelectionMatch(adapter, _selectionState?.CurrentElement);
            }

            return false;
        }

        bool HasPointerMoved(Vector2 pointerScreen, float threshold)
        {
            if (!_hasLastPointerPos)
            {
                _hasLastPointerPos = true;
                _lastPointerPos = pointerScreen;
                return false;
            }

            var sq = threshold * threshold;
            var moved = (pointerScreen - _lastPointerPos).sqrMagnitude > sq;
            _lastPointerPos = pointerScreen;
            return moved;
        }

        void BeginSpawn(ITooltipAdapter adapter, Vector2 pointerScreen)
        {
            if (_state != TooltipState.Idle)
                return;

            _state = TooltipState.Spawning;

            var ct = _lifetimeCts != null ? _lifetimeCts.Token : CancellationToken.None;
            UniTask.Void(async () => await SpawnAsync(adapter, pointerScreen, ct));
        }

        async UniTask SpawnAsync(ITooltipAdapter adapter, Vector2 pointerScreen, CancellationToken ct)
        {
            try
            {
                var spawnedScope = await SpawnRuntimeAsync(adapter, ct);
                if (spawnedScope == null)
                {
                    _state = TooltipState.Idle;
                    return;
                }

                _activeAdapter = adapter;
                _activeScope = spawnedScope;
                _activeTransform = GetTransformFromScope(spawnedScope);
                _activeRectTransform = _activeTransform as RectTransform;
                _activeBoundsService = ResolveBoundsService(spawnedScope, out _activeBoundsOutput);
                _activeAnchorX = adapter.AnchorX;
                _activeAnchorY = adapter.AnchorY;
                _hasLastPointerPos = false;

                // 初期フレームは画面外に退避し、Text/Layout 更新途中の崩れを見せない。
                MoveActiveOffScreen();

                await ExecuteShowCommands(adapter, spawnedScope, ct);
                await WaitSpawnWarmupFrames(ct);

                if (_activeBoundsService != null)
                {
                    _activeBoundsService.RebuildNow();
                    _activeBoundsOutput = _activeBoundsService as IVisualBoundsOutput;
                }

                if (_activeBoundsOutput == null || !_activeBoundsOutput.HasBounds)
                {
                    ApplyPlacementWithoutBounds(pointerScreen);
                    _state = TooltipState.Active;
                    return;
                }

                ApplyPlacement(pointerScreen, _activeAnchorX, _activeAnchorY, allowFlip: true);
                _state = TooltipState.Active;
            }
            catch (OperationCanceledException)
            {
                _state = TooltipState.Idle;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[TooltipSystem] Spawn failed: {ex.Message}");
                _state = TooltipState.Idle;
            }
        }

        async UniTask WaitSpawnWarmupFrames(CancellationToken ct)
        {
            var frames = Mathf.Max(0, _config.SpawnWarmupFrames);
            if (frames <= 0)
                return;

            for (int i = 0; i < frames; i++)
            {
                ct.ThrowIfCancellationRequested();
                await UniTask.NextFrame(ct);
                _activeBoundsService?.RebuildNow();
                _activeBoundsOutput = _activeBoundsService as IVisualBoundsOutput;
                MoveActiveOffScreen();
            }
        }

        async UniTask<IScopeNode?> SpawnRuntimeAsync(ITooltipAdapter adapter, CancellationToken ct)
        {
            var dynamicContext = new SimpleDynamicContext(NullVarStore.Instance, adapter.Owner);
            if (!adapter.TryResolveRuntimeTemplate(dynamicContext, out var runtimeTemplate) || runtimeTemplate == null)
            {
                Debug.LogError("[TooltipSystem] RuntimeTemplate is null.");
                return null;
            }

            if (_spawnerRegistry == null)
                return null;

            var kind = adapter.Kind == TooltipAdapterKind.UIScreen
                ? SpawnerKind.RuntimeUIElement
                : SpawnerKind.RuntimeEntity;

            var allowTagFallback = string.IsNullOrEmpty(adapter.SpawnerTag);
            var resolved = SceneSpawnerResolver.TryResolveAsyncSpawner(
                _spawnerRegistry,
                kind,
                adapter.SpawnerTag,
                allowTagFallback,
                allowRuntimeUiFallback: true);

            if (resolved.Spawner == null)
                return null;

            var transformParent = ResolveTransformParent(adapter);
            var spawnParams = SpawnParams.ForRuntime(
                runtimeTemplate,
                Vector3.zero,
                Quaternion.identity,
                Vector3.one,
                identity: null,
                transformParent: transformParent,
                lifetimeScopeParent: adapter.Owner,
                worldSpace: adapter.Kind != TooltipAdapterKind.UIScreen,
                allowPooling: runtimeTemplate.UsePooling);

            var resolver = await resolved.Spawner.SpawnAsync(spawnParams, ct);
            if (resolver == null)
                return null;

            if (!resolver.TryResolve<IScopeNode>(out var scope) || scope == null)
                return null;

            EnsureScopeBuiltIfNeeded(scope);
            return scope;
        }

        Transform? ResolveTransformParent(ITooltipAdapter adapter)
        {
            if (adapter.Kind == TooltipAdapterKind.UIScreen)
                return _config.TooltipRoot;

            if (_config.WorldRoot != null)
                return _config.WorldRoot;

            return adapter.AnchorTransform;
        }

        async UniTask ExecuteShowCommands(ITooltipAdapter adapter, IScopeNode scope, CancellationToken ct)
        {
            if (adapter.ShowCommands == null || adapter.ShowCommands.Count == 0)
                return;

            if (!TryResolveRunner(scope, out var runner) || runner == null)
                return;

            var vars = ResolveVars(scope);
            // Spawned RuntimeLTS 内のチャンネルを優先解決するため、actor は Owner ではなく実行 scope を使う。
            var ctx = new CommandContext(scope, vars, runner, actor: scope, options: CommandRunOptions.Default);

            try
            {
                var result = await runner.ExecuteListAsync(adapter.ShowCommands, ctx, ct, ctx.Options);
                if (result.Status == CommandRunStatus.Error && !string.IsNullOrEmpty(result.Message))
                    Debug.LogError($"[TooltipSystem] ShowCommands failed: {result.Message}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[TooltipSystem] ShowCommands exception: {ex.Message}");
            }
        }

        void BeginClose()
        {
            if (_state == TooltipState.Closing)
                return;

            if (_activeAdapter == null || _activeScope == null)
            {
                _state = TooltipState.Idle;
                _activeAdapter = null;
                _activeScope = null;
                return;
            }

            _state = TooltipState.Closing;
            var ct = _lifetimeCts != null ? _lifetimeCts.Token : CancellationToken.None;
            UniTask.Void(async () => await CloseAsync(ct));
        }

        async UniTask CloseAsync(CancellationToken ct)
        {
            try
            {
                if (_activeAdapter != null && _activeScope != null)
                    await ExecuteHideCommands(_activeAdapter, _activeScope, ct);
            }
            catch (OperationCanceledException)
            {
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[TooltipSystem] HideCommands exception: {ex.Message}");
            }
            finally
            {
                _activeAdapter = null;
                _activeScope = null;
                _activeTransform = null;
                _activeRectTransform = null;
                _activeBoundsService = null;
                _activeBoundsOutput = null;
                _state = TooltipState.Idle;
            }
        }

        async UniTask ExecuteHideCommands(ITooltipAdapter adapter, IScopeNode scope, CancellationToken ct)
        {
            if (!TryResolveRunner(scope, out var runner) || runner == null)
                return;

            var list = BuildHideList(adapter);
            if (list.Count == 0)
                return;

            var vars = ResolveVars(scope);
            // Hide も同様に、生成済み Tooltip RuntimeLTS のローカルチャンネルを起点に実行する。
            var ctx = new CommandContext(scope, vars, runner, actor: scope, options: CommandRunOptions.Default);

            var result = await runner.ExecuteListAsync(list, ctx, ct, ctx.Options);
            if (result.Status == CommandRunStatus.Error && !string.IsNullOrEmpty(result.Message))
                Debug.LogError($"[TooltipSystem] HideCommands failed: {result.Message}");
        }

        static CommandListData BuildHideList(ITooltipAdapter adapter)
        {
            var list = new CommandListData();
            if (adapter.HideCommands != null && adapter.HideCommands.Commands != null)
            {
                foreach (var cmd in adapter.HideCommands.Commands)
                {
                    if (cmd != null)
                        list.Add(cmd);
                }
            }

            list.Add(adapter.SelfDespawn);
            return list;
        }

        void UpdateActivePlacement(Vector2 pointerScreen)
        {
            if (_activeBoundsOutput == null)
                return;

            if (_activeAdapter == null)
                return;

            if (_activeTransform == null)
                return;

            ApplyPlacement(pointerScreen, _activeAnchorX, _activeAnchorY, allowFlip: false);
        }

        void ApplyPlacementWithoutBounds(Vector2 pointerScreen)
        {
            if (_activeAdapter == null || _activeTransform == null)
                return;

            if (_activeAdapter.Kind == TooltipAdapterKind.UIScreen && _activeRectTransform != null)
            {
                var root = _config.TooltipRoot;
                var baseLocal = ResolveUiBaseLocal(_activeAdapter, pointerScreen, root);
                var lp = _activeRectTransform.localPosition;
                _activeRectTransform.localPosition = new Vector3(baseLocal.x, baseLocal.y, lp.z);
                return;
            }

            var baseWorld = ResolveWorldBase(_activeAdapter);
            _activeTransform.position = baseWorld;
        }

        void MoveActiveOffScreen()
        {
            if (_activeTransform == null)
                return;

            if (_activeAdapter != null && _activeAdapter.Kind == TooltipAdapterKind.UIScreen && _activeRectTransform != null)
            {
                var local = _activeRectTransform.localPosition;
                _activeRectTransform.localPosition = new Vector3(UiHiddenLocalPosition, UiHiddenLocalPosition, local.z);
                return;
            }

            var pos = _activeTransform.position;
            _activeTransform.position = new Vector3(WorldHiddenPosition, WorldHiddenPosition, pos.z);
        }

        void ApplyPlacement(Vector2 pointerScreen, TooltipAnchorX anchorX, TooltipAnchorY anchorY, bool allowFlip)
        {
            if (_activeAdapter == null || _activeTransform == null || _activeBoundsOutput == null)
                return;

            var clamp = ComputePlacement(_activeAdapter, _activeBoundsOutput, pointerScreen, anchorX, anchorY);
            if (allowFlip && _config.ClampSettings.EnableClamp)
            {
                var nextAnchorX = anchorX;
                var nextAnchorY = anchorY;

                if (clamp.RightRate > _config.ClampSettings.FlipThresholdX)
                    nextAnchorX = TooltipAnchorX.Left;
                else if (clamp.LeftRate > _config.ClampSettings.FlipThresholdX)
                    nextAnchorX = TooltipAnchorX.Right;

                if (clamp.TopRate > _config.ClampSettings.FlipThresholdY)
                    nextAnchorY = TooltipAnchorY.Down;
                else if (clamp.BottomRate > _config.ClampSettings.FlipThresholdY)
                    nextAnchorY = TooltipAnchorY.Up;

                if (nextAnchorX != anchorX || nextAnchorY != anchorY)
                {
                    anchorX = nextAnchorX;
                    anchorY = nextAnchorY;
                    clamp = ComputePlacement(_activeAdapter, _activeBoundsOutput, pointerScreen, anchorX, anchorY);
                }
            }

            _activeAnchorX = anchorX;
            _activeAnchorY = anchorY;
            _activeBoundsService?.SetClampResult(clamp);
        }

        ScreenClampResult ComputePlacement(ITooltipAdapter adapter, IVisualBoundsOutput bounds, Vector2 pointerScreen, TooltipAnchorX anchorX, TooltipAnchorY anchorY)
        {
            if (adapter.Kind == TooltipAdapterKind.UIScreen)
            {
                return ComputeUiPlacement(adapter, bounds, pointerScreen, anchorX, anchorY);
            }

            return ComputeWorldPlacement(adapter, bounds, pointerScreen, anchorX, anchorY);
        }

        ScreenClampResult ComputeUiPlacement(ITooltipAdapter adapter, IVisualBoundsOutput bounds, Vector2 pointerScreen, TooltipAnchorX anchorX, TooltipAnchorY anchorY)
        {
            if (_activeRectTransform == null)
                return ScreenClampResult.Empty;

            var root = _config.TooltipRoot;
            var baseLocal = ResolveUiBaseLocal(adapter, pointerScreen, root);
            var localPos = baseLocal;
            if (bounds.HasBounds)
            {
                var anchorPoint = ResolveAnchorPoint(bounds.LocalRect, anchorX, anchorY);
                localPos = baseLocal - anchorPoint;
            }

            var lp = _activeRectTransform.localPosition;
            _activeRectTransform.localPosition = new Vector3(localPos.x, localPos.y, lp.z);

            if (!_config.ClampSettings.EnableClamp || !bounds.HasBounds)
                return ScreenClampResult.Empty;

            var cam = ResolveUiCamera(adapter);
            var tooltipRect = ComputeScreenRectFromLocal(bounds.LocalRect, _activeRectTransform, cam);
            var screenRect = ResolveClampScreenRect(cam);
            return _clampService.Evaluate(screenRect, tooltipRect);
        }

        ScreenClampResult ComputeWorldPlacement(ITooltipAdapter adapter, IVisualBoundsOutput bounds, Vector2 pointerScreen, TooltipAnchorX anchorX, TooltipAnchorY anchorY)
        {
            if (_activeTransform == null)
                return ScreenClampResult.Empty;

            var baseWorld = ResolveWorldBase(adapter);
            var rootPos = _activeTransform.position;
            var targetPos = baseWorld;
            if (bounds.HasBounds)
            {
                var anchorWorld = ResolveWorldAnchorPoint(bounds.WorldBounds, rootPos, anchorX, anchorY);
                var offset = anchorWorld - rootPos;
                targetPos = baseWorld - offset;
            }
            _activeTransform.position = targetPos;

            if (!_config.ClampSettings.EnableClamp || !bounds.HasBounds)
                return ScreenClampResult.Empty;

            var cam = ResolveWorldCamera(adapter);
            if (cam == null)
                return ScreenClampResult.Empty;

            var delta = targetPos - rootPos;
            var shifted = bounds.WorldBounds;
            shifted.center += delta;
            var tooltipRect = ComputeScreenRectFromWorld(shifted, cam);
            var screenRect = new Rect(0f, 0f, Screen.width, Screen.height);
            return _clampService.Evaluate(screenRect, tooltipRect);
        }

        Vector2 ResolveUiBaseLocal(ITooltipAdapter adapter, Vector2 pointerScreen, RectTransform root)
        {
            if (adapter.SpawnMode == TooltipSpawnMode.FollowPointer)
            {
                var cam = ResolveUiCamera(adapter);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(root, pointerScreen, cam, out var local);
                return local;
            }

            var anchor = adapter.AnchorTransform != null ? adapter.AnchorTransform : root.transform;
            var anchorLocal = (Vector2)root.InverseTransformPoint(anchor.position);
            return anchorLocal + adapter.FixedOffset;
        }

        Vector3 ResolveWorldBase(ITooltipAdapter adapter)
        {
            if (adapter.SpawnMode == TooltipSpawnMode.FollowPointer)
            {
                var cam = ResolveWorldCamera(adapter);
                if (cam != null && _pointerService != null)
                {
                    var z = ResolveWorldPlaneZ(adapter);
                    var world = _pointerService.PointerWorld(cam, z);
                    return new Vector3(world.x, world.y, z);
                }
            }

            var anchor = adapter.AnchorTransform != null ? adapter.AnchorTransform : _activeTransform;
            var pos = anchor != null ? anchor.position : Vector3.zero;
            return pos + new Vector3(adapter.FixedOffset.x, adapter.FixedOffset.y, 0f);
        }

        static Vector2 ResolveAnchorPoint(Rect localRect, TooltipAnchorX anchorX, TooltipAnchorY anchorY)
        {
            float x = anchorX switch
            {
                TooltipAnchorX.Left => localRect.max.x,
                TooltipAnchorX.Center => localRect.center.x,
                TooltipAnchorX.Right => localRect.min.x,
                _ => localRect.center.x
            };

            float y = anchorY switch
            {
                TooltipAnchorY.Up => localRect.min.y,
                TooltipAnchorY.Center => localRect.center.y,
                TooltipAnchorY.Down => localRect.max.y,
                _ => localRect.center.y
            };

            return new Vector2(x, y);
        }

        static Vector3 ResolveWorldAnchorPoint(in Bounds bounds, Vector3 rootPos, TooltipAnchorX anchorX, TooltipAnchorY anchorY)
        {
            float x = anchorX switch
            {
                TooltipAnchorX.Left => bounds.max.x,
                TooltipAnchorX.Center => bounds.center.x,
                TooltipAnchorX.Right => bounds.min.x,
                _ => bounds.center.x
            };

            float y = anchorY switch
            {
                TooltipAnchorY.Up => bounds.min.y,
                TooltipAnchorY.Center => bounds.center.y,
                TooltipAnchorY.Down => bounds.max.y,
                _ => bounds.center.y
            };

            return new Vector3(x, y, rootPos.z);
        }

        Rect ResolveClampScreenRect(Camera? cam)
        {
            if (_config.ClampArea == null)
                return new Rect(0f, 0f, Screen.width, Screen.height);

            var rt = _config.ClampArea;
            rt.GetWorldCorners(_rectCorners);
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < 4; i++)
            {
                var s = RectTransformUtility.WorldToScreenPoint(cam, _rectCorners[i]);
                min = Vector2.Min(min, s);
                max = Vector2.Max(max, s);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        Rect ComputeScreenRectFromLocal(Rect localRect, Transform root, Camera? cam)
        {
            var w0 = root.TransformPoint(new Vector3(localRect.min.x, localRect.min.y, 0f));
            var w1 = root.TransformPoint(new Vector3(localRect.min.x, localRect.max.y, 0f));
            var w2 = root.TransformPoint(new Vector3(localRect.max.x, localRect.max.y, 0f));
            var w3 = root.TransformPoint(new Vector3(localRect.max.x, localRect.min.y, 0f));

            var s0 = RectTransformUtility.WorldToScreenPoint(cam, w0);
            var s1 = RectTransformUtility.WorldToScreenPoint(cam, w1);
            var s2 = RectTransformUtility.WorldToScreenPoint(cam, w2);
            var s3 = RectTransformUtility.WorldToScreenPoint(cam, w3);

            var min = Vector2.Min(Vector2.Min(s0, s1), Vector2.Min(s2, s3));
            var max = Vector2.Max(Vector2.Max(s0, s1), Vector2.Max(s2, s3));
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        Rect ComputeScreenRectFromWorld(in Bounds bounds, Camera cam)
        {
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

            FillBoundsCorners(bounds, _rectCorners8);
            for (int i = 0; i < _rectCorners8.Length; i++)
            {
                var s = cam.WorldToScreenPoint(_rectCorners8[i]);
                min = Vector2.Min(min, s);
                max = Vector2.Max(max, s);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        bool TryResolveRunner(IScopeNode scope, out ICommandRunner? runner)
        {
            runner = null;
            var resolver = scope?.Resolver;
            if (resolver == null)
                return false;

            return resolver.TryResolve(out runner) && runner != null;
        }

        static IVarStore ResolveVars(IScopeNode scope)
        {
            var resolver = scope?.Resolver;
            if (resolver != null && resolver.TryResolve<IVarStore>(out var vars) && vars != null)
                return vars;
            return NullVarStore.Instance;
        }

        IVisualBoundsService? ResolveBoundsService(IScopeNode scope, out IVisualBoundsOutput? output)
        {
            output = null;
            var resolver = scope?.Resolver;
            if (resolver == null)
                return null;

            if (resolver.TryResolve<IVisualBoundsService>(out var service) && service != null)
            {
                output = service as IVisualBoundsOutput;
                return service;
            }

            return null;
        }

        static void EnsureScopeBuiltIfNeeded(IScopeNode scope)
        {
            if (scope is BaseLifetimeScope baseScope)
            {
                baseScope.EnsureScopeBuilt();
                return;
            }

            if (scope is RuntimeLifetimeScope runtimeScope)
            {
                runtimeScope.EnsureScopeBuilt();
            }
        }

        static Transform? GetTransformFromScope(IScopeNode scope)
        {
            if (scope == null)
                return null;

            var id = scope.Identity;
            if (id != null && id.SelfTransform != null)
                return id.SelfTransform;

            if (scope is Component comp)
                return comp.transform;

            return null;
        }

        Camera? ResolveUiCamera(ITooltipAdapter adapter)
        {
            if (adapter.UiCamera != null)
                return adapter.UiCamera;

            if (_config.UiCamera != null)
                return _config.UiCamera;

            var canvas = _config.TooltipRoot != null
                ? _config.TooltipRoot.GetComponentInParent<Canvas>()
                : null;
            return canvas != null ? canvas.worldCamera : null;
        }

        Camera? ResolveWorldCamera(ITooltipAdapter adapter)
        {
            if (adapter.WorldCamera != null)
                return adapter.WorldCamera;

            if (_config.WorldCamera != null)
                return _config.WorldCamera;

            return Camera.main;
        }

        float ResolveWorldPlaneZ(ITooltipAdapter adapter)
        {
            var anchor = adapter.AnchorTransform;
            if (anchor != null)
                return anchor.position.z;
            return 0f;
        }

        static void FillBoundsCorners(in Bounds bounds, Vector3[] corners)
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

        readonly Vector3[] _rectCorners = new Vector3[4];
        readonly Vector3[] _rectCorners8 = new Vector3[8];
    }
}
