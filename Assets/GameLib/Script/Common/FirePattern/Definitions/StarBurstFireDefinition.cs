#nullable enable
using System;
using System.Collections.Generic;
using Game.Common;
using Game.Search;
using UnityEngine;

namespace Game.Fire
{
    [Serializable]
    public sealed class StarBurstFireDefinition : FireDefinition
    {
        [Header("Star Settings")]
        [SerializeField] DynamicValue<int> _spikeCount = DynamicValueExtensions.FromLiteral(5);
        [SerializeField] DynamicValue<float> _outerRadius = DynamicValueExtensions.FromLiteral(2f);
        [SerializeField] DynamicValue<float> _innerRadius = DynamicValueExtensions.FromLiteral(0.85f);
        [SerializeField] DynamicValue<float> _rotationOffset = DynamicValueExtensions.FromLiteral(0f);
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
            var spikeCount = Mathf.Max(2, _spikeCount.Resolve(ctx));
            var outerRadius = Mathf.Max(0f, _outerRadius.Resolve(ctx));
            var innerRadius = Mathf.Max(0f, _innerRadius.Resolve(ctx));
            var rotationOffset = _rotationOffset.Resolve(ctx);

            var pointCount = spikeCount * 2;
            var result = new FirePoint[pointCount];
            var (targetDir, targetDist, hasTarget) = SelectTarget(origin, baseDirection, targetHits, _targetMode);

            for (var i = 0; i < pointCount; i++)
            {
                var progress = pointCount > 1 ? (float)i / (pointCount - 1) : 0f;
                var angle = rotationOffset + 360f * progress;
                var radialDir = RotateOrUp(baseDirection, angle);
                var radius = (i & 1) == 0 ? outerRadius : innerRadius;
                var spawnDirection = _tangentDirection ? RotateOrUp(radialDir, 90f) : radialDir;

                result[i] = new FirePoint(
                    index: i,
                    position: origin + radialDir * radius,
                    baseDirection: spawnDirection,
                    targetDirection: hasTarget ? targetDir : spawnDirection,
                    targetDistance: targetDist,
                    hasTarget: hasTarget,
                    targetHitCount: targetHits?.Count ?? 0,
                    normalizedPosition: progress,
                    totalCount: pointCount,
                    fireRepeatIndex: reqeatIndex);
            }

            return result;
        }

        public override Vector3[] GetPreviewPoints(int maxPoints = 100)
        {
            if (maxPoints <= 0)
                return Array.Empty<Vector3>();

            var spikeCount = Mathf.Max(2, _spikeCount.GetOrDefaultWithoutContext(5));
            var outerRadius = Mathf.Max(0f, _outerRadius.GetOrDefaultWithoutContext(2f));
            var innerRadius = Mathf.Max(0f, _innerRadius.GetOrDefaultWithoutContext(0.85f));
            var rotationOffset = _rotationOffset.GetOrDefaultWithoutContext(0f);

            var pointCount = Mathf.Clamp(spikeCount * 2, 4, 128);
            var points = new Vector3[pointCount];

            for (var i = 0; i < pointCount; i++)
            {
                var progress = pointCount > 1 ? (float)i / (pointCount - 1) : 0f;
                var angle = rotationOffset + 360f * progress;
                var radialDir = RotateOrUp(Vector3.up, angle);
                var radius = (i & 1) == 0 ? outerRadius : innerRadius;
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