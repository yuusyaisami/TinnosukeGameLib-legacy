#nullable enable
using System;
using Game.Common;
using Sirenix.OdinInspector;

namespace Game.Commands.VNext
{
    [Serializable]
    public sealed class UnityRoomSendScoreCommandData : ICommandData
    {
        public int CommandId => CommandIds.UnityRoomSendScore;
        public string DebugData => $"Score={CommandDebugDataHelper.GetDynamicDebugData(Score, "0")}";

        [BoxGroup("Scoreboard")]
        [LabelText("Score")]
        [InfoBox("The evaluated float is rounded with Mathf.RoundToInt before send.")]
        public DynamicValue<float> Score = DynamicValueExtensions.FromLiteral(0f);
    }
}
