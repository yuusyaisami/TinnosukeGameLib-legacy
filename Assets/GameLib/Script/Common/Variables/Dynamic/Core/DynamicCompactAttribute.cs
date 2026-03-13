// Game.Common.DynamicCompactAttribute.cs
//
// DynamicCompact属性 - DynamicValue の Inspector 表示を2行+展開ボタンに変更
//
// 使用例:
// [DynamicCompact]
// public DynamicValue myValue;

using System;

namespace Game.Common
{
    /// <summary>
    /// DynamicValue フィールドに付与すると、Inspector で
    /// コンパクトな 2 行表示 + 展開ボタンの UI になる。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class DynamicCompactAttribute : Attribute
    {
        /// <summary>
        /// ラベルを非表示にするか。
        /// </summary>
        public bool HideLabel { get; set; }

        public DynamicCompactAttribute()
        {
        }
    }
}
