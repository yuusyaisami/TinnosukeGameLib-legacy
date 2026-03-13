#nullable enable
using System;
using Game.Common;
using Game.Visual;
using Sirenix.OdinInspector;

namespace Game.Commands.VNext
{
    [Serializable]
    public struct VisualSelectorSpec
    {
        [EnumToggleButtons]
        [LabelText("Selector")]
        public VisualTargetSelectorKind Kind;

        [ShowIf("@Kind == VisualTargetSelectorKind.ByKind || Kind == VisualTargetSelectorKind.ByKindAndTag")]
        [LabelText("Hub Kind")]
        public VisualHubKind HubKind;

        [ShowIf("@Kind == VisualTargetSelectorKind.ByTag || Kind == VisualTargetSelectorKind.ByKindAndTag")]
        [LabelText("Tag")]
        public string Tag;

        public VisualTargetSelector ToSelector()
        {
            return Kind switch
            {
                VisualTargetSelectorKind.All => VisualTargetSelector.All(),
                VisualTargetSelectorKind.ByKind => VisualTargetSelector.ByKind(HubKind),
                VisualTargetSelectorKind.ByTag => VisualTargetSelector.ByTag(Tag),
                VisualTargetSelectorKind.ByKindAndTag => VisualTargetSelector.ByKindAndTag(HubKind, Tag),
                _ => VisualTargetSelector.All(),
            };
        }

        public string GetDebugLabel()
        {
            var tag = string.IsNullOrEmpty(Tag) ? "<none>" : Tag;
            return Kind switch
            {
                VisualTargetSelectorKind.All => "All",
                VisualTargetSelectorKind.ByKind => $"Kind={HubKind}",
                VisualTargetSelectorKind.ByTag => $"Tag={tag}",
                VisualTargetSelectorKind.ByKindAndTag => $"Kind={HubKind} Tag={tag}",
                _ => "All",
            };
        }

        public static VisualSelectorSpec All()
        {
            return new VisualSelectorSpec
            {
                Kind = VisualTargetSelectorKind.All,
                HubKind = default,
                Tag = string.Empty,
            };
        }
    }

    [Serializable]
    public sealed class VisualSetStateCommandData : ICommandData
    {
        public int CommandId => CommandIds.VisualSetState;
        public string DebugData => $"Target={Selector.GetDebugLabel()}";

        [LabelText("Target")]
        public VisualSelectorSpec Selector = VisualSelectorSpec.All();

        [LabelText("Payload")]
        public DynamicValue<MaterialFxPayload> MaterialFxSource;

        [LabelText("Clear Missing Keys")]
        public bool ClearMissingKeys = true;

        [LabelText("Base Priority")]
        public int BasePriority = 0;

        [LabelText("Allow Empty (Clear)")]
        public bool AllowEmpty = true;
    }

    [Serializable]
    public sealed class VisualBroadcastCommandData : ICommandData
    {
        public int CommandId => CommandIds.VisualBroadcast;
        public string DebugData => $"Target={Selector.GetDebugLabel()}";

        [LabelText("Target")]
        public VisualSelectorSpec Selector = VisualSelectorSpec.All();


        [LabelText("Payload")]
        public DynamicValue<MaterialFxPayload> MaterialFxSource;

        [LabelText("Base Priority")]
        public int BasePriority = 0;
    }
}
