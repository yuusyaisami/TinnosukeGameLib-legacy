#nullable enable
using System;
using Game.Common;
using Sirenix.OdinInspector;

namespace Game.Commands.VNext
{
    [Serializable]
    public sealed class ShowTooltipCommandData : ICommandData
    {
        [BoxGroup("Text")]
        [LabelText("Channel Key")]
        public string textChannelKey = "default";

        [BoxGroup("Text")]
        [LabelText("Text Content")]
        [MultiLineProperty(3)]
        public DynamicValue<string> text = DynamicValueExtensions.FromLiteral(string.Empty);

        [BoxGroup("Commands")]
        [LabelText("Extra Spawn Commands")]
        // tooltip側で実行されるコマンド群
        public CommandListData contentCommands = new CommandListData(); 

        public int CommandId => CommandIds.ShowTooltip;
        public string DebugData => "Show";
    }

    [Serializable]
    public sealed class HideTooltipCommandData : ICommandData
    {
        public int CommandId => CommandIds.HideTooltip;
        public string DebugData => "Hide";
    }
}
