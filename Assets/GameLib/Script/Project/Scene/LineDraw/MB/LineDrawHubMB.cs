using System.Collections.Generic;
using Game;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.LineDraw
{
    [DisallowMultipleComponent]
    public sealed class LineDrawHubMB : MonoBehaviour, IFeatureInstaller, ILineDrawSettings, ILineDrawMaterialSettings
    {
        const float PatternEpsilon = 0.0001f;

        [BoxGroup("Settings")]
        [SerializeField]
        [Tooltip("座標空間が指定されていない場合に使用されるデフォルトの座標空間。")]
        LineSpace defaultSpace = LineSpace.Local;

        [BoxGroup("Settings")]
        [InlineProperty]
        [HideLabel]
        [SerializeField]
        [Tooltip("スタイルが指定されていない（または無効なスタイルが指定されている）リクエストに適用されるデフォルトの線種。")]
        LineStyle defaultStyle = LineStyle.Default;

        [SerializeField]
        [HideInInspector]
        LinePattern defaultPattern = LinePattern.Solid;

        [BoxGroup("Performance")]
        [MinValue(1)]
        [SerializeField]
        [Tooltip("同時に存在できる最大行数。")]
        int maxLineCount = 128 / 4;

        [BoxGroup("Performance")]
        [MinValue(16)]
        [SerializeField]
        [Tooltip("単一の線が生成できる頂点の最大数。")]
        int maxVertexCount = 4096 / 4;

        [BoxGroup("Performance")]
        [MinValue(0.001f)]
        [SerializeField]
        [Tooltip("過剰な頂点を削減するために使用される最小セグメント長。")]
        float minSegmentLength = 0.1f;

        [BoxGroup("Performance")]
        [MinValue(0.1f)]
        [SerializeField]
        [Tooltip("線の頂点密度スケール。大きいほど頂点数を抑制する。")]
        // 1 より大きくすると、線の頂点密度を抑えられます（例: 2.0 で頂点数を半減、分割長を2倍）。
        float geometryQuality = 1f;

        [BoxGroup("Performance")]
        [MinValue(1f)]
        [SerializeField]
        [Tooltip("Line数が多いほど自動で頂点密度を下げるスケール。1=無効。")]
        float adaptiveQualityScale = 1f;

        [BoxGroup("Time")]
        [SerializeField]
        [Tooltip("真の場合、時間ベースの更新はスケーリングされていない時間を使用します。")]
        bool useUnscaledTime = false;

        [BoxGroup("Auto Draw")]
        [LabelText("Auto Draw On Acquire")]
        [SerializeField]
        [Tooltip("真の場合、スコープが捕捉された際に自動的に経路を描画する。")]
        bool autoDrawOnAcquire = false;

        [BoxGroup("Auto Draw")]
        [ShowIf(nameof(autoDrawOnAcquire))]
        [LabelText("Space")]
        [SerializeField]
        [Tooltip("自動描画ポイントに使用される座標空間。")]
        LineSpace autoDrawSpace = LineSpace.Local;

        [BoxGroup("Auto Draw/Style")]
        [ShowIf(nameof(autoDrawOnAcquire))]
        [InlineProperty]
        [HideLabel]
        [SerializeField]
        [Tooltip("自動描画に使用される線のスタイル。")]
        LineStyle autoDrawStyle = LineStyle.Default;

        [SerializeField]
        [HideInInspector]
        LinePattern autoDrawPattern = LinePattern.Solid;

        [BoxGroup("Auto Draw")]
        [ShowIf(nameof(autoDrawOnAcquire))]
        [LabelText("Closed")]
        [SerializeField]
        [Tooltip("真の場合、自動描画されたパスを閉じる。")]
        bool autoDrawClosed = false;

        [BoxGroup("Auto Draw")]
        [ShowIf(nameof(autoDrawOnAcquire))]
        [ListDrawerSettings(ShowFoldout = true)]
        [LabelText("Points (Local)")]
        [SerializeField]
        [Tooltip("パスを描くために使用する点。順序通りに配置されます。最低2点が必要です。")]
        List<Vector3> autoDrawPoints = new();

        [BoxGroup("Material")]
        [SerializeField]
        [Tooltip("ワールド空間の線に使用される材質。")]
        Material worldMaterial;

        [BoxGroup("Material")]
        [SerializeField]
        [Tooltip("UI（RectTransform）の線に使用される素材。")]
        Material uiMaterial;

        public LineSpace DefaultSpace => defaultSpace;
        public LineStyle DefaultStyle => defaultStyle;
        public int MaxLineCount => maxLineCount;
        public int MaxVertexCount => maxVertexCount;
        public float MinSegmentLength => minSegmentLength;
        public float GeometryQuality => geometryQuality;
        public float AdaptiveQualityScale => adaptiveQualityScale;
        public bool UseUnscaledTime => useUnscaledTime;
        public bool AutoDrawOnAcquire => autoDrawOnAcquire;
        public LineSpace AutoDrawSpace => autoDrawSpace;
        public LineStyle AutoDrawStyle => autoDrawStyle;
        public bool AutoDrawClosed => autoDrawClosed;
        public IReadOnlyList<Vector3> AutoDrawPoints => autoDrawPoints;

        public Material WorldMaterial => worldMaterial;
        public Material UiMaterial => uiMaterial;

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            ApplyLegacyPatternOverrides();

            builder.RegisterInstance<ILineDrawSettings>(this);
            builder.RegisterInstance<ILineDrawMaterialSettings>(this);

            builder.Register<LineDrawHubService>(Lifetime.Singleton)
                .As<ILineDrawService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .As<ITickable>()
                .AsSelf()
                .WithParameter<IScopeNode>(scope)
                .WithParameter(transform);
        }

        void Reset()
        {
            defaultSpace = LineSpace.Local;
            defaultPattern = LinePattern.Solid;
            defaultStyle = LineStyle.Default;
            maxLineCount = 128;
            maxVertexCount = 4096;
            minSegmentLength = 0.1f;
            geometryQuality = 1f;
            adaptiveQualityScale = 1f;
            useUnscaledTime = false;
            autoDrawOnAcquire = false;
            autoDrawSpace = LineSpace.Local;
            autoDrawStyle = LineStyle.Default;
            autoDrawPattern = LinePattern.Solid;
            autoDrawClosed = false;
            autoDrawPoints.Clear();
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            ApplyLegacyPatternOverrides();

            if (maxLineCount < 1)
                maxLineCount = 1;
            if (maxVertexCount < 16)
                maxVertexCount = 16;
            if (minSegmentLength < 0.001f)
                minSegmentLength = 0.001f;
            if (geometryQuality < 0.1f)
                geometryQuality = 0.1f;
            if (adaptiveQualityScale < 1f)
                adaptiveQualityScale = 1f;

            if (defaultStyle.BaseWidth <= 0f)
                defaultStyle.BaseWidth = LineStyle.Default.BaseWidth;
            if (defaultStyle.UVScale <= 0f)
                defaultStyle.UVScale = LineStyle.Default.UVScale;
            if (defaultStyle.Color.a <= 0f)
                defaultStyle.Color = LineStyle.Default.Color;

            if (autoDrawStyle.BaseWidth <= 0f)
                autoDrawStyle.BaseWidth = LineStyle.Default.BaseWidth;
            if (autoDrawStyle.UVScale <= 0f)
                autoDrawStyle.UVScale = LineStyle.Default.UVScale;
            if (autoDrawStyle.Color.a <= 0f)
                autoDrawStyle.Color = LineStyle.Default.Color;
        }
#endif

        void ApplyLegacyPatternOverrides()
        {
            if (ShouldApplyLegacyPattern(defaultPattern, defaultStyle.Pattern))
                defaultStyle.Pattern = defaultPattern;

            if (ShouldApplyLegacyPattern(autoDrawPattern, autoDrawStyle.Pattern))
                autoDrawStyle.Pattern = autoDrawPattern;
        }

        static bool ShouldApplyLegacyPattern(LinePattern legacy, LinePattern current)
        {
            return IsLegacyPatternOverride(legacy) && IsDefaultPattern(current);
        }

        static bool IsLegacyPatternOverride(LinePattern pattern)
        {
            return pattern.Type != LinePatternType.Solid ||
                   Mathf.Abs(pattern.DashLength) > PatternEpsilon ||
                   Mathf.Abs(pattern.GapLength) > PatternEpsilon ||
                   Mathf.Abs(pattern.DotLength) > PatternEpsilon ||
                   Mathf.Abs(pattern.WaveAmplitude) > PatternEpsilon ||
                   Mathf.Abs(pattern.WaveLength - 1f) > PatternEpsilon ||
                   Mathf.Abs(pattern.WavePhase) > PatternEpsilon;
        }

        static bool IsDefaultPattern(LinePattern pattern)
        {
            return pattern.Type == LinePatternType.Solid &&
                   Mathf.Abs(pattern.DashLength) <= PatternEpsilon &&
                   Mathf.Abs(pattern.GapLength) <= PatternEpsilon &&
                   Mathf.Abs(pattern.DotLength) <= PatternEpsilon &&
                   Mathf.Abs(pattern.WaveAmplitude) <= PatternEpsilon &&
                   Mathf.Abs(pattern.WaveLength - 1f) <= PatternEpsilon &&
                   Mathf.Abs(pattern.WavePhase) <= PatternEpsilon;
        }
    }
}
