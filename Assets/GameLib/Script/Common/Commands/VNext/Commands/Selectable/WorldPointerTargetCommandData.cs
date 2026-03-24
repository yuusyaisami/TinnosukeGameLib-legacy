#nullable enable
using System;
using System.Collections.Generic;
using Game.SelectRuntime;
using Sirenix.OdinInspector;

namespace Game.Commands.VNext
{
    [Serializable]
    public sealed class WorldPointerTargetCommandMutationStep
    {
        [LabelText("Targets")]
        [EnumToggleButtons]
        public WorldPointerTargetCommandTargets Targets = WorldPointerTargetCommandTargets.LeftClicked;

        [LabelText("Mutation")]
        [InlineProperty]
        public CommandListMutationStep Mutation = new()
        {
            Operation = CommandListMutationOperation.Override,
        };
    }

    [Serializable]
    public sealed class WorldPointerTargetCommandMutationProgram
    {
        [LabelText("Steps")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
        public List<WorldPointerTargetCommandMutationStep> Steps = new();
    }

    [Serializable]
    public sealed class WorldPointerTargetControlCommandData : ICommandData
    {
        public int CommandId => CommandIds.WorldPointerTargetControl;

        public string DebugData
        {
            get
            {
                var targetLabel = ActorSourceOdinLabelHelper.GetLabel("Target", Target);
                return $"{targetLabel} Steps={Count(EventCommandProgram)}";
            }
        }

        [BoxGroup("Target")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(Target)")]
        public ActorSource Target;

        [BoxGroup("Commands")]
        [LabelText("Command Program")]
        [InlineProperty]
        [HideLabel]
        public WorldPointerTargetCommandMutationProgram EventCommandProgram = new();

        static int Count(WorldPointerTargetCommandMutationProgram? program) => program?.Steps?.Count ?? 0;
    }
}