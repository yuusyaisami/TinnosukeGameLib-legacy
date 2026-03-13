// Game.Common.ManagedRefAssetSource<TAsset, TValue>
//
// 個別の AssetXxxPresetSource を共通化する generic asset source。

#nullable enable

using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Common
{
    /// <summary>
    /// ManagedRef 系の汎用 asset source。
    /// <para>
    /// Asset wrapper (<see cref="IDynamicValueAsset{TValue}"/>) から Preset を取得して返す。
    /// 個別の <c>AssetStateMachinePresetSource</c> 等を 1 つの generic 型で代替する。
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class ManagedRefAssetSource<TAsset, TValue> : IDynamicSource
        where TAsset : ScriptableObject, IDynamicValueAsset<TValue>
        where TValue : class
    {
        [SerializeField, HideLabel]
        TAsset? value;

        public string SourceTypeName => "Asset";
        public string GetDebugData => value != null ? value.name : "null";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            var preset = value != null ? value.Preset : default;
            return preset != null ? DynamicVariant.FromManagedRef(preset) : DynamicVariant.Null;
        }
    }
}
