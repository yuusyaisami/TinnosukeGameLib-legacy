#nullable enable
using System;
using System.Collections.Generic;
using Game.Common;
using Game.Search;
using UnityEngine;

namespace Game.Fire
{
    [Serializable]
    public sealed class SpreadFireDefinition : FireDefinition
    {
        [Header("Spread Settings")]
        [SerializeField] DynamicValue<int> _count = DynamicValueExtensions.FromLiteral(5);
        [SerializeField] DynamicValue<float> _spreadAngle = DynamicValueExtensions.FromLiteral(30f);
        [SerializeField] bool _centerAligned = true;

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
            float spreadAngle = _spreadAngle.Resolve(ctx);

            if (ctx?.Vars != null)
            {
                if (VarIdResolver.TryResolve("spreadCount", out var varId) && varId != 0)
                    ctx.Vars.TrySetVariant(varId, DynamicVariant.FromInt(count));
                if (VarIdResolver.TryResolve("spreadAngle", out varId) && varId != 0)
                    ctx.Vars.TrySetVariant(varId, DynamicVariant.FromFloat(spreadAngle));
            }

            var result = new FirePoint[count];
            var (targetDir, targetDist, hasTarget) = SelectTarget(origin, baseDirection, targetHits, _targetMode);

            float startAngle = _centerAligned ? -spreadAngle / 2f : 0f;
            float angleStep = count > 1 ? spreadAngle / (count - 1) : 0f;

            for (int i = 0; i < count; i++)
            {
                float angle = startAngle + angleStep * i;
                var dir = Quaternion.Euler(0, 0, angle) * baseDirection;
                if (dir.sqrMagnitude <= 0.000001f) dir = Vector3.up;
                else dir = dir.normalized;

                result[i] = new FirePoint(
                    index: i,
                    position: origin,
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
