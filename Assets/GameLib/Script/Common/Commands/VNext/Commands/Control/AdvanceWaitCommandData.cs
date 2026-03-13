#nullable enable
using System;
using System.Collections.Generic;
using Game.Common;
using Sirenix.OdinInspector;

namespace Game.Commands.VNext
{
    [Serializable]
    public sealed class AdvanceWaitEventEntry
    {
        [BoxGroup("Event")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(EventScope)")]
        public ActorSource EventScope;

        [BoxGroup("Event")]
        [LabelText("Event Key"), EventKeyDropdown]
        public string EventKey = string.Empty;

        [FoldoutGroup("Commands")]
        [HideLabel]
        [CommandListFunctionName("Control.AdvanceWait.Event")]
        public CommandListData Commands = new();
    }

    [Serializable]
    public sealed class AdvanceWaitCommandData : ICommandData
    {
        public int CommandId => CommandIds.AdvanceWait;
        public string DebugData
        {
            get
            {
                var condition = CommandDebugDataHelper.GetDynamicDebugData(Condition);
                var eventCount = Events?.Count ?? 0;
                return $"Cond={condition} Events={eventCount}";
            }
        }

        [BoxGroup("Condition")]
        [HideLabel]
        public DynamicValue<bool> Condition;

        [BoxGroup("Events")]
        [ListDrawerSettings(ShowFoldout = true, DraggableItems = false)]
        public List<AdvanceWaitEventEntry> Events = new();
    }
}
