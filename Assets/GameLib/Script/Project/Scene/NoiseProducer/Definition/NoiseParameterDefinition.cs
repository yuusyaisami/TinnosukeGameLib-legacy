#nullable enable
using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.NoiseProducer
{
    [Serializable]
    public sealed class NoiseParameterDefinition
    {
        [SerializeField] string _parameterKey = string.Empty;
        [SerializeField] NoiseParameterValueKind _valueKind = NoiseParameterValueKind.Float;
        [SerializeField] float _defaultFloat;
        [SerializeField] Vector2 _defaultVector2;
        [SerializeField] Color _defaultColor = Color.white;
        [SerializeField] bool _defaultBool;

        [SerializeField, MinMaxSlider(0f, 100f, true)]
        Vector2 _range = new(0f, 1f);

        [SerializeField] bool _exposed = true;

        [SerializeField]
        [Tooltip("この parameter が影響する stage の StageId")]
        string _affectsStageId = string.Empty;

        [SerializeField]
        [Tooltip("stage 内の対象フィールド名")]
        string _affectsField = string.Empty;

        [SerializeField, TextArea(1, 2)]
        string _description = string.Empty;

        // ── Accessors ───────────────────────────────────────────

        public string ParameterKey => _parameterKey;
        public NoiseParameterValueKind ValueKind => _valueKind;
        public float Min => _range.x;
        public float Max => _range.y;
        public bool Exposed => _exposed;
        public string AffectsStageId => _affectsStageId;
        public string AffectsField => _affectsField;
        public string Description => _description;

        public NoiseParameterValue GetDefaultValue()
        {
            return _valueKind switch
            {
                NoiseParameterValueKind.Float => NoiseParameterValue.Float(_defaultFloat),
                NoiseParameterValueKind.Vector2 => NoiseParameterValue.Vec2(_defaultVector2),
                NoiseParameterValueKind.Color => NoiseParameterValue.Col(_defaultColor),
                NoiseParameterValueKind.Bool => NoiseParameterValue.Bool(_defaultBool),
                NoiseParameterValueKind.Int => NoiseParameterValue.Int((int)_defaultFloat),
                _ => NoiseParameterValue.Float(0f),
            };
        }
    }
}
