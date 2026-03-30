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
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.UI
{
    internal sealed class WorldSpaceSliderVisualBackend : ISliderVisualBackend
    {
        readonly IScopeNode _owner;
        readonly ISliderOptions _options;
        readonly ISliderPlayerRuntime _output;
        readonly ISliderRuntimePresetProvider _presetProvider;

        ISceneSpawnerRegistry? _spawnerRegistry;
        ICommandRunner? _commandRunner;
        IScopeNode? _activeScope;
        IDynamicContext? _dynamicContext;

        SliderVisualizerPreset _visualizerPreset = new();
        SliderPlayerPreset _playerPreset = new();

        ActorSourceResolveCache _areaActorSourceCache;
        SliderRangeResolveStatus _lastRangeStatus = SliderRangeResolveStatus.Success;
        AreaRectSnapshot _lastRangeSnapshot;
        bool _hasLastRangeSnapshot;
        SliderOutputSnapshot _lastSnapshot;
        bool _hasLastSnapshot;
        bool _acquired;
        bool _visualDirty;
        bool _buildReady;
        int _builtBoundaryCount = -1;

        CancellationTokenSource? _spawnCts;
        SliderSpawnedRuntimeInstance? _backgroundRuntime;
        SliderSpawnedRuntimeInstance? _handleRuntime;
        readonly List<SliderSpawnedRuntimeInstance> _segmentBars = new();
        readonly List<SliderSpawnedRuntimeInstance> _markers = new();

        bool _loggedBackgroundRendererMissing;
        bool _loggedBackgroundScaleFallback;
        bool _loggedSegmentBarRendererMissing;
        bool _loggedSegmentScaleFallback;

        public WorldSpaceSliderVisualBackend(
            IScopeNode owner,
            ISliderOptions options,
            ISliderPlayerRuntime output,
            ISliderRuntimePresetProvider presetProvider)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _output = output ?? throw new ArgumentNullException(nameof(output));
            _presetProvider = presetProvider ?? throw new ArgumentNullException(nameof(presetProvider));
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _ = isReset;

            _acquired = true;
            _activeScope = scope;
            _areaActorSourceCache = default;
            _lastRangeStatus = SliderRangeResolveStatus.Success;
            _buildReady = false;
            _builtBoundaryCount = -1;
            _visualDirty = true;
            _hasLastRangeSnapshot = false;
            _loggedBackgroundRendererMissing = false;
            _loggedBackgroundScaleFallback = false;
            _loggedSegmentBarRendererMissing = false;
            _loggedSegmentScaleFallback = false;

            StopSpawn();
            ReleaseRuntimeInstances();

            var vars = ResolveVars(scope);
            _dynamicContext = new SimpleDynamicContext(vars, scope);
            ResolveServices(scope);
            RefreshCurrentPresets(scope);
            Subscribe();

            _lastSnapshot = BuildCurrentSnapshot();
            _hasLastSnapshot = true;
            BeginBuild();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;

            _acquired = false;
            _activeScope = null;
            Unsubscribe();
            StopSpawn();
            ReleaseRuntimeInstances();

            _spawnerRegistry = null;
            _commandRunner = null;
            _dynamicContext = null;
            _hasLastRangeSnapshot = false;
            _hasLastSnapshot = false;
            _buildReady = false;
            _builtBoundaryCount = -1;
            _backgroundRuntime = null;
            _handleRuntime = null;
            _segmentBars.Clear();
            _markers.Clear();
        }

        public void Tick()
        {
            if (!_acquired || _activeScope == null)
                return;

            if (_builtBoundaryCount != _output.BoundaryCount)
            {
                BeginBuild();
                return;
            }

            var rangeStatus = SliderRuntimeHelpers.TryResolveWorldRangeSnapshot(
                _activeScope,
                _options,
                ref _areaActorSourceCache,
                out var rangeSnapshot);
            if (rangeStatus != SliderRangeResolveStatus.Success)
            {
                LogRangeResolveStatus(rangeStatus);
                return;
            }

            _lastRangeStatus = rangeStatus;
            var rangeChanged = !_hasLastRangeSnapshot || !SliderRuntimeHelpers.ApproximatelyEquals(_lastRangeSnapshot, rangeSnapshot);
            _lastRangeSnapshot = rangeSnapshot;
            _hasLastRangeSnapshot = true;

            if (!_hasLastSnapshot)
                _lastSnapshot = BuildCurrentSnapshot();

            if (!_lastSnapshot.IsVisible)
            {
                SetRuntimeActive(false);
                _visualDirty = false;
                return;
            }

            if (!_buildReady)
                return;

            if (!rangeChanged && !_visualDirty)
                return;

            ApplySnapshot(_lastSnapshot, rangeSnapshot);
            _visualDirty = false;
        }

        void ResolveServices(IScopeNode scope)
        {
            _spawnerRegistry = null;
            _commandRunner = null;

            var resolver = scope.Resolver;
            if (resolver == null)
                return;

            if (!scope.TryResolveInAncestors(out _spawnerRegistry))
                resolver.TryResolve(out _spawnerRegistry);
            if (!scope.TryResolveInAncestors(out _commandRunner))
                resolver.TryResolve(out _commandRunner);
        }

        void RefreshCurrentPresets(IScopeNode scope)
        {
            if (_dynamicContext != null)
            {
                _visualizerPreset = _presetProvider.CurrentVisualizerPreset;
                _playerPreset = _presetProvider.CurrentPlayerPreset;
                return;
            }

            var vars = ResolveVars(scope);
            var dynamicContext = new SimpleDynamicContext(vars, scope);
            _visualizerPreset = _options.VisualizerPresetValue.GetOrDefault(dynamicContext, new SliderVisualizerPreset());
            _playerPreset = _options.PlayerPresetValue.GetOrDefault(dynamicContext, new SliderPlayerPreset());
        }

        void Subscribe()
        {
            _output.OnUpdated += HandleOutputUpdated;
            _presetProvider.OnVisualizerPresetChanged += HandleVisualizerPresetChanged;
            _presetProvider.OnPlayerPresetChanged += HandlePlayerPresetChanged;
        }

        void Unsubscribe()
        {
            _output.OnUpdated -= HandleOutputUpdated;
            _presetProvider.OnVisualizerPresetChanged -= HandleVisualizerPresetChanged;
            _presetProvider.OnPlayerPresetChanged -= HandlePlayerPresetChanged;
        }

        void HandleOutputUpdated(SliderOutputSnapshot snapshot)
        {
            _lastSnapshot = snapshot;
            _hasLastSnapshot = true;
            _visualDirty = true;
        }

        void HandleVisualizerPresetChanged()
        {
            if (_activeScope == null)
                return;

            RefreshCurrentPresets(_activeScope);
            BeginBuild();
        }

        void HandlePlayerPresetChanged()
        {
            if (_activeScope == null)
                return;

            RefreshCurrentPresets(_activeScope);
            BeginBuild();
        }

        void BeginBuild()
        {
            StopSpawn();
            ReleaseRuntimeInstances();

            _buildReady = false;
            _builtBoundaryCount = _output.BoundaryCount;
            _visualDirty = true;

            if (!_acquired || _activeScope == null)
                return;

            if (!IsAnyRuntimeBuildRequired())
            {
                _buildReady = true;
                return;
            }

            if (!TryResolveRuntimeSpawner(out var spawner) || spawner == null)
            {
                _buildReady = true;
                return;
            }

            _spawnCts = new CancellationTokenSource();
            BuildAsync(spawner, _spawnCts.Token).Forget();
        }

        bool IsAnyRuntimeBuildRequired()
        {
            return _visualizerPreset.Background.Enabled ||
                   _visualizerPreset.Segmented.SpawnSegmentBars ||
                   _visualizerPreset.Segmented.SpawnMarkers ||
                   _visualizerPreset.Handle.Enabled;
        }

        async UniTaskVoid BuildAsync(IAsyncSpawnerService spawner, CancellationToken ct)
        {
            SliderSpawnedRuntimeInstance? background = null;
            SliderSpawnedRuntimeInstance? handle = null;
            var bars = new List<SliderSpawnedRuntimeInstance>();
            var markers = new List<SliderSpawnedRuntimeInstance>();

            try
            {
                if (_dynamicContext == null)
                {
                    _buildReady = true;
                    return;
                }

                var (minValue, maxValue) = ResolvePlayerRange();

                if (_visualizerPreset.Background.Enabled)
                {
                    var template = SliderRuntimeHelpers.ResolveRuntimeTemplate(_visualizerPreset.Background.TemplatePreset, _dynamicContext);
                    if (template != null)
                    {
                        background = await SpawnSizedInstanceAsync(
                            spawner,
                            template,
                            ResolveBackgroundRoot(),
                            _visualizerPreset.Background.AnimationChannelTag,
                            SliderSpawnUnitKind.Background,
                            0,
                            minValue,
                            maxValue,
                            0f,
                            1f,
                            _visualizerPreset.Background.AllowPooling,
                            "[SliderVisualizerService] Background template requires an AnimationSprite target (SpriteRenderer/Image) or a fallback SpriteRenderer.",
                            ct);
                    }
                }

                if (_visualizerPreset.Segmented.SpawnSegmentBars)
                {
                    var template = SliderRuntimeHelpers.ResolveRuntimeTemplate(_visualizerPreset.Segmented.SegmentBarTemplatePreset, _dynamicContext);
                    if (template != null)
                    {
                        var segmentBarCount = SliderRuntimeHelpers.ResolveVisualSegmentBarCount(_visualizerPreset.Segmented, _output.BoundaryCount);
                        for (var i = 0; i < segmentBarCount; i++)
                        {
                            ct.ThrowIfCancellationRequested();
                            SliderRuntimeHelpers.ResolveVisualSegmentBarRange(
                                _visualizerPreset.Segmented,
                                _output,
                                i,
                                out var startRaw,
                                out var endRaw,
                                out var startNormalized,
                                out var endNormalized);
                            var instance = await SpawnSizedInstanceAsync(
                                spawner,
                                template,
                                ResolveSegmentBarsRoot(),
                                _visualizerPreset.Segmented.SegmentBarAnimationChannelTag,
                                SliderSpawnUnitKind.SegmentBar,
                                i,
                                startRaw,
                                endRaw,
                                startNormalized,
                                endNormalized,
                                _visualizerPreset.Segmented.AllowPooling,
                                "[SliderVisualizerService] Segment bar template requires an AnimationSprite target (SpriteRenderer/Image) or a fallback SpriteRenderer.",
                                ct);
                            if (instance != null)
                                bars.Add(instance);
                        }
                    }
                }

                if (_visualizerPreset.Segmented.SpawnMarkers)
                {
                    var template = SliderRuntimeHelpers.ResolveRuntimeTemplate(_visualizerPreset.Segmented.MarkerTemplatePreset, _dynamicContext);
                    if (template != null)
                    {
                        for (var i = 0; i < _output.BoundaryCount; i++)
                        {
                            ct.ThrowIfCancellationRequested();
                            var raw = _output.ResolveBoundaryRawValue(i);
                            var normalized = _output.ResolveBoundaryNormalizedValue(i);
                            var instance = await SpawnTransformInstanceAsync(
                                spawner,
                                template,
                                ResolveMarkersRoot(),
                                SliderSpawnUnitKind.Marker,
                                i,
                                raw,
                                normalized,
                                _visualizerPreset.Segmented.AllowPooling,
                                ct);
                            if (instance != null)
                                markers.Add(instance);
                        }
                    }
                }

                if (_visualizerPreset.Handle.Enabled)
                {
                    var template = SliderRuntimeHelpers.ResolveRuntimeTemplate(_visualizerPreset.Handle.TemplatePreset, _dynamicContext);
                    if (template != null)
                    {
                        handle = await SpawnTransformInstanceAsync(
                            spawner,
                            template,
                            ResolveMarkersRoot(),
                            SliderSpawnUnitKind.Handle,
                            0,
                            0f,
                            0f,
                            _visualizerPreset.Handle.AllowPooling,
                            ct);
                    }
                }

                _backgroundRuntime = background;
                _segmentBars.Clear();
                _segmentBars.AddRange(bars);
                _markers.Clear();
                _markers.AddRange(markers);
                _handleRuntime = handle;
                _buildReady = true;
                _visualDirty = true;

                await UniTask.SwitchToMainThread(ct);
                Tick();
                if (background != null)
                    await ExecuteSpawnCommandsAsync(background, _visualizerPreset.Background.OnSpawnCommands, ct);
                for (var i = 0; i < bars.Count; i++)
                    await ExecuteSpawnCommandsAsync(bars[i], _visualizerPreset.Segmented.OnSegmentBarSpawnCommands, ct);
                for (var i = 0; i < markers.Count; i++)
                    await ExecuteSpawnCommandsAsync(markers[i], _visualizerPreset.Segmented.OnMarkerSpawnCommands, ct);
                if (handle != null)
                    await ExecuteSpawnCommandsAsync(handle, _visualizerPreset.Handle.OnSpawnCommands, ct);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SliderVisualizerService] World slider build failed: {ex.Message}");
            }
            finally
            {
                if (!_buildReady)
                {
                    ReleaseRuntimeInstance(background);
                    ReleaseRuntimeInstances(bars);
                    ReleaseRuntimeInstances(markers);
                    ReleaseRuntimeInstance(handle);
                }
            }
        }

        void ApplySnapshot(in SliderOutputSnapshot snapshot, in AreaRectSnapshot rangeSnapshot)
        {
            SetRuntimeActive(true);

            if (_backgroundRuntime != null)
                ApplyBackgroundSnapshot(_backgroundRuntime, snapshot, rangeSnapshot);

            var segmentCount = Mathf.Min(
                _segmentBars.Count,
                SliderRuntimeHelpers.ResolveVisualSegmentBarCount(_visualizerPreset.Segmented, _output.BoundaryCount));
            for (var i = 0; i < segmentCount; i++)
            {
                SliderRuntimeHelpers.ResolveVisualSegmentBarRange(
                    _visualizerPreset.Segmented,
                    _output,
                    i,
                    out _,
                    out _,
                    out var startNormalized,
                    out var endNormalized);
                ApplySegmentBarSnapshot(_segmentBars[i], snapshot, rangeSnapshot, startNormalized, endNormalized);
            }

            var markerCount = Mathf.Min(_markers.Count, _output.BoundaryCount);
            for (var i = 0; i < markerCount; i++)
            {
                var normalized = _output.ResolveBoundaryNormalizedValue(i);
                ApplyMarkerSnapshot(_markers[i], rangeSnapshot, normalized);
            }

            if (_handleRuntime != null)
                ApplyHandleSnapshot(_handleRuntime, snapshot, rangeSnapshot);
        }

        void ApplyBackgroundSnapshot(
            SliderSpawnedRuntimeInstance instance,
            in SliderOutputSnapshot snapshot,
            in AreaRectSnapshot rangeSnapshot)
        {
            if (!SliderRuntimeHelpers.ShouldShowBackground(_visualizerPreset.Background, snapshot))
            {
                instance.Root.gameObject.SetActive(false);
                return;
            }

            SliderRuntimeHelpers.ResolveIntervalBarGeometry(
                rangeSnapshot,
                _visualizerPreset.Segmented.FillAxis,
                _visualizerPreset.Segmented.OriginSide,
                0f,
                1f,
                out var worldCenter,
                out var majorLength);

            var depthOffset = _visualizerPreset.Background.DepthOffset;
            if (rangeSnapshot.Plane == AreaPlane.XZ)
                worldCenter.y += depthOffset;
            else
                worldCenter.z += depthOffset;

            var minorLength = SliderRuntimeHelpers.ResolveAreaCrossLength(rangeSnapshot, _visualizerPreset.Segmented.FillAxis);
            var usedScaleFallback = SliderRuntimeHelpers.ApplySpawnedBarGeometry(
                instance,
                rangeSnapshot,
                _visualizerPreset.Segmented.FillAxis,
                worldCenter,
                majorLength,
                minorLength);

            if (usedScaleFallback && !_loggedBackgroundScaleFallback)
            {
                Debug.LogWarning("[SliderVisualizerService] Background SpriteRenderer drawMode is Simple. Falling back to transform scale.");
                _loggedBackgroundScaleFallback = true;
            }
        }

        void ApplySegmentBarSnapshot(
            SliderSpawnedRuntimeInstance instance,
            in SliderOutputSnapshot snapshot,
            in AreaRectSnapshot rangeSnapshot,
            float startNormalized,
            float endNormalized)
        {
            Vector3 worldCenter;
            float majorLength;
            SliderRuntimeHelpers.ResolveDisplayedSegmentBarInterval(
                _playerPreset.SegmentDisplayMode,
                _visualizerPreset.Segmented.SplitBarsByLayout,
                snapshot.DisplayedNormalizedValue,
                startNormalized,
                endNormalized,
                out var visibleStartNormalized,
                out var visibleEndNormalized,
                out var isVisible);

            SliderRuntimeHelpers.ResolveIntervalBarGeometry(
                rangeSnapshot,
                _visualizerPreset.Segmented.FillAxis,
                _visualizerPreset.Segmented.OriginSide,
                visibleStartNormalized,
                visibleEndNormalized,
                out worldCenter,
                out majorLength);

            if (_visualizerPreset.Segmented.SplitBarsByLayout)
                majorLength *= ResolveBarSpanScale();

            var minorLength = SliderRuntimeHelpers.ResolveAreaCrossLength(rangeSnapshot, _visualizerPreset.Segmented.FillAxis);
            var usedScaleFallback = SliderRuntimeHelpers.ApplySpawnedBarGeometry(
                instance,
                rangeSnapshot,
                _visualizerPreset.Segmented.FillAxis,
                worldCenter,
                majorLength,
                minorLength);

            if (usedScaleFallback && !_loggedSegmentScaleFallback)
            {
                Debug.LogWarning("[SliderVisualizerService] Segment bar SpriteRenderer drawMode is Simple. Falling back to transform scale.");
                _loggedSegmentScaleFallback = true;
            }

            if (instance.Root != null)
                instance.Root.gameObject.SetActive(isVisible);
        }

        void ApplyMarkerSnapshot(
            SliderSpawnedRuntimeInstance instance,
            in AreaRectSnapshot rangeSnapshot,
            float normalizedValue)
        {
            if (instance.Root == null)
                return;

            var worldPosition = SliderRuntimeHelpers.ResolveMarkerWorldPosition(
                rangeSnapshot,
                _visualizerPreset.Segmented.FillAxis,
                _visualizerPreset.Segmented.OriginSide,
                normalizedValue);
            SliderRuntimeHelpers.ApplyMarkerTransform(instance.Root, instance.BasePose, rangeSnapshot, worldPosition);
            instance.Root.gameObject.SetActive(true);
        }

        void ApplyHandleSnapshot(
            SliderSpawnedRuntimeInstance instance,
            in SliderOutputSnapshot snapshot,
            in AreaRectSnapshot rangeSnapshot)
        {
            if (instance.Root == null || _output.BoundaryCount <= 0)
                return;

            var boundaryIndex = _output.ResolveNearestBoundaryIndex(snapshot.TargetNormalizedValue);
            var normalizedValue = _output.ResolveBoundaryNormalizedValue(boundaryIndex);
            var worldPosition = SliderRuntimeHelpers.ResolveMarkerWorldPosition(
                rangeSnapshot,
                _visualizerPreset.Segmented.FillAxis,
                _visualizerPreset.Segmented.OriginSide,
                normalizedValue);
            SliderRuntimeHelpers.ApplyMarkerTransform(instance.Root, instance.BasePose, rangeSnapshot, worldPosition);
            instance.Root.gameObject.SetActive(true);
        }

        float ResolveBarSpanScale()
        {
            if (_dynamicContext != null)
                return Mathf.Max(0f, _visualizerPreset.Segmented.BarSpanScale.GetOrDefault(_dynamicContext, 1f));

            return Mathf.Max(0f, _visualizerPreset.Segmented.BarSpanScale.GetOrDefaultWithoutContext(1f));
        }

        bool TryResolveRuntimeSpawner(out IAsyncSpawnerService? spawner)
        {
            spawner = null;
            if (_spawnerRegistry == null)
            {
                Debug.LogWarning("[SliderVisualizerService] ISceneSpawnerRegistry is not available.");
                return false;
            }

            var resolvedSpawner = SceneSpawnerResolver.TryResolveAsyncSpawner(
                _spawnerRegistry,
                SpawnerKind.RuntimeEntity,
                string.Empty,
                allowTagFallback: true,
                allowRuntimeUiFallback: false);
            if (!resolvedSpawner.HasValue || resolvedSpawner.Spawner == null)
            {
                Debug.LogWarning("[SliderVisualizerService] RuntimeEntity spawner was not found.");
                return false;
            }

            spawner = resolvedSpawner.Spawner;
            return true;
        }

        Transform ResolveBackgroundRoot()
        {
            return _options.SegmentBarsRoot != null ? _options.SegmentBarsRoot : _options.OwnerTransform;
        }

        Transform ResolveSegmentBarsRoot()
        {
            return _options.SegmentBarsRoot != null ? _options.SegmentBarsRoot : _options.OwnerTransform;
        }

        Transform ResolveMarkersRoot()
        {
            return _options.SegmentMarkersRoot != null ? _options.SegmentMarkersRoot : _options.OwnerTransform;
        }

        async UniTask<SliderSpawnedRuntimeInstance?> SpawnSizedInstanceAsync(
            IAsyncSpawnerService spawner,
            BaseRuntimeTemplateSO template,
            Transform parent,
            string animationChannelTag,
            SliderSpawnUnitKind unitKind,
            int unitIndex,
            float startRawValue,
            float endRawValue,
            float startNormalized,
            float endNormalized,
            bool allowPooling,
            string warningMessage,
            CancellationToken ct)
        {
            var resolver = await SliderRuntimeHelpers.SpawnRuntimeAsync(spawner, template, parent, _owner, allowPooling, ct);
            if (resolver == null)
                return null;

            SliderRuntimeHelpers.ExtractSpawnedInfo(resolver, out var root, out var scopeNode, out _);
            if (root == null)
            {
                SliderRuntimeHelpers.ReleaseSpawnedRuntime(resolver);
                return null;
            }

            if (!SliderRuntimeHelpers.TryResolveRuntimeVisualTarget(
                    resolver,
                    root,
                    animationChannelTag,
                    out var visualTargetKind,
                    out var visualTransform,
                    out var spriteRenderer,
                    out var image) ||
                visualTargetKind == SliderRuntimeVisualTargetKind.None ||
                visualTransform == null)
            {
                if (unitKind == SliderSpawnUnitKind.Background)
                {
                    if (!_loggedBackgroundRendererMissing)
                    {
                        Debug.LogWarning(warningMessage);
                        _loggedBackgroundRendererMissing = true;
                    }
                }
                else if (!_loggedSegmentBarRendererMissing)
                {
                    Debug.LogWarning(warningMessage);
                    _loggedSegmentBarRendererMissing = true;
                }

                SliderRuntimeHelpers.ReleaseSpawnedRuntime(resolver);
                return null;
            }

            return new SliderSpawnedRuntimeInstance
            {
                Root = root,
                Scope = scopeNode,
                Resolver = resolver,
                BasePose = new SliderTransformPose(root),
                VisualTransform = visualTransform,
                VisualPose = new SliderTransformPose(visualTransform),
                VisualTargetKind = visualTargetKind,
                SpriteRenderer = spriteRenderer,
                SpriteState = spriteRenderer != null ? new SliderSpriteRenderState(spriteRenderer) : default,
                Image = image,
                ImageState = image != null ? new SliderImageRenderState(image) : default,
                UnitKind = unitKind,
                UnitIndex = unitIndex,
                StartRawValue = startRawValue,
                EndRawValue = endRawValue,
                StartNormalized = startNormalized,
                EndNormalized = endNormalized,
            };
        }

        async UniTask<SliderSpawnedRuntimeInstance?> SpawnTransformInstanceAsync(
            IAsyncSpawnerService spawner,
            BaseRuntimeTemplateSO template,
            Transform parent,
            SliderSpawnUnitKind unitKind,
            int unitIndex,
            float entryRawValue,
            float entryNormalized,
            bool allowPooling,
            CancellationToken ct)
        {
            var resolver = await SliderRuntimeHelpers.SpawnRuntimeAsync(spawner, template, parent, _owner, allowPooling, ct);
            if (resolver == null)
                return null;

            SliderRuntimeHelpers.ExtractSpawnedInfo(resolver, out var root, out var scopeNode, out _);
            if (root == null)
            {
                SliderRuntimeHelpers.ReleaseSpawnedRuntime(resolver);
                return null;
            }

            return new SliderSpawnedRuntimeInstance
            {
                Root = root,
                Scope = scopeNode,
                Resolver = resolver,
                BasePose = new SliderTransformPose(root),
                UnitKind = unitKind,
                UnitIndex = unitIndex,
                EntryIndex = unitKind == SliderSpawnUnitKind.Marker ? unitIndex : -1,
                EntryRawValue = entryRawValue,
                EntryNormalized = entryNormalized,
            };
        }

        async UniTask ExecuteSpawnCommandsAsync(
            SliderSpawnedRuntimeInstance instance,
            CommandListData commands,
            CancellationToken ct)
        {
            if (commands == null || commands.Count == 0)
                return;

            var actorScope = instance.Scope ?? _activeScope ?? _owner;
            if (actorScope == null)
                return;

            var runner = ResolveRunner(actorScope) ?? _commandRunner;
            if (runner == null)
                return;

            var vars = new VarStore();
            var snapshot = _lastSnapshot;
            SliderRuntimeHelpers.WriteCommonCommandVars(vars, snapshot, 0f, 0f);
            SliderRuntimeHelpers.WriteSpawnCommandVars(vars, instance);

            var options = CommandRunOptions.Default;
            var ctx = new CommandContext(actorScope, vars, runner, actorScope, options);

            try
            {
                await runner.ExecuteListAsync(commands, ctx, ct, options);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SliderVisualizerService] Spawn commands failed: {ex.Message}");
            }
        }

        void SetRuntimeActive(bool isActive)
        {
            if (_backgroundRuntime?.Root != null)
                _backgroundRuntime.Root.gameObject.SetActive(isActive);
            if (_handleRuntime?.Root != null)
                _handleRuntime.Root.gameObject.SetActive(isActive);

            for (var i = 0; i < _segmentBars.Count; i++)
            {
                if (_segmentBars[i].Root != null)
                    _segmentBars[i].Root.gameObject.SetActive(isActive);
            }

            for (var i = 0; i < _markers.Count; i++)
            {
                if (_markers[i].Root != null)
                    _markers[i].Root.gameObject.SetActive(isActive);
            }
        }

        void StopSpawn()
        {
            if (_spawnCts == null)
                return;

            _spawnCts.Cancel();
            _spawnCts.Dispose();
            _spawnCts = null;
        }

        void ReleaseRuntimeInstances()
        {
            ReleaseRuntimeInstance(_backgroundRuntime);
            _backgroundRuntime = null;
            ReleaseRuntimeInstance(_handleRuntime);
            _handleRuntime = null;
            ReleaseRuntimeInstances(_segmentBars);
            ReleaseRuntimeInstances(_markers);
            _segmentBars.Clear();
            _markers.Clear();
        }

        void ReleaseRuntimeInstances(List<SliderSpawnedRuntimeInstance> instances)
        {
            for (var i = 0; i < instances.Count; i++)
                ReleaseRuntimeInstance(instances[i]);
        }

        void ReleaseRuntimeInstance(SliderSpawnedRuntimeInstance? instance)
        {
            if (instance == null)
                return;

            SliderRuntimeHelpers.RestoreSpawnedRuntime(instance);
            SliderRuntimeHelpers.ReleaseSpawnedRuntime(instance.Resolver);
        }

        SliderOutputSnapshot BuildCurrentSnapshot()
        {
            return new SliderOutputSnapshot(
                _output.IsVisible,
                _output.TargetRawValue,
                _output.TargetNormalizedValue,
                _output.DisplayedRawValue,
                _output.DisplayedNormalizedValue);
        }

        (float MinValue, float MaxValue) ResolvePlayerRange()
        {
            var minValue = ResolveFloat(_playerPreset.MinValue, 0f);
            var maxValue = ResolveFloat(_playerPreset.MaxValue, 1f);
            return (Mathf.Min(minValue, maxValue), Mathf.Max(minValue, maxValue));
        }

        float ResolveFloat(DynamicValue<float> value, float fallback)
        {
            if (_dynamicContext != null)
                return value.GetOrDefault(_dynamicContext, fallback);

            return value.GetOrDefaultWithoutContext(fallback);
        }

        void LogRangeResolveStatus(SliderRangeResolveStatus status)
        {
            if (_lastRangeStatus == status)
                return;

            switch (status)
            {
                case SliderRangeResolveStatus.AreaScopeUnavailable:
                    Debug.LogWarning("[SliderVisualizerService] Area scope could not be resolved.");
                    break;
                case SliderRangeResolveStatus.AreaHubUnavailable:
                    Debug.LogWarning("[SliderVisualizerService] IAreaChannelHubService could not be resolved.");
                    break;
                case SliderRangeResolveStatus.AreaPlayerUnavailable:
                    Debug.LogWarning($"[SliderVisualizerService] Range source '{_options.AreaChannelTag}' could not be resolved.");
                    break;
                case SliderRangeResolveStatus.UnsupportedShape:
                    Debug.LogWarning("[SliderVisualizerService] Slider only supports RectAreaShape.");
                    break;
                case SliderRangeResolveStatus.UnsupportedWorldRectTransform:
                    Debug.LogWarning("[SliderVisualizerService] World slider does not support RectTransform range sources.");
                    break;
            }

            _lastRangeStatus = status;
        }

        ICommandRunner? ResolveRunner(IScopeNode scope)
        {
            if (scope.Resolver != null && scope.Resolver.TryResolve<ICommandRunner>(out var runner) && runner != null)
                return runner;

            if (scope.TryResolveInAncestors(out runner) && runner != null)
                return runner;

            return null;
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
    }
}
