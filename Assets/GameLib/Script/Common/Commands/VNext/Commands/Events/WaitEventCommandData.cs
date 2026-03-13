#nullable enable
using System;
using System.Collections.Generic;
using Game.Common;
using Sirenix.OdinInspector;

namespace Game.Commands.VNext
{
    [Serializable]
    public sealed class WaitEventCommandData : ICommandData
    {
        public int CommandId => CommandIds.WaitEvent;
        public string DebugData
        {
            get
            {
                var eventKey = string.IsNullOrEmpty(EventKey) ? "<none>" : EventKey;
                var scopeLabel = ActorSourceOdinLabelHelper.GetLabel("Scope", EventScope);
                return $"Wait={eventKey} {scopeLabel}";
            }
        }

        [BoxGroup("Target")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(EventScope)")]
        public ActorSource EventScope;

        [BoxGroup("Target")]
        [LabelText("Event Key"), EventKeyDropdown]
        public string EventKey = string.Empty;

        [BoxGroup("Capture")]
        [LabelText("Capture Payload")]
        public bool CapturePayload = false;

        [BoxGroup("Capture")]
        [ShowIf(nameof(CapturePayload))]
        [ListDrawerSettings(ShowFoldout = true)]
        public List<VarCaptureMap> CaptureMaps = new();
    }

    [Serializable]
    public sealed class VarCaptureMap
    {
        [HorizontalGroup]
        [LabelText("From (Payload Key)")]
        public string SourceKey = string.Empty;

        [HorizontalGroup]
        [LabelText("To (Context Key)")]
        public string TargetKey = string.Empty;
    }
}
