#nullable enable
using System;
using Game.Common;
using Sirenix.OdinInspector;

namespace Game.Commands.VNext
{
    public enum CommandChannelControlOperation
    {
        RegisterOrUpdate = 10,
        Unregister = 20,
        ClearAll = 30,
        MutateCommands = 40,
        SetPayload = 50,
        ClearPayload = 60,
    }

    [Serializable]
    public sealed class CommandChannelControlCommandData : ICommandData
    {
        public int CommandId => CommandIds.CommandChannelControl;

        public string DebugData
            => $"Owner={ActorSource.Kind} Op={Operation} Tag={Tag}";

        [BoxGroup("Target")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(ActorSource)")]
        [PropertyTooltip("CommandChannelHub を所有するスコープ。")]
        public ActorSource ActorSource;

        [BoxGroup("Target")]
        [LabelText("Operation")]
        [EnumToggleButtons]
        public CommandChannelControlOperation Operation = CommandChannelControlOperation.MutateCommands;

        [BoxGroup("Target")]
        [ShowIf(nameof(UsesTag))]
        [LabelText("Tag")]
        public string Tag = string.Empty;

        [BoxGroup("Register")]
        [ShowIf(nameof(UsesRegisterCommands))]
        [LabelText("Commands")]
        [InlineProperty]
        [HideLabel]
        public DynamicValue<CommandListData> RegisterCommands = DynamicValueExtensions.FromLiteral(new CommandListData());

        [BoxGroup("Mutate")]
        [ShowIf(nameof(UsesMutation))]
        [LabelText("Mutation")]
        [InlineProperty]
        [HideLabel]
        public CommandListMutationStep Mutation = new()
        {
            Operation = CommandListMutationOperation.Append,
        };

        [BoxGroup("Mutate")]
        [ShowIf(nameof(UsesMutationCommandsSource))]
        [LabelText("Commands Source")]
        [InlineProperty]
        [HideLabel]
        public DynamicValue<CommandListData> MutationCommands = DynamicValueExtensions.FromLiteral(new CommandListData());

        [BoxGroup("Payload")]
        [ShowIf(nameof(UsesPayload))]
        [LabelText("Overwrite Existing Vars")]
        public bool PayloadOverwriteExistingVars = true;

        [BoxGroup("Payload")]
        [ShowIf(nameof(UsesPayload))]
        [LabelText("Payload")]
        [InlineProperty]
        [HideLabel]
        public VarStorePayload Payload = new();

        bool UsesTag => Operation != CommandChannelControlOperation.ClearAll;
        bool UsesRegisterCommands => Operation == CommandChannelControlOperation.RegisterOrUpdate;
        bool UsesMutation => Operation == CommandChannelControlOperation.MutateCommands;
        bool UsesMutationCommandsSource => UsesMutation && Mutation.RequiresCommands();
        bool UsesPayload => Operation == CommandChannelControlOperation.SetPayload;
    }
}
