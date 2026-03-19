#nullable enable

using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    [CreateAssetMenu(menuName = "Game/Commands/VNext/Command Function", fileName = "CommandFunction")]
    public sealed class CommandFunctionSO : ScriptableObject, IDynamicValueAsset<CommandFunctionPreset>
    {
        [SerializeReference, InlineProperty, HideLabel]
        CommandFunctionPreset? preset = new();

        public CommandFunctionPreset? Preset
        {
            get
            {
                EnsurePreset();
                return preset;
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
            if (preset == null)
                preset = new CommandFunctionPreset();
        }

        void BindDebugOwner()
        {
            preset?.BindDebugOwner(this, "preset.Commands");
        }
    }
}
