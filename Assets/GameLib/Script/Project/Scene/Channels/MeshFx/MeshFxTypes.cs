#nullable enable
using System;
using DG.Tweening;
using Game.Commands.VNext;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Channel
{
    public enum MeshFxShapeMode
    {
        Beam = 0,
        WaveLine = 1,
        Ribbon = 2,
        Cone = 3,
        Arc = 4,
    }

    public enum MeshFxPathMode
    {
        ScopeToScope = 0,
        SingleDirection = 1,
        TrajectoryTrack = 2,
    }

    public enum MeshFxPerformanceTier
    {
        High = 0,
        Medium = 1,
        Low = 2,
    }

    public enum MeshFxCollisionPathSource
    {
        BaseCenterline = 0,
        DeformedVisual = 1,
    }

    public enum MeshFxLineJoinStyle
    {
        Miter = 0,
        Bevel = 1,
        Round = 2,
    }

    public enum MeshFxLineCapStyle
    {
        Butt = 0,
        Square = 1,
        Round = 2,
    }

    public enum MeshFxWaveSpace
    {
        World = 0,
        NormalizedLength = 1,
    }

    [Serializable]
    public sealed class MeshFxSingleDirectionSettings
    {
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(Origin)")]
        [Tooltip("Origin actor source. If resolution fails, owner scope transform is used.")]
        public ActorSource Origin;

        [Tooltip("Origin local offset in Origin space.")]
        public DynamicValue<Vector3> OriginLocalOffset = DynamicValueExtensions.FromLiteral(Vector3.zero);

        [Tooltip("Direction vector. Will be normalized at runtime.")]
        public DynamicValue<Vector2> Direction = DynamicValueExtensions.FromLiteral(Vector2.right);

        [Tooltip("If true, Direction is interpreted in world space.")]
        public bool UseWorldDirection = false;

        [Tooltip("Beam length in world units.")]
        public DynamicValue<float> Length = DynamicValueExtensions.FromLiteral(4f);
    }

    [Serializable]
    public sealed class MeshFxScopeToScopeSettings
    {
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(From)")]
        public ActorSource From;

        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(To)")]
        public ActorSource To;

        [Tooltip("From local offset in From transform space.")]
        public DynamicValue<Vector3> FromLocalOffset = DynamicValueExtensions.FromLiteral(Vector3.zero);

        [Tooltip("To local offset in To transform space.")]
        public DynamicValue<Vector3> ToLocalOffset = DynamicValueExtensions.FromLiteral(Vector3.zero);
    }

    [Serializable]
    public sealed class MeshFxTrajectoryTrackSettings
    {
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(Target)")]
        public ActorSource Target;

        [MinValue(0.1f)]
        public float DurationSeconds = 0.75f;

        [MinValue(0f)]
        public float MinDistance = 0.05f;

        [MinValue(0f)]
        public float MinTime = 0.03f;

        [MinValue(2)]
        public int MaxPoints = 64;

        public bool UseUnscaledTime = false;
    }

    [Serializable]
    public sealed class MeshFxBeamSettings
    {
        [TitleGroup("Width")]
        [MinValue(0.01f)]
        public float StartWidth = 0.5f;

        [TitleGroup("Width")]
        [MinValue(0.01f)]
        public float EndWidth = 0.5f;

        [TitleGroup("Width")]
        public AnimationCurve WidthCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

        [TitleGroup("Width")]
        public bool UseWorldUnits = true;

        [TitleGroup("Tip Taper")]
        public bool EnableStartTaper = false;

        [TitleGroup("Tip Taper")]
        public bool EnableEndTaper = true;

        [TitleGroup("Tip Taper")]
        [ShowIf(nameof(EnableStartTaper))]
        [Range(0f, 0.9f)]
        public float StartTaperLength = 0.1f;

        [TitleGroup("Tip Taper")]
        [ShowIf(nameof(EnableEndTaper))]
        [Range(0f, 0.9f)]
        public float EndTaperLength = 0.1f;

        [TitleGroup("Tip Taper")]
        [ShowIf(nameof(EnableStartTaper))]
        public Ease StartTaperEase = Ease.InOutSine;

        [TitleGroup("Tip Taper")]
        [ShowIf(nameof(EnableEndTaper))]
        public Ease EndTaperEase = Ease.InOutSine;

        [TitleGroup("Tip Taper")]
        [ShowIf(nameof(HasAnyTaper))]
        [Range(0f, 0.2f)]
        public float MinTipWidth = 0.01f;

        [TitleGroup("Corner")]
        public bool EnableCornerSmoothing = true;

        [TitleGroup("Corner")]
        [ShowIf(nameof(EnableCornerSmoothing))]
        [Range(5f, 170f)]
        public float CornerAngleThresholdDeg = 45f;

        [TitleGroup("Corner")]
        [ShowIf(nameof(EnableCornerSmoothing))]
        [Range(0f, 10f)]
        public float CornerRadius = 0.2f;

        [TitleGroup("Corner")]
        [ShowIf(nameof(EnableCornerSmoothing))]
        [Range(0, 8)]
        public int CornerSubdivision = 3;

        [TitleGroup("Corner")]
        [ShowIf(nameof(EnableCornerSmoothing))]
        public bool PreservePathLength = true;

        [TitleGroup("Sampling")]
        [Range(0.01f, 1f)]
        public float MinSegmentLength = 0.1f;

        [TitleGroup("Sampling")]
        [Range(4, 256)]
        public int MaxSegmentCount = 64;

        [TitleGroup("Sampling")]
        [Range(0f, 0.5f)]
        public float SimplifyTolerance = 0.02f;

        [TitleGroup("Join Cap")]
        public MeshFxLineJoinStyle JoinStyle = MeshFxLineJoinStyle.Round;

        [TitleGroup("Join Cap")]
        [ShowIf(nameof(IsMiterJoinStyle))]
        [Range(1f, 10f)]
        public float MiterLimit = 2f;

        [TitleGroup("Join Cap")]
        public MeshFxLineCapStyle StartCap = MeshFxLineCapStyle.Round;

        [TitleGroup("Join Cap")]
        public MeshFxLineCapStyle EndCap = MeshFxLineCapStyle.Round;

        bool HasAnyTaper => EnableStartTaper || EnableEndTaper;
        bool IsMiterJoinStyle => JoinStyle == MeshFxLineJoinStyle.Miter;
    }

    [Serializable]
    public sealed class MeshFxWaveLineSettings
    {
        [TitleGroup("Width")]
        [MinValue(0.01f)]
        public float StartWidth = 0.5f;

        [TitleGroup("Width")]
        [MinValue(0.01f)]
        public float EndWidth = 0.5f;

        [TitleGroup("Width")]
        public AnimationCurve WidthCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

        [TitleGroup("Width")]
        public bool UseWorldUnits = true;

        [TitleGroup("Wave")]
        [Range(0f, 5f)]
        public float WaveAmplitude = 0.2f;

        [TitleGroup("Wave")]
        [Range(0.1f, 20f)]
        public float WaveFrequency = 4f;

        [TitleGroup("Wave")]
        [Range(-6.283f, 6.283f)]
        public float WavePhaseOffset = 0f;

        [TitleGroup("Wave")]
        [Range(-20f, 20f)]
        public float WaveScrollSpeed = 0f;

        [TitleGroup("Wave")]
        public MeshFxWaveSpace WaveSpace = MeshFxWaveSpace.NormalizedLength;

        [TitleGroup("Corner")]
        public bool EnableCornerSmoothing = true;

        [TitleGroup("Corner")]
        [ShowIf(nameof(EnableCornerSmoothing))]
        [Range(5f, 170f)]
        public float CornerAngleThresholdDeg = 45f;

        [TitleGroup("Corner")]
        [ShowIf(nameof(EnableCornerSmoothing))]
        [Range(0f, 10f)]
        public float CornerRadius = 0.2f;

        [TitleGroup("Corner")]
        [ShowIf(nameof(EnableCornerSmoothing))]
        [Range(0, 8)]
        public int CornerSubdivision = 3;

        [TitleGroup("Sampling")]
        [Range(0.01f, 1f)]
        public float MinSegmentLength = 0.1f;

        [TitleGroup("Sampling")]
        [Range(4, 256)]
        public int MaxSegmentCount = 96;

        [TitleGroup("Sampling")]
        [Range(0f, 0.5f)]
        public float SimplifyTolerance = 0.02f;

        [TitleGroup("Join Cap")]
        public MeshFxLineJoinStyle JoinStyle = MeshFxLineJoinStyle.Round;

        [TitleGroup("Join Cap")]
        [ShowIf(nameof(IsMiterJoinStyle))]
        [Range(1f, 10f)]
        public float MiterLimit = 2f;

        [TitleGroup("Join Cap")]
        public MeshFxLineCapStyle StartCap = MeshFxLineCapStyle.Round;

        [TitleGroup("Join Cap")]
        public MeshFxLineCapStyle EndCap = MeshFxLineCapStyle.Round;

        bool IsMiterJoinStyle => JoinStyle == MeshFxLineJoinStyle.Miter;
    }

    [Serializable]
    public sealed class MeshFxRibbonSettings
    {
        [MinValue(0.01f)]
        public float StartWidth = 0.4f;

        [MinValue(0.01f)]
        public float EndWidth = 0.4f;

        public AnimationCurve WidthCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

        [Range(0.01f, 1f)]
        public float MinSegmentLength = 0.08f;

        [Range(4, 256)]
        public int MaxSegmentCount = 128;

        [Range(0f, 0.5f)]
        public float SimplifyTolerance = 0.01f;

        public bool UseWorldUnits = true;

        public MeshFxLineJoinStyle JoinStyle = MeshFxLineJoinStyle.Round;

        [ShowIf(nameof(IsMiterJoinStyle))]
        [Range(1f, 10f)]
        public float MiterLimit = 2f;

        bool IsMiterJoinStyle => JoinStyle == MeshFxLineJoinStyle.Miter;
    }

    [Serializable]
    public sealed class MeshFxConeSettings
    {
        [MinValue(0.01f)]
        [ShowIf(nameof(ShowLengthField))]
        public float Length = 3f;

        [Range(1f, 179f)]
        public float AngleDeg = 30f;

        [Range(3, 64)]
        public int Segments = 16;

        public bool UsePathLength = true;

        [MinValue(0f)]
        public float StartWidth = 0.08f;

        bool ShowLengthField => !UsePathLength;
    }

    [Serializable]
    public sealed class MeshFxArcSettings
    {
        [MinValue(0.01f)]
        public float Radius = 2f;

        [MinValue(0.01f)]
        public float Thickness = 0.2f;

        [Range(-360f, 360f)]
        public float SweepAngleDeg = 120f;

        [Range(3, 128)]
        public int Segments = 24;

        [Range(-180f, 180f)]
        public float StartAngleOffsetDeg = 0f;
    }

    [Serializable]
    public sealed class MeshFxCollisionApproximationSettings
    {
        [Range(4, 256)]
        public int MaxColliderCount = 64;

        [Range(0.01f, 2f)]
        public float MinCollisionSampleDistance = 0.1f;

        [Range(0.005f, 1f)]
        public float MinCollisionRadius = 0.02f;

        [Range(0f, 0.1f)]
        public float PositionUpdateThreshold = 0.01f;

        [Range(0f, 0.1f)]
        public float WidthUpdateThreshold = 0.01f;

        [Range(0f, 5f)]
        public float DirectionUpdateThresholdDeg = 1f;
    }

    [Serializable]
    public sealed class MeshFxRuntimeQualityOverride
    {
        [ReadOnly] public int CornerSubdivisionPenalty;
        [ReadOnly] public int CollisionIntervalBonus;
        [ReadOnly] public float SimplifyToleranceBonus;

        public void Clear()
        {
            CornerSubdivisionPenalty = 0;
            CollisionIntervalBonus = 0;
            SimplifyToleranceBonus = 0f;
        }
    }

    static class MeshFxMath
    {
        public static float Ease01(float t, Ease ease)
        {
            t = Mathf.Clamp01(t);
            return DOVirtual.EasedValue(0f, 1f, t, ease);
        }

        public static Vector2 SafeNormal2D(Vector2 v, Vector2 fallback)
        {
            if (v.sqrMagnitude <= 1e-8f)
                return fallback.sqrMagnitude > 1e-8f ? fallback.normalized : Vector2.right;
            return v.normalized;
        }

        public static float StableHash(float value)
        {
            return Mathf.Round(value * 1000f) * 0.001f;
        }
    }
}
