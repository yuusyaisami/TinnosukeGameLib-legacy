#nullable enable
using System;
using Game.Common;
using Sirenix.OdinInspector;

namespace Game.Commands.VNext
{
    public enum WithTargetChannelOrder
    {
        NearFirst = 10,
        FarFirst = 20,
    }

    [Serializable]
    public sealed class WithTargetChannelCommandData : ICommandData
    {
        public int CommandId => CommandIds.WithTargetChannel;

        public string DebugData
        {
            get
            {
                var bodyCount = Body?.Count ?? 0;
                var ownerLabel = ActorSourceOdinLabelHelper.GetLabel("Channel Owner", ChannelOwnerSource);
                return $"{ownerLabel} Tag='{NormalizedChannelTag}' Order={Order} Max={MaxTargets} Await={AwaitMode} Body={bodyCount}";
            }
        }

        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Channel Owner\", ChannelOwnerSource)")]
        public ActorSource ChannelOwnerSource = new() { Kind = ActorSourceKind.Current };

        [LabelText("Channel Tag")]
        public string ChannelTag = "default";

        [LabelText("Target Order")]
        [EnumToggleButtons]
        public WithTargetChannelOrder Order = WithTargetChannelOrder.NearFirst;

        [LabelText("Max Targets (0 = All)")]
        [MinValue(0)]
        public int MaxTargets = 0;

        [LabelText("Vars Policy")]
        public VarsPolicy VarsPolicy = VarsPolicy.Inherit;

        [LabelText("Await Mode")]
        public FlowRunAwaitMode AwaitMode = FlowRunAwaitMode.WaitForCompletion;

        [LabelText("Body")]
        [CommandListFunctionName("TargetChannel.WithTargets")]
        public CommandListData Body = new();

        public string NormalizedChannelTag => string.IsNullOrWhiteSpace(ChannelTag) ? "default" : ChannelTag.Trim();
    }
}
