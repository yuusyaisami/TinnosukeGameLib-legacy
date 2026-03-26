#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Channel
{
    abstract class MeshBaseClaySimulationRuntime : IMeshSimulationTrackRuntime
    {
        readonly Dictionary<string, List<Vector2[]>> _offsetsByCompositeTag = new(StringComparer.Ordinal);
        readonly Dictionary<string, List<Vector2[]>> _smoothWorkByCompositeTag = new(StringComparer.Ordinal);

        const float PenetrationWeight = 0.5f;
        const float MaxImpactPerHit = 1.5f;
        const float LateralEscapeStrength = 0.12f;
        const float SmoothingRate = 12f;
        const float DeepRecoverResistance = 4f;

        protected abstract float Radius { get; }
        protected abstract float Strength { get; }
        protected abstract float RecoverSpeed { get; }

        public void Reset()
        {
            _offsetsByCompositeTag.Clear();
            _smoothWorkByCompositeTag.Clear();
        }

        public void Apply(MeshSimulationContext context, MeshSimulationTrackRuntimeState track, MeshCompositeDraft composite)
        {
            if (!_offsetsByCompositeTag.TryGetValue(composite.Tag, out var offsets))
            {
                offsets = new List<Vector2[]>();
                _offsetsByCompositeTag[composite.Tag] = offsets;
            }

            if (!_smoothWorkByCompositeTag.TryGetValue(composite.Tag, out var smoothWork))
            {
                smoothWork = new List<Vector2[]>();
                _smoothWorkByCompositeTag[composite.Tag] = smoothWork;
            }

            EnsureOffsets(offsets, composite.Paths);
            EnsureOffsets(smoothWork, composite.Paths);
            DecayOffsets(context.DeltaTime, offsets);
            ApplyHits(context.Hits, composite.ColliderPreset, composite.Paths, offsets);
            SmoothOffsets(context.DeltaTime, offsets, smoothWork);
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

            for (var i = 0; i < offsets.Count; i++)
            {
                var buffer = offsets[i];
                for (var p = 0; p < buffer.Length; p++)
                {
                    var magnitude = buffer[p].magnitude;
                    var localRecover = recover / (1f + magnitude * DeepRecoverResistance);
                    var decay = Mathf.Exp(-localRecover * deltaTime);
                    buffer[p] *= decay;
                }
            }
        }

        void ApplyHits(
            IReadOnlyList<MeshHitContactInfo> hits,
            MeshPolygonTrackColliderPreset? colliderPreset,
            List<MeshRuntimePath> paths,
            List<Vector2[]> offsets)
        {
            if (hits == null || hits.Count == 0)
                return;

            var radius = Mathf.Max(0.001f, Radius);
            var sqrRadius = radius * radius;
            var velocityWeight = 1f;
            var impulseWeight = 1f;
            if (colliderPreset != null)
            {
                velocityWeight = Mathf.Max(0f, colliderPreset.VelocityWeight);
                impulseWeight = Mathf.Max(0f, colliderPreset.ImpulseWeight);
            }

            for (var h = 0; h < hits.Count; h++)
            {
                var hit = hits[h];
                var hitPoint = hit.ContactPoint;
                var hitNormal = hit.ContactNormal.sqrMagnitude > 0.0001f ? hit.ContactNormal.normalized : Vector2.up;
                var rawImpact = Mathf.Max(
                    0f,
                    hit.RelativeVelocity.magnitude * velocityWeight +
                    hit.ImpulseEstimate * impulseWeight +
                    hit.PenetrationEstimate * PenetrationWeight);
                var impact = Strength * Mathf.Sqrt(rawImpact);
                impact = Mathf.Min(impact, MaxImpactPerHit);
                if (impact <= 0.0001f)
                    continue;

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

                        var distance = Mathf.Sqrt(sqrDistance);
                        var t = Mathf.Clamp01(distance / radius);
                        var falloff = 1f - Smooth01(t);
                        var radialDir = distance > 0.0001f ? delta / distance : Vector2.zero;
                        var lateral = radialDir * (LateralEscapeStrength * (1f - falloff));
                        var deformDir = (-hitNormal + lateral);
                        if (deformDir.sqrMagnitude > 0.000001f)
                            deformDir.Normalize();
                        else
                            deformDir = -hitNormal;

                        buffer[p] += deformDir * impact * falloff * 0.01f;
                    }
                }
            }
        }

        void SmoothOffsets(float deltaTime, List<Vector2[]> offsets, List<Vector2[]> work)
        {
            var blend = 1f - Mathf.Exp(-SmoothingRate * Mathf.Max(0f, deltaTime));
            if (blend <= 0.0001f)
                return;

            for (var i = 0; i < offsets.Count; i++)
            {
                var buffer = offsets[i];
                if (buffer.Length < 3)
                    continue;

                var temp = work[i];
                temp[0] = buffer[0];
                temp[temp.Length - 1] = buffer[buffer.Length - 1];

                for (var p = 1; p < buffer.Length - 1; p++)
                {
                    var avg = (buffer[p - 1] + buffer[p] + buffer[p + 1]) / 3f;
                    temp[p] = Vector2.Lerp(buffer[p], avg, blend);
                }

                Array.Copy(temp, buffer, buffer.Length);
            }
        }

        static float Smooth01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
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
}
