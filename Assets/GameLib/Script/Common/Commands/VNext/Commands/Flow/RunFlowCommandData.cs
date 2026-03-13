#nullable enable
using System;
using Game.Flow;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    public enum FlowRunAwaitMode
    {
        WaitForCompletion = 0,
        RunInBackground = 1,
    }

    [Serializable]
    public sealed class RunFlowCommandData : ICommandData
    {
        public int CommandId => CommandIds.RunFlow;
        public string DebugData
        {
            get
            {
                var programName = Program != null ? Program.name : "null";
                var entry = string.IsNullOrEmpty(EntryFunctionName) ? "<none>" : EntryFunctionName;
                return $"Program={programName} Entry={entry}";
            }
        }

        [BoxGroup("Flow")]
        [LabelText("Program")]
        [AssetOrInternal]
        public FlowProgramAssetSO? Program;

        [BoxGroup("Flow")]
        [LabelText("Entry")]
        public string EntryFunctionName = "Main";

        [BoxGroup("Execution")]
        [LabelText("Await Mode")]
        public FlowRunAwaitMode AwaitMode = FlowRunAwaitMode.WaitForCompletion;
    }
}
