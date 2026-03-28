#nullable enable
using System;
using Game.Common;
using Game.UI;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    [Serializable]
    public sealed class SliderControlCommandData : ICommandData
    {
        public int CommandId => CommandIds.SliderControl;

        public string DebugData
        {
            get
            {
                var targetLabel = ActorSourceOdinLabelHelper.GetLabel("Target", Target);
                return $"{targetLabel} Tag={NormalizedChannelTag} Op={Operation} SwapV={ApplyVisualizerPreset} SwapP={ApplyPlayerPreset} MutV={ApplyVisualizerMutation} MutP={ApplyPlayerMutation} ResetV={ResetVisualizer} ResetP={ResetPlayer}";
            }
        }

        [BoxGroup("Target")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(Target)")]
        public ActorSource Target;

        [BoxGroup("Target")]
        [LabelText("Channel Tag")]
        public string ChannelTag = "default";

        [BoxGroup("Operation")]
        [LabelText("Operation")]
        public SliderControlOperation Operation = SliderControlOperation.MutateSettings;

        [BoxGroup("Swap")]
        [ShowIf(nameof(IsSwapOperation))]
        [ToggleLeft]
        [LabelText("Apply Visualizer Preset")]
        public bool ApplyVisualizerPreset;

        [BoxGroup("Swap")]
        [ShowIf(nameof(ShouldShowVisualizerPreset))]
        [LabelText("Visualizer Preset")]
        public DynamicValue<SliderVisualizerPreset> VisualizerPreset =
            DynamicValue<SliderVisualizerPreset>.FromSource(
                new ManagedRefLiteralSource<SliderVisualizerPreset>(new SliderVisualizerPreset()));

        [BoxGroup("Swap")]
        [ShowIf(nameof(IsSwapOperation))]
        [ToggleLeft]
        [LabelText("Apply Player Preset")]
        public bool ApplyPlayerPreset;

        [BoxGroup("Swap")]
        [ShowIf(nameof(ShouldShowPlayerPreset))]
        [LabelText("Player Preset")]
        public DynamicValue<SliderPlayerPreset> PlayerPreset =
            DynamicValue<SliderPlayerPreset>.FromSource(
                new ManagedRefLiteralSource<SliderPlayerPreset>(new SliderPlayerPreset()));

        [BoxGroup("Mutate")]
        [ShowIf(nameof(IsMutateOperation))]
        [ToggleLeft]
        [LabelText("Apply Visualizer Mutation")]
        public bool ApplyVisualizerMutation;

        [BoxGroup("Mutate")]
        [ShowIf(nameof(ShouldShowVisualizerMutation))]
        [InlineProperty]
        [HideLabel]
        public SliderVisualizerRuntimeMutation VisualizerMutation = new();

        [BoxGroup("Mutate")]
        [ShowIf(nameof(IsMutateOperation))]
        [ToggleLeft]
        [LabelText("Apply Player Mutation")]
        public bool ApplyPlayerMutation;

        [BoxGroup("Mutate")]
        [ShowIf(nameof(ShouldShowPlayerMutation))]
        [InlineProperty]
        [HideLabel]
        public SliderPlayerRuntimeMutation PlayerMutation = new();

        [BoxGroup("Reset")]
        [ShowIf(nameof(IsResetOperation))]
        [ToggleLeft]
        [LabelText("Reset Visualizer")]
        public bool ResetVisualizer = true;

        [BoxGroup("Reset")]
        [ShowIf(nameof(IsResetOperation))]
        [ToggleLeft]
        [LabelText("Reset Player")]
        public bool ResetPlayer = true;

        public string NormalizedChannelTag => string.IsNullOrWhiteSpace(ChannelTag) ? "default" : ChannelTag.Trim();

        bool IsSwapOperation() => Operation == SliderControlOperation.SwapPreset;
        bool IsMutateOperation() => Operation == SliderControlOperation.MutateSettings;
        bool IsResetOperation() => Operation == SliderControlOperation.ResetRuntimeOverrides;
        bool ShouldShowVisualizerPreset() => IsSwapOperation() && ApplyVisualizerPreset;
        bool ShouldShowPlayerPreset() => IsSwapOperation() && ApplyPlayerPreset;
        bool ShouldShowVisualizerMutation() => IsMutateOperation() && ApplyVisualizerMutation;
        bool ShouldShowPlayerMutation() => IsMutateOperation() && ApplyPlayerMutation;
    }
}
