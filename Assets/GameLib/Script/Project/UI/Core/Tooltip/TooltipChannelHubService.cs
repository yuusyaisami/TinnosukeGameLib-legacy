#nullable enable
using System;
using System.Collections.Generic;
using Game;
using Game.CameraSystem;
using Game.Common;
using Game.Input;
using Game.SelectRuntime;
using Game.Spawn;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.UI
{
    public sealed class TooltipChannelHubService :
        ITooltipChannelHubService,
        IScopeAcquireHandler,
        IScopeReleaseHandler,
        IScopeTickHandler
    {
        sealed class ChannelEntry
        {
            public string Tag = "default";
            public int Order;
            public TooltipChannelOptions Options = null!;
            public TooltipChannelPlayerRuntime Runtime = null!;
        }

        readonly IScopeNode _owner;
        readonly TooltipChannelHubMB _mb;
        readonly Dictionary<string, ChannelEntry> _channels = new(StringComparer.Ordinal);
        readonly List<ChannelEntry> _orderedChannels = new();
        readonly List<TooltipPlacementRequest> _requests = new();
        readonly List<TooltipPlacementSolution> _solutions = new();
        readonly Vector3[] _rectCorners = new Vector3[4];

        TooltipHubPreset _baseHubPreset = new();
        TooltipHubPreset _currentHubPreset = new();
        TooltipHitTestPreset _baseDefaultHitTest = TooltipHitTestPreset.CreateOwnerDefault();
        TooltipHitTestPreset _currentDefaultHitTest = TooltipHitTestPreset.CreateOwnerDefault();

        IScopeNode? _activeScope;
        IUIInputService? _uiInputService;
        IPointerService? _pointerService;
        IWorldPointerRuntimeService? _worldPointerService;
        IUISelectionState? _selectionState;
        IModalStackChannelHubService? _modalStackHub;
        ISceneSpawnerRegistry? _spawnerRegistry;
        ICameraLocationChannelService? _cameraLocationService;
        ITooltipSystemService? _tooltipSystemService;
        IScreenClampService _screenClampService = new ScreenClampService();
        ITooltipPlacementSolver? _solver;
        RectTransform? _uiRoot;
        Transform? _worldRoot;
        RectTransform? _clampArea;
        Canvas? _uiRootCanvas;
        Camera? _fallbackCameraCache;
        Rect _cachedClampRect;
        Camera? _cachedClampCamera;
        int _cachedClampScreenWidth;
        int _cachedClampScreenHeight;
        bool _hasCachedClampRect;
        TooltipChannelSpaceKind _spaceKind = TooltipChannelSpaceKind.Unknown;
        TooltipChannelRenderSpaceKind _renderSpaceKind = TooltipChannelRenderSpaceKind.Auto;
        int _nextDynamicOrder;
        bool _isAcquired;

        public TooltipChannelHubService(IScopeNode owner, TooltipChannelHubMB mb)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _mb = mb ?? throw new ArgumentNullException(nameof(mb));
        }

        public int ChannelCount => _orderedChannels.Count;
        public TooltipChannelSpaceKind SpaceKind => _spaceKind;

        internal TooltipHubPreset CurrentHubPreset => _currentHubPreset;
        internal TooltipHitTestPreset CurrentDefaultHitTest => _currentDefaultHitTest;
        internal TooltipChannelSpaceKind CurrentTriggerSpaceKind => _spaceKind;
        internal TooltipChannelRenderSpaceKind CurrentRenderSpaceKind => _renderSpaceKind;
        internal bool EnableDebugLog => _mb.EnableDebugLog;
        internal ITooltipSystemService? TooltipSystemService => _tooltipSystemService;
        internal RectTransform? UiRoot => _uiRoot;
        internal Transform? WorldRoot => _worldRoot;
        internal ISceneSpawnerRegistry? SpawnerRegistry => _spawnerRegistry;
        internal IPointerService? PointerService => _pointerService;
        internal IWorldPointerRuntimeService? WorldPointerService => _worldPointerService;
        internal IUISelectionState? SelectionState => _selectionState;
        internal IModalStackChannelHubService? ModalStackHub => _modalStackHub;
        internal int ResolveSpawnWarmupFrames()
        {
            var hubFrames = _currentHubPreset.SpawnWarmupFrames;
            if (hubFrames > 0)
                return hubFrames;
            return _tooltipSystemService != null ? Mathf.Max(0, _tooltipSystemService.SpawnWarmupFrames) : 0;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _activeScope = scope;
            _isAcquired = false;

            ResolveServices(scope);
            if (_tooltipSystemService == null)
            {
                Debug.LogWarning($"[TooltipChannelHub] TooltipSystemService was not found for '{scope.Identity?.Id ?? scope.Identity?.SelfTransform?.name ?? gameObjectName()}'. Hub is disabled.");
                return;
            }

            ResolveRoots();
            ResolveHubPreset(scope);
            ResolveSpaceAndSolver();
            LogDebug($"Acquire. TriggerSpace={_spaceKind} RenderSpace={_renderSpaceKind} UiRoot={DescribeObject(_uiRoot)} WorldRoot={DescribeObject(_worldRoot)}");
            if (_renderSpaceKind == TooltipChannelRenderSpaceKind.UIScreen && _uiRoot == null)
            {
                Debug.LogWarning($"[TooltipChannelHub] UI space hub requires TooltipSystem.TooltipRoot or Tooltip Root Override. Hub '{scope.Identity?.Id ?? scope.Identity?.SelfTransform?.name ?? gameObjectName()}' is disabled.");
                return;
            }

            _isAcquired = true;
            RebuildChannels(scope, isReset);
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            ReleaseChannels(scope, isReset);
            _activeScope = null;
            _uiInputService = null;
            _pointerService = null;
            _worldPointerService = null;
            _selectionState = null;
            _modalStackHub = null;
            _spawnerRegistry = null;
            _cameraLocationService = null;
            _tooltipSystemService = null;
            _solver = null;
            _spaceKind = TooltipChannelSpaceKind.Unknown;
            _renderSpaceKind = TooltipChannelRenderSpaceKind.Auto;
            _uiRoot = null;
            _worldRoot = null;
            _clampArea = null;
            _uiRootCanvas = null;
            _fallbackCameraCache = null;
            _cachedClampRect = default;
            _cachedClampCamera = null;
            _cachedClampScreenWidth = 0;
            _cachedClampScreenHeight = 0;
            _hasCachedClampRect = false;
            _baseHubPreset = new TooltipHubPreset();
            _currentHubPreset = new TooltipHubPreset();
            _baseDefaultHitTest = TooltipHitTestPreset.CreateOwnerDefault();
            _currentDefaultHitTest = TooltipHitTestPreset.CreateOwnerDefault();
            _nextDynamicOrder = 0;
            _isAcquired = false;
        }

        public void Tick()
        {
            if (!_isAcquired || _solver == null)
                return;

            var camera = ResolveCamera();
            var pointerScreen = _pointerService != null ? _pointerService.PointerScreen() : Vector2.zero;
            var inputMode = ResolveInputMode();

            for (var i = 0; i < _orderedChannels.Count; i++)
                _orderedChannels[i].Runtime.Tick(inputMode, pointerScreen, camera);

            _requests.Clear();
            _solutions.Clear();
            for (var i = 0; i < _orderedChannels.Count; i++)
            {
                var runtime = _orderedChannels[i].Runtime;
                if (_solver.TryBuildRequest(runtime, pointerScreen, camera, out var request))
                    _requests.Add(request);
            }

            if (_requests.Count <= 0)
                return;

            if (_requests.Count > 1)
            {
                _requests.Sort(static (a, b) =>
                {
                    var priorityCompare = b.Priority.CompareTo(a.Priority);
                    if (priorityCompare != 0)
                        return priorityCompare;
                    return a.Order.CompareTo(b.Order);
                });
            }

            var screenRect = ResolveClampScreenRect(camera);
            for (var i = 0; i < _requests.Count; i++)
            {
                var solution = _solver.Solve(_requests[i], _solutions, camera, _currentHubPreset, screenRect, _screenClampService);
                _solutions.Add(solution);
            }

            for (var i = 0; i < _solutions.Count; i++)
                _solver.ApplySolution(_solutions[i].Runtime, _solutions[i], camera);
        }

        public bool Contains(string tag)
        {
            return _channels.ContainsKey(NormalizeTag(tag));
        }

        public bool TryGetPlayer(string tag, out ITooltipChannelPlayer? player)
        {
            player = null;
            if (!_channels.TryGetValue(NormalizeTag(tag), out var entry))
                return false;

            player = entry.Runtime;
            return true;
        }

        public bool TryGetCommand(string tag, out ITooltipChannelCommandService? command)
        {
            command = null;
            if (!_channels.TryGetValue(NormalizeTag(tag), out var entry))
                return false;

            command = entry.Runtime;
            return true;
        }

        public bool TryGetControl(string tag, out ITooltipChannelControlService? control)
        {
            control = null;
            if (!_channels.TryGetValue(NormalizeTag(tag), out var entry))
                return false;

            control = entry.Runtime;
            return true;
        }

        public bool RegisterOrReplace(string tag, TooltipPlayerPreset preset)
        {
            if (preset == null)
                return false;

            var normalizedTag = NormalizeTag(tag);
            var options = new TooltipChannelOptions
            {
                PresetValue = DynamicValue<TooltipPlayerPreset>.FromSource(
                    new ManagedRefLiteralSource<TooltipPlayerPreset>(preset.CreateRuntimeCopy())),
            };

            if (_channels.TryGetValue(normalizedTag, out var existing))
            {
                var order = existing.Order;
                if (_isAcquired && _activeScope != null)
                    existing.Runtime.OnRelease(_activeScope, false);

                existing.Options = options;
                existing.Runtime = new TooltipChannelPlayerRuntime(this, _owner, normalizedTag, order, options);
                if (_isAcquired && _activeScope != null)
                    existing.Runtime.OnAcquire(_activeScope, false);
                return true;
            }

            var entry = CreateEntry(normalizedTag, _nextDynamicOrder++, options);
            _channels.Add(normalizedTag, entry);
            _orderedChannels.Add(entry);
            if (_isAcquired && _activeScope != null)
                entry.Runtime.OnAcquire(_activeScope, false);
            return true;
        }

        public bool Unregister(string tag)
        {
            var normalizedTag = NormalizeTag(tag);
            if (!_channels.TryGetValue(normalizedTag, out var entry))
                return false;

            if (_isAcquired && _activeScope != null)
                entry.Runtime.OnRelease(_activeScope, false);

            _channels.Remove(normalizedTag);
            _orderedChannels.Remove(entry);
            return true;
        }

        public void ClearAll()
        {
            if (_activeScope != null)
                ReleaseChannels(_activeScope, false);
            else
            {
                _channels.Clear();
                _orderedChannels.Clear();
            }
        }

        public bool SwapHubPreset(TooltipHubPreset? preset)
        {
            if (preset == null)
                return false;

            _baseHubPreset = preset.CreateRuntimeCopy();
            _currentHubPreset = _baseHubPreset.CreateRuntimeCopy();
            _baseDefaultHitTest = ResolveHitTestPreset(_currentHubPreset.DefaultHitTestValue, CreateDynamicContext(_activeScope ?? _owner), TooltipHitTestPreset.CreateOwnerDefault());
            _currentDefaultHitTest = _baseDefaultHitTest.CreateRuntimeCopy();
            return true;
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
            scope.TryResolveInAncestors(out _uiInputService);
            scope.TryResolveInAncestors(out _pointerService);
            scope.TryResolveInAncestors(out _worldPointerService);
            scope.TryResolveInAncestors(out _selectionState);
            scope.TryResolveInAncestors(out _modalStackHub);
            scope.TryResolveInAncestors(out _spawnerRegistry);
            scope.TryResolveInAncestors(out _cameraLocationService);
            scope.TryResolveInAncestors(out _tooltipSystemService);
            if (scope.TryResolveInAncestors<IScreenClampService>(out var clampService) && clampService != null)
                _screenClampService = clampService;
            else
                _screenClampService = new ScreenClampService();
        }

        void ResolveRoots()
        {
            if (_tooltipSystemService == null)
            {
                _uiRoot = null;
                _worldRoot = null;
                _clampArea = null;
                return;
            }

            var defaultUiRoot = _tooltipSystemService.TooltipRoot;
            _uiRoot = _mb.ApplyTooltipRootOverride && _mb.TooltipRootOverride != null
                ? _mb.TooltipRootOverride
                : defaultUiRoot;
            _worldRoot = _tooltipSystemService.WorldRoot;
            _clampArea = _tooltipSystemService.ClampArea;
            _uiRootCanvas = _uiRoot != null ? _uiRoot.GetComponentInParent<Canvas>() : null;
            _fallbackCameraCache = null;
            _hasCachedClampRect = false;
        }

        void ResolveHubPreset(IScopeNode scope)
        {
            var context = CreateDynamicContext(scope);
            if (_mb.ApplyHubPresetOverride)
            {
                _baseHubPreset = ResolveHubPreset(_mb.HubPresetValue, context);
            }
            else
            {
                _baseHubPreset = _tooltipSystemService != null
                    ? _tooltipSystemService.SharedHubPreset.CreateRuntimeCopy()
                    : new TooltipHubPreset();
            }

            _currentHubPreset = _baseHubPreset.CreateRuntimeCopy();
            _baseDefaultHitTest = ResolveHitTestPreset(_currentHubPreset.DefaultHitTestValue, context, TooltipHitTestPreset.CreateOwnerDefault());
            _currentDefaultHitTest = _baseDefaultHitTest.CreateRuntimeCopy();
        }

        void ResolveSpaceAndSolver()
        {
            _spaceKind = _mb.SpaceKind;
            if (_spaceKind == TooltipChannelSpaceKind.Unknown)
                _spaceKind = _uiRoot != null ? TooltipChannelSpaceKind.UIScreen : TooltipChannelSpaceKind.World;
            _renderSpaceKind = ResolveRenderSpaceKind(_currentHubPreset.RenderSpace, _uiRoot);
            _solver = _renderSpaceKind == TooltipChannelRenderSpaceKind.UIScreen
                ? new UITooltipPlacementSolver()
                : new WorldTooltipPlacementSolver();
        }

        static TooltipChannelRenderSpaceKind ResolveRenderSpaceKind(TooltipChannelRenderSpaceKind configured, RectTransform? uiRoot)
        {
            if (configured == TooltipChannelRenderSpaceKind.UIScreen)
                return uiRoot != null ? TooltipChannelRenderSpaceKind.UIScreen : TooltipChannelRenderSpaceKind.World;

            if (configured == TooltipChannelRenderSpaceKind.World)
                return TooltipChannelRenderSpaceKind.World;

            return uiRoot != null ? TooltipChannelRenderSpaceKind.UIScreen : TooltipChannelRenderSpaceKind.World;
        }

        void RebuildChannels(IScopeNode scope, bool isReset)
        {
            ReleaseChannels(scope, isReset);
            _nextDynamicOrder = 0;

            var definitions = _mb.Channels;
            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition == null)
                    continue;

                var tag = NormalizeTag(definition.ChannelTag);
                if (_channels.ContainsKey(tag))
                {
                    Debug.LogWarning($"[TooltipChannelHub] Duplicate channel tag '{tag}' was skipped.");
                    continue;
                }

                var entry = CreateEntry(tag, _nextDynamicOrder++, definition.CreateOptions());
                entry.Runtime.OnAcquire(scope, isReset);
                _channels.Add(tag, entry);
                _orderedChannels.Add(entry);
            }

            LogDebug($"Channels rebuilt. Count={_orderedChannels.Count}");
        }

        void ReleaseChannels(IScopeNode scope, bool isReset)
        {
            for (var i = _orderedChannels.Count - 1; i >= 0; i--)
                _orderedChannels[i].Runtime.OnRelease(scope, isReset);

            _channels.Clear();
            _orderedChannels.Clear();
        }

        ChannelEntry CreateEntry(string tag, int order, TooltipChannelOptions options)
        {
            return new ChannelEntry
            {
                Tag = tag,
                Order = order,
                Options = options,
                Runtime = new TooltipChannelPlayerRuntime(this, _owner, tag, order, options),
            };
        }

        Camera? ResolveCamera()
        {
            if (_cameraLocationService != null &&
                _cameraLocationService.TryGetCamera(_currentHubPreset.CameraLocationTag, out var resolvedCamera) &&
                resolvedCamera != null)
            {
                return resolvedCamera;
            }

            if (_renderSpaceKind == TooltipChannelRenderSpaceKind.UIScreen && _uiRootCanvas != null)
            {
                if (_uiRootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
                    return null;

                if (_uiRootCanvas.worldCamera != null)
                    return _uiRootCanvas.worldCamera;
            }

            if (_fallbackCameraCache != null)
            {
                if (_fallbackCameraCache.isActiveAndEnabled)
                    return _fallbackCameraCache;

                _fallbackCameraCache = null;
            }

            _fallbackCameraCache = Camera.main;
            return _fallbackCameraCache;
        }

        TooltipChannelInputMode ResolveInputMode()
        {
            var mode = _currentHubPreset.InputMode;
            if (mode != TooltipChannelInputMode.AutoByInputService)
                return mode;

            if (_tooltipSystemService != null && _tooltipSystemService.InputMode != TooltipChannelInputMode.AutoByInputService)
                return _tooltipSystemService.InputMode;

            if (_uiInputService == null)
                return TooltipChannelInputMode.Pointer;

            if (_uiInputService.IsPointerModeActive)
                return TooltipChannelInputMode.Pointer;

            if (_uiInputService.IsNavigationModeActive)
                return TooltipChannelInputMode.Navigation;

            return TooltipChannelInputMode.Pointer;
        }

        Rect ResolveClampScreenRect(Camera? camera)
        {
            if (_clampArea == null)
                return new Rect(0f, 0f, Screen.width, Screen.height);

            if (_hasCachedClampRect &&
                ReferenceEquals(_cachedClampCamera, camera) &&
                _cachedClampScreenWidth == Screen.width &&
                _cachedClampScreenHeight == Screen.height &&
                !_clampArea.hasChanged)
            {
                return _cachedClampRect;
            }

            _clampArea.GetWorldCorners(_rectCorners);
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (var i = 0; i < _rectCorners.Length; i++)
            {
                var screen = RectTransformUtility.WorldToScreenPoint(camera, _rectCorners[i]);
                min = Vector2.Min(min, screen);
                max = Vector2.Max(max, screen);
            }

            _cachedClampRect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            _cachedClampCamera = camera;
            _cachedClampScreenWidth = Screen.width;
            _cachedClampScreenHeight = Screen.height;
            _hasCachedClampRect = true;
            _clampArea.hasChanged = false;
            return _cachedClampRect;
        }

        internal static TooltipHubPreset ResolveHubPreset(DynamicValue<TooltipHubPreset> value, IDynamicContext context)
        {
            if (value.TryGet(context, out TooltipHubPreset? preset) && preset != null)
                return preset.CreateRuntimeCopy();

            return new TooltipHubPreset();
        }

        internal static TooltipHitTestPreset ResolveHitTestPreset(
            DynamicValue<TooltipHitTestPreset> value,
            IDynamicContext context,
            TooltipHitTestPreset fallback)
        {
            if (value.TryGet(context, out TooltipHitTestPreset? preset) && preset != null)
                return preset.CreateRuntimeCopy();

            return fallback.CreateRuntimeCopy();
        }

        internal static IDynamicContext CreateDynamicContext(IScopeNode scope)
        {
            return new SimpleDynamicContext(ResolveVars(scope), scope);
        }

        internal static IVarStore ResolveVars(IScopeNode scope)
        {
            var resolver = scope?.Resolver;
            if (resolver != null)
            {
                if (resolver.TryResolve<IVarStore>(out var vars) && vars != null)
                    return vars;

                if (resolver.TryResolve<IBlackboardService>(out var blackboard) && blackboard?.LocalVars != null)
                    return blackboard.LocalVars;
            }

            return NullVarStore.Instance;
        }

        internal static void EnsureScopeBuiltIfNeeded(IScopeNode scope)
        {
            if (scope is BaseLifetimeScope baseScope)
            {
                baseScope.EnsureScopeBuilt();
                return;
            }

            if (scope is RuntimeLifetimeScope runtimeScope)
                runtimeScope.EnsureScopeBuilt();
        }

        internal static Transform? GetTransformFromScope(IScopeNode scope)
        {
            if (scope.Identity?.SelfTransform != null)
                return scope.Identity.SelfTransform;

            if (scope is Component component)
                return component.transform;

            return null;
        }

        internal static void ReleaseSpawnedRuntime(IScopeNode? scope)
        {
            if (scope == null)
                return;

            if (scope is RuntimeLifetimeScope runtimeScope)
            {
                if (runtimeScope.Resolver != null &&
                    runtimeScope.Resolver.TryResolve<IRuntimeLifetimeScopePool>(out var pool) &&
                    pool != null)
                {
                    pool.Release(runtimeScope);
                    return;
                }

                UnityEngine.Object.Destroy(runtimeScope.gameObject);
                return;
            }

            var transform = GetTransformFromScope(scope);
            if (transform != null)
                UnityEngine.Object.Destroy(transform.gameObject);
        }

        static string NormalizeTag(string? tag)
        {
            return string.IsNullOrWhiteSpace(tag) ? "default" : tag.Trim();
        }

        void LogDebug(string message)
        {
            if (!_mb.EnableDebugLog)
                return;

            Debug.Log($"[TooltipChannelHub] {message}");
        }

        static string DescribeObject(UnityEngine.Object? obj)
        {
            if (obj == null)
                return "null";

            if (obj is Component component)
                return $"{component.GetType().Name}({component.gameObject.name})";

            return obj.name;
        }

        string gameObjectName()
        {
            return _mb != null ? _mb.name : "(unknown)";
        }
    }
}
