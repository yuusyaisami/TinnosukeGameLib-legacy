#nullable enable

using System;
using Sirenix.OdinInspector;

namespace Game.Flow
{
    [Serializable]
    public sealed class FlowCallStmt : FlowStatement
    {
        [BoxGroup("Call")]
        public string FunctionName = string.Empty;

        public override void EnsureIntegrity()
        {
            FunctionName ??= string.Empty;
        }
    }
}
