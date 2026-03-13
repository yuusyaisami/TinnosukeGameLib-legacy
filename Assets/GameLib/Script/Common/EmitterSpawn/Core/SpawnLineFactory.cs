#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Spawn
{
    public static class SpawnLineFactory
    {
        public static SpawnLine FromPoints(Vector3[] points)
        {
            if (points == null || points.Length == 0)
                return SpawnLine.Empty;

            var normalized = new float[points.Length];
            float total = 0f;

            normalized[0] = 0f;
            for (int i = 1; i < points.Length; i++)
            {
                total += Vector3.Distance(points[i - 1], points[i]);
                normalized[i] = total;
            }

            if (total > 0f)
            {
                for (int i = 0; i < normalized.Length; i++)
                    normalized[i] /= total;
            }

            return new SpawnLine
            {
                Points = points,
                NormalizedPositions = normalized,
                TotalLength = total
            };
        }

        public static SpawnLine CreateLine(Vector3 start, Vector3 end, int segments)
        {
            segments = Mathf.Max(2, segments);
            var points = new Vector3[segments];
            for (int i = 0; i < segments; i++)
            {
                float t = i / (segments - 1f);
                points[i] = Vector3.Lerp(start, end, t);
            }
            return FromPoints(points);
        }

        public static SpawnLine CreateCircle(int segments, float radius, Vector3 center = default)
        {
            segments = Mathf.Max(3, segments);
            var points = new Vector3[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float a = t * Mathf.PI * 2f;
                points[i] = center + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
            }
            return FromPoints(points);
        }

        public static SpawnLine CreatePolygon(int sides, float radius, Vector3 center = default)
        {
            sides = Mathf.Max(3, sides);
            var points = new Vector3[sides + 1];
            for (int i = 0; i <= sides; i++)
            {
                float t = i / (float)sides;
                float a = t * Mathf.PI * 2f;
                points[i] = center + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
            }
            return FromPoints(points);
        }

        public static SpawnLine CreateCatmullRom(Vector3[] controlPoints, int segmentsPerCurve = 10, bool closed = false)
        {
            if (controlPoints == null || controlPoints.Length < 4)
                return SpawnLine.Empty;

            segmentsPerCurve = Mathf.Max(1, segmentsPerCurve);

            var cps = new List<Vector3>(controlPoints.Length + (closed ? 3 : 0));
            cps.AddRange(controlPoints);
            if (closed)
            {
                cps.Add(controlPoints[0]);
                cps.Add(controlPoints[1]);
                cps.Add(controlPoints[2]);
            }

            int curveCount = closed ? controlPoints.Length : controlPoints.Length - 3;
            var points = new List<Vector3>(curveCount * segmentsPerCurve + 1);

            for (int c = 0; c < curveCount; c++)
            {
                var p0 = cps[c];
                var p1 = cps[c + 1];
                var p2 = cps[c + 2];
                var p3 = cps[c + 3];

                for (int i = 0; i < segmentsPerCurve; i++)
                {
                    float t = i / (float)segmentsPerCurve;
                    points.Add(CatmullRom(t, p0, p1, p2, p3));
                }
            }

            points.Add(closed ? points[0] : cps[cps.Count - 2]);
            return FromPoints(points.ToArray());
        }

        static Vector3 CatmullRom(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            return 0.5f * (
                (2f * p1) +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        public static SpawnLine CreateArchimedeanSpiral(
            float startRadius,
            float radiusPerTurn,
            float turns,
            int segments,
            Vector3 center = default)
        {
            segments = Mathf.Max(2, segments);
            turns = Mathf.Max(0f, turns);

            float totalTheta = turns * Mathf.PI * 2f;
            var points = new Vector3[segments];
            for (int i = 0; i < segments; i++)
            {
                float t = i / (segments - 1f);
                float theta = t * totalTheta;
                float r = startRadius + (radiusPerTurn / (Mathf.PI * 2f)) * theta;
                points[i] = center + new Vector3(r * Mathf.Cos(theta), r * Mathf.Sin(theta), 0f);
            }
            return FromPoints(points);
        }

        public static SpawnLine CreateLogarithmicSpiral(
            float a,
            float b,
            float turns,
            int segments,
            Vector3 center = default)
        {
            segments = Mathf.Max(2, segments);
            turns = Mathf.Max(0f, turns);

            float totalTheta = turns * Mathf.PI * 2f;
            var points = new Vector3[segments];
            for (int i = 0; i < segments; i++)
            {
                float t = i / (segments - 1f);
                float theta = t * totalTheta;
                float r = a * Mathf.Exp(b * theta);
                points[i] = center + new Vector3(r * Mathf.Cos(theta), r * Mathf.Sin(theta), 0f);
            }
            return FromPoints(points);
        }
    }
}
