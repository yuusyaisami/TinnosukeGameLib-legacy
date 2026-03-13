using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Common;
using VContainer.Unity;

namespace Game.Direction
{
    public sealed class DirectionChannelHubService : IDirectionChannelHub, ITickable, IDirectionChannelHubTelemetry
    {
        const float DefaultRiseSpeed = 3.5f;
        const float DefaultFallSpeed = 7f;
        const float OppositeDotThreshold = -0.85f;
        const float DownwardBiasStrength = 0.65f;
        const float MovementEpsilon = 1e-5f;

        readonly Dictionary<string, DirectionChannelRuntime> _layers = new(StringComparer.Ordinal);
        readonly List<DirectionChannelRuntime> _sortedLayers = new();
        readonly DirectionOutput _output = new();
        readonly System.Action _markDirty;
        readonly System.Action _markSortDirty;

        Vector2 _targetVector;
        Vector2 _outputVector;
        float _transitionSpeedOverride = -1f;
        bool _sortDirty = true;
        bool _dirty = true;
        bool _disposed;
        int _telemetryVersion;

        public DirectionChannelHubService(List<DirectionLayerDef> layerDefs = null)
        {
            _markDirty = () =>
            {
                _dirty = true;
                BumpTelemetry();
            };
            _markSortDirty = () =>
            {
                _sortDirty = true;
                _dirty = true;
                BumpTelemetry();
            };

            if (layerDefs != null)
            {
                foreach (var def in layerDefs)
                {
                    if (string.IsNullOrEmpty(def.Tag))
                        continue;

                    RegisterLayer(def.Tag, def);
                }
            }
        }

        public IDirectionOutput Output => _output;

        public Vector2 Target => _targetVector;

        public int LayerCount => _layers.Count;
        public int TelemetryVersion => _telemetryVersion;

        public IDirectionChannelHandle RegisterLayer(string tag, DirectionLayerDef def)
        {
            if (string.IsNullOrEmpty(tag))
                throw new ArgumentException("Tag cannot be null or empty", nameof(tag));

            if (_layers.TryGetValue(tag, out var existing))
                return existing;

            var runtime = new DirectionChannelRuntime(tag, def, _markDirty, _markSortDirty);
            _layers[tag] = runtime;
            _sortedLayers.Add(runtime);
            _sortDirty = true;
            _dirty = true;
            BumpTelemetry();
            return runtime;
        }

        public void UnregisterLayer(string tag)
        {
            if (string.IsNullOrEmpty(tag))
                return;

            if (_layers.Remove(tag))
            {
                for (int i = 0; i < _sortedLayers.Count; i++)
                {
                    if (_sortedLayers[i].Tag == tag)
                    {
                        _sortedLayers.RemoveAt(i);
                        break;
                    }
                }
                _dirty = true;
                BumpTelemetry();
            }
        }

        public bool TryGetLayer(string tag, out IDirectionChannelHandle handle)
        {
            if (!string.IsNullOrEmpty(tag) && _layers.TryGetValue(tag, out var runtime))
            {
                handle = runtime;
                return true;
            }

            handle = null;
            return false;
        }

        public bool ContainsLayer(string tag) =>
            !string.IsNullOrEmpty(tag) && _layers.ContainsKey(tag);

        public void Tick()
        {
            if (_disposed)
                return;

            var beforeOutputVersion = _output.Version;

            if (_dirty || _sortDirty)
                RebuildTarget();

            // NOTE:
            // OutputValue must keep magnitude.
            // If normalized here, ZeroHoldThreshold / ActivationThreshold checks in
            // DirectionStateMachineOptionService become ineffective (OutputValue is almost always length=1).
            AdvanceOutput(Time.deltaTime);
            _output.SetValues(_targetVector, _outputVector);
            if (_output.Version != beforeOutputVersion)
                BumpTelemetry();
        }

        void RebuildTarget()
        {
            if (_sortDirty)
            {
                _sortedLayers.Sort((a, b) => a.Priority.CompareTo(b.Priority));
                _sortDirty = false;
            }

            Vector2 composed = Vector2.zero;
            float highestOverride = -1f;
            bool overrideApplied = false;
            int overridePriority = int.MaxValue;

            for (int i = 0; i < _sortedLayers.Count; i++)
            {
                var layer = _sortedLayers[i];
                if (!layer.Enabled)
                    continue;

                Vector2 direction = layer.Direction;
                if (direction.sqrMagnitude <= MovementEpsilon)
                    continue;

                if (layer.TransitionSpeedOverride >= 0f)
                    highestOverride = Mathf.Max(highestOverride, layer.TransitionSpeedOverride);

                if (layer.BlendMode == DirectionBlendMode.Override)
                {
                    if (overrideApplied && layer.Priority > overridePriority)
                        continue;

                    composed = direction * Mathf.Clamp01(layer.Influence);
                    overrideApplied = true;
                    overridePriority = layer.Priority;
                    continue;
                }

                if (overrideApplied && layer.Priority > overridePriority)
                    continue;

                float influence = Mathf.Clamp01(layer.Influence);
                switch (layer.BlendMode)
                {
                    case DirectionBlendMode.Add:
                        composed += direction * influence;
                        break;
                    case DirectionBlendMode.Weighted:
                        var normalized = direction.normalized;
                        if (normalized.sqrMagnitude > MovementEpsilon)
                            composed += normalized * influence;
                        break;
                    case DirectionBlendMode.MaxMagnitude:
                        if (direction.sqrMagnitude * influence > composed.sqrMagnitude)
                            composed = direction * influence;
                        break;
                }
            }

            _targetVector = composed;
            _transitionSpeedOverride = highestOverride;
            _dirty = false;
        }

        bool AdvanceOutput(float deltaTime)
        {
            var target = GetSafeTarget(_targetVector, _outputVector);
            var delta = target - _outputVector;
            float distance = delta.magnitude;
            if (distance <= MovementEpsilon)
            {
                if (_outputVector != _targetVector)
                {
                    _outputVector = _targetVector;
                    return true;
                }

                return false;
            }

            float speed = ComputeSpeed(_outputVector, _targetVector);
            float step = speed * deltaTime;
            if (step >= distance)
            {
                _outputVector = _targetVector;
                return true;
            }

            Vector2 direction = delta / distance;
            if (ShouldBiasDownward(_outputVector, _targetVector))
            {
                direction = Vector2.Lerp(direction, Vector2.down, DownwardBiasStrength);
                if (direction.sqrMagnitude > MovementEpsilon)
                    direction.Normalize();
            }

            _outputVector += direction * step;
            return true;
        }

        float ComputeSpeed(Vector2 current, Vector2 target)
        {
            float baseSpeed = target.magnitude > current.magnitude ? DefaultRiseSpeed : DefaultFallSpeed;
            if (_transitionSpeedOverride >= 0f)
            {
                baseSpeed = Mathf.Max(baseSpeed, _transitionSpeedOverride);
            }
            return baseSpeed;
        }

        Vector2 GetSafeTarget(Vector2 target, Vector2 current)
        {
            if (target.sqrMagnitude <= MovementEpsilon)
                return target;

            if (current.sqrMagnitude <= MovementEpsilon)
                return target;

            var currentDir = current.normalized;
            var targetDir = target.normalized;
            float dot = Vector2.Dot(currentDir, targetDir);
            if (dot >= OppositeDotThreshold)
                return target;

            float blend = Mathf.InverseLerp(-1f, OppositeDotThreshold, dot);
            var downward = Vector2.down * target.magnitude;
            return Vector2.Lerp(target, downward, 0.5f + 0.5f * blend);
        }

        bool ShouldBiasDownward(Vector2 current, Vector2 target)
        {
            if (target.sqrMagnitude <= MovementEpsilon || current.sqrMagnitude <= MovementEpsilon)
                return false;

            var currentDir = current.normalized;
            var targetDir = target.normalized;
            float dot = Vector2.Dot(currentDir, targetDir);
            return dot < OppositeDotThreshold;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _layers.Clear();
            _sortedLayers.Clear();
            BumpTelemetry();
        }

        public DirectionChannelHubTelemetrySnapshot GetTelemetrySnapshot()
        {
            var layers = new List<DirectionLayerTelemetrySnapshot>(_sortedLayers.Count);
            for (int i = 0; i < _sortedLayers.Count; i++)
            {
                var layer = _sortedLayers[i];
                layers.Add(new DirectionLayerTelemetrySnapshot(
                    layer.Tag,
                    layer.Enabled,
                    layer.Direction,
                    layer.Priority,
                    layer.BlendMode,
                    layer.Influence,
                    layer.TransitionSpeedOverride));
            }

            return new DirectionChannelHubTelemetrySnapshot(
                _telemetryVersion,
                _targetVector,
                _outputVector,
                _transitionSpeedOverride,
                _dirty,
                _sortDirty,
                DefaultRiseSpeed,
                DefaultFallSpeed,
                OppositeDotThreshold,
                DownwardBiasStrength,
                layers);
        }

        void BumpTelemetry()
        {
            unchecked
            {
                _telemetryVersion++;
            }
        }
    }
}
