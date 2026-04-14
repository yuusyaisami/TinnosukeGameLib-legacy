#nullable enable
using System;
using System.Collections.Generic;
using Game.Common;
using Game.Search;
using UnityEngine;

namespace Game.Fire
{
    [Serializable]
    public sealed class FlowerFireDefinition : FireDefinition
    {
        [Header("Flower Settings")]
        [SerializeField] DynamicValue<int> _count = DynamicValueExtensions.FromLiteral(16);
        [SerializeField] DynamicValue<int> _petalCount = DynamicValueExtensions.FromLiteral(5);
        [SerializeField] DynamicValue<float> _radius = DynamicValueExtensions.FromLiteral(1.5f);
        [SerializeField] DynamicValue<float> _petalAmplitude = DynamicValueExtensions.FromLiteral(0.85f);
        [SerializeField] DynamicValue<float> _rotationOffset = DynamicValueExtensions.FromLiteral(0f);
        [SerializeField] DynamicValue<float> _phaseOffset = DynamicValueExtensions.FromLiteral(0f);
        [SerializeField] bool _tangentDirection = false;

        [Header("Target Settings")]
        [SerializeField] TargetSelectionMode _targetMode = TargetSelectionMode.Nearest;

        public override FirePoint[] Build(
            Vector3 origin,
            Vector3 baseDirection,
            int reqeatIndex,
            IReadOnlyList<DynamicSearchHit> targetHits,
            IDynamicContext ctx)
        {
            var count = Mathf.Max(1, _count.Resolve(ctx));
            var petalCount = Mathf.Max(1, _petalCount.Resolve(ctx));
            var radius = Mathf.Max(0f, _radius.Resolve(ctx));
            var amplitude = Mathf.Max(0f, _petalAmplitude.Resolve(ctx));
            var rotationOffset = _rotationOffset.Resolve(ctx);
            var phaseOffset = _phaseOffset.Resolve(ctx);

            var result = new FirePoint[count];
            var (targetDir, targetDist, hasTarget) = SelectTarget(origin, baseDirection, targetHits, _targetMode);

            for (var i = 0; i < count; i++)
            {
                var progress = count > 1 ? (float)i / (count - 1) : 0f;
                var angle = rotationOffset + 360f * progress;
                var radialDir = RotateOrUp(baseDirection, angle);
                var petalWave = Mathf.Sin((petalCount * angle + phaseOffset) * Mathf.Deg2Rad);
                var radialDistance = Mathf.Max(0f, radius + petalWave * amplitude);
                var spawnDirection = _tangentDirection ? RotateOrUp(radialDir, 90f) : radialDir;

                result[i] = new FirePoint(
                    index: i,
                    position: origin + radialDir * radialDistance,
                    baseDirection: spawnDirection,
                    targetDirection: hasTarget ? targetDir : spawnDirection,
                    targetDistance: targetDist,
                    hasTarget: hasTarget,
                    targetHitCount: targetHits?.Count ?? 0,
                    normalizedPosition: progress,
                    totalCount: count,
                    fireRepeatIndex: reqeatIndex);
            }

            return result;
        }

        public override Vector3[] GetPreviewPoints(int maxPoints = 100)
        {
            if (maxPoints <= 0)
                return Array.Empty<Vector3>();

            var count = Mathf.Clamp(maxPoints, 24, 128);
            var points = new Vector3[count];

            var petalCount = Mathf.Max(1, _petalCount.GetOrDefaultWithoutContext(5));
            var radius = Mathf.Max(0f, _radius.GetOrDefaultWithoutContext(1.5f));
            var amplitude = Mathf.Max(0f, _petalAmplitude.GetOrDefaultWithoutContext(0.85f));
            var rotationOffset = _rotationOffset.GetOrDefaultWithoutContext(0f);
            var phaseOffset = _phaseOffset.GetOrDefaultWithoutContext(0f);

            for (var i = 0; i < count; i++)
            {
                var progress = count > 1 ? (float)i / (count - 1) : 0f;
                var angle = rotationOffset + 360f * progress;
                var radialDir = RotateOrUp(Vector3.up, angle);
                var petalWave = Mathf.Sin((petalCount * angle + phaseOffset) * Mathf.Deg2Rad);
                var radialDistance = Mathf.Max(0f, radius + petalWave * amplitude);
                points[i] = radialDir * radialDistance;
            }

            return points;
        }

        static Vector3 RotateOrUp(Vector3 direction, float degrees)
        {
            if (direction.sqrMagnitude <= 0.000001f)
                direction = Vector3.up;

            var rotated = Quaternion.Euler(0f, 0f, degrees) * direction.normalized;
            if (rotated.sqrMagnitude <= 0.000001f)
                return Vector3.up;

            return rotated.normalized;
        }
    }
}