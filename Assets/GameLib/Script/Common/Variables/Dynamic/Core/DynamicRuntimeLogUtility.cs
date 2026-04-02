using System.Text;
using Game;
using Game.Commands.VNext;

namespace Game.Common
{
    internal static class DynamicRuntimeLogUtility
    {
        public static void AppendField(StringBuilder sb, string key, string value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            sb.Append(" | ");
            sb.Append(key);
            sb.Append('=');
            sb.Append('\'');
            sb.Append(Sanitize(value));
            sb.Append('\'');
        }

        public static void AppendDynamicContextFields(StringBuilder sb, IDynamicContext dynamicContext)
        {
            AppendField(sb, "scope", DescribeScope(dynamicContext?.Scope));
            AppendField(sb, "vars", dynamicContext?.Vars != null ? "available" : "null");

            if (dynamicContext is CommandContext commandContext)
            {
                AppendField(sb, "actor", DescribeScope(commandContext.Actor));
                AppendField(sb, "commandRoot", DescribeScope(commandContext.CommandRootScope));
                AppendField(sb, "rootActor", DescribeScope(commandContext.RootActor));
                AppendField(sb, "callerActor", DescribeScope(commandContext.CallerActor));
            }
        }

        public static void AppendCommandTraceFields(StringBuilder sb)
        {
            if (!CommandExecutionTrace.TryGetCurrent(out var trace))
                return;

            AppendField(sb, "cmdTraceIndex", trace.CommandIndex.ToString());
            AppendField(sb, "cmdTrace", BuildCommandLabel(trace.CommandName, trace.CommandId));
            AppendField(sb, "cmdSource", trace.SourceName);
            AppendField(sb, "cmdType", trace.DataType);
            AppendField(sb, "cmdList", trace.ListLabel);
            AppendField(sb, "cmdFunction", trace.ListFunctionName);
            AppendField(sb, "cmdData", trace.DebugData);
            AppendField(sb, "cmdScope", DescribeScope(trace.Scope));
            AppendField(sb, "cmdActor", DescribeScope(trace.Actor));
            AppendField(sb, "cmdCommandRoot", DescribeScope(trace.CommandRootScope));
            AppendField(sb, "cmdRootActor", DescribeScope(trace.RootActor));
            AppendField(sb, "cmdCallerActor", DescribeScope(trace.CallerActor));
        }

        public static string DescribeScope(IScopeNode scope)
        {
            return CommandExecutionTrace.DescribeScope(scope);
        }

        static string BuildCommandLabel(string commandName, int commandId)
        {
            var name = string.IsNullOrEmpty(commandName) ? "<unknown>" : commandName;
            return name + "(Id=" + commandId + ")";
        }

        static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var normalized = value.Replace("\r", string.Empty).Replace("\n", "\\n");
            const int maxLen = 240;
            if (normalized.Length <= maxLen)
                return normalized;

            return normalized.Substring(0, maxLen) + "...";
        }
    }
}
