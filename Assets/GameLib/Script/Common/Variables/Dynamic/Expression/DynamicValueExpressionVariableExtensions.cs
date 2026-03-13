// Game.Common.DynamicValueExpressionVariableExtensions.cs
//
// DynamicValue から Expression 変数共有を注入するためのヘルパ。

using System.Collections.Generic;

namespace Game.Common
{
    public static class DynamicValueExpressionVariableExtensions
    {
        public static bool TrySetExternalExpressionVariables(
            this DynamicValue value,
            IReadOnlyList<ExpressionVariable> variables,
            bool includeLocalVariables = false)
        {
            if (variables == null)
                return false;

            if (!value.TryGetSource<IExternalExpressionVariablesReceiver>(out var receiver) || receiver == null)
                return false;

            receiver.SetExternalVariables(variables, includeLocalVariables);
            return true;
        }

        public static bool TrySetExternalExpressionVariables<T>(
            this DynamicValue<T> value,
            IReadOnlyList<ExpressionVariable> variables,
            bool includeLocalVariables = false)
        {
            if (variables == null)
                return false;

            if (!value.TryGetSource<IExternalExpressionVariablesReceiver>(out var receiver) || receiver == null)
                return false;

            receiver.SetExternalVariables(variables, includeLocalVariables);
            return true;
        }

        public static bool TryClearExternalExpressionVariables(this DynamicValue value)
        {
            if (!value.TryGetSource<IExternalExpressionVariablesReceiver>(out var receiver) || receiver == null)
                return false;

            receiver.ClearExternalVariables();
            return true;
        }

        public static bool TryClearExternalExpressionVariables<T>(this DynamicValue<T> value)
        {
            if (!value.TryGetSource<IExternalExpressionVariablesReceiver>(out var receiver) || receiver == null)
                return false;

            receiver.ClearExternalVariables();
            return true;
        }
    }
}

