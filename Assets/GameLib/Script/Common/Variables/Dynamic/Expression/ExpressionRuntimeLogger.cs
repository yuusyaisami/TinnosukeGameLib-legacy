using System.Text;
using UnityEngine;

namespace Game.Common
{
    internal struct ExpressionRuntimeLogContext
    {
        public string SourceType;
        public string Phase;
        public string Expression;
        public string Variables;
        public string Detail;
        public bool? AllowImplicitKeys;
        public IDynamicContext DynamicContext;
    }

    internal static class ExpressionRuntimeLogger
    {
        public static void Error(string code, string message, in ExpressionRuntimeLogContext context)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError(BuildMessage("ERROR", code, message, context));
#endif
        }

        static string BuildMessage(string level, string code, string message, in ExpressionRuntimeLogContext context)
        {
            var sb = new StringBuilder(768);
            DynamicRuntimeLogUtility.AppendLogHeader(sb, "Expression", level, string.IsNullOrEmpty(code) ? "EX-UNKNOWN" : code, message ?? string.Empty);

            DynamicRuntimeLogUtility.AppendSection(sb, "Expression");
            DynamicRuntimeLogUtility.AppendFieldLine(sb, "source", context.SourceType);
            DynamicRuntimeLogUtility.AppendFieldLine(sb, "phase", context.Phase);
            DynamicRuntimeLogUtility.AppendFieldLine(sb, "expression", context.Expression);
            DynamicRuntimeLogUtility.AppendFieldLine(sb, "variables", context.Variables);
            if (context.AllowImplicitKeys.HasValue)
                DynamicRuntimeLogUtility.AppendFieldLine(sb, "allowImplicitKeys", context.AllowImplicitKeys.Value ? "true" : "false");
            DynamicRuntimeLogUtility.AppendFieldLine(sb, "detail", context.Detail, allowMultiline: true);

            DynamicRuntimeLogUtility.AppendDynamicContextSection(sb, context.DynamicContext);
            DynamicRuntimeLogUtility.AppendCommandTraceSection(sb);
            return sb.ToString();
        }
    }
}
