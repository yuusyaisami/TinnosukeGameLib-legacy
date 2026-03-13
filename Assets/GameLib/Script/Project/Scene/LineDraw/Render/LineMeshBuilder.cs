using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace Game.LineDraw
{
    public sealed class LineMeshBuilder
    {
        const float Epsilon = 0.0001f;

        readonly List<Vector3> _sourcePoints = new();
        readonly List<Vector3> _resampledPoints = new();
        readonly List<float> _resampledDistances = new();
        readonly List<Vector3> _segmentPoints = new();
        readonly List<float> _segmentDistances = new();

        public void Build(
            LineMeshData data,
            IReadOnlyList<Vector3> points,
            bool closed,
            LineStyle style,
            float minSegmentLength,
            int maxVertexCount,
            float pixelScale)
        {
            if (data == null)
                return;

            data.Clear();

            if (points == null || points.Count < 2)
                return;

            PrepareSourcePoints(points, closed, _sourcePoints);
            if (_sourcePoints.Count < 2)
                return;

            float totalLength = ComputeTotalLength(_sourcePoints);
            if (totalLength <= Epsilon)
                return;

            float effectiveMin = Mathf.Max(Epsilon, minSegmentLength);
            int maxPoints = Mathf.Max(2, maxVertexCount / 2);
            if (maxPoints > 1)
            {
                float minByMax = totalLength / (maxPoints - 1);
                if (minByMax > effectiveMin)
                    effectiveMin = minByMax;
            }

            ResamplePoints(_sourcePoints, effectiveMin, _resampledPoints, _resampledDistances);
            if (_resampledPoints.Count < 2)
                return;

            AppendWithPattern(data, _resampledPoints, _resampledDistances, totalLength, style, pixelScale);
        }

        void AppendWithPattern(
            LineMeshData data,
            List<Vector3> points,
            List<float> distances,
            float totalLength,
            LineStyle style,
            float pixelScale)
        {
            var pattern = style.Pattern;
            float unitScale = GetUnitScale(pattern.Unit, pixelScale);

            if (pattern.Type == LinePatternType.Solid || pattern.Type == LinePatternType.Wave)
            {
                AppendPolyline(data, points, distances, totalLength, style, unitScale, pixelScale);
                return;
            }

            float dashLength = pattern.Type == LinePatternType.Dotted ? pattern.DotLength : pattern.DashLength;
            dashLength = Mathf.Max(0f, dashLength) * unitScale;
            float gapLength = Mathf.Max(0f, pattern.GapLength) * unitScale;

            if (dashLength <= Epsilon || gapLength <= Epsilon)
            {
                AppendPolyline(data, points, distances, totalLength, style, unitScale, pixelScale);
                return;
            }

            _segmentPoints.Clear();
            _segmentDistances.Clear();

            // Offsetを適用して開始状態を計算
            float offset = Mathf.Max(0f, pattern.Offset) * unitScale;
            float cycleLength = dashLength + gapLength;
            float normalizedOffset = cycleLength > Epsilon ? (offset % cycleLength) : 0f;
            bool drawing = normalizedOffset < dashLength;
            float remaining = drawing ? (dashLength - normalizedOffset) : (cycleLength - normalizedOffset);

            AddPointIfDrawing(points[0], distances[0], drawing);

            for (int i = 0; i < points.Count - 1; i++)
            {
                var a = points[i];
                var b = points[i + 1];
                float segLen = Vector3.Distance(a, b);
                if (segLen <= Epsilon)
                    continue;

                float segPos = 0f;
                float segStart = distances[i];

                while (segPos + Epsilon < segLen)
                {
                    float step = Mathf.Min(segLen - segPos, remaining);
                    segPos += step;
                    float t = segPos / segLen;
                    var pos = Vector3.LerpUnclamped(a, b, t);
                    float dist = segStart + segPos;

                    AddPointIfDrawing(pos, dist, drawing);

                    remaining -= step;
                    if (remaining > Epsilon)
                        continue;

                    if (drawing)
                    {
                        FlushSegment(data, totalLength, style, unitScale, pixelScale);
                    }

                    drawing = !drawing;
                    remaining = drawing ? dashLength : gapLength;

                    AddPointIfDrawing(pos, dist, drawing);
                }
            }

            if (drawing)
                FlushSegment(data, totalLength, style, unitScale, pixelScale);
        }

        void AddPointIfDrawing(Vector3 pos, float dist, bool drawing)
        {
            if (!drawing)
                return;

            int count = _segmentPoints.Count;
            if (count > 0)
            {
                var last = _segmentPoints[count - 1];
                if ((last - pos).sqrMagnitude <= Epsilon * Epsilon)
                    return;
            }

            _segmentPoints.Add(pos);
            _segmentDistances.Add(dist);
        }

        void FlushSegment(
            LineMeshData data,
            float totalLength,
            LineStyle style,
            float unitScale,
            float pixelScale)
        {
            if (_segmentPoints.Count >= 2)
                AppendPolyline(data, _segmentPoints, _segmentDistances, totalLength, style, unitScale, pixelScale);

            _segmentPoints.Clear();
            _segmentDistances.Clear();
        }

        void AppendPolyline(
            LineMeshData data,
            List<Vector3> points,
            List<float> distances,
            float totalLength,
            LineStyle style,
            float unitScale,
            float pixelScale)
        {
            int count = points.Count;
            if (count < 2)
                return;

            float baseWidth = Mathf.Max(0f, style.BaseWidth);
            if (!style.UseWorldUnits)
                baseWidth *= pixelScale;

            if (baseWidth <= Epsilon)
            {
                Debug.LogWarning($"[LineMeshBuilder] AppendPolyline: baseWidth too small. style.BaseWidth={style.BaseWidth}, pixelScale={pixelScale}, final={baseWidth}, UseWorldUnits={style.UseWorldUnits}");
                return;
            }

            int baseVertex = data.Vertices.Count;
            int vertexCount = count * 2;
            int indexCount = (count - 1) * 6;
            data.EnsureCapacity(baseVertex + vertexCount, data.Indices.Count + indexCount);

            bool useWave = style.Pattern.Type == LinePatternType.Wave &&
                           Mathf.Abs(style.Pattern.WaveAmplitude) > Epsilon &&
                           Mathf.Abs(style.Pattern.WaveLength) > Epsilon;
            float waveAmplitude = style.Pattern.WaveAmplitude * unitScale;
            float waveLength = Mathf.Max(Epsilon, style.Pattern.WaveLength * unitScale);
            // OffsetをPhase（ラジアン）に変換して加算
            float wavePhase = style.Pattern.WavePhase + (style.Pattern.Offset * unitScale / waveLength) * Mathf.PI * 2f;

            for (int i = 0; i < count; i++)
            {
                var p = points[i];
                float dist = distances[i];

                Vector2 dirPrev;
                Vector2 dirNext;

                if (i == 0)
                {
                    dirPrev = (points[i + 1] - p);
                    dirNext = dirPrev;
                }
                else if (i == count - 1)
                {
                    dirPrev = (p - points[i - 1]);
                    dirNext = dirPrev;
                }
                else
                {
                    dirPrev = (p - points[i - 1]);
                    dirNext = (points[i + 1] - p);
                }

                dirPrev = Normalize2D(dirPrev);
                dirNext = Normalize2D(dirNext);

                Vector2 tangent = dirPrev + dirNext;
                if (tangent.sqrMagnitude <= Epsilon * Epsilon)
                    tangent = dirNext.sqrMagnitude > Epsilon ? dirNext : dirPrev;
                tangent = Normalize2D(tangent);

                if (style.Cap == LineCapStyle.Square)
                {
                    float half = baseWidth * 0.5f;
                    if (i == 0)
                    {
                        p -= new Vector3(tangent.x, tangent.y, 0f) * half;
                    }
                    else if (i == count - 1)
                    {
                        p += new Vector3(tangent.x, tangent.y, 0f) * half;
                    }
                }

                if (useWave && waveAmplitude != 0f)
                {
                    float theta = (dist / waveLength) * Mathf.PI * 2f + wavePhase;
                    float offset = Mathf.Sin(theta) * waveAmplitude;
                    Vector2 waveNormal = new Vector2(-tangent.y, tangent.x);
                    p += new Vector3(waveNormal.x, waveNormal.y, 0f) * offset;
                }

                float taperScale = EvaluateTaper(style.Taper, dist, totalLength, pixelScale);
                float width = baseWidth * taperScale;
                if (width <= Epsilon)
                    width = Epsilon;

                Vector2 normalPrev = new Vector2(-dirPrev.y, dirPrev.x);
                Vector2 normalNext = new Vector2(-dirNext.y, dirNext.x);
                Vector2 miter = normalPrev + normalNext;
                if (miter.sqrMagnitude <= Epsilon * Epsilon)
                    miter = normalPrev;
                miter = Normalize2D(miter);

                float miterLength = 1f / Mathf.Max(Epsilon, Vector2.Dot(miter, normalPrev));
                if (style.Join == LineJoinStyle.Bevel)
                    miterLength = Mathf.Min(1f, miterLength);

                float halfWidth = width * 0.5f;
                Vector2 offsetVec = miter * halfWidth * miterLength;

                var left = p + new Vector3(offsetVec.x, offsetVec.y, 0f);
                var right = p - new Vector3(offsetVec.x, offsetVec.y, 0f);

                float u = style.UVScale > Epsilon ? dist / style.UVScale : dist;
                data.Vertices.Add(left);
                data.Vertices.Add(right);
                data.UVs.Add(new Vector2(u, 0f));
                data.UVs.Add(new Vector2(u, 1f));
                data.Colors.Add(style.Color);
                data.Colors.Add(style.Color);
            }

            for (int i = 0; i < count - 1; i++)
            {
                int idx = baseVertex + i * 2;
                data.Indices.Add(idx);
                data.Indices.Add(idx + 2);
                data.Indices.Add(idx + 3);
                data.Indices.Add(idx);
                data.Indices.Add(idx + 3);
                data.Indices.Add(idx + 1);
            }
        }

        static void PrepareSourcePoints(IReadOnlyList<Vector3> points, bool closed, List<Vector3> buffer)
        {
            buffer.Clear();
            if (points == null || points.Count == 0)
                return;

            for (int i = 0; i < points.Count; i++)
                buffer.Add(points[i]);

            if (closed && points.Count > 2)
                buffer.Add(points[0]);
        }

        static float ComputeTotalLength(List<Vector3> points)
        {
            float total = 0f;
            for (int i = 0; i < points.Count - 1; i++)
            {
                float seg = Vector3.Distance(points[i], points[i + 1]);
                if (seg > Epsilon)
                    total += seg;
            }
            return total;
        }

        static void ResamplePoints(
            List<Vector3> source,
            float minSegmentLength,
            List<Vector3> points,
            List<float> distances)
        {
            points.Clear();
            distances.Clear();

            if (source.Count == 0)
                return;

            points.Add(source[0]);
            distances.Add(0f);

            float total = 0f;

            for (int i = 0; i < source.Count - 1; i++)
            {
                var a = source[i];
                var b = source[i + 1];
                float segLen = Vector3.Distance(a, b);
                if (segLen <= Epsilon)
                    continue;

                int steps = Mathf.Max(1, Mathf.CeilToInt(segLen / minSegmentLength));
                float step = segLen / steps;

                for (int s = 1; s <= steps; s++)
                {
                    float t = (float)s / steps;
                    var p = Vector3.LerpUnclamped(a, b, t);
                    total += step;
                    points.Add(p);
                    distances.Add(total);
                }
            }
        }

        static float GetUnitScale(LinePatternUnit unit, float pixelScale)
        {
            return unit == LinePatternUnit.Pixel ? pixelScale : 1f;
        }

        static float EvaluateTaper(LineWidthTaper taper, float dist, float totalLength, float pixelScale)
        {
            float startLen = ResolveTaperLength(taper.Unit, taper.StartLength, totalLength, pixelScale);
            float endLen = ResolveTaperLength(taper.Unit, taper.EndLength, totalLength, pixelScale);

            float startScale = 1f;
            if (startLen > Epsilon)
            {
                float t = Mathf.Clamp01(dist / startLen);
                float eased = DOVirtual.EasedValue(0f, 1f, t, taper.StartEase);
                startScale = Mathf.Lerp(taper.StartScale, 1f, eased);
            }

            float endScale = 1f;
            if (endLen > Epsilon)
            {
                float t = Mathf.Clamp01((totalLength - dist) / endLen);
                float eased = DOVirtual.EasedValue(0f, 1f, t, taper.EndEase);
                endScale = Mathf.Lerp(taper.EndScale, 1f, eased);
            }

            return Mathf.Min(startScale, endScale);
        }

        static float ResolveTaperLength(LineTaperUnit unit, float length, float totalLength, float pixelScale)
        {
            switch (unit)
            {
                case LineTaperUnit.Normalized:
                    return Mathf.Max(0f, totalLength * Mathf.Max(0f, length));
                case LineTaperUnit.Pixel:
                    return Mathf.Max(0f, length * pixelScale);
                default:
                    return Mathf.Max(0f, length);
            }
        }

        static Vector2 Normalize2D(Vector2 v)
        {
            float mag = v.magnitude;
            if (mag <= Epsilon)
                return Vector2.right;
            return v / mag;
        }
    }
}
