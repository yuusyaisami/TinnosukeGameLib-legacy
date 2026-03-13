#nullable enable
using System;
using Game.Common;
using Sirenix.OdinInspector;

namespace Game.Commands.VNext
{
    [Serializable]
    public sealed class PublishEventCommandData : ICommandData
    {
        public int CommandId => CommandIds.PublishEvent;
        public string DebugData
        {
            get
            {
                var eventKey = string.IsNullOrEmpty(EventKey) ? "<none>" : EventKey;
                var scopeLabel = ActorSourceOdinLabelHelper.GetLabel("Scope", EventScope);
                return $"Event={eventKey} {scopeLabel}";
            }
        }

        [BoxGroup("Target")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(EventScope)")]
        public ActorSource EventScope;

        [BoxGroup("Target")]
        [LabelText("Event Key"), EventKeyDropdown]
        public string EventKey = string.Empty;

        [BoxGroup("Run")]
        [LabelText("Await Mode")]
        public FlowRunAwaitMode AwaitMode = FlowRunAwaitMode.WaitForCompletion;

        [BoxGroup("Payload")]
        [LabelText("Use Context Vars")]
        public bool UseContextVars = true;

        [BoxGroup("Payload")]
        [LabelText("Overwrite Existing Vars")]
        public bool OverwriteExistingVars = true;

        [BoxGroup("Payload")]
        [InlineProperty]
        [HideLabel]
        public VarStorePayload Payload = new();
    }
}
