#nullable enable

namespace Game.Commands.VNext
{
    public readonly struct CommandRunFrame
    {
        public readonly int CommandIndex;
        public readonly int CommandId;
        public readonly string SourceType;
        public readonly string DataType;
        public readonly string DebugData;

        public CommandRunFrame(int commandIndex, int commandId, string sourceType, string dataType, string debugData = "")
        {
            CommandIndex = commandIndex;
            CommandId = commandId;
            SourceType = sourceType ?? string.Empty;
            DataType = dataType ?? string.Empty;
            DebugData = debugData ?? string.Empty;
        }
    }
}
