#nullable enable

using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Flow
{
    [Serializable]
    public sealed class FlowWhileStmt : FlowStatement
    {
        [BoxGroup("While")]
        public FlowArgDef Condition;

        [BoxGroup("While")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
        [SerializeReference]
        public FlowStatement[] Body = Array.Empty<FlowStatement>();

        public override void EnsureIntegrity()
        {
            Condition.EnsureIntegrity();
            Body ??= Array.Empty<FlowStatement>();
            for (int i = 0; i < Body.Length; i++) Body[i]?.EnsureIntegrity();
        }
    }
}
