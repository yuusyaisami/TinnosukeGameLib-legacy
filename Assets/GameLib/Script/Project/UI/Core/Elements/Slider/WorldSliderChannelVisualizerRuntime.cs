#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Commands.VNext;
using Game.Common;
using Game.DI;
using Game.Spawn;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.UI
{
    public sealed class WorldSliderChannelVisualizerRuntime :
        IWorldSliderVisualizerService,
        IScopeAcquireHandler,
        IScopeReleaseHandler,
        ITickable
    {
        readonly IScopeNode _owner;
        readonly IWorldSliderOptions _options;
        readonly IWorldSliderOutput? _directOutput;
        readonly IWorldSliderRuntimePresetProvider? _directPresetProvider;

        ISceneSpawnerRegistry? _spawnerRegistry;
        IWorldSliderOutput? _output;
        IWorldSliderRuntimePresetProvider? _presetProvider;
        ICommandRunner? _commandRunner;
        IScopeNode? _activeScope;

        IDynamicContext? _dynamicContext;
        WorldSliderVisualizerPreset _visualizerPreset = new();
        WorldSliderPlayerPreset _playerPreset = new();
        WorldSliderResolvedSegmentLayout? _segmentLayout;

        bool _hasLastSnapshot;
        WorldSliderOutputSnapshot _lastSnapshot;
        bool _subscribedOutput;
        bool _subscribedProvider;
        bool _acquired;
        bool _visualDirty;

        bool _hasAreaSnapshot;
        WorldSliderAreaSnapshot _lastAreaSnapshot;
        WorldSliderAreaResolveStatus _lastAreaResolveStatus = WorldSliderAreaResolveStatus.Success;
        ActorSourceResolveCache _areaActorSourceCache;

        bool _hasSimpleState;
        WorldSliderTransformPose _simplePose;
        WorldSliderSpriteRenderState _simpleSpriteState;

        CancellationTokenSource? _spawnCts;
        WorldSliderSpawnedRuntimeInstance? _simpleRuntimeBar;
        readonly List<WorldSliderSpawnedRuntimeInstance> _segmentBars = new();
        readonly List<WorldSliderSpawnedRuntimeInstance> _segmentMarkers = new();
        bool _simpleRuntimeReady;
        bool _segmentedReady;

        bool _loggedSimpleRendererMissing;
        bool _loggedSimpleScaleFallback;
        bool _loggedSimpleRuntimeRendererMissing;
        bool _loggedSegmentScaleFallback;
        bool _loggedSegmentBarRendererMissing;

        public WorldSliderChannelVisualizerRuntime(
            IScopeNode owner,
            IWorldSliderOptions options,
            IWorldSliderOutput? output = null,
            IWorldSliderRuntimePresetProvider? presetProvider = null)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _directOutput = output;
            _directPresetProvider = presetProvider;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _ = isReset;

            _acquired = true;
            _activeScope = scope;
            _visualDirty = true;
            _hasAreaSnapshot = false;
            _lastAreaResolveStatus = WorldSliderAreaResolveStatus.Success;
            _areaActorSourceCache = default;
            _loggedSimpleRendererMissing = false;
            _loggedSimpleScaleFallback = false;
            _loggedSimpleRuntimeRendererMissing = false;
            _loggedSegmentScaleFallback = false;
            _loggedSegmentBarRendererMissing = false;

            StopSpawn();
            ReleaseRuntimeInstances();
            RestoreSimpleState();

            var vars = ResolveVars(scope);
            _dynamicContext = new SimpleDynamicContext(vars, scope);
            ResolveServices(scope);
            RefreshCurrentPresets(scope);

            SubscribeOutput();
            SubscribePresetProvider();

            if (_output != null)
            {
                _lastSnapshot = new WorldSliderOutputSnapshot(
                    _output.IsVisible,
                    _output.TargetRawValue,
                    _output.TargetNormalizedValue,
                    _output.DisplayedRawValue,
                    _output.DisplayedNormalizedValue);
                _hasLastSnapshot = true;
            }
            else
            {
                _hasLastSnapshot = false;
            }

            BeginVisualizerBuild();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;

            _acquired = false;
            _activeScope = null;
            UnsubscribeOutput();
            UnsubscribePresetProvider();
            StopSpawn();
            RestoreSimpleState();
            ReleaseRuntimeInstances();
            _dynamicContext = null;
            _presetProvider = null;
            _segmentLayout = null;
            _commandRunner = null;
            _spawnerRegistry = null;
            _hasLastSnapshot = false;
            _hasAreaSnapshot = false;
            _visualDirty = false;
            _areaActorSourceCache = default;
        }

        public void Tick()
        {
            if (!_acquired || !_hasLastSnapshot || _activeScope == null)
                return;

            if (!_lastSnapshot.IsVisible)
            {
                ApplyHiddenState();
                _visualDirty = false;
                return;
            }

            if (RequiresRuntimeBuild() && !IsRuntimeBuildReady())
                return;

            if (!TryResolveAreaSnapshot(_activeScope, out var areaSnapshot))
                return;

            var areaChanged = !_hasAreaSnapshot || !areaSnapshot.ApproximatelyEquals(_lastAreaSnapshot);
            if (!areaChanged && !_visualDirty)
                return;

            _lastAreaSnapshot = areaSnapshot;
            _hasAreaSnapshot = true;
            ApplyCurrentSnapshot(_lastSnapshot, areaSnapshot);
            _visualDirty = false;
        }

        void ResolveServices(IScopeNode scope)
        {
            _output = _directOutput;
            _spawnerRegistry = null;
            _presetProvider = _directPresetProvider;
            _commandRunner = null;

            var resolver = scope?.Resolver;
            if (resolver == null)
                return;

            if (_output == null)
                resolver.TryResolve(out _output);
            if (_presetProvider == null)
                resolver.TryResolve(out _presetProvider);
            if (!scope.TryResolveInAncestors(out _spawnerRegistry))
                resolver.TryResolve(out _spawnerRegistry);
            if (!scope.TryResolveInAncestors(out _commandRunner))
                resolver.TryResolve(out _commandRunner);
        }

        void RefreshCurrentPresets(IScopeNode scope)
        {
            if (_presetProvider != null)
            {
                _visualizerPreset = _presetProvider.CurrentVisualizerPreset;
                _playerPreset = _presetProvider.CurrentPlayerPreset;
            }
            else if (_dynamicContext != null)
            {
                _visualizerPreset = WorldSliderRuntimeHelpers.ResolveVisualizerPreset(_options.VisualizerPresetValue, _dynamicContext);
                _playerPreset = WorldSliderRuntimeHelpers.ResolvePlayerPreset(_options.PlayerPresetValue, _dynamicContext);
            }
            else
            {
                var vars = ResolveVars(scope);
                var dynamicContext = new SimpleDynamicContext(vars, scope);
                _visualizerPreset = WorldSliderRuntimeHelpers.ResolveVisualizerPreset(_options.VisualizerPresetValue, dynamicContext);
                _playerPreset = WorldSliderRuntimeHelpers.ResolvePlayerPreset(_options.PlayerPresetValue, dynamicContext);
            }

            _segmentLayout = BuildSegmentLayout();
        }

        void SubscribeOutput()
        {
            if (_output == null || _subscribedOutput)
                return;

            _output.OnUpdated += HandleOutputUpdated;
            _subscribedOutput = true;
        }

        void UnsubscribeOutput()
        {
            if (_output == null || !_subscribedOutput)
                return;

            _output.OnUpdated -= HandleOutputUpdated;
            _subscribedOutput = false;
        }

        void SubscribePresetProvider()
        {
            if (_presetProvider == null || _subscribedProvider)
                return;

            _presetProvider.OnVisualizerPresetChanged += HandleVisualizerPresetChanged;
            _presetProvider.OnPlayerPresetChanged += HandlePlayerPresetChanged;
            _subscribedProvider = true;
        }

        void UnsubscribePresetProvider()
        {
            if (_presetProvider == null || !_subscribedProvider)
                return;

            _presetProvider.OnVisualizerPresetChanged -= HandleVisualizerPresetChanged;
            _presetProvider.OnPlayerPresetChanged -= HandlePlayerPresetChanged;
            _subscribedProvider = false;
        }

        void HandleOutputUpdated(WorldSliderOutputSnapshot snapshot)
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
            BeginVisualizerBuild();
        }

        void HandlePlayerPresetChanged()
        {
            if (_activeScope == null)
                return;

            RefreshCurrentPresets(_activeScope);
            if (_visualizerPreset.Mode == WorldSliderVisualizerMode.Segmented)
            {
                BeginVisualizerBuild();
                return;
            }

            _visualDirty = true;
        }

        void BeginVisualizerBuild()
        {
            StopSpawn();
            ReleaseRuntimeInstances();
            RestoreSimpleState();
            _simpleRuntimeReady = false;
            _segmentedReady = false;
            _visualDirty = true;

            if (!_acquired || _activeScope == null)
                return;

            if (IsSimpleSceneBackend())
            {
                CacheSimpleState();
                Tick();
                return;
            }

            _spawnCts = new CancellationTokenSource();
            if (IsSimpleRuntimeBackend())
            {
                RebuildSimpleRuntimeAsync(_spawnCts.Token).Forget();
                return;
            }

            RebuildSegmentedAsync(_spawnCts.Token).Forget();
        }

        bool RequiresRuntimeBuild()
        {
            return IsSimpleRuntimeBackend() || _visualizerPreset.Mode == WorldSliderVisualizerMode.Segmented;
        }

        bool IsRuntimeBuildReady()
        {
            if (IsSimpleRuntimeBackend())
                return _simpleRuntimeReady;

            if (_visualizerPreset.Mode == WorldSliderVisualizerMode.Segmented)
                return _segmentedReady;

            return true;
        }

        bool IsSimpleSceneBackend()
        {
            return _visualizerPreset.Mode == WorldSliderVisualizerMode.Simple &&
                   _visualizerPreset.Simple.RenderBackend == WorldSliderSimpleRenderBackend.SceneSpriteRenderer;
        }

        bool IsSimpleRuntimeBackend()
        {
            return _visualizerPreset.Mode == WorldSliderVisualizerMode.Simple &&
                   _visualizerPreset.Simple.RenderBackend == WorldSliderSimpleRenderBackend.RuntimeGeneratedBar;
        }

        void CacheSimpleState()
        {
            var simpleBarRenderer = _options.SimpleBarRenderer;
            if (simpleBarRenderer == null)
            {
                if (!_loggedSimpleRendererMissing)
                {
                    Debug.LogWarning("[WorldSliderVisualizerService] SimpleBarRenderer is null.");
                    _loggedSimpleRendererMissing = true;
                }

                _hasSimpleState = false;
                return;
            }

            _simplePose = new WorldSliderTransformPose(simpleBarRenderer.transform);
            _simpleSpriteState = new WorldSliderSpriteRenderState(simpleBarRenderer);
            _hasSimpleState = true;
        }

        void RestoreSimpleState()
        {
            if (!_hasSimpleState || _options.SimpleBarRenderer == null)
                return;

            _simplePose.ApplyTo(_options.SimpleBarRenderer.transform);
            _simpleSpriteState.ApplyTo(_options.SimpleBarRenderer);
            _hasSimpleState = false;
        }

        void ApplyCurrentSnapshot(
            in WorldSliderOutputSnapshot snapshot,
            in WorldSliderAreaSnapshot areaSnapshot)
        {
            if (IsSimpleSceneBackend())
            {
                ApplySimpleSceneSnapshot(snapshot, areaSnapshot);
                return;
            }

            if (IsSimpleRuntimeBackend())
            {
                ApplySimpleRuntimeSnapshot(snapshot, areaSnapshot);
                return;
            }

            ApplySegmentedSnapshot(snapshot, areaSnapshot);
        }

        void ApplySimpleSceneSnapshot(
            in WorldSliderOutputSnapshot snapshot,
            in WorldSliderAreaSnapshot areaSnapshot)
        {
            var simpleBarRenderer = _options.SimpleBarRenderer;
            if (!_hasSimpleState || simpleBarRenderer == null)
                return;

            simpleBarRenderer.enabled = snapshot.IsVisible;
            ApplyBarSnapshot(
                simpleBarRenderer,
                _simplePose,
                _simpleSpriteState,
                _visualizerPreset.Simple.FillAxis,
                _visualizerPreset.Simple.OriginSide,
                snapshot,
                areaSnapshot,
                ref _loggedSimpleScaleFallback,
                "[WorldSliderVisualizerService] Simple bar SpriteRenderer drawMode is Simple. Falling back to transform scale.");
        }

        void ApplySimpleRuntimeSnapshot(
            in WorldSliderOutputSnapshot snapshot,
            in WorldSliderAreaSnapshot areaSnapshot)
        {
            var instance = _simpleRuntimeBar;
            if (instance == null || instance.VisualTargetKind == WorldSliderRuntimeVisualTargetKind.None)
                return;

            if (instance.Root != null)
                instance.Root.gameObject.SetActive(snapshot.IsVisible);

            ApplySpawnedBarSnapshot(
                instance,
                _visualizerPreset.Simple.FillAxis,
                _visualizerPreset.Simple.OriginSide,
                snapshot,
                areaSnapshot,
                ResolveSimpleRuntimeBarSpanScale(),
                ref _loggedSimpleScaleFallback,
                "[WorldSliderVisualizerService] Simple runtime bar SpriteRenderer drawMode is Simple. Falling back to transform scale.");
        }

        static void ApplyBarSnapshot(
            SpriteRenderer renderer,
            in WorldSliderTransformPose basePose,
            in WorldSliderSpriteRenderState spriteState,
            WorldSliderAreaFillAxis fillAxis,
            WorldSliderAreaOriginSide originSide,
            in WorldSliderOutputSnapshot snapshot,
            in WorldSliderAreaSnapshot areaSnapshot,
            ref bool loggedScaleFallback,
            string scaleFallbackMessage)
        {
            WorldSliderRuntimeHelpers.ResolveFilledBarGeometry(
                areaSnapshot,
                fillAxis,
                originSide,
                snapshot.DisplayedNormalizedValue,
                out var worldCenter,
                out var majorLength);
            var minorLength = WorldSliderRuntimeHelpers.ResolveAreaCrossLength(areaSnapshot, fillAxis);

            var usedScaleFallback = WorldSliderRuntimeHelpers.ApplyBarRendererGeometry(
                renderer,
                basePose,
                spriteState,
                areaSnapshot,
                fillAxis,
                worldCenter,
                majorLength,
                minorLength);

            if (usedScaleFallback && !loggedScaleFallback)
            {
                Debug.LogWarning(scaleFallbackMessage);
                loggedScaleFallback = true;
            }
        }

        static void ApplySpawnedBarSnapshot(
            WorldSliderSpawnedRuntimeInstance instance,
            WorldSliderAreaFillAxis fillAxis,
            WorldSliderAreaOriginSide originSide,
            in WorldSliderOutputSnapshot snapshot,
            in WorldSliderAreaSnapshot areaSnapshot,
            float spanScale,
            ref bool loggedScaleFallback,
            string scaleFallbackMessage)
        {
            WorldSliderRuntimeHelpers.ResolveFilledBarGeometry(
                areaSnapshot,
                fillAxis,
                originSide,
                snapshot.DisplayedNormalizedValue,
                out var worldCenter,
                out var majorLength);
            majorLength *= Mathf.Max(0f, spanScale);
            var minorLength = WorldSliderRuntimeHelpers.ResolveAreaCrossLength(areaSnapshot, fillAxis);

            var usedScaleFallback = WorldSliderRuntimeHelpers.ApplySpawnedBarGeometry(
                instance,
                areaSnapshot,
                fillAxis,
                worldCenter,
                majorLength,
                minorLength);

            if (usedScaleFallback && !loggedScaleFallback)
            {
                Debug.LogWarning(scaleFallbackMessage);
                loggedScaleFallback = true;
            }
        }

        async UniTaskVoid RebuildSimpleRuntimeAsync(CancellationToken ct)
        {
            if (_dynamicContext == null)
                return;

            WorldSliderSpawnedRuntimeInstance? localBar = null;

            try
            {
                if (!TryResolveRuntimeSpawner(out var resolvedSpawner) || resolvedSpawner == null)
                    return;
                var spawner = resolvedSpawner;

                var barTemplate = WorldSliderRuntimeHelpers.ResolveRuntimeTemplate(_visualizerPreset.Simple.RuntimeBarTemplatePreset, _dynamicContext);
                if (barTemplate == null)
                {
                    Debug.LogWarning("[WorldSliderVisualizerService] Simple runtime bar template is null.");
                    return;
                }

                var (minValue, maxValue) = ResolvePlayerRange();
                var parent = ResolveSimpleRuntimeParent();
                localBar = await SpawnBarInstanceAsync(
                    spawner,
                    barTemplate,
                    parent,
                    _visualizerPreset.Simple.RuntimeBarAnimationChannelTag,
                    WorldSliderSpawnUnitKind.SimpleBar,
                    unitIndex: 0,
                    startRawValue: minValue,
                    endRawValue: maxValue,
                    startNormalized: 0f,
                    endNormalized: 1f,
                    allowPooling: _visualizerPreset.Simple.AllowPooling,
                    isSimpleRuntime: true,
                    ct);

                if (localBar == null || ct.IsCancellationRequested)
                    return;

                _simpleRuntimeBar = localBar;
                _simpleRuntimeReady = true;
                _visualDirty = true;

                await UniTask.SwitchToMainThread(ct);
                Tick();
                await ExecuteSpawnCommandsAsync(localBar, _visualizerPreset.Simple.OnBarSpawnCommands, ct);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WorldSliderVisualizerService] Simple runtime build failed: {ex.Message}");
            }
            finally
            {
                if (!_simpleRuntimeReady && localBar != null)
                    ReleaseRuntimeInstance(localBar);
            }
        }

        async UniTaskVoid RebuildSegmentedAsync(CancellationToken ct)
        {
            if (_dynamicContext == null || _segmentLayout == null)
                return;

            var segmented = _visualizerPreset.Segmented;
            var localBars = new List<WorldSliderSpawnedRuntimeInstance>();
            var localMarkers = new List<WorldSliderSpawnedRuntimeInstance>();

            try
            {
                if (!TryResolveRuntimeSpawner(out var resolvedSpawner) || resolvedSpawner == null)
                    return;
                var spawner = resolvedSpawner;

                var (minValue, maxValue) = ResolvePlayerRange();

                if (segmented.SpawnSegmentBars)
                {
                    var segmentBarsRoot = ResolveSegmentBarsRoot();
                    var barTemplate = WorldSliderRuntimeHelpers.ResolveRuntimeTemplate(segmented.SegmentBarTemplatePreset, _dynamicContext);
                    if (barTemplate == null)
                    {
                        Debug.LogWarning("[WorldSliderVisualizerService] Segment bar template is null.");
                    }
                    else
                    {
                        for (int i = 0; i < _segmentLayout.Boundaries.Count - 1; i++)
                        {
                            ct.ThrowIfCancellationRequested();
                            var startRawValue = _segmentLayout.Boundaries[i];
                            var endRawValue = _segmentLayout.Boundaries[i + 1];
                            var startNormalized = WorldSliderRuntimeHelpers.Normalize(startRawValue, minValue, maxValue);
                            var endNormalized = WorldSliderRuntimeHelpers.Normalize(endRawValue, minValue, maxValue);
                            var instance = await SpawnBarInstanceAsync(
                                spawner,
                                barTemplate,
                                segmentBarsRoot,
                                segmented.SegmentBarAnimationChannelTag,
                                WorldSliderSpawnUnitKind.SegmentBar,
                                i,
                                startRawValue,
                                endRawValue,
                                startNormalized,
                                endNormalized,
                                segmented.AllowPooling,
                                isSimpleRuntime: false,
                                ct);
                            if (instance != null)
                                localBars.Add(instance);
                        }
                    }
                }

                if (segmented.SpawnMarkers)
                {
                    var markersRoot = ResolveMarkersRoot();
                    var markerTemplate = WorldSliderRuntimeHelpers.ResolveRuntimeTemplate(segmented.MarkerTemplatePreset, _dynamicContext);
                    if (markerTemplate == null)
                    {
                        Debug.LogWarning("[WorldSliderVisualizerService] Marker template is null.");
                    }
                    else
                    {
                        for (int i = 0; i < _segmentLayout.Entries.Count; i++)
                        {
                            ct.ThrowIfCancellationRequested();
                            var entry = _segmentLayout.Entries[i];
                            var instance = await SpawnMarkerInstanceAsync(
                                spawner,
                                markerTemplate,
                                markersRoot,
                                entry.Index,
                                entry.RawValue,
                                entry.NormalizedValue,
                                segmented.AllowPooling,
                                ct);
                            if (instance != null)
                                localMarkers.Add(instance);
                        }
                    }
                }

                if (ct.IsCancellationRequested)
                    return;

                _segmentBars.Clear();
                _segmentBars.AddRange(localBars);
                _segmentMarkers.Clear();
                _segmentMarkers.AddRange(localMarkers);
                _segmentedReady = true;
                _visualDirty = true;

                await UniTask.SwitchToMainThread(ct);
                Tick();

                for (int i = 0; i < _segmentBars.Count; i++)
                    await ExecuteSpawnCommandsAsync(_segmentBars[i], segmented.OnSegmentBarSpawnCommands, ct);

                for (int i = 0; i < _segmentMarkers.Count; i++)
                    await ExecuteSpawnCommandsAsync(_segmentMarkers[i], segmented.OnMarkerSpawnCommands, ct);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WorldSliderVisualizerService] Segmented build failed: {ex.Message}");
            }
            finally
            {
                if (!_segmentedReady)
                {
                    ReleaseRuntimeInstances(localBars);
                    ReleaseRuntimeInstances(localMarkers);
                }
            }
        }

        async UniTask<WorldSliderSpawnedRuntimeInstance?> SpawnBarInstanceAsync(
            IAsyncSpawnerService spawner,
            BaseRuntimeTemplateSO template,
            Transform parent,
            string animationChannelTag,
            WorldSliderSpawnUnitKind unitKind,
            int unitIndex,
            float startRawValue,
            float endRawValue,
            float startNormalized,
            float endNormalized,
            bool allowPooling,
            bool isSimpleRuntime,
            CancellationToken ct)
        {
            var resolver = await WorldSliderRuntimeHelpers.SpawnRuntimeAsync(
                spawner,
                template,
                parent,
                _owner,
                allowPooling,
                ct);
            if (resolver == null)
                return null;

            WorldSliderRuntimeHelpers.ExtractSpawnedInfo(resolver, out var root, out var scopeNode, out _);
            if (root == null)
            {
                WorldSliderRuntimeHelpers.ReleaseSpawnedRuntime(resolver);
                return null;
            }

            if (!WorldSliderRuntimeHelpers.TryResolveRuntimeVisualTarget(
                    resolver,
                    root,
                    animationChannelTag,
                    out var visualTargetKind,
                    out var visualTransform,
                    out var spriteRenderer,
                    out var image) ||
                visualTargetKind == WorldSliderRuntimeVisualTargetKind.None ||
                visualTransform == null)
            {
                if (isSimpleRuntime)
                {
                    if (!_loggedSimpleRuntimeRendererMissing)
                    {
                        Debug.LogWarning("[WorldSliderVisualizerService] Simple runtime bar template requires an AnimationSprite target (SpriteRenderer/Image) or a fallback SpriteRenderer.");
                        _loggedSimpleRuntimeRendererMissing = true;
                    }
                }
                else if (!_loggedSegmentBarRendererMissing)
                {
                    Debug.LogWarning("[WorldSliderVisualizerService] Segment bar template requires an AnimationSprite target (SpriteRenderer/Image) or a fallback SpriteRenderer.");
                    _loggedSegmentBarRendererMissing = true;
                }

                WorldSliderRuntimeHelpers.ReleaseSpawnedRuntime(resolver);
                return null;
            }

            return new WorldSliderSpawnedRuntimeInstance
            {
                Root = root,
                Scope = scopeNode,
                Resolver = resolver,
                BasePose = new WorldSliderTransformPose(root),
                VisualTransform = visualTransform,
                VisualPose = new WorldSliderTransformPose(visualTransform),
                VisualTargetKind = visualTargetKind,
                SpriteRenderer = spriteRenderer,
                SpriteState = spriteRenderer != null ? new WorldSliderSpriteRenderState(spriteRenderer) : default,
                Image = image,
                ImageState = image != null ? new WorldSliderImageRenderState(image) : default,
                UnitKind = unitKind,
                UnitIndex = unitIndex,
                StartRawValue = startRawValue,
                EndRawValue = endRawValue,
                StartNormalized = startNormalized,
                EndNormalized = endNormalized,
            };
        }

        async UniTask<WorldSliderSpawnedRuntimeInstance?> SpawnMarkerInstanceAsync(
            IAsyncSpawnerService spawner,
            BaseRuntimeTemplateSO template,
            Transform parent,
            int entryIndex,
            float entryRawValue,
            float entryNormalized,
            bool allowPooling,
            CancellationToken ct)
        {
            var resolver = await WorldSliderRuntimeHelpers.SpawnRuntimeAsync(
                spawner,
                template,
                parent,
                _owner,
                allowPooling,
                ct);
            if (resolver == null)
                return null;

            WorldSliderRuntimeHelpers.ExtractSpawnedInfo(resolver, out var root, out var scopeNode, out _);
            if (root == null)
            {
                WorldSliderRuntimeHelpers.ReleaseSpawnedRuntime(resolver);
                return null;
            }

            return new WorldSliderSpawnedRuntimeInstance
            {
                Root = root,
                Scope = scopeNode,
                Resolver = resolver,
                BasePose = new WorldSliderTransformPose(root),
                UnitKind = WorldSliderSpawnUnitKind.Marker,
                UnitIndex = entryIndex,
                EntryIndex = entryIndex,
                EntryRawValue = entryRawValue,
                EntryNormalized = entryNormalized,
            };
        }

        async UniTask ExecuteSpawnCommandsAsync(
            WorldSliderSpawnedRuntimeInstance instance,
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
            var snapshot = _hasLastSnapshot ? _lastSnapshot : default;
            WorldSliderRuntimeHelpers.WriteCommonCommandVars(vars, snapshot, 0f, 0f);
            WorldSliderRuntimeHelpers.WriteSpawnCommandVars(vars, instance);

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
                Debug.LogError($"[WorldSliderVisualizerService] Spawn commands failed: {ex.Message}");
            }
        }

        void ApplySegmentedSnapshot(
            in WorldSliderOutputSnapshot snapshot,
            in WorldSliderAreaSnapshot areaSnapshot)
        {
            var displayedNormalizedValue = Mathf.Clamp01(snapshot.DisplayedNormalizedValue);
            var fillAxis = _visualizerPreset.Segmented.FillAxis;
            var originSide = _visualizerPreset.Segmented.OriginSide;
            var minorLength = WorldSliderRuntimeHelpers.ResolveAreaCrossLength(areaSnapshot, fillAxis);
            var spanScale = ResolveSegmentBarSpanScale();
            var useReachedStageSlotGeometry = _playerPreset.SegmentDisplayMode == WorldSliderSegmentDisplayMode.ReachedStageFloor;

            for (int i = 0; i < _segmentBars.Count; i++)
            {
                var instance = _segmentBars[i];
                if (instance.VisualTargetKind == WorldSliderRuntimeVisualTargetKind.None)
                    continue;

                Vector3 worldCenter;
                float majorLength;
                bool isVisible;

                if (useReachedStageSlotGeometry)
                {
                    WorldSliderRuntimeHelpers.ResolveIntervalBarGeometry(
                        areaSnapshot,
                        fillAxis,
                        originSide,
                        instance.StartNormalized,
                        instance.EndNormalized,
                        out worldCenter,
                        out majorLength);
                    isVisible = displayedNormalizedValue >= instance.EndNormalized - 0.0001f;
                }
                else
                {
                    var filledEndNormalized = Mathf.Clamp(displayedNormalizedValue, instance.StartNormalized, instance.EndNormalized);
                    WorldSliderRuntimeHelpers.ResolveIntervalBarGeometry(
                        areaSnapshot,
                        fillAxis,
                        originSide,
                        instance.StartNormalized,
                        filledEndNormalized,
                        out worldCenter,
                        out majorLength);
                    isVisible = majorLength > 0.0001f;
                }

                majorLength *= spanScale;

                var usedScaleFallback = WorldSliderRuntimeHelpers.ApplySpawnedBarGeometry(
                    instance,
                    areaSnapshot,
                    fillAxis,
                    worldCenter,
                    majorLength,
                    minorLength);

                if (usedScaleFallback && !_loggedSegmentScaleFallback)
                {
                    Debug.LogWarning("[WorldSliderVisualizerService] Segment bar SpriteRenderer drawMode is Simple. Falling back to transform scale.");
                    _loggedSegmentScaleFallback = true;
                }

                if (instance.Root != null)
                    instance.Root.gameObject.SetActive(isVisible);
            }

            for (int i = 0; i < _segmentMarkers.Count; i++)
            {
                var instance = _segmentMarkers[i];
                if (instance.Root == null)
                    continue;

                var worldPosition = WorldSliderRuntimeHelpers.ResolveMarkerWorldPosition(
                    areaSnapshot,
                    fillAxis,
                    originSide,
                    instance.EntryNormalized);
                WorldSliderRuntimeHelpers.ApplyMarkerTransform(
                    instance.Root,
                    instance.BasePose,
                    areaSnapshot,
                    worldPosition);
                instance.Root.gameObject.SetActive(displayedNormalizedValue >= instance.EntryNormalized);
            }
        }

        void ApplyHiddenState()
        {
            var simpleBarRenderer = _options.SimpleBarRenderer;
            if (simpleBarRenderer != null)
                simpleBarRenderer.enabled = false;

            if (_simpleRuntimeBar?.Root != null)
                _simpleRuntimeBar.Root.gameObject.SetActive(false);

            SetRuntimeInstancesActive(_segmentBars, false);
            SetRuntimeInstancesActive(_segmentMarkers, false);
        }

        bool TryResolveRuntimeSpawner(out IAsyncSpawnerService? spawner)
        {
            spawner = null;
            if (_spawnerRegistry == null)
            {
                Debug.LogWarning("[WorldSliderVisualizerService] ISceneSpawnerRegistry is not available.");
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
                Debug.LogWarning("[WorldSliderVisualizerService] RuntimeEntity spawner was not found.");
                return false;
            }

            spawner = resolvedSpawner.Spawner;
            return true;
        }

        Transform ResolveSimpleRuntimeParent()
        {
            if (_options.SegmentBarsRoot != null)
                return _options.SegmentBarsRoot;

            return _options.OwnerTransform;
        }

        Transform ResolveSegmentBarsRoot()
        {
            if (_options.SegmentBarsRoot != null)
                return _options.SegmentBarsRoot;

            return _options.OwnerTransform;
        }

        Transform ResolveMarkersRoot()
        {
            if (_options.SegmentMarkersRoot != null)
                return _options.SegmentMarkersRoot;

            return _options.OwnerTransform;
        }

        WorldSliderResolvedSegmentLayout? BuildSegmentLayout()
        {
            if (_dynamicContext == null)
                return null;

            var (minValue, maxValue) = ResolvePlayerRange();
            return WorldSliderRuntimeHelpers.BuildSegmentLayout(_visualizerPreset, _dynamicContext, minValue, maxValue);
        }

        (float MinValue, float MaxValue) ResolvePlayerRange()
        {
            var resolvedMinValue = ResolveFloat(_playerPreset.MinValue, 0f);
            var resolvedMaxValue = ResolveFloat(_playerPreset.MaxValue, 1f);
            return (Mathf.Min(resolvedMinValue, resolvedMaxValue), Mathf.Max(resolvedMinValue, resolvedMaxValue));
        }

        float ResolveFloat(DynamicValue<float> dynamicValue, float fallback)
        {
            if (_dynamicContext != null)
                return dynamicValue.GetOrDefault(_dynamicContext, fallback);

            return dynamicValue.GetOrDefaultWithoutContext(fallback);
        }

        float ResolveSimpleRuntimeBarSpanScale()
        {
            return Mathf.Max(0f, ResolveFloat(_visualizerPreset.Simple.RuntimeBarSpanScale, 1f));
        }

        float ResolveSegmentBarSpanScale()
        {
            return Mathf.Max(0f, ResolveFloat(_visualizerPreset.Segmented.BarSpanScale, 1f));
        }

        void ReleaseRuntimeInstances()
        {
            ReleaseRuntimeInstance(_simpleRuntimeBar);
            _simpleRuntimeBar = null;
            ReleaseRuntimeInstances(_segmentBars);
            ReleaseRuntimeInstances(_segmentMarkers);
            _segmentBars.Clear();
            _segmentMarkers.Clear();
            _simpleRuntimeReady = false;
            _segmentedReady = false;
        }

        static void ReleaseRuntimeInstances(List<WorldSliderSpawnedRuntimeInstance> instances)
        {
            for (int i = 0; i < instances.Count; i++)
                ReleaseRuntimeInstance(instances[i]);
        }

        static void SetRuntimeInstancesActive(List<WorldSliderSpawnedRuntimeInstance> instances, bool isActive)
        {
            for (int i = 0; i < instances.Count; i++)
            {
                var root = instances[i]?.Root;
                if (root != null)
                    root.gameObject.SetActive(isActive);
            }
        }

        static void ReleaseRuntimeInstance(WorldSliderSpawnedRuntimeInstance? instance)
        {
            if (instance == null)
                return;

            WorldSliderRuntimeHelpers.RestoreSpawnedRuntime(instance);
            WorldSliderRuntimeHelpers.ReleaseSpawnedRuntime(instance.Resolver);
        }

        bool TryResolveAreaSnapshot(IScopeNode scope, out WorldSliderAreaSnapshot snapshot)
        {
            var status = WorldSliderRuntimeHelpers.TryResolveAreaSnapshot(
                scope,
                _options.AreaActorSource,
                _options.AreaChannelTag,
                ref _areaActorSourceCache,
                out snapshot);
            if (status == WorldSliderAreaResolveStatus.Success)
            {
                _lastAreaResolveStatus = status;
                return true;
            }

            if (_lastAreaResolveStatus != status)
            {
                switch (status)
                {
                    case WorldSliderAreaResolveStatus.AreaScopeUnavailable:
                        Debug.LogWarning("[WorldSliderVisualizerService] Area scope could not be resolved.");
                        break;
                    case WorldSliderAreaResolveStatus.AreaHubUnavailable:
                        Debug.LogWarning("[WorldSliderVisualizerService] IAreaChannelHubService could not be resolved.");
                        break;
                    case WorldSliderAreaResolveStatus.AreaPlayerUnavailable:
                        Debug.LogWarning($"[WorldSliderVisualizerService] AreaChannel '{_options.AreaChannelTag}' could not be resolved.");
                        break;
                    case WorldSliderAreaResolveStatus.UnsupportedShape:
                        Debug.LogWarning("[WorldSliderVisualizerService] World slider only supports RectAreaShape.");
                        break;
                }
            }

            _lastAreaResolveStatus = status;
            return false;
        }

        void StopSpawn()
        {
            if (_spawnCts == null)
                return;

            _spawnCts.Cancel();
            _spawnCts.Dispose();
            _spawnCts = null;
        }

        ICommandRunner? ResolveRunner(IScopeNode scope)
        {
            if (scope.TryResolveInAncestors(out ICommandRunner? runner) && runner != null)
                return runner;

            scope.Resolver?.TryResolve(out runner);
            return runner;
        }

        static IVarStore ResolveVars(IScopeNode scope)
        {
            if (scope?.Resolver != null &&
                scope.Resolver.TryResolve<IVarStore>(out var vars) &&
                vars != null)
            {
                return vars;
            }

            return NullVarStore.Instance;
        }
    }
}
