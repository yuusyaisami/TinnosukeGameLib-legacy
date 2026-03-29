#nullable enable
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.UI
{
    [CreateAssetMenu(menuName = "Game/UI/Tooltip/Commands Preset", fileName = "TooltipCommandsPreset")]
    public sealed class TooltipCommandsPresetSO : ScriptableObject, IDynamicValueAsset<TooltipCommandsPreset>
    {
        [SerializeReference, InlineProperty, HideLabel]
        TooltipCommandsPreset? _preset = new();

        public TooltipCommandsPreset? Preset
        {
            get
            {
                EnsurePreset();
                return _preset;
            }
        }

        void OnEnable()
        {
            EnsurePreset();
            BindDebugOwner();
        }

        void OnValidate()
        {
            EnsurePreset();
            BindDebugOwner();
        }

        void EnsurePreset()
        {
            _preset ??= new TooltipCommandsPreset();
        }

        void BindDebugOwner()
        {
            _preset?.BindDebugOwner(this, nameof(_preset));
        }
    }
}
