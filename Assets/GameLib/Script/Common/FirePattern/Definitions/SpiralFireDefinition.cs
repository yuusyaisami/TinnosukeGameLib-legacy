#nullable enable
using System;
using System.Collections.Generic;
using Game.Common;
using Game.Search;
using UnityEngine;

namespace Game.Fire
{
    [Serializable]
    public sealed class SpiralFireDefinition : FireDefinition
    {
        [Header("Spiral Settings")]
        [SerializeField] DynamicValue<int> _count = DynamicValueExtensions.FromLiteral(12);
        [SerializeField] DynamicValue<float> _revolutions = DynamicValueExtensions.FromLiteral(2f);
        [SerializeField] DynamicValue<float> _startRadius = DynamicValueExtensions.FromLiteral(0.2f);
        [SerializeField] DynamicValue<float> _endRadius = DynamicValueExtensions.FromLiteral(2f);
        [SerializeField] DynamicValue<float> _rotationOffset = DynamicValueExtensions.FromLiteral(0f);
        [SerializeField] bool _tangentDirection = true;

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
            var revolutions = _revolutions.Resolve(ctx);
            var startRadius = Mathf.Max(0f, _startRadius.Resolve(ctx));
            var endRadius = Mathf.Max(0f, _endRadius.Resolve(ctx));
            var rotationOffset = _rotationOffset.Resolve(ctx);

            var result = new FirePoint[count];
            var (targetDir, targetDist, hasTarget) = SelectTarget(origin, baseDirection, targetHits, _targetMode);

            for (var i = 0; i < count; i++)
            {
                var progress = count > 1 ? (float)i / (count - 1) : 0f;
                var angle = rotationOffset + revolutions * 360f * progress;
                var radialDir = RotateOrUp(baseDirection, angle);
                var spawnDirection = _tangentDirection ? RotateOrUp(radialDir, 90f) : radialDir;
                var radius = Mathf.Lerp(startRadius, endRadius, progress);

                result[i] = new FirePoint(
                    index: i,
                    position: origin + radialDir * radius,
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

            var count = Mathf.Clamp(maxPoints, 16, 128);
            var points = new Vector3[count];

            var revolutions = Mathf.Max(0f, _revolutions.GetOrDefaultWithoutContext(2f));
            var startRadius = Mathf.Max(0f, _startRadius.GetOrDefaultWithoutContext(0.2f));
            var endRadius = Mathf.Max(0f, _endRadius.GetOrDefaultWithoutContext(2f));
            var rotationOffset = _rotationOffset.GetOrDefaultWithoutContext(0f);

            for (var i = 0; i < count; i++)
            {
                var progress = count > 1 ? (float)i / (count - 1) : 0f;
                var angle = rotationOffset + revolutions * 360f * progress;
                var radialDir = RotateOrUp(Vector3.up, angle);
                var radius = Mathf.Lerp(startRadius, endRadius, progress);
                points[i] = radialDir * radius;
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