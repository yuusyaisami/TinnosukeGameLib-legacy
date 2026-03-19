#nullable enable

using System;
using Game.Common;
using Sirenix.OdinInspector;

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
}
