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
        public string DebugData => $"State={State}";

        [LabelText("State")]
        public GameState State;
    }
}
