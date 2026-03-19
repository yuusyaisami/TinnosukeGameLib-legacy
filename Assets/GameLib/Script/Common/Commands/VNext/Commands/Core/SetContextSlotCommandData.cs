#nullable enable

using System;
using Sirenix.OdinInspector;

namespace Game.Commands.VNext
{
    [Serializable]
    public sealed class SetContextSlotCommandData : ICommandData
    {
        public int CommandId => CommandIds.SetContextSlot;
        public string DebugData => $"Slot={Slot} Actor={ActorSource.Kind}";

        [LabelText("Context Slot")]
        public CommandLtsSlot Slot = CommandLtsSlot.ContextA;

        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(ActorSource)")]
        public ActorSource ActorSource = new() { Kind = ActorSourceKind.Current };
    }
}
