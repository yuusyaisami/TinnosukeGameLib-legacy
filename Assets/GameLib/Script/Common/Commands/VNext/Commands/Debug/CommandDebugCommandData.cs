#nullable enable
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Game;
using Game.Common;
namespace Game.Commands.VNext
{
    [Serializable]
    public sealed class CommandDebugCommandData : ICommandData
    {
        public int CommandId => CommandIds.DebugCommandContext;
        public string DebugData
        {
            get
            {
                var label = string.IsNullOrEmpty(Label) ? "<none>" : Label;
                var watchCount = Watches?.Count ?? 0;
                return $"Label={label} Watches={watchCount}";
            }
        }

        [BoxGroup("Description")]
        [LabelText("Label")]
        public string Label = "CommandDebug";

        [BoxGroup("Description")]
        [LabelText("Message")]
        [TextArea]
        public string Message = string.Empty;

        [BoxGroup("Output")]
        [LabelText("Log Scope Info")]
        public bool LogScopeInfo = true;

        [BoxGroup("Output")]
        [LabelText("Log Runner Info")]
        public bool LogRunnerInfo = true;

        [BoxGroup("Output")]
        [LabelText("Log Context Slots")]
        public bool LogContextSlots = true;

        [BoxGroup("Output")]
        [LabelText("Log Options")]
        public bool LogOptions = true;

        [BoxGroup("Output")]
        [LabelText("Log VarStore")]
        public bool LogVarStore = true;

        [BoxGroup("Output")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(TargetScope)")]
        public ActorSource TargetScope = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Output")]
        [LabelText("Log Target Scope Info")]
        public bool LogTargetScopeInfo = true;

        [BoxGroup("Output")]
        [LabelText("Log Target Blackboard")]
        public bool LogTargetBlackboard = true;

        [BoxGroup("Output")]
        [LabelText("Max Blackboard Entries")]
        [MinValue(0)]
        public int MaxBlackboardEntries = 0;

        [BoxGroup("Output")]
        [LabelText("Log Target Grid Blackboard")]
        public bool LogTargetGridBlackboard = true;

        [BoxGroup("Output")]
        [LabelText("Max Grid Blackboard Entries")]
        [MinValue(0)]
        public int MaxGridBlackboardEntries = 0;

        [BoxGroup("Output")]
        [LabelText("Log Target Scalar")]
        public bool LogTargetScalar = true;

        [BoxGroup("Output")]
        [LabelText("Max Scalar Keys")]
        [MinValue(0)]
        public int MaxScalarEntries = 0;

        [BoxGroup("Output")]
        [LabelText("Log Scalar Snapshots")]
        public bool LogScalarSnapshots = true;

        [BoxGroup("Output")]
        [LabelText("Max Scalar Snapshots / Key")]
        [MinValue(0)]
        public int MaxScalarSnapshotsPerKey = 0;

        [BoxGroup("Output")]
        [LabelText("Max Var Entries")]
        [MinValue(0)]
        public int MaxVarEntries = 64;

        [BoxGroup("Watches")]
        [LabelText("Log Watches")]
        public bool LogWatches = true;

        [BoxGroup("Watches")]
        [LabelText("Max Watch Entries")]
        [MinValue(0)]
        public int MaxWatchEntries = 16;

        [BoxGroup("Watches")]
        [LabelText("Watches")]
        [ListDrawerSettings(ShowFoldout = true, DraggableItems = true)]
        public List<CommandDebugWatchEntry> Watches = new();
    }

    [Serializable]
    public sealed class CommandDebugWatchEntry
    {
        [LabelText("Label")]
        public string Label = string.Empty;

        [LabelText("Value")]
        [HideLabel]
        public DynamicValue Value;

        [LabelText("Include Source Info")]
        public bool IncludeSourceInfo = true;
    }
}
