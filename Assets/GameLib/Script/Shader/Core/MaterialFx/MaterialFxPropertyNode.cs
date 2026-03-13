using System;
using UnityEngine;
using Game.Registry;
using Sirenix.OdinInspector;

namespace Game.MaterialFx
{
    /// <summary>
    /// MaterialFx プロパティのノード。
    /// </summary>
    [Serializable]
    public sealed class MaterialFxPropertyNode : HierarchyNodeBase
    {
        [SerializeField] string stableKey;
        [SerializeField] MaterialFxSenderKind sender;
        [SerializeField] ValueKind valueType;
        [SerializeField] string shaderPropertyName;

        [Header("Enum-like Settings")]
        [Tooltip("When ValueType is Int or Float, optionally bind an EnumDefinition for value selection")]
        [ShowIf("@valueType == ValueKind.Int || valueType == ValueKind.Float")]
        [SerializeField] MaterialFxEnumDefinitionSO enumDefinition;

        [Header("Range Settings")]
        [Tooltip("Enable range constraint for Float values")]
        [ShowIf("@valueType == ValueKind.Float")]
        [SerializeField] bool rangeEnabled;

        [Tooltip("Min and Max values for Range (x=min, y=max)")]
        [ShowIf("@valueType == ValueKind.Float && rangeEnabled")]
        [SerializeField] Vector2 rangeMinMax = new Vector2(0f, 1f);

        /// <summary>安定キー（変更されないID）</summary>
        public string StableKey { get => stableKey; set => stableKey = value ?? string.Empty; }

        /// <summary>送信元の種類</summary>
        public MaterialFxSenderKind Sender { get => sender; set => sender = value; }

        /// <summary>値の型</summary>
        public ValueKind ValueType { get => valueType; set => valueType = value; }

        /// <summary>シェーダープロパティ名</summary>
        public string ShaderPropertyName { get => shaderPropertyName; set => shaderPropertyName = value ?? string.Empty; }

        /// <summary>Enum-like 定義（Int/Float 時のモード選択用）</summary>
        public MaterialFxEnumDefinitionSO EnumDefinition { get => enumDefinition; set => enumDefinition = value; }

        /// <summary>EnumDefinition が設定されているか</summary>
        public bool HasEnumDefinition => enumDefinition != null;

        /// <summary>Range 制約が有効か（Float 時のみ）</summary>
        public bool RangeEnabled { get => rangeEnabled; set => rangeEnabled = value; }

        /// <summary>Range の最小値・最大値（x=min, y=max）</summary>
        public Vector2 RangeMinMax { get => rangeMinMax; set => rangeMinMax = value; }

        /// <summary>Range 最小値</summary>
        public float RangeMin => rangeMinMax.x;

        /// <summary>Range 最大値</summary>
        public float RangeMax => rangeMinMax.y;
    }
}
