#nullable enable

using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Flow
{
    [Serializable]
    public sealed class FlowIfStmt : FlowStatement
    {
        [BoxGroup("If")]
        public FlowArgDef Condition;

        [BoxGroup("If")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
        [SerializeReference]
        public FlowStatement[] Then = Array.Empty<FlowStatement>();

        [BoxGroup("If")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
        [SerializeReference]
        public FlowStatement[] Else = Array.Empty<FlowStatement>();

        public override void EnsureIntegrity()
        {
            Condition.EnsureIntegrity();
            Then ??= Array.Empty<FlowStatement>();
            Else ??= Array.Empty<FlowStatement>();
            for (int i = 0; i < Then.Length; i++) Then[i]?.EnsureIntegrity();
            for (int i = 0; i < Else.Length; i++) Else[i]?.EnsureIntegrity();
        }
    }
}
