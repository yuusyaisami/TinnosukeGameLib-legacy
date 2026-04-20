#nullable enable
using System;
using Game.Channel;
using Game.Common;
using Game.Vars.Generated;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    [Serializable]
    public sealed class BindGridObjectChannelCommandData : ICommandData
    {
        public int CommandId => CommandIds.BindGridObjectChannel;
        public string DebugData => $"Tag={ChannelTag} Rebuild={Rebuild}";

        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(Target)")]
        [Tooltip("Inspector setting.")]
        public ActorSource Target = new() { Kind = ActorSourceKind.Current };

        [Tooltip("Inspector setting.")]
        public string ChannelTag = "default";

        [Tooltip("Inspector setting.")]
        public bool Rebuild = true;

        [InlineProperty]
        [HideLabel]
        [Tooltip("Inspector setting.")]
        public GridObjectChannelBindRequest Request = new();
    }

    [Serializable]
    public sealed class RefreshGridObjectChannelCommandData : ICommandData
    {
        public int CommandId => CommandIds.RefreshGridObjectChannel;
        public string DebugData => $"Tag={ChannelTag} Mode={RefreshMode}";

        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(Target)")]
        [Tooltip("Inspector setting.")]
        public ActorSource Target = new() { Kind = ActorSourceKind.Current };

        [Tooltip("Inspector setting.")]
        public string ChannelTag = "default";

        [Tooltip("Inspector setting.")]
        public GridObjectChannelRefreshMode RefreshMode = GridObjectChannelRefreshMode.Incremental;
    }

    [Serializable]
    public sealed class ClearGridObjectChannelCommandData : ICommandData
    {
        public int CommandId => CommandIds.ClearGridObjectChannel;
        public string DebugData => $"Tag={ChannelTag} KeepBinding={KeepBinding}";

        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(Target)")]
        [Tooltip("Inspector setting.")]
        public ActorSource Target = new() { Kind = ActorSourceKind.Current };

        [Tooltip("Inspector setting.")]
        public string ChannelTag = "default";

        [Tooltip("Inspector setting.")]
        public bool KeepBinding;
    }

    [Serializable]
    public sealed class ShowGridObjectChoiceAndWaitCommandData : ICommandData
    {
        public int CommandId => CommandIds.ShowGridObjectChoiceAndWait;
        public string DebugData => $"Tag={ChannelTag} Entries={Request.Entries.Count}";

        [BoxGroup("Target")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(Target)")]
        [Tooltip("Inspector setting.")]
        public ActorSource Target = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Target")]
        [LabelText("Channel Tag")]
        [Tooltip("Inspector setting.")]
        public string ChannelTag = "default";

        [BoxGroup("Choice")]
        [LabelText("Request")]
        [InlineProperty]
        [Tooltip("Inspector setting.")]
        public GridObjectChoiceRequest Request = new();

        [BoxGroup("Result")]
        [LabelText("Write SelectedIndex")]
        [Tooltip("Inspector setting.")]
        public bool WriteSelectedIndexToVars = true;

        [BoxGroup("Result")]
        [ShowIf(nameof(WriteSelectedIndexToVars))]
        [LabelText("SelectedIndex Var")]
        [Tooltip("Inspector setting.")]
        public VarKeyRef SelectedIndexVar = new(VarIds.GameLib.Base.CommandVar.i, "i");

        [BoxGroup("Canceled")]
        [LabelText("Treat Replaced As Canceled")]
        [Tooltip("Inspector setting.")]
        public bool TreatReplacedAsCanceled = true;

        [BoxGroup("Canceled")]
        [LabelText("On Canceled Commands")]
        [CommandListFunctionName("GridObjectChannel.Choice.OnCanceled")]
        [Tooltip("Inspector setting.")]
        public CommandListData OnCanceledCommands = new();

        [BoxGroup("Timeout")]
        [LabelText("On Timeout Commands")]
        [CommandListFunctionName("GridObjectChannel.Choice.OnTimeout")]
        [Tooltip("Inspector setting.")]
        public CommandListData OnTimeoutCommands = new();
    }
}
