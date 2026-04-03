#nullable enable
using System;
using Game.Channel;
using Game.Common;
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
}
