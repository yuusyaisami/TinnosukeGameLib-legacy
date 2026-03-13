#nullable enable
using System;
using System.Collections.Generic;
using Game.Common;
using Game.Search;
using Game.Vars.Generated;
using UnityEngine;
using VInspector;

namespace Game.Fire
{
    [Serializable]
    public sealed class BurstFireDefinition : FireDefinition
    {
        [Header("Burst Settings")]
        [SerializeField] DynamicValue<int> _burstCount = DynamicValueExtensions.FromLiteral(3);
        [SerializeField] DynamicValue<float> _burstInterval = DynamicValueExtensions.FromLiteral(0.1f);
        enum BurstAngleMode { TotalSpread, PerShot }
        [Header("Spread Settings")]
        [SerializeField] BurstAngleMode _angleMode = BurstAngleMode.TotalSpread;
        [SerializeField, ShowIf(nameof(_angleMode), BurstAngleMode.TotalSpread), Tooltip("When AngleMode=TotalSpread: total spread angle (degrees) across the burst")] DynamicValue<float> _totalSpread = DynamicValueExtensions.FromLiteral(30f);
        [SerializeField, ShowIf(nameof(_angleMode), BurstAngleMode.PerShot), Tooltip("When AngleMode=PerShot: degrees between consecutive shots")] DynamicValue<float> _perShotDegrees = DynamicValueExtensions.FromLiteral(10f);
        [SerializeField, Tooltip("If true, the spread is centered around baseDirection; otherwise starts at baseDirection and fans out")] bool _centerAligned = true;

        [Header("Target Settings")]
        [SerializeField] TargetSelectionMode _targetMode = TargetSelectionMode.Nearest;

        public override FirePoint[] Build(
            Vector3 origin,
            Vector3 baseDirection,
            int reqeatIndex,
            IReadOnlyList<DynamicSearchHit> targetHits,
            IDynamicContext ctx)
        {
            int count = Mathf.Max(1, _burstCount.Resolve(ctx));
            float interval = _burstInterval.Resolve(ctx);

            float totalSpread = 0f;
            float angleStep = 0f;
            if (count > 1)
            {
                if (_angleMode == BurstAngleMode.TotalSpread)
                {
                    totalSpread = _totalSpread.Resolve(ctx);
                    angleStep = totalSpread / (count - 1);
                }
                else
                {
                    angleStep = _perShotDegrees.Resolve(ctx);
                    totalSpread = angleStep * (count - 1);
                }
            }

            if (ctx?.Vars != null)
            {
                // DelayTime の式から参照できるように公開
                ctx.Vars.TrySetVariant(VarIds.GameLib.SpawnPattern.FirePattern.BurstFire.burstCount, DynamicVariant.FromInt(count));
                ctx.Vars.TrySetVariant(VarIds.GameLib.SpawnPattern.FirePattern.BurstFire.burstInterval, DynamicVariant.FromFloat(interval));
            }

            var result = new FirePoint[count];
            var (targetDir, targetDist, hasTarget) = SelectTarget(origin, baseDirection, targetHits, _targetMode);

            float startAngle = 0f;
            if (count > 1)
            {
                startAngle = _centerAligned ? -totalSpread / 2f : 0f;
            }

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
