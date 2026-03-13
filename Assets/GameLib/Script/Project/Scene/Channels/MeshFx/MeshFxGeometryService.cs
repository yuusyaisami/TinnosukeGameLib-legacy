#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace Game.Channel
{
    sealed class MeshFxGeometryFrame
    {
        public readonly List<Vector3> Vertices = new(256);
        public readonly List<Vector2> UV = new(256);
        public readonly List<int> Triangles = new(384);

        public readonly List<Vector3> BaseCenterline = new(128);
        public readonly List<Vector3> VisualCenterline = new(128);
        public readonly List<float> WidthSamples = new(128);

        public bool HasMesh => Vertices.Count > 0 && Triangles.Count > 0;

        public void ClearAll()
        {
            Vertices.Clear();
            UV.Clear();
            Triangles.Clear();
            BaseCenterline.Clear();
            VisualCenterline.Clear();
            WidthSamples.Clear();
        }

        public void ClearMesh()
        {
            Vertices.Clear();
            UV.Clear();
            Triangles.Clear();
        }
    }

    interface IMeshFxGeometryService
    {
        bool Evaluate(
            MeshFxChannelDef def,
            IReadOnlyList<Vector3> pathPoints,
            float timeSeconds,
            MeshFxRuntimeQualityOverride runtimeOverride,
            bool buildMesh,
            MeshFxGeometryFrame frame);
    }

    sealed class MeshFxGeometryService : IMeshFxGeometryService
    {
        const float Epsilon = 0.00001f;

        readonly List<Vector3> _simplified = new(128);
        readonly List<Vector3> _smoothed = new(128);
        readonly List<Vector3> _resampled = new(128);
        readonly List<float> _resampledDistance = new(128);
        readonly List<Vector3> _deformed = new(128);
        readonly List<float> _widths = new(128);

        public bool Evaluate(
            MeshFxChannelDef def,
            IReadOnlyList<Vector3> pathPoints,
            float timeSeconds,
            MeshFxRuntimeQualityOverride runtimeOverride,
            bool buildMesh,
            MeshFxGeometryFrame frame)
        {
            frame.ClearAll();

            if (pathPoints == null || pathPoints.Count < 2)
                return false;

            switch (def.Mode)
            {
                case MeshFxShapeMode.Beam:
                    return EvaluateBeam(def.BeamSettings, pathPoints, runtimeOverride, buildMesh, frame);
                case MeshFxShapeMode.WaveLine:
                    return EvaluateWaveLine(def.WaveLineSettings, pathPoints, timeSeconds, runtimeOverride, buildMesh, frame);
                case MeshFxShapeMode.Ribbon:
                    return EvaluateRibbon(def.RibbonSettings, pathPoints, runtimeOverride, buildMesh, frame);
                case MeshFxShapeMode.Cone:
                    return EvaluateCone(def.ConeSettings, pathPoints, buildMesh, frame);
                case MeshFxShapeMode.Arc:
                    return EvaluateArc(def.ArcSettings, pathPoints, buildMesh, frame);
                default:
                    return false;
            }
        }

        bool EvaluateBeam(
            MeshFxBeamSettings settings,
            IReadOnlyList<Vector3> pathPoints,
            MeshFxRuntimeQualityOverride runtimeOverride,
            bool buildMesh,
            MeshFxGeometryFrame frame)
        {
            var cornerSubdivision = Mathf.Max(0, settings.CornerSubdivision - runtimeOverride.CornerSubdivisionPenalty);
            var simplifyTolerance = Mathf.Clamp(settings.SimplifyTolerance + runtimeOverride.SimplifyToleranceBonus, 0f, 0.5f);

            if (!PrepareLinePoints(
                pathPoints,
                settings.EnableCornerSmoothing,
                settings.CornerAngleThresholdDeg,
                settings.CornerRadius,
                cornerSubdivision,
                settings.MinSegmentLength,
                settings.MaxSegmentCount,
                simplifyTolerance,
                frame.BaseCenterline))
            {
                return false;
            }

            BuildBeamWidths(settings, frame.BaseCenterline, _widths);
            frame.WidthSamples.AddRange(_widths);
            frame.VisualCenterline.AddRange(frame.BaseCenterline);

            if (buildMesh)
            {
                BuildLineStripMesh(
                    frame.VisualCenterline,
                    frame.WidthSamples,
                    settings.StartCap,
                    settings.EndCap,
                    frame);
            }

            return true;
        }

        bool EvaluateWaveLine(
            MeshFxWaveLineSettings settings,
            IReadOnlyList<Vector3> pathPoints,
            float timeSeconds,
            MeshFxRuntimeQualityOverride runtimeOverride,
            bool buildMesh,
            MeshFxGeometryFrame frame)
        {
            var cornerSubdivision = Mathf.Max(0, settings.CornerSubdivision - runtimeOverride.CornerSubdivisionPenalty);
            var simplifyTolerance = Mathf.Clamp(settings.SimplifyTolerance + runtimeOverride.SimplifyToleranceBonus, 0f, 0.5f);

            if (!PrepareLinePoints(
                pathPoints,
                settings.EnableCornerSmoothing,
                settings.CornerAngleThresholdDeg,
                settings.CornerRadius,
                cornerSubdivision,
                settings.MinSegmentLength,
                settings.MaxSegmentCount,
                simplifyTolerance,
                frame.BaseCenterline))
            {
                return false;
            }

            BuildSimpleWidths(settings.StartWidth, settings.EndWidth, settings.WidthCurve, frame.BaseCenterline, _widths);
            ApplyWaveDeformation(frame.BaseCenterline, _resampledDistance, settings, timeSeconds, _deformed);

            frame.WidthSamples.AddRange(_widths);
            frame.VisualCenterline.AddRange(_deformed);

            if (buildMesh)
            {
                BuildLineStripMesh(
                    frame.VisualCenterline,
                    frame.WidthSamples,
                    settings.StartCap,
                    settings.EndCap,
                    frame);
            }

            return true;
        }

        bool EvaluateRibbon(
            MeshFxRibbonSettings settings,
            IReadOnlyList<Vector3> pathPoints,
            MeshFxRuntimeQualityOverride runtimeOverride,
            bool buildMesh,
            MeshFxGeometryFrame frame)
        {
            var simplifyTolerance = Mathf.Clamp(settings.SimplifyTolerance + runtimeOverride.SimplifyToleranceBonus, 0f, 0.5f);

            if (!PrepareLinePoints(
                pathPoints,
                enableCornerSmoothing: false,
                cornerAngleThresholdDeg: 0f,
                cornerRadius: 0f,
                cornerSubdivision: 0,
                settings.MinSegmentLength,
                settings.MaxSegmentCount,
                simplifyTolerance,
                frame.BaseCenterline))
            {
                return false;
            }

            BuildSimpleWidths(settings.StartWidth, settings.EndWidth, settings.WidthCurve, frame.BaseCenterline, _widths);

            frame.WidthSamples.AddRange(_widths);
            frame.VisualCenterline.AddRange(frame.BaseCenterline);

            if (buildMesh)
            {
                BuildLineStripMesh(
                    frame.VisualCenterline,
                    frame.WidthSamples,
                    MeshFxLineCapStyle.Butt,
                    MeshFxLineCapStyle.Butt,
                    frame);
            }

            return true;
        }

        bool EvaluateCone(
            MeshFxConeSettings settings,
            IReadOnlyList<Vector3> pathPoints,
            bool buildMesh,
            MeshFxGeometryFrame frame)
        {
            var origin = pathPoints[0];
            var pathEnd = pathPoints[pathPoints.Count - 1];
            var direction = pathEnd - origin;
            direction.z = 0f;
            if (direction.sqrMagnitude <= Epsilon)
                direction = Vector3.right;
            else
                direction.Normalize();

            var pathLength = ComputeLength(pathPoints);
            var length = settings.UsePathLength ? pathLength : Mathf.Max(0.01f, settings.Length);
            length = Mathf.Max(0.01f, length);

            var halfAngleRad = Mathf.Clamp(settings.AngleDeg, 1f, 179f) * Mathf.Deg2Rad * 0.5f;
            var endWidth = Mathf.Max(settings.StartWidth, 2f * Mathf.Tan(halfAngleRad) * length);

            frame.BaseCenterline.Add(origin);
            frame.BaseCenterline.Add(origin + direction * length);
            frame.VisualCenterline.AddRange(frame.BaseCenterline);
            frame.WidthSamples.Add(Mathf.Max(0.01f, settings.StartWidth));
            frame.WidthSamples.Add(Mathf.Max(0.01f, endWidth));

            if (!buildMesh)
                return true;

            var segments = Mathf.Clamp(settings.Segments, 3, 64);
            var baseAngle = Mathf.Atan2(direction.y, direction.x);

            frame.Vertices.Add(origin);
            frame.UV.Add(new Vector2(0.5f, 0f));

            for (int i = 0; i <= segments; i++)
            {
                var t = i / (float)segments;
                var angle = baseAngle - halfAngleRad + (halfAngleRad * 2f) * t;
                var dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                frame.Vertices.Add(origin + dir * length);
                frame.UV.Add(new Vector2(t, 1f));
            }

            for (int i = 0; i < segments; i++)
            {
                frame.Triangles.Add(0);
                frame.Triangles.Add(i + 1);
                frame.Triangles.Add(i + 2);
            }

            return true;
        }

        bool EvaluateArc(
            MeshFxArcSettings settings,
            IReadOnlyList<Vector3> pathPoints,
            bool buildMesh,
            MeshFxGeometryFrame frame)
        {
            var center = pathPoints[0];
            var pathEnd = pathPoints[pathPoints.Count - 1];
            var direction = pathEnd - center;
            direction.z = 0f;
            if (direction.sqrMagnitude <= Epsilon)
                direction = Vector3.right;
            else
                direction.Normalize();

            var radius = Mathf.Max(0.01f, settings.Radius);
            var thickness = Mathf.Max(0.01f, settings.Thickness);
            var segments = Mathf.Clamp(settings.Segments, 3, 128);

            var sweepRad = settings.SweepAngleDeg * Mathf.Deg2Rad;
            var baseAngle = Mathf.Atan2(direction.y, direction.x) + settings.StartAngleOffsetDeg * Mathf.Deg2Rad;

            for (int i = 0; i <= segments; i++)
            {
                var t = i / (float)segments;
                var angle = baseAngle + sweepRad * t;
                var radial = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                frame.BaseCenterline.Add(center + radial * radius);
                frame.WidthSamples.Add(thickness);
            }

            frame.VisualCenterline.AddRange(frame.BaseCenterline);

            if (!buildMesh)
                return true;

            var halfThickness = thickness * 0.5f;
            var innerRadius = Mathf.Max(0.001f, radius - halfThickness);
            var outerRadius = Mathf.Max(innerRadius + 0.001f, radius + halfThickness);

            for (int i = 0; i <= segments; i++)
            {
                var t = i / (float)segments;
                var angle = baseAngle + sweepRad * t;
                var radial = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);

                frame.Vertices.Add(center + radial * innerRadius);
                frame.UV.Add(new Vector2(0f, t));

                frame.Vertices.Add(center + radial * outerRadius);
                frame.UV.Add(new Vector2(1f, t));
            }

            for (int i = 0; i < segments; i++)
            {
                var vi = i * 2;
                frame.Triangles.Add(vi);
                frame.Triangles.Add(vi + 1);
                frame.Triangles.Add(vi + 2);

                frame.Triangles.Add(vi + 1);
                frame.Triangles.Add(vi + 3);
                frame.Triangles.Add(vi + 2);
            }

            return true;
        }

        bool PrepareLinePoints(
            IReadOnlyList<Vector3> source,
            bool enableCornerSmoothing,
            float cornerAngleThresholdDeg,
            float cornerRadius,
            int cornerSubdivision,
            float minSegmentLength,
            int maxSegmentCount,
            float simplifyTolerance,
            List<Vector3> output)
        {
            output.Clear();
            _resampledDistance.Clear();

            SimplifyPolyline(source, simplifyTolerance, _simplified);
            if (_simplified.Count < 2)
                return false;

            if (enableCornerSmoothing && cornerSubdivision > 0)
            {
                SmoothCorners(
                    _simplified,
                    cornerAngleThresholdDeg,
                    cornerRadius,
                    cornerSubdivision,
                    _smoothed);
            }
            else
            {
                _smoothed.Clear();
                _smoothed.AddRange(_simplified);
            }

            if (_smoothed.Count < 2)
                return false;

            ResamplePolyline(
                _smoothed,
                Mathf.Max(0.01f, minSegmentLength),
                Mathf.Clamp(maxSegmentCount, 4, 256),
                _resampled,
                _resampledDistance);

            if (_resampled.Count < 2)
                return false;

            output.AddRange(_resampled);
            return true;
        }

        static void SimplifyPolyline(IReadOnlyList<Vector3> source, float tolerance, List<Vector3> output)
        {
            output.Clear();
            if (source == null || source.Count == 0)
                return;

            if (source.Count <= 2 || tolerance <= Epsilon)
            {
                output.AddRange(source);
                return;
            }

            output.Add(source[0]);
            for (int i = 1; i < source.Count - 1; i++)
            {
                var prev = output[output.Count - 1];
                var curr = source[i];
                var next = source[i + 1];
                var dist = DistancePointToSegment(curr, prev, next);
                if (dist > tolerance)
                    output.Add(curr);
            }

            output.Add(source[source.Count - 1]);
        }

        static float DistancePointToSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            var ab = b - a;
            var abLenSq = ab.sqrMagnitude;
            if (abLenSq <= Epsilon)
                return Vector3.Distance(point, a);

            var t = Vector3.Dot(point - a, ab) / abLenSq;
            t = Mathf.Clamp01(t);
            var nearest = a + ab * t;
            return Vector3.Distance(point, nearest);
        }

        static void SmoothCorners(
            IReadOnlyList<Vector3> source,
            float angleThresholdDeg,
            float radius,
            int subdivision,
            List<Vector3> output)
        {
            output.Clear();

            if (source == null || source.Count < 3)
            {
                if (source != null)
                    output.AddRange(source);
                return;
            }

            output.Add(source[0]);

            var clampedSub = Mathf.Clamp(subdivision, 0, 8);
            var clampedRadius = Mathf.Max(0f, radius);

            for (int i = 1; i < source.Count - 1; i++)
            {
                var prev = source[i - 1];
                var curr = source[i];
                var next = source[i + 1];

                var inDir = curr - prev;
                var outDir = next - curr;
                inDir.z = 0f;
                outDir.z = 0f;
                if (inDir.sqrMagnitude <= Epsilon || outDir.sqrMagnitude <= Epsilon)
                {
                    output.Add(curr);
                    continue;
                }

                inDir.Normalize();
                outDir.Normalize();

                var cornerAngle = Vector3.Angle(-inDir, outDir);
                if (cornerAngle > angleThresholdDeg || cornerAngle >= 179.5f || clampedRadius <= Epsilon || clampedSub <= 0)
                {
                    output.Add(curr);
                    continue;
                }

                var lenIn = Mathf.Min(clampedRadius, Vector3.Distance(prev, curr) * 0.5f);
                var lenOut = Mathf.Min(clampedRadius, Vector3.Distance(curr, next) * 0.5f);

                var p0 = curr - inDir * lenIn;
                var p1 = curr + outDir * lenOut;

                output.Add(p0);
                for (int s = 1; s < clampedSub; s++)
                {
                    var t = s / (float)clampedSub;
                    var u = 1f - t;
                    var q = u * u * p0 + 2f * u * t * curr + t * t * p1;
                    output.Add(q);
                }
                output.Add(p1);
            }

            output.Add(source[source.Count - 1]);
        }

        static void ResamplePolyline(
            IReadOnlyList<Vector3> source,
            float minSegmentLength,
            int maxSegmentCount,
            List<Vector3> outPoints,
            List<float> outDistances)
        {
            outPoints.Clear();
            outDistances.Clear();

            if (source == null || source.Count < 2)
                return;

            var totalLength = ComputeLength(source);
            if (totalLength <= Epsilon)
                return;

            var maxPoints = Mathf.Max(2, maxSegmentCount);
            var stepByCount = totalLength / (maxPoints - 1);
            var step = Mathf.Max(minSegmentLength, stepByCount);

            outPoints.Add(source[0]);
            outDistances.Add(0f);

            var accumulated = 0f;
            var nextDistance = step;

            for (int i = 0; i < source.Count - 1; i++)
            {
                var a = source[i];
                var b = source[i + 1];
                var segLen = Vector3.Distance(a, b);
                if (segLen <= Epsilon)
                    continue;

                while (nextDistance < accumulated + segLen && outPoints.Count < maxPoints - 1)
                {
                    var t = (nextDistance - accumulated) / segLen;
                    outPoints.Add(Vector3.LerpUnclamped(a, b, t));
                    outDistances.Add(nextDistance);
                    nextDistance += step;
                }

                accumulated += segLen;
            }

            outPoints.Add(source[source.Count - 1]);
            outDistances.Add(totalLength);
        }

        static void BuildSimpleWidths(float startWidth, float endWidth, AnimationCurve widthCurve, IReadOnlyList<Vector3> points, List<float> outWidths)
        {
            outWidths.Clear();
            if (points == null || points.Count == 0)
                return;

            var count = points.Count;
            for (int i = 0; i < count; i++)
            {
                var t = count <= 1 ? 0f : i / (float)(count - 1);
                var baseWidth = Mathf.Lerp(startWidth, endWidth, t);
                if (widthCurve != null)
                    baseWidth *= Mathf.Max(0f, widthCurve.Evaluate(t));
                outWidths.Add(Mathf.Max(0.001f, baseWidth));
            }
        }

        static void BuildBeamWidths(MeshFxBeamSettings settings, IReadOnlyList<Vector3> points, List<float> outWidths)
        {
            outWidths.Clear();
            if (points == null || points.Count == 0)
                return;

            var count = points.Count;
            var minTip = Mathf.Max(0f, settings.MinTipWidth);

            for (int i = 0; i < count; i++)
            {
                var t = count <= 1 ? 0f : i / (float)(count - 1);
                var width = Mathf.Lerp(settings.StartWidth, settings.EndWidth, t);
                if (settings.WidthCurve != null)
                    width *= Mathf.Max(0f, settings.WidthCurve.Evaluate(t));

                if (settings.EnableStartTaper && settings.StartTaperLength > Epsilon)
                {
                    var k = Mathf.Clamp01(t / settings.StartTaperLength);
                    var eased = MeshFxMath.Ease01(k, settings.StartTaperEase);
                    width = Mathf.Lerp(minTip, width, eased);
                }

                if (settings.EnableEndTaper && settings.EndTaperLength > Epsilon)
                {
                    var k = Mathf.Clamp01((1f - t) / settings.EndTaperLength);
                    var eased = MeshFxMath.Ease01(k, settings.EndTaperEase);
                    width = Mathf.Lerp(minTip, width, eased);
                }

                outWidths.Add(Mathf.Max(minTip, width));
            }
        }

        static void ApplyWaveDeformation(
            IReadOnlyList<Vector3> basePoints,
            IReadOnlyList<float> distances,
            MeshFxWaveLineSettings settings,
            float timeSeconds,
            List<Vector3> output)
        {
            output.Clear();
            if (basePoints == null || basePoints.Count == 0)
                return;

            var totalLength = distances.Count > 0 ? distances[distances.Count - 1] : 0f;

            for (int i = 0; i < basePoints.Count; i++)
            {
                var p = basePoints[i];
                var tangent = ResolveTangent(basePoints, i);
                var normal = new Vector3(-tangent.y, tangent.x, 0f);

                var dist = i < distances.Count ? distances[i] : 0f;
                float phase;
                if (settings.WaveSpace == MeshFxWaveSpace.NormalizedLength)
                {
                    var t = totalLength > Epsilon ? dist / totalLength : 0f;
                    phase = t * settings.WaveFrequency * Mathf.PI * 2f;
                }
                else
                {
                    phase = dist * settings.WaveFrequency;
                }

                phase += settings.WavePhaseOffset + settings.WaveScrollSpeed * timeSeconds;
                var offset = Mathf.Sin(phase) * settings.WaveAmplitude;
                output.Add(p + normal * offset);
            }
        }

        static Vector3 ResolveTangent(IReadOnlyList<Vector3> points, int index)
        {
            if (points.Count == 1)
                return Vector3.right;

            Vector3 tangent;
            if (index == 0)
            {
                tangent = points[1] - points[0];
            }
            else if (index == points.Count - 1)
            {
                tangent = points[index] - points[index - 1];
            }
            else
            {
                tangent = points[index + 1] - points[index - 1];
            }

            tangent.z = 0f;
            if (tangent.sqrMagnitude <= Epsilon)
                tangent = Vector3.right;
            else
                tangent.Normalize();

            return tangent;
        }

        static void BuildLineStripMesh(
            IReadOnlyList<Vector3> centerline,
            IReadOnlyList<float> widths,
            MeshFxLineCapStyle startCap,
            MeshFxLineCapStyle endCap,
            MeshFxGeometryFrame frame)
        {
            frame.ClearMesh();

            if (centerline == null || widths == null)
                return;
            if (centerline.Count < 2 || widths.Count != centerline.Count)
                return;

            var totalLength = ComputeLength(centerline);
            var accumulated = 0f;

            for (int i = 0; i < centerline.Count; i++)
            {
                var p = centerline[i];
                var tangent = ResolveTangent(centerline, i);
                var half = Mathf.Max(0.001f, widths[i] * 0.5f);

                if (i == 0 && startCap == MeshFxLineCapStyle.Square)
                {
                    p -= tangent * half;
                }
                else if (i == centerline.Count - 1 && endCap == MeshFxLineCapStyle.Square)
                {
                    p += tangent * half;
                }

                if (i > 0)
                    accumulated += Vector3.Distance(centerline[i - 1], centerline[i]);

                var normal = new Vector3(-tangent.y, tangent.x, 0f);
                var left = p + normal * half;
                var right = p - normal * half;

                var v = totalLength > Epsilon ? accumulated / totalLength : 0f;

                frame.Vertices.Add(left);
                frame.Vertices.Add(right);

                frame.UV.Add(new Vector2(0f, v));
                frame.UV.Add(new Vector2(1f, v));
            }

            for (int i = 0; i < centerline.Count - 1; i++)
            {
                var vi = i * 2;
                frame.Triangles.Add(vi);
                frame.Triangles.Add(vi + 2);
                frame.Triangles.Add(vi + 1);

                frame.Triangles.Add(vi + 1);
                frame.Triangles.Add(vi + 2);
                frame.Triangles.Add(vi + 3);
            }
        }

        static float ComputeLength(IReadOnlyList<Vector3> points)
        {
            if (points == null || points.Count < 2)
                return 0f;

            var len = 0f;
            for (int i = 1; i < points.Count; i++)
            {
                len += Vector3.Distance(points[i - 1], points[i]);
            }

            return len;
        }
    }
}
