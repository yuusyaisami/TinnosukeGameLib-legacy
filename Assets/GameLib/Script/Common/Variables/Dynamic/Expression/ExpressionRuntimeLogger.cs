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
            var sb = new StringBuilder(512);
            sb.Append("[Expression]");
            sb.Append('[').Append(level).Append(']');
            sb.Append('[').Append(string.IsNullOrEmpty(code) ? "EX-UNKNOWN" : code).Append("] ");
            sb.Append(message ?? string.Empty);

            DynamicRuntimeLogUtility.AppendField(sb, "source", context.SourceType);
            DynamicRuntimeLogUtility.AppendField(sb, "phase", context.Phase);
            DynamicRuntimeLogUtility.AppendField(sb, "expression", context.Expression);
            DynamicRuntimeLogUtility.AppendField(sb, "detail", context.Detail);
            DynamicRuntimeLogUtility.AppendField(sb, "variables", context.Variables);
            if (context.AllowImplicitKeys.HasValue)
                DynamicRuntimeLogUtility.AppendField(sb, "allowImplicitKeys", context.AllowImplicitKeys.Value ? "true" : "false");

            DynamicRuntimeLogUtility.AppendDynamicContextFields(sb, context.DynamicContext);
            DynamicRuntimeLogUtility.AppendCommandTraceFields(sb);
            return sb.ToString();
        }
    }
}
