#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Movement
{
    public sealed class PhysicsDetourPlanner2D : IMoveToInputPointPathPlanner2D
    {
        public bool Plan(in MoveToInputPointPlanContext ctx, List<Vector2> outWaypoints)
        {
            if (outWaypoints == null) throw new ArgumentNullException(nameof(outWaypoints));
            outWaypoints.Clear();

            var start = ctx.Start;
            var goal = ctx.Goal;

            var delta = goal - start;
            float dist = delta.magnitude;
            if (dist < 0.0001f)
            {
                outWaypoints.Add(goal);
                return true;
            }

            var dir = delta / dist;

            if (!CircleCastHit(start, dir, dist, ctx.AgentRadius, ctx.ObstacleMask, ctx.UseTriggers, out var hit))
            {
                if (ctx.AllowArcOnClearLine && ctx.ArcMaxOffset > 0f && ctx.ArcOffsetFactor > 0f)
                {
                    var perp = new Vector2(-dir.y, dir.x);
                    float offset = Mathf.Min(ctx.ArcMaxOffset, dist * ctx.ArcOffsetFactor);

                    int sign = ((ctx.ArcSeed * 73856093) ^ 0x9E3779B9) >= 0 ? 1 : -1;
                    var mid = (start + goal) * 0.5f + perp * (offset * sign);

                    outWaypoints.Add(mid);
                }

                outWaypoints.Add(goal);
                return true;
            }

            var hitPoint = hit.point;
            var normal = hit.normal;

            float clearance = ctx.AgentRadius + Mathf.Max(0f, ctx.DetourClearance);

            var basePoint = hitPoint + normal * clearance;
            var perpDir = new Vector2(-dir.y, dir.x);

            var left = basePoint + perpDir * clearance;
            var right = basePoint - perpDir * clearance;

            bool leftOk = IsSegmentClear(start, left, ctx) && IsSegmentClear(left, goal, ctx);
            bool rightOk = IsSegmentClear(start, right, ctx) && IsSegmentClear(right, goal, ctx);

            if (leftOk || rightOk)
            {
                if (leftOk && rightOk)
                {
                    float l = (left - start).magnitude + (goal - left).magnitude;
                    float r = (right - start).magnitude + (goal - right).magnitude;
                    if (l <= r)
                    {
                        outWaypoints.Add(left);
                        outWaypoints.Add(goal);
                        return true;
                    }
                    else
                    {
                        outWaypoints.Add(right);
                        outWaypoints.Add(goal);
                        return true;
                    }
                }

                if (leftOk)
                {
                    outWaypoints.Add(left);
                    outWaypoints.Add(goal);
                    return true;
                }

                outWaypoints.Add(right);
                outWaypoints.Add(goal);
                return true;
            }

            outWaypoints.Add(goal);
            return false;
        }

        static bool IsSegmentClear(Vector2 a, Vector2 b, in MoveToInputPointPlanContext ctx)
        {
            var d = b - a;
            float dist = d.magnitude;
            if (dist < 0.0001f) return true;
            var dir = d / dist;
            return !CircleCastHit(a, dir, dist, ctx.AgentRadius, ctx.ObstacleMask, ctx.UseTriggers, out _);
        }

        static bool CircleCastHit(
            Vector2 origin,
            Vector2 dir,
            float distance,
            float radius,
            LayerMask mask,
            bool useTriggers,
            out RaycastHit2D hit)
        {
            var prev = Physics2D.queriesHitTriggers;
            Physics2D.queriesHitTriggers = useTriggers;
            hit = Physics2D.CircleCast(origin, radius, dir, distance, mask);
            Physics2D.queriesHitTriggers = prev;
            return hit.collider != null;
        }
    }
}
