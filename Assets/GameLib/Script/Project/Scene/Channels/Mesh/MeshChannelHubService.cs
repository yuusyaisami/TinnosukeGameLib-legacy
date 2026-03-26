#nullable enable
using System;
using System.Collections.Generic;
using DG.Tweening;
using Game.VariableLayer;
using UnityEngine;
using VContainer.Unity;

namespace Game.Channel
{
    public sealed class MeshChannelHubService :
        IMeshChannelHubService,
        IMeshChannelControlService,
        IMeshMaterialFxControlService,
        IScopeAcquireHandler,
        IScopeReleaseHandler,
        ITickable,
        IDisposable
    {
        readonly Dictionary<string, MeshChannelPlayerRuntime> _playersByTag = new(StringComparer.Ordinal);
        readonly List<IMeshChannelPlayerRuntime> _players = new();
        readonly IScopeNode _scope;
        readonly Transform _ownerTransform;
        readonly Shader? _defaultShader;

        public IReadOnlyList<IMeshChannelPlayerRuntime> Players => _players;

        public MeshChannelHubService(
            MeshChannelEntry[] entries,
            IScopeNode scope,
            Transform ownerTransform,
            Shader? defaultShader)
        {
            _scope = scope;
            _ownerTransform = ownerTransform;
            _defaultShader = defaultShader;

            if (entries == null)
                return;

            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry == null)
                    continue;

                var tag = string.IsNullOrWhiteSpace(entry.Tag) ? "default" : entry.Tag.Trim();
                var runtime = new MeshChannelPlayerRuntime(tag, entry.Definition, scope, ownerTransform, defaultShader);
                _playersByTag[tag] = runtime;
                _players.Add(runtime);
            }
        }

        public bool TryGetPlayer(string tag, out IMeshChannelPlayerRuntime player)
        {
            tag = NormalizeTag(tag);
            if (_playersByTag.TryGetValue(tag, out var runtime))
            {
                player = runtime;
                return true;
            }

            player = null!;
            return false;
        }

        public IMeshChannelPlayerRuntime GetPlayer(string tag)
        {
            tag = NormalizeTag(tag);
            if (_playersByTag.TryGetValue(tag, out var runtime))
                return runtime;

            throw new KeyNotFoundException($"[MeshChannelHub] Channel '{tag}' not found.");
        }

        public bool SwapRootDefinition(string tag, MeshDefinitionPreset preset)
        {
            var runtime = GetOrCreateRuntime(tag);
            return runtime.SwapRootDefinition(preset);
        }

        public bool SwapTrackDefinition(string tag, string key, MeshTrackDefinition definition)
        {
            var runtime = GetOrCreateRuntime(tag);
            return runtime.SwapTrackDefinition(key, definition);
        }

        public bool MutateTrackVisualizer(string tag, string key, MeshTrackVisualizerRuntimeMutation mutation)
        {
            return TryGetRuntime(tag, out var runtime) && runtime.MutateTrackVisualizer(key, mutation);
        }

        public bool MutateTrackPlayer(string tag, string key, MeshTrackPlayerRuntimeMutation mutation)
        {
            return TryGetRuntime(tag, out var runtime) && runtime.MutateTrackPlayer(key, mutation);
        }

        public bool MutateTrackCollider(string tag, string key, MeshTrackColliderRuntimeMutation mutation)
        {
            return TryGetRuntime(tag, out var runtime) && runtime.MutateTrackCollider(key, mutation);
        }

        public bool MutateTrackMaterial(string tag, string key, MeshTrackMaterialRuntimeMutation mutation)
        {
            return TryGetRuntime(tag, out var runtime) && runtime.MutateTrackMaterial(key, mutation);
        }

        public bool MutateSimulationTrack(string tag, string key, MeshSimulationTrackRuntimeMutation mutation)
        {
            return TryGetRuntime(tag, out var runtime) && runtime.MutateSimulationTrack(key, mutation);
        }

        public bool ResetRuntimeOverrides(
            string tag,
            bool resetVisualizer,
            bool resetPlayer,
            bool resetCollider,
            bool resetMaterial,
            bool resetSimulation)
        {
            return TryGetRuntime(tag, out var runtime) &&
                   runtime.ResetRuntimeOverrides(
                       resetVisualizer,
                       resetPlayer,
                       resetCollider,
                       resetMaterial,
                       resetSimulation);
        }

        public bool SetTrackEnabled(string tag, string key, bool enabled)
        {
            return TryGetRuntime(tag, out var runtime) && runtime.SetTrackEnabled(key, enabled);
        }

        public bool SetEntry(string channelTag, string compositeTag, int nodeId, string tag, VariableLayerValue value, float lifetimeSeconds = -1f)
        {
            return TryGetRuntime(channelTag, out var runtime) &&
                   runtime.SetMaterialEntry(compositeTag, nodeId, tag, value, lifetimeSeconds);
        }

        public bool SetEntryFade(string channelTag, string compositeTag, int nodeId, string tag, VariableLayerValue value, float durationSeconds, Ease ease, float lifetimeSeconds = -1f)
        {
            return TryGetRuntime(channelTag, out var runtime) &&
                   runtime.SetMaterialEntryFade(compositeTag, nodeId, tag, value, durationSeconds, ease, lifetimeSeconds);
        }

        public bool RemoveTag(string channelTag, string compositeTag, int nodeId, string tag)
        {
            return TryGetRuntime(channelTag, out var runtime) &&
                   runtime.RemoveMaterialTag(compositeTag, nodeId, tag);
        }

        public bool ClearContext(string channelTag, string compositeTag, string tag)
        {
            return TryGetRuntime(channelTag, out var runtime) &&
                   runtime.ClearMaterialContext(compositeTag, tag);
        }

        public bool ClearNode(string channelTag, string compositeTag, int nodeId)
        {
            return TryGetRuntime(channelTag, out var runtime) &&
                   runtime.ClearMaterialNode(compositeTag, nodeId);
        }

        public bool ResetDefaults(string channelTag, string compositeTag)
        {
            return TryGetRuntime(channelTag, out var runtime) &&
                   runtime.ResetMaterialDefaults(compositeTag);
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;

            foreach (var pair in _playersByTag)
                pair.Value.OnAcquire();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;

            foreach (var pair in _playersByTag)
                pair.Value.OnRelease();
        }

        public void Tick()
        {
            var dt = Time.deltaTime;
            var frameIndex = Time.frameCount;
            foreach (var pair in _playersByTag)
                pair.Value.Tick(frameIndex, dt);
        }

        public void Dispose()
        {
            foreach (var pair in _playersByTag)
                pair.Value.Dispose();
        }

        bool TryGetRuntime(string tag, out MeshChannelPlayerRuntime runtime)
        {
            tag = NormalizeTag(tag);
            return _playersByTag.TryGetValue(tag, out runtime!);
        }

        MeshChannelPlayerRuntime GetOrCreateRuntime(string tag)
        {
            tag = NormalizeTag(tag);
            if (_playersByTag.TryGetValue(tag, out var runtime))
                return runtime;

            runtime = new MeshChannelPlayerRuntime(
                tag,
                MeshChannelDynamicValueFactory.FromManaged(new MeshDefinitionPreset()),
                _scope,
                _ownerTransform,
                _defaultShader);
            _playersByTag[tag] = runtime;
            _players.Add(runtime);
            runtime.OnAcquire();
            return runtime;
        }

        static string NormalizeTag(string tag)
        {
            return string.IsNullOrWhiteSpace(tag) ? "default" : tag.Trim();
        }
    }
}
