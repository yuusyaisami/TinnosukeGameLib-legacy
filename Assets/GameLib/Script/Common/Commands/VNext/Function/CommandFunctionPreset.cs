#nullable enable

using System;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    [Serializable]
    public sealed class CommandFunctionPreset : IDynamicManagedRefValue
    {
        [LabelText("Commands")]
        [HideLabel]
        [CommandListFunctionName("Command.Function.Body")]
        public CommandListData Commands = new();

        public void BindDebugOwner(UnityEngine.Object owner, string fieldPath)
        {
            Commands?.BindDebugOwner(owner, fieldPath);
        }

        public override string ToString()
        {
            return $"Function Commands={Commands?.Count ?? 0}";
        }
    }

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
