#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Channel
{
    sealed class MeshFluidSimulationRuntime : IMeshSimulationTrackRuntime
    {
        readonly struct RippleEval
        {
            public readonly RippleState Ripple;
            public readonly float Amplitude;
            public readonly float Age;
            public readonly float WaveFront;
            public readonly float MinDistSq;
            public readonly float MaxDistSq;

            public RippleEval(
                RippleState ripple,
                float amplitude,
                float age,
                float waveFront,
                float minDistSq,
                float maxDistSq)
            {
                Ripple = ripple;
                Amplitude = amplitude;
                Age = age;
                WaveFront = waveFront;
                MinDistSq = minDistSq;
                MaxDistSq = maxDistSq;
            }
        }

        sealed class RippleState
        {
            public Vector2 Point;
            public Vector2 Normal;
            public float Amplitude;
            public float StartedAt;
            public float PhaseOffset;
            public float SpatialFrequency;
            public float TemporalFrequency;
            public float WaveSpeed;
        }

        readonly MeshFluidSimulationPreset _preset;
        readonly Dictionary<string, List<RippleState>> _ripplesByCompositeTag = new(StringComparer.Ordinal);
        readonly List<RippleEval> _activeRipples = new();

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
            _ = track;

            if (!_ripplesByCompositeTag.TryGetValue(composite.Tag, out var ripples))
            {
                ripples = new List<RippleState>();
                _ripplesByCompositeTag[composite.Tag] = ripples;
            }

            for (var i = 0; i < context.Hits.Count; i++)
            {
                var hit = context.Hits[i];
                var impact = Mathf.Max(0f, hit.RelativeVelocity.magnitude + hit.ImpulseEstimate);
                var scaledImpact = Mathf.Sqrt(impact);
                var amplitude = _preset.WaveStrength * scaledImpact * Mathf.Max(0f, _preset.ImpactScale);
                amplitude = Mathf.Min(amplitude, Mathf.Max(0f, _preset.MaxAmplitude));
                if (amplitude <= 0.0001f)
                    continue;

                var jitter = Mathf.Clamp(_preset.FrequencyJitter, 0f, 0.5f);
                var minScale = 1f - jitter;
                var maxScale = 1f + jitter;
                var frequencyScale = UnityEngine.Random.Range(minScale, maxScale);

                ripples.Add(new RippleState
                {
                    Point = hit.ContactPoint,
                    Normal = hit.ContactNormal.sqrMagnitude > 0.0001f ? hit.ContactNormal.normalized : Vector2.up,
                    Amplitude = amplitude,
                    StartedAt = context.TimeSeconds,
                    PhaseOffset = UnityEngine.Random.Range(0f, Mathf.PI * 2f),
                    SpatialFrequency = Mathf.Max(0f, _preset.SpatialFrequency) * frequencyScale,
                    TemporalFrequency = Mathf.Max(0f, _preset.TemporalFrequency) * frequencyScale,
                    WaveSpeed = Mathf.Max(0f, _preset.WaveSpeed) * frequencyScale,
                });
            }

            var maxActiveRipples = Mathf.Max(1, _preset.MaxActiveRipples);
            var overflow = ripples.Count - maxActiveRipples;
            if (overflow > 0)
                ripples.RemoveRange(0, overflow);

            var radius = Mathf.Max(0.001f, _preset.Radius);
            var bandWidth = Mathf.Max(0.001f, _preset.BandWidth);
            var damping = Mathf.Max(0f, _preset.Damping);
            var distanceDamping = Mathf.Max(0f, _preset.DistanceDamping);
            var distanceFalloffWeight = Mathf.Clamp01(_preset.DistanceFalloffWeight);
            var radialBlend = Mathf.Clamp01(_preset.RadialBlend);
            var radialFalloffWeight = Mathf.Clamp01(_preset.RadialFalloffWeight);
            var edgeSoftness = Mathf.Clamp01(_preset.EdgeSoftness);
            var softEdgeWidth = Mathf.Max(0.0001f, radius * edgeSoftness);
            var maxReach = radius + softEdgeWidth;

            _activeRipples.Clear();

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

                var waveFront = age * ripple.WaveSpeed;
                if (waveFront - bandWidth > maxReach)
                {
                    ripples.RemoveAt(i);
                    continue;
                }

                var minDist = Mathf.Max(0f, waveFront - bandWidth);
                var maxDist = Mathf.Min(maxReach, waveFront + bandWidth);
                _activeRipples.Add(new RippleEval(
                    ripple,
                    amplitude,
                    age,
                    waveFront,
                    minDist * minDist,
                    maxDist * maxDist));
            }

            if (_activeRipples.Count == 0)
                return;

            for (var p = 0; p < composite.Paths.Count; p++)
            {
                var points = composite.Paths[p].Points;
                if (points.Count == 0)
                    continue;

                var baseline = new Vector2[points.Count];
                var offsets = new Vector2[points.Count];
                for (var n = 0; n < points.Count; n++)
                    baseline[n] = points[n];

                for (var r = 0; r < _activeRipples.Count; r++)
                {
                    var eval = _activeRipples[r];
                    var ripple = eval.Ripple;

                    for (var n = 0; n < baseline.Length; n++)
                    {
                        var delta = baseline[n] - ripple.Point;
                        var distSq = delta.sqrMagnitude;
                        if (distSq < eval.MinDistSq || distSq > eval.MaxDistSq)
                            continue;

                        var distance = Mathf.Sqrt(distSq);
                        var frontDelta = Mathf.Abs(distance - eval.WaveFront);
                        if (frontDelta > bandWidth)
                            continue;

                        var bandT = Mathf.Clamp01(frontDelta / bandWidth);
                        var bandEnvelope = 1f - Smooth01(bandT);

                        var edgeFade = 1f;
                        if (distance > radius)
                        {
                            var edgeT = Mathf.Clamp01((distance - radius) / softEdgeWidth);
                            edgeFade = 1f - Smooth01(edgeT);
                        }

                        var radialT = Mathf.Clamp01(distance / radius);
                        var radialFalloff = Mathf.Lerp(1f, 1f - Smooth01(radialT), radialFalloffWeight);

                        var distanceFade = Mathf.Lerp(
                            1f,
                            Mathf.Exp(-distance * distanceDamping),
                            distanceFalloffWeight);

                        var wave = Mathf.Sin(distance * ripple.SpatialFrequency - eval.Age * ripple.TemporalFrequency + ripple.PhaseOffset);

                        var radialDir = distance > 0.0001f ? delta / distance : Vector2.zero;
                        var deformDir = ripple.Normal * (1f - radialBlend) + radialDir * radialBlend;
                        if (deformDir.sqrMagnitude <= 0.000001f)
                            deformDir = ripple.Normal;
                        else
                            deformDir.Normalize();

                        var displacement = wave * eval.Amplitude * bandEnvelope * edgeFade * radialFalloff * distanceFade;
                        offsets[n] += deformDir * displacement;
                    }
                }

                for (var n = 0; n < baseline.Length; n++)
                    points[n] = baseline[n] + offsets[n];
            }
        }

        static float Smooth01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
