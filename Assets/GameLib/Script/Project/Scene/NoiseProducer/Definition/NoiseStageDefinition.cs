#nullable enable
using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.NoiseProducer
{
    [Serializable]
    public sealed class NoiseStageDefinition
    {
        // ── Common ──────────────────────────────────────────────

        [FoldoutGroup("Stage")]
        [SerializeField] string _stageId = string.Empty;

        [FoldoutGroup("Stage")]
        [SerializeField] NoiseStageKind _stageKind = NoiseStageKind.Generator;

        [FoldoutGroup("Stage")]
        [SerializeField] bool _enabled = true;

        [FoldoutGroup("Stage")]
        [Tooltip("後続 stage がこの slot 名で参照できる")]
        [SerializeField] string _outputSlot = string.Empty;

        [FoldoutGroup("Stage/Time")]
        [SerializeField] bool _useGlobalTime = true;

        [FoldoutGroup("Stage/Time")]
        [SerializeField] float _speed = 1f;

        [FoldoutGroup("Stage/Time")]
        [SerializeField] float _phase;

        // ── Generator ───────────────────────────────────────────

        [FoldoutGroup("Generator")]
        [ShowIf(nameof(_stageKind), NoiseStageKind.Generator)]
        [SerializeField] NoiseGeneratorOp _generatorOp = NoiseGeneratorOp.ValueNoise;

        [FoldoutGroup("Generator")]
        [ShowIf(nameof(_stageKind), NoiseStageKind.Generator)]
        [SerializeField] int _seed;

        [FoldoutGroup("Generator")]
        [ShowIf(nameof(_stageKind), NoiseStageKind.Generator)]
        [SerializeField] string _baseUvInput = string.Empty;

        [FoldoutGroup("Generator")]
        [ShowIf(nameof(_stageKind), NoiseStageKind.Generator)]
        [SerializeField] Vector2 _scale = Vector2.one;

        [FoldoutGroup("Generator")]
        [ShowIf(nameof(_stageKind), NoiseStageKind.Generator)]
        [SerializeField] Vector2 _offset = Vector2.zero;

        [FoldoutGroup("Generator")]
        [ShowIf(nameof(_stageKind), NoiseStageKind.Generator)]
        [Range(0f, 360f)]
        [SerializeField] float _rotation;

        [FoldoutGroup("Generator")]
        [ShowIf(nameof(_stageKind), NoiseStageKind.Generator)]
        [SerializeField] Color _gradientA = Color.black;

        [FoldoutGroup("Generator")]
        [ShowIf(nameof(_stageKind), NoiseStageKind.Generator)]
        [SerializeField] Color _gradientB = Color.white;

        [FoldoutGroup("Generator")]
        [ShowIf(nameof(_stageKind), NoiseStageKind.Generator)]
        [Range(1, 8)]
        [SerializeField] int _octaves = 4;

        [FoldoutGroup("Generator")]
        [ShowIf(nameof(_stageKind), NoiseStageKind.Generator)]
        [SerializeField] float _lacunarity = 2f;

        [FoldoutGroup("Generator")]
        [ShowIf(nameof(_stageKind), NoiseStageKind.Generator)]
        [SerializeField] float _gain = 0.5f;

        // ── Uv ──────────────────────────────────────────────────

        [FoldoutGroup("UV")]
        [ShowIf(nameof(_stageKind), NoiseStageKind.Uv)]
        [SerializeField] NoiseUvOp _uvOp = NoiseUvOp.Scroll;

        [FoldoutGroup("UV")]
        [ShowIf(nameof(_stageKind), NoiseStageKind.Uv)]
        [SerializeField] string _uvBaseUvInput = string.Empty;

        [FoldoutGroup("UV")]
        [ShowIf(nameof(_stageKind), NoiseStageKind.Uv)]
        [SerializeField] string _vectorInput = string.Empty;

        [FoldoutGroup("UV")]
        [ShowIf(nameof(_stageKind), NoiseStageKind.Uv)]
        [SerializeField] Vector2 _scroll = Vector2.zero;

        [FoldoutGroup("UV")]
        [ShowIf(nameof(_stageKind), NoiseStageKind.Uv)]
        [SerializeField] float _flowStrength = 1f;

        [FoldoutGroup("UV")]
        [ShowIf(nameof(_stageKind), NoiseStageKind.Uv)]
        [Range(0f, 360f)]
        [SerializeField] float _uvRotation;

        [FoldoutGroup("UV")]
        [ShowIf(nameof(_stageKind), NoiseStageKind.Uv)]
        [SerializeField] Vector2 _polarCenter = new(0.5f, 0.5f);

        // ── Filter ──────────────────────────────────────────────

        [FoldoutGroup("Filter")]
        [ShowIf(nameof(_stageKind), NoiseStageKind.Filter)]
        [SerializeField] NoiseFilterOp _filterOp = NoiseFilterOp.Levels;

        [FoldoutGroup("Filter")]
        [ShowIf(nameof(_stageKind), NoiseStageKind.Filter)]
        [SerializeField] string _primaryInput = string.Empty;

        [FoldoutGroup("Filter")]
        [ShowIf(nameof(_stageKind), NoiseStageKind.Filter)]
        [SerializeField] string _uvInput = string.Empty;

        [FoldoutGroup("Filter")]
        [ShowIf(nameof(_stageKind), NoiseStageKind.Filter)]
        [Range(0f, 1f)]
        [SerializeField] float _strength = 1f;

        [FoldoutGroup("Filter")]
        [ShowIf(nameof(_stageKind), NoiseStageKind.Filter)]
        [Range(0f, 1f)]
        [SerializeField] float _threshold = 0.5f;

        [FoldoutGroup("Filter")]
        [ShowIf(nameof(_stageKind), NoiseStageKind.Filter)]
        [Range(0f, 1f)]
        [SerializeField] float _softness = 0.1f;

        [FoldoutGroup("Filter")]
        [ShowIf(nameof(_stageKind), NoiseStageKind.Filter)]
        [SerializeField] string _warpVectorInput = string.Empty;

        [FoldoutGroup("Filter")]
        [ShowIf(nameof(_stageKind), NoiseStageKind.Filter)]
        [SerializeField] float _normalStrength = 1f;

        // ── Composite ───────────────────────────────────────────

        [FoldoutGroup("Composite")]
        [ShowIf(nameof(_stageKind), NoiseStageKind.Composite)]
        [SerializeField] NoiseCompositeOp _compositeOp = NoiseCompositeOp.Blend;

        [FoldoutGroup("Composite")]
        [ShowIf(nameof(_stageKind), NoiseStageKind.Composite)]
        [SerializeField] string _compositePrimaryInput = string.Empty;

        [FoldoutGroup("Composite")]
        [ShowIf(nameof(_stageKind), NoiseStageKind.Composite)]
        [SerializeField] string _secondaryInput = string.Empty;

        [FoldoutGroup("Composite")]
        [ShowIf(nameof(_stageKind), NoiseStageKind.Composite)]
        [SerializeField] string _maskInput = string.Empty;

        [FoldoutGroup("Composite")]
        [ShowIf(nameof(_stageKind), NoiseStageKind.Composite)]
        [Range(0f, 1f)]
        [SerializeField] float _blend = 0.5f;

        [FoldoutGroup("Composite")]
        [ShowIf(nameof(_stageKind), NoiseStageKind.Composite)]
        [Range(0f, 1f)]
        [SerializeField] float _opacity = 1f;

        // ── Public Accessors ────────────────────────────────────

        public string StageId => _stageId;
        public NoiseStageKind StageKind => _stageKind;
        public bool Enabled => _enabled;
        public string OutputSlot => _outputSlot;
        public bool UseGlobalTime => _useGlobalTime;
        public float Speed => _speed;
        public float Phase => _phase;

        // Generator
        public NoiseGeneratorOp GeneratorOp => _generatorOp;
        public int Seed => _seed;
        public string BaseUvInput => _baseUvInput;
        public Vector2 Scale => _scale;
        public Vector2 Offset => _offset;
        public float Rotation => _rotation;
        public Color GradientA => _gradientA;
        public Color GradientB => _gradientB;
        public int Octaves => _octaves;
        public float Lacunarity => _lacunarity;
        public float Gain => _gain;

        // UV
        public NoiseUvOp UvOp => _uvOp;
        public string UvBaseUvInput => _uvBaseUvInput;
        public string VectorInput => _vectorInput;
        public Vector2 Scroll => _scroll;
        public float FlowStrength => _flowStrength;
        public float UvRotation => _uvRotation;
        public Vector2 PolarCenter => _polarCenter;

        // Filter
        public NoiseFilterOp FilterOp => _filterOp;
        public string PrimaryInput => _primaryInput;
        public string UvInput => _uvInput;
        public float Strength => _strength;
        public float Threshold => _threshold;
        public float Softness => _softness;
        public string WarpVectorInput => _warpVectorInput;
        public float NormalStrength => _normalStrength;

        // Composite
        public NoiseCompositeOp CompositeOp => _compositeOp;
        public string CompositePrimaryInput => _compositePrimaryInput;
        public string SecondaryInput => _secondaryInput;
        public string MaskInput => _maskInput;
        public float Blend => _blend;
        public float Opacity => _opacity;

        public bool IsTimeReactive
            => _stageKind == NoiseStageKind.Uv && _uvOp == NoiseUvOp.Scroll;
    }
}
