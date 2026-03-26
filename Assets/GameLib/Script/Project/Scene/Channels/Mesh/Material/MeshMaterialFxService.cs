#nullable enable
using System;
using System.Collections.Generic;
using DG.Tweening;
using Game.VariableLayer;
using UnityEngine;

namespace Game.Channel
{
    public interface IMeshMaterialFxService : IDisposable
    {
        void Update(MeshTrackMaterialPreset preset, Material material, int sortingOrder, IReadOnlyList<Vector2[]> contourPaths, float deltaTime);
        void Advance(float deltaTime);
        bool SetEntry(int nodeId, string tag, VariableLayerValue value, float lifetimeSeconds = -1f);
        bool SetEntryFade(int nodeId, string tag, VariableLayerValue value, float durationSeconds, Ease ease, float lifetimeSeconds = -1f);
        bool RemoveTag(int nodeId, string tag);
        bool ClearContext(string tag);
        bool ClearNode(int nodeId);
        void ResetDefaults();
    }

    public interface IMeshMaterialFxServiceFactory
    {
        IMeshMaterialFxService CreateForMeshRenderer(MeshRenderer renderer);
        IMeshMaterialFxService CreateForSkinnedMeshRenderer(SkinnedMeshRenderer renderer);
    }

    public interface IMeshMaterialFxControlService
    {
        bool SetEntry(string channelTag, string compositeTag, int nodeId, string tag, VariableLayerValue value, float lifetimeSeconds = -1f);
        bool SetEntryFade(string channelTag, string compositeTag, int nodeId, string tag, VariableLayerValue value, float durationSeconds, Ease ease, float lifetimeSeconds = -1f);
        bool RemoveTag(string channelTag, string compositeTag, int nodeId, string tag);
        bool ClearContext(string channelTag, string compositeTag, string tag);
        bool ClearNode(string channelTag, string compositeTag, int nodeId);
        bool ResetDefaults(string channelTag, string compositeTag);
    }

    public sealed class MeshMaterialFxServiceFactory : IMeshMaterialFxServiceFactory
    {
        readonly IMeshMaterialPropertyRegistry _registry;

        public MeshMaterialFxServiceFactory(IMeshMaterialPropertyRegistry? registry = null)
        {
            _registry = registry ?? MeshMaterialPropertyCatalog.Instance;
        }

        public IMeshMaterialFxService CreateForMeshRenderer(MeshRenderer renderer)
        {
            return new MeshMaterialFxService(_registry, new MeshRendererMaterialFxTargetAdapter(renderer));
        }

        public IMeshMaterialFxService CreateForSkinnedMeshRenderer(SkinnedMeshRenderer renderer)
        {
            return new MeshMaterialFxService(_registry, new SkinnedMeshRendererMaterialFxTargetAdapter(renderer));
        }
    }

    public sealed class MeshMaterialFxService : IMeshMaterialFxService
    {
        internal const int MaxContourSamples = 64;

        static readonly int ContourSampleCountId = Shader.PropertyToID("_MeshContourSampleCount");
        static readonly int ContourBoundsId = Shader.PropertyToID("_MeshContourBounds");
        static readonly int ContourSamplesId = Shader.PropertyToID("_MeshContourSamples");

        readonly IMeshMaterialPropertyRegistry _registry;
        readonly IVariableLayerService _layers;
        readonly IMeshMaterialFxTargetAdapter _adapter;
        readonly Dictionary<int, VariableLayerValue> _cachedDefaults = new();
        readonly List<int> _dirtyNodeIds = new();
        readonly Vector4[] _contourSamples = new Vector4[MaxContourSamples];

        bool _needsDefaultResync = true;

        internal MeshMaterialFxService(IMeshMaterialPropertyRegistry registry, IMeshMaterialFxTargetAdapter adapter)
        {
            _registry = registry;
            _adapter = adapter;
            _layers = new VariableLayerRuntime(registry);
        }

        public void Update(MeshTrackMaterialPreset preset, Material material, int sortingOrder, IReadOnlyList<Vector2[]> contourPaths, float deltaTime)
        {
            if (!_adapter.IsValid)
                return;

            _layers.Tick(deltaTime);
            _adapter.BindMaterial(material);
            _adapter.SetSortingOrder(sortingOrder);

            if (!preset.Enabled)
            {
                _needsDefaultResync = true;
                ApplyDisabledState();
                return;
            }

            SyncDefaults(preset);
            ApplyContourData(contourPaths, preset.ContourSampling);
            FlushDirty();
        }

        public void Advance(float deltaTime)
        {
            if (!_adapter.IsValid)
                return;

            _layers.Tick(deltaTime);
            FlushDirty();
        }

        public bool SetEntry(int nodeId, string tag, VariableLayerValue value, float lifetimeSeconds = -1f)
        {
            return _layers.SetEntry(nodeId, tag, value, 0f, lifetimeSeconds, Ease.Linear);
        }

        public bool SetEntryFade(int nodeId, string tag, VariableLayerValue value, float durationSeconds, Ease ease, float lifetimeSeconds = -1f)
        {
            return _layers.SetEntry(nodeId, tag, value, durationSeconds, lifetimeSeconds, ease);
        }

        public bool RemoveTag(int nodeId, string tag)
        {
            _needsDefaultResync = true;
            return _layers.RemoveTag(nodeId, tag);
        }

        public bool ClearContext(string tag)
        {
            _needsDefaultResync = true;
            return _layers.ClearContext(tag);
        }

        public bool ClearNode(int nodeId)
        {
            _needsDefaultResync = true;
            return _layers.ClearNode(nodeId);
        }

        public void ResetDefaults()
        {
            _needsDefaultResync = true;
            _layers.ResetDefaults();
        }

        public void Dispose()
        {
            _adapter.Dispose();
        }

        void SyncDefaults(MeshTrackMaterialPreset preset)
        {
            SyncDefault(MeshMaterialPropertyCatalog.Ids.BaseTint, VariableLayerValue.FromColor(preset.BaseTint));

            SyncDefault(MeshMaterialPropertyCatalog.Ids.ContourGradientEnabled, VariableLayerValue.FromBool(preset.ContourGradient.Enabled));
            SyncDefault(MeshMaterialPropertyCatalog.Ids.ContourGradientColor, VariableLayerValue.FromColor(preset.ContourGradient.Color));
            SyncDefault(MeshMaterialPropertyCatalog.Ids.ContourGradientStrength, VariableLayerValue.FromFloat(preset.ContourGradient.Strength));
            SyncDefault(MeshMaterialPropertyCatalog.Ids.ContourGradientRange, VariableLayerValue.FromFloat(preset.ContourGradient.Range));
            SyncDefault(MeshMaterialPropertyCatalog.Ids.ContourGradientFalloff, VariableLayerValue.FromFloat(preset.ContourGradient.Falloff));

            SyncDefault(MeshMaterialPropertyCatalog.Ids.EdgeAlphaEnabled, VariableLayerValue.FromBool(preset.EdgeAlpha.Enabled));
            SyncDefault(MeshMaterialPropertyCatalog.Ids.EdgeAlphaGain, VariableLayerValue.FromFloat(preset.EdgeAlpha.Gain));
            SyncDefault(MeshMaterialPropertyCatalog.Ids.EdgeAlphaRange, VariableLayerValue.FromFloat(preset.EdgeAlpha.Range));
            SyncDefault(MeshMaterialPropertyCatalog.Ids.EdgeAlphaSoftness, VariableLayerValue.FromFloat(preset.EdgeAlpha.Softness));

            SyncDefault(MeshMaterialPropertyCatalog.Ids.BandsEnabled, VariableLayerValue.FromBool(preset.Bands.Enabled));
            SyncDefault(MeshMaterialPropertyCatalog.Ids.BandsCount, VariableLayerValue.FromInt(preset.Bands.Count));
            SyncDefault(MeshMaterialPropertyCatalog.Ids.BandsContrast, VariableLayerValue.FromFloat(preset.Bands.Contrast));
            SyncDefault(MeshMaterialPropertyCatalog.Ids.BandsColor, VariableLayerValue.FromColor(preset.Bands.Color));
            SyncDefault(MeshMaterialPropertyCatalog.Ids.BandsIntensity, VariableLayerValue.FromFloat(preset.Bands.Intensity));

            SyncDefault(MeshMaterialPropertyCatalog.Ids.EdgeFlowEnabled, VariableLayerValue.FromBool(preset.EdgeFlow.Enabled));
            SyncDefault(MeshMaterialPropertyCatalog.Ids.EdgeFlowColor, VariableLayerValue.FromColor(preset.EdgeFlow.Color));
            SyncDefault(MeshMaterialPropertyCatalog.Ids.EdgeFlowWidth, VariableLayerValue.FromFloat(preset.EdgeFlow.Width));
            SyncDefault(MeshMaterialPropertyCatalog.Ids.EdgeFlowSpeed, VariableLayerValue.FromFloat(preset.EdgeFlow.Speed));
            SyncDefault(MeshMaterialPropertyCatalog.Ids.EdgeFlowIntensity, VariableLayerValue.FromFloat(preset.EdgeFlow.Intensity));

            SyncDefault(MeshMaterialPropertyCatalog.Ids.InteriorNoiseEnabled, VariableLayerValue.FromBool(preset.InteriorNoise.Enabled));
            SyncDefault(MeshMaterialPropertyCatalog.Ids.InteriorNoiseScale, VariableLayerValue.FromFloat(preset.InteriorNoise.Scale));
            SyncDefault(MeshMaterialPropertyCatalog.Ids.InteriorNoiseSpeed, VariableLayerValue.FromFloat(preset.InteriorNoise.Speed));
            SyncDefault(MeshMaterialPropertyCatalog.Ids.InteriorNoiseStrength, VariableLayerValue.FromFloat(preset.InteriorNoise.Strength));

            _needsDefaultResync = false;
        }

        void ApplyDisabledState()
        {
            _adapter.SetValue(MeshMaterialPropertyCatalog.Ids.BaseTint, VariableLayerValue.FromColor(Color.white));
            _adapter.SetValue(MeshMaterialPropertyCatalog.Ids.ContourGradientEnabled, VariableLayerValue.FromBool(false));
            _adapter.SetValue(MeshMaterialPropertyCatalog.Ids.EdgeAlphaEnabled, VariableLayerValue.FromBool(false));
            _adapter.SetValue(MeshMaterialPropertyCatalog.Ids.BandsEnabled, VariableLayerValue.FromBool(false));
            _adapter.SetValue(MeshMaterialPropertyCatalog.Ids.EdgeFlowEnabled, VariableLayerValue.FromBool(false));
            _adapter.SetValue(MeshMaterialPropertyCatalog.Ids.InteriorNoiseEnabled, VariableLayerValue.FromBool(false));
            _adapter.SetValue(ContourSampleCountId, VariableLayerValue.FromInt(0));
            _adapter.SetValue(ContourBoundsId, VariableLayerValue.FromVector4(Vector4.zero));
            _adapter.SetVectorArray(ContourSamplesId, _contourSamples, 0);
            _adapter.Apply();
        }

        void SyncDefault(int nodeId, VariableLayerValue value)
        {
            if (!_needsDefaultResync &&
                _cachedDefaults.TryGetValue(nodeId, out var cached) &&
                VariableLayerValueUtility.Approximately(cached, value))
            {
                return;
            }

            _cachedDefaults[nodeId] = value;
            _layers.SetEntry(nodeId, "default", value, 0f, -1f, Ease.Linear);
        }

        void FlushDirty()
        {
            _layers.GetDirtyNodeIds(_dirtyNodeIds);
            for (var i = 0; i < _dirtyNodeIds.Count; i++)
            {
                var nodeId = _dirtyNodeIds[i];
                if (!_layers.TryGetResolvedValue(nodeId, out var value))
                {
                    _layers.ClearDirtyNode(nodeId);
                    continue;
                }

                if (_registry.TryGetMeshNode(nodeId, out var node) && node.ShaderPropertyId != 0)
                    _adapter.SetValue(node.ShaderPropertyId, value);

                _layers.ClearDirtyNode(nodeId);
            }

            _adapter.Apply();
        }

        void ApplyContourData(IReadOnlyList<Vector2[]> contourPaths, MeshContourSamplingPreset sampling)
        {
            if (!_adapter.SupportsContourEffects)
                return;

            var count = BuildContourSamples(contourPaths, sampling, _contourSamples, out var bounds);
            _adapter.SetValue(ContourSampleCountId, VariableLayerValue.FromInt(count));
            _adapter.SetValue(ContourBoundsId, VariableLayerValue.FromVector4(bounds));
            _adapter.SetVectorArray(ContourSamplesId, _contourSamples, count);
        }

        static int BuildContourSamples(
            IReadOnlyList<Vector2[]> contourPaths,
            MeshContourSamplingPreset sampling,
            Vector4[] output,
            out Vector4 bounds)
        {
            var maxSamples = Mathf.Clamp(sampling.MaxSamples, 4, MaxContourSamples);
            var minSpacing = Mathf.Max(0.001f, sampling.MinSampleSpacing);
            var count = 0;
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            var hasAny = false;
            var lastAccepted = Vector2.zero;

            if (contourPaths != null)
            {
                for (var pathIndex = 0; pathIndex < contourPaths.Count && count < maxSamples; pathIndex++)
                {
                    var path = contourPaths[pathIndex];
                    if (path == null || path.Length == 0)
                        continue;

                    for (var pointIndex = 0; pointIndex < path.Length && count < maxSamples; pointIndex++)
                    {
                        var point = path[pointIndex];
                        min = Vector2.Min(min, point);
                        max = Vector2.Max(max, point);

                        if (!hasAny || (point - lastAccepted).sqrMagnitude >= minSpacing * minSpacing)
                        {
                            output[count] = new Vector4(point.x, point.y, 0f, 0f);
                            lastAccepted = point;
                            count++;
                            hasAny = true;
                        }
                    }
                }
            }

            for (var i = count; i < output.Length; i++)
                output[i] = Vector4.zero;

            bounds = hasAny
                ? new Vector4(min.x, min.y, max.x, max.y)
                : Vector4.zero;

            return count;
        }
    }
}
