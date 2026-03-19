#nullable enable
using System;
using Sirenix.OdinInspector;

namespace Game.Commands.VNext
{
    public enum SharedLTSChannelOperation
    {
        Register = 10,
        Unregister = 20,
        ClearAll = 30,
    }

    [Serializable]
    public sealed class SharedLTSChannelCommandData : ICommandData
    {
        public int CommandId => CommandIds.SharedLTSChannel;
        public string DebugData => $"Op={Operation} Tag={Tag} Actor={ActorSource.Kind}";

        [LabelText("Operation")]
        [EnumToggleButtons]
        public SharedLTSChannelOperation Operation = SharedLTSChannelOperation.Register;

        [LabelText("Tag")]
        public string Tag = string.Empty;

        [ShowIf(nameof(UsesActorSource))]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(ActorSource)")]
        public ActorSource ActorSource;

        bool UsesActorSource => Operation == SharedLTSChannelOperation.Register;
    }
}
