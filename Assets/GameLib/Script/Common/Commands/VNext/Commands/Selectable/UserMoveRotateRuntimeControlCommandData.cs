#nullable enable
using System;
using Sirenix.OdinInspector;

namespace Game.Commands.VNext
{
    public enum UserMoveRotateRuntimeControlMode
    {
        Enter = 10,
        Exit = 20,
        Toggle = 30,
    }

    [Serializable]
    public sealed class UserMoveRotateRuntimeControlCommandData : ICommandData
    {
        public int CommandId => CommandIds.UserMoveRotateRuntimeControl;

        public string DebugData
        {
            get
            {
                var targetLabel = ActorSourceOdinLabelHelper.GetLabel("Target", Target);
                return $"{targetLabel} Mode={Mode} RunExit={RunExitCommands}";
            }
        }

        [BoxGroup("Target")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(Target)")]
        public ActorSource Target;

        [BoxGroup("Control")]
        [EnumToggleButtons]
        [LabelText("Mode")]
        public UserMoveRotateRuntimeControlMode Mode = UserMoveRotateRuntimeControlMode.Toggle;

        [BoxGroup("Control")]
        [ShowIf("@Mode != Game.Commands.VNext.UserMoveRotateRuntimeControlMode.Enter")]
        [LabelText("Run Exit Commands")]
        public bool RunExitCommands = true;
    }
}
