#nullable enable
using Game.Common;

namespace Game.Commands.VNext
{
    public static class CommandDebugDataHelper
    {
        public static string GetDynamicDebugData(DynamicValue value, string fallback = "<none>")
        {
            return value.HasSource ? value.DebugData : fallback;
        }

        public static string GetDynamicDebugData<T>(DynamicValue<T> value, string fallback = "<none>")
        {
            DynamicValue raw = value;
            return raw.HasSource ? raw.DebugData : fallback;
        }
    }
}
