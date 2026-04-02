#nullable enable
using System;
using Game.Common;
using Game.UI;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    [Serializable]
    public sealed class BindTraitListChannelCommandData : ICommandData
    {
        public int CommandId => CommandIds.BindTraitListChannel;
        public string DebugData => $"Tag={ChannelTag} Rebuild={Rebuild}";

        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(Target)")]
        [Tooltip("TraitListChannelHubMB を持つ対象スコープ。ここから ChannelTag に一致する channel を解決します。")]
        public ActorSource Target = new() { Kind = ActorSourceKind.Current };

        [Tooltip("操作対象の TraitList channel tag。TraitListChannelHubMB 内の Channel Tag と一致させます。")]
        public string ChannelTag = "default";

        [Tooltip("true のとき bind 後に即座に build/refresh を実行します。false の場合は binding だけ保持します。")]
        public bool Rebuild = true;

        [InlineProperty]
        [HideLabel]
        [Tooltip("holder source/key、range、preset override をまとめた bind 設定です。")]
        public TraitListChannelBindRequest Request = new();
    }

    [Serializable]
    public sealed class RefreshTraitListChannelCommandData : ICommandData
    {
        public int CommandId => CommandIds.RefreshTraitListChannel;
        public string DebugData => $"Tag={ChannelTag} Mode={RefreshMode}";

        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(Target)")]
        [Tooltip("TraitListChannelHubMB を持つ対象スコープ。ここから ChannelTag に一致する channel を解決します。")]
        public ActorSource Target = new() { Kind = ActorSourceKind.Current };

        [Tooltip("更新対象の TraitList channel tag。TraitListChannelHubMB 内の Channel Tag と一致させます。")]
        public string ChannelTag = "default";

        [Tooltip("FullRebuild は全再生成、Incremental は差分更新、LayoutOnly は配置更新のみを行います。")]
        public TraitListChannelRefreshMode RefreshMode = TraitListChannelRefreshMode.Incremental;
    }

    [Serializable]
    public sealed class SetTraitListChannelRangeCommandData : ICommandData
    {
        public int CommandId => CommandIds.SetTraitListChannelRange;
        public string DebugData => $"Tag={ChannelTag} UseRange={UseRange} Start={Range.StartIndex} Count={Range.Count} Rebuild={Rebuild}";

        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(Target)")]
        [Tooltip("TraitListChannelHubMB を持つ対象スコープ。ここから ChannelTag に一致する channel を解決します。")]
        public ActorSource Target = new() { Kind = ActorSourceKind.Current };

        [Tooltip("範囲を更新する対象の TraitList channel tag。TraitListChannelHubMB 内の Channel Tag と一致させます。")]
        public string ChannelTag = "default";

        [Tooltip("true のとき StartIndex / Count を使った範囲表示を有効にします。false のとき先頭から layout capacity 分を表示します。")]
        public bool UseRange = true;

        [ShowIf(nameof(UseRange))]
        [InlineProperty]
        [Tooltip("表示する trait の開始位置と件数です。UseRange が true のときだけ使われます。")]
        public TraitListChannelRange Range;

        [Tooltip("true のとき範囲更新後に即座に再配置/再生成を行います。")]
        public bool Rebuild = true;
    }

    [Serializable]
    public sealed class ClearTraitListChannelCommandData : ICommandData
    {
        public int CommandId => CommandIds.ClearTraitListChannel;
        public string DebugData => $"Tag={ChannelTag} KeepBinding={KeepBinding}";

        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(Target)")]
        [Tooltip("TraitListChannelHubMB を持つ対象スコープ。ここから ChannelTag に一致する channel を解決します。")]
        public ActorSource Target = new() { Kind = ActorSourceKind.Current };

        [Tooltip("clear 対象の TraitList channel tag。TraitListChannelHubMB 内の Channel Tag と一致させます。")]
        public string ChannelTag = "default";

        [Tooltip("true のとき current binding を保持したまま生成済み要素だけを破棄します。")]
        public bool KeepBinding;
    }
}
