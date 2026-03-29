#nullable enable
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.UI
{
    [CreateAssetMenu(menuName = "Game/UI/Tooltip/HitTest Preset", fileName = "TooltipHitTestPreset")]
    public sealed class TooltipHitTestPresetSO : ScriptableObject, IDynamicValueAsset<TooltipHitTestPreset>
    {
        [SerializeReference, InlineProperty, HideLabel]
        TooltipHitTestPreset? _preset = new();

        public TooltipHitTestPreset? Preset
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
            _preset ??= new TooltipHitTestPreset();
        }
    }
}
