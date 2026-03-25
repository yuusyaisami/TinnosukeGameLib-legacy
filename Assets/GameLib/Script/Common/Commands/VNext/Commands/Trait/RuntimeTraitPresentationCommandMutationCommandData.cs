#nullable enable
using System;
using Game.Common;
using Game.Trait;
using Sirenix.OdinInspector;

namespace Game.Commands.VNext
{
    [Serializable]
    public sealed class RuntimeTraitPresentationCommandMutationCommandData : ICommandData
    {
        public int CommandId => CommandIds.RuntimeTraitPresentationCommandMutation;

        public string DebugData
            => $"Target={TargetActorSource.Kind} Stream={TargetStream} Op={Mutation.Operation}";

        [BoxGroup("Target")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(TargetActorSource)")]
        [PropertyTooltip("RuntimeTraitMB を持つ RuntimeScope を指定します。")]
        public ActorSource TargetActorSource;

        [BoxGroup("Target")]
        [LabelText("Stream")]
        [EnumToggleButtons]
        public RuntimeTraitPresentationCommandTarget TargetStream = RuntimeTraitPresentationCommandTarget.Both;

        [BoxGroup("Mutation")]
        [LabelText("Mutation")]
        [InlineProperty]
        [HideLabel]
        public CommandListMutationStep Mutation = new()
        {
            Operation = CommandListMutationOperation.Append,
        };

    }
}
