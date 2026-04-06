// Game.StatusEffect.EffectVisualData.cs
//
// StatusEffect 用の表示データ

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
        [Tooltip("追加説明文を表示する条件の判定方法です。")]
        public StatusEffectAdditionalDescriptionConditionType ConditionType = StatusEffectAdditionalDescriptionConditionType.OperationEnabledById;

        [ShowIf(nameof(UsesOperationEnabledById))]
        [LabelText("Operation Id")]
        [Tooltip("Condition Type が OperationEnabledById のときに参照する operationId です。")]
        public string OperationId = string.Empty;

        [ShowIf(nameof(UsesDynamicBool))]
        [LabelText("Bool Value")]
        [Tooltip("Condition Type が DynamicBool のときに評価する bool 値です。")]
        public DynamicValue<bool> BoolValue;

        [LabelText("Compare")]
        [EnumToggleButtons]
        [Tooltip("判定値と Expected を == / != で比較します。")]
        public StatusEffectBooleanCompareMode CompareMode = StatusEffectBooleanCompareMode.Equals;

        [LabelText("Expected")]
        [Tooltip("Compare で比較する期待値です。")]
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
        [Tooltip("この条件が成立したときだけ Text を追加します。")]
        public StatusEffectAdditionalDescriptionCondition Condition = new();

        [LabelText("Text")]
        [Tooltip("条件成立時に追加する説明文です。既定で RichText source を使用します。")]
        public DynamicValue<string> Text = DynamicValue<string>.FromSource(new RichTextSource());
    }

    /// <summary>
    /// StatusEffect 用の表示データ。
    /// </summary>
    [Serializable]
    public sealed class EffectVisualData
    {
        [LabelText("Display Name")]
        [InlineProperty]
        [Tooltip("UI 等で表示される名前です。")]
        public RichTextTemplateData DisplayName = new();

        [LabelText("Description")]
        [InlineProperty]
        [Tooltip("基本説明文です。")]
        public RichTextTemplateData Description = new();

        [LabelText("Stack Description")]
        [InlineProperty]
        [Tooltip("Stack 情報を反映して表示する説明文テンプレートです。")]
        public RichTextTemplateData StackDescription = new();

        [LabelText("Additional Descriptions")]
        [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = false, ShowFoldout = true)]
        [Tooltip("条件成立時のみ末尾に追加する説明文です。複数設定できます。")]
        public List<StatusEffectAdditionalDescriptionEntry> AdditionalDescriptions = new();

        [LabelText("Icon")]
        [PreviewField(55)]
        [Tooltip("StatusEffect 表示に使うアイコンです。")]
        public Sprite Icon;

        /// <summary>優先度表示用のソート順</summary>
        [LabelText("Sort Order")]
        [Tooltip("一覧表示時の並び順です。小さい値ほど前に表示されます。")]
        public int SortOrder;

        public string DisplayNameText => DisplayName?.Template ?? string.Empty;
        public string DescriptionText => Description?.Template ?? string.Empty;
        public string StackDescriptionText => StackDescription?.Template ?? string.Empty;
    }
}
