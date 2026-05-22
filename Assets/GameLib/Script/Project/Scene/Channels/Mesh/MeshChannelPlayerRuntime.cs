#nullable enable
using System;
using System.Collections.Generic;
using Game.Commands.VNext;
using Game.Common;
using Game.VariableLayer;
using UnityEngine;

namespace Game.Channel
{
    public sealed class MeshChannelPlayerRuntime : IMeshChannelPlayerRuntime
    {
        static readonly int MeshBaseColorPropertyId = Shader.PropertyToID("_MeshBaseColor");

        readonly string _tag;
        readonly DynamicValue<MeshDefinitionPreset> _definitionSource;
        readonly IScopeNode _scope;
        readonly Transform _ownerTransform;

        readonly Dictionary<string, MeshCompositeVisualObject> _visualsByTag = new(StringComparer.Ordinal);
        readonly List<string> _trackKeys = new();
        readonly Dictionary<string, List<MeshHitContactInfo>> _hitsByCompositeTag = new(StringComparer.Ordinal);
        readonly List<MeshHitContactInfo> _hitBuffer = new();
        readonly List<MeshRuntimePath> _sharedPathBuffer = new();
        readonly List<MeshRegularTrackContributor> _contributors = new();

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
            Transform ownerTransform)
        {
            if (string.IsNullOrWhiteSpace(tag))
                throw new ArgumentException("Mesh channel tag must be specified.", nameof(tag));

            if (!definitionSource.HasSource)
                throw new ArgumentException("Mesh definition source must have an authored source.", nameof(definitionSource));

            if (scope == null)
                throw new ArgumentNullException(nameof(scope));

            if (ownerTransform == null)
                throw new ArgumentNullException(nameof(ownerTransform));

            _tag = tag.Trim();
            _definitionSource = definitionSource;
            _scope = scope;
            _ownerTransform = ownerTransform;
        }

        public void OnAcquire()
        {
            _vars = MeshRuntimeStateFactory.ResolveVars(_scope);
            var dynamicContext = CreateDynamicContext();
            var authoredPreset = MeshRuntimeStateFactory.ResolveDefinition(_definitionSource, dynamicContext);
            _baseState = MeshRuntimeStateFactory.BuildRuntimeState(authoredPreset, dynamicContext);
            _currentState = MeshRuntimeStateFactory.CloneRuntimeState(_baseState);
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
                visual.Dispose();
            _visualsByTag.Clear();
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
                RefreshTrackEnabled(track, context.DynamicContext);
                if (!track.Enabled)
                    continue;

                if (!track.PlayerRuntime.TryEvaluate(context, out var evaluation) || evaluation == null)
                    continue;

                _sharedPathBuffer.Clear();
                if (!track.VisualizerRuntime.TryBuildPaths(context, track, evaluation, _sharedPathBuffer))
                    continue;

                if (_sharedPathBuffer.Count == 0)
                    continue;

                _contributors.Add(new MeshRegularTrackContributor(track, _sharedPathBuffer));
                _sharedPathBuffer.Clear();
            }

            var composites = BlendContributors();
            ApplySimulation(deltaTime, composites);
            RenderComposites(composites, frameIndex, Mathf.Max(0f, deltaTime));
        }

        public bool SwapRootDefinition(MeshDefinitionPreset preset)
        {
            var dynamicContext = CreateDynamicContext();
            var authored = preset?.CreateRuntimeCopy() ?? new MeshDefinitionPreset();
            _baseState = MeshRuntimeStateFactory.BuildRuntimeState(authored, dynamicContext);
            _currentState = MeshRuntimeStateFactory.CloneRuntimeState(_baseState);
            RefreshTrackKeyCache();
            return true;
        }

        public bool SwapTrackDefinition(string key, MeshTrackDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;

            var runtime = MeshRuntimeStateFactory.ResolveRegularTrack(
                definition?.CreateRuntimeCopy() ?? new MeshTrackDefinition(),
                CreateDynamicContext());
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
            {
                track.VisualizerPreset = MeshRuntimeStateFactory.ResolveVisualizerPreset(
                    mutation.Preset,
                    CreateDynamicContext());
            }

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
                    line.WaveEnabled = mutation.WaveEnabled;
                    line.WaveAmplitude = mutation.WaveAmplitude;
                    line.WaveLength = mutation.WaveLength;
                    line.WavePhase = mutation.WavePhase;
                    line.WaveScrollSpeed = mutation.WaveScrollSpeed;
                }
                if (mutation.ApplyDash)
                {
                    line.DashEnabled = mutation.DashEnabled;
                    line.DashSpace = mutation.DashSpace;
                    line.DashScrollSpeed = mutation.DashScrollSpeed;
                    line.DashScrollOffset = mutation.DashScrollOffset;
                    line.Pattern = mutation.Pattern != null
                        ? new List<MeshLineDashPatternElement>(mutation.Pattern)
                        : new List<MeshLineDashPatternElement>();
                }
            }

            track.VisualizerRuntime = MeshRuntimeStateFactory.CreateVisualizerRuntime(track.VisualizerPreset);
            return true;
        }

        public bool MutateTrackPlayer(string key, MeshTrackPlayerRuntimeMutation mutation)
        {
            if (!_currentState.RegularTracksByKey.TryGetValue(key, out var track) || mutation == null || !mutation.HasAnyMutation())
                return false;

            if (mutation.ReplacePreset)
            {
                track.PlayerPreset = MeshRuntimeStateFactory.ResolvePlayerPreset(
                    mutation.Preset,
                    CreateDynamicContext());
            }

            if (track.PlayerPreset is MeshLineTrackPlayerPreset line && mutation.ApplyPoints)
                line.Points = new List<DynamicValue<Vector3>>(mutation.Points);

            if (track.PlayerPreset is MeshLineTrackPlayerPreset conditionLine && mutation.ApplyCondition)
                conditionLine.Condition = mutation.Condition;

            if (track.PlayerPreset is MeshTargetLinkTrackPlayerPreset conditionTargetLink && mutation.ApplyCondition)
                conditionTargetLink.Condition = mutation.Condition;

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

            if (track.PlayerPreset is MeshTargetLinkTrackPlayerPreset targetLink && mutation.ApplyTargetLinkConfig)
            {
                targetLink.SelfActorSource = mutation.SelfActorSource;
                targetLink.TargetChannelTag = mutation.TargetChannelTag;
                targetLink.TopN = Mathf.Max(0, mutation.TopN);
                targetLink.Topology = mutation.Topology;
            }

            RefreshTrackEnabled(track, CreateDynamicContext());
            track.PlayerRuntime = MeshRuntimeStateFactory.CreatePlayerRuntime(track.PlayerPreset);
            return true;
        }

        public bool MutateTrackCollider(string key, MeshTrackColliderRuntimeMutation mutation)
        {
            if (!_currentState.RegularTracksByKey.TryGetValue(key, out var track) || mutation == null || !mutation.HasAnyMutation())
                return false;

            if (mutation.ReplacePreset)
            {
                track.ColliderPreset = MeshRuntimeStateFactory.ResolveColliderPreset(
                    mutation.Preset,
                    CreateDynamicContext());
            }

            if (track.ColliderPreset is MeshPolygonTrackColliderPreset polygon)
            {
                if (mutation.ApplySyncToggle)
                    polygon.SyncPolygonToCollider = mutation.SyncPolygonToCollider;
                if (mutation.ApplyHitCaptureToggle)
                    polygon.EnableHitCapture = mutation.EnableHitCapture;
                if (mutation.ApplySyncSettings)
                    polygon.Sync = mutation.Sync?.CreateRuntimeCopy() ?? new MeshPolygonSyncSettings();
            }

            track.ColliderRuntime = MeshRuntimeStateFactory.CreateColliderRuntime(track.ColliderPreset);
            return true;
        }

        public bool MutateTrackMaterial(string key, MeshTrackMaterialRuntimeMutation mutation)
        {
            if (!_currentState.RegularTracksByKey.TryGetValue(key, out var track) || mutation == null || !mutation.HasAnyMutation())
                return false;

            if (mutation.ReplacePreset)
            {
                track.MaterialPreset = MeshRuntimeStateFactory.ResolveMaterialPreset(
                    mutation.Preset,
                    CreateDynamicContext());
            }

            if (mutation.ApplyEnabled)
                track.MaterialPreset.Enabled = mutation.Enabled;
            if (mutation.ApplyBaseTint)
                track.MaterialPreset.BaseTint = mutation.BaseTint;
            if (mutation.ApplySortingOrderOffset)
                track.MaterialPreset.SortingOrderOffset = mutation.SortingOrderOffset;

            return true;
        }

        public bool MutateSimulationTrack(string key, MeshSimulationTrackRuntimeMutation mutation)
        {
            if (!_currentState.SimulationTracksByKey.TryGetValue(key, out var track) || mutation == null || !mutation.HasAnyMutation())
                return false;

            if (mutation.ReplacePreset)
            {
                track.Preset = MeshRuntimeStateFactory.ResolveSimulationPreset(
                    mutation.Preset,
                    CreateDynamicContext());
            }
            if (mutation.ApplyPriority)
                track.Priority = mutation.Priority;
            if (mutation.ApplyEnabled)
                track.Enabled = mutation.Enabled;

            track.Runtime = MeshRuntimeStateFactory.CreateSimulationRuntime(track.Preset);
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

            var resetState = MeshRuntimeStateFactory.CloneRuntimeState(_baseState);
            var dynamicContext = CreateDynamicContext();

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
                    RefreshTrackEnabled(current, dynamicContext);
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
            {
                _currentState.SimulationTracksByKey.Clear();
                foreach (var pair in resetState.SimulationTracksByKey)
                    _currentState.SimulationTracksByKey[pair.Key] = pair.Value;
            }

            RefreshTrackKeyCache();
            return true;
        }

        public bool SetTrackEnabled(string key, bool enabled)
        {
            if (_currentState.RegularTracksByKey.TryGetValue(key, out var track))
            {
                track.RequestedEnabled = enabled;
                RefreshTrackEnabled(track, CreateDynamicContext());
                return true;
            }

            if (_currentState.SimulationTracksByKey.TryGetValue(key, out var simulationTrack))
            {
                simulationTrack.Enabled = enabled;
                return true;
            }

            return false;
        }

        void RefreshTrackEnabled(MeshRegularTrackRuntimeState track, IDynamicContext dynamicContext)
        {
            track.ConditionEnabled = MeshRuntimeStateFactory.EvaluateConditionEnabled(
                track.PlayerPreset,
                dynamicContext,
                track.ConditionEnabled);
            track.RecalculateEnabled();
        }

        public bool SetMaterialEntry(string compositeTag, int nodeId, string layerTag, VariableLayerValue value, float lifetimeSeconds = -1f)
        {
            return GetOrCreateVisual(compositeTag).SetMaterialEntry(nodeId, layerTag, value, lifetimeSeconds);
        }

        public bool SetMaterialEntryFade(string compositeTag, int nodeId, string layerTag, VariableLayerValue value, float durationSeconds, DG.Tweening.Ease ease, float lifetimeSeconds = -1f)
        {
            return GetOrCreateVisual(compositeTag).SetMaterialEntryFade(nodeId, layerTag, value, durationSeconds, ease, lifetimeSeconds);
        }

        public bool RemoveMaterialTag(string compositeTag, int nodeId, string layerTag)
        {
            return GetOrCreateVisual(compositeTag).RemoveMaterialTag(nodeId, layerTag);
        }

        public bool ClearMaterialContext(string compositeTag, string layerTag)
        {
            return GetOrCreateVisual(compositeTag).ClearMaterialContext(layerTag);
        }

        public bool ClearMaterialNode(string compositeTag, int nodeId)
        {
            return GetOrCreateVisual(compositeTag).ClearMaterialNode(nodeId);
        }

        public bool ResetMaterialDefaults(string compositeTag)
        {
            GetOrCreateVisual(compositeTag).ResetMaterialDefaults();
            return true;
        }

        IDynamicContext CreateDynamicContext()
        {
            return new SimpleDynamicContext(_vars, _scope);
        }

        void CaptureCurrentHits()
        {
            _hitsByCompositeTag.Clear();
            foreach (var pair in _visualsByTag)
            {
                pair.Value.CaptureHits(_hitBuffer);
                if (_hitBuffer.Count == 0)
                    continue;

                _hitsByCompositeTag[pair.Key] = new List<MeshHitContactInfo>(_hitBuffer);
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
                        : null;
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

        void RenderComposites(Dictionary<string, MeshCompositeDraft> composites, int frameIndex, float deltaTime)
        {
            foreach (var pair in composites)
            {
                var visual = GetOrCreateVisual(pair.Key);

                visual.Apply(
                    pair.Value,
                    _currentState.RenderPipeline,
                    GetOrCreateFallbackMaterial(),
                    deltaTime,
                    frameIndex);
            }

            foreach (var pair in _visualsByTag)
            {
                if (composites.ContainsKey(pair.Key))
                    continue;

                pair.Value.Clear();
                pair.Value.AdvanceMaterial(deltaTime);
            }
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
            var shader = ResolveCompatibleFallbackShader();
            if (_fallbackMaterial != null && _fallbackMaterial.shader == shader)
                return _fallbackMaterial;

            if (_fallbackMaterial != null)
            {
                UnityEngine.Object.Destroy(_fallbackMaterial);
                _fallbackMaterial = null;
            }

            _fallbackMaterial = new Material(shader)
            {
                name = $"MeshChannel.{_tag}.FallbackMaterial",
                hideFlags = HideFlags.DontSave,
            };
            return _fallbackMaterial;
        }

        Shader ResolveCompatibleFallbackShader()
        {
            var configuredShader = _currentState.RenderPipeline.DefaultShader;
            if (ShaderSupportsMaterialFx(configuredShader))
                return configuredShader!;

            var meshChannelShader = Shader.Find("Game/Mesh/MeshChannelSurface");
            if (meshChannelShader != null)
                return meshChannelShader;

            if (configuredShader != null)
                return configuredShader;

            return Shader.Find("Sprites/Default");
        }

        static bool ShaderSupportsMaterialFx(Shader? shader)
        {
            if (shader == null)
                return false;

            var probe = new Material(shader);
            try
            {
                return probe.HasProperty(MeshBaseColorPropertyId);
            }
            finally
            {
                UnityEngine.Object.Destroy(probe);
            }
        }

        MeshCompositeVisualObject GetOrCreateVisual(string compositeTag)
        {
            compositeTag = string.IsNullOrWhiteSpace(compositeTag) ? "default" : compositeTag.Trim();
            if (_visualsByTag.TryGetValue(compositeTag, out var visual))
                return visual;

            visual = new MeshCompositeVisualObject($"MeshComposite.{_tag}.{compositeTag}", _ownerTransform);
            _visualsByTag.Add(compositeTag, visual);
            return visual;
        }
    }
}
