#nullable enable
using System;
using System.Collections.Generic;
using Game.Common;
using Game.Search;
using UnityEngine;

namespace Game.Fire
{
    [Serializable]
    public abstract class FireDefinition
    {
        public abstract FirePoint[] Build(
            Vector3 origin,
            Vector3 baseDirection,
            int reqeatIndex,
            IReadOnlyList<DynamicSearchHit> targetHits,
            IDynamicContext ctx);

        public virtual Vector3[] GetPreviewPoints(int maxPoints = 100) => Array.Empty<Vector3>();

        protected static (Vector3 TargetDir, float TargetDist, bool HasTarget) SelectTarget(
            Vector3 origin,
            Vector3 fallbackDirection,
            IReadOnlyList<DynamicSearchHit> hits,
            TargetSelectionMode mode)
        {
            if (hits == null || hits.Count == 0)
                return (fallbackDirection, 0f, false);

            // v0.1: All / ByIndex は予約値として Nearest にフォールバック
            if (mode == TargetSelectionMode.All || mode == TargetSelectionMode.ByIndex)
                mode = TargetSelectionMode.Nearest;

            if (mode == TargetSelectionMode.None)
                return (fallbackDirection, 0f, false);

            int bestIndex = -1;
            float bestDistSq = (mode == TargetSelectionMode.Farthest) ? float.MinValue : float.MaxValue;

            Vector2 origin2 = new Vector2(origin.x, origin.y);

            if (mode == TargetSelectionMode.Random)
            {
                bestIndex = UnityEngine.Random.Range(0, hits.Count);
            }
            else
            {
                for (int i = 0; i < hits.Count; i++)
                {
                    var hit = hits[i];

                    Vector2 hitPos = new Vector2(hit.Position.x, hit.Position.y);
                    float distSq = (hitPos - origin2).sqrMagnitude;

                    if (mode == TargetSelectionMode.Farthest)
                    {
                        if (distSq > bestDistSq)
                        {
                            bestDistSq = distSq;
                            bestIndex = i;
                        }
                    }
                    else
                    {
                        if (distSq < bestDistSq)
                        {
                            bestDistSq = distSq;
                            bestIndex = i;
                        }
                    }
                }
            }

            if (bestIndex < 0)
                return (fallbackDirection, 0f, false);

            var best = hits[bestIndex];
            Vector2 targetPos2 = new Vector2(best.Position.x, best.Position.y);
            var diff2 = targetPos2 - origin2;
            float dist = diff2.magnitude;

            Vector3 dir;
            if (dist > 0.0001f)
                dir = new Vector3(diff2.x / dist, diff2.y / dist, 0f);
            else
                dir = fallbackDirection;

            if (dir.sqrMagnitude <= 0.000001f)
                dir = Vector3.up;
            else
                dir = dir.normalized;

            return (dir, dist, true);
        }
    }
}
