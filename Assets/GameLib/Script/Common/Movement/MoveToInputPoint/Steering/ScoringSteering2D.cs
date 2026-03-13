#nullable enable
using UnityEngine;

namespace Game.Movement
{
    public sealed class ScoringSteering2D : IMoveToInputPointSteering2D
    {
        public Vector2 Compute(in MoveToInputPointSteeringContext ctx)
        {
            var prevTriggers = Physics2D.queriesHitTriggers;
            Physics2D.queriesHitTriggers = ctx.UseTriggers;
            try
            {
            var desired = ctx.DesiredDir;
            float lenSq = desired.sqrMagnitude;
            if (lenSq < 0.000001f)
                return Vector2.zero;

            desired /= Mathf.Sqrt(lenSq);

            int n = Mathf.Max(1, ctx.SamplesPerSide);
            float maxDeg = Mathf.Clamp(ctx.MaxAvoidAngleDeg, 1f, 179f);
            float step = maxDeg / n;

            float bestScore = float.NegativeInfinity;
            Vector2 bestDir = desired;
            float bestFree = -1f;

            Vector2 perp = new Vector2(-desired.y, desired.x);
            float bias = Mathf.Clamp(ctx.SideBias, -0.5f, 0.5f);
            float maxScore = ctx.AlignmentWeight + Mathf.Abs(bias);
            float probeDist = Mathf.Max(0.0001f, ctx.ProbeDistance);

            for (int k = 0; k <= n; k++)
            {
                if (k == 0)
                {
                    Evaluate(desired, 0f, in ctx, desired, perp, bias, ref bestScore, ref bestDir, ref bestFree);
                    if (bestFree >= probeDist && bestScore >= maxScore - 0.0001f)
                        break;
                }
                else
                {
                    Evaluate(desired, +k * step, in ctx, desired, perp, bias, ref bestScore, ref bestDir, ref bestFree);
                    if (bestFree >= probeDist && bestScore >= maxScore - 0.0001f)
                        break;
                    Evaluate(desired, -k * step, in ctx, desired, perp, bias, ref bestScore, ref bestDir, ref bestFree);
                    if (bestFree >= probeDist && bestScore >= maxScore - 0.0001f)
                        break;
                }
            }

            float outLenSq = bestDir.sqrMagnitude;
            if (outLenSq < 0.000001f) return Vector2.zero;
            return bestDir / Mathf.Sqrt(outLenSq);
            }
            finally
            {
                Physics2D.queriesHitTriggers = prevTriggers;
            }
        }

        static void Evaluate(
            Vector2 baseDir,
            float angleDeg,
            in MoveToInputPointSteeringContext ctx,
            Vector2 desiredDir,
            Vector2 desiredPerp,
            float sideBias,
            ref float bestScore,
            ref Vector2 bestDir,
            ref float bestFree)
        {
            Vector2 cand = Rotate(baseDir, angleDeg);

            float free = ProbeFreeDistance(ctx.CurrentPos, cand, ctx.AgentRadius, ctx.ProbeDistance, ctx.ObstacleMask, ctx.UseTriggers);

            float align = Mathf.Clamp01(Vector2.Dot(desiredDir, cand));

            float obstaclePenalty = 1f - Mathf.Clamp01(free / Mathf.Max(0.0001f, ctx.ProbeDistance));

            float side = Vector2.Dot(desiredPerp, cand);
            float sideTerm = side * sideBias;

            float score =
                align * ctx.AlignmentWeight
                - obstaclePenalty * ctx.ObstacleWeight
                + sideTerm;

            if (score > bestScore || (Mathf.Approximately(score, bestScore) && free > bestFree))
            {
                bestScore = score;
                bestDir = cand;
                bestFree = free;
            }
        }

        static float ProbeFreeDistance(
            Vector2 origin,
            Vector2 dir,
            float radius,
            float distance,
            LayerMask mask,
            bool useTriggers)
        {
            var hit = Physics2D.CircleCast(origin, radius, dir, distance, mask);

            if (hit.collider == null)
                return distance;

            return Mathf.Max(0f, hit.distance);
        }

        static Vector2 Rotate(Vector2 v, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float s = Mathf.Sin(rad);
            float c = Mathf.Cos(rad);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }
    }
}
