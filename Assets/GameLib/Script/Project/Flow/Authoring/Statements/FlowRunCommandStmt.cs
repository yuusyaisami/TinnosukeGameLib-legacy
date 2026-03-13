#nullable enable

using System;
using Game.Commands.VNext;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Flow
{
    [Serializable]
    public sealed class FlowRunCommandStmt : FlowStatement
    {
        [BoxGroup("RunCommand")]
        [LabelText("Command")]
        [HideReferenceObjectPicker]
        [SerializeReference]
        public ICommandSource? Command;

        [BoxGroup("RunCommand")]
        [LabelText("Use Actor Override")]
        public bool UseActorOverride;

        [BoxGroup("RunCommand")]
        [LabelText("Actor (ScopeNode)")]
        [ShowIf(nameof(UseActorOverride))]
        public FlowArgDef Actor = default;

        public override void EnsureIntegrity()
        {
            if (UseActorOverride)
                Actor.EnsureIntegrity();
        }
    }
}
