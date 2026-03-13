// Game.Common.IExternalExpressionVariablesReceiver.cs
//
// Expression 系 IDynamicSource に外部の ExpressionVariable リストを注入するためのインターフェイス。

using System.Collections.Generic;

namespace Game.Common
{
    public interface IExternalExpressionVariablesReceiver
    {
        void SetExternalVariables(IReadOnlyList<ExpressionVariable> variables, bool includeLocalVariables = false);
        void ClearExternalVariables();
    }
}

