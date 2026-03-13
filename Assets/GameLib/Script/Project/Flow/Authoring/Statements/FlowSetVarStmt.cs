#nullable enable

using System;
using Sirenix.OdinInspector;

namespace Game.Flow
{
    [Serializable]
    public sealed class FlowSetVarStmt : FlowStatement
    {
        [BoxGroup("SetVar")]
        public FlowTargetScope TargetScope = FlowTargetScope.Shared;

        [BoxGroup("SetVar")]
        [ShowIf(nameof(ShowStableKey))]
        [VariableKeyPicker]
        public string StableKey = string.Empty;

        [BoxGroup("SetVar")]
        [ShowIf(nameof(ShowLocalName))]
        public string LocalName = string.Empty;

        [BoxGroup("SetVar")]
        public FlowArgDef Value;

        bool ShowStableKey() => TargetScope == FlowTargetScope.Shared;
        bool ShowLocalName() => TargetScope == FlowTargetScope.Local;

        public override void EnsureIntegrity()
        {
            StableKey ??= string.Empty;
            LocalName ??= string.Empty;
            Value.EnsureIntegrity();
        }
    }
}
