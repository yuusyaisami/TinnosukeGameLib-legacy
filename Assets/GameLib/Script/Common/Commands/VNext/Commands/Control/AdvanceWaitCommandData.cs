#nullable enable
using System;
using System.Collections.Generic;
using Game.Common;
using Sirenix.OdinInspector;

namespace Game.Commands.VNext
{
    public enum AdvanceWaitMode
    {
        ConditionOnly = 10,
        EventOnly = 20,
        ConditionAndEvent = 30,
    }

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
                var mode = WaitMode.ToString();
                var condition = CommandDebugDataHelper.GetDynamicDebugData(Condition);
                var eventCount = Events?.Count ?? 0;
                return $"Mode={mode} Cond={condition} Events={eventCount}";
            }
        }

        bool ShouldShowCondition => WaitMode != AdvanceWaitMode.EventOnly;
        bool ShouldShowEvents => WaitMode != AdvanceWaitMode.ConditionOnly;

        [BoxGroup("Mode")]
        [LabelText("Wait Mode")]
        [EnumToggleButtons]
        public AdvanceWaitMode WaitMode = AdvanceWaitMode.ConditionAndEvent;

        [BoxGroup("Condition")]
        [ShowIf(nameof(ShouldShowCondition))]
        [HideLabel]
        public DynamicValue<bool> Condition;

        [BoxGroup("Events")]
        [ShowIf(nameof(ShouldShowEvents))]
        [ListDrawerSettings(ShowFoldout = true, DraggableItems = false)]
        public List<AdvanceWaitEventEntry> Events = new();
    }
}
