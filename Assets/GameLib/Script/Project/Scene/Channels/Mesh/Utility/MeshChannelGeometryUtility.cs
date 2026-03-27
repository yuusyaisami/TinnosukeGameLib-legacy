#nullable enable
using System;
using System.Buffers;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Channel
{
    static class MeshChannelGeometryUtility
    {
        const float Epsilon = 0.0001f;

        public static void BuildCatmullRom(IReadOnlyList<Vector2> source, bool closed, int subdivisions, List<Vector2> output)
        {
            output.Clear();
            if (source == null || source.Count == 0)
                return;
            if (source.Count < 2)
            {
                output.AddRange(source);
                return;
            }

            var count = source.Count;
            for (var i = 0; i < count - (closed ? 0 : 1); i++)
            {
                var p0 = source[WrapIndex(i - 1, count, closed)];
                var p1 = source[WrapIndex(i, count, closed)];
                var p2 = source[WrapIndex(i + 1, count, closed)];
                var p3 = source[WrapIndex(i + 2, count, closed)];

                if (!closed && i == 0)
                    p0 = p1;
                if (!closed && i >= count - 2)
                    p3 = p2;

                if (i == 0)
                    output.Add(p1);

                for (var s = 1; s <= subdivisions; s++)
                {
                    var t = s / (float)subdivisions;
                    output.Add(EvaluateCatmullRom(p0, p1, p2, p3, t));
                }
            }
        }

        public static void Resample(
            IReadOnlyList<Vector2> source,
            float minSegmentLength,
            int maxPointCount,
            List<Vector2> points,
            List<float> distances)
        {
            points.Clear();
            distances.Clear();
            if (source == null || source.Count == 0)
                return;

            points.Add(source[0]);
            distances.Add(0f);

            var total = 0f;
            var effectiveMin = Mathf.Max(Epsilon, minSegmentLength);
            for (var i = 0; i < source.Count - 1; i++)
            {
                var a = source[i];
                var b = source[i + 1];
                var length = Vector2.Distance(a, b);
                if (length <= Epsilon)
                    continue;

                var steps = Mathf.Max(1, Mathf.CeilToInt(length / effectiveMin));
                for (var s = 1; s <= steps; s++)
                {
                    var t = s / (float)steps;
                    total += length / steps;
                    points.Add(Vector2.LerpUnclamped(a, b, t));
                    distances.Add(total);
                    if (points.Count >= maxPointCount)
                        return;
                }
            }
        }

        public static void BuildLineVisualPaths(
            IReadOnlyList<Vector2> points,
            IReadOnlyList<float> distances,
            bool closed,
            MeshLineTrackVisualizerPreset preset,
            float timeSeconds,
            List<MeshRuntimePath> outputPaths)
        {
            outputPaths.Clear();
            if (points == null || points.Count < 2)
                return;

            var deformedPoints = ListPool<Vector2>.Get();
            var deformedDistances = ListPool<float>.Get();
            var chunkPoints = ListPool<Vector2>.Get();
            var chunkDistances = ListPool<float>.Get();

            try
            {
                BuildDeformedCenterLine(
                    points,
                    distances,
                    closed,
                    preset,
                    timeSeconds,
                    deformedPoints,
                    deformedDistances,
                    out var totalLength);

                if (deformedPoints.Count < 2)
                    return;

                if (!preset.DashEnabled || !TryBuildDashedLineVisualPaths(deformedPoints, deformedDistances, preset, timeSeconds, totalLength, outputPaths, chunkPoints, chunkDistances))
                    AppendRibbonPath(deformedPoints, deformedDistances, closed, totalLength, preset, outputPaths);
            }
            finally
            {
                ListPool<Vector2>.Release(deformedPoints);
                ListPool<float>.Release(deformedDistances);
                ListPool<Vector2>.Release(chunkPoints);
                ListPool<float>.Release(chunkDistances);
            }
        }

        public static List<Vector2[]> ConvertWorldPathsToLocal(Transform ownerTransform, List<MeshRuntimePath> worldPaths)
        {
            var paths = new List<Vector2[]>(worldPaths.Count);
            for (var i = 0; i < worldPaths.Count; i++)
            {
                var worldPath = worldPaths[i];
                var local = new Vector2[worldPath.Points.Count];
                for (var p = 0; p < worldPath.Points.Count; p++)
                {
                    var world = worldPath.Points[p];
                    var local3 = ownerTransform.InverseTransformPoint(new Vector3(world.x, world.y, 0f));
                    local[p] = new Vector2(local3.x, local3.y);
                }
                paths.Add(local);
            }
            return paths;
        }

        public static List<Vector2[]> SimplifyPaths(List<Vector2[]> sourcePaths, MeshPolygonSyncSettings settings)
        {
            var result = new List<Vector2[]>(sourcePaths.Count);
            for (var i = 0; i < sourcePaths.Count; i++)
                result.Add(SimplifyPath(sourcePaths[i], settings));
            return result;
        }

        public static bool ShouldSyncPaths(
            IReadOnlyList<Vector2[]> lastPaths,
            IReadOnlyList<Vector2[]> nextPaths,
            MeshPolygonSyncSettings settings,
            int frameIndex,
            int lastSyncFrame)
        {
            if (lastSyncFrame != int.MinValue &&
                (long)frameIndex - lastSyncFrame < Mathf.Max(1, settings.UpdateIntervalFrames))
            {
                return false;
            }

            if (lastPaths == null || lastPaths.Count != nextPaths.Count)
                return true;

            for (var i = 0; i < nextPaths.Count; i++)
            {
                var a = lastPaths[i];
                var b = nextPaths[i];
                if (a == null || b == null || a.Length != b.Length)
                    return true;

                if (ComputeMaxMove(a, b) >= settings.MinPointMove)
                    return true;
                if (Mathf.Abs(ComputeSignedArea(a) - ComputeSignedArea(b)) >= settings.MinAreaDelta)
                    return true;
                if (ComputeMaxAngleDelta(a, b) >= settings.MinAngleDelta)
                    return true;
            }

            return false;
        }

        public static void CopyMesh(Mesh source, Mesh destination)
        {
            destination.Clear();
            destination.vertices = source.vertices;
            destination.normals = source.normals;
            destination.tangents = source.tangents;
            destination.colors = source.colors;
            destination.uv = source.uv;
            destination.uv2 = source.uv2;
            destination.triangles = source.triangles;
        }

        public static void BuildFallbackMesh(IReadOnlyList<Vector2[]> paths, Mesh mesh)
        {
            mesh.Clear();
            if (paths == null || paths.Count == 0)
                return;

            var vertices = ListPool<Vector3>.Get();
            var triangles = ListPool<int>.Get();

            try
            {
                for (var i = 0; i < paths.Count; i++)
                {
                    var path = paths[i];
                    if (path == null || path.Length < 3)
                        continue;

                    var baseVertex = vertices.Count;
                    for (var p = 0; p < path.Length; p++)
                        vertices.Add(new Vector3(path[p].x, path[p].y, 0f));

                    Triangulate(path, triangles, baseVertex);
                }

                if (vertices.Count == 0 || triangles.Count == 0)
                    return;

                mesh.SetVertices(vertices);
                mesh.SetTriangles(triangles, 0, true);
            }
            finally
            {
                ListPool<Vector3>.Release(vertices);
                ListPool<int>.Release(triangles);
            }
        }

        static void BuildDeformedCenterLine(
            IReadOnlyList<Vector2> points,
            IReadOnlyList<float> distances,
            bool closed,
            MeshLineTrackVisualizerPreset preset,
            float timeSeconds,
            List<Vector2> outputPoints,
            List<float> outputDistances,
            out float totalLength)
        {
            outputPoints.Clear();
            outputDistances.Clear();

            totalLength = distances.Count > 0 ? distances[distances.Count - 1] : 0f;
            if (points == null || points.Count == 0)
                return;

            var safeTotalLength = Mathf.Max(Epsilon, totalLength);
            for (var i = 0; i < points.Count; i++)
            {
                var prev = i == 0 ? (closed ? points[points.Count - 1] : points[i]) : points[i - 1];
                var next = i == points.Count - 1 ? (closed ? points[0] : points[i]) : points[i + 1];
                var tangent = next - prev;
                if (tangent.sqrMagnitude <= Epsilon)
                    tangent = Vector2.right;
                tangent.Normalize();

                var normal = new Vector2(-tangent.y, tangent.x);
                var point = points[i];
                var distance = i < distances.Count ? distances[i] : 0f;

                if (preset.WaveEnabled && preset.WaveAmplitude > 0f)
                {
                    var sampleLength = preset.WaveSpace == MeshWaveSpace.NormalizedLength
                        ? distance / safeTotalLength
                        : distance;
                    var theta = (sampleLength / Mathf.Max(Epsilon, preset.WaveLength)) * Mathf.PI * 2f +
                                preset.WavePhase +
                                timeSeconds * preset.WaveScrollSpeed;
                    point += normal * (Mathf.Sin(theta) * preset.WaveAmplitude);
                }

                outputPoints.Add(point);
                outputDistances.Add(distance);
            }
        }

        static bool TryBuildDashedLineVisualPaths(
            IReadOnlyList<Vector2> points,
            IReadOnlyList<float> distances,
            MeshLineTrackVisualizerPreset preset,
            float timeSeconds,
            float totalLength,
            List<MeshRuntimePath> outputPaths,
            List<Vector2> chunkPoints,
            List<float> chunkDistances)
        {
            if (!TryBuildResolvedDashPattern(preset, totalLength, timeSeconds, out var patternLengths, out var patternKinds, out var patternTotalLength, out var patternOffset))
                return false;

            chunkPoints.Clear();
            chunkDistances.Clear();

            try
            {
                for (var i = 0; i < points.Count - 1; i++)
                {
                    var startDistance = distances[i];
                    var endDistance = distances[i + 1];
                    var segmentLength = endDistance - startDistance;
                    if (segmentLength <= Epsilon)
                        continue;

                    var startPoint = points[i];
                    var endPoint = points[i + 1];
                    var cursor = startDistance;

                    while (cursor < endDistance - Epsilon)
                    {
                        var shifted = Mathf.Repeat(cursor + patternOffset, patternTotalLength);
                        ResolvePatternSegment(patternLengths, shifted, out var patternIndex, out var localOffset);

                        var remainingInPattern = Mathf.Max(Epsilon, patternLengths[patternIndex] - localOffset);
                        var nextCursor = Mathf.Min(endDistance, cursor + remainingInPattern);
                        var visible = patternKinds[patternIndex] == MeshLineDashPatternKind.Visible;

                        if (visible)
                        {
                            var localStart = Mathf.Clamp01((cursor - startDistance) / segmentLength);
                            var localEnd = Mathf.Clamp01((nextCursor - startDistance) / segmentLength);
                            var visibleStart = Vector2.LerpUnclamped(startPoint, endPoint, localStart);
                            var visibleEnd = Vector2.LerpUnclamped(startPoint, endPoint, localEnd);

                            AppendChunkPoint(chunkPoints, chunkDistances, visibleStart, cursor);
                            AppendChunkPoint(chunkPoints, chunkDistances, visibleEnd, nextCursor);
                        }

                        var crossedGap = !visible && chunkPoints.Count >= 2;
                        if (crossedGap)
                        {
                            AppendRibbonPath(chunkPoints, chunkDistances, false, totalLength, preset, outputPaths);
                            chunkPoints.Clear();
                            chunkDistances.Clear();
                        }

                        cursor = nextCursor;
                    }
                }

                if (chunkPoints.Count >= 2)
                    AppendRibbonPath(chunkPoints, chunkDistances, false, totalLength, preset, outputPaths);
            }
            finally
            {
                ArrayPool<float>.Shared.Return(patternLengths);
                ArrayPool<MeshLineDashPatternKind>.Shared.Return(patternKinds);
            }

            return true;
        }

        static bool TryBuildResolvedDashPattern(
            MeshLineTrackVisualizerPreset preset,
            float totalLength,
            float timeSeconds,
            out float[] patternLengths,
            out MeshLineDashPatternKind[] patternKinds,
            out float patternTotalLength,
            out float patternOffset)
        {
            patternLengths = Array.Empty<float>();
            patternKinds = Array.Empty<MeshLineDashPatternKind>();
            patternTotalLength = 0f;
            patternOffset = 0f;

            if (!preset.DashEnabled || preset.Pattern == null || preset.Pattern.Count == 0)
                return false;

            var count = preset.Pattern.Count;
            patternLengths = ArrayPool<float>.Shared.Rent(count);
            patternKinds = ArrayPool<MeshLineDashPatternKind>.Shared.Rent(count);

            var hasVisible = false;
            patternTotalLength = 0f;
            for (var i = 0; i < count; i++)
            {
                var element = preset.Pattern[i];
                var resolvedLength = ResolveLengthBySpace(element.Length, preset.DashSpace, totalLength);
                if (resolvedLength <= Epsilon)
                {
                    patternLengths[i] = 0f;
                    patternKinds[i] = element.Kind;
                    continue;
                }

                patternLengths[i] = resolvedLength;
                patternKinds[i] = element.Kind;
                patternTotalLength += resolvedLength;
                if (element.Kind == MeshLineDashPatternKind.Visible)
                    hasVisible = true;
            }

            if (!hasVisible || patternTotalLength <= Epsilon)
            {
                ArrayPool<float>.Shared.Return(patternLengths);
                ArrayPool<MeshLineDashPatternKind>.Shared.Return(patternKinds);
                patternLengths = Array.Empty<float>();
                patternKinds = Array.Empty<MeshLineDashPatternKind>();
                patternTotalLength = 0f;
                return false;
            }

            patternOffset = ResolveSignedLengthBySpace(preset.DashScrollOffset + timeSeconds * preset.DashScrollSpeed, preset.DashSpace, totalLength);
            return true;
        }

        static void ResolvePatternSegment(float[] patternLengths, float shiftedDistance, out int patternIndex, out float localOffset)
        {
            var cursor = shiftedDistance;
            for (var i = 0; i < patternLengths.Length; i++)
            {
                var length = patternLengths[i];
                if (length <= Epsilon)
                    continue;

                if (cursor < length)
                {
                    patternIndex = i;
                    localOffset = cursor;
                    return;
                }

                cursor -= length;
            }

            patternIndex = 0;
            localOffset = 0f;
        }

        static float ResolveLengthBySpace(float value, MeshWaveSpace space, float totalLength)
        {
            if (space == MeshWaveSpace.NormalizedLength)
                return Mathf.Abs(value) * Mathf.Max(Epsilon, totalLength);
            return Mathf.Abs(value);
        }

        static float ResolveSignedLengthBySpace(float value, MeshWaveSpace space, float totalLength)
        {
            if (space == MeshWaveSpace.NormalizedLength)
                return value * Mathf.Max(Epsilon, totalLength);
            return value;
        }

        static void AppendChunkPoint(List<Vector2> chunkPoints, List<float> chunkDistances, Vector2 point, float distance)
        {
            if (chunkPoints.Count > 0)
            {
                var lastPoint = chunkPoints[chunkPoints.Count - 1];
                var lastDistance = chunkDistances[chunkDistances.Count - 1];
                if ((lastPoint - point).sqrMagnitude <= Epsilon * Epsilon &&
                    Mathf.Abs(lastDistance - distance) <= Epsilon)
                {
                    return;
                }
            }

            chunkPoints.Add(point);
            chunkDistances.Add(distance);
        }

        static void AppendRibbonPath(
            IReadOnlyList<Vector2> points,
            IReadOnlyList<float> distances,
            bool closed,
            float totalLength,
            MeshLineTrackVisualizerPreset preset,
            List<MeshRuntimePath> outputPaths)
        {
            if (points == null || points.Count < 2)
                return;

            var left = ListPool<Vector2>.Get();
            var right = ListPool<Vector2>.Get();

            try
            {
                BuildRibbonOutline(points, distances, closed, totalLength, preset, left, right);
                if (left.Count < 2 || right.Count < 2)
                    return;

                var path = new MeshRuntimePath();
                for (var i = 0; i < left.Count; i++)
                    path.Points.Add(left[i]);
                for (var i = right.Count - 1; i >= 0; i--)
                    path.Points.Add(right[i]);

                if (path.Points.Count >= 3)
                    outputPaths.Add(path);
            }
            finally
            {
                ListPool<Vector2>.Release(left);
                ListPool<Vector2>.Release(right);
            }
        }

        static void BuildRibbonOutline(
            IReadOnlyList<Vector2> points,
            IReadOnlyList<float> distances,
            bool closed,
            float totalLength,
            MeshLineTrackVisualizerPreset preset,
            List<Vector2> left,
            List<Vector2> right)
        {
            left.Clear();
            right.Clear();
            if (points == null || points.Count < 2)
                return;

            var safeTotalLength = Mathf.Max(Epsilon, totalLength);
            for (var i = 0; i < points.Count; i++)
            {
                var prev = i == 0 ? (closed ? points[points.Count - 1] : points[i]) : points[i - 1];
                var next = i == points.Count - 1 ? (closed ? points[0] : points[i]) : points[i + 1];
                var tangent = next - prev;
                if (tangent.sqrMagnitude <= Epsilon)
                    tangent = Vector2.right;
                tangent.Normalize();

                var normal = new Vector2(-tangent.y, tangent.x);
                var point = points[i];
                var distance = i < distances.Count ? distances[i] : 0f;

                var widthScale = 1f;
                if (preset.HeadTaperNormalized > Epsilon)
                {
                    var t = Mathf.Clamp01(distance / Mathf.Max(Epsilon, safeTotalLength * preset.HeadTaperNormalized));
                    widthScale = Mathf.Min(widthScale, t);
                }

                if (preset.TailTaperNormalized > Epsilon)
                {
                    var tailLength = safeTotalLength * preset.TailTaperNormalized;
                    var tailT = Mathf.Clamp01((safeTotalLength - distance) / Mathf.Max(Epsilon, tailLength));
                    widthScale = Mathf.Min(widthScale, tailT);
                }

                var halfWidth = Mathf.Max(Epsilon, preset.BaseWidth * Mathf.Max(Epsilon, widthScale)) * 0.5f;
                left.Add(point + normal * halfWidth);
                right.Add(point - normal * halfWidth);
            }
        }

        static Vector2[] SimplifyPath(Vector2[] source, MeshPolygonSyncSettings settings)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<Vector2>();

            var tolerance = Mathf.Max(0f, settings.ContourTolerance);
            var points = ListPool<Vector2>.Get();
            try
            {
                points.Add(source[0]);
                for (var i = 1; i < source.Length; i++)
                {
                    if ((source[i] - points[points.Count - 1]).sqrMagnitude < tolerance * tolerance)
                        continue;
                    points.Add(source[i]);
                }

                while (points.Count > settings.MaxPointCount)
                {
                    for (var i = points.Count - 2; i > 0 && points.Count > settings.MaxPointCount; i -= 2)
                        points.RemoveAt(i);
                }

                if (points.Count < 3)
                    return source;

                return points.ToArray();
            }
            finally
            {
                ListPool<Vector2>.Release(points);
            }
        }

        static Vector2 EvaluateCatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            var t2 = t * t;
            var t3 = t2 * t;
            return 0.5f * ((2f * p1) +
                           (-p0 + p2) * t +
                           (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                           (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        static int WrapIndex(int index, int count, bool closed)
        {
            if (closed)
            {
                while (index < 0)
                    index += count;
                return index % count;
            }

            return Mathf.Clamp(index, 0, count - 1);
        }

        static float ComputeMaxMove(Vector2[] a, Vector2[] b)
        {
            var max = 0f;
            for (var i = 0; i < a.Length && i < b.Length; i++)
                max = Mathf.Max(max, Vector2.Distance(a[i], b[i]));
            return max;
        }

        static float ComputeSignedArea(IReadOnlyList<Vector2> polygon)
        {
            if (polygon == null || polygon.Count < 3)
                return 0f;

            var area = 0f;
            for (var i = 0; i < polygon.Count; i++)
            {
                var current = polygon[i];
                var next = polygon[(i + 1) % polygon.Count];
                area += current.x * next.y - next.x * current.y;
            }
            return area * 0.5f;
        }

        static float ComputeMaxAngleDelta(Vector2[] a, Vector2[] b)
        {
            var max = 0f;
            for (var i = 0; i < a.Length && i < b.Length; i++)
            {
                var aPrev = a[(i - 1 + a.Length) % a.Length];
                var aNext = a[(i + 1) % a.Length];
                var bPrev = b[(i - 1 + b.Length) % b.Length];
                var bNext = b[(i + 1) % b.Length];
                var aDir = (aNext - aPrev).normalized;
                var bDir = (bNext - bPrev).normalized;
                if (aDir.sqrMagnitude <= Epsilon || bDir.sqrMagnitude <= Epsilon)
                    continue;
                max = Mathf.Max(max, Vector2.Angle(aDir, bDir));
            }
            return max;
        }

        static void Triangulate(IReadOnlyList<Vector2> polygon, List<int> triangles, int baseVertex)
        {
            var indices = ListPool<int>.Get();
            try
            {
                for (var i = 0; i < polygon.Count; i++)
                    indices.Add(i);

                if (ComputeSignedArea(polygon) < 0f)
                    indices.Reverse();

                var guard = 0;
                while (indices.Count >= 3 && guard < 4096)
                {
                    guard++;
                    var earFound = false;
                    for (var i = 0; i < indices.Count; i++)
                    {
                        var prev = indices[(i - 1 + indices.Count) % indices.Count];
                        var current = indices[i];
                        var next = indices[(i + 1) % indices.Count];

                        if (!IsEar(polygon, indices, prev, current, next))
                            continue;

                        triangles.Add(baseVertex + prev);
                        triangles.Add(baseVertex + current);
                        triangles.Add(baseVertex + next);
                        indices.RemoveAt(i);
                        earFound = true;
                        break;
                    }

                    if (!earFound)
                        break;
                }
            }
            finally
            {
                ListPool<int>.Release(indices);
            }
        }

        static bool IsEar(IReadOnlyList<Vector2> polygon, List<int> indices, int prev, int current, int next)
        {
            var a = polygon[prev];
            var b = polygon[current];
            var c = polygon[next];
            if (Cross(b - a, c - b) <= 0f)
                return false;

            for (var i = 0; i < indices.Count; i++)
            {
                var idx = indices[i];
                if (idx == prev || idx == current || idx == next)
                    continue;
                if (PointInTriangle(polygon[idx], a, b, c))
                    return false;
            }

            return true;
        }

        static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        static bool PointInTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
        {
            var ab = Cross(b - a, point - a);
            var bc = Cross(c - b, point - b);
            var ca = Cross(a - c, point - c);
            var hasNegative = ab < 0f || bc < 0f || ca < 0f;
            var hasPositive = ab > 0f || bc > 0f || ca > 0f;
            return !(hasNegative && hasPositive);
        }
    }
}
