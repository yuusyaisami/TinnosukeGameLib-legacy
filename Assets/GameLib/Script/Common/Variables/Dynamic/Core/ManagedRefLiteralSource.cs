// Game.Common.ManagedRefLiteralSource<TValue>
//
// 個別の LiteralXxxPresetSource を共通化する generic literal source。

#nullable enable

using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Common
{
    /// <summary>
    /// ManagedRef 系の汎用 literal source。
    /// <para>
    /// 個別の <c>LiteralStateMachinePresetSource</c> / <c>LiteralHealthPresetSource</c> 等を
    /// 1 つの generic 型で代替する。
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class ManagedRefLiteralSource<TValue> : IDynamicSource
        where TValue : class
    {
        [SerializeReference, InlineProperty, HideLabel]
        TValue? value;

        public ManagedRefLiteralSource() { }
        public ManagedRefLiteralSource(TValue value) => this.value = value;

        public string SourceTypeName => "Literal";
        public string GetDebugData => value?.ToString() ?? "null";

        public DynamicVariant Evaluate(IDynamicContext context)
            => value != null ? DynamicVariant.FromManagedRef(value) : DynamicVariant.Null;
    }
}
