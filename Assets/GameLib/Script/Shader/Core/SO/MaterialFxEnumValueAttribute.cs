#nullable enable
using System;
using UnityEngine;

namespace Game.MaterialFx
{
    /// <summary>
    /// Int/Float フィールドに EnumDefinition による Dropdown 表示を適用する属性。
    /// PropertyDrawer でこの属性を検出し、指定された EnumDefinition を使用して
    /// ドロップダウンを表示する。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class MaterialFxEnumValueAttribute : PropertyAttribute
    {
        /// <summary>
        /// 静的に指定する EnumDefinition のアセット名。
        /// 空の場合は動的に決定される（Registry から取得など）。
        /// </summary>
        public string EnumDefinitionName { get; }

        /// <summary>
        /// EnumDefinition を Registry から動的に取得するための StableKey。
        /// 空の場合は使用しない。
        /// </summary>
        public string StableKeyField { get; }

        /// <summary>
        /// コンストラクタ（静的 EnumDefinition 名指定）
        /// </summary>
        /// <param name="enumDefinitionName">EnumDefinition アセットの名前</param>
        public MaterialFxEnumValueAttribute(string enumDefinitionName = "")
        {
            EnumDefinitionName = enumDefinitionName ?? string.Empty;
            StableKeyField = string.Empty;
        }

        /// <summary>
        /// コンストラクタ（StableKey フィールド名指定）
        /// </summary>
        /// <param name="enumDefinitionName">EnumDefinition アセット名（空可）</param>
        /// <param name="stableKeyField">StableKey を取得するフィールド名</param>
        public MaterialFxEnumValueAttribute(string enumDefinitionName, string stableKeyField)
        {
            EnumDefinitionName = enumDefinitionName ?? string.Empty;
            StableKeyField = stableKeyField ?? string.Empty;
        }
    }
}
