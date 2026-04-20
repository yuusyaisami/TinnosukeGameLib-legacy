// Game.StatusEffect.EffectVisualData.cs
//
// StatusEffect 逕ｨ縺ｮ陦ｨ遉ｺ繝・・繧ｿ

using System;
using System.Collections.Generic;
using Game.Common;
using Game.Trait;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.StatusEffect
{
    public enum StatusEffectAdditionalDescriptionConditionType
    {
        OperationEnabledById = 10,
        DynamicBool = 20,
    }

    public enum StatusEffectBooleanCompareMode
    {
        Equals = 10,
        NotEquals = 20,
    }

    [Serializable]
    public sealed class StatusEffectAdditionalDescriptionCondition
    {
        [LabelText("Condition Type")]
        [EnumToggleButtons]
        [Tooltip("Inspector setting.")]
        public StatusEffectAdditionalDescriptionConditionType ConditionType = StatusEffectAdditionalDescriptionConditionType.OperationEnabledById;

        [ShowIf(nameof(UsesOperationEnabledById))]
        [LabelText("Operation Id")]
        [Tooltip("Inspector setting.")]
        public string OperationId = string.Empty;

        [ShowIf(nameof(UsesDynamicBool))]
        [LabelText("Bool Value")]
        [Tooltip("Inspector setting.")]
        public DynamicValue<bool> BoolValue;

        [LabelText("Compare")]
        [EnumToggleButtons]
        [Tooltip("Inspector setting.")]
        public StatusEffectBooleanCompareMode CompareMode = StatusEffectBooleanCompareMode.Equals;

        [LabelText("Expected")]
        [Tooltip("Inspector setting.")]
        public bool Expected = true;

        bool UsesOperationEnabledById()
            => ConditionType == StatusEffectAdditionalDescriptionConditionType.OperationEnabledById;

        bool UsesDynamicBool()
            => ConditionType == StatusEffectAdditionalDescriptionConditionType.DynamicBool;
    }

    [Serializable]
    public sealed class StatusEffectAdditionalDescriptionEntry
    {
        [LabelText("Condition")]
        [InlineProperty]
        [Tooltip("Inspector setting.")]
        public StatusEffectAdditionalDescriptionCondition Condition = new();

        [LabelText("Override")]
        [Tooltip("Inspector setting.")]
        public bool OverrideDescription;

        [LabelText("Text")]
        [Tooltip("Inspector setting.")]
        public DynamicValue<string> Text = DynamicValue<string>.FromSource(new RichTextSource());
    }

    /// <summary>
    /// StatusEffect 逕ｨ縺ｮ陦ｨ遉ｺ繝・・繧ｿ縲・
    /// </summary>
    [Serializable]
    public sealed class EffectVisualData
    {
        [LabelText("Display Name")]
        [InlineProperty]
        [Tooltip("Inspector setting.")]
        public RichTextTemplateData DisplayName = new();

        [LabelText("Description")]
        [InlineProperty]
        [Tooltip("Inspector setting.")]
        public RichTextTemplateData Description = new();

        [LabelText("Stack Description")]
        [InlineProperty]
        [Tooltip("Inspector setting.")]
        public RichTextTemplateData StackDescription = new();

        [LabelText("Additional Descriptions")]
        [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = true, ShowFoldout = true)]
        [Tooltip("Inspector setting.")]
        public List<StatusEffectAdditionalDescriptionEntry> AdditionalDescriptions = new();

        [LabelText("Icon")]
        [PreviewField(55)]
        [Tooltip("Inspector setting.")]
        public Sprite Icon;

        /// <summary>蜆ｪ蜈亥ｺｦ陦ｨ遉ｺ逕ｨ縺ｮ繧ｽ繝ｼ繝磯・/summary>
        [LabelText("Sort Order")]
        [Tooltip("Inspector setting.")]
        public int SortOrder;

        public string DisplayNameText => DisplayName?.Template ?? string.Empty;
        public string DescriptionText => Description?.Template ?? string.Empty;
        public string StackDescriptionText => StackDescription?.Template ?? string.Empty;
    }
}
