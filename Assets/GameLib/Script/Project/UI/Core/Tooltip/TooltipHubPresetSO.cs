#nullable enable
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.UI
{
    [CreateAssetMenu(menuName = "Game/UI/Tooltip/Hub Preset", fileName = "TooltipHubPreset")]
    public sealed class TooltipHubPresetSO : ScriptableObject, IDynamicValueAsset<TooltipHubPreset>
    {
        [SerializeReference, InlineProperty, HideLabel]
        TooltipHubPreset? _preset = new();

        public TooltipHubPreset? Preset
        {
            get
            {
                EnsurePreset();
                return _preset;
            }
        }

        void OnEnable() => EnsurePreset();
        void OnValidate() => EnsurePreset();

        void EnsurePreset()
        {
            _preset ??= new TooltipHubPreset();
        }
    }
}
