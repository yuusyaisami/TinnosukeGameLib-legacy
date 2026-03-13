#nullable enable
using Game;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.LineDraw
{
    [DisallowMultipleComponent]
    public sealed class LineDrawTrailMB : MonoBehaviour, IFeatureInstaller, ILineDrawTrailSettings
    {
        const float PatternEpsilon = 0.0001f;

        [BoxGroup("Trail")]
        [LabelText("Enable")]
        [SerializeField]
        [Tooltip("トレイル描画の有効化/無効化")]
        bool enableTrail = true;

        [BoxGroup("Trail")]
        [LabelText("Space")]
        [SerializeField]
        [Tooltip("トレイルセグメントに使用される座標空間。")]
        LineSpace space = LineSpace.World;

        [BoxGroup("Trail/Style")]
        [InlineProperty]
        [HideLabel]
        [SerializeField]
        [Tooltip("トレイルセグメントに使用される線種。")]
        LineStyle style = LineStyle.Default;

        [SerializeField]
        [HideInInspector]
        LinePattern pattern = LinePattern.Solid;

        [BoxGroup("Trail/Config")]
        [LabelText("Duration (s)")]
        [MinValue(0.01f)]
        [SerializeField]
        [Tooltip("セグメントが削除されるまでの保持時間（秒）。")]
        float durationSeconds = 2f;

        [BoxGroup("Trail/Config")]
        [LabelText("Min Distance")]
        [MinValue(0f)]
        [SerializeField]
        [Tooltip("新しい区間が作成される前に移動する最小距離。")]
        float minDistance = 0.05f;

        [BoxGroup("Trail/Config")]
        [LabelText("Min Time (s)")]
        [MinValue(0f)]
        [SerializeField]
        [Tooltip("セグメント作成間の最小間隔時間。")]
        float minTime = 0.05f;

        [BoxGroup("Trail/Smoothing")]
        [LabelText("Max Segment Length")]
        [MinValue(0f)]
        [SerializeField]
        [Tooltip("1セグメントあたりの最大長さ。0で無効。")]
        float maxSegmentLength = 0.2f;

        [BoxGroup("Trail/Smoothing")]
        [LabelText("Max Jump Distance")]
        [MinValue(0f)]
        [SerializeField]
        [Tooltip("この距離を超える移動はジャンプ扱いで線を引かずにリセットする。0で無効。")]
        float maxJumpDistance = 0f;

        [BoxGroup("Trail/Adaptive")]
        [LabelText("Speed Adaptive Time")]
        [SerializeField]
        [Tooltip("速度に応じてMin Timeを短くする。")]
        bool useSpeedAdaptiveTime = false;

        [BoxGroup("Trail/Adaptive")]
        [ShowIf(nameof(useSpeedAdaptiveTime))]
        [LabelText("Speed To Time Scale")]
        [MinValue(0f)]
        [SerializeField]
        [Tooltip("速度に応じてMin Timeを減衰させる係数。")]
        float speedToMinTimeScale = 0.1f;

        [BoxGroup("Trail/Adaptive")]
        [ShowIf(nameof(useSpeedAdaptiveTime))]
        [LabelText("Min Time Floor")]
        [MinValue(0f)]
        [SerializeField]
        [Tooltip("速度適応時の最小Min Time。")]
        float minTimeFloor = 0f;

        [BoxGroup("Trail/Config")]
        [LabelText("Max Points")]
        [MinValue(2)]
        [SerializeField]
        [Tooltip("保持するトレイル点の最大数。最も古い点から削除される。")]
        int maxSegments = 256;

        [BoxGroup("Trail/Time")]
        [LabelText("Use Unscaled Time")]
        [SerializeField]
        [Tooltip("もし真であれば、トレイルのタイミングはスケーリングされていない時間を使用する。")]
        bool useUnscaledTime = false;

        public bool EnableTrail => enableTrail;
        public LineSpace Space => space;
        public LineStyle Style => style;
        public float DurationSeconds => durationSeconds;
        public float MinDistance => minDistance;
        public float MinTime => minTime;
        public float MaxSegmentLength => maxSegmentLength;
        public float MaxJumpDistance => maxJumpDistance;
        public bool UseSpeedAdaptiveTime => useSpeedAdaptiveTime;
        public float SpeedToMinTimeScale => speedToMinTimeScale;
        public float MinTimeFloor => minTimeFloor;
        public int MaxSegments => maxSegments;
        public bool UseUnscaledTime => useUnscaledTime;

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            ApplyLegacyPatternOverride();

            builder.RegisterInstance<ILineDrawTrailSettings>(this);

            builder.Register<LineDrawTrailService>(Lifetime.Singleton)
                .As<ILineDrawTrailService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .As<ITickable>()
                .AsSelf();
        }

        void Reset()
        {
            enableTrail = true;
            space = LineSpace.World;
            style = LineStyle.Default;
            pattern = LinePattern.Solid;
            durationSeconds = 2f;
            minDistance = 0.05f;
            minTime = 0.05f;
            maxSegmentLength = 0.2f;
            maxJumpDistance = 0f;
            useSpeedAdaptiveTime = false;
            speedToMinTimeScale = 0.1f;
            minTimeFloor = 0f;
            maxSegments = 256;
            useUnscaledTime = false;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            ApplyLegacyPatternOverride();

            if (durationSeconds < 0.01f)
                durationSeconds = 0.01f;
            if (minDistance < 0f)
                minDistance = 0f;
            if (minTime < 0f)
                minTime = 0f;
            if (maxSegmentLength < 0f)
                maxSegmentLength = 0f;
            if (maxJumpDistance < 0f)
                maxJumpDistance = 0f;
            if (speedToMinTimeScale < 0f)
                speedToMinTimeScale = 0f;
            if (minTimeFloor < 0f)
                minTimeFloor = 0f;
            if (maxSegments < 2)
                maxSegments = 2;

            if (style.BaseWidth <= 0f)
                style.BaseWidth = LineStyle.Default.BaseWidth;
            if (style.UVScale <= 0f)
                style.UVScale = LineStyle.Default.UVScale;
            if (style.Color.a <= 0f)
                style.Color = LineStyle.Default.Color;
        }
#endif

        void ApplyLegacyPatternOverride()
        {
            if (IsLegacyPatternOverride(pattern) && IsDefaultPattern(style.Pattern))
                style.Pattern = pattern;
        }

        static bool IsLegacyPatternOverride(LinePattern legacy)
        {
            return legacy.Type != LinePatternType.Solid ||
                   Mathf.Abs(legacy.DashLength) > PatternEpsilon ||
                   Mathf.Abs(legacy.GapLength) > PatternEpsilon ||
                   Mathf.Abs(legacy.DotLength) > PatternEpsilon ||
                   Mathf.Abs(legacy.WaveAmplitude) > PatternEpsilon ||
                   Mathf.Abs(legacy.WaveLength - 1f) > PatternEpsilon ||
                   Mathf.Abs(legacy.WavePhase) > PatternEpsilon;
        }

        static bool IsDefaultPattern(LinePattern current)
        {
            return current.Type == LinePatternType.Solid &&
                   Mathf.Abs(current.DashLength) <= PatternEpsilon &&
                   Mathf.Abs(current.GapLength) <= PatternEpsilon &&
                   Mathf.Abs(current.DotLength) <= PatternEpsilon &&
                   Mathf.Abs(current.WaveAmplitude) <= PatternEpsilon &&
                   Mathf.Abs(current.WaveLength - 1f) <= PatternEpsilon &&
                   Mathf.Abs(current.WavePhase) <= PatternEpsilon;
        }
    }
}
