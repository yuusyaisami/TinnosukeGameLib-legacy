using System.Globalization;
using System.Text;
using Game;
using Game.Commands.VNext;
using Game.Scalar;
using VContainer;

namespace Game.Common
{
    internal static class DynamicRuntimeLogUtility
    {
        const string ChannelColor = "#4FC1FF";
        const string ErrorColor = "#F44747";
        const string WarnColor = "#DCDCAA";
        const string InfoColor = "#4EC9B0";
        const string SectionColor = "#C586C0";
        const string KeyColor = "#9CDCFE";

        public static void AppendLogHeader(StringBuilder sb, string channel, string level, string code, string message)
        {
            var normalizedChannel = string.IsNullOrEmpty(channel) ? "Dynamic" : channel;
            var normalizedLevel = string.IsNullOrEmpty(level) ? "INFO" : level;
            var normalizedCode = string.IsNullOrEmpty(code) ? "UNKNOWN" : code;
            var levelColor = GetLevelColor(normalizedLevel);

            sb.Append(Colorize("[" + normalizedChannel + "]", ChannelColor));
            sb.Append(Colorize("[" + normalizedLevel + "]", levelColor));
            sb.Append(Colorize("[" + normalizedCode + "]", levelColor));
            if (!string.IsNullOrEmpty(message))
            {
                sb.Append(' ');
                sb.Append(message);
            }
        }

        public static void AppendSection(StringBuilder sb, string title)
        {
            if (string.IsNullOrEmpty(title))
                return;

            if (sb.Length > 0)
                sb.Append("\n\n");

            sb.Append(Colorize("[" + title + "]", SectionColor));
        }

        public static void AppendFieldLine(StringBuilder sb, string key, string value, bool allowMultiline = false)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value))
                return;

            var normalized = NormalizeLineBreak(value);
            if (!allowMultiline || normalized.IndexOf('\n') < 0)
            {
                sb.Append('\n');
                sb.Append("  ");
                sb.Append(Colorize(key + ":", KeyColor));
                sb.Append(' ');
                sb.Append(ClampLength(normalized, 320));
                return;
            }

            sb.Append('\n');
            sb.Append("  ");
            sb.Append(Colorize(key + ":", KeyColor));

            var lines = normalized.Split('\n');
            var printed = 0;
            const int maxLines = 28;

            for (var i = 0; i < lines.Length; i++)
            {
                if (printed >= maxLines)
                {
                    sb.Append('\n');
                    sb.Append("    ...");
                    break;
                }

                var line = ClampLength(lines[i], 320);
                if (line.Length == 0)
                    continue;

                sb.Append('\n');
                sb.Append("    ");
                sb.Append(line);
                printed++;
            }
        }

        public static void AppendDynamicContextSection(StringBuilder sb, IDynamicContext dynamicContext)
        {
            AppendSection(sb, "Context");
            AppendFieldLine(sb, "scope", DescribeScope(dynamicContext?.Scope));
            AppendFieldLine(sb, "vars", dynamicContext?.Vars != null ? "available" : "null");

            if (dynamicContext is CommandContext commandContext)
            {
                AppendFieldLine(sb, "actor", DescribeScope(commandContext.Actor));
                AppendFieldLine(sb, "commandRoot", DescribeScope(commandContext.CommandRootScope));
                AppendFieldLine(sb, "rootActor", DescribeScope(commandContext.RootActor));
                AppendFieldLine(sb, "callerActor", DescribeScope(commandContext.CallerActor));
            }
        }

        public static bool AppendCommandTraceSection(StringBuilder sb)
        {
            if (!CommandExecutionTrace.TryGetCurrent(out var trace))
                return false;

            AppendSection(sb, "Command Trace");
            AppendFieldLine(sb, "index", trace.CommandIndex.ToString());
            AppendFieldLine(sb, "command", BuildCommandLabel(trace.CommandName, trace.CommandId));
            AppendFieldLine(sb, "source", trace.SourceName);
            AppendFieldLine(sb, "type", trace.DataType);
            AppendFieldLine(sb, "list", trace.ListLabel);
            AppendFieldLine(sb, "function", trace.ListFunctionName);
            AppendFieldLine(sb, "data", trace.DebugData, allowMultiline: true);
            AppendFieldLine(sb, "scope", DescribeScope(trace.Scope));
            AppendFieldLine(sb, "actor", DescribeScope(trace.Actor));
            AppendFieldLine(sb, "commandRoot", DescribeScope(trace.CommandRootScope));
            AppendFieldLine(sb, "rootActor", DescribeScope(trace.RootActor));
            AppendFieldLine(sb, "callerActor", DescribeScope(trace.CallerActor));
            return true;
        }

        public static bool AppendActorStoresSection(StringBuilder sb, IDynamicContext dynamicContext, int maxEntries)
        {
            var normalizedMaxEntries = maxEntries < 0 ? 0 : maxEntries;
            var hasAny = false;
            hasAny |= AppendActorBlackboardStoreSection(sb, dynamicContext, normalizedMaxEntries);
            hasAny |= AppendActorVarStoreSection(sb, dynamicContext, normalizedMaxEntries);
            hasAny |= AppendActorScalarSection(sb, dynamicContext, normalizedMaxEntries, normalizedMaxEntries);
            return hasAny;
        }

        public static bool AppendActorBlackboardSection(StringBuilder sb, IDynamicContext dynamicContext, int maxEntries)
        {
            return AppendActorStoresSection(sb, dynamicContext, maxEntries);
        }

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
            // Legacy one-line format kept for backward compatibility.
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
            // Legacy one-line format kept for backward compatibility.
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

        static IScopeNode ResolveActorScope(IDynamicContext dynamicContext)
        {
            if (dynamicContext is CommandContext commandContext)
                return commandContext.Actor ?? commandContext.Scope;

            return dynamicContext?.Scope;
        }

        static string BuildVarStoreEntries(IVarStore vars, int maxEntries, out int totalCount, out int printedCount, out bool truncated)
        {
            totalCount = 0;
            printedCount = 0;
            truncated = false;

            var sb = new StringBuilder(256);
            foreach (var varId in vars.EnumerateVarIds())
            {
                if (varId == 0)
                    continue;

                totalCount++;
                if (maxEntries > 0 && printedCount >= maxEntries)
                {
                    truncated = true;
                    continue;
                }

                if (sb.Length > 0)
                    sb.Append('\n');

                var kind = vars.GetVarKind(varId);
                var key = VarIdResolver.TryGetIdToStable(varId) ?? $"varId={varId}";
                var value = DescribeVarValue(vars, varId, kind);
                sb.Append($"varId={varId} key={key} kind={kind} value={value}");
                printedCount++;
            }

            if (totalCount == 0)
                return "(none)";

            if (sb.Length == 0)
                return "(none)";

            return sb.ToString();
        }

        static bool AppendActorBlackboardStoreSection(StringBuilder sb, IDynamicContext dynamicContext, int maxEntries)
        {
            AppendSection(sb, "Actor Blackboard");

            var actorScope = ResolveActorScope(dynamicContext);
            AppendFieldLine(sb, "actor", DescribeScope(actorScope));

            if (actorScope?.Resolver == null)
            {
                AppendFieldLine(sb, "status", "Actor scope or resolver is unavailable.");
                return false;
            }

            if (!actorScope.Resolver.TryResolve<IBlackboardService>(out var blackboard) || blackboard == null)
            {
                AppendFieldLine(sb, "status", "IBlackboardService is not available on actor scope.");
                return false;
            }

            var localVars = blackboard.LocalVars;
            if (localVars == null)
            {
                AppendFieldLine(sb, "status", "blackboard.LocalVars is null.");
                return false;
            }

            var entries = BuildVarStoreEntries(localVars, maxEntries, out var totalCount, out var printedCount, out var truncated);

            AppendFieldLine(sb, "count", totalCount.ToString());
            if (truncated)
                AppendFieldLine(sb, "truncated", $"true (maxEntries={maxEntries})");
            AppendFieldLine(sb, "printed", printedCount.ToString());
            AppendFieldLine(sb, "entries", entries, allowMultiline: true);
            return true;
        }

        static bool AppendActorVarStoreSection(StringBuilder sb, IDynamicContext dynamicContext, int maxEntries)
        {
            AppendSection(sb, "Actor VarStore");

            if (dynamicContext?.Vars == null)
            {
                AppendFieldLine(sb, "status", "dynamicContext.Vars is null.");
                return false;
            }

            var entries = BuildVarStoreEntries(dynamicContext.Vars, maxEntries, out var totalCount, out var printedCount, out var truncated);
            AppendFieldLine(sb, "count", totalCount.ToString());
            if (truncated)
                AppendFieldLine(sb, "truncated", $"true (maxEntries={maxEntries})");
            AppendFieldLine(sb, "printed", printedCount.ToString());
            AppendFieldLine(sb, "entries", entries, allowMultiline: true);
            return true;
        }

        static bool AppendActorScalarSection(StringBuilder sb, IDynamicContext dynamicContext, int maxKeys, int maxSnapshotsPerKey)
        {
            AppendSection(sb, "Actor Scalar");

            var actorScope = ResolveActorScope(dynamicContext);
            AppendFieldLine(sb, "actor", DescribeScope(actorScope));

            if (actorScope?.Resolver == null)
            {
                AppendFieldLine(sb, "status", "Actor scope or resolver is unavailable.");
                return false;
            }

            if (!actorScope.Resolver.TryResolve<IBaseScalarService>(out var scalar) || scalar == null)
            {
                AppendFieldLine(sb, "status", "IBaseScalarService is not available on actor scope.");
                return false;
            }

            if (!actorScope.Resolver.TryResolve<IScalarTelemetry>(out var telemetry) || telemetry == null)
            {
                AppendFieldLine(sb, "status", "IScalarTelemetry is not available on actor scope.");
                return false;
            }

            var printed = 0;
            var total = 0;
            var truncated = false;
            var entries = new StringBuilder(256);

            foreach (var key in telemetry.EnumerateKeys())
            {
                if (key.Id == 0 && string.IsNullOrWhiteSpace(key.Name))
                    continue;

                total++;
                if (maxKeys > 0 && printed >= maxKeys)
                {
                    truncated = true;
                    continue;
                }

                if (entries.Length > 0)
                    entries.Append('\n');

                var hasLocal = scalar.LocalTryGet(key, out var localValue);
                var hasGlobal = scalar.GlobalTryGet(key, out var globalValue);
                var current = hasLocal ? localValue : (hasGlobal ? globalValue : 0f);

                entries.Append("key=");
                entries.Append(key.FormatLabel());
                entries.Append(" current=");
                entries.Append(FormatNumber(current));
                entries.Append(" local=");
                entries.Append(hasLocal ? FormatNumber(localValue) : "<missing>");
                entries.Append(" global=");
                entries.Append(hasGlobal ? FormatNumber(globalValue) : "<missing>");

                var snapshotCount = 0;
                foreach (var snapshot in telemetry.Enumerate(key))
                {
                    if (maxSnapshotsPerKey > 0 && snapshotCount >= maxSnapshotsPerKey)
                    {
                        entries.Append('\n');
                        entries.Append("  ...snapshots truncated after ");
                        entries.Append(maxSnapshotsPerKey.ToString());
                        entries.Append(" items.");
                        break;
                    }

                    entries.Append('\n');
                    entries.Append("  ");
                    entries.Append(DescribeScalarSnapshot(snapshot));
                    snapshotCount++;
                }

                if (snapshotCount == 0)
                {
                    entries.Append('\n');
                    entries.Append("  (no snapshots)");
                }

                printed++;
            }

            AppendFieldLine(sb, "count", total.ToString());
            if (truncated)
                AppendFieldLine(sb, "truncated", $"true (maxKeys={maxKeys})");
            AppendFieldLine(sb, "printed", printed.ToString());
            AppendFieldLine(sb, "entries", printed == 0 ? "(none)" : entries.ToString(), allowMultiline: true);
            return true;
        }

        static string DescribeScalarSnapshot(ScalarSnapshot snapshot)
        {
            var kindLabel = snapshot.Kind == ScalarModKind.Add ? "Add" : "Mul";
            var valueText = snapshot.Kind == ScalarModKind.Mul
                ? "x" + FormatNumber(snapshot.Value)
                : (snapshot.Value >= 0f ? "+" : string.Empty) + FormatNumber(snapshot.Value);
            var sourceText = snapshot.Source != null ? " src=" + snapshot.Source : string.Empty;
            var tagText = string.IsNullOrWhiteSpace(snapshot.Tag) ? string.Empty : " tag=" + snapshot.Tag;
            var layerText = string.IsNullOrWhiteSpace(snapshot.Layer) ? string.Empty : " layer=" + snapshot.Layer;
            var remainText = snapshot.Remain < 0f ? string.Empty : " remain=" + FormatNumber(snapshot.Remain) + "s";
            return "[" + kindLabel + "] " + valueText + sourceText + tagText + layerText + remainText;
        }

        static string FormatNumber(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        static string DescribeVarValue(IVarStore vars, int varId, ValueKind kind)
        {
            if (kind == ValueKind.ManagedRef)
            {
                if (vars.TryGetManagedRef(varId, out var managed))
                    return managed?.ToString() ?? "<null>";

                return "<null>";
            }

            if (vars.TryGetVariant(varId, out var variant))
                return variant.ToString();

            return "<unavailable>";
        }

        static string Colorize(string text, string color)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            if (string.IsNullOrEmpty(color))
                return text;

            return "<color=" + color + ">" + text + "</color>";
        }

        static string GetLevelColor(string level)
        {
            if (string.Equals(level, "ERROR", System.StringComparison.OrdinalIgnoreCase))
                return ErrorColor;
            if (string.Equals(level, "WARN", System.StringComparison.OrdinalIgnoreCase))
                return WarnColor;

            return InfoColor;
        }

        static string NormalizeLineBreak(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Replace("\r\n", "\n").Replace('\r', '\n');
        }

        static string ClampLength(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || maxLength <= 0)
                return string.Empty;

            return value.Length <= maxLength ? value : value.Substring(0, maxLength) + "...";
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
