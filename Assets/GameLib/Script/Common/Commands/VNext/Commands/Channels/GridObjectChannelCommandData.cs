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
        [Tooltip("GridObjectChannelHubMB を持つ対象スコープ。ここから ChannelTag に一致する channel を解決します。")]
        public ActorSource Target = new() { Kind = ActorSourceKind.Current };

        [Tooltip("操作対象の GridObjectChannel tag。GridObjectChannelHubMB 内の Channel Tag と一致させます。")]
        public string ChannelTag = "default";

        [Tooltip("true のとき bind 後に即座に build/refresh を実行します。false の場合は binding だけ保持します。")]
        public bool Rebuild = true;

        [InlineProperty]
        [HideLabel]
        [Tooltip("player/layout/visualizer preset override をまとめた bind 設定です。")]
        public GridObjectChannelBindRequest Request = new();
    }

    [Serializable]
    public sealed class RefreshGridObjectChannelCommandData : ICommandData
    {
        public int CommandId => CommandIds.RefreshGridObjectChannel;
        public string DebugData => $"Tag={ChannelTag} Mode={RefreshMode}";

        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(Target)")]
        [Tooltip("GridObjectChannelHubMB を持つ対象スコープ。ここから ChannelTag に一致する channel を解決します。")]
        public ActorSource Target = new() { Kind = ActorSourceKind.Current };

        [Tooltip("更新対象の GridObjectChannel tag。GridObjectChannelHubMB 内の Channel Tag と一致させます。")]
        public string ChannelTag = "default";

        [Tooltip("FullRebuild は全再生成、Incremental は差分更新、LayoutOnly は配置更新のみを行います。")]
        public GridObjectChannelRefreshMode RefreshMode = GridObjectChannelRefreshMode.Incremental;
    }

    [Serializable]
    public sealed class ClearGridObjectChannelCommandData : ICommandData
    {
        public int CommandId => CommandIds.ClearGridObjectChannel;
        public string DebugData => $"Tag={ChannelTag} KeepBinding={KeepBinding}";

        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(Target)")]
        [Tooltip("GridObjectChannelHubMB を持つ対象スコープ。ここから ChannelTag に一致する channel を解決します。")]
        public ActorSource Target = new() { Kind = ActorSourceKind.Current };

        [Tooltip("clear 対象の GridObjectChannel tag。GridObjectChannelHubMB 内の Channel Tag と一致させます。")]
        public string ChannelTag = "default";

        [Tooltip("true のとき current binding を保持したまま生成済み要素だけを破棄します。")]
        public bool KeepBinding;
    }

    [Serializable]
    public sealed class ShowGridObjectChoiceAndWaitCommandData : ICommandData
    {
        public int CommandId => CommandIds.ShowGridObjectChoiceAndWait;
        public string DebugData => $"Tag={ChannelTag} Entries={Request.Entries.Count}";

        [BoxGroup("Target")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(Target)")]
        [Tooltip("GridObjectChannelHubMB を持つ対象スコープ。ここから ChannelTag に一致する channel を解決します。")]
        public ActorSource Target = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Target")]
        [LabelText("Channel Tag")]
        [Tooltip("操作対象の GridObjectChannel tag。GridObjectChannelHubMB 内の Channel Tag と一致させます。")]
        public string ChannelTag = "default";

        [BoxGroup("Choice")]
        [LabelText("Request")]
        [InlineProperty]
        [Tooltip("表示する entry / bind override / wait option をまとめた choice request です。")]
        public GridObjectChoiceRequest Request = new();

        [BoxGroup("Result")]
        [LabelText("Write SelectedIndex")]
        [Tooltip("true のとき selected index を SelectedIndexVar へ書き込みます。")]
        public bool WriteSelectedIndexToVars = true;

        [BoxGroup("Result")]
        [ShowIf(nameof(WriteSelectedIndexToVars))]
        [LabelText("SelectedIndex Var")]
        [Tooltip("選択確定時に selected index を書き込む var です。")]
        public VarKeyRef SelectedIndexVar = new(VarIds.GameLib.Base.CommandVar.i, "i");

        [BoxGroup("Canceled")]
        [LabelText("Treat Replaced As Canceled")]
        [Tooltip("true のとき ConcurrencyPolicy=CancelAndReplace で置換された結果を canceled 分岐として扱います。")]
        public bool TreatReplacedAsCanceled = true;

        [BoxGroup("Canceled")]
        [LabelText("On Canceled Commands")]
        [CommandListFunctionName("GridObjectChannel.Choice.OnCanceled")]
        [Tooltip("cancel 完了時に実行する command list です。成功扱いで続行します。")]
        public CommandListData OnCanceledCommands = new();

        [BoxGroup("Timeout")]
        [LabelText("On Timeout Commands")]
        [CommandListFunctionName("GridObjectChannel.Choice.OnTimeout")]
        [Tooltip("timeout 完了時に実行する command list です。成功扱いで続行します。")]
        public CommandListData OnTimeoutCommands = new();
    }
}
