#nullable enable
using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.MaterialFx
{
    public enum BaseShaderFxPresetSourceMode
    {
        Inline = 0,
        Asset = 1,
    }

    [Serializable]
    public sealed class BaseShaderFxPresetReference
    {
        [SerializeField, EnumToggleButtons]
        BaseShaderFxPresetSourceMode sourceMode = BaseShaderFxPresetSourceMode.Asset;

        [SerializeField, ShowIf(nameof(IsAssetMode))]
        BaseShaderFxPresetSO? assetPreset;

        [SerializeReference, ShowIf(nameof(IsInlineMode)), InlineProperty, HideLabel]
        BaseShaderFxPreset inlinePreset = new();

        public BaseShaderFxPresetSourceMode SourceMode => sourceMode;
        public BaseShaderFxPresetSO? AssetPreset => assetPreset;
        public BaseShaderFxPreset InlinePreset => inlinePreset;

        public BaseShaderFxPreset? ResolvePreset()
        {
            if (sourceMode == BaseShaderFxPresetSourceMode.Asset)
                return assetPreset?.Preset;
            return inlinePreset;
        }

        bool IsInlineMode => sourceMode == BaseShaderFxPresetSourceMode.Inline;
        bool IsAssetMode => sourceMode == BaseShaderFxPresetSourceMode.Asset;
    }
}
