using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace Game.LineDraw
{
    public readonly struct LineHandle
    {
        public readonly int Id;
        public readonly int Generation;

        public LineHandle(int id, int generation)
        {
            Id = id;
            Generation = generation;
        }

        public bool IsValid => Id >= 0;

        public static LineHandle Invalid => new LineHandle(-1, 0);
    }

    public enum LineSpace
    {
        World,
        Local,
        RectTransform
    }

    public enum LinePatternType
    {
        Solid,
        Dashed,
        Dotted,
        Wave
    }

    public enum LinePatternUnit
    {
        World,
        Local,
        Pixel
    }

    public enum LineTaperUnit
    {
        World,
        Normalized,
        Pixel
    }

    public enum LineCapStyle
    {
        Butt,
        Square,
        Round
    }

    public enum LineJoinStyle
    {
        Miter,
        Bevel,
        Round
    }

    [Serializable]
    public struct LineAnchor
    {
        [Tooltip("参照するTransform。指定されている場合はLocalOffsetをTransform座標系で解釈します。")]
        public Transform Transform;
        [Tooltip("Transform基準のローカルオフセット。Transformがnullの場合は位置として扱います。")]
        public Vector3 LocalOffset;

        public LineAnchor(Transform transform, Vector3 localOffset)
        {
            Transform = transform;
            LocalOffset = localOffset;
        }

        public static LineAnchor FromPosition(Vector3 position)
        {
            return new LineAnchor(null, position);
        }
    }

    [Serializable]
    public struct LinePoint
    {
        [Tooltip("ポイントの位置。")]
        public Vector3 Position;
        [Tooltip("ポイントの時刻（任意）。")]
        public float Time;

        public LinePoint(Vector3 position, float time = 0f)
        {
            Position = position;
            Time = time;
        }
    }

    public sealed class LinePath
    {
        public IReadOnlyList<LinePoint> Points { get; private set; }
        public bool Closed { get; private set; }

        public LinePath(IReadOnlyList<LinePoint> points, bool closed = false)
        {
            Points = points ?? Array.Empty<LinePoint>();
            Closed = closed;
        }
    }

    [Serializable]
    public struct LinePattern
    {
        [Tooltip("パターン種別（実線/破線/点線/波線）。")]
        public LinePatternType Type;
        [Tooltip("パターンの長さ基準（World/Local/Pixel）。")]
        public LinePatternUnit Unit;
        [Tooltip("破線の線長。")]
        public float DashLength;
        [Tooltip("破線の隙間長。")]
        public float GapLength;
        [Tooltip("点線の点の長さ。")]
        public float DotLength;
        [Tooltip("波線の振幅。")]
        public float WaveAmplitude;
        [Tooltip("波線の波長。")]
        public float WaveLength;
        [Tooltip("波線の位相。")]
        public float WavePhase;
        [Tooltip("パターンオフセット（破線/波線の開始位置）。")]
        public float Offset;
        [Tooltip("オフセットの毎秒の速度（自動的に動く破線/波線）。")]
        public float OffsetVelocity;

        public static LinePattern Solid => new LinePattern
        {
            Type = LinePatternType.Solid,
            Unit = LinePatternUnit.World,
            DashLength = 0f,
            GapLength = 0f,
            DotLength = 0f,
            WaveAmplitude = 0f,
            WaveLength = 1f,
            WavePhase = 0f,
            Offset = 0f,
            OffsetVelocity = 0f
        };
    }

    [Serializable]
    public struct LineWidthTaper
    {
        [Tooltip("テーパーの長さ基準（World/Normalized/Pixel）。")]
        public LineTaperUnit Unit;
        [Tooltip("開始側のテーパー長。")]
        public float StartLength;
        [Tooltip("終端側のテーパー長。")]
        public float EndLength;
        [Tooltip("開始側の幅倍率。")]
        public float StartScale;
        [Tooltip("終端側の幅倍率。")]
        public float EndScale;
        [Tooltip("開始側のイージング。")]
        public Ease StartEase;
        [Tooltip("終端側のイージング。")]
        public Ease EndEase;

        public static LineWidthTaper None => new LineWidthTaper
        {
            Unit = LineTaperUnit.Normalized,
            StartLength = 0f,
            EndLength = 0f,
            StartScale = 1f,
            EndScale = 1f,
            StartEase = Ease.Linear,
            EndEase = Ease.Linear
        };
    }

    [Serializable]
    public struct LineStyle
    {
        [Tooltip("線の基準幅。")]
        public float BaseWidth;
        [Tooltip("線の色。")]
        public Color Color;
        [Tooltip("テクスチャUVのスケール。")]
        public float UVScale;
        [Tooltip("trueのとき幅をワールド単位として扱います。")]
        public bool UseWorldUnits;
        [Tooltip("線端の形状。")]
        public LineCapStyle Cap;
        [Tooltip("線の接合部の形状。")]
        public LineJoinStyle Join;
        [Tooltip("線のパターン設定。")]
        public LinePattern Pattern;
        [Tooltip("線幅テーパー設定。")]
        public LineWidthTaper Taper;

        public static LineStyle Default => new LineStyle
        {
            BaseWidth = 1f,
            Color = Color.white,
            UVScale = 1f,
            UseWorldUnits = true,
            Cap = LineCapStyle.Butt,
            Join = LineJoinStyle.Miter,
            Pattern = LinePattern.Solid,
            Taper = LineWidthTaper.None
        };

        public LineStyle WithPattern(LinePattern pattern)
        {
            Pattern = pattern;
            return this;
        }
    }

    public readonly struct LineSegmentRequest
    {
        public readonly LineAnchor From;
        public readonly LineAnchor To;
        public readonly LineSpace Space;
        public readonly LineStyle Style;

        public LineSegmentRequest(LineAnchor from, LineAnchor to, LineSpace space, LineStyle style)
        {
            From = from;
            To = to;
            Space = space;
            Style = style;
        }
    }

    public readonly struct LinePathRequest
    {
        public readonly LinePath Path;
        public readonly LineSpace Space;
        public readonly LineStyle Style;

        public LinePathRequest(LinePath path, LineSpace space, LineStyle style)
        {
            Path = path;
            Space = space;
            Style = style;
        }
    }

    public readonly struct LineTrailConfig
    {
        public readonly float DurationSeconds;
        public readonly float MinDistance;
        public readonly float MinTime;
        public readonly int MaxPoints;

        public LineTrailConfig(float durationSeconds, float minDistance, float minTime, int maxPoints)
        {
            DurationSeconds = durationSeconds;
            MinDistance = minDistance;
            MinTime = minTime;
            MaxPoints = maxPoints;
        }
    }
}
