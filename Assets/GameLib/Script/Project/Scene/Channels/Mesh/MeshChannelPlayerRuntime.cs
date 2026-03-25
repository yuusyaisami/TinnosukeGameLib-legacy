#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using Game.Commands.VNext;
using Game.Common;
using UnityEngine;
using VContainer;

namespace Game.Channel
{
    interface IMeshTrackPlayerRuntime
    {
        void Reset();
        bool TryEvaluate(MeshTrackEvaluationContext context, out MeshTrackPlayerEvaluation evaluation);
    }

    interface IMeshTrackVisualizerRuntime
    {
        bool TryBuildPaths(
            MeshTrackEvaluationContext context,
            MeshRegularTrackRuntimeState track,
            MeshTrackPlayerEvaluation evaluation,
            List<MeshRuntimePath> outputPaths);
    }

    interface IMeshTrackColliderRuntime
    {
        MeshPolygonTrackColliderPreset? Preset { get; }
    }

    interface IMeshSimulationTrackRuntime
    {
        void Reset();
        void Apply(MeshSimulationContext context, MeshSimulationTrackRuntimeState track, MeshCompositeDraft composite);
    }

    abstract class MeshTrackPlayerEvaluation
    {
    }

    sealed class MeshCenterLineEvaluation : MeshTrackPlayerEvaluation
    {
        public readonly List<Vector2> Points = new();
        public bool Closed;
        public bool SmoothPath;
        public int SmoothingSubdivisions;
    }

    sealed class MeshContourEvaluation : MeshTrackPlayerEvaluation
    {
        public readonly List<MeshRuntimePath> Paths = new();
    }

    sealed class MeshRuntimePath
    {
        public readonly List<Vector2> Points = new();
        public bool IsHole;

        public MeshRuntimePath Clone()
        {
            var clone = new MeshRuntimePath
            {
                IsHole = IsHole,
            };
            clone.Points.AddRange(Points);
            return clone;
        }
    }

    sealed class MeshTrackEvaluationContext
    {
        public readonly IScopeNode Scope;
        public readonly IDynamicContext DynamicContext;
        public readonly float DeltaTime;
        public readonly float TimeSeconds;
        public readonly int FrameIndex;

        public MeshTrackEvaluationContext(IScopeNode scope, IDynamicContext dynamicContext, float deltaTime, float timeSeconds, int frameIndex)
        {
            Scope = scope;
            DynamicContext = dynamicContext;
            DeltaTime = deltaTime;
            TimeSeconds = timeSeconds;
            FrameIndex = frameIndex;
        }
    }

    sealed class MeshSimulationContext
    {
        public readonly float DeltaTime;
        public readonly float TimeSeconds;
        public readonly IReadOnlyList<MeshHitContactInfo> Hits;

        public MeshSimulationContext(float deltaTime, float timeSeconds, IReadOnlyList<MeshHitContactInfo> hits)
        {
            DeltaTime = deltaTime;
            TimeSeconds = timeSeconds;
            Hits = hits;
        }
    }

    sealed class MeshRegularTrackRuntimeState
    {
        public string Key = string.Empty;
        public string Tag = string.Empty;
        public int Priority;
        public bool Enabled;
        public MeshTrackPlayerPresetBase PlayerPreset = new MeshLineTrackPlayerPreset();
        public MeshTrackVisualizerPresetBase VisualizerPreset = new MeshLineTrackVisualizerPreset();
        public MeshTrackColliderPresetBase ColliderPreset = new MeshPolygonTrackColliderPreset();
        public MeshTrackMaterialPreset MaterialPreset = new();
        public IMeshTrackPlayerRuntime PlayerRuntime = new MeshLineTrackPlayerRuntime(new MeshLineTrackPlayerPreset());
        public IMeshTrackVisualizerRuntime VisualizerRuntime = new MeshLineTrackVisualizerRuntime(new MeshLineTrackVisualizerPreset());
        public IMeshTrackColliderRuntime ColliderRuntime = new MeshPolygonTrackColliderRuntime(new MeshPolygonTrackColliderPreset());
    }

    sealed class MeshSimulationTrackRuntimeState
    {
        public string Key = string.Empty;
        public int Priority;
        public bool Enabled;
        public MeshSimulationPresetBase Preset = new MeshClayTransientSimulationPreset();
        public IMeshSimulationTrackRuntime Runtime = new MeshClayTransientSimulationRuntime(new MeshClayTransientSimulationPreset());
    }

    sealed class MeshRuntimeDefinitionState
    {
        public MeshRenderPipelinePreset RenderPipeline = new();
        public readonly Dictionary<string, MeshRegularTrackRuntimeState> RegularTracksByKey = new(StringComparer.Ordinal);
        public readonly Dictionary<string, MeshSimulationTrackRuntimeState> SimulationTracksByKey = new(StringComparer.Ordinal);

        public List<MeshRegularTrackRuntimeState> GetSortedRegularTracks()
        {
            return RegularTracksByKey.Values
                .OrderByDescending(static x => x.Priority)
                .ThenBy(static x => x.Key, StringComparer.Ordinal)
                .ToList();
        }

        public List<MeshSimulationTrackRuntimeState> GetSortedSimulationTracks()
        {
            return SimulationTracksByKey.Values
                .OrderByDescending(static x => x.Priority)
                .ThenBy(static x => x.Key, StringComparer.Ordinal)
                .ToList();
        }
    }

    sealed class MeshCompositeDraft
    {
        public string Tag = string.Empty;
        public int HighestPriority = int.MinValue;
        public MeshTrackMaterialPreset MaterialPreset = new();
        public MeshPolygonTrackColliderPreset ColliderPreset = new();
        public readonly List<MeshRuntimePath> Paths = new();
    }

    sealed class MeshMaterialLayerRuntime
    {
        sealed class LayerState
        {
            public string StableKey = string.Empty;
            public string ContextTag = "default";
            public int Priority;
            public MeshMaterialBlendMode BlendMode = MeshMaterialBlendMode.Override;
            public Color FromColor = Color.white;
            public Color ToColor = Color.white;
            public float StartedAt;
            public float DurationSeconds;
            public float LifetimeSeconds;
            public Ease Ease = Ease.Linear;
            public bool IsAuthoring;
        }

        readonly Dictionary<string, LayerState> _layers = new(StringComparer.Ordinal);
        readonly List<string> _removeBuffer = new();

        public void SyncAuthoringLayers(IReadOnlyList<MeshMaterialLayerPreset>? layers, float currentTime, Color currentBaseColor)
        {
            if (layers != null)
            {
                for (var i = 0; i < layers.Count; i++)
                {
                    var layer = layers[i];
                    if (layer == null || string.IsNullOrWhiteSpace(layer.StableKey))
                        continue;

                    if (!_layers.TryGetValue(layer.StableKey, out var state))
                    {
                        state = new LayerState
                        {
                            StableKey = layer.StableKey,
                            StartedAt = currentTime,
                            FromColor = currentBaseColor,
                            IsAuthoring = true,
                        };
                        _layers[layer.StableKey] = state;
                    }

                    state.ContextTag = string.IsNullOrWhiteSpace(layer.ContextTag) ? "default" : layer.ContextTag;
                    state.Priority = layer.Priority;
                    state.BlendMode = layer.BlendMode;
                    state.ToColor = layer.Color;
                    state.DurationSeconds = Mathf.Max(0f, layer.DurationSeconds);
                    state.LifetimeSeconds = layer.LifetimeSeconds;
                    state.Ease = layer.Ease;
                    state.IsAuthoring = true;
                }
            }

            foreach (var pair in _layers)
            {
                if (pair.Value.IsAuthoring)
                    pair.Value.IsAuthoring = false;
                else if (pair.Value.LifetimeSeconds >= 0f && currentTime - pair.Value.StartedAt >= pair.Value.LifetimeSeconds)
                    _removeBuffer.Add(pair.Key);
            }

            for (var i = 0; i < _removeBuffer.Count; i++)
                _layers.Remove(_removeBuffer[i]);
            _removeBuffer.Clear();
        }

        public void ClearContext(string contextTag)
        {
            foreach (var pair in _layers)
            {
                if (string.Equals(pair.Value.ContextTag, contextTag, StringComparison.Ordinal))
                    _removeBuffer.Add(pair.Key);
            }

            for (var i = 0; i < _removeBuffer.Count; i++)
                _layers.Remove(_removeBuffer[i]);
            _removeBuffer.Clear();
        }

        public Color Evaluate(Color baseColor, float currentTime)
        {
            var result = baseColor;
            if (_layers.Count == 0)
                return result;

            var ordered = _layers.Values.OrderBy(static x => x.Priority).ToList();
            for (var i = 0; i < ordered.Count; i++)
            {
                var layer = ordered[i];
                var weight = 1f;
                if (layer.DurationSeconds > 0f)
                {
                    var t = Mathf.Clamp01((currentTime - layer.StartedAt) / layer.DurationSeconds);
                    weight = DOVirtual.EasedValue(0f, 1f, t, layer.Ease);
                }

                switch (layer.BlendMode)
                {
                    case MeshMaterialBlendMode.Add:
                        result += layer.ToColor * weight;
                        break;

                    case MeshMaterialBlendMode.Multiply:
                        result = Color.Lerp(result, result * layer.ToColor, weight);
                        break;

                    default:
                        result = Color.Lerp(result, layer.ToColor, weight);
                        break;
                }
            }

            result.r = Mathf.Clamp01(result.r);
            result.g = Mathf.Clamp01(result.g);
            result.b = Mathf.Clamp01(result.b);
            result.a = Mathf.Clamp01(result.a);
            return result;
        }
    }

    sealed class MeshCompositeVisualObject : IDisposable
    {
        static readonly int ColorId = Shader.PropertyToID("_Color");

        readonly Transform _ownerTransform;
        readonly GameObject _rootObject;
        readonly MeshFilter _meshFilter;
        readonly MeshRenderer _meshRenderer;
        readonly PolygonCollider2D _polygonCollider;
        readonly MeshChannelColliderRelay _hitRelay;
        readonly Mesh _mesh;
        readonly MaterialPropertyBlock _propertyBlock = new();
        readonly MeshMaterialLayerRuntime _materialRuntime = new();
        readonly List<Vector2[]> _lastPaths = new();
        readonly List<MeshHitContactInfo> _hitBuffer = new();

        int _lastSyncFrame = int.MinValue;

        public MeshCompositeVisualObject(string name, Transform ownerTransform)
        {
            _ownerTransform = ownerTransform;
            _rootObject = new GameObject(name);
            _rootObject.transform.SetParent(ownerTransform, false);

            _meshFilter = _rootObject.AddComponent<MeshFilter>();
            _meshRenderer = _rootObject.AddComponent<MeshRenderer>();
            _polygonCollider = _rootObject.AddComponent<PolygonCollider2D>();
            _polygonCollider.enabled = false;
            _hitRelay = _rootObject.AddComponent<MeshChannelColliderRelay>();

            _mesh = new Mesh
            {
                name = $"{name}.Mesh",
            };
            _mesh.MarkDynamic();
            _meshFilter.sharedMesh = _mesh;
        }

        public void CaptureHits(List<MeshHitContactInfo> output)
        {
            _hitRelay.CaptureHits(output);
        }

        public void Apply(
            MeshCompositeDraft draft,
            MeshRenderPipelinePreset pipeline,
            Material fallbackMaterial,
            float deltaTime,
            float timeSeconds,
            int frameIndex)
        {
            if (draft.Paths.Count == 0)
            {
                Clear();
                return;
            }

            var colliderPreset = draft.ColliderPreset;
            var materialPreset = draft.MaterialPreset;
            var localPaths = MeshChannelGeometryUtility.ConvertWorldPathsToLocal(_ownerTransform, draft.Paths);
            var simplifiedPaths = MeshChannelGeometryUtility.SimplifyPaths(localPaths, colliderPreset.Sync);

            var shouldSync = MeshChannelGeometryUtility.ShouldSyncPaths(
                _lastPaths,
                simplifiedPaths,
                colliderPreset.Sync,
                frameIndex,
                _lastSyncFrame);

            if (shouldSync)
            {
                SyncColliderPaths(simplifiedPaths);
                _lastSyncFrame = frameIndex;
            }

            _polygonCollider.enabled = colliderPreset.SyncPolygonToCollider || colliderPreset.EnableHitCapture;
            _materialRuntime.SyncAuthoringLayers(materialPreset.Layers, timeSeconds, materialPreset.BaseColor);

            var color = _materialRuntime.Evaluate(materialPreset.BaseColor, timeSeconds);
            ApplyMaterial(pipeline, materialPreset, fallbackMaterial, color);

            if (pipeline.EnableVisual)
                RebuildMeshFromCollider();
            else
                _mesh.Clear();
        }

        public void Clear()
        {
            _mesh.Clear();
            _polygonCollider.pathCount = 0;
            _polygonCollider.enabled = false;
            _meshRenderer.enabled = false;
            _hitRelay.ClearAll();
            _lastPaths.Clear();
            _lastSyncFrame = int.MinValue;
        }

        void ApplyMaterial(MeshRenderPipelinePreset pipeline, MeshTrackMaterialPreset materialPreset, Material fallbackMaterial, Color color)
        {
            _meshRenderer.enabled = true;
            _meshRenderer.sharedMaterial = materialPreset.Material != null ? materialPreset.Material : fallbackMaterial;
            _meshRenderer.sortingOrder = pipeline.SortingOrder + materialPreset.SortingOrderOffset;
            _propertyBlock.Clear();
            _propertyBlock.SetColor(ColorId, color);
            _meshRenderer.SetPropertyBlock(_propertyBlock);
        }

        void SyncColliderPaths(List<Vector2[]> paths)
        {
            _polygonCollider.pathCount = paths.Count;
            _lastPaths.Clear();

            for (var i = 0; i < paths.Count; i++)
            {
                _polygonCollider.SetPath(i, paths[i]);
                var copy = new Vector2[paths[i].Length];
                Array.Copy(paths[i], copy, copy.Length);
                _lastPaths.Add(copy);
            }
        }

        void RebuildMeshFromCollider()
        {
            var generated = _polygonCollider.CreateMesh(false, false);

            if (generated != null)
            {
                MeshChannelGeometryUtility.CopyMesh(generated, _mesh);
                UnityEngine.Object.Destroy(generated);
                return;
            }

            MeshChannelGeometryUtility.BuildFallbackMesh(_lastPaths, _mesh);
        }

        public void Dispose()
        {
            if (_mesh != null)
                UnityEngine.Object.Destroy(_mesh);
            if (_rootObject != null)
                UnityEngine.Object.Destroy(_rootObject);
        }
    }

    public sealed class MeshChannelPlayerRuntime : IMeshChannelPlayerRuntime
    {
        readonly string _tag;
        readonly DynamicValue<MeshDefinitionPreset> _definitionSource;
        readonly IScopeNode _scope;
        readonly Transform _ownerTransform;
        readonly Material? _defaultMaterial;

        readonly Dictionary<string, MeshCompositeVisualObject> _visualsByTag = new(StringComparer.Ordinal);
        readonly List<string> _trackKeys = new();
        readonly Dictionary<string, List<MeshHitContactInfo>> _hitsByCompositeTag = new(StringComparer.Ordinal);
        readonly List<MeshHitContactInfo> _hitBuffer = new();
        readonly List<MeshRuntimePath> _sharedPathBuffer = new();
        readonly List<MeshTrackRegularTrackContributor> _contributors = new();

        MeshRuntimeDefinitionState _baseState = new();
        MeshRuntimeDefinitionState _currentState = new();
        IVarStore _vars = NullVarStore.Instance;
        Material? _fallbackMaterial;
        bool _acquired;
        float _timeSeconds;

        public string Tag => _tag;
        public bool IsActive { get; private set; }
        public IReadOnlyList<string> TrackKeys => _trackKeys;

        public MeshChannelPlayerRuntime(
            string tag,
            DynamicValue<MeshDefinitionPreset> definitionSource,
            IScopeNode scope,
            Transform ownerTransform,
            Material? defaultMaterial)
        {
            _tag = string.IsNullOrWhiteSpace(tag) ? "default" : tag;
            _definitionSource = definitionSource;
            _scope = scope;
            _ownerTransform = ownerTransform;
            _defaultMaterial = defaultMaterial;
        }

        public void OnAcquire()
        {
            _vars = ResolveVars(_scope);
            var authoredPreset = ResolveDefinition(_definitionSource, CreateDynamicContext(), new MeshDefinitionPreset());
            _baseState = BuildRuntimeState(authoredPreset);
            _currentState = CloneRuntimeState(_baseState);
            RefreshTrackKeyCache();
            _timeSeconds = 0f;
            IsActive = authoredPreset.EnabledOnAcquire;
            _acquired = true;
        }

        public void OnRelease()
        {
            _acquired = false;
            IsActive = false;
            _timeSeconds = 0f;
            _baseState = new MeshRuntimeDefinitionState();
            _currentState = new MeshRuntimeDefinitionState();
            _trackKeys.Clear();
            foreach (var visual in _visualsByTag.Values)
                visual.Clear();
        }

        public void Dispose()
        {
            OnRelease();
            foreach (var pair in _visualsByTag)
                pair.Value.Dispose();
            _visualsByTag.Clear();
            if (_fallbackMaterial != null)
            {
                UnityEngine.Object.Destroy(_fallbackMaterial);
                _fallbackMaterial = null;
            }
        }

        public void Tick(int frameIndex, float deltaTime)
        {
            if (!_acquired || !IsActive)
                return;

            _timeSeconds += Mathf.Max(0f, deltaTime);
            CaptureCurrentHits();

            var context = new MeshTrackEvaluationContext(
                _scope,
                CreateDynamicContext(),
                Mathf.Max(0f, deltaTime),
                _timeSeconds,
                frameIndex);

            _contributors.Clear();
            var regularTracks = _currentState.GetSortedRegularTracks();
            for (var i = 0; i < regularTracks.Count; i++)
            {
                var track = regularTracks[i];
                if (!track.Enabled)
                    continue;

                if (!track.PlayerRuntime.TryEvaluate(context, out var evaluation) || evaluation == null)
                    continue;

                _sharedPathBuffer.Clear();
                if (!track.VisualizerRuntime.TryBuildPaths(context, track, evaluation, _sharedPathBuffer))
                    continue;

                if (_sharedPathBuffer.Count == 0)
                    continue;

                _contributors.Add(new MeshTrackRegularTrackContributor(track, _sharedPathBuffer));
                _sharedPathBuffer.Clear();
            }

            var composites = BlendContributors();
            ApplySimulation(deltaTime, composites);
            RenderComposites(composites, deltaTime, frameIndex);
        }

        public bool SwapRootDefinition(MeshDefinitionPreset preset)
        {
            var authored = preset?.CreateRuntimeCopy() ?? new MeshDefinitionPreset();
            _baseState = BuildRuntimeState(authored);
            _currentState = CloneRuntimeState(_baseState);
            RefreshTrackKeyCache();
            return true;
        }

        public bool SwapTrackDefinition(string key, MeshTrackDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;

            var runtime = ResolveRegularTrack(definition?.CreateRuntimeCopy() ?? new MeshTrackDefinition());
            runtime.Key = key;
            _currentState.RegularTracksByKey[key] = runtime;
            RefreshTrackKeyCache();
            return true;
        }

        public bool MutateTrackVisualizer(string key, MeshTrackVisualizerRuntimeMutation mutation)
        {
            if (!_currentState.RegularTracksByKey.TryGetValue(key, out var track) || mutation == null || !mutation.HasAnyMutation())
                return false;

            if (mutation.ReplacePreset)
                track.VisualizerPreset = ResolveVisualizerPreset(mutation.Preset, CreateDynamicContext(), new MeshLineTrackVisualizerPreset());

            if (track.VisualizerPreset is MeshLineTrackVisualizerPreset line)
            {
                if (mutation.ApplyWidth)
                    line.BaseWidth = mutation.BaseWidth;
                if (mutation.ApplyTaper)
                {
                    line.HeadTaperNormalized = mutation.HeadTaperNormalized;
                    line.TailTaperNormalized = mutation.TailTaperNormalized;
                }
                if (mutation.ApplyWave)
                {
                    line.WaveAmplitude = mutation.WaveAmplitude;
                    line.WaveLength = mutation.WaveLength;
                    line.WavePhase = mutation.WavePhase;
                    line.WaveScrollSpeed = mutation.WaveScrollSpeed;
                }
            }

            track.VisualizerRuntime = CreateVisualizerRuntime(track.VisualizerPreset);
            return true;
        }

        public bool MutateTrackPlayer(string key, MeshTrackPlayerRuntimeMutation mutation)
        {
            if (!_currentState.RegularTracksByKey.TryGetValue(key, out var track) || mutation == null || !mutation.HasAnyMutation())
                return false;

            if (mutation.ReplacePreset)
                track.PlayerPreset = ResolvePlayerPreset(mutation.Preset, CreateDynamicContext(), new MeshLineTrackPlayerPreset());

            if (track.PlayerPreset is MeshLineTrackPlayerPreset line && mutation.ApplyPoints)
                line.Points = new List<DynamicValue<Vector3>>(mutation.Points);

            if (track.PlayerPreset is MeshTrailTrackPlayerPreset trail && mutation.ApplyTrailConfig)
            {
                trail.TargetPosition = mutation.TargetPosition;
                trail.DurationSeconds = mutation.DurationSeconds;
                trail.MinDistance = mutation.MinDistance;
                trail.MinTime = mutation.MinTime;
                trail.MaxPoints = mutation.MaxPoints;
            }

            if (track.PlayerPreset is MeshAreaFillTrackPlayerPreset area && mutation.ApplyAreaTag)
                area.AreaTag = mutation.AreaTag;

            track.PlayerRuntime = CreatePlayerRuntime(track.PlayerPreset);
            return true;
        }

        public bool MutateTrackCollider(string key, MeshTrackColliderRuntimeMutation mutation)
        {
            if (!_currentState.RegularTracksByKey.TryGetValue(key, out var track) || mutation == null || !mutation.HasAnyMutation())
                return false;

            if (mutation.ReplacePreset)
                track.ColliderPreset = ResolveColliderPreset(mutation.Preset, CreateDynamicContext(), new MeshPolygonTrackColliderPreset());

            if (track.ColliderPreset is MeshPolygonTrackColliderPreset polygon)
            {
                if (mutation.ApplySyncToggle)
                    polygon.SyncPolygonToCollider = mutation.SyncPolygonToCollider;
                if (mutation.ApplyHitCaptureToggle)
                    polygon.EnableHitCapture = mutation.EnableHitCapture;
                if (mutation.ApplySyncSettings)
                    polygon.Sync = mutation.Sync?.CreateRuntimeCopy() ?? new MeshPolygonSyncSettings();
            }

            track.ColliderRuntime = CreateColliderRuntime(track.ColliderPreset);
            return true;
        }

        public bool MutateTrackMaterial(string key, MeshTrackMaterialRuntimeMutation mutation)
        {
            if (!_currentState.RegularTracksByKey.TryGetValue(key, out var track) || mutation == null || !mutation.HasAnyMutation())
                return false;

            if (mutation.ReplacePreset)
                track.MaterialPreset = ResolveMaterialPreset(mutation.Preset, CreateDynamicContext(), new MeshTrackMaterialPreset());

            if (mutation.ApplyBaseColor)
                track.MaterialPreset.BaseColor = mutation.BaseColor;
            if (mutation.ApplySortingOrderOffset)
                track.MaterialPreset.SortingOrderOffset = mutation.SortingOrderOffset;

            return true;
        }

        public bool MutateSimulationTrack(string key, MeshSimulationTrackRuntimeMutation mutation)
        {
            if (!_currentState.SimulationTracksByKey.TryGetValue(key, out var track) || mutation == null || !mutation.HasAnyMutation())
                return false;

            if (mutation.ReplacePreset)
                track.Preset = ResolveSimulationPreset(mutation.Preset, CreateDynamicContext(), new MeshClayTransientSimulationPreset());
            if (mutation.ApplyPriority)
                track.Priority = mutation.Priority;
            if (mutation.ApplyEnabled)
                track.Enabled = mutation.Enabled;

            track.Runtime = CreateSimulationRuntime(track.Preset);
            return true;
        }

        public bool ResetRuntimeOverrides(
            bool resetVisualizer,
            bool resetPlayer,
            bool resetCollider,
            bool resetMaterial,
            bool resetSimulation)
        {
            var resetAnything = resetVisualizer || resetPlayer || resetCollider || resetMaterial || resetSimulation;
            if (!resetAnything)
                return false;

            var resetState = CloneRuntimeState(_baseState);

            foreach (var pair in resetState.RegularTracksByKey)
            {
                if (!_currentState.RegularTracksByKey.TryGetValue(pair.Key, out var current))
                {
                    _currentState.RegularTracksByKey[pair.Key] = pair.Value;
                    continue;
                }

                if (resetVisualizer)
                {
                    current.VisualizerPreset = pair.Value.VisualizerPreset;
                    current.VisualizerRuntime = pair.Value.VisualizerRuntime;
                }
                if (resetPlayer)
                {
                    current.PlayerPreset = pair.Value.PlayerPreset;
                    current.PlayerRuntime = pair.Value.PlayerRuntime;
                }
                if (resetCollider)
                {
                    current.ColliderPreset = pair.Value.ColliderPreset;
                    current.ColliderRuntime = pair.Value.ColliderRuntime;
                }
                if (resetMaterial)
                    current.MaterialPreset = pair.Value.MaterialPreset;
            }

            if (resetSimulation)
                _currentState.SimulationTracksByKey.Clear();

            if (resetSimulation)
            {
                foreach (var pair in resetState.SimulationTracksByKey)
                    _currentState.SimulationTracksByKey[pair.Key] = pair.Value;
            }

            return true;
        }

        public bool SetTrackEnabled(string key, bool enabled)
        {
            if (_currentState.RegularTracksByKey.TryGetValue(key, out var track))
            {
                track.Enabled = enabled;
                return true;
            }

            if (_currentState.SimulationTracksByKey.TryGetValue(key, out var simulationTrack))
            {
                simulationTrack.Enabled = enabled;
                return true;
            }

            return false;
        }

        void CaptureCurrentHits()
        {
            _hitsByCompositeTag.Clear();
            foreach (var pair in _visualsByTag)
            {
                pair.Value.CaptureHits(_hitBuffer);
                if (_hitBuffer.Count == 0)
                    continue;

                var list = new List<MeshHitContactInfo>(_hitBuffer);
                _hitsByCompositeTag[pair.Key] = list;
            }
        }

        Dictionary<string, MeshCompositeDraft> BlendContributors()
        {
            var composites = new Dictionary<string, MeshCompositeDraft>(StringComparer.Ordinal);
            for (var i = 0; i < _contributors.Count; i++)
            {
                var contributor = _contributors[i];
                var tag = string.IsNullOrWhiteSpace(contributor.Track.Tag) ? contributor.Track.Key : contributor.Track.Tag;
                if (!composites.TryGetValue(tag, out var composite))
                {
                    composite = new MeshCompositeDraft
                    {
                        Tag = tag,
                    };
                    composites[tag] = composite;
                }

                for (var p = 0; p < contributor.Paths.Count; p++)
                    composite.Paths.Add(contributor.Paths[p].Clone());

                if (contributor.Track.Priority >= composite.HighestPriority)
                {
                    composite.HighestPriority = contributor.Track.Priority;
                    composite.MaterialPreset = contributor.Track.MaterialPreset.CreateRuntimeCopy();
                    composite.ColliderPreset = contributor.Track.ColliderPreset is MeshPolygonTrackColliderPreset polygon
                        ? (MeshPolygonTrackColliderPreset)polygon.CreateRuntimeCopy()
                        : new MeshPolygonTrackColliderPreset();
                }
            }

            return composites;
        }

        void ApplySimulation(float deltaTime, Dictionary<string, MeshCompositeDraft> composites)
        {
            if (composites.Count == 0)
                return;

            var simulations = _currentState.GetSortedSimulationTracks();
            for (var i = 0; i < simulations.Count; i++)
            {
                var track = simulations[i];
                if (!track.Enabled)
                    continue;

                foreach (var pair in composites)
                {
                    var hits = _hitsByCompositeTag.TryGetValue(pair.Key, out var capturedHits)
                        ? (IReadOnlyList<MeshHitContactInfo>)capturedHits
                        : Array.Empty<MeshHitContactInfo>();

                    var context = new MeshSimulationContext(deltaTime, _timeSeconds, hits);
                    track.Runtime.Apply(context, track, pair.Value);
                }
            }
        }

        void RenderComposites(Dictionary<string, MeshCompositeDraft> composites, float deltaTime, int frameIndex)
        {
            foreach (var pair in composites)
            {
                if (!_visualsByTag.TryGetValue(pair.Key, out var visual))
                {
                    visual = new MeshCompositeVisualObject($"MeshComposite.{_tag}.{pair.Key}", _ownerTransform);
                    _visualsByTag[pair.Key] = visual;
                }

                visual.Apply(
                    pair.Value,
                    _currentState.RenderPipeline,
                    GetOrCreateFallbackMaterial(),
                    deltaTime,
                    _timeSeconds,
                    frameIndex);
            }

            var removeKeys = ListPool<string>.Get();
            foreach (var pair in _visualsByTag)
            {
                if (composites.ContainsKey(pair.Key))
                    continue;

                pair.Value.Clear();
                removeKeys.Add(pair.Key);
            }

            for (var i = 0; i < removeKeys.Count; i++)
            {
                var key = removeKeys[i];
                if (_visualsByTag.TryGetValue(key, out var visual))
                {
                    visual.Dispose();
                    _visualsByTag.Remove(key);
                }
            }

            ListPool<string>.Release(removeKeys);
        }

        MeshRuntimeDefinitionState BuildRuntimeState(MeshDefinitionPreset preset)
        {
            var state = new MeshRuntimeDefinitionState
            {
                RenderPipeline = ResolveRenderPipeline(preset.RenderPipeline, CreateDynamicContext(), new MeshRenderPipelinePreset()),
            };

            for (var i = 0; i < preset.RegularTracks.Count; i++)
            {
                var runtime = ResolveRegularTrack(preset.RegularTracks[i]);
                state.RegularTracksByKey[runtime.Key] = runtime;
            }

            for (var i = 0; i < preset.SimulationTracks.Count; i++)
            {
                var runtime = ResolveSimulationTrack(preset.SimulationTracks[i]);
                state.SimulationTracksByKey[runtime.Key] = runtime;
            }

            return state;
        }

        MeshRuntimeDefinitionState CloneRuntimeState(MeshRuntimeDefinitionState source)
        {
            var clone = new MeshRuntimeDefinitionState
            {
                RenderPipeline = source.RenderPipeline.CreateRuntimeCopy(),
            };

            foreach (var pair in source.RegularTracksByKey)
            {
                var src = pair.Value;
                var copy = new MeshRegularTrackRuntimeState
                {
                    Key = src.Key,
                    Tag = src.Tag,
                    Priority = src.Priority,
                    Enabled = src.Enabled,
                    PlayerPreset = src.PlayerPreset.CreateRuntimeCopy(),
                    VisualizerPreset = src.VisualizerPreset.CreateRuntimeCopy(),
                    ColliderPreset = src.ColliderPreset.CreateRuntimeCopy(),
                    MaterialPreset = src.MaterialPreset.CreateRuntimeCopy(),
                };
                copy.PlayerRuntime = CreatePlayerRuntime(copy.PlayerPreset);
                copy.VisualizerRuntime = CreateVisualizerRuntime(copy.VisualizerPreset);
                copy.ColliderRuntime = CreateColliderRuntime(copy.ColliderPreset);
                clone.RegularTracksByKey[pair.Key] = copy;
            }

            foreach (var pair in source.SimulationTracksByKey)
            {
                var src = pair.Value;
                var copy = new MeshSimulationTrackRuntimeState
                {
                    Key = src.Key,
                    Priority = src.Priority,
                    Enabled = src.Enabled,
                    Preset = src.Preset.CreateRuntimeCopy(),
                };
                copy.Runtime = CreateSimulationRuntime(copy.Preset);
                clone.SimulationTracksByKey[pair.Key] = copy;
            }

            return clone;
        }

        MeshRegularTrackRuntimeState ResolveRegularTrack(MeshTrackDefinition authored)
        {
            var key = string.IsNullOrWhiteSpace(authored.Key) ? Guid.NewGuid().ToString("N") : authored.Key.Trim();
            var runtime = new MeshRegularTrackRuntimeState
            {
                Key = key,
                Tag = string.IsNullOrWhiteSpace(authored.Tag) ? key : authored.Tag.Trim(),
                Priority = authored.Priority,
                Enabled = authored.Enabled,
                PlayerPreset = ResolvePlayerPreset(authored.Player, CreateDynamicContext(), new MeshLineTrackPlayerPreset()),
                VisualizerPreset = ResolveVisualizerPreset(authored.Visualizer, CreateDynamicContext(), new MeshLineTrackVisualizerPreset()),
                ColliderPreset = ResolveColliderPreset(authored.Collider, CreateDynamicContext(), new MeshPolygonTrackColliderPreset()),
                MaterialPreset = ResolveMaterialPreset(authored.Material, CreateDynamicContext(), new MeshTrackMaterialPreset()),
            };

            runtime.PlayerRuntime = CreatePlayerRuntime(runtime.PlayerPreset);
            runtime.VisualizerRuntime = CreateVisualizerRuntime(runtime.VisualizerPreset);
            runtime.ColliderRuntime = CreateColliderRuntime(runtime.ColliderPreset);
            return runtime;
        }

        MeshSimulationTrackRuntimeState ResolveSimulationTrack(MeshSimulationTrackDefinition authored)
        {
            var key = string.IsNullOrWhiteSpace(authored.Key) ? Guid.NewGuid().ToString("N") : authored.Key.Trim();
            var runtime = new MeshSimulationTrackRuntimeState
            {
                Key = key,
                Priority = authored.Priority,
                Enabled = authored.Enabled,
                Preset = ResolveSimulationPreset(authored.Preset, CreateDynamicContext(), new MeshClayTransientSimulationPreset()),
            };
            runtime.Runtime = CreateSimulationRuntime(runtime.Preset);
            return runtime;
        }

        IDynamicContext CreateDynamicContext()
        {
            return new SimpleDynamicContext(_vars, _scope);
        }

        void RefreshTrackKeyCache()
        {
            _trackKeys.Clear();
            foreach (var pair in _currentState.RegularTracksByKey)
                _trackKeys.Add(pair.Key);
            foreach (var pair in _currentState.SimulationTracksByKey)
                _trackKeys.Add(pair.Key);
            _trackKeys.Sort(StringComparer.Ordinal);
        }

        Material GetOrCreateFallbackMaterial()
        {
            if (_currentState.RenderPipeline.DefaultMaterial != null)
                return _currentState.RenderPipeline.DefaultMaterial;
            if (_defaultMaterial != null)
                return _defaultMaterial;
            if (_fallbackMaterial != null)
                return _fallbackMaterial;

            var shader = Shader.Find("Sprites/Default");
            _fallbackMaterial = new Material(shader)
            {
                name = $"MeshChannel.{_tag}.FallbackMaterial",
                hideFlags = HideFlags.DontSave,
            };
            return _fallbackMaterial;
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

        static MeshDefinitionPreset ResolveDefinition(DynamicValue<MeshDefinitionPreset> value, IDynamicContext context, MeshDefinitionPreset fallback)
        {
            if (value.TryGet(context, out MeshDefinitionPreset? preset) && preset != null)
                return preset.CreateRuntimeCopy();
            return fallback.CreateRuntimeCopy();
        }

        static MeshRenderPipelinePreset ResolveRenderPipeline(DynamicValue<MeshRenderPipelinePreset> value, IDynamicContext context, MeshRenderPipelinePreset fallback)
        {
            if (value.TryGet(context, out MeshRenderPipelinePreset? preset) && preset != null)
                return preset.CreateRuntimeCopy();
            return fallback.CreateRuntimeCopy();
        }

        static MeshTrackPlayerPresetBase ResolvePlayerPreset(DynamicValue<MeshTrackPlayerPresetBase> value, IDynamicContext context, MeshTrackPlayerPresetBase fallback)
        {
            if (value.TryGet(context, out MeshTrackPlayerPresetBase? preset) && preset != null)
                return preset.CreateRuntimeCopy();
            return fallback.CreateRuntimeCopy();
        }

        static MeshTrackVisualizerPresetBase ResolveVisualizerPreset(DynamicValue<MeshTrackVisualizerPresetBase> value, IDynamicContext context, MeshTrackVisualizerPresetBase fallback)
        {
            if (value.TryGet(context, out MeshTrackVisualizerPresetBase? preset) && preset != null)
                return preset.CreateRuntimeCopy();
            return fallback.CreateRuntimeCopy();
        }

        static MeshTrackColliderPresetBase ResolveColliderPreset(DynamicValue<MeshTrackColliderPresetBase> value, IDynamicContext context, MeshTrackColliderPresetBase fallback)
        {
            if (value.TryGet(context, out MeshTrackColliderPresetBase? preset) && preset != null)
                return preset.CreateRuntimeCopy();
            return fallback.CreateRuntimeCopy();
        }

        static MeshTrackMaterialPreset ResolveMaterialPreset(DynamicValue<MeshTrackMaterialPreset> value, IDynamicContext context, MeshTrackMaterialPreset fallback)
        {
            if (value.TryGet(context, out MeshTrackMaterialPreset? preset) && preset != null)
                return preset.CreateRuntimeCopy();
            return fallback.CreateRuntimeCopy();
        }

        static MeshSimulationPresetBase ResolveSimulationPreset(DynamicValue<MeshSimulationPresetBase> value, IDynamicContext context, MeshSimulationPresetBase fallback)
        {
            if (value.TryGet(context, out MeshSimulationPresetBase? preset) && preset != null)
                return preset.CreateRuntimeCopy();
            return fallback.CreateRuntimeCopy();
        }

        static IMeshTrackPlayerRuntime CreatePlayerRuntime(MeshTrackPlayerPresetBase preset)
        {
            return preset switch
            {
                MeshTrailTrackPlayerPreset trail => new MeshTrailTrackPlayerRuntime(trail),
                MeshAreaFillTrackPlayerPreset area => new MeshAreaFillTrackPlayerRuntime(area),
                MeshLineTrackPlayerPreset line => new MeshLineTrackPlayerRuntime(line),
                _ => new MeshLineTrackPlayerRuntime(new MeshLineTrackPlayerPreset()),
            };
        }

        static IMeshTrackVisualizerRuntime CreateVisualizerRuntime(MeshTrackVisualizerPresetBase preset)
        {
            return preset switch
            {
                MeshAreaFillTrackVisualizerPreset area => new MeshAreaFillTrackVisualizerRuntime(area),
                MeshLineTrackVisualizerPreset line => new MeshLineTrackVisualizerRuntime(line),
                _ => new MeshLineTrackVisualizerRuntime(new MeshLineTrackVisualizerPreset()),
            };
        }

        static IMeshTrackColliderRuntime CreateColliderRuntime(MeshTrackColliderPresetBase preset)
        {
            return preset switch
            {
                MeshPolygonTrackColliderPreset polygon => new MeshPolygonTrackColliderRuntime(polygon),
                _ => new MeshPolygonTrackColliderRuntime(new MeshPolygonTrackColliderPreset()),
            };
        }

        static IMeshSimulationTrackRuntime CreateSimulationRuntime(MeshSimulationPresetBase preset)
        {
            return preset switch
            {
                MeshClayPersistentSimulationPreset persistent => new MeshClayPersistentSimulationRuntime(persistent),
                MeshFluidSimulationPreset fluid => new MeshFluidSimulationRuntime(fluid),
                MeshClayTransientSimulationPreset clay => new MeshClayTransientSimulationRuntime(clay),
                _ => new MeshClayTransientSimulationRuntime(new MeshClayTransientSimulationPreset()),
            };
        }
    }

    readonly struct MeshTrackRegularTrackContributor
    {
        public readonly MeshRegularTrackRuntimeState Track;
        public readonly List<MeshRuntimePath> Paths;

        public MeshTrackRegularTrackContributor(MeshRegularTrackRuntimeState track, List<MeshRuntimePath> sourcePaths)
        {
            Track = track;
            Paths = new List<MeshRuntimePath>(sourcePaths.Count);
            for (var i = 0; i < sourcePaths.Count; i++)
                Paths.Add(sourcePaths[i].Clone());
        }
    }

    sealed class MeshLineTrackPlayerRuntime : IMeshTrackPlayerRuntime
    {
        readonly MeshLineTrackPlayerPreset _preset;

        public MeshLineTrackPlayerRuntime(MeshLineTrackPlayerPreset preset)
        {
            _preset = preset;
        }

        public void Reset()
        {
        }

        public bool TryEvaluate(MeshTrackEvaluationContext context, out MeshTrackPlayerEvaluation evaluation)
        {
            var result = new MeshCenterLineEvaluation
            {
                Closed = _preset.Closed,
                SmoothPath = _preset.SmoothPath,
                SmoothingSubdivisions = _preset.SmoothingSubdivisions,
            };

            for (var i = 0; i < _preset.Points.Count; i++)
            {
                if (_preset.Points[i].TryGet(context.DynamicContext, out Vector3 worldPoint))
                    result.Points.Add(new Vector2(worldPoint.x, worldPoint.y));
            }

            evaluation = result;
            return result.Points.Count >= 2;
        }
    }

    sealed class MeshTrailTrackPlayerRuntime : IMeshTrackPlayerRuntime
    {
        readonly MeshTrailTrackPlayerPreset _preset;
        readonly List<(Vector2 Point, float Time)> _samples = new();
        float _lastCaptureTime = float.MinValue;

        public MeshTrailTrackPlayerRuntime(MeshTrailTrackPlayerPreset preset)
        {
            _preset = preset;
        }

        public void Reset()
        {
            _samples.Clear();
            _lastCaptureTime = float.MinValue;
        }

        public bool TryEvaluate(MeshTrackEvaluationContext context, out MeshTrackPlayerEvaluation evaluation)
        {
            if (!_preset.TargetPosition.TryGet(context.DynamicContext, out Vector3 worldPoint))
            {
                evaluation = new MeshCenterLineEvaluation();
                return false;
            }

            var point = new Vector2(worldPoint.x, worldPoint.y);
            var shouldCapture = _samples.Count == 0;
            if (!shouldCapture)
            {
                var last = _samples[_samples.Count - 1];
                shouldCapture = (point - last.Point).sqrMagnitude >= _preset.MinDistance * _preset.MinDistance;
                if (!shouldCapture)
                    shouldCapture = context.TimeSeconds - _lastCaptureTime >= _preset.MinTime;
            }

            if (shouldCapture)
            {
                _samples.Add((point, context.TimeSeconds));
                _lastCaptureTime = context.TimeSeconds;
            }

            var expireBefore = context.TimeSeconds - _preset.DurationSeconds;
            while (_samples.Count > 0 && _samples[0].Time < expireBefore)
                _samples.RemoveAt(0);

            while (_samples.Count > _preset.MaxPoints)
                _samples.RemoveAt(0);

            var result = new MeshCenterLineEvaluation
            {
                Closed = false,
                SmoothPath = _preset.SmoothPath,
                SmoothingSubdivisions = _preset.SmoothingSubdivisions,
            };

            for (var i = 0; i < _samples.Count; i++)
                result.Points.Add(_samples[i].Point);

            evaluation = result;
            return result.Points.Count >= 2;
        }
    }

    sealed class MeshAreaFillTrackPlayerRuntime : IMeshTrackPlayerRuntime
    {
        readonly MeshAreaFillTrackPlayerPreset _preset;

        public MeshAreaFillTrackPlayerRuntime(MeshAreaFillTrackPlayerPreset preset)
        {
            _preset = preset;
        }

        public void Reset()
        {
        }

        public bool TryEvaluate(MeshTrackEvaluationContext context, out MeshTrackPlayerEvaluation evaluation)
        {
            evaluation = new MeshContourEvaluation();

            var scope = ActorSourceFastResolver.Resolve(context.DynamicContext, _preset.AreaHubSource);
            if (scope?.Resolver == null)
                return false;

            if (!scope.Resolver.TryResolve<IAreaChannelHubService>(out var hub) || hub == null)
                return false;

            if (!hub.TryGetContour(_preset.AreaTag, out var contour))
                return false;

            var result = (MeshContourEvaluation)evaluation;
            for (var i = 0; i < contour.Paths.Count; i++)
            {
                var contourPath = contour.Paths[i];
                if (contourPath.Points == null || contourPath.Points.Count < 3)
                    continue;

                var path = new MeshRuntimePath
                {
                    IsHole = contourPath.IsHole,
                };

                for (var p = 0; p < contourPath.Points.Count; p++)
                    path.Points.Add(contourPath.Points[p]);

                result.Paths.Add(path);
            }

            return result.Paths.Count > 0;
        }
    }

    sealed class MeshLineTrackVisualizerRuntime : IMeshTrackVisualizerRuntime
    {
        readonly MeshLineTrackVisualizerPreset _preset;

        public MeshLineTrackVisualizerRuntime(MeshLineTrackVisualizerPreset preset)
        {
            _preset = preset;
        }

        public bool TryBuildPaths(
            MeshTrackEvaluationContext context,
            MeshRegularTrackRuntimeState track,
            MeshTrackPlayerEvaluation evaluation,
            List<MeshRuntimePath> outputPaths)
        {
            outputPaths.Clear();

            if (evaluation is not MeshCenterLineEvaluation centerLine || centerLine.Points.Count < 2)
                return false;

            var smoothed = ListPool<Vector2>.Get();
            var resampled = ListPool<Vector2>.Get();
            var distances = ListPool<float>.Get();
            var left = ListPool<Vector2>.Get();
            var right = ListPool<Vector2>.Get();

            try
            {
                if (centerLine.SmoothPath && centerLine.Points.Count > 2)
                    MeshChannelGeometryUtility.BuildCatmullRom(centerLine.Points, centerLine.Closed, Mathf.Max(1, centerLine.SmoothingSubdivisions), smoothed);
                else
                    smoothed.AddRange(centerLine.Points);

                if (smoothed.Count < 2)
                    return false;

                MeshChannelGeometryUtility.Resample(smoothed, _preset.MinSegmentLength, _preset.MaxPointCount, resampled, distances);
                if (resampled.Count < 2)
                    return false;

                MeshChannelGeometryUtility.BuildRibbonOutline(
                    resampled,
                    distances,
                    centerLine.Closed,
                    _preset,
                    context.TimeSeconds,
                    left,
                    right);

                if (left.Count < 2 || right.Count < 2)
                    return false;

                var path = new MeshRuntimePath();
                for (var i = 0; i < left.Count; i++)
                    path.Points.Add(left[i]);
                for (var i = right.Count - 1; i >= 0; i--)
                    path.Points.Add(right[i]);

                if (path.Points.Count < 3)
                    return false;

                outputPaths.Add(path);
                return true;
            }
            finally
            {
                ListPool<Vector2>.Release(smoothed);
                ListPool<Vector2>.Release(resampled);
                ListPool<float>.Release(distances);
                ListPool<Vector2>.Release(left);
                ListPool<Vector2>.Release(right);
            }
        }
    }

    sealed class MeshAreaFillTrackVisualizerRuntime : IMeshTrackVisualizerRuntime
    {
        readonly MeshAreaFillTrackVisualizerPreset _preset;

        public MeshAreaFillTrackVisualizerRuntime(MeshAreaFillTrackVisualizerPreset preset)
        {
            _preset = preset;
        }

        public bool TryBuildPaths(
            MeshTrackEvaluationContext context,
            MeshRegularTrackRuntimeState track,
            MeshTrackPlayerEvaluation evaluation,
            List<MeshRuntimePath> outputPaths)
        {
            outputPaths.Clear();
            if (evaluation is not MeshContourEvaluation contour || contour.Paths.Count == 0)
                return false;

            for (var i = 0; i < contour.Paths.Count; i++)
                outputPaths.Add(contour.Paths[i].Clone());

            return outputPaths.Count > 0;
        }
    }

    sealed class MeshPolygonTrackColliderRuntime : IMeshTrackColliderRuntime
    {
        public MeshPolygonTrackColliderPreset? Preset { get; }

        public MeshPolygonTrackColliderRuntime(MeshPolygonTrackColliderPreset preset)
        {
            Preset = preset;
        }
    }

    abstract class MeshBaseClaySimulationRuntime : IMeshSimulationTrackRuntime
    {
        readonly Dictionary<string, List<Vector2[]>> _offsetsByCompositeTag = new(StringComparer.Ordinal);

        protected abstract float Radius { get; }
        protected abstract float Strength { get; }
        protected abstract float RecoverSpeed { get; }

        public void Reset()
        {
            _offsetsByCompositeTag.Clear();
        }

        public void Apply(MeshSimulationContext context, MeshSimulationTrackRuntimeState track, MeshCompositeDraft composite)
        {
            if (!_offsetsByCompositeTag.TryGetValue(composite.Tag, out var offsets))
            {
                offsets = new List<Vector2[]>();
                _offsetsByCompositeTag[composite.Tag] = offsets;
            }

            EnsureOffsets(offsets, composite.Paths);
            DecayOffsets(context.DeltaTime, offsets);
            ApplyHits(context.Hits, composite.Paths, offsets);
            ApplyOffsets(composite.Paths, offsets);
        }

        void EnsureOffsets(List<Vector2[]> offsets, List<MeshRuntimePath> paths)
        {
            while (offsets.Count < paths.Count)
                offsets.Add(Array.Empty<Vector2>());

            for (var i = 0; i < paths.Count; i++)
            {
                if (offsets[i].Length != paths[i].Points.Count)
                    offsets[i] = new Vector2[paths[i].Points.Count];
            }
        }

        void DecayOffsets(float deltaTime, List<Vector2[]> offsets)
        {
            var recover = Mathf.Max(0f, RecoverSpeed);
            if (recover <= 0f)
                return;

            var weight = Mathf.Clamp01(deltaTime * recover);
            for (var i = 0; i < offsets.Count; i++)
            {
                var buffer = offsets[i];
                for (var p = 0; p < buffer.Length; p++)
                    buffer[p] = Vector2.Lerp(buffer[p], Vector2.zero, weight);
            }
        }

        void ApplyHits(IReadOnlyList<MeshHitContactInfo> hits, List<MeshRuntimePath> paths, List<Vector2[]> offsets)
        {
            if (hits == null || hits.Count == 0)
                return;

            var radius = Mathf.Max(0.001f, Radius);
            var sqrRadius = radius * radius;
            for (var h = 0; h < hits.Count; h++)
            {
                var hit = hits[h];
                var hitPoint = hit.ContactPoint;
                var hitNormal = hit.ContactNormal.sqrMagnitude > 0.0001f ? hit.ContactNormal.normalized : Vector2.up;
                var impact = Strength *
                             (1f + hit.RelativeVelocity.magnitude + hit.ImpulseEstimate + hit.PenetrationEstimate);

                for (var i = 0; i < paths.Count; i++)
                {
                    var points = paths[i].Points;
                    var buffer = offsets[i];
                    for (var p = 0; p < points.Count; p++)
                    {
                        var delta = points[p] - hitPoint;
                        var sqrDistance = delta.sqrMagnitude;
                        if (sqrDistance > sqrRadius)
                            continue;

                        var falloff = 1f - Mathf.Clamp01(Mathf.Sqrt(sqrDistance) / radius);
                        buffer[p] += -hitNormal * impact * falloff * 0.01f;
                    }
                }
            }
        }

        static void ApplyOffsets(List<MeshRuntimePath> paths, List<Vector2[]> offsets)
        {
            for (var i = 0; i < paths.Count; i++)
            {
                var points = paths[i].Points;
                var buffer = offsets[i];
                for (var p = 0; p < points.Count && p < buffer.Length; p++)
                    points[p] += buffer[p];
            }
        }
    }

    sealed class MeshClayTransientSimulationRuntime : MeshBaseClaySimulationRuntime
    {
        readonly MeshClayTransientSimulationPreset _preset;

        public MeshClayTransientSimulationRuntime(MeshClayTransientSimulationPreset preset)
        {
            _preset = preset;
        }

        protected override float Radius => _preset.Radius;
        protected override float Strength => _preset.ImpactStrength;
        protected override float RecoverSpeed => _preset.RecoverSpeed;
    }

    sealed class MeshClayPersistentSimulationRuntime : MeshBaseClaySimulationRuntime
    {
        readonly MeshClayPersistentSimulationPreset _preset;

        public MeshClayPersistentSimulationRuntime(MeshClayPersistentSimulationPreset preset)
        {
            _preset = preset;
        }

        protected override float Radius => _preset.Radius;
        protected override float Strength => _preset.ImpactStrength;
        protected override float RecoverSpeed => _preset.RecoverSpeed;
    }

    sealed class MeshFluidSimulationRuntime : IMeshSimulationTrackRuntime
    {
        sealed class RippleState
        {
            public Vector2 Point;
            public Vector2 Normal;
            public float Amplitude;
            public float StartedAt;
        }

        readonly MeshFluidSimulationPreset _preset;
        readonly Dictionary<string, List<RippleState>> _ripplesByCompositeTag = new(StringComparer.Ordinal);

        public MeshFluidSimulationRuntime(MeshFluidSimulationPreset preset)
        {
            _preset = preset;
        }

        public void Reset()
        {
            _ripplesByCompositeTag.Clear();
        }

        public void Apply(MeshSimulationContext context, MeshSimulationTrackRuntimeState track, MeshCompositeDraft composite)
        {
            if (!_ripplesByCompositeTag.TryGetValue(composite.Tag, out var ripples))
            {
                ripples = new List<RippleState>();
                _ripplesByCompositeTag[composite.Tag] = ripples;
            }

            for (var i = 0; i < context.Hits.Count; i++)
            {
                var hit = context.Hits[i];
                ripples.Add(new RippleState
                {
                    Point = hit.ContactPoint,
                    Normal = hit.ContactNormal.sqrMagnitude > 0.0001f ? hit.ContactNormal.normalized : Vector2.up,
                    Amplitude = _preset.WaveStrength * (1f + hit.RelativeVelocity.magnitude + hit.ImpulseEstimate) * 0.02f,
                    StartedAt = context.TimeSeconds,
                });
            }

            var radius = Mathf.Max(0.001f, _preset.Radius);
            var damping = Mathf.Max(0f, _preset.Damping);

            for (var i = ripples.Count - 1; i >= 0; i--)
            {
                var ripple = ripples[i];
                var age = context.TimeSeconds - ripple.StartedAt;
                var amplitude = ripple.Amplitude * Mathf.Exp(-age * damping);
                if (amplitude <= 0.0005f)
                {
                    ripples.RemoveAt(i);
                    continue;
                }

                for (var p = 0; p < composite.Paths.Count; p++)
                {
                    var points = composite.Paths[p].Points;
                    for (var n = 0; n < points.Count; n++)
                    {
                        var delta = points[n] - ripple.Point;
                        var distance = delta.magnitude;
                        if (distance > radius)
                            continue;

                        var falloff = 1f - Mathf.Clamp01(distance / radius);
                        var wave = Mathf.Sin((distance / radius) * Mathf.PI * 2f - age * 8f);
                        points[n] += ripple.Normal * (wave * amplitude * falloff);
                    }
                }
            }
        }
    }

    static class MeshChannelGeometryUtility
    {
        const float Epsilon = 0.0001f;

        public static void BuildCatmullRom(IReadOnlyList<Vector2> source, bool closed, int subdivisions, List<Vector2> output)
        {
            output.Clear();
            if (source == null || source.Count == 0)
                return;
            if (source.Count < 2)
            {
                output.AddRange(source);
                return;
            }

            var count = source.Count;
            for (var i = 0; i < count - (closed ? 0 : 1); i++)
            {
                var p0 = source[WrapIndex(i - 1, count, closed)];
                var p1 = source[WrapIndex(i, count, closed)];
                var p2 = source[WrapIndex(i + 1, count, closed)];
                var p3 = source[WrapIndex(i + 2, count, closed)];

                if (!closed && i == 0)
                    p0 = p1;
                if (!closed && i >= count - 2)
                    p3 = p2;

                if (i == 0)
                    output.Add(p1);

                for (var s = 1; s <= subdivisions; s++)
                {
                    var t = s / (float)subdivisions;
                    output.Add(EvaluateCatmullRom(p0, p1, p2, p3, t));
                }
            }
        }

        public static void Resample(
            IReadOnlyList<Vector2> source,
            float minSegmentLength,
            int maxPointCount,
            List<Vector2> points,
            List<float> distances)
        {
            points.Clear();
            distances.Clear();
            if (source == null || source.Count == 0)
                return;

            points.Add(source[0]);
            distances.Add(0f);

            var total = 0f;
            var effectiveMin = Mathf.Max(Epsilon, minSegmentLength);
            for (var i = 0; i < source.Count - 1; i++)
            {
                var a = source[i];
                var b = source[i + 1];
                var length = Vector2.Distance(a, b);
                if (length <= Epsilon)
                    continue;

                var steps = Mathf.Max(1, Mathf.CeilToInt(length / effectiveMin));
                for (var s = 1; s <= steps; s++)
                {
                    var t = s / (float)steps;
                    total += length / steps;
                    points.Add(Vector2.LerpUnclamped(a, b, t));
                    distances.Add(total);
                    if (points.Count >= maxPointCount)
                        return;
                }
            }
        }

        public static void BuildRibbonOutline(
            IReadOnlyList<Vector2> points,
            IReadOnlyList<float> distances,
            bool closed,
            MeshLineTrackVisualizerPreset preset,
            float timeSeconds,
            List<Vector2> left,
            List<Vector2> right)
        {
            left.Clear();
            right.Clear();
            if (points == null || points.Count < 2)
                return;

            var totalLength = distances.Count > 0 ? distances[distances.Count - 1] : 0f;
            if (totalLength <= Epsilon)
                totalLength = 1f;

            for (var i = 0; i < points.Count; i++)
            {
                var prev = i == 0 ? (closed ? points[points.Count - 1] : points[i]) : points[i - 1];
                var next = i == points.Count - 1 ? (closed ? points[0] : points[i]) : points[i + 1];
                var tangent = (next - prev);
                if (tangent.sqrMagnitude <= Epsilon)
                    tangent = Vector2.right;
                tangent.Normalize();

                var normal = new Vector2(-tangent.y, tangent.x);
                var p = points[i];
                var dist = i < distances.Count ? distances[i] : 0f;

                if (preset.WaveAmplitude > 0f)
                {
                    var sampleLength = preset.WaveSpace == MeshWaveSpace.NormalizedLength
                        ? dist / Mathf.Max(Epsilon, totalLength)
                        : dist;
                    var theta = (sampleLength / Mathf.Max(Epsilon, preset.WaveLength)) * Mathf.PI * 2f +
                                preset.WavePhase +
                                timeSeconds * preset.WaveScrollSpeed;
                    p += normal * (Mathf.Sin(theta) * preset.WaveAmplitude);
                }

                var widthScale = 1f;
                if (preset.HeadTaperNormalized > Epsilon)
                {
                    var t = Mathf.Clamp01(dist / Mathf.Max(Epsilon, totalLength * preset.HeadTaperNormalized));
                    widthScale = Mathf.Min(widthScale, t);
                }

                if (preset.TailTaperNormalized > Epsilon)
                {
                    var tailLength = totalLength * preset.TailTaperNormalized;
                    var tailT = Mathf.Clamp01((totalLength - dist) / Mathf.Max(Epsilon, tailLength));
                    widthScale = Mathf.Min(widthScale, tailT);
                }

                var halfWidth = Mathf.Max(Epsilon, preset.BaseWidth * Mathf.Max(Epsilon, widthScale)) * 0.5f;
                left.Add(p + normal * halfWidth);
                right.Add(p - normal * halfWidth);
            }
        }

        public static List<Vector2[]> ConvertWorldPathsToLocal(Transform ownerTransform, List<MeshRuntimePath> worldPaths)
        {
            var paths = new List<Vector2[]>(worldPaths.Count);
            for (var i = 0; i < worldPaths.Count; i++)
            {
                var worldPath = worldPaths[i];
                var local = new Vector2[worldPath.Points.Count];
                for (var p = 0; p < worldPath.Points.Count; p++)
                {
                    var world = worldPath.Points[p];
                    var local3 = ownerTransform.InverseTransformPoint(new Vector3(world.x, world.y, 0f));
                    local[p] = new Vector2(local3.x, local3.y);
                }
                paths.Add(local);
            }
            return paths;
        }

        public static List<Vector2[]> SimplifyPaths(List<Vector2[]> sourcePaths, MeshPolygonSyncSettings settings)
        {
            var result = new List<Vector2[]>(sourcePaths.Count);
            for (var i = 0; i < sourcePaths.Count; i++)
                result.Add(SimplifyPath(sourcePaths[i], settings));
            return result;
        }

        public static bool ShouldSyncPaths(
            IReadOnlyList<Vector2[]> lastPaths,
            IReadOnlyList<Vector2[]> nextPaths,
            MeshPolygonSyncSettings settings,
            int frameIndex,
            int lastSyncFrame)
        {
            if (frameIndex - lastSyncFrame < Mathf.Max(1, settings.UpdateIntervalFrames))
                return false;

            if (lastPaths == null || lastPaths.Count != nextPaths.Count)
                return true;

            for (var i = 0; i < nextPaths.Count; i++)
            {
                var a = lastPaths[i];
                var b = nextPaths[i];
                if (a == null || b == null || a.Length != b.Length)
                    return true;

                if (ComputeMaxMove(a, b) >= settings.MinPointMove)
                    return true;
                if (Mathf.Abs(ComputeSignedArea(a) - ComputeSignedArea(b)) >= settings.MinAreaDelta)
                    return true;
                if (ComputeMaxAngleDelta(a, b) >= settings.MinAngleDelta)
                    return true;
            }

            return false;
        }

        public static void CopyMesh(Mesh source, Mesh destination)
        {
            destination.Clear();
            destination.vertices = source.vertices;
            destination.normals = source.normals;
            destination.tangents = source.tangents;
            destination.colors = source.colors;
            destination.uv = source.uv;
            destination.uv2 = source.uv2;
            destination.triangles = source.triangles;
        }

        public static void BuildFallbackMesh(IReadOnlyList<Vector2[]> paths, Mesh mesh)
        {
            mesh.Clear();
            if (paths == null || paths.Count == 0)
                return;

            var vertices = ListPool<Vector3>.Get();
            var triangles = ListPool<int>.Get();

            try
            {
                for (var i = 0; i < paths.Count; i++)
                {
                    var path = paths[i];
                    if (path == null || path.Length < 3)
                        continue;

                    var baseVertex = vertices.Count;
                    for (var p = 0; p < path.Length; p++)
                        vertices.Add(new Vector3(path[p].x, path[p].y, 0f));

                    Triangulate(path, triangles, baseVertex);
                }

                if (vertices.Count == 0 || triangles.Count == 0)
                    return;

                mesh.SetVertices(vertices);
                mesh.SetTriangles(triangles, 0, true);
            }
            finally
            {
                ListPool<Vector3>.Release(vertices);
                ListPool<int>.Release(triangles);
            }
        }

        static Vector2[] SimplifyPath(Vector2[] source, MeshPolygonSyncSettings settings)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<Vector2>();

            var tolerance = Mathf.Max(0f, settings.ContourTolerance);
            var points = ListPool<Vector2>.Get();
            try
            {
                points.Add(source[0]);
                for (var i = 1; i < source.Length; i++)
                {
                    if ((source[i] - points[points.Count - 1]).sqrMagnitude < tolerance * tolerance)
                        continue;
                    points.Add(source[i]);
                }

                while (points.Count > settings.MaxPointCount)
                {
                    for (var i = points.Count - 2; i > 0 && points.Count > settings.MaxPointCount; i -= 2)
                        points.RemoveAt(i);
                }

                if (points.Count < 3)
                    return source;

                return points.ToArray();
            }
            finally
            {
                ListPool<Vector2>.Release(points);
            }
        }

        static Vector2 EvaluateCatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            var t2 = t * t;
            var t3 = t2 * t;
            return 0.5f * ((2f * p1) +
                           (-p0 + p2) * t +
                           (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                           (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        static int WrapIndex(int index, int count, bool closed)
        {
            if (closed)
            {
                while (index < 0)
                    index += count;
                return index % count;
            }

            return Mathf.Clamp(index, 0, count - 1);
        }

        static float ComputeMaxMove(Vector2[] a, Vector2[] b)
        {
            var max = 0f;
            for (var i = 0; i < a.Length && i < b.Length; i++)
                max = Mathf.Max(max, Vector2.Distance(a[i], b[i]));
            return max;
        }

        static float ComputeSignedArea(Vector2[] polygon)
        {
            if (polygon == null || polygon.Length < 3)
                return 0f;

            var area = 0f;
            for (var i = 0; i < polygon.Length; i++)
            {
                var current = polygon[i];
                var next = polygon[(i + 1) % polygon.Length];
                area += current.x * next.y - next.x * current.y;
            }
            return area * 0.5f;
        }

        static float ComputeMaxAngleDelta(Vector2[] a, Vector2[] b)
        {
            var max = 0f;
            for (var i = 0; i < a.Length && i < b.Length; i++)
            {
                var aPrev = a[(i - 1 + a.Length) % a.Length];
                var aNext = a[(i + 1) % a.Length];
                var bPrev = b[(i - 1 + b.Length) % b.Length];
                var bNext = b[(i + 1) % b.Length];
                var aDir = (aNext - aPrev).normalized;
                var bDir = (bNext - bPrev).normalized;
                if (aDir.sqrMagnitude <= Epsilon || bDir.sqrMagnitude <= Epsilon)
                    continue;
                max = Mathf.Max(max, Vector2.Angle(aDir, bDir));
            }
            return max;
        }

        static void Triangulate(IReadOnlyList<Vector2> polygon, List<int> triangles, int baseVertex)
        {
            var indices = ListPool<int>.Get();
            try
            {
                for (var i = 0; i < polygon.Count; i++)
                    indices.Add(i);

                if (ComputeSignedArea(polygon.ToArray()) < 0f)
                    indices.Reverse();

                var guard = 0;
                while (indices.Count >= 3 && guard < 4096)
                {
                    guard++;
                    var earFound = false;
                    for (var i = 0; i < indices.Count; i++)
                    {
                        var prev = indices[(i - 1 + indices.Count) % indices.Count];
                        var current = indices[i];
                        var next = indices[(i + 1) % indices.Count];

                        if (!IsEar(polygon, indices, prev, current, next))
                            continue;

                        triangles.Add(baseVertex + prev);
                        triangles.Add(baseVertex + current);
                        triangles.Add(baseVertex + next);
                        indices.RemoveAt(i);
                        earFound = true;
                        break;
                    }

                    if (!earFound)
                        break;
                }
            }
            finally
            {
                ListPool<int>.Release(indices);
            }
        }

        static bool IsEar(IReadOnlyList<Vector2> polygon, List<int> indices, int prev, int current, int next)
        {
            var a = polygon[prev];
            var b = polygon[current];
            var c = polygon[next];
            if (Cross(b - a, c - b) <= 0f)
                return false;

            for (var i = 0; i < indices.Count; i++)
            {
                var idx = indices[i];
                if (idx == prev || idx == current || idx == next)
                    continue;
                if (PointInTriangle(polygon[idx], a, b, c))
                    return false;
            }

            return true;
        }

        static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        static bool PointInTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
        {
            var ab = Cross(b - a, point - a);
            var bc = Cross(c - b, point - b);
            var ca = Cross(a - c, point - c);
            var hasNegative = ab < 0f || bc < 0f || ca < 0f;
            var hasPositive = ab > 0f || bc > 0f || ca > 0f;
            return !(hasNegative && hasPositive);
        }
    }

    static class ListPool<T>
    {
        static readonly Stack<List<T>> Pool = new();

        public static List<T> Get()
        {
            if (Pool.Count > 0)
                return Pool.Pop();
            return new List<T>();
        }

        public static void Release(List<T> list)
        {
            list.Clear();
            Pool.Push(list);
        }
    }
}
