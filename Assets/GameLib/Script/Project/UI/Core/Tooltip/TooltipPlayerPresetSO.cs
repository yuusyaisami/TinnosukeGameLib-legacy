#nullable enable
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.UI
{
    [CreateAssetMenu(menuName = "Game/UI/Tooltip/Player Preset", fileName = "TooltipPlayerPreset")]
    public sealed class TooltipPlayerPresetSO : ScriptableObject, IDynamicValueAsset<TooltipPlayerPreset>
    {
        [SerializeReference, InlineProperty, HideLabel]
        TooltipPlayerPreset? _preset = new();

        public TooltipPlayerPreset? Preset
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
            _preset ??= new TooltipPlayerPreset();
        }
    }
}
