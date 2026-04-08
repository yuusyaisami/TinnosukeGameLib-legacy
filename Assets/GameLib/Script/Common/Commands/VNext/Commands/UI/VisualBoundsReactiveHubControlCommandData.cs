#nullable enable
using System;
using Game.Common;
using Game.UI;
using Sirenix.OdinInspector;

namespace Game.Commands.VNext
{
    public enum VisualBoundsReactiveHubControlOperation
    {
        RegisterOrReplace = 10,
        Unregister = 20,
        ClearAll = 30,
        ResetRuntimeOverrides = 40,
        ResetAllRuntimeOverrides = 50,
    }

    [Serializable]
    public sealed class VisualBoundsReactiveHubControlCommandData : ICommandData
    {
        public int CommandId => CommandIds.VisualBoundsReactiveHubControl;

        public string DebugData
        {
            get
            {
                if (UsesChannelTag())
                    return $"Operation={Operation} Tag={NormalizedChannelTag}";

                return $"Operation={Operation}";
            }
        }

        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(Target)")]
        public ActorSource Target;

        [LabelText("Operation")]
        public VisualBoundsReactiveHubControlOperation Operation = VisualBoundsReactiveHubControlOperation.RegisterOrReplace;

        [ShowIf(nameof(UsesChannelTag))]
        [LabelText("Channel Tag")]
        public string ChannelTag = "default";

        [ShowIf(nameof(UsesPreset))]
        [LabelText("Preset")]
        [InlineProperty]
        public DynamicValue<VisualBoundsReactiveChannelPreset> Preset =
            DynamicValue<VisualBoundsReactiveChannelPreset>.FromSource(
                new ManagedRefLiteralSource<VisualBoundsReactiveChannelPreset>(new VisualBoundsReactiveChannelPreset()));

        public string NormalizedChannelTag => VisualBoundsReactiveTagUtility.Normalize(ChannelTag);

        bool UsesChannelTag()
        {
            return Operation == VisualBoundsReactiveHubControlOperation.RegisterOrReplace ||
                   Operation == VisualBoundsReactiveHubControlOperation.Unregister ||
                   Operation == VisualBoundsReactiveHubControlOperation.ResetRuntimeOverrides;
        }

        bool UsesPreset()
        {
            return Operation == VisualBoundsReactiveHubControlOperation.RegisterOrReplace;
        }
    }
}