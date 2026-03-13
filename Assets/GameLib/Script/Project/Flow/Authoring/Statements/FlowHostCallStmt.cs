#nullable enable

using System;
using Sirenix.OdinInspector;

namespace Game.Flow
{
    [Serializable]
    public sealed class FlowHostCallStmt : FlowStatement
    {
        [BoxGroup("HostCall")]
        public int SysId;

        [BoxGroup("HostCall")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
        public FlowArgDef[] Args = Array.Empty<FlowArgDef>();

        [BoxGroup("HostCall")]
        [LabelText("Result StableKey (Shared)")]
        [VariableKeyPicker]
        public string ResultStableKey = string.Empty;

        public override void EnsureIntegrity()
        {
            Args ??= Array.Empty<FlowArgDef>();
            for (int i = 0; i < Args.Length; i++) Args[i].EnsureIntegrity();
            ResultStableKey ??= string.Empty;
        }
    }
}
