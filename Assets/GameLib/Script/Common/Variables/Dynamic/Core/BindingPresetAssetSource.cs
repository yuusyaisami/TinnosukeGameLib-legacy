// Game.Common.BindingPresetAssetSource
//
// DynamicValue<BaseProfileData> 用の汎用 asset source。
// IDynamicValueAsset<BaseProfileData> の共変性を利用して、
// 任意の ProfileSO wrapper から Preset を取得する。

#nullable enable

using System;
using Sirenix.OdinInspector;
using UnityEngine;
using Game.Profile;

namespace Game.Common
{
    [Serializable]
    public sealed class BindingPresetAssetSource : IDynamicSource
    {
        [SerializeField, HideLabel]
        ScriptableObject? _asset;

        public string SourceTypeName => "Asset";
        public string GetDebugData => _asset != null ? _asset.name : "null";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (_asset is IDynamicValueAsset<BaseProfileData> wrapper)
            {
                var preset = wrapper.Preset;
                if (preset != null)
                    return DynamicVariant.FromManagedRef(preset);
            }
            return DynamicVariant.Null;
        }
    }
}
