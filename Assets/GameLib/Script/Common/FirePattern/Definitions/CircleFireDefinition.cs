#nullable enable
using System;
using System.Collections.Generic;
using Game.Common;
using Game.Search;
using Game.Vars.Generated;
using UnityEngine;

namespace Game.Fire
{
    [Serializable]
    public sealed class CircleFireDefinition : FireDefinition
    {
        [Header("Circle Settings")]
        [SerializeField] DynamicValue<int> _count = DynamicValueExtensions.FromLiteral(8);
        [SerializeField] DynamicValue<float> _radius = DynamicValueExtensions.FromLiteral(0f);
        [SerializeField] DynamicValue<float> _startAngle = DynamicValueExtensions.FromLiteral(0f);
        [SerializeField] DynamicValue<float> _endAngle = DynamicValueExtensions.FromLiteral(360f);
        [SerializeField] bool _includeEndAngle = false;

        [Header("Target Settings")]
        [SerializeField] TargetSelectionMode _targetMode = TargetSelectionMode.Nearest;

        public override FirePoint[] Build(
            Vector3 origin,
            Vector3 baseDirection,
            int reqeatIndex,
            IReadOnlyList<DynamicSearchHit> targetHits,
            IDynamicContext ctx)
        {
            int count = Mathf.Max(1, _count.Resolve(ctx));
            float radius = _radius.Resolve(ctx);
            float startAngle = _startAngle.Resolve(ctx);
            float endAngle = _endAngle.Resolve(ctx);

            if (ctx?.Vars != null)
            {
                ctx.Vars.TrySetVariant(VarIds.GameLib.SpawnPattern.FirePattern.CircleFire.circleCount, DynamicVariant.FromInt(count));
                ctx.Vars.TrySetVariant(VarIds.GameLib.SpawnPattern.FirePattern.CircleFire.circleRadius, DynamicVariant.FromFloat(radius));
                ctx.Vars.TrySetVariant(VarIds.GameLib.SpawnPattern.FirePattern.CircleFire.circleStartAngle, DynamicVariant.FromFloat(startAngle));
                ctx.Vars.TrySetVariant(VarIds.GameLib.SpawnPattern.FirePattern.CircleFire.circleEndAngle, DynamicVariant.FromFloat(endAngle));
            }

            var result = new FirePoint[count];

            float denom = _includeEndAngle ? (count - 1) : count;
            float angleStep = (count > 1 && denom > 0f)
                ? (endAngle - startAngle) / denom
                : 0f;

            var (targetDir, targetDist, hasTarget) = SelectTarget(origin, baseDirection, targetHits, _targetMode);

            for (int i = 0; i < count; i++)
            {
                float angle = startAngle + angleStep * i;
                var dir = Quaternion.Euler(0, 0, angle) * baseDirection;
                if (dir.sqrMagnitude <= 0.000001f) dir = Vector3.up;
                else dir = dir.normalized;

                var pos = origin + dir * radius;

                result[i] = new FirePoint(
                    index: i,
                    position: pos,
                    baseDirection: dir,
                    targetDirection: hasTarget ? targetDir : dir,
                    targetDistance: targetDist,
                    hasTarget: hasTarget,
                    targetHitCount: targetHits?.Count ?? 0,
                    normalizedPosition: count > 1 ? (float)i / (count - 1) : 0f,
                    totalCount: count,
                    fireRepeatIndex: reqeatIndex);
            }

            return result;
        }
    }
}
