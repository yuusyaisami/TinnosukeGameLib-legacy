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
        [Tooltip("Inspector setting.")]
        public ActorSource Target = new() { Kind = ActorSourceKind.Current };

        [Tooltip("Inspector setting.")]
        public string ChannelTag = "default";

        [Tooltip("Inspector setting.")]
        public bool Rebuild = true;

        [InlineProperty]
        [HideLabel]
        [Tooltip("Inspector setting.")]
        public TraitListChannelBindRequest Request = new();
    }

    [Serializable]
    public sealed class RefreshTraitListChannelCommandData : ICommandData
    {
        public int CommandId => CommandIds.RefreshTraitListChannel;
        public string DebugData => $"Tag={ChannelTag} Mode={RefreshMode}";

        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(Target)")]
        [Tooltip("Inspector setting.")]
        public ActorSource Target = new() { Kind = ActorSourceKind.Current };

        [Tooltip("Inspector setting.")]
        public string ChannelTag = "default";

        [Tooltip("Inspector setting.")]
        public TraitListChannelRefreshMode RefreshMode = TraitListChannelRefreshMode.Incremental;
    }

    [Serializable]
    public sealed class SetTraitListChannelRangeCommandData : ICommandData
    {
        public int CommandId => CommandIds.SetTraitListChannelRange;
        public string DebugData => $"Tag={ChannelTag} UseRange={UseRange} Start={Range.StartIndex} Count={Range.Count} Rebuild={Rebuild}";

        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(Target)")]
        [Tooltip("Inspector setting.")]
        public ActorSource Target = new() { Kind = ActorSourceKind.Current };

        [Tooltip("Inspector setting.")]
        public string ChannelTag = "default";

        [Tooltip("Inspector setting.")]
        public bool UseRange = true;

        [ShowIf(nameof(UseRange))]
        [InlineProperty]
        [Tooltip("Inspector setting.")]
        public TraitListChannelRange Range;

        [Tooltip("Inspector setting.")]
        public bool Rebuild = true;
    }

    [Serializable]
    public sealed class ClearTraitListChannelCommandData : ICommandData
    {
        public int CommandId => CommandIds.ClearTraitListChannel;
        public string DebugData => $"Tag={ChannelTag} KeepBinding={KeepBinding}";

        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(Target)")]
        [Tooltip("Inspector setting.")]
        public ActorSource Target = new() { Kind = ActorSourceKind.Current };

        [Tooltip("Inspector setting.")]
        public string ChannelTag = "default";

        [Tooltip("Inspector setting.")]
        public bool KeepBinding;
    }
}
