#nullable enable
using System;
using Sirenix.OdinInspector;

namespace Game.Commands.VNext
{
    [Serializable]
    public sealed class RunPlayerCommandsCommandData : ICommandData
    {
        public int CommandId => CommandIds.RunPlayerCommands;
        public string DebugData
        {
            get
            {
                var count = Commands?.Count ?? 0;
                return $"Commands={count}";
            }
        }

        [LabelText("Commands")]
        public CommandListData Commands = new();
    }
}
