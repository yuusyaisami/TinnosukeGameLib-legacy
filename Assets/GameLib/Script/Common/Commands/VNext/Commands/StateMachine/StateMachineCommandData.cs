#nullable enable
using System;
using Game.StateMachine.Editor;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    public enum StateMachineAction
    {
        SetState = 0,
        ReleaseState = 1,
        FirePulse = 2,
        SetGlobalOption = 3,
        SetLocalOption = 4,
        ReleaseGlobalOption = 5,
        ReleaseLocalOption = 6,
    }

    [Serializable]
    public sealed class StateMachineCommandData : ICommandData
    {
        public int CommandId => CommandIds.StateMachine;
        public string DebugData
        {
            get
            {
                var key = string.IsNullOrEmpty(StateKey) ? "<none>" : StateKey;
                var option = string.IsNullOrEmpty(OptionValue) ? "<none>" : OptionValue;
                var tag = string.IsNullOrEmpty(Tag) ? "<none>" : Tag;
                var layer = string.IsNullOrEmpty(LocalOptionLayerKey) ? "<none>" : LocalOptionLayerKey;
                return $"Action={Action} State={key} Tag={tag} Option={option} Layer={layer}";
            }
        }

        [BoxGroup("General")]
        [LabelText("Action")]
        [SerializeField]
        public StateMachineAction Action = StateMachineAction.SetState;

        [BoxGroup("Target")]
        [LabelText("State Key")]
        [SerializeField]
        [StateKeyPicker]
        public string? StateKey;

        [BoxGroup("Target")]
        [LabelText("Tag")]
        [SerializeField]
        public string Tag = "command";

        [ShowIf("@Action == StateMachineAction.SetState")]
        [BoxGroup("Set")]
        [LabelText("Owner Id")]
        [SerializeField]
        public string OwnerId = "Command";


        [ShowIf("@Action == StateMachineAction.FirePulse")]
        [BoxGroup("Pulse")]
        [LabelText("Required Tag (optional)")]
        [SerializeField]
        [OptionKeyPicker]
        public string? RequiredTag;

        [ShowIf("@Action == StateMachineAction.SetGlobalOption || Action == StateMachineAction.SetLocalOption")]
        [BoxGroup("Option Set")]
        [LabelText("Option Value")]
        [Tooltip("設定する OptionValue (例: Movement.Direction.Left)")]
        [SerializeField]
        [OptionKeyPicker]
        public string? OptionValue;

        [ShowIf("@Action == StateMachineAction.SetLocalOption || Action == StateMachineAction.ReleaseLocalOption")]
        [BoxGroup("Option Local")]
        [LabelText("Layer Key")]
        [Tooltip("対象 LayerKey。空の場合は CurrentLayer を使用。")]
        [SerializeField]
        public string? LocalOptionLayerKey;

        [ShowIf("@Action == StateMachineAction.ReleaseGlobalOption || Action == StateMachineAction.ReleaseLocalOption")]
        [BoxGroup("Option Release")]
        [LabelText("Option Key")]
        [Tooltip("解除する OptionKey (例: Movement.Direction)")]
        [SerializeField]
        [OptionKeyPicker]
        public string? OptionKey;
    }
}
