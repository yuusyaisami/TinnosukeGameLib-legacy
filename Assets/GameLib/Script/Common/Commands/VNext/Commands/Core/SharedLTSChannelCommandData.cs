#nullable enable
using System;
using Sirenix.OdinInspector;

namespace Game.Commands.VNext
{
    public enum SharedLTSChannelOperation
    {
        Register = 10,
        Get = 15,
        Unregister = 20,
        ClearAll = 30,
    }

    [Serializable]
    public sealed class SharedLTSChannelCommandData : ICommandData
    {
        public int CommandId => CommandIds.SharedLTSChannel;

        public string DebugData
        {
            get
            {
                var baseText = $"Hub={HubSource.Kind} Op={Operation} Tag={Tag}";
                if (Operation == SharedLTSChannelOperation.Register)
                    return $"{baseText} Actor={ActorSource.Kind}";

                if (Operation == SharedLTSChannelOperation.Get)
                    return $"{baseText} Slot={ContextSlot}";

                return baseText;
            }
        }

        [LabelText("Operation")]
        [EnumToggleButtons]
        public SharedLTSChannelOperation Operation = SharedLTSChannelOperation.Register;

        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Hub Source\", HubSource)")]
        public ActorSource HubSource = new() { Kind = ActorSourceKind.Current };

        [LabelText("Tag")]
        public string Tag = string.Empty;

        [ShowIf(nameof(UsesContextSlot))]
        [LabelText("Context Slot")]
        public CommandLtsSlot ContextSlot = CommandLtsSlot.ContextA;

        [ShowIf(nameof(UsesActorSource))]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(ActorSource)")]
        public ActorSource ActorSource;

        bool UsesActorSource => Operation == SharedLTSChannelOperation.Register;
        bool UsesContextSlot => Operation == SharedLTSChannelOperation.Get;
    }
}
