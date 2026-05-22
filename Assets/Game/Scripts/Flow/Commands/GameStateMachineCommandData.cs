#nullable enable
using System;
using Game.Actions;
using Sirenix.OdinInspector;

namespace Game.Commands.VNext
{
    [Serializable]
    public sealed class ChangeGameStateCommandData : ICommandData
    {
        public int CommandId => CommandIds.ChangeGameState;
        public string DebugData => $"{ActorSourceOdinLabelHelper.GetLabel("State Machine Source", StateMachineSource)} State={State}";

        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"State Machine Source\", StateMachineSource)")]
        public ActorSource StateMachineSource = new() { Kind = ActorSourceKind.GameLogicRoot };

        [LabelText("State")]
        public GameState State;
    }
}
